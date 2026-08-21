using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Economy;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Fires automatically the moment Play mode starts, on whatever scene happens to be open -
    /// no hand-authored .unity scene file needed. Builds the whole first-playable prototype
    /// from code (Canvas, EventSystem, CardDatabase, BattleController, and every UI panel).
    ///
    /// Reconstructed 2026-08-05 after a local Assets-folder loss wiped out the version of this
    /// file that had the layout/interaction fixes below - the Drive backup only had the
    /// original pre-fix version (this file was never synced to Drive mid-development). Rebuilt
    /// from the documented history of each fix rather than from the lost source directly.
    /// </summary>
    public static class GameBootstrapLoader
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("GameBootstrap");
            go.AddComponent<GameBootstrap>();
        }
    }

    /// <summary>
    /// Chapter 1's guided tutorial sequence - a strict, one-action-at-a-time script layered on
    /// top of the ordinary Formation/Combat flow (BattlePhase), replacing the old "place all
    /// three starters anywhere" gate. Battle-owned, UI-flow state only: it never changes a rule,
    /// a card, a reward, or a save - it only decides which of the *already-legal* existing
    /// actions the player is currently allowed to take, and advances itself once that one action
    /// actually succeeds. Every step's exact allowance is centralized in
    /// GameBootstrap.TutorialAllowedCardId/TutorialAllowedLane/RefreshTutorialStepControls -
    /// nowhere else invents its own gating rule.
    /// </summary>
    public enum TutorialStep
    {
        /// <summary>1. Only the approved first starter card (warrior) is tappable; no lane, no
        /// other card, nothing else. Advances once it is selected.</summary>
        CardCost,

        /// <summary>2. The hand is fully locked (the selection from step 1 already stands); only
        /// the Front lane is tappable. Advances once warrior is actually placed there.</summary>
        FrontLane,

        /// <summary>3. Only novice_knight (hand) and Middle (lane) are tappable. Advances once
        /// novice_knight is placed in Middle.</summary>
        MiddleLane,

        /// <summary>4. Only goblin_caster (hand) and Back (lane) are tappable. Advances once
        /// goblin_caster is placed in Back.</summary>
        BackLane,

        /// <summary>5. Formation is complete; only Start Battle is tappable. Advances once
        /// Combat actually begins.</summary>
        BeginBattle,

        /// <summary>6. The first combat tick has already resolved (synchronously, the instant
        /// Combat began - see OnPrimaryActionPressed); its numbers are shown and only the
        /// tutorial's own Continue control is tappable. Advances on Continue.</summary>
        FirstCombatResult,

        /// <summary>7. Exactly one spell (Firestorm) is unlocked/highlighted; once tapped,
        /// exactly one enemy lane (Middle - the sole survivor of the first exchange) is a legal
        /// target. Advances once that cast actually resolves.</summary>
        SpellLesson,

        /// <summary>8. Only Continue is tappable; each tap advances one more real combat tick.
        /// The scripted encounter is tuned so this reaches a real, engine-resolved victory
        /// within a couple of taps - see StartApprovedTutorialBattle's own numbers.</summary>
        Finish,
    }

    public class GameBootstrap : MonoBehaviour
    {
        // V3 landscape replacement, 2026-08-16 (Battle_Screen_V3_VSCode_Implementation_Handoff.md):
        // reference resolution is now 1920x1080 landscape, locked - this game's canvas was
        // portrait-shaped (720x1280) before this. Every anchor below is a direct min/max fraction
        // of this canvas, taken straight from the handoff's own anchor table rather than derived,
        // so this file's geometry can be checked against that table line by line.
        private const float CanvasWidth = 1920f;
        private const float CanvasHeight = 1080f;

        // Battle Screen Production V4 anchor table, verbatim from
        // Battle_Screen_Production_V4_Implementation_Handoff.md's own region table (normalized
        // (left, bottom, right, top)):
        //   Top HUD:               (.015, .885) - (.985, .995)
        //   Player HUD:            (.020, .895) - (.300, .985)
        //   Phase HUD:             (.360, .895) - (.640, .985)
        //   Enemy HUD:             (.700, .895) - (.980, .985)
        //   Lane labels:           (.015, .225) - (.165, .875)
        //   Enemy 3x3 board:       (.170, .565) - (.715, .875)
        //   Player 3x3 board:      (.170, .225) - (.715, .535)
        //   Lane totals/overflow:  (.715, .225) - (.755, .875)
        //   Combat activity rail:  (.760, .520) - (.985, .875)
        //   Spell/action rail:     (.760, .225) - (.985, .500)
        //   Hand dock:             (.015, .025) - (.730, .195)
        //   Primary action:        (.745, .025) - (.985, .195)
        private static readonly Vector2 TopHudMin = new Vector2(0.015f, 0.885f);
        private static readonly Vector2 TopHudMax = new Vector2(0.985f, 0.995f);
        private static readonly Vector2 PlayerHudMin = new Vector2(0.020f, 0.895f);
        private static readonly Vector2 PlayerHudMax = new Vector2(0.300f, 0.985f);
        private static readonly Vector2 PhaseHudMin = new Vector2(0.360f, 0.895f);
        private static readonly Vector2 PhaseHudMax = new Vector2(0.640f, 0.985f);
        private static readonly Vector2 EnemyHudMin = new Vector2(0.700f, 0.895f);
        private static readonly Vector2 EnemyHudMax = new Vector2(0.980f, 0.985f);
        private static readonly Vector2 LaneLabelsMin = new Vector2(0.015f, 0.225f);
        private static readonly Vector2 LaneLabelsMax = new Vector2(0.165f, 0.875f);
        private static readonly Vector2 EnemyBoardMin = new Vector2(0.170f, 0.565f);
        private static readonly Vector2 EnemyBoardMax = new Vector2(0.715f, 0.875f);
        private static readonly Vector2 PlayerBoardMin = new Vector2(0.170f, 0.225f);
        private static readonly Vector2 PlayerBoardMax = new Vector2(0.715f, 0.535f);
        private static readonly Vector2 LaneTotalsMin = new Vector2(0.715f, 0.225f);
        private static readonly Vector2 LaneTotalsMax = new Vector2(0.755f, 0.875f);
        private static readonly Vector2 ActivityRailMin = new Vector2(0.760f, 0.520f);
        private static readonly Vector2 ActivityRailMax = new Vector2(0.985f, 0.875f);
        private static readonly Vector2 SpellRailMin = new Vector2(0.760f, 0.225f);
        private static readonly Vector2 SpellRailMax = new Vector2(0.985f, 0.500f);
        private static readonly Vector2 HandPanelMin = new Vector2(0.015f, 0.025f);
        private static readonly Vector2 HandPanelMax = new Vector2(0.730f, 0.195f);
        private static readonly Vector2 PrimaryActionMin = new Vector2(0.745f, 0.025f);
        private static readonly Vector2 PrimaryActionMax = new Vector2(0.985f, 0.195f);

        // Thin gap band between the Top HUD's own bottom edge (.885) and the Enemy board's top
        // edge (.875) - the only non-HUD, non-board sliver anywhere near the top of the V4
        // layout, reused for the approved tutorial-only guidance caption (see
        // BuildTutorialGuidanceCaption). V4's own region table has no dedicated slot for this
        // (it is tutorial-only, not part of the always-on HUD/board/rail/dock regions), and it
        // must never render into the Top HUD region itself per the handoff's collision table
        // ("Header controls... reserve HUD regions; no board, guide, tooltip, or combat text may
        // render into them") - non-raycasting and effectively invisible outside a guided step.
        private const float TitleY0 = 0.876f, TitleY1 = 0.884f;

        // Battle Screen Production V4 board slot geometry, verbatim from the handoff's own
        // "Board geometry" section: "Each board row has three equal slots. A slot is 0.165
        // screen width by 0.088 screen height before internal card padding." At 1920x1080 that
        // is 316.8 x 95.04px - rounded to whole pixels. Row *positioning* (both boards share the
        // same 0.310 span - EnemyBoardMax.y - EnemyBoardMin.y equals PlayerBoardMax.y -
        // PlayerBoardMin.y) is computed directly from anchor fractions in BuildBattleBoardSide,
        // not from a pixel row-height constant, so it can never drift out of sync with the
        // region table above.
        private const float BoardSlotWidth = 316f;
        private const float BoardSlotHeight = 95f;
        private const float BoardSlotSpacing = 14f;

        // Visual-review fix, 2026-08-17, corrected 2026-08-18: BoardSlotWidth/Height (316x95) is
        // a wide, short CELL - correct for the row's tap-target/spacing math, wrong as the shape
        // to render a card frame at (stretching a portrait frame to fill it read as "flattened/
        // stretched"). The rendered card tile (CreateBoardCardTile) is instead sized to the
        // cell's own height at each card's OWN rarity-frame aspect (GetRarityFrameAspect) -
        // asset audit 2026-08-18 found Common/Rare/Epic (0.739 w/h) and Legendary (0.870) card
        // frames are genuinely different shapes, so a single shared aspect was itself part of
        // the stretching defect, not just the wide cell.

        // Palette widened 2026-08-05 from a near-uniform dark-purple-on-everything look (flat
        // single color per panel, no depth) to distinct tinted gradients per section - Enemy
        // reads warm/hostile (red-brown), Player reads cool/allied (teal-blue), matching the
        // red/blue Avatar HP convention from the reference images without needing new art:
        // every panel/button background below is a top-to-bottom gradient (CreateGradientSprite),
        // not a flat Image.color, which is what actually reads as "designed" instead of "boxes."
        private static readonly Color BackgroundTop = new Color(0.09f, 0.07f, 0.13f);
        private static readonly Color BackgroundBottom = new Color(0.05f, 0.04f, 0.08f);
        private static readonly Color PanelColor = new Color(0.18f, 0.14f, 0.24f); // still used for small chrome (mini-cards, badges)
        private static readonly Color EnemyPanelTop = new Color(0.28f, 0.1f, 0.13f);
        private static readonly Color EnemyPanelBottom = new Color(0.16f, 0.06f, 0.09f);
        private static readonly Color PlayerPanelTop = new Color(0.1f, 0.19f, 0.24f);
        private static readonly Color PlayerPanelBottom = new Color(0.06f, 0.12f, 0.16f);
        private static readonly Color HandPanelTop = new Color(0.16f, 0.13f, 0.22f);
        private static readonly Color HandPanelBottom = new Color(0.1f, 0.08f, 0.15f);

        // How see-through the main HUD bands (Enemy/Player/Hand) are over the arena backdrop -
        // 2026-08-05, direct request: the backdrop art was invisible behind near-opaque panels.
        // Modal popups (card detail, match result) deliberately do NOT use this - they sit over
        // an extra 0.75-alpha dim layer and need to stay fully readable as the focused element.
        // 2026-08-06: dropped to 0 ("the orange area shows the opaque shaded area - remove
        // them"). The tinted bands were the last thing sitting between the player and the arena
        // art, and every element that actually needs contrast now carries its own dark plate
        // (stat chips, name plates, the card-detail stats panel), so the bands were only
        // covering the backdrop. CreateGradientBandPanel skips drawing entirely at 0 rather
        // than adding an invisible full-screen Graphic for the canvas to batch.
        private const float HudPanelAlpha = 0f;

        // V3 replacement, 2026-08-16: the handoff's own visual system explicitly calls for
        // "charcoal/navy stone" solid panels ("dark fantasy" section of the handoff), reversing
        // the 2026-08-06 decision above for this game's OLDER layout (which removed all panel
        // backgrounds so backdrop art could show through, relying on per-element dark chips for
        // contrast instead). Both are real, deliberate decisions for their own layout - V3's own
        // regions use this constant, not HudPanelAlpha, so the two eras' intent stay distinct in
        // the code rather than one silently overwriting the other's reasoning.
        private const float V3PanelAlpha = 0.90f;
        private static Color WithAlpha(Color c, float a) => new Color(c.r, c.g, c.b, a);
        private static readonly Color AccentBorderColor = new Color(0.85f, 0.72f, 0.4f, 0.5f);
        private static readonly Color GoldTextColor = new Color(0.9f, 0.78f, 0.45f);
        private static readonly Color SelectedColor = new Color(1.0f, 0.85f, 0.4f);
        private static readonly Color ButtonNormalTop = new Color(0.62f, 0.14f, 0.16f);
        private static readonly Color ButtonNormalBottom = new Color(0.38f, 0.06f, 0.08f);
        private static readonly Color ButtonDisabledColor = new Color(0.25f, 0.22f, 0.28f);
        private static readonly Color ButtonTextNormalColor = new Color(0.97f, 0.89f, 0.68f);
        private static readonly Color ButtonTextSelectedColor = new Color(0.15f, 0.1f, 0.05f);
        private static readonly Color ButtonTextDisabledColor = new Color(0.55f, 0.5f, 0.5f);
        private static readonly Color HealthBarFillColor = new Color(0.8f, 0.2f, 0.18f);
        private static readonly Color HealthBarEmptyColor = new Color(0.18f, 0.07f, 0.08f);
        private static readonly Color PlayerHealthBarFillColor = new Color(0.2f, 0.65f, 0.7f);
        private static readonly Color ResourceBarFillColor = new Color(0.85f, 0.68f, 0.25f);
        private static readonly Color ResourceBarEmptyColor = new Color(0.14f, 0.14f, 0.1f);

        /// <summary>Seconds between automated lane clashes once formation is locked. Slow enough
        /// to read what happened each tick, fast enough that a match doesn't drag.</summary>
        private const float CombatTickSeconds = 2.2f;

        private Coroutine _combatLoop;
        private Transform _canvasTransform;
        private const string SeenIntroPrefKey = "MOD_SeenIntro";
        private GameObject _tutorialOverlay;
        private Image _narrativePortrait;
        private Text _narrativeSpeaker;
        private Text _narrativeText;
        private Text _narrativeProgress;
        private Image _narrativeBackground;
        private Image _tutorialArrow;
        private Coroutine _typewriter;
        private Coroutine _backgroundFade;
        private Coroutine _arrowBounce;

        /// <summary>One line of the intro sequence - a story beat or a tutorial step, since both
        /// play through the same overlay.</summary>
        private readonly struct NarrativeBeat
        {
            public readonly string Speaker;
            public readonly string Portrait;
            public readonly string Text;
            public readonly string Background;
            public readonly string Highlight;

            public NarrativeBeat(string speaker, string portrait, string text, string background, string highlight)
            {
                Speaker = speaker;
                Portrait = portrait;
                Text = text;
                Background = background;
                Highlight = highlight;
            }
        }

        private readonly List<NarrativeBeat> _narrativeQueue = new List<NarrativeBeat>();
        private int _narrativeIndex;

        private GameObject _lanePickerOverlay;
        private Text _lanePickerTitle;
        private Text _lanePickerAvailableCaption;
        private RectTransform _lanePickerDeployedRow;
        private RectTransform _lanePickerAvailableRow;
        private Lane _pickerLane;
        private Text _synergyText;
        private Button _primaryActionButton;
        private Text _primaryActionLabel;
        private readonly List<Button> _spellButtons = new List<Button>();
        private readonly List<Text> _spellLabels = new List<Text>();
        private readonly List<Image> _spellIcons = new List<Image>();
        private RectTransform _spellBar;

        /// <summary>Spell affordability hint (2026-08-22): the SpellRail's existing "SPELLS"
        /// heading - reused, not a new element - so a player can tell at a glance that something
        /// is castable without reading every button. See RefreshPhaseControls for the actual
        /// affordability check (SpellAffordability.AnyCastable).</summary>
        private Text _spellsTitleText;

        /// <summary>V3's activity rail shows each spell's real Name (handoff: "spell buttons
        /// with numeric energy cost/cooldown"), not just an icon - the pre-V3 64px icon tile had
        /// no room for one. Set once per spell in RefreshPhaseControls, alongside the existing
        /// cost/cooldown label.</summary>
        private readonly List<Text> _spellNameLabels = new List<Text>();

        /// <summary>Activity rail's own log of recent resolved ticks - see RefreshActivityLog.</summary>
        private Text _activityLogText;

        /// <summary>Spellbook index of the spell currently awaiting a lane tap, or -1 when no
        /// spell is armed. AvatarStrike spells never set this - they cast immediately on tap
        /// (see RequiresLaneTargeting) since there is no lane for them to target.</summary>
        private int _armedSpellIndex = -1;
        private readonly Dictionary<Lane, Button> _enemyLaneButtons = new Dictionary<Lane, Button>();
        private GameObject _spellTargetCancelCatcher;
        private GameObject _spellTooltip;
        private Text _spellTooltipText;

        private CardDatabase _cardDatabase;
        private BattleController _battleController;
        private PlayerProfile _profile;
        private PlayerEmpireData _empireData;

        /// <summary>The loaded player profile. Exposed so tests (and a future main menu) can read
        /// progression without reaching through the UI.</summary>
        public PlayerProfile Profile => _profile;
        private SoloAIScalingSystem _aiScaling;
        private AIBattleProfile _aiProfile;

        private AudioSource _musicSource;
        private AudioClip _battleMusicClip;

        private Card _selectedCard;
        private Card _previewedCard;

        private readonly Dictionary<Lane, Transform> _enemyLaneSlots = new Dictionary<Lane, Transform>();
        private readonly Dictionary<Lane, Transform> _playerLaneSlots = new Dictionary<Lane, Transform>();
        private readonly Dictionary<Lane, Button> _playerLaneButtons = new Dictionary<Lane, Button>();
        private readonly Dictionary<Lane, Text> _enemyLaneTotalTexts = new Dictionary<Lane, Text>();
        private readonly Dictionary<Lane, Text> _playerLaneTotalTexts = new Dictionary<Lane, Text>();
        private Image _enemyHealthFill;
        private Text _enemyAvatarText;
        private Image _playerHealthFill;
        private Text _playerAvatarText;

        /// <summary>Enemy portrait's name label, captured so RefreshAll can keep it in step with
        /// _aiProfile - unlike the player's fixed "Lightbringer", the AI opponent is re-derived
        /// every match (see SoloAIScalingSystem.GenerateAIOpponent) and used to change without a
        /// full Initialize(), so a name set once at construction would go stale after "Play
        /// Again" faced a different opponent.</summary>
        private Text _enemyNameLabel;
        private Image _resourceFill;
        private Text _resourceText;

        /// <summary>V3 header adds a visible enemy Resource readout beside the enemy HP bar
        /// (handoff's required bindings: "Formation resource... equivalent EnemyState values") -
        /// the pre-V3 layout never displayed this at all; EnemyState.Resource already existed
        /// and updates the same way PlayerState.Resource does (BattleController.BeginTurn calls
        /// GainResourceForTurn on both sides), only nothing read it into the UI before now.</summary>
        private Image _enemyResourceFill;
        private Text _enemyResourceText;
        private Text _turnText;
        private Text _deckCountText;

        /// <summary>V3 header's "HAND n" readout beside the player portrait - existing data
        /// (HandCardCount/PlayerState.Hand.Count), just not previously surfaced as its own label.</summary>
        private Text _handCountText;
        private Transform _handRow;
        private Text _handHintText;

        /// <summary>The Hand/placement dock's own outer RectTransform - captured for the
        /// corrective geometry regression tests (never overlaps a Player lane button's bounds).</summary>
        private RectTransform _handAndPlacementPanelRect;

        /// <summary>Battle Release Layout pass: the single presentation root every Battle child
        /// (backdrop, header, both boards, right rail, hand dock, action well, and every modal
        /// overlay) is built under - see Initialize()'s own comment for why.</summary>
        private RectTransform _battlePresentationRoot;

        /// <summary>Exposed for tests: the Hand dock's real (post-layout) RectTransform.</summary>
        public RectTransform HandDockRectForTests => _handAndPlacementPanelRect;

        /// <summary>Exposed for tests: a Player lane's own tap-target RectTransform (the lane
        /// row Button built by CreateBoardRow, stored in _playerLaneButtons).</summary>
        public RectTransform PlayerLaneButtonRectForTests(Lane lane) =>
            _playerLaneButtons.TryGetValue(lane, out Button button) ? button.GetComponent<RectTransform>() : null;

        /// <summary>Exposed for tests: an Enemy lane's own tap-target RectTransform - the same
        /// pattern as PlayerLaneButtonRectForTests, for the Battle Release Layout pass' region
        /// geometry regression coverage (the enemy board is the one named region with no
        /// otherwise-findable-by-name wrapping panel).</summary>
        public RectTransform EnemyLaneButtonRectForTests(Lane lane) =>
            _enemyLaneButtons.TryGetValue(lane, out Button button) ? button.GetComponent<RectTransform>() : null;

        /// <summary>Exposed for tests: the single BattlePresentationRoot every Battle child is
        /// built under (Battle Release Layout pass - see Initialize()'s own comment).</summary>
        public RectTransform BattlePresentationRootForTests => _battlePresentationRoot;

        /// <summary>Exposed for tests: the Hand dock's own decorative background Image, to check
        /// its raycastTarget setting directly.</summary>
        public Image HandDockBackgroundImageForTests { get; private set; }

        /// <summary>V3's passive "Selected Card / Place In" status box - text-only, never itself
        /// a placement control (see BuildHandAndPlacementPanel's own comment on why).</summary>
        private Text _selectedCardText;
        private readonly List<Button> _handButtons = new List<Button>();

        private GameObject _cardDetailOverlay;
        private Text _cardDetailClassTag;
        private Image _cardDetailArt;
        private Image _cardDetailArtFrame;
        private Text _cardDetailName;
        private Text _cardDetailStats;
        private Text _cardDetailSkill;
        private Button _cardDetailActionButton;
        private Text _cardDetailActionLabel;

        private GameObject _resultOverlay;
        private Text _resultText;
        private Button _playAgainButton;
        private Text _playAgainLabel;
        private Button _returnToCityButton;
        private Text _returnToCityLabel;
        private Text _tutorialGuidanceCaption;
        private Button _resetLineupButton;
        private Button _recommendedLineupButton;

        /// <summary>Current step of the guided Chapter 1 sequence, or null for a normal match
        /// (which never constructs one) - see the TutorialStep enum's own doc comment.</summary>
        private TutorialStep? _tutorialStep;

        /// <summary>Set once the step-7 spell lesson actually resolves, so step 8's caption can
        /// keep reporting the exact cast numbers (Energy spent, enemy Health change) rather than
        /// losing them the moment the step advances.</summary>
        private string _tutorialSpellCastSummary;

        /// <summary>Set once Warrior is actually placed in Front, so step 3's caption can open
        /// with "Warrior now has N ATK" - the resulting-number confirmation for the step that
        /// just completed, carried into the next step's own instruction rather than needing a
        /// separate no-action confirmation screen.</summary>
        private string _tutorialFrontPlacementSummary;

        private Button _tutorialContinueButton;
        private Text _tutorialContinueLabel;
        private string _normalMatchStartError;

        // ---------- Tutorial teaching overlay (full-screen blocker + Tutorial Action Proxy) ----------
        // Built last in Initialize() so its sibling index is the highest in the canvas.
        //
        // REPLACES two earlier designs, both rejected against real manual QA:
        //  1. A "spotlight hole" (four dark rectangles framing a measured gap) - rejected
        //     2026-08-16: the hole's coordinate math depended on precise agreement between the
        //     target's measured world bounds and the scrim's own, which drifted in practice.
        //  2. Reparenting the real target GameObject into an always-topmost slot - rejected
        //     2026-08-16 again: a real manual QA pass found the highlighted Novice Knight visible
        //     but still not tappable.
        //
        // This design never touches the real gameplay hierarchy at all. Every real hand card,
        // lane button, spell tile and Continue button stays in its original parent, sibling
        // order and layout, exactly as normal (non-tutorial) play already relies on - see
        // RefreshTutorialActionProxy's own comment for why this specifically is what makes taps
        // reliable: a full-screen blocker sits above normal gameplay; a small, transparent
        // "Tutorial Action Proxy" Button - sized to the real target's own current world bounds,
        // rebuilt fresh every refresh - sits above the blocker and is the only thing that ever
        // receives the tap, forwarding it straight into the exact same private handler
        // (OnHandCardPressed/OnLanePressed/OnSpellTapped/OnSpellTargetLanePressed/
        // OnPrimaryActionPressed/OnTutorialContinuePressed) a real tap on the real control would
        // have called. A purely decorative, non-raycasting marker (border + arrow) sits around it
        // so the target still reads as clearly highlighted.
        private GameObject _tutorialTeachingOverlay;
        private Image _tutorialFullScreenBlocker;
        private RectTransform _tutorialProxyContainer;
        private Button _tutorialActionProxy;
        private RectTransform _tutorialMarkerTop;
        private RectTransform _tutorialMarkerBottom;
        private RectTransform _tutorialMarkerLeft;
        private RectTransform _tutorialMarkerRight;
        private RectTransform _tutorialMarkerArrow;
        private RectTransform _tutorialGuidePanelRect;
        private Text _tutorialGuideSpeakerText;
        private Text _tutorialGuideBodyText;
        private Button _tutorialSkipButton;

        /// <summary>Exposed for tests: which guided step is active, or null outside the tutorial.</summary>
        public TutorialStep? TutorialStepForTests => _tutorialStep;

        /// <summary>Number of cards currently rendered in the hand row - exposed for tests.</summary>
        public int HandCardCount => _handButtons.Count;

        /// <summary>Exposed for tests, which can't rely on Awake() firing outside Play Mode.</summary>
        public BattleController Battle => _battleController;

        /// <summary>Exposed for tests: the AI archetype actually driving the current match's
        /// enemy deployment (SimpleAIOpponent.TakeTurn's lane choice) - lets a test prove the
        /// tutorial's own fixed Balanced profile is in effect, independent of whatever a prior
        /// normal match last generated.</summary>
        public AIArchetype AiArchetypeForTests => _aiProfile.Archetype;

        /// <summary>Exposed for tests: whether the battle music AudioSource is currently
        /// playing the battle track - lets a test prove the start/stop/no-duplicate rules
        /// without needing a real audio device.</summary>
        public bool BattleMusicIsPlayingForTests => _musicSource != null && _musicSource.isPlaying;

        /// <summary>Exposed for tests: how many AudioSources exist under the battle canvas -
        /// proves Retry/Reset/Recommended never create a second, layered music source.</summary>
        public int BattleMusicSourceCountForTests =>
            _canvasTransform == null ? 0 : _canvasTransform.GetComponentsInChildren<AudioSource>(true).Length;

        /// <summary>
        /// The running battle screen, for external code that needs to show/hide it - the home
        /// screen's own handoff, specifically (2026-08-06). Not a CardDatabase-style guarded
        /// singleton (no "destroy the second instance" logic) - EditMode tests construct
        /// GameBootstrap directly and repeatedly, and each one only ever cares about its own
        /// most-recently-initialized instance, the same way the existing tests already work.
        /// </summary>
        public static GameBootstrap Instance { get; private set; }

        /// <summary>
        /// True only for the offline, local-only tutorial encounter started by
        /// StartApprovedTutorialBattle() - lets HomePagePresenter.HandleMatchCompleted
        /// recognize and skip its reward grant for this specific match without this file
        /// needing to know anything about gold, gems, or save rewards itself. Reset to false
        /// by StartNewMatch() (the generic path), so a tutorial run can never leak into the
        /// next normal match's reward eligibility.
        /// </summary>
        public bool IsTutorialMatch { get; private set; }

        /// <summary>Exposed for tests: read-only access to the result overlay's text/labels/
        /// button-visibility state, which tutorial guidance now branches by IsTutorialMatch -
        /// no other way for a test to observe this without a broader UI-inspection API.</summary>
        public string ResultTextForTests => _resultText != null ? _resultText.text : null;
        public string PlayAgainLabelForTests => _playAgainLabel != null ? _playAgainLabel.text : null;
        public string ReturnToCityLabelForTests => _returnToCityLabel != null ? _returnToCityLabel.text : null;
        public bool PlayAgainButtonActiveForTests => _playAgainButton != null && _playAgainButton.gameObject.activeSelf;
        public bool ReturnToCityButtonActiveForTests => _returnToCityButton != null && _returnToCityButton.gameObject.activeSelf;

        /// <summary>Exposed for tests: the Formation "Ready"/Combat objective caption's current
        /// visibility and text - see BuildTutorialGuidanceCaption's own comment.</summary>
        public bool TutorialGuidanceCaptionActiveForTests => _tutorialGuidanceCaption != null && _tutorialGuidanceCaption.gameObject.activeSelf;
        public string TutorialGuidanceCaptionTextForTests => _tutorialGuidanceCaption != null ? _tutorialGuidanceCaption.text : null;

        /// <summary>Exposed for tests: Reset/Recommended Lineup's current visibility - the
        /// readiness-audit fix hides both for the whole duration of any tutorial match (see
        /// RefreshPhaseControls' own comment).</summary>
        public bool ResetLineupButtonActiveForTests => _resetLineupButton != null && _resetLineupButton.gameObject.activeSelf;
        public bool RecommendedLineupButtonActiveForTests => _recommendedLineupButton != null && _recommendedLineupButton.gameObject.activeSelf;
        public string NormalMatchStatusForTests => _normalMatchStartError;

        /// <summary>Exposed for tests: the single primary-action button's current label/
        /// visibility/interactable state - it swaps between "AUTO FORMATION" and "START BATTLE"
        /// depending on real board state (see ShouldOfferAutoFormation), and is hidden entirely
        /// when no valid deck was confirmed (see RefreshPhaseControls' own normalDeckBlocked
        /// check).</summary>
        public string PrimaryActionLabelForTests => _primaryActionLabel != null ? _primaryActionLabel.text : null;
        public bool PrimaryActionButtonActiveForTests => _primaryActionButton != null && _primaryActionButton.gameObject.activeSelf;
        public bool PrimaryActionButtonInteractableForTests => _primaryActionButton != null && _primaryActionButton.interactable;

        /// <summary>Exposed for tests: the SpellRail heading's current text - "SPELLS" normally,
        /// or the affordability-hint copy while at least one spell is really castable in Combat.
        /// See RefreshPhaseControls.</summary>
        public string SpellRailTitleTextForTests => _spellsTitleText != null ? _spellsTitleText.text : null;

        /// <summary>Exposed for tests: the turn/clash text's current value - "Formation" outside
        /// Combat, "Clash N/12" during an ordinary Combat tick, or "REINFORCE! N/12" while
        /// BattleController.IsReinforcementWindowOpen is true (ticks 4 and 8). See RefreshAll,
        /// which already sets this unconditionally for every match type.</summary>
        public string TurnTextForTests => _turnText != null ? _turnText.text : null;

        /// <summary>Exposed for tests: opens the lane picker overlay exactly as tapping a
        /// player lane does (OpenLanePicker is private), then returns the resulting title text
        /// - the only way to observe RefreshLanePicker's tutorial-guidance append without a
        /// broader UI-inspection API.</summary>
        public string OpenLanePickerAndGetTitleForTests(Lane lane)
        {
            OpenLanePicker(lane);
            return _lanePickerTitle != null ? _lanePickerTitle.text : null;
        }

        /// <summary>
        /// Fires after OnReturnToCityPressed has hidden the battle canvas - the other half of
        /// the battle/metagame handoff OnReturnToCityPressed's own comment already flagged as
        /// missing (2026-08-06: "re-showing it is the other half of that same open question").
        /// Payload-free and generic on purpose: this file should not need to know HomePagePresenter
        /// exists, only that *something* wants to know when a return-to-city has happened, the
        /// same reasoning BattleController.OnMatchCompleted already follows for match results.
        /// Reward/progression are already fully resolved before this ever fires (HandleMatchEnded
        /// and HomePagePresenter.HandleMatchCompleted both run at match resolution, well before
        /// the player reaches this button) - this event carries no data and must never be given
        /// any reason to touch either.
        /// </summary>
        public event System.Action OnReturnToCityRequested;

        private void Awake() => Initialize();

        public void Initialize()
        {
            Instance = this;
            Font defaultFont = GetDefaultFont();

            Canvas canvas = BuildCanvas();
            _canvasTransform = canvas.transform;
            BuildEventSystem();

            // Battle Release Layout pass: one BattlePresentationRoot under Canvas owns every
            // Battle child from here down - built first (before even the music source and
            // StartNewMatch) so nothing Battle-owned is ever a direct sibling of it under Canvas.
            // A pure SetParent change for every Build* call below (StretchFull(root) makes the
            // root's own fraction space identical to the canvas's), so every existing
            // fractional/pixel anchor is completely unaffected - zero geometry change from this
            // alone. Consolidates what was previously a flat list of panels siblinged directly
            // under Canvas (with the backdrop as a further, separately-drawn sibling before all
            // of them - see BuildBattleBackdrop) into one owned hierarchy, matching the
            // six-region contract: header/HUD, enemy board, player board, right rail, hand dock,
            // action well, plus the backdrop, music source, and every modal overlay as the
            // root's own additional owned chrome.
            var battleRootGo = new GameObject("BattlePresentationRoot", typeof(RectTransform));
            battleRootGo.transform.SetParent(canvas.transform, false);
            RectTransform battleRoot = (RectTransform)battleRootGo.transform;
            StretchFull(battleRoot);
            _battlePresentationRoot = battleRoot;

            // Built before StartNewMatch() below, which is the first call that can start battle
            // music. A child of the battle presentation root specifically - see
            // OnReturnToCityPressed's own comment for why the canvas being hidden alone isn't
            // relied on to silence it (unaffected by which Battle-owned ancestor this sits under).
            var musicGo = new GameObject("BattleMusicSource");
            musicGo.transform.SetParent(battleRoot, false);
            _musicSource = musicGo.AddComponent<AudioSource>();
            _musicSource.playOnAwake = false;
            _musicSource.loop = true;
            _battleMusicClip = Resources.Load<AudioClip>("Audio/Music/Battle_Theme");
            _musicSource.clip = _battleMusicClip;

            _cardDatabase = CardDatabase.Instance;
            if (_cardDatabase == null)
            {
                var dbGo = new GameObject("CardDatabase");
                _cardDatabase = dbGo.AddComponent<CardDatabase>();
                _cardDatabase.Initialize();
            }

            var controllerGo = new GameObject("BattleController");
            _battleController = controllerGo.AddComponent<BattleController>();

            // Progression now loads from disk (Assets/Scripts/Save) instead of being a hardcoded
            // profile that reset on every launch. Levels earned by winning survive a restart, and
            // HandleMatchEnded writes them back.
            //
            // Reads the shared SaveSystem.CurrentProfile (2026-08-06), not its own private
            // PlayerProfile.LoadOrCreate() copy - the home screen and shop read/write the same
            // profile in the same session, and two independently-loaded copies could silently
            // overwrite each other's changes. See SaveSystem.CurrentProfile's own comment.
            //
            // EditMode tests construct GameBootstrap directly and must not read or write the
            // machine's real profile, so outside Play Mode this stays in memory only.
            _profile = SaveSystem.CurrentProfile;

            SeedIfNewProfile(_profile);

            // Unconditional and idempotent, not folded into SeedNewProfile's NewGame-only branch:
            // PlayerProfile's default LoadStatus is Success (not NewGame), so a profile built via
            // `new PlayerProfile()` directly - every EditMode test, and any other construction
            // path that doesn't go through SaveSystem.Load - never took the NewGame branch above
            // and its Empire was never derived from its levels at all, which for a fresh
            // PlayerEmpireData leaves DeckSlotCount at 0 and sizes every match's deck to zero cards.
            _profile.ApplyDataToEmpire();
            _empireData = _profile.Empire;

            // The enemy is no longer pinned at a fixed level-1 baseline. That pinning was itself
            // a fix for an earlier bug (the enemy used to mirror the player's economy exactly, so
            // levelling up never made a win easier to reach) but it overcorrected: the player
            // gains Avatar levels per win against an opponent that never changed, so the game
            // got monotonically *easier* the longer it was played. SoloAIScalingSystem now derives
            // the opponent from the player's own progression each match - see that class for what
            // it does and does not scale.
            _aiScaling = new SoloAIScalingSystem();

            StartNewMatch();
            _battleController.OnMatchEnded += HandleMatchEnded;

            BuildBattleBackdrop(battleRoot);

            // Built before any panel with a lane button - see BuildSpellTargetCancelCatcher's own
            // comment for why its position in this call order is load-bearing, not cosmetic.
            BuildSpellTargetCancelCatcher(battleRoot);

            // Title panel removed 2026-08-06 ("remove MOD"): a permanent game-title banner across
            // the top of the battle screen is menu chrome, not gameplay information, and it was
            // eating a band of screen the board could use. BuildTitlePanel is kept for a future
            // main menu rather than deleted. The TitleY0/Y1 band it used to occupy is what
            // BuildTutorialGuidanceCaption reuses below - already free, no board space taken.
            BuildTutorialGuidanceCaption(battleRoot, defaultFont);
            BuildHeaderBar(battleRoot, defaultFont);
            BuildBattleBoards(battleRoot, defaultFont);
            BuildActivityRail(battleRoot, defaultFont);
            BuildHandAndPlacementPanel(battleRoot, defaultFont);
            BuildPrimaryActionAndSpells(battleRoot, defaultFont);
            BuildCardDetailOverlay(battleRoot, defaultFont);
            BuildLanePickerOverlay(battleRoot, defaultFont);
            BuildResultOverlay(battleRoot, defaultFont);
            BuildTutorialOverlay(battleRoot, defaultFont);
            BuildSpellTooltip(battleRoot, defaultFont);

            // Built last, so it is the highest sibling under the root and draws on top of
            // every other panel above - required for the scrim to actually dim/block the rest of
            // the screen. Unrelated to _tutorialOverlay/BuildTutorialOverlay above (the JSON-
            // driven first-run narrative/arrow intro) - this is the guided-battle TutorialStep
            // machine's own teaching overlay.
            BuildTutorialTeachingOverlay(battleRoot, defaultFont);

            RefreshAll();
            MaybeShowTutorial();
        }

        // What level a brand-new player starts at. Before the save system existed, GameBootstrap
        // hardcoded these same three numbers as a "mid-range test profile" and every launch began
        // there. RESOLVED 2026-08-13 (Command Centre decision, extended same day to cover Castle
        // and Barracks): a genuinely new player starts at Avatar/Castle/Barracks level 1, not
        // mid-game - the onboarding Health taper in PlayerEmpireData.OnboardingBonusFor exists
        // precisely to keep a level-1 match from being too short for a spell to ever be cast, so
        // none of the three tracks need a mid-range value to stand in for that fix anymore. Level
        // 0/locked was considered and rejected: PlayerProfile (FROZEN) has no representation for
        // an unbuilt building, so 1 is the lowest state these fields can hold. Gate is not listed
        // here because SeedNewProfile never touched it - PlayerProfile.gateLevel already defaults
        // to 1 on its own, which already satisfies the same decision for that fourth track.
        private const int NewProfileAvatarLevel = 1;
        private const int NewProfileCastleLevel = 1;
        private const int NewProfileBarracksLevel = 1;

        /// <summary>
        /// Seeds a profile the save system identified as genuinely new (LoadStatus.NewGame, set
        /// by SaveSystem.Load() purely from whether a save file existed on disk - never from
        /// anything client-supplied). Existing and migrated profiles carry any other LoadStatus
        /// and never reach SeedNewProfile, so their saved levels are untouched by this gate.
        /// </summary>
        private static void SeedIfNewProfile(PlayerProfile profile)
        {
            if (profile.LoadStatus == SaveLoadStatus.NewGame)
            {
                SeedNewProfile(profile);
            }
        }

        /// <summary>Exposed for tests: EditMode construction never goes through
        /// SaveSystem.Load() (see the comment on Initialize()'s _profile assignment), so a test
        /// that wants to exercise the NewGame seeding path - or prove a non-NewGame profile is
        /// left alone by it - has to invoke the same gate Initialize() uses directly.</summary>
        public static void SeedIfNewProfileForTests(PlayerProfile profile) => SeedIfNewProfile(profile);

        private static void SeedNewProfile(PlayerProfile profile)
        {
            profile.Data.avatarLevel = NewProfileAvatarLevel;
            profile.Data.castleLevel = NewProfileCastleLevel;
            profile.Data.barracksLevel = NewProfileBarracksLevel;

            // One-time carry-over from the PlayerPrefs flag onboarding used before the save file
            // existed, so anyone already testing this build is not shown the intro a second time.
            // Safe to delete once no installed build predates the save system.
            if (PlayerPrefs.GetInt(SeenIntroPrefKey, 0) == 1)
            {
                profile.Data.seenIntro = true;
            }

            // ApplyDataToEmpire() is no longer called here - Initialize() now calls it
            // unconditionally right after this method returns, covering every profile (not just
            // NewGame ones). See that call site's comment.
        }

        /// <summary>
        /// Builds a fresh match from the current Empire levels - called once from Initialize()
        /// and again from "Play Again" after a match ends, so ApplyMatchResult()'s level change
        /// actually shows up as a harder/easier next match, not just a number nobody sees change.
        /// Does not call RefreshAll() itself - callers that run before the UI panels exist
        /// (Initialize()) and callers that run after (Play Again) both need to control exactly
        /// when that happens.
        /// </summary>
        /// <summary>
        /// useRecommendedDeck picks the player's deck with BuildStrongestDeck (see that method)
        /// instead of the default path - the concrete stand-in for the reference UI's
        /// "Recommended Lineup" button.
        ///
        /// allowSavedDeck governs the default (useRecommendedDeck: false) path only:
        /// true (initial boot, "Play Again") makes the player's last-confirmed
        /// DeckBuilderPresenter deck (activeDeckCardIds) the exclusive source of the normal
        /// player deck - see TryBuildSavedPlayerDeck. An invalid or incomplete saved deck
        /// (missing, wrong size, duplicate/unknown ids) blocks the match instead of padding,
        /// substituting, or generating a fallback deck (Command Centre decision, see
        /// _normalMatchStartError below) - the player must confirm a complete deck in Deck
        /// Builder before a normal battle can start. false ("Reset Lineup" specifically) bypasses
        /// the saved deck entirely and always generates a fresh random one - "Reset" must keep
        /// meaning what its own name and the player-facing UI already promise, not silently
        /// become "reload my saved deck again" once one exists to reload.
        /// </summary>
        private void StartNewMatch(bool useRecommendedDeck = false, bool allowSavedDeck = true)
        {
            // Modal precedence guard, mirroring StartApprovedTutorialBattle's own (2026-08-17): a
            // normal match must be completely free-play, with no leftover modal from a previous
            // session. If the older JSON-driven "how to play" narrative walkthrough
            // (MaybeShowTutorial/_tutorialOverlay) is still open - e.g. a player who never
            // engaged the guided tutorial reaches a normal battle straight from Deck Builder's
            // own "To Battle" action while SeenIntro is still false - force it closed now, before
            // anything else. A cheap no-op the very first time this runs, at Initialize() time,
            // since _tutorialOverlay does not exist yet.
            if (_tutorialOverlay != null && _tutorialOverlay.activeSelf) CloseNarrative();

            List<Card> fullPool = _cardDatabase.AllCards.ToList();
            List<Card> playerDeck;
            List<Card> enemyDeck;
            _normalMatchStartError = null;

            // Both sides get the same deck size. Deck size is a poor difficulty lever here (10-20
            // cards against a 3x3 board that fills long before a deck runs out), so making it
            // uneven would mostly just look unfair without changing much - the AI's actual
            // difficulty comes from SoloAIScalingSystem's HP/resource scaling and its archetype.
            int deckSize = _empireData.DeckSlotCount;

            if (useRecommendedDeck)
            {
                playerDeck = BuildStrongestDeck(fullPool, deckSize);
                List<Card> remainingPool = fullPool.Except(playerDeck).ToList();
                (_, enemyDeck) = BuildBalancedDecks(remainingPool, 0, deckSize);
            }
            else
            {
                if (allowSavedDeck)
                {
                    if (!TryBuildSavedPlayerDeck(deckSize, out playerDeck))
                    {
                        // First-normal-battle onboarding: this exact string is shown to the
                        // player as-is (RefreshNormalMatchGuidanceCaption) as well as exposed to
                        // tests via NormalMatchStatusForTests - one source of truth for both, so
                        // the player-facing copy and the test-visible signal can never drift.
                        _normalMatchStartError = $"No complete {deckSize}-card deck saved. Return to Deck Builder and save {deckSize} cards to enter Battle.";
                        playerDeck = new List<Card>();
                        enemyDeck = new List<Card>();
                    }
                    else if (_pendingCampaignStage != null)
                    {
                        // Campaign-stage battle-configuration contract: a stage launched via
                        // Campaign -> Home's onLaunchBattle callback -> OnToBattleClicked sets
                        // _pendingCampaignStage before revealing Battle (SetPendingCampaignStageForNextMatch).
                        // Only the ENEMY deck is stage-specific; the player still fields their own
                        // saved deck exactly as any other normal match, and enemy HP/Resource still
                        // comes from the same SoloAIScalingSystem call below, untouched - stages are
                        // distinct only through TryResolveCampaignEnemyDeck's verified composition
                        // (requirement 8), not new scaling. HomePagePresenter already refuses to
                        // launch an unresolvable stage before Battle is ever revealed
                        // (IsCampaignStageBattleConfigValid), so this failing here is a defense-in-
                        // depth backstop, not the primary gate - it still blocks the whole match via
                        // the same _normalMatchStartError surface rather than silently falling back
                        // to a random enemy deck (requirement 9).
                        if (!TryResolveCampaignEnemyDeck(_pendingCampaignStage, out enemyDeck))
                        {
                            _normalMatchStartError = $"Stage {_pendingCampaignStage.stageId} has an invalid battle configuration. Return to the Campaign map.";
                            playerDeck = new List<Card>();
                        }
                    }
                    else
                    {
                        (_, enemyDeck) = BuildBalancedDecks(fullPool, 0, deckSize);
                    }
                }
                else
                {
                    playerDeck = BuildBalancedDecks(fullPool, deckSize, deckSize).playerDeck;
                    (_, enemyDeck) = BuildBalancedDecks(fullPool, 0, deckSize);
                }
            }

            // Re-derived every match rather than once at startup, so the level gained from the
            // last result actually changes who shows up for the next one.
            _aiProfile = _aiScaling.GenerateAIOpponent(_empireData);

            var playerEconomy = new BattleController.MatchEconomy(
                _empireData.ResourceCap, _empireData.Turn1Resource, _empireData.StartingAvatarHealth);

            // AI formation-resource parity contract, requirement 1: the AI's Formation-phase
            // resource budget is now exactly the player's own (_empireData.ResourceCap/
            // Turn1Resource), not _aiProfile's difficulty-scaled StartingResourceCap/Turn1Resource
            // (which could sit above OR below the player's, depending on AIDifficultyTier - see
            // SoloAIScalingSystem's Novice/Titan resource ratios). PlayerBattleState.Resource is
            // computed as Min(ResourceCap, turn1Resource + ...) (see its own BeginTurn), so both
            // the cap and the turn-1 value must match for the clamp not to silently reintroduce a
            // gap - passing only Turn1Resource while leaving StartingResourceCap scaled would have
            // clamped straight back down to the old scaled figure. HP scaling (_aiProfile.
            // MaxAvatarHealth) is untouched - difficulty still comes from Avatar Health and
            // archetype placement (SimpleAIOpponent), never from a bigger card budget than the
            // player gets. TryPlayCard already checks and deducts ResourceCost identically for
            // both sides (requirements 2-4) - this call site is the only production reader of
            // _aiProfile.StartingResourceCap/Turn1Resource, so this one edit closes the gap
            // everywhere both normal and Campaign matches reach this method.
            var enemyEconomy = new BattleController.MatchEconomy(
                _empireData.ResourceCap, _empireData.Turn1Resource, _aiProfile.MaxAvatarHealth);
            _battleController.StartMatch(playerDeck, enemyDeck, playerEconomy, enemyEconomy);

            // Deal both sides their whole formation hand up front. The entire point of the
            // formation model is that the squad is built in one sitting rather than dribbled out
            // two cards per turn, so the hand has to be there in one go.
            _battleController.DealFormationHand(_battleController.PlayerState);
            _battleController.DealFormationHand(_battleController.EnemyState);

            if (_combatLoop != null)
            {
                // A restart ("Play Again", "Reset Lineup") during a running fight must not leave
                // the previous match's tick loop alive - it would keep advancing combat on the
                // new match's state.
                StopCoroutine(_combatLoop);
                _combatLoop = null;
            }

            // A generic match (initial boot, Play Again, Reset Lineup) must never inherit
            // IsTutorialMatch from a previous tutorial run - see that property's own comment.
            IsTutorialMatch = false;

            // Same reasoning, for the guided tutorial's own step gate: a normal match must never
            // inherit a leftover _tutorialStep from a previous tutorial run, or every one of the
            // gates in OnHandCardPressed/OnLanePressed/OnSpellTapped above would wrongly start
            // restricting it too - "normal matches remain completely unrestricted" is the whole
            // point of gating everything off _tutorialStep specifically, never IsTutorialMatch.
            _tutorialStep = null;

            PlayBattleMusicIfNeeded();
        }

        /// <summary>
        /// Starts the battle music loop, but only if it isn't already the thing playing.
        /// StartNewMatch and StartApprovedTutorialBattle both call this every time a battle
        /// (re)starts - initial boot, Play Again, Retry, Reset Lineup, Recommended - all of which
        /// must resume the same already-playing track rather than restarting or layering a
        /// second copy on top of it. There is exactly one AudioSource for the lifetime of this
        /// GameBootstrap (created once in Initialize()), so "already playing" is sufficient to
        /// prevent duplicates without any extra bookkeeping.
        /// </summary>
        private void PlayBattleMusicIfNeeded()
        {
            if (_musicSource == null || _battleMusicClip == null) return;
            if (_musicSource.isPlaying && _musicSource.clip == _battleMusicClip) return;

            _musicSource.clip = _battleMusicClip;
            _musicSource.Play();
        }

        /// <summary>
        /// Approved offline tutorial encounter - player Formation warrior/Front,
        /// novice_knight/Middle, goblin_caster/Back; enemy butcher, cursed_soldier,
        /// giant_worms, tribal_warrior; the same level-1 economy already validated in
        /// BalanceSimulationTests.Balance_ApprovedTutorialEncounter_FormsAndResolvesAtTheCastleOneBarracksOneBaseline.
        ///
        /// Reuses the existing _battleController/UI rather than a second instance - every
        /// panel and input surface in this file is hardwired to that one field, so a second
        /// controller would have no visible UI at all. Grants the starter cards into the
        /// local profile directly and sets IsTutorialMatch so HomePagePresenter's reward
        /// guard can recognize this match; makes no server call, confirms no victory, and
        /// advances no checkpoint - purely a local, offline prototype battle.
        /// </summary>
        /// <param name="showOpeningCinematic">True for a fresh tutorial start (default - matches
        /// every existing external caller, including HomePagePresenter's Start Tutorial action);
        /// false for a tutorial retry after defeat (see OnPlayAgainOrRetryPressed), which must
        /// re-enter Formation directly, not replay the opening.</param>
        public void StartApprovedTutorialBattle(bool showOpeningCinematic = true)
        {
            // Modal precedence guard, 2026-08-16: the older, JSON-driven "how to play" narrative
            // walkthrough (MaybeShowTutorial/_tutorialOverlay - independent of this guided,
            // interactive sequence and gated only on the player's own SeenIntro flag) can still
            // be mid-sequence (its own Next/Skip buttons up) at the exact moment a caller starts
            // this battle. Reported 2026-08-16: that overlay sat on top of and blocked this
            // sequence's own Continue button. This guided sequence already teaches everything the
            // narrative's "how to play" beats cover, interactively, so it is always safe to force
            // the narrative closed here rather than let two modal walkthroughs coexist.
            if (_tutorialOverlay != null && _tutorialOverlay.activeSelf) CloseNarrative();

            GrantApprovedStarterCardsIfMissing();

            var playerDeck = new List<Card>
            {
                _cardDatabase.GetCard("warrior"),
                _cardDatabase.GetCard("novice_knight"),
                _cardDatabase.GetCard("goblin_caster"),
            };

            // Teaching-encounter fix, 2026-08-16, extended for the guided step sequence: was the
            // same 4-card, normal-strength roster a real match uses - a genuine full board
            // against the player's fixed 3-card starter squad, reported losing three times in a
            // row playing it correctly. Tutorial-only, isolated entirely to this method: exactly
            // 3 cards, chosen from the existing approved card pool (no new card data), with
            // DIFFERENT rarities specifically so SimpleAIOpponent.TakeTurn's own
            // highest-ResourceCost-first ordering (see its own doc comment) deterministically
            // seats them - highest cost always goes to the first empty lane it tries (Front for
            // Balanced), then next-highest to the next-emptiest (Middle), lowest to the last
            // (Back) - regardless of the enemy hand's shuffle order:
            //   Front  - cleric (rarity 3, ATK3/HP3): dies outright to the player's Front ATK.
            //   Middle - zombified_captain (rarity 2, ATK2/HP2 -> HP3 with the Middle bonus):
            //            deliberately SURVIVES the player's Middle ATK2 with 1 HP left, so it is
            //            the sole living enemy unit going into the guided spell lesson (step 7) -
            //            the run's own "exactly one valid target".
            //   Back   - giant_worms (rarity 1, ATK1/HP1): dies outright to the player's Back ATK.
            // Verified empirically (not just by hand) in TutorialGuidedSequenceTests, which runs
            // the real production sequence end to end - see that file for the actual numbers.
            var enemyDeck = new List<Card>
            {
                _cardDatabase.GetCard("cleric"),
                _cardDatabase.GetCard("zombified_captain"),
                _cardDatabase.GetCard("giant_worms"),
            };

            // Fixed level-1 baseline for the approved tutorial content specifically - not
            // derived from the player's real progression, since this encounter's content is
            // the same regardless of how far along the player's own Empire actually is.
            var tutorialEmpire = new PlayerEmpireData();
            tutorialEmpire.SetLevels(avatarLevel: 1, castleLevel: 1, barracksLevel: 1);
            tutorialEmpire.InitializeTCGModifiers();
            var economy = new BattleController.MatchEconomy(
                tutorialEmpire.ResourceCap, tutorialEmpire.Turn1Resource, tutorialEmpire.StartingAvatarHealth);

            // Enemy keeps the same Resource/Turn1Resource as the player (so its 3 cards are
            // always affordable to deploy in full) but a deliberately tuned Avatar Health,
            // isolated to this tutorial encounter only. 40 is chosen to SURVIVE the first
            // clash's overflow damage (Front and Back clear, Middle's zombified_captain does
            // not - see enemyDeck's own comment) so there is a real "first combat result" to
            // show and a real spell-lesson target left standing, then be comfortably finished
            // off by the guided spell cast plus the guaranteed follow-up clash once every enemy
            // lane sits permanently empty against the player's still-living board. Not a
            // razor's-edge exact kill either way - verified empirically in
            // TutorialGuidedSequenceTests.
            const int tutorialEnemyStartingHealth = 40;
            var enemyEconomy = new BattleController.MatchEconomy(
                tutorialEmpire.ResourceCap, tutorialEmpire.Turn1Resource, tutorialEnemyStartingHealth);

            // Tutorial-owned, always explicitly Balanced - _aiProfile is otherwise re-derived
            // per normal match (see StartNewMatch) and would sit stale here, so enemy lane
            // deployment (SimpleAIOpponent.TakeTurn reads _aiProfile.Archetype) could
            // accidentally inherit whatever archetype a previous normal match happened to roll.
            // MaxAvatarHealth mirrors tutorialEnemyStartingHealth (not the player's economy), so
            // the enemy HP bar's own max value matches what StartMatch actually gave it below.
            _aiProfile = new AIBattleProfile
            {
                DisplayName = SoloAIScalingSystem.GetOpponentName(AIDifficultyTier.Novice),
                DifficultyTier = AIDifficultyTier.Novice,
                Archetype = AIArchetype.Balanced,
                MaxAvatarHealth = tutorialEnemyStartingHealth,
                StartingResourceCap = tutorialEmpire.ResourceCap,
                Turn1Resource = tutorialEmpire.Turn1Resource,
            };

            _battleController.StartMatch(playerDeck, enemyDeck, economy, enemyEconomy);
            _battleController.DealFormationHand(_battleController.PlayerState);
            _battleController.DealFormationHand(_battleController.EnemyState);

            // Deterministic hand order for this scripted encounter only: PlayerBattleState
            // shuffles its DrawPile unconditionally (real matches want that), which left the
            // player's dealt Hand in a random permutation of these same three cards - harmless
            // to the guided step gates themselves (they check card id, not hand position), but
            // it meant any caller that places the hand in its own iteration order without
            // looking each card up by id could visit them out of the guided CardCost -> FrontLane
            // -> MiddleLane -> BackLane sequence and have a legal placement rejected. Restored to
            // the same fixed order playerDeck was authored in above.
            _battleController.PlayerState.Hand.Sort((a, b) =>
                playerDeck.FindIndex(c => c.Id == a.Id).CompareTo(playerDeck.FindIndex(c => c.Id == b.Id)));

            if (_combatLoop != null)
            {
                StopCoroutine(_combatLoop);
                _combatLoop = null;
            }

            IsTutorialMatch = true;
            PlayBattleMusicIfNeeded();

            // Guided Chapter 1 sequence, 2026-08-16: always resets to the first step, on both a
            // fresh start and a retry after defeat - see OnPlayAgainOrRetryPressed's own comment
            // for why a retry still restarts the step sequence but skips the opening cinematic.
            _tutorialStep = TutorialStep.CardCost;
            _tutorialSpellCastSummary = null;
            _tutorialFrontPlacementSummary = null;

            RefreshAll();

            // Purely additive - Formation's real state above is already fully built and
            // RefreshAll()'d; this only shows a blocking overlay on top of it in Play Mode (a
            // no-op in EditMode, since it only creates a GameObject and starts a coroutine that
            // never ticks outside Play Mode - same reasoning as every other coroutine in this
            // file, e.g. CombatLoop). No existing Formation-state assertion is affected by it.
            if (showOpeningCinematic) BeginOpeningCinematic();
        }

        // ---------- Chapter 1 tutorial cinematics ----------
        //
        // Phase A: runtime flow only (timed, skippable, layered-still overlay; the elaborate
        // per-layer percent-motion/camera-crop/particle values in
        // CHAPTER_1_CINEMATIC_LAYER_MOTION_SPEC.md are deferred to a later pass). Both
        // cinematics are purely additive: by the time BeginOpeningCinematic/BeginVictoryCinematic
        // is ever called, the real Formation state or result overlay it sits in front of has
        // already been fully built and activated exactly as it always was - the cinematic only
        // covers it and blocks input for its duration, then reveals it by disappearing. Nothing
        // about the underlying battle state is gated behind the cinematic, so no existing test's
        // assertions about that state (immediately after StartApprovedTutorialBattle/
        // HandleMatchEnded) are affected by whether a cinematic is also showing.

        private CinematicSequence _activeCinematic;
        private Coroutine _cinematicCoroutine;
        private GameObject _cinematicOverlay;
        private Text _cinematicCopyText;
        private readonly List<Image> _cinematicLayerImages = new List<Image>();

        /// <summary>Exposed for tests: whether a cinematic is currently blocking input.</summary>
        public bool CinematicActiveForTests => _activeCinematic != null;

        /// <summary>Exposed for tests: whether the older JSON-driven "how to play" narrative
        /// walkthrough (MaybeShowTutorial/_tutorialOverlay/BuildTutorialOverlay) is currently
        /// active - distinct from the guided tutorial's own teaching overlay.</summary>
        public bool NarrativeOverlayActiveForTests => _tutorialOverlay != null && _tutorialOverlay.activeSelf;

        /// <summary>Exposed for tests: forces the narrative overlay active, simulating a session
        /// where MaybeShowTutorial left it open (MaybeShowTutorial itself never fires in EditMode
        /// - Application.isPlaying is always false there - so this is the only way to reproduce
        /// "the narrative was still open" as a starting condition for a test).</summary>
        public void ForceNarrativeOverlayActiveForTests()
        {
            if (_tutorialOverlay != null) _tutorialOverlay.SetActive(true);
        }

        /// <summary>Exposed for tests: which cinematic is active, or null if none is.</summary>
        public CinematicKind? CinematicKindForTests => _activeCinematic?.Kind;

        /// <summary>Exposed for tests: the real Skip handler, without needing to simulate a UI
        /// click - same pattern as every other ...ForTests() method in this file.</summary>
        public void SkipCinematicForTests() => OnCinematicSkipPressed();

        /// <summary>Exposed for tests: the current match's Empire data (Avatar/Castle/Barracks
        /// levels) - lets a test prove an action never granted progression.</summary>
        public PlayerEmpireData EmpireForTests => _empireData;

        /// <summary>Exposed for tests: the local profile's owned-card ids - lets a test prove an
        /// action never duplicated a starter-card grant.</summary>
        public List<string> ProfileCardCollectionForTests => _profile?.cardCollection;

        /// <summary>Exposed for tests: whether the battle canvas is currently visible - the same
        /// state SetBattleCanvasVisible(bool) controls for the metagame side.</summary>
        public bool BattleCanvasVisibleForTests => _canvasTransform != null && _canvasTransform.gameObject.activeSelf;

        private static readonly string[] OpeningCinematicLayers =
        {
            "Cinematics/Chapter1/Opening/OPEN_01_SKY_WEATHER_BG",
            "Cinematics/Chapter1/Opening/OPEN_02_MOUNTAINS_FAR",
            "Cinematics/Chapter1/Opening/OPEN_03_FORTRESS_MID",
            "Cinematics/Chapter1/Opening/OPEN_04_BRIDGE_FOREGROUND",
            "Cinematics/Chapter1/Opening/OPEN_05_THREAT_FG",
            "Cinematics/Chapter1/Opening/OPEN_06_ATMOS_LIGHT_FX",
        };

        private static readonly string[] VictoryCinematicLayers =
        {
            "Cinematics/Chapter1/Victory/CLOSE_01_SKY_WEATHER_BG",
            "Cinematics/Chapter1/Victory/CLOSE_02_MOUNTAINS_FAR",
            "Cinematics/Chapter1/Victory/CLOSE_03_FORTRESS_MID",
            "Cinematics/Chapter1/Victory/CLOSE_04_BRIDGE_FOREGROUND",
            "Cinematics/Chapter1/Victory/CLOSE_05_ATMOS_LIGHT_FX",
        };

        /// <summary>Opening line 1 and 2, verbatim from the handoff's Copy contract, shown
        /// together for Phase A rather than the timing sheet's separate 1.00-2.70s/3.00-5.20s
        /// crossfade windows.</summary>
        private const string OpeningCinematicCopy =
            "The Empire stands wounded.\nLearn to form your ranks and face the first threat.";

        private const string VictoryCinematicCopy = "Victory. The first threat has been driven back.";

        private void BeginOpeningCinematic() =>
            BeginCinematic(CinematicKind.Opening, 8f, OpeningCinematicLayers, OpeningCinematicCopy);

        private void BeginVictoryCinematic() =>
            BeginCinematic(CinematicKind.Victory, 5f, VictoryCinematicLayers, VictoryCinematicCopy);

        private void BeginCinematic(CinematicKind kind, float durationSeconds, string[] layerResourcePaths, string copy)
        {
            if (_cinematicCoroutine != null)
            {
                StopCoroutine(_cinematicCoroutine);
                _cinematicCoroutine = null;
            }

            _activeCinematic = new CinematicSequence(kind, durationSeconds);
            BuildCinematicOverlay(layerResourcePaths, copy);
            _cinematicCoroutine = StartCoroutine(RunCinematic());

            // Modal precedence guard: re-evaluate immediately so the teaching overlay hides
            // itself the instant a cinematic exists, rather than staying active underneath it
            // until the next unrelated RefreshAll() happens to fire.
            RefreshTutorialTeachingOverlay();
        }

        /// <summary>Drives CinematicSequence.Advance() with real frame time - never ticks outside
        /// Play Mode (Unity coroutines don't advance in EditMode), same as CombatLoop.</summary>
        private IEnumerator RunCinematic()
        {
            while (_activeCinematic != null && !_activeCinematic.IsComplete)
            {
                yield return null;
                _activeCinematic?.Advance(Time.deltaTime);
                DriftCinematicLayers();
            }
            CompleteActiveCinematic();
        }

        /// <summary>Simple continuous horizontal drift on the two outermost layers (sky drifts
        /// one way, the nearest foreground layer drifts the other) - a lightweight approximation
        /// of the motion spec's per-layer parallax for this phase, not its exact percent curves.</summary>
        private void DriftCinematicLayers()
        {
            if (_activeCinematic == null || _cinematicLayerImages.Count == 0) return;

            float t = _activeCinematic.ElapsedSeconds;
            Image sky = _cinematicLayerImages[0];
            if (sky != null) sky.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t * 0.05f) * 14f, 0f);

            Image nearest = _cinematicLayerImages[_cinematicLayerImages.Count - 1];
            if (nearest != null && nearest != sky)
            {
                nearest.rectTransform.anchoredPosition = new Vector2(Mathf.Sin(t * 0.08f + 1.5f) * -22f, 0f);
            }
        }

        private void OnCinematicSkipPressed()
        {
            if (_activeCinematic == null) return;

            // First accepted Skip wins; a second tap while the coroutine is already stopping
            // finds _activeCinematic already null (CompleteActiveCinematic cleared it) and this
            // whole method is a no-op - see CinematicSequence.Skip's own comment for the same
            // rule at the timer level.
            _activeCinematic.Skip();
            if (_cinematicCoroutine != null)
            {
                StopCoroutine(_cinematicCoroutine);
                _cinematicCoroutine = null;
            }
            CompleteActiveCinematic();
        }

        private void CompleteActiveCinematic()
        {
            if (_activeCinematic == null) return;
            _activeCinematic = null;
            HideCinematicOverlay();

            // Modal precedence guard, mirroring BeginCinematic's own call: Formation guidance may
            // only begin once the opening cinematic has fully finished or been skipped - this is
            // what lets it reappear the instant that happens, rather than waiting for the next
            // unrelated RefreshAll().
            RefreshTutorialTeachingOverlay();
        }

        /// <summary>
        /// Full-screen blocking overlay: an opaque background (raycastTarget=true, so it
        /// intercepts every tap meant for the board beneath it - "Cinematic blocks battle input
        /// while active"), the approved layered art stacked back-to-front, the runtime copy, and
        /// a Skip button. Rebuilt fresh per cinematic rather than reused, since this shows at
        /// most twice in a tutorial run (opening, victory) - not worth a persistent pooled object.
        /// </summary>
        private void BuildCinematicOverlay(string[] layerResourcePaths, string copy)
        {
            if (_cinematicOverlay != null) DestroyImmediate(_cinematicOverlay);
            _cinematicLayerImages.Clear();

            _cinematicOverlay = new GameObject("Chapter1Cinematic", typeof(RectTransform));
            _cinematicOverlay.transform.SetParent(_canvasTransform, false);
            StretchFull((RectTransform)_cinematicOverlay.transform);

            // Opaque fallback so the overlay still fully blocks input and reads as a real
            // transition even if a layer sprite fails to load - never a blank/see-through gap.
            Image background = CreateImage(_cinematicOverlay.transform, Color.black);
            StretchFull(background.rectTransform);

            Font font = GetDefaultFont();

            foreach (string path in layerResourcePaths)
            {
                Sprite sprite = Resources.Load<Sprite>(path);
                if (sprite == null) continue; // missing/optional layer - play with what's approved and present.

                var layerGo = new GameObject(path.Substring(path.LastIndexOf('/') + 1), typeof(RectTransform));
                layerGo.transform.SetParent(_cinematicOverlay.transform, false);
                var layerImage = layerGo.AddComponent<Image>();
                layerImage.sprite = sprite;
                layerImage.preserveAspect = false;
                layerImage.raycastTarget = false;
                // Slight overscan so the small drift in DriftCinematicLayers never exposes an
                // edge - "Layer source files require overscan equal to maximum movement plus 1%
                // safety on every moving edge" (motion spec).
                var layerRect = (RectTransform)layerGo.transform;
                layerRect.anchorMin = new Vector2(-0.03f, -0.03f);
                layerRect.anchorMax = new Vector2(1.03f, 1.03f);
                layerRect.offsetMin = Vector2.zero;
                layerRect.offsetMax = Vector2.zero;
                _cinematicLayerImages.Add(layerImage);
            }

            // Matte bars - top and bottom, static for this phase (the timing sheet's animated
            // retract is deferred, same as the rest of the per-frame motion curve).
            Image topMatte = CreateImage(_cinematicOverlay.transform, Color.black);
            topMatte.raycastTarget = false;
            AnchorBand(topMatte.rectTransform, 0.97f, 1f, 0f, 0f);
            Image bottomMatte = CreateImage(_cinematicOverlay.transform, Color.black);
            bottomMatte.raycastTarget = false;
            AnchorBand(bottomMatte.rectTransform, 0f, 0.03f, 0f, 0f);

            _cinematicCopyText = CreateText(_cinematicOverlay.transform, copy, 26, Color.white, font);
            _cinematicCopyText.fontStyle = FontStyle.Bold;
            _cinematicCopyText.alignment = TextAnchor.UpperLeft;
            _cinematicCopyText.raycastTarget = false;
            _cinematicCopyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            var copyOutline = _cinematicCopyText.gameObject.AddComponent<Outline>();
            copyOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            copyOutline.effectDistance = new Vector2(2f, -2f);
            // Upper-left copy-safe region, per the motion spec.
            AnchorBand(_cinematicCopyText.rectTransform, 0.72f, 0.92f, 0.06f, 0.32f);

            Button skipButton = CreateButton(_cinematicOverlay.transform, "Skip", font, OnCinematicSkipPressed);
            RectTransform skipRect = skipButton.GetComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(1f, 1f);
            skipRect.anchorMax = new Vector2(1f, 1f);
            skipRect.pivot = new Vector2(1f, 1f);
            skipRect.sizeDelta = new Vector2(140f, 56f);
            skipRect.anchoredPosition = new Vector2(-24f, -24f);
            skipButton.GetComponentInChildren<Text>().fontSize = 18;

            // Distinct from "Skip" above (which only skips *this cinematic* and continues into
            // the battle) - ends the whole tutorial encounter. Stacked directly above it so both
            // remain individually reachable rather than one replacing the other.
            Button skipTutorialButton = CreateButton(_cinematicOverlay.transform, "SKIP TUTORIAL", font, OnSkipTutorialPressed);
            RectTransform skipTutorialRect = skipTutorialButton.GetComponent<RectTransform>();
            skipTutorialRect.anchorMin = new Vector2(1f, 1f);
            skipTutorialRect.anchorMax = new Vector2(1f, 1f);
            skipTutorialRect.pivot = new Vector2(1f, 1f);
            skipTutorialRect.sizeDelta = new Vector2(190f, 56f);
            skipTutorialRect.anchoredPosition = new Vector2(-24f, -88f);
            Text skipTutorialLabel = skipTutorialButton.GetComponentInChildren<Text>();
            skipTutorialLabel.fontSize = 18;
            skipTutorialLabel.fontStyle = FontStyle.Bold;
        }

        private void HideCinematicOverlay()
        {
            if (_cinematicOverlay == null) return;
            DestroyImmediate(_cinematicOverlay);
            _cinematicOverlay = null;
            _cinematicCopyText = null;
            _cinematicLayerImages.Clear();
        }

        // ---------- Chapter 1 guided tutorial step machine ----------
        //
        // Every step's exact allowance is centralized here so no handler invents its own gating
        // rule - OnHandCardPressed/SelectOrDeselectFormationHandCard, OnLanePressed,
        // OnSpellTapped and OnSpellTargetLanePressed each just ask TutorialAllowedCardId/
        // TutorialAllowedLane (or check _tutorialStep directly for the spell step) before doing
        // anything, and the same call sites that already advance real game state (card placed,
        // battle started, spell cast, tick resolved) are the only places that also advance
        // _tutorialStep - see TutorialStep's own doc comment for the full step table.

        /// <summary>The one hand card allowed this step, or null if none is.</summary>
        private string TutorialAllowedCardId() => _tutorialStep switch
        {
            TutorialStep.CardCost => "warrior",
            TutorialStep.MiddleLane => "novice_knight",
            TutorialStep.BackLane => "goblin_caster",
            _ => null,
        };

        /// <summary>The one player lane allowed this step, or null if none is - including
        /// BeginBattle, which has nothing left to place and must leave every lane locked.</summary>
        private Lane? TutorialAllowedLane() => _tutorialStep switch
        {
            TutorialStep.FrontLane => Lane.Front,
            TutorialStep.MiddleLane => Lane.Middle,
            TutorialStep.BackLane => Lane.Back,
            _ => null,
        };

        /// <summary>True for the Formation-phase card-placement steps specifically - used only
        /// where a check needs to distinguish "a placement step" from "some other tutorial step
        /// that happens to also return null from TutorialAllowedLane" (BeginBattle).</summary>
        private bool IsTutorialFormationGateStep() =>
            _tutorialStep is TutorialStep.CardCost or TutorialStep.FrontLane
                or TutorialStep.MiddleLane or TutorialStep.BackLane;

        private void AdvanceTutorialStep(TutorialStep next) => _tutorialStep = next;

        /// <summary>Called from OnLanePressed's own successful-placement branch - the single
        /// place all three Front/Middle/Back placements advance the step, so the advancement
        /// rule can never drift out of sync with which lane/card actually just succeeded.</summary>
        private void AdvanceTutorialStepAfterPlacement(Card playedCard, Lane lane)
        {
            if (_tutorialStep == TutorialStep.FrontLane && lane == Lane.Front)
            {
                BattleCardInstance placed = _battleController.PlayerState.Lanes[Lane.Front].Cards.LastOrDefault();
                _tutorialFrontPlacementSummary = placed != null
                    ? $"Warrior now has {placed.Attack} ATK in Front."
                    : null;
                AdvanceTutorialStep(TutorialStep.MiddleLane);
            }
            else if (_tutorialStep == TutorialStep.MiddleLane && lane == Lane.Middle)
            {
                AdvanceTutorialStep(TutorialStep.BackLane);
            }
            else if (_tutorialStep == TutorialStep.BackLane && lane == Lane.Back)
            {
                AdvanceTutorialStep(TutorialStep.BeginBattle);
            }
        }

        /// <summary>Instructional caption for the current guided step - the one place this copy
        /// lives, so nothing else has to duplicate or drift from it.</summary>
        private string TutorialStepCaption() => _tutorialStep switch
        {
            TutorialStep.CardCost =>
                "Cards cost Energy to play. Tap Warrior to select it.",
            TutorialStep.FrontLane =>
                "Front grants +1 ATK. Tap the Front lane to place Warrior there.",
            TutorialStep.MiddleLane =>
                (_tutorialFrontPlacementSummary != null ? _tutorialFrontPlacementSummary + " " : "") +
                "Middle grants +1 HP. Tap Novice Knight, then the Middle lane.",
            TutorialStep.BackLane =>
                "Back has no lane bonus. Tap Goblin Caster, then the Back lane.",
            TutorialStep.BeginBattle =>
                "Your formation is ready. Tap Start Battle.",
            TutorialStep.FirstCombatResult => TutorialFirstCombatResultCaption(),
            TutorialStep.SpellLesson =>
                "Spells spend Energy to change the battle. Tap Firestorm, then the highlighted enemy lane.",
            TutorialStep.Finish =>
                (_tutorialSpellCastSummary != null ? _tutorialSpellCastSummary + " " : "") +
                "Tap Continue to finish the battle.",
            _ => string.Empty,
        };

        private string TutorialFirstCombatResultCaption()
        {
            PlayerBattleState enemy = _battleController.EnemyState;
            PlayerBattleState player = _battleController.PlayerState;
            return $"Clash {_battleController.TickCount} resolved - Enemy Health {enemy.AvatarHealth}/{enemy.MaxAvatarHealth}, " +
                   $"your Health {player.AvatarHealth}/{player.MaxAvatarHealth}. Tap Continue.";
        }

        /// <summary>
        /// Central per-step gate, called every RefreshAll(): shows/hides and colors the
        /// tutorial caption and the Continue control. Interactable state for hand cards, player
        /// lanes and spells is set at their own creation/refresh points (RefreshHand,
        /// RefreshLaneButtons, RefreshPhaseControls) rather than here, so each stays next to the
        /// code that already builds that control.
        /// </summary>
        private void RefreshTutorialStepControls()
        {
            if (_tutorialGuidanceCaption == null) return;

            bool active = _tutorialStep != null;
            // Reverted 2026-08-18: a prior fix pointed this at a static, phase-based "Ready.../
            // Hold your formation..." readout instead, on the theory that duplicating the guide
            // panel's own TutorialStepCaption text here was pure redundancy. That broke
            // TutorialTeachingOverlayTests' own expectation that THIS field carries the real
            // per-step outcome text (e.g. the spell-cast summary naming the actual enemy unit and
            // HP change) - a static placeholder is strictly less informative to the player than
            // the real outcome, so this field's own test suite is the more load-bearing one.
            // TutorialGuidanceTests' two caption assertions were updated to match instead (see
            // that file's own 2026-08-18 note).
            if (active)
            {
                _tutorialGuidanceCaption.gameObject.SetActive(true);
                _tutorialGuidanceCaption.text = TutorialStepCaption();
            }
            else
            {
                // First-normal-battle onboarding (release repair): _tutorialStep is always null
                // for a normal match (StartNewMatch's own guarantee) - this same caption surface,
                // otherwise idle for the whole match, now carries the Auto Formation/"formation
                // ready"/blocked-deck instructions instead. Confirmed compatible before adding:
                // this branch only ever runs when the guided-tutorial branch above did not, so it
                // can never fight the tutorial for this Text's content or visibility.
                //
                // First-time Campaign onboarding: a Campaign attempt (_pendingCampaignStage set by
                // the real launch gate - see TryLaunchCampaignStage/StartNewMatch) gets the fuller
                // explanatory captions instead of the terser ordinary-normal-match ones below,
                // since this same idle caption surface is exactly what the onboarding brief asks
                // to reuse. Ordinary "To Battle" matches (_pendingCampaignStage == null) keep the
                // existing, already-tested RefreshNormalMatchGuidanceCaption copy unchanged.
                if (_pendingCampaignStage != null)
                {
                    RefreshCampaignGuidanceCaption();
                }
                else
                {
                    RefreshNormalMatchGuidanceCaption();
                }
            }

            if (_tutorialContinueButton != null)
            {
                bool showContinue = _tutorialStep is TutorialStep.FirstCombatResult or TutorialStep.Finish;
                _tutorialContinueButton.gameObject.SetActive(showContinue);
            }

            RefreshTutorialTeachingOverlay();
        }

        /// <summary>
        /// First-normal-battle onboarding (release repair): drives the shared tutorial-guidance
        /// caption for a normal (non-tutorial) match only - called exclusively from
        /// RefreshTutorialStepControls' own _tutorialStep == null branch, so it never runs
        /// alongside the guided tutorial's own use of the same Text. Three mutually exclusive
        /// states, each the exact copy the release-repair brief requires:
        ///   - no valid confirmed deck: the blocked-start status (_normalMatchStartError),
        ///     telling the player to return to Deck Builder - never an old/unrelated hand,
        ///     which StartNewMatch's own saved-deck gate already guarantees is never dealt;
        ///   - a valid deck but an empty board: "build a formation, or tap Auto Formation";
        ///   - every player lane occupied: "formation ready, tap Start Battle".
        /// Hidden outside Formation (Combat/Resolved already have their own activity-rail and
        /// result-screen readouts) and for the whole duration of any tutorial match.
        /// </summary>
        private void RefreshNormalMatchGuidanceCaption()
        {
            if (IsTutorialMatch || _battleController.Phase != BattlePhase.Formation)
            {
                _tutorialGuidanceCaption.gameObject.SetActive(false);
                return;
            }

            if (_normalMatchStartError != null)
            {
                _tutorialGuidanceCaption.text = _normalMatchStartError;
                _tutorialGuidanceCaption.gameObject.SetActive(true);
                return;
            }

            if (AllPlayerLanesOccupied())
            {
                _tutorialGuidanceCaption.text = "Formation ready. Tap Start Battle.";
                _tutorialGuidanceCaption.gameObject.SetActive(true);
                return;
            }

            if (!AnyPlayerLaneOccupied())
            {
                _tutorialGuidanceCaption.text = "Your saved deck fills the hand. Tap Auto Formation to deploy a starting squad.";
                _tutorialGuidanceCaption.gameObject.SetActive(true);
                return;
            }

            // Partial manual placement (1-2 lanes filled, not via Auto Formation) - no
            // prescribed copy for this in-between state; the existing selected-card/placement
            // status text already carries what's needed here.
            _tutorialGuidanceCaption.gameObject.SetActive(false);
        }

        /// <summary>
        /// First-time Campaign onboarding: drives the same shared caption surface
        /// RefreshNormalMatchGuidanceCaption uses, but only for a Campaign attempt
        /// (_pendingCampaignStage != null) and across Formation/Combat/Resolved (that method stays
        /// Formation-only) - a new player's "everything is on autopilot" confusion specifically
        /// named the loop this covers. Deterministic and state-driven, same as every other caption
        /// in this file: identical board/phase state always produces identical text, no flag, no
        /// timer, no "seen once" gate, and nothing here disables or delays a single real control
        /// (Auto Formation, manual placement, spells, Start Battle, Retry, Return Home all keep
        /// their own existing interactable rules untouched).
        ///
        /// Five states, matching the onboarding brief's own five numbered requirements:
        ///   1. Formation, nothing placed: saved deck -> hand, Auto Formation is optional, and the
        ///      exact manual-placement steps (tap a hand card, then an empty lane slot; Resource
        ///      spent normally - not a new rule, TryPlayCard already works this way).
        ///   2/3. Formation, at least one card placed: may add more affordable cards, plain-
        ///      language lane roles (Front/Middle/Back - the existing +1 Attack / +1 Health /
        ///      no-bonus mechanics BattleController.TryPlayCard already applies, not invented
        ///      here), and that Start Battle is available whenever ready.
        ///   4. Combat: cards resolve automatically each clash; spells remain the player's active
        ///      choice when available - restates BattleController's own real behavior, adds no
        ///      new mechanic.
        ///   5. Resolved: handled separately, on the existing result-overlay text
        ///      (HandleMatchEnded's own _resultText) rather than this caption - see that method's
        ///      own comment for why.
        /// The blocked-deck state reuses _normalMatchStartError exactly as the non-Campaign
        /// caption does (defense-in-depth backstop only - HomePagePresenter's own pre-launch gate
        /// already prevents a real Campaign attempt from ever reaching Formation without a valid
        /// deck).
        /// </summary>
        private void RefreshCampaignGuidanceCaption()
        {
            if (_tutorialGuidanceCaption == null) return;

            if (_battleController.Phase == BattlePhase.Resolved)
            {
                // The result overlay's own text (HandleMatchEnded) already carries the
                // requirement-5 next-action line for a Campaign outcome - this caption stays
                // hidden rather than duplicating it on top of that overlay.
                _tutorialGuidanceCaption.gameObject.SetActive(false);
                return;
            }

            if (_battleController.Phase == BattlePhase.Combat)
            {
                _tutorialGuidanceCaption.text = "Combat is automatic - your cards attack on their own each clash. Cast a spell below if one is ready; that choice is still yours.";
                _tutorialGuidanceCaption.gameObject.SetActive(true);
                return;
            }

            if (_normalMatchStartError != null)
            {
                _tutorialGuidanceCaption.text = _normalMatchStartError;
                _tutorialGuidanceCaption.gameObject.SetActive(true);
                return;
            }

            if (AnyPlayerLaneOccupied())
            {
                _tutorialGuidanceCaption.text = "You can add more cards from hand if you can afford them - Front gives +1 Attack, Middle gives +1 Health, Back has no bonus but is safest. Tap Start Battle when ready; that begins automatic combat, and your placement (plus any spells you cast) is your strategy.";
                _tutorialGuidanceCaption.gameObject.SetActive(true);
                return;
            }

            _tutorialGuidanceCaption.text = "Your saved 10-card deck fills the hand below. Tap Auto Formation for an optional basic three-lane squad, or place manually: tap a hand card, then an empty lane slot - Resource is spent as normal.";
            _tutorialGuidanceCaption.gameObject.SetActive(true);
        }

        /// <summary>
        /// The one RectTransform the teaching overlay currently highlights - matches exactly the
        /// one permitted action for the current step (TutorialAllowedCardId/TutorialAllowedLane,
        /// and the SpellLesson-specific gates in OnSpellTapped/OnSpellTargetLanePressed), so the
        /// spotlight can never point at anything the player isn't actually allowed to tap.
        ///
        /// MiddleLane/BackLane and SpellLesson each cover two sequential sub-actions (select a
        /// card, then place it; arm a spell, then target a lane) - the target tracks whichever
        /// of the two hasn't happened yet, using the same state (_selectedCard, _armedSpellIndex)
        /// the real handlers already gate on, so it can never drift out of sync with what a tap
        /// would actually do right now.
        /// </summary>
        private RectTransform GetTutorialActiveTargetRect()
        {
            switch (_tutorialStep)
            {
                case TutorialStep.CardCost:
                    return TutorialHandCardRect("warrior");

                case TutorialStep.FrontLane:
                    return _playerLaneButtons.TryGetValue(Lane.Front, out Button frontButton)
                        ? frontButton.GetComponent<RectTransform>() : null;

                case TutorialStep.MiddleLane:
                    return (_selectedCard != null && _selectedCard.Id == "novice_knight")
                        ? (_playerLaneButtons.TryGetValue(Lane.Middle, out Button middleButton)
                            ? middleButton.GetComponent<RectTransform>() : null)
                        : TutorialHandCardRect("novice_knight");

                case TutorialStep.BackLane:
                    return (_selectedCard != null && _selectedCard.Id == "goblin_caster")
                        ? (_playerLaneButtons.TryGetValue(Lane.Back, out Button backButton)
                            ? backButton.GetComponent<RectTransform>() : null)
                        : TutorialHandCardRect("goblin_caster");

                case TutorialStep.BeginBattle:
                    return _primaryActionButton != null ? _primaryActionButton.GetComponent<RectTransform>() : null;

                case TutorialStep.FirstCombatResult:
                case TutorialStep.Finish:
                    return _tutorialContinueButton != null ? _tutorialContinueButton.GetComponent<RectTransform>() : null;

                case TutorialStep.SpellLesson:
                    if (_armedSpellIndex == TutorialLessonSpellIndex)
                    {
                        return _enemyLaneButtons.TryGetValue(TutorialLessonTargetLane, out Button enemyLaneButton)
                            ? enemyLaneButton.GetComponent<RectTransform>() : null;
                    }
                    return _spellButtons.Count > TutorialLessonSpellIndex
                        ? _spellButtons[TutorialLessonSpellIndex].GetComponent<RectTransform>() : null;

                default:
                    return null;
            }
        }

        /// <summary>
        /// The one place "is some other modal walkthrough currently up" is decided - both
        /// RefreshTutorialTeachingOverlay (won't show while this is true) and this file's
        /// cinematic begin/end hooks (which re-run RefreshTutorialTeachingOverlay whenever this
        /// value could have changed) read the same two real, live flags: whether a cinematic
        /// sequence exists, and whether the older JSON-driven narrative overlay is currently
        /// active. No separate cached bool to drift out of sync with either.
        /// </summary>
        private bool IsAnyOtherTutorialModalActive() =>
            _activeCinematic != null || (_tutorialOverlay != null && _tutorialOverlay.activeSelf);

        /// <summary>Exposed for tests: the same real, live state
        /// RefreshTutorialTeachingOverlay's own precedence guard reads.</summary>
        public bool AnyOtherTutorialModalActiveForTests => IsAnyOtherTutorialModalActive();

        /// <summary>Finds a hand-row card button by card id - "Card_{id}" is the exact
        /// GameObject name CreateCardButton already gives it, so no separate id-lookup table is
        /// needed.</summary>
        private RectTransform TutorialHandCardRect(string cardId)
        {
            Button button = _handButtons.FirstOrDefault(b => b.gameObject.name == $"Card_{cardId}");
            return button != null ? button.GetComponent<RectTransform>() : null;
        }

        /// <summary>
        /// Restores whichever real control is currently reparented into the highlight slot back
        /// to its original parent/sibling position - called before a different target takes over
        /// and when the overlay hides entirely. A hand card target has already been destroyed by
        /// RefreshHand() before this ever runs (its GameObject reference is already gone, and
        /// Unity's overridden null-check on a destroyed Object correctly reports true.
        /// </summary>
        private void DestroyTutorialActionProxy()
        {
            if (_tutorialActionProxy == null) return;
            DestroyImmediate(_tutorialActionProxy.gameObject);
            _tutorialActionProxy = null;
        }

        /// <summary>
        /// The exact production handler call the current step's one allowed action must invoke -
        /// the literal same call a real tap on the real control (OnHandCardPressed's own
        /// onClick.AddListener, OnLanePressed's own button wiring, etc.) already makes. Mirrors
        /// GetTutorialActiveTargetRect's own per-step/sub-step logic exactly, since the proxy
        /// must always act on whichever control that method says is the current target.
        /// </summary>
        private UnityEngine.Events.UnityAction GetTutorialActiveProxyAction()
        {
            switch (_tutorialStep)
            {
                case TutorialStep.CardCost:
                    return () => { Card card = TutorialFindHandCard("warrior"); if (card != null) OnHandCardPressed(card); };

                case TutorialStep.FrontLane:
                    return () => OnLanePressed(Lane.Front);

                case TutorialStep.MiddleLane:
                    return (_selectedCard != null && _selectedCard.Id == "novice_knight")
                        ? (UnityEngine.Events.UnityAction)(() => OnLanePressed(Lane.Middle))
                        : () => { Card card = TutorialFindHandCard("novice_knight"); if (card != null) OnHandCardPressed(card); };

                case TutorialStep.BackLane:
                    return (_selectedCard != null && _selectedCard.Id == "goblin_caster")
                        ? (UnityEngine.Events.UnityAction)(() => OnLanePressed(Lane.Back))
                        : () => { Card card = TutorialFindHandCard("goblin_caster"); if (card != null) OnHandCardPressed(card); };

                case TutorialStep.BeginBattle:
                    return OnPrimaryActionPressed;

                case TutorialStep.FirstCombatResult:
                case TutorialStep.Finish:
                    return OnTutorialContinuePressed;

                case TutorialStep.SpellLesson:
                    return (_armedSpellIndex == TutorialLessonSpellIndex)
                        ? (UnityEngine.Events.UnityAction)(() => OnSpellTargetLanePressed(TutorialLessonTargetLane))
                        : () => OnSpellTapped(TutorialLessonSpellIndex);

                default:
                    return null;
            }
        }

        /// <summary>The real Card instance for a hand-card proxy action to pass to
        /// OnHandCardPressed - looked up by id at click time (not captured earlier), so it always
        /// reflects whatever RefreshHand() most recently built the hand from.</summary>
        private Card TutorialFindHandCard(string cardId) =>
            _battleController?.PlayerState.Hand.FirstOrDefault(c => c.Id == cardId);

        /// <summary>
        /// Rebuilds the Tutorial Action Proxy and repositions the decorative marker every
        /// refresh, without ever touching the real gameplay hierarchy. See this region's own
        /// top-of-file comment for why: earlier designs computed a coordinate-matched "hole" or
        /// reparented the real control above the blocker, and both were rejected against real
        /// manual QA (drift, and unclickable-despite-visible, respectively). This design instead
        /// leaves every real card/lane/spell/button exactly where normal Unity layout already
        /// puts it - a full-screen blocker dims it like everything else - and creates one small,
        /// transparent, real UnityEngine.UI.Button ("TutorialActionProxy") sized to that control's
        /// own current world bounds, sitting above the blocker, whose onClick is wired directly
        /// to the exact same private handler a real tap on the real control calls
        /// (GetTutorialActiveProxyAction). Destroyed and rebuilt fresh every call (never reused,
        /// never left stale), so at most one proxy - matching at most one allowed action - can
        /// ever exist. Called every RefreshAll() via RefreshTutorialStepControls.
        /// </summary>
        private void RefreshTutorialTeachingOverlay()
        {
            if (_tutorialTeachingOverlay == null) return;

            // Explicit precedence guard/state assertion: this overlay and any cinematic (opening
            // or victory) - or the older JSON-driven narrative walkthrough, if a caller ever
            // reactivates it - must never be active at once. A cinematic or the narrative always
            // outranks Formation/guided-step guidance, since both are meant to be watched/read
            // uninterrupted; this overlay simply refuses to show at all while either is up,
            // regardless of what _tutorialStep says.
            bool anotherModalIsActive = IsAnyOtherTutorialModalActive();

            // Never shown once the match has resolved - the Result overlay owns the whole screen
            // at that point, and the Finish step's own Continue target no longer means anything.
            bool matchResolved = _battleController != null && _battleController.Phase == BattlePhase.Resolved;

            // Explicit state assertion (requirement, 2026-08-17): this overlay must only ever
            // exist for the real guided tutorial match, never a normal one - checked directly
            // against IsTutorialMatch itself, not only inferred from _tutorialStep being non-null
            // (which StartNewMatch already resets to null for every normal match, but this makes
            // the invariant explicit rather than implicit, and safe even if the two ever drifted).
            RectTransform target = (_tutorialStep != null && IsTutorialMatch && !matchResolved && !anotherModalIsActive)
                ? GetTutorialActiveTargetRect() : null;

            DestroyTutorialActionProxy();

            if (target == null)
            {
                _tutorialTeachingOverlay.SetActive(false);
                return;
            }

            _tutorialTeachingOverlay.SetActive(true);
            ForceFullCanvasLayoutRebuild();

            (float fxMin, float fxMax, float fyMin, float fyMax) = TutorialWorldBoundsAsCanvasFractions(target, paddingPx: 0f);
            BuildTutorialActionProxy(fxMin, fyMin, fxMax, fyMax, GetTutorialActiveProxyAction());
            PositionMarkerAroundTarget(target);

            (Vector2 panelMin, Vector2 panelMax) = TutorialGuidePanelAnchors(_tutorialStep.Value);
            _tutorialGuidePanelRect.anchorMin = panelMin;
            _tutorialGuidePanelRect.anchorMax = panelMax;

            if (_tutorialGuideBodyText != null) _tutorialGuideBodyText.text = TutorialStepCaption();
        }

        /// <summary>
        /// Creates the one, fresh Tutorial Action Proxy for this refresh: an otherwise-normal
        /// Button/Image sized to the given canvas-fraction bounds, fully transparent
        /// (alpha 0 - still a real raycast target; Unity's default alphaHitTestMinimumThreshold
        /// of 0 does not exempt transparent pixels from hit-testing) so the real, dimmed control
        /// underneath still reads through it, parented as the last child of the always-topmost
        /// proxy container so nothing can render above it, and wired to invoke the exact
        /// production handler action passed in.
        /// </summary>
        private void BuildTutorialActionProxy(float xMin, float yMin, float xMax, float yMax, UnityEngine.Events.UnityAction action)
        {
            if (action == null) return;

            var proxyGo = new GameObject("TutorialActionProxy", typeof(RectTransform));
            proxyGo.transform.SetParent(_tutorialProxyContainer, false);
            var proxyRect = (RectTransform)proxyGo.transform;
            proxyRect.anchorMin = new Vector2(xMin, yMin);
            proxyRect.anchorMax = new Vector2(xMax, yMax);
            proxyRect.offsetMin = Vector2.zero;
            proxyRect.offsetMax = Vector2.zero;

            var proxyImage = proxyGo.AddComponent<Image>();
            proxyImage.color = new Color(0f, 0f, 0f, 0f);
            proxyImage.raycastTarget = true;

            var proxyButton = proxyGo.AddComponent<Button>();
            proxyButton.transition = Selectable.Transition.None;
            proxyButton.onClick.AddListener(action);

            _tutorialActionProxy = proxyButton;
        }

        /// <summary>Converts a RectTransform's real, current world corners into canvas-local
        /// anchor fractions (0..1), optionally padded outward by a fixed pixel margin - the one
        /// place this conversion is done, shared by the proxy's exact-fit bounds (padding 0) and
        /// the marker's slightly wider decorative frame.</summary>
        private (float xMin, float xMax, float yMin, float yMax) TutorialWorldBoundsAsCanvasFractions(RectTransform target, float paddingPx)
        {
            var canvasRect = (RectTransform)_canvasTransform;
            var corners = new Vector3[4];
            target.GetWorldCorners(corners);
            float xMin = float.MaxValue, xMax = float.MinValue, yMin = float.MaxValue, yMax = float.MinValue;
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = canvasRect.InverseTransformPoint(corners[i]);
                xMin = Mathf.Min(xMin, local.x);
                xMax = Mathf.Max(xMax, local.x);
                yMin = Mathf.Min(yMin, local.y);
                yMax = Mathf.Max(yMax, local.y);
            }

            Rect canvasLocal = canvasRect.rect;
            float fxMin = Mathf.Clamp01(((xMin - paddingPx) - canvasLocal.xMin) / canvasLocal.width);
            float fxMax = Mathf.Clamp01(((xMax + paddingPx) - canvasLocal.xMin) / canvasLocal.width);
            float fyMin = Mathf.Clamp01(((yMin - paddingPx) - canvasLocal.yMin) / canvasLocal.height);
            float fyMax = Mathf.Clamp01(((yMax + paddingPx) - canvasLocal.yMin) / canvasLocal.height);
            return (fxMin, fxMax, fyMin, fyMax);
        }

        /// <summary>
        /// Purely decorative (raycastTarget=false throughout) - a bright frame around the
        /// target's real world bounds plus a downward arrow above it, using the same existing
        /// "UI/Icons/Tutorial_Arrow" sprite the JSON-driven intro overlay already uses elsewhere
        /// in this file. Positioned independently of the proxy (which is exact-fit, no margin) -
        /// nothing here needs to be pixel-exact, since the proxy - not this marker - is what
        /// makes the target clickable.
        /// </summary>
        private void PositionMarkerAroundTarget(RectTransform target)
        {
            const float marginPx = 8f;
            (float fxMin, float fxMax, float fyMin, float fyMax) = TutorialWorldBoundsAsCanvasFractions(target, marginPx);

            Rect canvasLocal = ((RectTransform)_canvasTransform).rect;
            float thicknessFracX = 6f / canvasLocal.width;
            float thicknessFracY = 6f / canvasLocal.height;

            SetMarkerRect(_tutorialMarkerTop, fxMin, Mathf.Max(fyMin, fyMax - thicknessFracY), fxMax, fyMax);
            SetMarkerRect(_tutorialMarkerBottom, fxMin, fyMin, fxMax, Mathf.Min(fyMax, fyMin + thicknessFracY));
            SetMarkerRect(_tutorialMarkerLeft, fxMin, fyMin, Mathf.Min(fxMax, fxMin + thicknessFracX), fyMax);
            SetMarkerRect(_tutorialMarkerRight, Mathf.Max(fxMin, fxMax - thicknessFracX), fyMin, fxMax, fyMax);

            float arrowCenterX = (fxMin + fxMax) / 2f;
            float arrowHalfWidth = Mathf.Min(0.03f, (fxMax - fxMin) / 2f);
            float arrowHeight = 0.035f;
            float arrowGap = 0.006f;
            float arrowYMin = Mathf.Clamp(fyMax + arrowGap, 0f, 1f - arrowHeight);
            _tutorialMarkerArrow.anchorMin = new Vector2(Mathf.Clamp01(arrowCenterX - arrowHalfWidth), arrowYMin);
            _tutorialMarkerArrow.anchorMax = new Vector2(Mathf.Clamp01(arrowCenterX + arrowHalfWidth), Mathf.Clamp01(arrowYMin + arrowHeight));
            _tutorialMarkerArrow.offsetMin = Vector2.zero;
            _tutorialMarkerArrow.offsetMax = Vector2.zero;
        }

        private static void SetMarkerRect(RectTransform rect, float xMin, float yMin, float xMax, float yMax)
        {
            rect.anchorMin = new Vector2(xMin, yMin);
            rect.anchorMax = new Vector2(xMax, yMax);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>
        /// Fixed, analytically-safe parking spots for the guide panel - not computed from the
        /// target's own position, because every guided step's target (and the very next step's
        /// target) falls into one of exactly two known regions of the V3 layout, so a fixed
        /// choice per region is already guaranteed never to cover either one:
        ///  - Every step except SpellLesson: the target is in the Hand dock, a Player lane, or
        ///    the primary-action region - never the Enemy board, so parking there is always safe.
        ///  - SpellLesson specifically: the target is the Firestorm tile (Activity rail) or the
        ///    enemy Middle lane (Enemy board) - never the Player board, so parking there instead
        ///    is safe for this one step.
        /// </summary>
        private static (Vector2 min, Vector2 max) TutorialGuidePanelAnchors(TutorialStep step)
        {
            if (step == TutorialStep.SpellLesson)
            {
                return (new Vector2(0.10f, 0.27f), new Vector2(0.55f, 0.47f));
            }

            return (new Vector2(0.22f, 0.53f), new Vector2(0.68f, 0.85f));
        }

        /// <summary>Forces every RectTransform under the canvas to resolve its real, current
        /// layout before world corners are read anywhere - bottom-up (deepest first), then one
        /// final root-level pass, exactly the two-part sequence
        /// TutorialHandDockGeometryTests.SpawnAndInitializeBootstrap uses (see its own comment
        /// for why a single top-down call is not reliable). Cheap enough to call on every
        /// tutorial-step refresh - this file's UI tree is small and refreshes only on real player
        /// actions, never per-frame.</summary>
        private void ForceFullCanvasLayoutRebuild()
        {
            var canvasRect = _canvasTransform as RectTransform;
            if (canvasRect == null) return;

            foreach (RectTransform rt in canvasRect.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(canvasRect);
        }

        /// <summary>
        /// The tutorial's own Continue control (step 6 and step 8 - see TutorialStep's own doc
        /// comment). Step 6 -> 7: grants the scripted Energy for the spell lesson (see
        /// BattleController.SetEnergyForTutorial's own comment for why a direct grant is
        /// necessary here) and advances. Step 8: advances one real combat tick per tap - the
        /// same production AdvanceCombatTick() every other tick in this game runs through, not a
        /// separate resolution path - until the scripted encounter's own numbers (see
        /// StartApprovedTutorialBattle) resolve it, which HandleMatchEnded (already wired to
        /// BattleController.OnMatchEnded) then handles exactly as it always does.
        /// </summary>
        private void OnTutorialContinuePressed()
        {
            if (_tutorialStep == TutorialStep.FirstCombatResult)
            {
                const int tutorialSpellLessonEnergy = 30; // exactly Firestorm's EnergyCost
                _battleController.SetEnergyForTutorial(tutorialSpellLessonEnergy);
                AdvanceTutorialStep(TutorialStep.SpellLesson);
                RefreshAll();
                return;
            }

            if (_tutorialStep == TutorialStep.Finish)
            {
                if (_battleController.Phase == BattlePhase.Combat)
                {
                    TurnResolutionResult result = _battleController.AdvanceCombatTick();
                    RefreshAll();
                    ShowTurnDamage(result);
                    ShowClashEffects(result);
                }
            }
        }

        /// <summary>Exposed for tests: the real Continue tap calls the private
        /// OnTutorialContinuePressed() directly - same pattern as every other ...ForTests()
        /// method in this file.</summary>
        public void TutorialContinueForTests() => OnTutorialContinuePressed();

        /// <summary>Exposed for tests: the real Continue button (FirstCombatResult/Finish).</summary>
        public Button TutorialContinueButtonForTests => _tutorialContinueButton;

        /// <summary>
        /// Ends the tutorial encounter safely from either the opening cinematic or the guided
        /// teaching overlay: clears the gate first so nothing re-shows after the canvas hides,
        /// stops any in-flight cinematic, then routes through the exact same
        /// OnReturnToCityPressed() a normal match's own "Return to City" button calls. That
        /// method only ever hides the battle canvas, stops the music and fires
        /// OnReturnToCityRequested - it never grants rewards or progression (that only ever
        /// happens in HandleMatchEnded, reached solely through a real BattleController.OnMatchEnded
        /// firing, which a skip never triggers), so skipping is inherently reward/progression-free
        /// without needing its own separate guard. Starter-card duplication is likewise already
        /// impossible - GrantApprovedStarterCardsIfMissing (called once, at
        /// StartApprovedTutorialBattle) is idempotent and is not called again here.
        /// </summary>
        private void OnSkipTutorialPressed()
        {
            if (_activeCinematic != null)
            {
                if (_cinematicCoroutine != null)
                {
                    StopCoroutine(_cinematicCoroutine);
                    _cinematicCoroutine = null;
                }
                CompleteActiveCinematic();
            }

            _tutorialStep = null;
            DestroyTutorialActionProxy();
            OnReturnToCityPressed();
        }

        /// <summary>Exposed for tests: the real Skip Tutorial tap, from either overlay - both
        /// buttons call this same private handler.</summary>
        public void SkipTutorialForTests() => OnSkipTutorialPressed();

        /// <summary>Exposed for tests: the real, currently-highlighted target RectTransform for
        /// the active guided step, or null outside the tutorial - see
        /// GetTutorialActiveTargetRect's own comment for the per-step mapping.</summary>
        public RectTransform TutorialActiveTargetRectForTests() => GetTutorialActiveTargetRect();

        /// <summary>Exposed for tests: the teaching overlay's own root GameObject.</summary>
        public GameObject TutorialTeachingOverlayForTests => _tutorialTeachingOverlay;

        /// <summary>Exposed for tests: the guide panel's RectTransform (avatar/speaker/instruction/Continue).</summary>
        public RectTransform TutorialGuidePanelRectForTests => _tutorialGuidePanelRect;

        /// <summary>Exposed for tests: the always-topmost container the Tutorial Action Proxy (and
        /// the decorative marker) live in - proxy.transform.IsChildOf(this) proves the proxy
        /// renders/raycasts above the blocker.</summary>
        public RectTransform TutorialProxyContainerForTests => _tutorialProxyContainer;

        /// <summary>Exposed for tests: the single opaque, raycast-blocking, full-screen Image
        /// every real gameplay control (including the current step's real target) renders
        /// behind - the proxy is the only thing that ever sits above it.</summary>
        public Image TutorialFullScreenBlockerForTests => _tutorialFullScreenBlocker;

        /// <summary>Exposed for tests: the current step's one Tutorial Action Proxy Button, or
        /// null when no guided step is active.</summary>
        public Button TutorialActionProxyForTests => _tutorialActionProxy;

        /// <summary>Exposed for tests: the "SKIP TUTORIAL" button on the teaching overlay.</summary>
        public Button TutorialSkipButtonForTests => _tutorialSkipButton;

        /// <summary>Exposed for tests: the real Canvas's own GraphicRaycaster - lets a test drive
        /// an actual Unity pointer raycast rather than reimplementing hit-testing.</summary>
        public GraphicRaycaster CanvasRaycasterForTests => _canvasTransform != null
            ? _canvasTransform.GetComponent<GraphicRaycaster>() : null;

        /// <summary>Exposed for tests: forces a full layout + canvas rebuild, then re-derives the
        /// teaching overlay for the current step - the same rebuild
        /// RefreshTutorialTeachingOverlay always performs internally, callable standalone so a
        /// test can force it immediately before reading real post-layout state.</summary>
        public void ForceTutorialOverlayRebuildForTests()
        {
            ForceFullCanvasLayoutRebuild();
            RefreshTutorialTeachingOverlay();
        }

        /// <summary>
        /// The curated, valid, first-deck-ready starter collection - ten unique existing card
        /// ids (CardDatabase-verified), superseding the original three-card entitlement. Those
        /// original three (warrior/novice_knight/goblin_caster) are kept as the first three
        /// entries unchanged - they are still, separately, the tutorial's own hardcoded scripted
        /// formation (see StartApprovedTutorialBattle's own playerDeck) - and are joined by seven
        /// more low-rarity cards (rarity 1-3) spanning every card Type (warrior, knight,
        /// strategist, perfect) so a player who owns only this set can field a legally varied
        /// Deck Builder deck, not just a lopsided one. 2026-08-17: a tutorial completer who owned
        /// only the original three could never reach Deck Builder's 10-unique-card minimum,
        /// blocking normal play entirely - see GrantApprovedStarterCardsIfMissing's own comment.
        /// </summary>
        private static readonly string[] ApprovedStarterCollectionCardIds =
        {
            "warrior", "novice_knight", "goblin_caster",
            "cleric", "archer_elf", "fox", "bunny", "forest", "tribal_warrior", "undead_soldier",
        };

        /// <summary>
        /// Idempotent: adds only whichever of the ten approved starter-collection ids the local
        /// profile doesn't already own (existing players who only ever received the original
        /// three-card entitlement receive exactly the seven newly missing ids, nothing
        /// duplicated). Grants no currency, XP, level, or stage-unlock - only cardCollection
        /// membership. Local/offline only - this is not a trusted or server-verified grant, only
        /// a direct write to the same cardCollection list ShopPresenter.cs already writes to
        /// elsewhere in the game.
        ///
        /// Deliberately never touches activeDeckCardIds. 2026-08-17: an earlier revision of this
        /// method also auto-seeded activeDeckCardIds with this same ten-card set whenever the
        /// saved deck was empty/unusable, which meant Deck Builder opened already sitting at
        /// 10/10 before the player ever pressed anything - removing their agency over the
        /// starting deck and making the Recommended Deck button's own action look like it did
        /// nothing. A fresh player now owns ten valid cards but has no confirmed deck; pressing
        /// Recommended Deck in Deck Builder remains the one, explicit way that deck gets built -
        /// unchanged production behavior this method must not shortcut around.
        /// </summary>
        /// <summary>Public alias for callers outside this file - currently HomePagePresenter's
        /// "To Battle" redirect, which must ensure a fresh player actually owns cards before
        /// sending them into Deck Builder to build a first deck. Calls the exact same entitlement
        /// method the approved tutorial start already uses (see its own doc comment above); no
        /// second grant path is introduced, and idempotency/persistence rules are unchanged - a
        /// player who already owns the full starter set is a no-op, not a repeated write.</summary>
        public void EnsureApprovedStarterCollectionGranted() => GrantApprovedStarterCardsIfMissing();

        private void GrantApprovedStarterCardsIfMissing()
        {
            if (_profile?.cardCollection == null) return;

            // Persisted so the grant survives an app restart - previously this only ever
            // mutated cardCollection in memory, and (per the earlier persistence audit) only
            // ever reached disk as a side effect of some unrelated save firing later in the
            // same session (Shop/DeckBuilder/Collection returning home, or a normal match win).
            // An immediate restart lost it. _profile.Save() (not SaveManager.Save()) is what
            // actually saves the exact instance that just received the grant - see this
            // method's call site comment / Initialize()'s own _profile assignment for why the
            // two are not interchangeable in EditMode.
            bool anyGranted = false;
            foreach (string cardId in ApprovedStarterCollectionCardIds)
            {
                if (!_profile.cardCollection.Contains(cardId))
                {
                    _profile.cardCollection.Add(cardId);
                    anyGranted = true;
                }
            }

            // Only when something actually changed - a retry or a second tutorial start finds
            // every card already owned and must not re-save (idempotent no-op, not a repeated
            // disk write) every time the tutorial is (re)started.
            if (anyGranted)
            {
                _profile.Save();
            }
        }

        /// <summary>
        /// Resolves the player's last-confirmed deck (DeckBuilderPresenter.ConfirmDeck's own
        /// write into activeDeckCardIds) back into real Card references - the exclusive source
        /// of the normal player battle deck. Any defect at all (missing profile/field, a
        /// duplicate id, an id that no longer resolves to a real card, or a count that doesn't
        /// match the current DeckSlotCount) fails the WHOLE deck, not just the offending id -
        /// this must block normal battle (see StartNewMatch's own comment and
        /// _normalMatchStartError), never silently pad, substitute, or skip-and-continue with a
        /// partial/regenerated deck.
        /// </summary>
        private bool TryBuildSavedPlayerDeck(int deckSize, out List<Card> savedDeck)
        {
            savedDeck = new List<Card>();
            if (_profile == null || _profile.activeDeckCardIds == null)
            {
                return false;
            }

            var seenIds = new HashSet<string>();
            foreach (string cardId in _profile.activeDeckCardIds)
            {
                if (string.IsNullOrEmpty(cardId) || !seenIds.Add(cardId))
                {
                    savedDeck.Clear();
                    return false;
                }

                Card card = _cardDatabase != null ? _cardDatabase.GetCard(cardId) : null;
                if (card == null)
                {
                    savedDeck.Clear();
                    return false;
                }

                savedDeck.Add(card);
            }

            return savedDeck.Count == deckSize;
        }

        /// <summary>Set by HomePagePresenter's launch handoff (SetPendingCampaignStageForNextMatch)
        /// immediately before revealing Battle; null for the ordinary "To Battle" path and for
        /// Tutorial (which never touches this field at all - StartApprovedTutorialBattle builds
        /// its own fixed decks directly, bypassing StartNewMatch entirely). Deliberately NOT
        /// cleared after one match: "Play Again"/Retry during a campaign stage must keep facing
        /// the same stage's configured enemy deck, not a random one - it is only replaced or
        /// cleared by the next explicit OnToBattleClicked call (campaign or normal).</summary>
        private CampaignStageData _pendingCampaignStage;

        /// <summary>Campaign-stage battle-configuration contract: the one handoff point a caller
        /// outside this file (HomePagePresenter's launch callback) uses to attach a stage's
        /// enemy-deck configuration to the next normal-path match, before calling
        /// SetBattleCanvasVisible(true). Passing null clears any previous stage (the ordinary "To
        /// Battle" tile always does this).</summary>
        public void SetPendingCampaignStageForNextMatch(CampaignStageData stage) => _pendingCampaignStage = stage;

        /// <summary>
        /// Resolves a Campaign stage's data-defined enemy deck (CampaignStageData.enemyDeckCardIds)
        /// into real Card references - the single validator both the pre-launch check
        /// (IsCampaignStageBattleConfigValid, called before Battle is ever revealed) and the actual
        /// match setup (StartNewMatch) share. Mirrors TryBuildSavedPlayerDeck's own all-or-nothing
        /// rule: a missing/empty list, a duplicate id, or an id CardDatabase cannot resolve fails
        /// the WHOLE deck - never a partial, padded, or randomly-substituted one (requirement 9).
        /// Deliberately does NOT require the list to match the player's current DeckSlotCount -
        /// unlike the player's own deck, a campaign stage's enemy roster is a fixed, authored
        /// composition, not something that should silently grow as the player's Barracks levels up
        /// (requirement 8: stages stay distinct through composition, not scaling).
        /// </summary>
        private bool TryResolveCampaignEnemyDeck(CampaignStageData stage, out List<Card> enemyDeck)
        {
            enemyDeck = new List<Card>();
            if (stage?.enemyDeckCardIds == null || stage.enemyDeckCardIds.Length == 0)
            {
                return false;
            }

            var seenIds = new HashSet<string>();
            foreach (string cardId in stage.enemyDeckCardIds)
            {
                if (string.IsNullOrEmpty(cardId) || !seenIds.Add(cardId))
                {
                    enemyDeck.Clear();
                    return false;
                }

                Card card = _cardDatabase != null ? _cardDatabase.GetCard(cardId) : null;
                if (card == null)
                {
                    enemyDeck.Clear();
                    return false;
                }

                enemyDeck.Add(card);
            }

            return true;
        }

        /// <summary>Exposed for HomePagePresenter's launch callback: whether a stage's enemy-deck
        /// configuration is resolvable right now, so an invalid/missing config can block the
        /// launch BEFORE Battle is ever revealed (requirement 9) rather than being discovered only
        /// once inside StartNewMatch's own defense-in-depth check.</summary>
        public bool IsCampaignStageBattleConfigValid(CampaignStageData stage) =>
            TryResolveCampaignEnemyDeck(stage, out _);

        /// <summary>How much a single Campaign stage attempt costs - launch and retry alike
        /// (Campaign stamina-entry contract, requirements 1 and 4). Not a balancing knob this
        /// task touches beyond satisfying "exactly 1" - no other value is used anywhere.</summary>
        private const int CampaignStaminaCostPerAttempt = 1;

        /// <summary>
        /// The sole Campaign-stamina spend choke point: called from exactly two real sites -
        /// HomePagePresenter.LaunchCampaignStage (initial launch, after every other prerequisite
        /// already passed) and this file's own OnPlayAgainOrRetryPressed (a Campaign retry,
        /// requirement 4 - "a new attempt"). Nothing else spends Campaign Stamina, and this
        /// method itself never touches Battle visibility, campaign context, rewards, or unlocks -
        /// callers decide what to do on failure. CurrencyManager.SpendStamina is the sole
        /// authority (requirement 9): this only decides WHEN 1 Stamina is due, not how the
        /// balance is read, deducted, or persisted.
        /// </summary>
        public bool TrySpendCampaignStaminaForAttempt() =>
            CurrencyManager.SpendStamina(_profile, CampaignStaminaCostPerAttempt);

        /// <summary>
        /// First-time normal-battle entry contract: the one saved-deck validation gate exposed
        /// for callers outside this file - currently HomePagePresenter's "To Battle" tile, which
        /// must know whether a valid confirmed deck exists BEFORE revealing the Battle canvas at
        /// all, not after (see StartNewMatch's own _normalMatchStartError, which only ever fires
        /// once Battle is already visible). Shares TryBuildSavedPlayerDeck exactly as StartNewMatch
        /// does - same missing/duplicate/unresolved/wrong-count rules, not a second, independently
        /// maintained validator. Returns false (redirect-to-Deck-Builder territory) rather than
        /// throwing if called before Initialize() has finished setting up Empire data.
        /// </summary>
        public bool HasValidConfirmedDeckForNormalBattle()
        {
            if (_empireData == null) return false;
            return TryBuildSavedPlayerDeck(_empireData.DeckSlotCount, out _);
        }

        /// <summary>
        /// "Recommended Lineup": simply the strongest cards in the pool.
        ///
        /// 2026-08-05, reported as "the recommended lineup should pick the best card to play".
        /// This used to enforce an even cheap/mid/expensive cost curve with a random pick inside
        /// each band, which made Recommended *weaker* than the plain Reset split, not stronger -
        /// a third of the deck was forced to be cost-1-2 cards (rarity 1-2, the weakest in the
        /// game) while Reset's rarity-fair split handed out mostly rarity 5-7. Measured directly
        /// by a test before this rewrite: 12.9 average Attack+Health versus Reset's 14.9.
        ///
        /// The cost curve was dropped rather than merely reweighted because under the current
        /// economy resource is not a binding constraint: a level-1 player starts on 20 Resource
        /// against a maximum card cost of 7, and a mid-level player on 60. What actually limits
        /// a turn is board space (3 lanes x 3 slots), so the best card for a slot is just the
        /// strongest one - there is no curve to respect. If card costs or the resource formula
        /// are ever retuned so that cost genuinely bites, this needs revisiting.
        /// </summary>
        private static List<Card> BuildStrongestDeck(List<Card> pool, int deckSize)
        {
            // Attack + Health is the whole of a card's raw board presence in this game (there
            // are no other numeric stats), so it's the honest measure of "best" here. Ties break
            // toward the cheaper card, which is strictly better value for the same body.
            return pool
                .OrderByDescending(c => c.Attack + c.Health)
                .ThenBy(c => c.ResourceCost)
                .Take(deckSize)
                .ToList();
        }

        /// <summary>
        /// Splits the pool into a player deck and an enemy deck (sizes may now differ - the
        /// enemy is deliberately pinned to a lower-level, smaller-deck profile, see Initialize()),
        /// balanced by rarity rather than a pure random slice - a plain random split could hand
        /// one side (by chance) noticeably more high-rarity cards than the other, which reads as
        /// "the enemy is just stronger" even though it's sampling variance, not designed
        /// difficulty. Processes rarity tiers highest-first and always feeds whichever side is
        /// proportionally furthest from filling its own target size, so both sides get
        /// comparable *proportional* access to the pool's best cards even at different deck
        /// sizes. Still a placeholder for real deck-building (which cards, not just how many).
        /// </summary>
        private static (List<Card> playerDeck, List<Card> enemyDeck) BuildBalancedDecks(
            List<Card> pool, int playerDeckSize, int enemyDeckSize)
        {
            var playerDeck = new List<Card>();
            var enemyDeck = new List<Card>();

            var rarityTiers = pool.GroupBy(c => c.Rarity).OrderByDescending(g => g.Key);
            foreach (var tier in rarityTiers)
            {
                List<Card> shuffled = tier.ToList();
                Shuffle(shuffled);
                foreach (Card card in shuffled)
                {
                    bool playerFull = playerDeck.Count >= playerDeckSize;
                    bool enemyFull = enemyDeck.Count >= enemyDeckSize;
                    if (playerFull && enemyFull) break;

                    float playerFraction = playerDeckSize > 0 ? (float)playerDeck.Count / playerDeckSize : 1f;
                    float enemyFraction = enemyDeckSize > 0 ? (float)enemyDeck.Count / enemyDeckSize : 1f;

                    if (!playerFull && (enemyFull || playerFraction <= enemyFraction))
                    {
                        playerDeck.Add(card);
                    }
                    else if (!enemyFull)
                    {
                        enemyDeck.Add(card);
                    }
                }
                if (playerDeck.Count >= playerDeckSize && enemyDeck.Count >= enemyDeckSize) break;
            }

            if (playerDeck.Count < playerDeckSize || enemyDeck.Count < enemyDeckSize)
            {
                // Pool too small to give both sides a full disjoint deck at these sizes -
                // fall back to sharing, same simplification the prototype always used.
                return (playerDeck, playerDeck);
            }

            return (playerDeck, enemyDeck);
        }

        // ---------- Canvas / EventSystem ----------

        private Canvas BuildCanvas()
        {
            var canvasGo = new GameObject("Canvas");
            var canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(CanvasWidth, CanvasHeight);
            scaler.matchWidthOrHeight = 0.5f;

            canvasGo.AddComponent<GraphicRaycaster>();

            return canvas;
        }

        /// <summary>
        /// Battle Release Layout pass: the arena backdrop + dim wash, now built as
        /// BattlePresentationRoot's own first children (item 1 of the root's owned chrome)
        /// instead of living directly on Canvas outside any region - previously a sibling of
        /// every real panel rather than a child of one shared root.
        ///
        /// Bug found and fixed in this same pass: neither Image ever set raycastTarget = false.
        /// Unity's GraphicRaycaster hit-tests front-to-back, so a real panel drawn on top of
        /// these still received the tap correctly - but any pixel NOT covered by a later panel
        /// (every inter-region gap; see CreateAnchoredPanel's own comment about the identical bug
        /// once found in per-panel backgrounds) was a live, invisible tap target on a purely
        /// decorative Image. Fixed at the source instead of per-region, matching the fix already
        /// applied to CreateAnchoredPanel's own backgrounds.
        /// </summary>
        private void BuildBattleBackdrop(Transform root)
        {
            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(root, false);
            var bg = bgGo.AddComponent<Image>();
            Sprite backdrop = Resources.Load<Sprite>("UI/Backdrops/Arenas/Lava_Fortress");
            if (backdrop != null)
            {
                bg.sprite = backdrop;
                bg.type = Image.Type.Simple;
                bg.preserveAspect = false; // fill the whole canvas, cropping rather than letterboxing
            }
            else
            {
                bg.sprite = CreateGradientSprite(BackgroundTop, BackgroundBottom);
                bg.type = Image.Type.Simple;
            }
            bg.raycastTarget = false;
            StretchFull(bg.rectTransform);

            if (backdrop != null)
            {
                // The panels' own gradients already add contrast for their own text, but the
                // gaps between panels sit directly over a busy painted scene now instead of a
                // plain dark background - a dim overlay keeps the whole HUD readable over it.
                Image dim = CreateImage(root, new Color(0f, 0f, 0f, 0.35f));
                dim.raycastTarget = false;
                StretchFull(dim.rectTransform);
            }
        }

        private void BuildEventSystem()
        {
            if (EventSystem.current != null) return;
            var esGo = new GameObject("EventSystem");
            esGo.AddComponent<EventSystem>();
            esGo.AddComponent<StandaloneInputModule>();
        }

        // ---------- Panel builders (each its own fixed Y-band) ----------

        private void BuildTitlePanel(Transform canvasTransform, Font font)
        {
            RectTransform panel = CreateBandPanel(canvasTransform, "TitlePanel", Color.clear, TitleY0, TitleY1);
            Text title = CreateText(panel, "Myriad of Dragons", 26, GoldTextColor, GetDisplayFont());
            StretchFull(title.rectTransform);
        }

        /// <summary>
        /// One reusable caption reused for both the approved tutorial's "Ready" (Formation) and
        /// combat-objective (Combat) copy - the two phases are mutually exclusive, so one Text
        /// with its content and visibility set in RefreshPhaseControls covers both without a
        /// second GameObject. Lives in the same TitleY0/Y1 band BuildTitlePanel used to occupy
        /// (see the comment at its Initialize() call site) - already free screen space, nothing
        /// displaced. Hidden by default; only ever shown for a tutorial match.
        /// </summary>
        private void BuildTutorialGuidanceCaption(Transform canvasTransform, Font font)
        {
            RectTransform panel = CreateBandPanel(canvasTransform, "TutorialGuidanceCaption", Color.clear, TitleY0, TitleY1);
            _tutorialGuidanceCaption = CreateText(panel, "", 18, GoldTextColor, font);
            _tutorialGuidanceCaption.fontStyle = FontStyle.Bold;
            StretchFull(_tutorialGuidanceCaption.rectTransform);
            _tutorialGuidanceCaption.gameObject.SetActive(false);
        }

        /// <summary>Generic direct-fraction anchored panel - unlike CreateBandPanel (full width,
        /// Y-only), this takes an arbitrary min/max anchor pair so a region can be placed exactly
        /// as the V3 handoff's own anchor table states it, with no derived math in between.</summary>
        private static RectTransform CreateAnchoredPanel(Transform parent, string name, Color background,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (background.a > 0f)
            {
                var image = go.AddComponent<Image>();
                image.color = background;
                // Decorative tint only - every real call site of this helper builds its actual
                // interactive controls as separate children afterward (Buttons, lane groups,
                // etc.), never relies on this background Image itself catching a tap. Left at
                // the default (true) it silently intercepted taps meant for whatever sits behind
                // it whenever two regions' rendered bounds came closer than their nominal anchors
                // suggested - confirmed root cause of the Back-lane tutorial action becoming
                // untappable once the Hand dock's real (post-layout) bounds crept into the Player
                // board's own bottom row. Fixed at this one shared helper rather than per call
                // site, since every other CreateAnchoredPanel background has the same latent risk.
                image.raycastTarget = false;
            }

            return rect;
        }

        /// <summary>
        /// V4 layout: three independent columns (lane labels / 3x3 boards / lane totals), each
        /// spanning both the enemy and player board zones as one continuous strip of six rows -
        /// this is what keeps a label, its board row, and its total vertically aligned without
        /// duplicating the row-band math three times. Replaces the old titled-panel-with-
        /// centered-rows approach (V3), which is what produced the rejected "huge mostly-empty
        /// tinted panel" and portrait-era BoardRowHeight/BoardRowSpacing constants.
        /// </summary>
        private void BuildBattleBoards(Transform canvasTransform, Font font)
        {
            BuildBattleBoardSide(canvasTransform, font, isEnemySide: true);
            BuildBattleBoardSide(canvasTransform, font, isEnemySide: false);
        }

        /// <summary>
        /// Splits one board zone's vertical span (EnemyBoardMin/Max or PlayerBoardMin/Max) into
        /// exactly 3 row bands per the handoff's "six row bands, each 0.095 screen height, 0.010
        /// separation" spec, scaled to whatever span this zone actually has (both zones share the
        /// same 0.310 span in the V4 table, but this stays correct even if that ever changes).
        /// </summary>
        private static (float top, float bottom)[] ComputeThreeRowBands(float zoneTop, float zoneBottom)
        {
            float span = zoneTop - zoneBottom;
            float rowH = span * (0.095f / 0.305f);
            float gap = span * (0.010f / 0.305f);
            var bands = new (float top, float bottom)[3];
            float cursor = zoneTop;
            for (int i = 0; i < 3; i++)
            {
                bands[i] = (cursor, cursor - rowH);
                cursor -= rowH + gap;
            }
            return bands;
        }

        private void BuildBattleBoardSide(Transform canvasTransform, Font font, bool isEnemySide)
        {
            Vector2 boardMin = isEnemySide ? EnemyBoardMin : PlayerBoardMin;
            Vector2 boardMax = isEnemySide ? EnemyBoardMax : PlayerBoardMax;
            Color accentColor = isEnemySide ? new Color(0.85f, 0.35f, 0.35f) : new Color(0.4f, 0.85f, 0.55f);
            Color rowTint = isEnemySide ? new Color(0.3f, 0.1f, 0.1f, 0.55f) : new Color(0.08f, 0.22f, 0.16f, 0.55f);

            var bands = ComputeThreeRowBands(boardMax.y, boardMin.y);
            Lane[] lanesInOrder = { Lane.Front, Lane.Middle, Lane.Back };

            for (int i = 0; i < 3; i++)
            {
                Lane lane = lanesInOrder[i];
                float top = bands[i].top;
                float bottom = bands[i].bottom;
                string side = isEnemySide ? "Enemy" : "Player";

                // Visual-review fix, 2026-08-17: was Color.clear - bare text floating over the
                // battle backdrop with no framing at all, reported as reading like a raw colour
                // square/text rather than a premium label. Both the lane-label and lane-total
                // columns now sit on the same dark charcoal-plus-bronze-accent plate every other
                // V4 panel (activity rail, spell rail) already uses.
                RectTransform labelBox = CreateAnchoredPanel(canvasTransform, $"LaneLabel_{side}_{lane}",
                    new Color(0.09f, 0.08f, 0.13f, V3PanelAlpha), new Vector2(LaneLabelsMin.x, bottom), new Vector2(LaneLabelsMax.x, top));
                AddBronzeAccentStripe(labelBox);
                BuildLaneLabelContent(labelBox, lane, accentColor, font);

                CreateBoardRow(canvasTransform, lane, font, isEnemySide,
                    new Vector2(boardMin.x, bottom), new Vector2(boardMax.x, top), rowTint,
                    out Transform slots, out Button laneButton);

                RectTransform totalBox = CreateAnchoredPanel(canvasTransform, $"LaneTotal_{side}_{lane}",
                    new Color(0.09f, 0.08f, 0.13f, V3PanelAlpha), new Vector2(LaneTotalsMin.x, bottom), new Vector2(LaneTotalsMax.x, top));
                AddBronzeAccentStripe(totalBox);
                Text total = CreateText(totalBox, "ATK 0\nOVERFLOW 0", 16, Color.white, font);
                total.raycastTarget = false;
                total.alignment = TextAnchor.MiddleCenter;
                total.horizontalOverflow = HorizontalWrapMode.Wrap;
                total.resizeTextForBestFit = true;
                total.resizeTextMinSize = 11;
                total.resizeTextMaxSize = 16;
                AnchorBand(total.rectTransform, 0.05f, 0.95f, 0.06f, 0.06f);

                if (isEnemySide)
                {
                    _enemyLaneSlots[lane] = slots;
                    _enemyLaneButtons[lane] = laneButton;
                    _enemyLaneTotalTexts[lane] = total;
                }
                else
                {
                    _playerLaneSlots[lane] = slots;
                    _playerLaneButtons[lane] = laneButton;
                    _playerLaneTotalTexts[lane] = total;
                }
            }
        }

        /// <summary>Thin bronze top accent stripe - the same "a visibly bordered, less-black
        /// panel reads as part of the shell" cue every other V4 panel already uses (see
        /// BuildActivityRail's own railAccent). Applied to the lane-label and lane-total plates
        /// too so they read as premium framed columns, not bare text over the backdrop.</summary>
        private static void AddBronzeAccentStripe(RectTransform panel)
        {
            Image accent = CreateImage(panel, AccentBorderColor);
            accent.rectTransform.anchorMin = new Vector2(0f, 1f);
            accent.rectTransform.anchorMax = new Vector2(1f, 1f);
            accent.rectTransform.pivot = new Vector2(0.5f, 1f);
            accent.rectTransform.sizeDelta = new Vector2(0f, 3f);
            accent.rectTransform.anchoredPosition = Vector2.zero;
            accent.raycastTarget = false;
        }

        /// <summary>Icon badge + lane name + runtime lane modifier, centered to fit the narrow
        /// Lane labels column (.015-.165) without overlapping the board.</summary>
        private void BuildLaneLabelContent(RectTransform labelBox, Lane lane, Color accentColor, Font font)
        {
            // A small, fixed-size circular medallion - not a swatch stretched across most of the
            // column's own width, which is what read as "a raw colour square" rather than a lane
            // icon in visual review.
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(labelBox, false);
            var iconRect = (RectTransform)iconGo.transform;
            iconRect.anchorMin = iconRect.anchorMax = new Vector2(0.5f, 0.82f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);
            iconRect.sizeDelta = new Vector2(26f, 26f);
            iconRect.anchoredPosition = Vector2.zero;
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = CreateRoundedGradientSprite(accentColor, accentColor, 24, 13);
            icon.type = Image.Type.Sliced;
            icon.raycastTarget = false;

            Text nameText = CreateText(labelBox, lane.ToString().ToUpperInvariant(), 19, GoldTextColor, font);
            nameText.fontStyle = FontStyle.Bold;
            nameText.alignment = TextAnchor.MiddleCenter;
            nameText.raycastTarget = false;
            nameText.horizontalOverflow = HorizontalWrapMode.Wrap;
            AnchorBand(nameText.rectTransform, 0.42f, 0.65f, 0.04f, 0.04f);

            string bonus = LaneBonusLabel(lane);
            Text modifierText = CreateText(labelBox, bonus, 14, accentColor, font);
            modifierText.alignment = TextAnchor.MiddleCenter;
            modifierText.raycastTarget = false;
            modifierText.horizontalOverflow = HorizontalWrapMode.Wrap;
            AnchorBand(modifierText.rectTransform, 0.20f, 0.42f, 0.04f, 0.04f);
        }

        /// <summary>
        /// One board row: exactly LaneState.MaxSlots card slots inside the given normalized
        /// (min,max) anchor rect, tinted and (for the player side) directly tappable for
        /// placement/reinforcement - same click/slot contract RefreshLaneSlots already expects,
        /// just anchored to a fixed row band instead of centered inside a titled panel.
        /// </summary>
        private void CreateBoardRow(Transform canvasTransform, Lane lane, Font font, bool isEnemySide,
            Vector2 anchorMin, Vector2 anchorMax, Color rowTint,
            out Transform slotsContainer, out Button laneButton)
        {
            RectTransform rowBg = CreateAnchoredPanel(canvasTransform,
                $"BoardRow_{(isEnemySide ? "Enemy" : "Player")}_{lane}", rowTint, anchorMin, anchorMax);

            var slotsGo = new GameObject($"Lane_{lane}", typeof(RectTransform));
            slotsGo.transform.SetParent(rowBg, false);
            var slotsRect = (RectTransform)slotsGo.transform;
            slotsRect.anchorMin = new Vector2(0f, 0f);
            slotsRect.anchorMax = new Vector2(1f, 1f);
            slotsRect.offsetMin = new Vector2(8f, 8f);
            slotsRect.offsetMax = new Vector2(-8f, -8f);

            // Fully transparent, not invisible/absent - an Image with alpha 0 still receives
            // clicks, which is what keeps the row tappable without painting a visible band on
            // top of the rowBg tint set above.
            var slotsImage = slotsGo.AddComponent<Image>();
            slotsImage.color = new Color(0, 0, 0, 0f);

            var button = slotsGo.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            if (!isEnemySide)
            {
                button.onClick.AddListener(() => OnLanePressed(lane));
            }
            else
            {
                // Interactive (Firestorm targets an ENEMY lane) but not interactable by default -
                // ArmSpellTargeting is what turns a lane on, only while a damage spell is armed,
                // only for the lanes it can actually hit.
                button.interactable = false;
            }

            var slotsLayout = slotsGo.AddComponent<HorizontalLayoutGroup>();
            slotsLayout.spacing = BoardSlotSpacing;
            slotsLayout.childAlignment = TextAnchor.MiddleCenter;
            slotsLayout.childForceExpandWidth = false;
            slotsLayout.childForceExpandHeight = true;
            slotsLayout.childControlWidth = true;
            slotsLayout.childControlHeight = true;

            slotsContainer = slotsGo.transform;
            laneButton = button;
        }

        /// <summary>
        /// Text-only description of the bonus BattleController.TryPlayCard actually applies to
        /// this lane (Part II §2.3) - kept in this one place so the label can never drift out
        /// of sync with the real bonus values the way a hardcoded string per lane could.
        /// </summary>
        private static string LaneBonusLabel(Lane lane) => lane switch
        {
            Lane.Front => "+1 ATK",
            Lane.Middle => "+1 HP",
            _ => "",
        };

        /// <summary>Approved tutorial-only copy (Command Centre decision, 2026-08-15) for the
        /// lane picker overlay - see RefreshLanePicker's own comment on why this is appended,
        /// not a replacement, of the existing lane-bonus/slot line.</summary>
        private static string TutorialLaneGuidance(Lane lane) => lane switch
        {
            Lane.Front => "Front: Place cards in the Front row.",
            Lane.Middle => "Middle: Place cards in the Middle row.",
            Lane.Back => "Back: Place cards in the Back row.",
            _ => "",
        };

        /// <summary>
        /// Text description of what BattleController.ApplyOnPlayClassHook and
        /// BattleCardInstance's Taunt check actually do for this card's Class, kept in one
        /// place for the same reason as LaneBonusLabel - so the shown description can't drift
        /// out of sync with the real behavior.
        /// </summary>
        private static string CardSkillDescription(Card card) => card.Class switch
        {
            CardClass.Warrior => "Warrior: frontline fighter (+1 Attack already included above).",
            CardClass.Knight => "Knight: Taunt - absorbs incoming lane damage before other cards.",
            CardClass.Strategist => "Strategist: On Play - draw a card.",
            CardClass.Perfect => "Perfect: On Play in the Back lane - draw a card (no bonus in Front/Middle).",
            _ => "",
        };

        /// <summary>Maps a card's 1-7 Rarity onto the 4 supplied frame tiers (Resources.Load
        /// caches internally, so repeated calls for the same rarity are cheap). Returns null if
        /// the asset isn't present, so callers can fall back to the procedural frame instead of
        /// silently rendering a blank Image.</summary>
        private static Sprite GetRarityFrameSprite(int rarity)
        {
            string tier = rarity switch
            {
                <= 2 => "Common_Card_Frame",
                <= 4 => "Rare_Card_Frame",
                <= 6 => "Epic_Card_Frame",
                _ => "Legendary_Card_Frame",
            };
            return Resources.Load<Sprite>($"UI/Frames/{tier}");
        }

        /// <summary>
        /// Asset audit, 2026-08-18 (docs/Battle_Screen_Landscape_Asset_Audit_2026-08-18.md):
        /// Common/Rare/Epic_Card_Frame.png are 340x460 (0.739 w/h); Legendary_Card_Frame.png is
        /// 400x460 (0.870 w/h) - a genuinely different, wider aspect, not an authoring mistake.
        /// Every card renderer that used to force ALL rarities into one fixed box (the "flattened/
        /// stretched" defect visual review flagged) now sizes each card tile at ITS OWN frame's
        /// real aspect instead. Falls back to Common's aspect if the sprite failed to load, same
        /// as GetRarityFrameSprite's own fallback tier.
        /// </summary>
        private static float GetRarityFrameAspect(int rarity) => rarity switch
        {
            <= 6 => 340f / 460f,
            _ => 400f / 460f, // Legendary
        };

        /// <summary>
        /// V4 Top HUD: a background strip (TopHudMin/Max) with three independently-anchored
        /// clusters inside it - Player HUD (left), Phase HUD (center), Enemy HUD (right), each
        /// its own fixed normalized rect per the handoff table rather than fractions of one
        /// shared panel. Reserved for identity/HP/resource/phase only, per the handoff's
        /// collision-safe rule that no board/guide/tooltip/combat text may render into it.
        /// </summary>
        private void BuildHeaderBar(Transform canvasTransform, Font font)
        {
            CreateAnchoredPanel(canvasTransform, "TopHud",
                new Color(0.05f, 0.04f, 0.07f, V3PanelAlpha), TopHudMin, TopHudMax);

            // ----- Player cluster (left) -----
            RectTransform playerCluster = CreateAnchoredPanel(canvasTransform, "PlayerHud", Color.clear,
                PlayerHudMin, PlayerHudMax);
            CreatePortrait(playerCluster, "Paladin", 0.06f, 0.94f, 0.02f, 0.30f, "Lightbringer", font,
                out _, out _);

            Image playerHealthBar = CreateBar(playerCluster, HealthBarEmptyColor, PlayerHealthBarFillColor, font, 20,
                out _playerHealthFill, out _playerAvatarText, "Health_Empty");
            AnchorBand(playerHealthBar.rectTransform, 0.56f, 0.86f, 0.34f, 0.02f);

            Image playerResourceBar = CreateBar(playerCluster, ResourceBarEmptyColor, ResourceBarFillColor, font, 20,
                out _resourceFill, out _resourceText, "Mana_Fill");
            AnchorBand(playerResourceBar.rectTransform, 0.30f, 0.54f, 0.34f, 0.02f);

            _handCountText = CreateText(playerCluster, "", 18, GoldTextColor, font);
            AnchorBand(_handCountText.rectTransform, 0.08f, 0.28f, 0.34f, 0.02f);

            // ----- Phase cluster (center) -----
            // _turnText already carries the combined phase/clash string exactly as RefreshAll
            // sets it today ("Formation" / "Clash N/12" / "REINFORCE! N/12") - shown large and
            // centered here, matching the mockup's "FORMATION / CLASH 0/12" stack, without
            // splitting RefreshAll's single string across two fields it was never designed to
            // populate separately.
            RectTransform phaseCluster = CreateAnchoredPanel(canvasTransform, "PhaseHud", Color.clear,
                PhaseHudMin, PhaseHudMax);
            _turnText = CreateText(phaseCluster, "", 26, GoldTextColor, font);
            _turnText.fontStyle = FontStyle.Bold;
            StretchFull(_turnText.rectTransform);

            // ----- Enemy cluster (right) -----
            RectTransform enemyCluster = CreateAnchoredPanel(canvasTransform, "EnemyHud", Color.clear,
                EnemyHudMin, EnemyHudMax);
            CreatePortrait(enemyCluster, "Orc_King", 0.06f, 0.94f, 0.02f, 0.30f, _aiProfile.DisplayName, font,
                out _, out _enemyNameLabel);

            Image enemyHealthBar = CreateBar(enemyCluster, HealthBarEmptyColor, HealthBarFillColor, font, 20,
                out _enemyHealthFill, out _enemyAvatarText, "Health_Empty");
            AnchorBand(enemyHealthBar.rectTransform, 0.56f, 0.86f, 0.34f, 0.02f);

            Image enemyResourceBar = CreateBar(enemyCluster, ResourceBarEmptyColor, ResourceBarFillColor, font, 20,
                out _enemyResourceFill, out _enemyResourceText, "Mana_Fill");
            AnchorBand(enemyResourceBar.rectTransform, 0.14f, 0.44f, 0.34f, 0.02f);
        }

        /// <summary>A background+fill bar with a bold centered text label on top, for HP/Resource -
        /// matches the "health bar with a number on it" look instead of plain text.</summary>
        /// <summary>
        /// A square Avatar portrait with the circular frame ring overlaid on top - matches the
        /// reference images' "portrait in top corner" convention. The frame is a ring (real
        /// transparency in its center hole and outside the ring itself, not a filled disc), so
        /// this isn't a true circular mask on the portrait underneath - the portrait's square
        /// corners can peek out slightly past the ring in the gaps. Accepted as a reasonable
        /// approximation without a filled-circle mask asset; a real mask would need one.
        /// Falls back to rendering nothing if the named portrait isn't found under
        /// Resources/UI/Portraits, rather than showing a broken/blank Image.
        /// </summary>
        private static void CreatePortrait(Transform parent, string portraitName, float y0, float y1, float x0, float x1,
            string displayName, Font nameFont, out RectTransform portraitBox, out Text nameLabelOut)
        {
            Sprite portraitSprite = Resources.Load<Sprite>($"UI/Portraits/{portraitName}");
            if (portraitSprite == null) { portraitBox = null; nameLabelOut = null; return; }

            var portraitGo = new GameObject($"Portrait_{portraitName}", typeof(RectTransform));
            portraitGo.transform.SetParent(parent, false);
            var portraitRect = (RectTransform)portraitGo.transform;
            portraitRect.anchorMin = new Vector2(x0, y0);
            portraitRect.anchorMax = new Vector2(x1, y1);
            portraitRect.offsetMin = new Vector2(4, 2);
            portraitRect.offsetMax = new Vector2(-4, -2);
            portraitBox = portraitRect;

            // REGRESSION FIXED 2026-08-06. The previous revision put an AspectRatioFitter
            // directly on portraitGo to force it square. AspectRatioFitter sizes a
            // RectTransform relative to its *parent*, not to its own anchors - so FitInParent
            // resized the portrait against the whole panel and completely discarded the
            // anchors above it. The result was an enormous portrait parked in the middle of
            // the board, sitting on top of the lane slots. That is what was covering the
            // deployed cards ("I can't see what I deploy at all") and what put the Avatar in
            // the centre of the screen. The fitter has to live on an inner child, so it fits
            // *this* anchored box rather than the panel.
            var innerGo = new GameObject("PortraitSquare", typeof(RectTransform));
            innerGo.transform.SetParent(portraitGo.transform, false);
            var innerRect = (RectTransform)innerGo.transform;
            innerRect.anchorMin = new Vector2(0.5f, 0.5f);
            innerRect.anchorMax = new Vector2(0.5f, 0.5f);
            innerRect.pivot = new Vector2(0.5f, 0.5f);
            innerRect.anchoredPosition = Vector2.zero;

            var portraitImg = innerGo.AddComponent<Image>();
            portraitImg.sprite = portraitSprite;
            // Asset audit, 2026-08-18: was forced to fill a square box exactly (preserveAspect
            // false against a 1:1 AddSquareFitter) regardless of the portrait's own real aspect -
            // Portraits/Paladin.png (0.691 w/h) and Portraits/Orc_King.png (0.800) are not the
            // same shape as each other, so the old approach stretched at least one of them.
            // preserveAspect fits within the box instead - see the box-aspect comment below for
            // why the box itself is no longer forced to 1:1 either.
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;

            Sprite frameSprite = Resources.Load<Sprite>("UI/Frames/Avatar_Circle_Frame");
            if (frameSprite != null)
            {
                // Box sized to the FRAME's own real aspect (400x500 = 0.8 w/h, asset audit
                // 2026-08-18), not a hardcoded 1:1 square - the frame art is a ring with corner
                // gem ornaments extending its canvas taller than it is wide, and forcing that
                // into a square visibly ovaled the ring (a plain circle is one of the more
                // noticeable shapes to distort). Stretching zero, not "less" - the frame now
                // renders at its own native aspect exactly.
                AddAspectFitter(innerGo, 400f / 500f);

                var frameGo = new GameObject("Frame", typeof(RectTransform));
                frameGo.transform.SetParent(innerGo.transform, false);
                var frameImg = frameGo.AddComponent<Image>();
                frameImg.sprite = frameSprite;
                frameImg.preserveAspect = false;
                frameImg.raycastTarget = false;
                StretchFull((RectTransform)frameGo.transform);
            }
            else
            {
                // No frame asset to size the box against - fall back to the portrait's own
                // aspect so at least the portrait itself never stretches.
                float portraitAspect = portraitSprite.rect.height > 0f
                    ? portraitSprite.rect.width / portraitSprite.rect.height
                    : 1f;
                AddAspectFitter(innerGo, portraitAspect);
            }

            if (string.IsNullOrEmpty(displayName)) { nameLabelOut = null; return; }

            // Name label with no plate behind it - "the image should be a PNG whereby the
            // rectangle background should not be showing" (2026-08-06). A dark rectangle behind
            // a circular portrait is exactly the boxiness being designed out; an outline keeps
            // the text readable over the arena without drawing a box to do it.
            Text nameLabel = CreateText(parent, displayName.ToUpperInvariant(), 14,
                GoldTextColor, nameFont ?? GetDefaultFont());
            nameLabel.fontStyle = FontStyle.Bold;
            nameLabel.raycastTarget = false;
            nameLabel.rectTransform.anchorMin = new Vector2(x0, y0 - 0.13f);
            nameLabel.rectTransform.anchorMax = new Vector2(x1, y0 - 0.01f);
            nameLabel.rectTransform.offsetMin = Vector2.zero;
            nameLabel.rectTransform.offsetMax = Vector2.zero;

            var outline = nameLabel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.95f);
            outline.effectDistance = new Vector2(1.6f, -1.6f);

            nameLabelOut = nameLabel;
        }

        /// <summary>
        /// The actual "template for animation" this was built for: spawns a temporary sprite
        /// centered in `parent`, scales up slightly while fading out over `duration`, then
        /// destroys itself. PlayFloatingText below reuses the exact same fade/destroy coroutine
        /// for combat numbers - both are meant as a starting pattern other effects (buffs,
        /// debuffs, more VFX from Resources/UI/VFX) can copy rather than a finished VFX system.
        /// </summary>
        private void PlayEffect(Transform parent, Sprite sprite, Vector2 anchoredPosition, float size, float duration)
        {
            if (sprite == null || parent == null) return;

            var go = new GameObject("Effect", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size, size);
            rect.anchoredPosition = anchoredPosition;

            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.raycastTarget = false;
            image.preserveAspect = true;

            StartCoroutine(FadeScaleAndDestroy(go, image, duration, growTo: 1.3f));
        }

        /// <summary>Floating combat number (damage/heal) - rises and fades over the lane it's
        /// triggered from, using the same animation template as PlayEffect.</summary>
        private void PlayFloatingText(Transform parent, string text, Color color, float duration = 1.1f)
        {
            if (parent == null) return;

            var go = new GameObject("FloatingText", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(140, 40);
            rect.anchoredPosition = Vector2.zero;

            Text label = CreateText(go.transform, text, 20, color, GetDisplayFont());
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            StretchFull(label.rectTransform);

            StartCoroutine(RiseFadeAndDestroy(go, label, duration));
        }

        private static IEnumerator FadeScaleAndDestroy(GameObject go, Image image, float duration, float growTo)
        {
            Color startColor = image.color;
            float elapsed = 0f;
            while (elapsed < duration && go != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                image.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
                go.transform.localScale = Vector3.one * Mathf.Lerp(1f, growTo, t);
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        private static IEnumerator RiseFadeAndDestroy(GameObject go, Text label, float duration)
        {
            var rect = (RectTransform)go.transform;
            Color startColor = label.color;
            float elapsed = 0f;
            while (elapsed < duration && go != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                label.color = new Color(startColor.r, startColor.g, startColor.b, Mathf.Lerp(1f, 0f, t));
                rect.anchoredPosition = new Vector2(0f, Mathf.Lerp(0f, 40f, t));
                yield return null;
            }
            if (go != null) Destroy(go);
        }

        // ---------- Card animations (idle shimmer / slide into lane) ----------

        /// <summary>
        /// A slow, continuous breathing pulse on a hand card, so the hand looks alive rather
        /// than like a static row of images. Deliberately subtle (a 3% scale swing over ~2.4s):
        /// this runs on every card in hand simultaneously and forever, so anything more
        /// pronounced reads as distracting flicker rather than polish.
        ///
        /// `phaseOffset` staggers each card so they don't all pulse in lockstep, which looks
        /// mechanical - the hand should shimmer like a fan of cards, not blink as one block.
        ///
        /// Coroutines only run in Play Mode, so this is a no-op (and correctly skipped) when
        /// the EditMode test suite drives Initialize()/RefreshAll() - see StartCardShimmer.
        /// </summary>
        private static IEnumerator ShimmerCard(GameObject card, float phaseOffset)
        {
            var rect = (RectTransform)card.transform;
            const float period = 2.4f;
            const float amplitude = 0.03f;

            while (card != null)
            {
                float t = ((Time.time + phaseOffset) % period) / period;
                float scale = 1f + amplitude * Mathf.Sin(t * Mathf.PI * 2f);
                rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
        }

        /// <summary>
        /// Starts <see cref="ShimmerCard"/> only when a coroutine can actually run. Application
        /// .isPlaying is checked explicitly because StartCoroutine throws outside Play Mode, and
        /// RefreshHand() is reachable from the EditMode tests via Initialize().
        /// </summary>
        private void StartCardShimmer(GameObject card, int handIndex)
        {
            if (!Application.isPlaying) return;
            StartCoroutine(ShimmerCard(card, phaseOffset: handIndex * 0.35f));
        }

        /// <summary>
        /// Slides the just-played card from the hand up into its lane slot, instead of it simply
        /// vanishing from the hand and appearing on the board. RefreshAll() has already rebuilt
        /// the lane by the time this runs, so this animates the *newly created* slot display
        /// (the last real card in that lane) from an offset start position back to its resting
        /// place - which is why it's called after RefreshAll(), not before.
        /// </summary>
        private void SlideNewestCardIntoLane(Lane lane)
        {
            if (!Application.isPlaying) return;

            Transform container = _playerLaneSlots[lane];
            int cardCount = _battleController.PlayerState.Lanes[lane].Cards.Count;
            if (container == null || cardCount <= 0 || cardCount > container.childCount) return;

            // Occupied slots are rendered before the empty-slot filler (see RefreshLaneSlots),
            // so the newest real card is at index cardCount - 1.
            Transform newest = container.GetChild(cardCount - 1);
            StartCoroutine(SlideIn((RectTransform)newest, from: new Vector2(0f, -140f), duration: 0.28f));
        }

        /// <summary>
        /// Punches the chosen card's scale up and settles it back, so picking a card has a
        /// visible reaction instead of only a colour change. Finds the card by index in the
        /// current hand because RefreshAll() destroys and rebuilds every hand button, so any
        /// reference captured before the refresh is already dead.
        /// </summary>
        private void PopSelectedHandCard(Card card)
        {
            if (!Application.isPlaying) return;

            int index = _battleController.PlayerState.Hand.IndexOf(card);
            if (index < 0 || index >= _handButtons.Count) return;

            StartCoroutine(PopScale((RectTransform)_handButtons[index].transform, peak: 1.18f, duration: 0.26f));
        }

        private static IEnumerator PopScale(RectTransform rect, float peak, float duration)
        {
            float elapsed = 0f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Up then back down over the duration - Sin(t * PI) peaks at the midpoint and
                // returns to 0 at both ends, so the card lands back at its resting size.
                float scale = 1f + (peak - 1f) * Mathf.Sin(t * Mathf.PI);
                rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }
            if (rect != null) rect.localScale = Vector3.one;
        }

        private static IEnumerator SlideIn(RectTransform rect, Vector2 from, float duration)
        {
            Vector2 destination = rect.anchoredPosition;
            var canvasGroup = rect.gameObject.AddComponent<CanvasGroup>();

            float elapsed = 0f;
            while (elapsed < duration && rect != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // Ease-out: fast arrival then settle, which reads as the card being placed
                // rather than drifting into position at a constant speed.
                float eased = 1f - Mathf.Pow(1f - t, 3f);
                rect.anchoredPosition = Vector2.Lerp(destination + from, destination, eased);
                canvasGroup.alpha = eased;
                yield return null;
            }

            if (rect != null)
            {
                rect.anchoredPosition = destination;
                canvasGroup.alpha = 1f;
            }
        }

        /// <summary>
        /// A health/resource bar. `fillArtName` names a sprite under Resources/UI/Bars - pass
        /// null for the procedural fallback.
        ///
        /// 2026-08-06: the uploaded bar art is finally in use ("the mana and health bar are
        /// still missing, do use the asset from the folder"). It was held back for two rounds
        /// because every one of those PNGs has its own name painted across it - "FULL HEALTH
        /// BAR", "MANA BAR". Rather than keep refusing them, each was rebuilt by compositing its
        /// ornate left cap and arrow right cap around a slice taken from the clear right-hand
        /// stretch, which skips the caption entirely. See Resources/UI/Bars.
        /// </summary>
        private Image CreateBar(Transform parent, Color emptyColor, Color fillColor, Font font, int fontSize,
            out Image fill, out Text label, string fillArtName = null)
        {
            var bgGo = new GameObject("Bar", typeof(RectTransform));
            bgGo.transform.SetParent(parent, false);
            Image bg = bgGo.AddComponent<Image>();

            Sprite emptyArt = fillArtName == null ? null : Resources.Load<Sprite>("UI/Bars/Health_Empty");
            if (emptyArt != null)
            {
                bg.sprite = emptyArt;
                bg.color = Color.white;
            }
            else
            {
                bg.sprite = CreateRoundedGradientSprite(emptyColor, emptyColor, cornerRadius: 16);
            }
            bg.type = Image.Type.Sliced;

            // Mask clips children to this Image's alpha - the safe way to combine a shaped
            // background with a Type.Filled fill: without it, the fill's square corners would
            // poke past the shaped edge at high fill amounts.
            bgGo.AddComponent<Mask>().showMaskGraphic = true;

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(bg.transform, false);
            fill = fillGo.AddComponent<Image>();

            Sprite fillArt = fillArtName == null ? null : Resources.Load<Sprite>($"UI/Bars/{fillArtName}");
            if (fillArt != null)
            {
                fill.sprite = fillArt;
                fill.color = Color.white; // the art carries its own colour; tinting would mute it
            }
            else
            {
                fill.color = fillColor;
            }

            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            fill.fillAmount = 1f;
            fill.raycastTarget = false;
            StretchFull((RectTransform)fillGo.transform);

            label = CreateText(bg.transform, "", fontSize, Color.white, font);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            // Outlined so the value stays readable over the bar art's own highlights, which are
            // much busier than the flat procedural fill it replaced.
            var labelOutline = label.gameObject.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            labelOutline.effectDistance = new Vector2(1.4f, -1.4f);
            StretchFull(label.rectTransform);

            return bg;
        }

        /// <summary>
        /// V3's hand and placement panel (handoff anchor: (.02,.02)-(.70,.23)). Left: a passive
        /// "Selected Card / Place In" status box mirroring the mockup, plus deck count and the
        /// synergy line (both existing data, moved out of the old status row) and the existing
        /// Reset/Recommended Lineup controls tucked into its corner - relocated, not removed or
        /// duplicated. Right: the hand row - same CreateCardButton/RefreshHand logic as before,
        /// only the container moved.
        /// </summary>
        private void BuildHandAndPlacementPanel(Transform canvasTransform, Font font)
        {
            // Corrective fix, 2026-08-16 ("purple hand dock overlaps the Player Back lane"): the
            // panel's own background used to be painted at the FULL HandPanelMin-Max rect
            // (Color.clear here instead), whose nominal top edge (y=0.23) sat only ~22px below
            // the Player board zone's own nominal bottom edge (y=0.25) at 1080p - real content in
            // both zones (see BuildBattleBoardSide's own row-band math) could close that gap
            // entirely. The visible purple tint is now a separate, explicitly inset child
            // (12% of this panel's own height, ~25px) rather than the panel's own full-rect
            // background, so there is a real, deliberate buffer between the painted color and the
            // Player board above it - functional content (hand row, placement box, buttons below)
            // keeps its existing anchors unchanged, only the decorative tint shrank away from the
            // top edge. raycastTarget=false either way (see CreateAnchoredPanel's own comment) -
            // this is defense in depth on the geometry itself, not the input-blocking half of the
            // bug, which is already fixed at the shared helper.
            RectTransform panel = CreateAnchoredPanel(canvasTransform, "HandAndPlacementPanel",
                Color.clear, HandPanelMin, HandPanelMax);
            _handAndPlacementPanelRect = panel;
            RectTransform panelBackground = CreateAnchoredPanel(panel, "HandAndPlacementPanelBackground",
                WithAlpha(HandPanelTop, V3PanelAlpha), new Vector2(0f, 0f), new Vector2(1f, 0.88f));
            panelBackground.transform.SetAsFirstSibling(); // stays behind every real control built below
            HandDockBackgroundImageForTests = panelBackground.GetComponent<Image>();

            // Left status box - text only. Never itself a placement control: FRONT/MIDDLE/BACK
            // here are a passive reminder of the selection already made via OnHandCardPressed/
            // OnLanePressed (existing input routes), not a second way to place a card - adding
            // tappable buttons here would be a new control the handoff explicitly forbids.
            RectTransform placementBox = CreateAnchoredPanel(panel, "SelectedCardBox",
                new Color(0f, 0f, 0f, 0.35f), new Vector2(0.005f, 0.30f), new Vector2(0.15f, 0.98f));
            _selectedCardText = CreateText(placementBox, "", 16, GoldTextColor, font);
            _selectedCardText.raycastTarget = false;
            _selectedCardText.alignment = TextAnchor.UpperCenter;
            _selectedCardText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _selectedCardText.resizeTextForBestFit = true;
            _selectedCardText.resizeTextMinSize = 10;
            _selectedCardText.resizeTextMaxSize = 16;
            StretchFull(_selectedCardText.rectTransform);

            _deckCountText = CreateText(panel, "", 18, GoldTextColor, font);
            AnchorBand(_deckCountText.rectTransform, 0.06f, 0.28f, 0.005f, 0.85f);
            _deckCountText.alignment = TextAnchor.MiddleLeft;

            _synergyText = CreateText(panel, "", 16, SelectedColor, font);
            _synergyText.alignment = TextAnchor.MiddleLeft;
            _synergyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _synergyText.verticalOverflow = VerticalWrapMode.Truncate;
            _synergyText.resizeTextForBestFit = true;
            _synergyText.resizeTextMinSize = 11;
            _synergyText.resizeTextMaxSize = 16;
            AnchorBand(_synergyText.rectTransform, 0.0f, 0.05f, 0.005f, 0.85f);

            // Existing Reset/Recommended Lineup mechanic (see the original BuildLineupButtons'
            // own comment for why these must keep working) - compact, tucked into this panel's
            // own top-left corner rather than a separate always-on-screen band.
            _resetLineupButton = CreateButton(panel, "Reset", font, () => OnLineupButtonPressed(useRecommendedDeck: false));
            _resetLineupButton.GetComponentInChildren<Text>().fontSize = 14;
            RectTransform resetRect = _resetLineupButton.GetComponent<RectTransform>();
            resetRect.anchorMin = new Vector2(0.005f, 0.98f);
            resetRect.anchorMax = new Vector2(0.005f, 0.98f);
            resetRect.pivot = new Vector2(0f, 1f);
            resetRect.sizeDelta = new Vector2(78f, 26f);
            resetRect.anchoredPosition = Vector2.zero;

            // Release feature: this control is Auto Formation now, not "Recommended" - same
            // Button/GameObject/slot (no new control created), relabeled and repointed to
            // OnAutoFormationPressed. OnLineupButtonPressed(useRecommendedDeck: true) and
            // AutoDeployRecommendedFormation are deliberately left fully intact and unreferenced
            // by any control - BattleLogicTests still exercises them directly via
            // UseRecommendedLineupForTests(), which must keep passing unchanged.
            _recommendedLineupButton = CreateButton(panel, "AUTO FORMATION", font, OnAutoFormationPressed);
            _recommendedLineupButton.GetComponentInChildren<Text>().fontSize = 14;
            RectTransform recRect = _recommendedLineupButton.GetComponent<RectTransform>();
            recRect.anchorMin = new Vector2(0.005f, 0.98f);
            recRect.anchorMax = new Vector2(0.005f, 0.98f);
            recRect.pivot = new Vector2(0f, 1f);
            recRect.sizeDelta = new Vector2(110f, 26f);
            recRect.anchoredPosition = new Vector2(82f, 0f);

            // Centered, not left-packed - a short hand (e.g. the tutorial's 3 cards) used to leave
            // a wide empty gap to its right when packed against the left edge ("giant empty
            // panel"); centering makes a small hand read as intentionally compact instead.
            var rowGo = new GameObject("HandRow", typeof(RectTransform));
            rowGo.transform.SetParent(panel, false);
            var rowRect = (RectTransform)rowGo.transform;
            rowRect.anchorMin = new Vector2(0.16f, 0f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.offsetMin = new Vector2(8, 4);
            rowRect.offsetMax = new Vector2(-8, -4);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 14f;
            rowLayout.childAlignment = TextAnchor.MiddleCenter;
            rowLayout.childForceExpandWidth = false;
            // 2026-08-16, real-raycast investigation: with this false, the layout group only
            // ever used each card's LayoutElement.preferredWidth (130, from
            // CreateCardButton/SetPreferredWidth) to compute row *spacing*, but never actually
            // wrote it onto the card's own RectTransform - confirmed directly (a real
            // GraphicRaycaster query at a hand card's own reported world-corner center found no
            // hit at all; a manual sweep of every raycastable Graphic under canvas showed the
            // card's real rect was still Unity's untouched 100x100 default, not 130x196 - the
            // cards had silently never been their intended size). true is what makes the layout
            // group actually apply that preferred size to the child's rect - same fix, same root
            // cause, as CreateBoardRow's childControlHeight earlier this session.
            rowLayout.childControlWidth = true;
            rowLayout.childControlHeight = true;
            rowLayout.childForceExpandHeight = true;
            _handRow = rowRect;

            // Hint text (affordability/placement rejection messages - ShowLaneHint) anchored
            // over the placement box, the one part of this panel not already busy with cards.
            _handHintText = CreateText(placementBox, "", 14, new Color(0.85f, 0.6f, 0.4f), font);
            _handHintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _handHintText.resizeTextForBestFit = true;
            _handHintText.resizeTextMinSize = 10;
            _handHintText.resizeTextMaxSize = 14;
            StretchFull(_handHintText.rectTransform);
            _handHintText.gameObject.SetActive(false);
        }

        /// <summary>
        /// V3's primary action region (handoff anchor: (.72,.02)-(.98,.18)) - just the single
        /// large START BATTLE button now; the spell bar that used to share this band moved to the
        /// activity rail (see BuildActivityRail), since the rail is where the mockup actually
        /// puts spells and this region is Formation-only per the handoff ("Existing combat/result
        /// state determines its later presentation; do not add actions").
        /// </summary>
        private void BuildPrimaryActionAndSpells(Transform canvasTransform, Font font)
        {
            RectTransform panel = CreateAnchoredPanel(canvasTransform, "PrimaryActionPanel", Color.clear,
                PrimaryActionMin, PrimaryActionMax);

            // Visual-review fix, 2026-08-16: the external "play match button" sprite (designed
            // for a small ~220x60 button) was being stretched to fill this entire ~499x173 region
            // (StretchFull below), which rendered as a plain white/blank rectangle at that scale
            // rather than a readable button. Dropped the external-sprite override entirely and
            // kept the standard procedural gradient pill CreateButton already uses everywhere else
            // in this file (Reset/Recommended, spell rows, etc.) - proven, never white, and sized
            // with real margin inside its region instead of edge-to-edge stretch.
            _primaryActionButton = CreateButton(panel, "START BATTLE", font, OnPrimaryActionPressed);
            RectTransform primaryRect = _primaryActionButton.GetComponent<RectTransform>();
            primaryRect.anchorMin = new Vector2(0.5f, 0.5f);
            primaryRect.anchorMax = new Vector2(0.5f, 0.5f);
            primaryRect.pivot = new Vector2(0.5f, 0.5f);
            primaryRect.sizeDelta = new Vector2(440f, 130f);
            primaryRect.anchoredPosition = Vector2.zero;

            _primaryActionLabel = _primaryActionButton.GetComponentInChildren<Text>();
            _primaryActionLabel.fontSize = 30;
            _primaryActionLabel.fontStyle = FontStyle.Bold;

            // Guided-tutorial Continue control (steps 6 and 8 - see TutorialStep's own doc
            // comment) - shares this same region/sizing as Start Battle since the two are never
            // shown together (Start Battle is Formation-only, Continue is Combat-only), and
            // hidden by default outside those two steps (RefreshTutorialStepControls).
            _tutorialContinueButton = CreateButton(panel, "Continue", font, OnTutorialContinuePressed);
            RectTransform continueRect = _tutorialContinueButton.GetComponent<RectTransform>();
            continueRect.anchorMin = new Vector2(0.5f, 0.5f);
            continueRect.anchorMax = new Vector2(0.5f, 0.5f);
            continueRect.pivot = new Vector2(0.5f, 0.5f);
            continueRect.sizeDelta = new Vector2(440f, 130f);
            continueRect.anchoredPosition = Vector2.zero;

            _tutorialContinueLabel = _tutorialContinueButton.GetComponentInChildren<Text>();
            _tutorialContinueLabel.fontSize = 30;
            _tutorialContinueLabel.fontStyle = FontStyle.Bold;
            _tutorialContinueButton.gameObject.SetActive(false);
        }

        /// <summary>
        /// V4 splits the old combined rail into two separate, non-overlapping regions per the
        /// handoff table: Combat activity rail (top-right) and Spell/action rail (below it) -
        /// each a permanent in-shell panel, not a modal or floating overlay (the rejected pre-V3
        /// Combat Log attempt was exactly that mistake - see its own revert).
        /// </summary>
        private void BuildActivityRail(Transform canvasTransform, Font font)
        {
            // Softened from near-black (0.05,0.05,0.08) and given a bronze top accent stripe -
            // visual review read the stark near-black panel as "a floating black overlay"
            // (echoing the earlier, rejected Combat Log slice's own mistake), even though this
            // panel is genuinely native/in-shell, built alongside every other Build* region in
            // Initialize(), never toggled as a modal. A visibly bordered, less-black panel reads
            // as part of the shell rather than something floating disconnected on top of it.
            RectTransform rail = CreateAnchoredPanel(canvasTransform, "ActivityRail",
                new Color(0.09f, 0.08f, 0.13f, V3PanelAlpha), ActivityRailMin, ActivityRailMax);
            Image railAccent = CreateImage(rail, AccentBorderColor);
            railAccent.rectTransform.anchorMin = new Vector2(0f, 1f);
            railAccent.rectTransform.anchorMax = new Vector2(1f, 1f);
            railAccent.rectTransform.pivot = new Vector2(0.5f, 1f);
            railAccent.rectTransform.sizeDelta = new Vector2(0f, 3f);
            railAccent.rectTransform.anchoredPosition = Vector2.zero;
            railAccent.raycastTarget = false;

            Text railTitle = CreateText(rail, "COMBAT ACTIVITY", 19, GoldTextColor, font);
            railTitle.fontStyle = FontStyle.Bold;
            railTitle.raycastTarget = false;
            AnchorBand(railTitle.rectTransform, 0.92f, 0.99f, 0.04f, 0.04f);

            _activityLogText = CreateText(rail, "", 15, Color.white, font);
            _activityLogText.raycastTarget = false;
            _activityLogText.alignment = TextAnchor.UpperLeft;
            _activityLogText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _activityLogText.verticalOverflow = VerticalWrapMode.Truncate;
            AnchorBand(_activityLogText.rectTransform, 0.02f, 0.90f, 0.04f, 0.04f);

            // Separate Spell/action rail region, below and independent of the activity rail -
            // never scrolls together and never shares a raycastable background with it.
            RectTransform spellRail = CreateAnchoredPanel(canvasTransform, "SpellRail",
                new Color(0.09f, 0.08f, 0.13f, V3PanelAlpha), SpellRailMin, SpellRailMax);
            Image spellRailAccent = CreateImage(spellRail, AccentBorderColor);
            spellRailAccent.rectTransform.anchorMin = new Vector2(0f, 1f);
            spellRailAccent.rectTransform.anchorMax = new Vector2(1f, 1f);
            spellRailAccent.rectTransform.pivot = new Vector2(0.5f, 1f);
            spellRailAccent.rectTransform.sizeDelta = new Vector2(0f, 3f);
            spellRailAccent.rectTransform.anchoredPosition = Vector2.zero;
            spellRailAccent.raycastTarget = false;

            Text spellsTitle = CreateText(spellRail, "SPELLS", 19, GoldTextColor, font);
            spellsTitle.fontStyle = FontStyle.Bold;
            spellsTitle.raycastTarget = false;
            AnchorBand(spellsTitle.rectTransform, 0.90f, 0.99f, 0.04f, 0.04f);
            _spellsTitleText = spellsTitle;

            _spellBar = CreateAnchoredPanel(spellRail, "SpellList", Color.clear, new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.88f));
            var spellLayout = _spellBar.gameObject.AddComponent<VerticalLayoutGroup>();
            spellLayout.spacing = 6f;
            spellLayout.childAlignment = TextAnchor.UpperCenter;
            spellLayout.childForceExpandWidth = true;
            spellLayout.childForceExpandHeight = false;
            spellLayout.childControlWidth = true;
            spellLayout.childControlHeight = false;

            for (int i = 0; i < 4; i++)
            {
                int spellIndex = i; // captured per iteration, not shared across the closures

                // No onClick here (null) - a quick tap and a press-and-hold need to resolve to
                // different, mutually exclusive actions (cast/arm vs. inspect), and Button's own
                // onClick always fires on release regardless of hold duration, which can't
                // express that. SpellIconPointerHandler below owns the distinction instead.
                Button spell = CreateButton(_spellBar, "", font, null);
                SetPreferredHeight(spell.gameObject, 62f);
                var spellRowLayout = spell.gameObject.AddComponent<HorizontalLayoutGroup>();
                spellRowLayout.spacing = 8f;
                spellRowLayout.padding = new RectOffset(6, 6, 4, 4);
                spellRowLayout.childAlignment = TextAnchor.MiddleLeft;
                spellRowLayout.childForceExpandWidth = false;
                spellRowLayout.childForceExpandHeight = true;
                spellRowLayout.childControlWidth = false;
                spellRowLayout.childControlHeight = true;

                var pointerHandler = spell.gameObject.AddComponent<SpellIconPointerHandler>();
                pointerHandler.OnQuickTap = () => OnSpellTapped(spellIndex);
                pointerHandler.OnHoldStart = () => ShowSpellTooltip(spellIndex, spell.GetComponent<RectTransform>());
                pointerHandler.OnHoldEnd = HideSpellTooltip;

                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(spell.transform, false);
                SetPreferredWidth(iconGo, 52f);
                var icon = iconGo.AddComponent<Image>();
                icon.raycastTarget = false;
                icon.preserveAspect = true;

                var textColGo = new GameObject("Text", typeof(RectTransform));
                textColGo.transform.SetParent(spell.transform, false);
                SetPreferredWidth(textColGo, 260f);
                var textColLayout = textColGo.AddComponent<VerticalLayoutGroup>();
                textColLayout.childAlignment = TextAnchor.MiddleLeft;
                textColLayout.childForceExpandWidth = true;
                textColLayout.childForceExpandHeight = false;
                textColLayout.childControlWidth = true;
                textColLayout.childControlHeight = false;

                Text spellName = CreateText(textColGo.transform, "", 17, Color.white, font);
                spellName.fontStyle = FontStyle.Bold;
                spellName.raycastTarget = false;
                spellName.alignment = TextAnchor.MiddleLeft;
                spellName.horizontalOverflow = HorizontalWrapMode.Overflow;
                SetPreferredHeight(spellName.gameObject, 24f);

                // Cost/cooldown number - see RefreshPhaseControls for exactly what this shows
                // (live cost while ready, remaining cooldown ticks while not).
                Text spellLabel = CreateText(textColGo.transform, "", 15, Color.white, font);
                spellLabel.raycastTarget = false;
                spellLabel.alignment = TextAnchor.MiddleLeft;
                spellLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                SetPreferredHeight(spellLabel.gameObject, 20f);

                _spellButtons.Add(spell);
                _spellLabels.Add(spellLabel);
                _spellIcons.Add(icon);
                _spellNameLabels.Add(spellName);
            }
            _spellBar.gameObject.SetActive(false);
        }

        /// <summary>
        /// One overlay that plays both the story chapters and the tutorial steps - they're the
        /// same shape on screen (a portrait, a speaker, a line of text, and Next), so building
        /// two of them would have meant maintaining the same layout twice.
        ///
        /// Content lives in Resources/Data/Story (see Assets/Scripts/Story/) rather than being
        /// hardcoded here, so it can be rewritten without touching C#.
        /// </summary>
        private void BuildTutorialOverlay(Transform canvasTransform, Font font)
        {
            _tutorialOverlay = new GameObject("TutorialOverlay");
            _tutorialOverlay.transform.SetParent(canvasTransform, false);
            StretchFull(_tutorialOverlay.AddComponent<RectTransform>());

            // Dimmer is a Button so a tap anywhere advances - on a phone, hunting for a small
            // Next button between every line of dialogue is needless friction.
            // Full-screen landscape artwork behind the dialogue, not a dark modal over the
            // battle board. "The story is just clicking Next with no animation or change of
            // background - this is not what I'm looking for" (2026-08-06). Cropped-fill so a
            // wide painting covers any window shape without letterboxing or squashing.
            _narrativeBackground = CreateCroppedArt(_tutorialOverlay.transform, null,
                Vector2.zero, Vector2.one);

            var dimGo = new GameObject("Dim", typeof(RectTransform));
            dimGo.transform.SetParent(_tutorialOverlay.transform, false);
            var dim = dimGo.AddComponent<Image>();
            // Much lighter than the old 0.72 modal dim - the artwork is the point now, this only
            // has to keep the dialogue legible over it.
            dim.color = new Color(0, 0, 0, 0.38f);
            StretchFull(dim.rectTransform);
            var dimButton = dimGo.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(AdvanceNarrative);

            // The pointing arrow, shown for tutorial steps that name a UI element to point at.
            var arrowGo = new GameObject("TutorialArrow", typeof(RectTransform));
            arrowGo.transform.SetParent(_tutorialOverlay.transform, false);
            _tutorialArrow = arrowGo.AddComponent<Image>();
            _tutorialArrow.sprite = Resources.Load<Sprite>("UI/Icons/Tutorial_Arrow");
            _tutorialArrow.preserveAspect = true;
            _tutorialArrow.raycastTarget = false;
            var arrowRect = (RectTransform)arrowGo.transform;
            arrowRect.anchorMin = arrowRect.anchorMax = new Vector2(0.5f, 0.5f);
            arrowRect.pivot = new Vector2(0.5f, 0.5f);
            arrowRect.sizeDelta = new Vector2(190f, 60f);
            arrowGo.SetActive(false);

            RectTransform panel = CreateRoundedPanel(_tutorialOverlay.transform, "NarrativePanel",
                HandPanelTop, HandPanelBottom);
            panel.anchorMin = new Vector2(0.08f, 0.10f);
            panel.anchorMax = new Vector2(0.92f, 0.52f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;

            var portraitGo = new GameObject("NarrativePortrait", typeof(RectTransform));
            portraitGo.transform.SetParent(panel, false);
            AnchorBand((RectTransform)portraitGo.transform, 0.30f, 0.86f, 0.06f, 0.76f);
            _narrativePortrait = portraitGo.AddComponent<Image>();
            _narrativePortrait.preserveAspect = true;
            _narrativePortrait.raycastTarget = false;

            _narrativeSpeaker = CreateText(panel, "", 18, GoldTextColor, font);
            _narrativeSpeaker.fontStyle = FontStyle.Bold;
            _narrativeSpeaker.alignment = TextAnchor.MiddleLeft;
            _narrativeSpeaker.raycastTarget = false;
            AnchorBand(_narrativeSpeaker.rectTransform, 0.76f, 0.88f, 0.26f, 0.08f);

            _narrativeText = CreateText(panel, "", 16, Color.white, font);
            _narrativeText.alignment = TextAnchor.UpperLeft;
            _narrativeText.raycastTarget = false;
            AnchorBand(_narrativeText.rectTransform, 0.30f, 0.76f, 0.26f, 0.08f);

            _narrativeProgress = CreateText(panel, "", 13, ButtonTextDisabledColor, font);
            _narrativeProgress.alignment = TextAnchor.MiddleLeft;
            _narrativeProgress.raycastTarget = false;
            AnchorBand(_narrativeProgress.rectTransform, 0.04f, 0.16f, 0.06f, 0.60f);

            Button next = CreateButton(panel, "Next", font, AdvanceNarrative);
            AnchorBand(next.GetComponent<RectTransform>(), 0.04f, 0.18f, 0.58f, 0.22f);

            Button skip = CreateButton(panel, "Skip", font, CloseNarrative);
            AnchorBand(skip.GetComponent<RectTransform>(), 0.04f, 0.18f, 0.80f, 0.04f);

            _tutorialOverlay.SetActive(false);
        }

        /// <summary>
        /// Builds the guided-tutorial teaching overlay: a single full-screen blocker, an
        /// always-topmost proxy container (holding a bright marker frame + downward arrow, plus
        /// whichever Tutorial Action Proxy RefreshTutorialTeachingOverlay currently builds - see
        /// this region's own top-of-file comment for why nothing real is ever reparented into
        /// it), a readable guide panel (existing Lightbringer portrait, speaker name, instruction
        /// text), and a top-right Skip Tutorial button. Hidden by default;
        /// RefreshTutorialTeachingOverlay (via RefreshTutorialStepControls, called by every
        /// RefreshAll()) shows, hides and repositions it every refresh.
        ///
        /// Unrelated to _tutorialOverlay/BuildTutorialOverlay above (the JSON-driven first-run
        /// narrative/pointing-arrow intro, gated on Application.isPlaying and never active during
        /// a guided battle) - this overlay is driven purely by _tutorialStep instead.
        /// </summary>
        private void BuildTutorialTeachingOverlay(Transform canvasTransform, Font font)
        {
            _tutorialTeachingOverlay = new GameObject("TutorialTeachingOverlay");
            _tutorialTeachingOverlay.transform.SetParent(canvasTransform, false);
            StretchFull(_tutorialTeachingOverlay.AddComponent<RectTransform>());

            var blockerGo = new GameObject("TutorialFullScreenBlocker", typeof(RectTransform));
            blockerGo.transform.SetParent(_tutorialTeachingOverlay.transform, false);
            _tutorialFullScreenBlocker = blockerGo.AddComponent<Image>();
            _tutorialFullScreenBlocker.color = new Color(0f, 0f, 0f, 0.78f);
            _tutorialFullScreenBlocker.raycastTarget = true; // the entire point: block every non-target tap
            StretchFull(_tutorialFullScreenBlocker.rectTransform);

            BuildTutorialGuidePanel(_tutorialTeachingOverlay.transform, font);

            _tutorialSkipButton = CreateButton(_tutorialTeachingOverlay.transform, "SKIP TUTORIAL", font, OnSkipTutorialPressed);
            RectTransform skipRect = _tutorialSkipButton.GetComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(1f, 1f);
            skipRect.anchorMax = new Vector2(1f, 1f);
            skipRect.pivot = new Vector2(1f, 1f);
            skipRect.sizeDelta = new Vector2(190f, 56f);
            skipRect.anchoredPosition = new Vector2(-24f, -24f);
            Text skipLabel = _tutorialSkipButton.GetComponentInChildren<Text>();
            skipLabel.fontSize = 18;
            skipLabel.fontStyle = FontStyle.Bold;

            // Always the last child under the overlay (and therefore under the whole canvas) -
            // holds only the decorative marker (built here, permanent) and the Tutorial Action
            // Proxy (built/destroyed fresh every refresh, appended as its last child) - never a
            // real gameplay control. Never reordered after this.
            var containerGo = new GameObject("TutorialProxyContainer", typeof(RectTransform));
            containerGo.transform.SetParent(_tutorialTeachingOverlay.transform, false);
            _tutorialProxyContainer = (RectTransform)containerGo.transform;
            StretchFull(_tutorialProxyContainer);

            Color markerColor = new Color(0.35f, 0.95f, 0.95f); // cyan
            _tutorialMarkerTop = BuildTutorialMarkerStrip(_tutorialProxyContainer, "MarkerTop", markerColor);
            _tutorialMarkerBottom = BuildTutorialMarkerStrip(_tutorialProxyContainer, "MarkerBottom", markerColor);
            _tutorialMarkerLeft = BuildTutorialMarkerStrip(_tutorialProxyContainer, "MarkerLeft", markerColor);
            _tutorialMarkerRight = BuildTutorialMarkerStrip(_tutorialProxyContainer, "MarkerRight", markerColor);

            var arrowGo = new GameObject("TutorialMarkerArrow", typeof(RectTransform));
            arrowGo.transform.SetParent(_tutorialProxyContainer, false);
            _tutorialMarkerArrow = (RectTransform)arrowGo.transform;
            var arrowImage = arrowGo.AddComponent<Image>();
            arrowImage.sprite = Resources.Load<Sprite>("UI/Icons/Tutorial_Arrow");
            arrowImage.color = markerColor;
            arrowImage.preserveAspect = true;
            arrowImage.raycastTarget = false;
            // Arrow art points right by default elsewhere in this file (PointArrowAt) - rotated
            // to point down at the target sitting directly below it here.
            arrowGo.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

            _tutorialTeachingOverlay.SetActive(false);
        }

        /// <summary>One thin, bright, non-raycast-blocking strip of the target marker's frame -
        /// purely decorative, see PositionMarkerAroundTarget's own comment.</summary>
        private static RectTransform BuildTutorialMarkerStrip(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return (RectTransform)go.transform;
        }

        /// <summary>
        /// The readable guide panel: existing Lightbringer portrait, speaker name (>=22px), and
        /// instruction text (>=28px) over a near-opaque, high-contrast panel with real margins on
        /// every side - rejected 2026-08-16 for being "readable only at extreme zoom" at the
        /// previous 15/16px sizes with tight insets.
        /// </summary>
        private void BuildTutorialGuidePanel(Transform parent, Font font)
        {
            _tutorialGuidePanelRect = CreateRoundedPanel(parent, "TutorialGuidePanel",
                new Color(0.04f, 0.04f, 0.06f, 0.97f), new Color(0.04f, 0.04f, 0.06f, 0.97f));
            _tutorialGuidePanelRect.offsetMin = Vector2.zero;
            _tutorialGuidePanelRect.offsetMax = Vector2.zero;

            // A solid, opaque fill behind the ornate frame sprite CreateRoundedPanel prefers -
            // that frame has transparent corners/edges by design (see its own comment), which is
            // the right look for a modal but leaves this panel's text sitting directly over
            // whatever board content is behind it, hurting contrast. Inserted as the first (so,
            // furthest-back) child specifically so the frame art still shows on top of it.
            Image solidBackdrop = CreateImage(_tutorialGuidePanelRect, new Color(0.04f, 0.04f, 0.06f, 0.94f));
            StretchFull(solidBackdrop.rectTransform);
            solidBackdrop.raycastTarget = false;
            solidBackdrop.transform.SetAsFirstSibling();

            var portraitGo = new GameObject("TutorialGuidePortrait", typeof(RectTransform));
            portraitGo.transform.SetParent(_tutorialGuidePanelRect, false);
            AnchorBand((RectTransform)portraitGo.transform, 0.08f, 0.92f, 0.06f, 0.68f);
            var portraitImg = portraitGo.AddComponent<Image>();
            portraitImg.sprite = Resources.Load<Sprite>("UI/Portraits/Paladin");
            portraitImg.preserveAspect = true;
            portraitImg.raycastTarget = false;

            _tutorialGuideSpeakerText = CreateText(_tutorialGuidePanelRect, "LIGHTBRINGER", 24, GoldTextColor, font);
            _tutorialGuideSpeakerText.fontStyle = FontStyle.Bold;
            _tutorialGuideSpeakerText.alignment = TextAnchor.UpperLeft;
            _tutorialGuideSpeakerText.raycastTarget = false;
            AnchorBand(_tutorialGuideSpeakerText.rectTransform, 0.80f, 0.94f, 0.36f, 0.06f);

            _tutorialGuideBodyText = CreateText(_tutorialGuidePanelRect, "", 30, Color.white, font);
            _tutorialGuideBodyText.alignment = TextAnchor.UpperLeft;
            _tutorialGuideBodyText.raycastTarget = false;
            _tutorialGuideBodyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _tutorialGuideBodyText.verticalOverflow = VerticalWrapMode.Overflow;
            AnchorBand(_tutorialGuideBodyText.rectTransform, 0.06f, 0.78f, 0.36f, 0.06f);
        }

        /// <summary>
        /// Shows the prologue and the tutorial on a player's first run only.
        ///
        /// Gated on the saved profile rather than the PlayerPrefs flag this used before the save
        /// system existed. PlayerPrefs lives in per-machine registry/plist state that no export,
        /// no cloud save and no "reset progress" path can see, so onboarding used to be the one
        /// piece of player state that a profile wipe could not clear.
        ///
        /// RESTORED 2026-08-07 to load story_chapters.json/tutorial_steps.json directly rather
        /// than through MyriadOfDragons.Story.StoryDatabase - that class was rebuilt (by the
        /// metagame side) into a static, in-code sequence lookup for per-stage pre-battle banter
        /// (CampaignMapPresenter's "1-1_pre" etc.), which has no equivalent for
        /// TutorialStep.highlight at all: pointing PointArrowAt() at a live GameObject name
        /// (SpellBar, StatusRow, PlayerPanel...) is battle-UI knowledge that never belonged in a
        /// generic dialogue model to begin with. The two features - narrative intro and
        /// interactive board tutorial - are genuinely different things that happened to share one
        /// loader before; splitting them here means this file no longer depends on the metagame
        /// side's data shape at all, and the metagame side is free to keep reshaping its own
        /// per-stage dialogue system without this method breaking again. The JSON itself was
        /// untouched by that rebuild and still holds the real, original content.
        /// </summary>
        private void MaybeShowTutorial()
        {
            if (!Application.isPlaying) return;   // never during EditMode tests
            if (_profile.SeenIntro) return;       // already seen

            _narrativeQueue.Clear();

            string lastBackground = string.Empty;
            IntroChapterData prologue = LoadIntroChapter("prologue");
            if (prologue?.beats != null)
            {
                foreach (IntroBeatData beat in prologue.beats)
                {
                    // An empty background carries the previous beat's forward, so a run of
                    // dialogue in one place only names its location once.
                    if (!string.IsNullOrEmpty(beat.background)) lastBackground = beat.background;
                    _narrativeQueue.Add(new NarrativeBeat(beat.speaker, beat.portrait, beat.text,
                        lastBackground, highlight: string.Empty));
                }
            }

            foreach (IntroTutorialStepData step in LoadTutorialSteps())
            {
                // Tutorial steps have no speaker or portrait of their own - they're the game
                // talking to the player, not a character - and no background, because they point
                // at the live board behind them rather than replacing it.
                _narrativeQueue.Add(new NarrativeBeat("How to play", string.Empty, step.instruction,
                    background: string.Empty, highlight: step.highlight));
            }

            if (_narrativeQueue.Count == 0) return;

            _narrativeIndex = 0;
            _tutorialOverlay.SetActive(true);
            ShowNarrativeBeat();
        }

        // Public, and the two loaders below are public static, specifically so the EditMode suite
        // can exercise the JSON parsing directly - MaybeShowTutorial() itself early-returns under
        // Application.isPlaying == false (see its own comment), which is always true in EditMode,
        // so testing through it would never actually touch this loading logic at all.
        [System.Serializable]
        public class IntroBeatData
        {
            public string speaker;
            public string portrait;
            public string background;
            public string text;
        }

        [System.Serializable]
        public class IntroChapterData
        {
            public string id;
            public string title;
            public IntroBeatData[] beats;
        }

        [System.Serializable]
        private class IntroChapterListData
        {
            public IntroChapterData[] chapters;
        }

        [System.Serializable]
        public class IntroTutorialStepData
        {
            public string id;
            public string instruction;
            public string highlight;
        }

        [System.Serializable]
        private class IntroTutorialStepListData
        {
            public IntroTutorialStepData[] steps;
        }

        public static IntroChapterData LoadIntroChapter(string chapterId)
        {
            TextAsset json = Resources.Load<TextAsset>("Data/Story/story_chapters");
            if (json == null) return null;

            IntroChapterListData parsed = JsonUtility.FromJson<IntroChapterListData>(json.text);
            return parsed?.chapters?.FirstOrDefault(c => c.id == chapterId);
        }

        public static IntroTutorialStepData[] LoadTutorialSteps()
        {
            TextAsset json = Resources.Load<TextAsset>("Data/Story/tutorial_steps");
            if (json == null) return System.Array.Empty<IntroTutorialStepData>();

            IntroTutorialStepListData parsed = JsonUtility.FromJson<IntroTutorialStepListData>(json.text);
            return parsed?.steps ?? System.Array.Empty<IntroTutorialStepData>();
        }

        /// <summary>Replays the intro on demand - exposed so a menu (or a test) can trigger it
        /// without editing the save by hand.</summary>
        public void ReplayIntro()
        {
            _profile.ResetOnboarding();
            MaybeShowTutorial();
        }

        private void ShowNarrativeBeat()
        {
            NarrativeBeat beat = _narrativeQueue[_narrativeIndex];

            _narrativeSpeaker.text = beat.Speaker ?? string.Empty;
            _narrativeProgress.text = $"{_narrativeIndex + 1} / {_narrativeQueue.Count}   (tap to continue)";

            Sprite portraitSprite = string.IsNullOrEmpty(beat.Portrait)
                ? null
                : Resources.Load<Sprite>($"UI/Portraits/{beat.Portrait}");
            _narrativePortrait.sprite = portraitSprite;
            _narrativePortrait.enabled = portraitSprite != null;

            ApplyNarrativeBackground(beat.Background);
            PointArrowAt(beat.Highlight);

            // Typewriter, and a fade-in on the background, so each beat is an event rather than
            // a silent text swap. Advancing mid-type completes the line instantly (see
            // AdvanceNarrative) - a typewriter you can't skip is worse than none at all.
            if (_typewriter != null) StopCoroutine(_typewriter);
            if (Application.isPlaying)
            {
                _typewriter = StartCoroutine(TypeText(_narrativeText, beat.Text ?? string.Empty));
            }
            else
            {
                _narrativeText.text = beat.Text ?? string.Empty;
            }
        }

        private void ApplyNarrativeBackground(string backgroundName)
        {
            if (_narrativeBackground == null) return;

            if (string.IsNullOrEmpty(backgroundName))
            {
                // Tutorial steps carry no background - they point at the live board, so the
                // artwork layer gets out of the way entirely.
                _narrativeBackground.enabled = false;
                return;
            }

            Sprite art = Resources.Load<Sprite>($"Story/Backgrounds/{backgroundName}");
            if (art == null)
            {
                _narrativeBackground.enabled = false;
                return;
            }

            _narrativeBackground.sprite = art;
            _narrativeBackground.enabled = true;

            var fitter = _narrativeBackground.GetComponent<AspectRatioFitter>();
            if (fitter != null && art.rect.height > 0f)
            {
                fitter.aspectRatio = art.rect.width / art.rect.height;
            }

            if (Application.isPlaying)
            {
                if (_backgroundFade != null) StopCoroutine(_backgroundFade);
                _backgroundFade = StartCoroutine(FadeInBackground(_narrativeBackground));
            }
        }

        private static IEnumerator FadeInBackground(Image image)
        {
            const float duration = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration && image != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                image.color = new Color(1f, 1f, 1f, t);
                yield return null;
            }
            if (image != null) image.color = Color.white;
        }

        private static IEnumerator TypeText(Text target, string fullText)
        {
            target.text = string.Empty;
            var builder = new System.Text.StringBuilder(fullText.Length);
            foreach (char c in fullText)
            {
                builder.Append(c);
                target.text = builder.ToString();
                yield return new WaitForSeconds(0.018f);
            }
        }

        /// <summary>
        /// Points the bouncing arrow at the named UI element. `highlight` matches the GameObject
        /// names GameBootstrap creates ("HandPanel", "SpellBar", ...) so the tutorial JSON can
        /// name a target without a second lookup table to keep in sync.
        /// </summary>
        private void PointArrowAt(string highlight)
        {
            if (_tutorialArrow == null) return;

            if (_arrowBounce != null)
            {
                StopCoroutine(_arrowBounce);
                _arrowBounce = null;
            }

            GameObject target = string.IsNullOrEmpty(highlight) ? null : GameObject.Find(highlight);
            if (target == null || !Application.isPlaying)
            {
                _tutorialArrow.gameObject.SetActive(false);
                return;
            }

            _tutorialArrow.gameObject.SetActive(true);
            _tutorialArrow.transform.position = target.transform.position;
            _arrowBounce = StartCoroutine(BounceArrow((RectTransform)_tutorialArrow.transform));
        }

        private static IEnumerator BounceArrow(RectTransform arrow)
        {
            Vector3 origin = arrow.position;
            while (arrow != null)
            {
                // Horizontal nudge, because the arrow art points right - bouncing it vertically
                // would read as unrelated motion rather than as "look over here".
                float offset = Mathf.Sin(Time.time * 6f) * 14f;
                arrow.position = origin + new Vector3(offset, 0f, 0f);
                yield return null;
            }
        }

        private void AdvanceNarrative()
        {
            // A tap while text is still typing completes the line instead of skipping it - the
            // usual convention, and it stops fast readers being held hostage by the animation.
            if (_typewriter != null)
            {
                StopCoroutine(_typewriter);
                _typewriter = null;

                string full = _narrativeQueue[_narrativeIndex].Text ?? string.Empty;
                if (_narrativeText.text.Length < full.Length)
                {
                    _narrativeText.text = full;
                    return;
                }
            }

            _narrativeIndex++;
            if (_narrativeIndex >= _narrativeQueue.Count)
            {
                CloseNarrative();
                return;
            }
            ShowNarrativeBeat();
        }

        private void CloseNarrative()
        {
            // Marked seen on Skip as well as on completion - a player who skips has made a
            // decision, and re-showing it next launch would override that decision.
            _profile.MarkIntroSeen();

            // The prologue is recorded by id too, not just by the single seenIntro flag. Once
            // there is more than one chapter, "has this player seen the intro" and "which chapters
            // has this player read" stop being the same question, and a per-chapter record cannot
            // be reconstructed after the fact from a boolean.
            _profile.MarkChapterSeen("prologue");

            // Every coroutine this overlay owns is stopped explicitly. The arrow bounce in
            // particular loops forever by design, so leaving it running would keep nudging a
            // hidden object and holding a reference to it for the rest of the session.
            foreach (Coroutine running in new[] { _typewriter, _backgroundFade, _arrowBounce })
            {
                if (running != null) StopCoroutine(running);
            }
            _typewriter = null;
            _backgroundFade = null;
            _arrowBounce = null;

            _tutorialOverlay.SetActive(false);
        }

        /// <summary>
        /// The lane picker: tap a lane, see that lane's three slots and every card you could put
        /// in them, fill it, close.
        ///
        /// Replaces select-a-card-then-tap-a-lane (2026-08-06, direct request). That flow made
        /// the *card* the unit of decision and the lane an afterthought, needing two taps per
        /// card with the hand and the board far apart on screen. Since lane choice is the actual
        /// strategic decision in this game - Front trades Health for Attack, Middle is where
        /// Taunt belongs, Back is where draw hooks fire - the lane should be what you commit to,
        /// with its three cards chosen together and comparable side by side.
        /// </summary>
        private void BuildLanePickerOverlay(Transform canvasTransform, Font font)
        {
            _lanePickerOverlay = new GameObject("LanePickerOverlay");
            _lanePickerOverlay.transform.SetParent(canvasTransform, false);
            StretchFull(_lanePickerOverlay.AddComponent<RectTransform>());

            var dimGo = new GameObject("Dim", typeof(RectTransform));
            dimGo.transform.SetParent(_lanePickerOverlay.transform, false);
            var dim = dimGo.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, 0.8f);
            StretchFull(dim.rectTransform);
            var dimButton = dimGo.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(CloseLanePicker);

            RectTransform panel = CreateRoundedPanel(_lanePickerOverlay.transform, "LanePickerPanel",
                HandPanelTop, HandPanelBottom);
            panel.anchorMin = new Vector2(0.06f, 0.16f);
            panel.anchorMax = new Vector2(0.94f, 0.86f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            _lanePickerTitle = CreateText(panel, "", 22, GoldTextColor, font);
            _lanePickerTitle.fontStyle = FontStyle.Bold;
            AnchorBand(_lanePickerTitle.rectTransform, 0.86f, 0.95f, 0.08f, 0.08f);

            Text deployedCaption = CreateText(panel, "Deployed - tap to take back", 14, Color.white, font);
            AnchorBand(deployedCaption.rectTransform, 0.79f, 0.855f, 0.08f, 0.08f);

            var deployedGo = new GameObject("DeployedRow", typeof(RectTransform));
            deployedGo.transform.SetParent(panel, false);
            AnchorBand((RectTransform)deployedGo.transform, 0.50f, 0.79f, 0.10f, 0.10f);
            var deployedLayout = deployedGo.AddComponent<HorizontalLayoutGroup>();
            deployedLayout.spacing = 16f;
            deployedLayout.childForceExpandWidth = false;
            deployedLayout.childControlWidth = false;
            deployedLayout.childForceExpandHeight = true;
            deployedLayout.childControlHeight = true;
            deployedLayout.childAlignment = TextAnchor.MiddleCenter;
            _lanePickerDeployedRow = (RectTransform)deployedGo.transform;

            _lanePickerAvailableCaption = CreateText(panel, "", 14, Color.white, font);
            AnchorBand(_lanePickerAvailableCaption.rectTransform, 0.43f, 0.50f, 0.08f, 0.08f);

            var availableGo = new GameObject("AvailableRow", typeof(RectTransform));
            availableGo.transform.SetParent(panel, false);
            AnchorBand((RectTransform)availableGo.transform, 0.13f, 0.43f, 0.04f, 0.04f);
            var availableLayout = availableGo.AddComponent<HorizontalLayoutGroup>();
            availableLayout.spacing = 12f;
            availableLayout.childForceExpandWidth = false;
            availableLayout.childControlWidth = false;
            availableLayout.childForceExpandHeight = true;
            availableLayout.childControlHeight = true;
            availableLayout.childAlignment = TextAnchor.MiddleCenter;
            _lanePickerAvailableRow = (RectTransform)availableGo.transform;

            Button done = CreateButton(panel, "Done", font, CloseLanePicker);
            AnchorBand(done.GetComponent<RectTransform>(), 0.02f, 0.11f, 0.35f, 0.35f);

            _lanePickerOverlay.SetActive(false);
        }

        private void OpenLanePicker(Lane lane)
        {
            _pickerLane = lane;
            _lanePickerOverlay.SetActive(true);
            RefreshLanePicker();
        }

        private void CloseLanePicker()
        {
            _lanePickerOverlay.SetActive(false);
            RefreshAll();
        }

        /// <summary>
        /// Rebuilds both rows from live state. Called after every add/remove so the two rows and
        /// the Resource counter can never drift out of sync with the board behind the overlay.
        /// </summary>
        private void RefreshLanePicker()
        {
            if (_lanePickerOverlay == null || !_lanePickerOverlay.activeSelf) return;

            Font font = GetDefaultFont();
            PlayerBattleState player = _battleController.PlayerState;
            LaneState laneState = player.Lanes[_pickerLane];

            // Counts slots, not cards - a rarity 5+ card takes two of the lane's three
            // (Card.SlotWeight), so "2/3 slots" can mean a single big card.
            _lanePickerTitle.text = $"{_pickerLane.ToString().ToUpperInvariant()} LANE   {LaneBonusLabel(_pickerLane)}" +
                                    $"   -   {laneState.SlotsUsed}/{LaneState.MaxSlots} slots";

            // Approved tutorial-only copy (Command Centre decision, 2026-08-15), appended rather
            // than replacing the line above - a normal match keeps exactly the existing lane
            // bonus/slot text, unchanged.
            if (IsTutorialMatch)
            {
                _lanePickerTitle.text += "\n" + TutorialLaneGuidance(_pickerLane);
            }

            ClearChildren(_lanePickerDeployedRow);
            for (int i = 0; i < laneState.Cards.Count; i++)
            {
                int slot = i; // captured per iteration
                Button occupied = CreateCardButton(_lanePickerDeployedRow, laneState.Cards[i].Definition,
                    font, affordable: true, isSelected: true);
                // A two-slot card is rendered double width, so the board reads honestly - a
                // 7-star visibly consumes the space it costs rather than looking like any other
                // card that happens to block more.
                SetPreferredWidth(occupied.gameObject, 118 * laneState.Cards[i].Definition.SlotWeight);
                occupied.onClick.AddListener(() =>
                {
                    if (_battleController.TryRecallCard(_pickerLane, slot)) RefreshLanePicker();
                });
            }
            for (int i = 0; i < laneState.FreeSlots; i++)
            {
                CreateEmptySlotDisplay(_lanePickerDeployedRow);
            }

            int resource = player.Resource;
            _lanePickerAvailableCaption.text = laneState.HasOpenSlot
                ? $"Your cards - tap to deploy here   ({laneState.FreeSlots} free, Resource {resource}/{player.ResourceCap})"
                : $"{_pickerLane} lane is full - take one back to swap it";

            ClearChildren(_lanePickerAvailableRow);
            if (!laneState.HasOpenSlot) return;

            foreach (Card card in player.Hand.ToList())
            {
                Card captured = card;
                // "Affordable" here means playable *into this lane*: enough Resource and enough
                // room. A 7-star with one slot free is shown dimmed rather than hidden, so the
                // reason it can't go here is visible instead of the card silently vanishing.
                bool affordable = card.ResourceCost <= resource && laneState.HasRoomFor(card);
                Button option = CreateCardButton(_lanePickerAvailableRow, card, font, affordable, isSelected: false);
                option.onClick.AddListener(() =>
                {
                    if (_battleController.TryPlayCard(_battleController.PlayerState, captured, _pickerLane))
                    {
                        RefreshLanePicker();
                    }
                });
            }
        }

        private static void ClearChildren(Transform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                // DestroyImmediate, not Destroy - this is reachable from Initialize() outside
                // Play Mode, same as the other refresh paths.
                DestroyImmediate(parent.GetChild(i).gameObject);
            }
        }

        /// <summary>
        /// Tap a hand card to zoom in on its full art and stats, then confirm from there -
        /// tapping the card itself no longer instantly (de)selects it, since on a phone-sized
        /// hand row the art/stats are too small to read before committing to play something.
        /// </summary>
        private void BuildCardDetailOverlay(Transform canvasTransform, Font font)
        {
            _cardDetailOverlay = new GameObject("CardDetailOverlay");
            _cardDetailOverlay.transform.SetParent(canvasTransform, false);
            RectTransform overlayRect = _cardDetailOverlay.AddComponent<RectTransform>();
            StretchFull(overlayRect);

            var dimGo = new GameObject("Dim", typeof(RectTransform));
            dimGo.transform.SetParent(_cardDetailOverlay.transform, false);
            var dimImage = dimGo.AddComponent<Image>();
            dimImage.color = new Color(0, 0, 0, 0.75f);
            StretchFull((RectTransform)dimGo.transform);
            Button dimButton = dimGo.AddComponent<Button>();
            dimButton.transition = Selectable.Transition.None;
            dimButton.onClick.AddListener(CloseCardDetail);

            // Narrowed hard from 0.08-0.92 to 0.26-0.74 (2026-08-05). The panel was 84% of the
            // screen wide while its only real content - one portrait-shaped card, a name, and
            // two lines of text - is naturally a narrow column, so most of the popup was empty
            // stone texture either side of the art. On a wide desktop window that was most of
            // the screen. A card zoom should be card-shaped.
            RectTransform panel = CreateRoundedPanel(_cardDetailOverlay.transform, "CardDetailPanel", HandPanelTop, HandPanelBottom);
            // Narrowed again 2026-08-06 - still carrying blank frame either side of the card at
            // 0.26-0.74. A card zoom only needs to be as wide as a card.
            panel.anchorMin = new Vector2(0.33f, 0.08f);
            panel.anchorMax = new Vector2(0.67f, 0.94f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;
            // A raycast-blocking Button (not just an Image) so a tap on the panel is consumed
            // here rather than falling through to the dim background's close handler beneath it.
            panel.gameObject.AddComponent<Button>().transition = Selectable.Transition.None;

            // Every child below is anchored as a *fraction* of the panel, not a fixed pixel
            // size/offset from the top - a fixed-pixel art box (the original version of this)
            // silently overflowed past a short/wide window's actual panel height, pushing the
            // name/stats/skill text fully off-screen with nothing visibly wrong in the code.
            // Real bug, caught from a manual playtest screenshot, not a design nitpick.

            _cardDetailClassTag = CreateText(panel, "", 15, GoldTextColor, font);
            _cardDetailClassTag.fontStyle = FontStyle.Bold;
            AnchorBand(_cardDetailClassTag.rectTransform, 0.925f, 0.975f, 0.10f, 0.10f);

            // The rarity frame now sits tight around the art rather than floating in a much
            // wider box - "the border is not at the card" (2026-08-05). With the panel itself
            // narrowed, a 0.06 inset keeps this box roughly card-shaped (portrait), so the
            // frame's edge lands right at the artwork instead of a hand's width away from it.
            var artFrameGo = new GameObject("ArtFrame", typeof(RectTransform));
            artFrameGo.transform.SetParent(panel, false);
            AnchorBand((RectTransform)artFrameGo.transform, 0.46f, 0.92f, 0.02f, 0.02f);

            _cardDetailArtFrame = artFrameGo.AddComponent<Image>();
            _cardDetailArtFrame.type = Image.Type.Sliced;
            _cardDetailArtFrame.raycastTarget = false;

            // Cropped fill (see CreateCroppedArt). Turning preserveAspect off outright, as the
            // previous revision did, is what produced the badly stretched portrait.
            _cardDetailArt = CreateCroppedArt(artFrameGo.transform, null,
                new Vector2(0.05f, 0.05f), new Vector2(0.95f, 0.95f));

            // Default font, not GetDisplayFont(): the decorative ENDORALT face was rendering the
            // card name soft and hard to read at this size ("the wordings are too blur").
            // A display font is worth it on a title; on the one line that tells you which card
            // you're looking at, legibility wins.
            _cardDetailName = CreateText(panel, "", 22, Color.white, font);
            _cardDetailName.fontStyle = FontStyle.Bold;
            AnchorBand(_cardDetailName.rectTransform, 0.39f, 0.455f, 0.03f, 0.03f);

            // A dark plate behind the stat/skill text. Without it these sat straight on the
            // frame's mottled stone texture, which is exactly why they read as unhighlighted and
            // hard to scan ("the words should be highlight and readable") - the text colour was
            // never the problem, the busy backdrop behind it was.
            Image statsPlate = CreateImage(panel, new Color(0f, 0f, 0f, 0.55f));
            AnchorBand(statsPlate.rectTransform, 0.185f, 0.385f, 0.03f, 0.03f);
            statsPlate.raycastTarget = false;

            _cardDetailStats = CreateText(panel, "", 18, GoldTextColor, font);
            _cardDetailStats.fontStyle = FontStyle.Bold;
            AnchorBand(_cardDetailStats.rectTransform, 0.29f, 0.38f, 0.04f, 0.04f);

            _cardDetailSkill = CreateText(panel, "", 15, Color.white, font);
            _cardDetailSkill.fontStyle = FontStyle.Italic;
            AnchorBand(_cardDetailSkill.rectTransform, 0.19f, 0.29f, 0.04f, 0.04f);

            // Compact, centered pill buttons. Narrowed again 2026-08-06 - still reading as bars
            // rather than buttons at a 0.16 inset on the previous panel width.
            _cardDetailActionButton = CreateButton(panel, "", font, OnCardDetailActionPressed);
            _cardDetailActionLabel = _cardDetailActionButton.GetComponentInChildren<Text>();
            AnchorBand(_cardDetailActionButton.GetComponent<RectTransform>(), 0.095f, 0.165f, 0.22f, 0.22f);

            Button closeButton = CreateButton(panel, "Close", font, CloseCardDetail);
            AnchorBand(closeButton.GetComponent<RectTransform>(), 0.02f, 0.085f, 0.34f, 0.34f);

            _cardDetailOverlay.SetActive(false);
        }

        /// <summary>Anchors a RectTransform to a horizontal band of its parent, expressed
        /// entirely as fractions (y0-y1 of height, xInset from each side) - never a fixed pixel
        /// size or offset, so it scales correctly regardless of the parent's actual pixel size.</summary>
        private static void AnchorBand(RectTransform rect, float y0, float y1, float xInsetMin, float xInsetMax)
        {
            rect.anchorMin = new Vector2(xInsetMin, y0);
            rect.anchorMax = new Vector2(1f - xInsetMax, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private void BuildResultOverlay(Transform canvasTransform, Font font)
        {
            _resultOverlay = new GameObject("ResultOverlay");
            _resultOverlay.transform.SetParent(canvasTransform, false);
            RectTransform overlayRect = _resultOverlay.AddComponent<RectTransform>();
            StretchFull(overlayRect);

            Image dim = CreateImage(_resultOverlay.transform, new Color(0, 0, 0, 0.7f));
            StretchFull(dim.rectTransform);

            // Was (0.1,0.4)-(0.9,0.6) - only 20% of screen height but 80% of the width, an
            // extremely short/wide panel. CreateRoundedPanel now fills this with the ornate
            // Popup_Frame art (~100px border on all 4 sides), and a panel that short couldn't
            // fit even one full border height, let alone two - "the asset alignment of the
            // popup victory is off" (2026-08-05). Taller/narrower fixes it.
            RectTransform panel = CreateRoundedPanel(_resultOverlay.transform, "ResultPanel", HandPanelTop, HandPanelBottom);
            panel.anchorMin = new Vector2(0.15f, 0.30f);
            panel.anchorMax = new Vector2(0.85f, 0.70f);
            panel.offsetMin = Vector2.zero;
            panel.offsetMax = Vector2.zero;

            // V3 visual-review fix, 2026-08-16: this overlay is anchored against the full 1920x1080
            // canvas (not one of V3's new smaller regions), so its panel is already large - only
            // its internal text was still sized for the old 720-wide canvas. Bumped for legibility
            // at the new scale; no layout/anchor change.
            _resultText = CreateText(panel, "", 32, Color.white, font);
            AnchorBand(_resultText.rectTransform, 0.4f, 0.85f, 0.06f, 0.06f);

            // Side by side rather than stacked (2026-08-06, "RETURN TO CITY" added for the
            // battle/metagame split) - both are single-tap, equally weighted exits from this
            // screen, and stacking two full-width buttons here would just be the "giant pill"
            // problem the spell bar already had.
            //
            // onClick is OnPlayAgainOrRetryPressed, not OnPlayAgainPressed directly - the same
            // physical button serves as "Play Again" (normal) or "Retry Battle" (tutorial
            // defeat), see that method's own comment for why the branch has to live there.
            _playAgainButton = CreateButton(panel, "Play Again", font, OnPlayAgainOrRetryPressed);
            AnchorBand(_playAgainButton.GetComponent<RectTransform>(), 0.2f, 0.38f, 0.53f, 0.05f);
            _playAgainLabel = _playAgainButton.GetComponentInChildren<Text>();
            _playAgainLabel.fontSize = 22;

            _returnToCityButton = CreateButton(panel, "Return to City", font, OnReturnToCityPressed);
            AnchorBand(_returnToCityButton.GetComponent<RectTransform>(), 0.2f, 0.38f, 0.05f, 0.53f);
            _returnToCityLabel = _returnToCityButton.GetComponentInChildren<Text>();
            _returnToCityLabel.fontSize = 22;

            _resultOverlay.SetActive(false);
        }

        // ---------- Interaction ----------

        private void OnHandCardPressed(Card card)
        {
            // Direct-selection fix (Command Centre, 2026-08-15): during Formation, tapping a
            // hand card now selects it immediately for placement instead of only opening the
            // detail overlay - see SelectOrDeselectFormationHandCard's own comment for the
            // reported root cause this closes. Combat is unchanged: it still opens the detail
            // overlay here, and reinforcement selection through it already works correctly via
            // OnLanePressed's existing _selectedCard branch - only Formation was broken.
            if (_battleController.Phase == BattlePhase.Formation)
            {
                SelectOrDeselectFormationHandCard(card);
                return;
            }

            ShowCardDetail(card);
        }

        /// <summary>
        /// Reported root cause ("cards cannot be selected/placed reliably"): the only way to
        /// set _selectedCard during Formation was through the card-detail overlay's own confirm
        /// button (OnCardDetailActionPressed), and even then OnLanePressed never checked
        /// _selectedCard during Formation at all - it always reopened the lane picker,
        /// discarding the selection. This gives Formation the same direct one-tap selection
        /// Combat's reinforcement flow already had. Same affordability message
        /// OnLanePressed/OnCardDetailActionPressed's own deploy path already uses; deselecting
        /// (tapping the already-selected card again) needs no message, only reselecting.
        /// </summary>
        private void SelectOrDeselectFormationHandCard(Card card)
        {
            // Guided-tutorial gate: the real block for a UI tap already happened at the button's
            // own Button.interactable (see RefreshHand) - this re-check is what makes a direct
            // (test) call to OnHandCardPressed/HandCardPressedForTests honor the same rule.
            if (_tutorialStep != null)
            {
                string allowedCardId = TutorialAllowedCardId();
                if (allowedCardId == null || card.Id != allowedCardId)
                {
                    ShowLaneHint("Follow the tutorial: tap the highlighted card.");
                    return;
                }
            }

            bool nowSelected = _selectedCard != card;
            if (nowSelected && card.ResourceCost > _battleController.PlayerState.Resource)
            {
                ShowLaneHint($"Not enough Resource for {card.DisplayName} (needs {card.ResourceCost}).");
                return;
            }

            _selectedCard = nowSelected ? card : null;

            // Step 1 (CardCost) advances the instant its one approved card is selected - see
            // TutorialStep.CardCost's own doc comment.
            if (nowSelected && _tutorialStep == TutorialStep.CardCost && card.Id == "warrior")
            {
                AdvanceTutorialStep(TutorialStep.FrontLane);
            }

            RefreshAll();
            if (nowSelected) PopSelectedHandCard(card);
        }

        private void OnLanePressed(Lane lane)
        {
            // Guided-tutorial gate: only during Formation, where every step but the two card-
            // placement ones has no legal lane action at all (TutorialAllowedLane returns null
            // for CardCost and BeginBattle alike, blocking every lane exactly as required). The
            // real UI-level block already happened at the button's own Button.interactable (see
            // RefreshLaneButtons); this is what makes a direct (test) call honor the same rule.
            // Combat-phase gating (the SpellLesson step's single valid target) lives in
            // OnSpellTargetLanePressed instead, since armed-spell taps route through there.
            if (_tutorialStep != null && _battleController.Phase == BattlePhase.Formation)
            {
                Lane? allowedLane = TutorialAllowedLane();
                if (allowedLane == null || lane != allowedLane.Value)
                {
                    ShowLaneHint("Follow the tutorial: tap the highlighted lane.");
                    return;
                }
            }

            // A friendly-targeted spell (Mend, War Cry) being armed takes over what a player-lane
            // tap means, ahead of every other reading below - card placement and reinforcement
            // are Formation/Combat concepts that already coexist by phase, but spell targeting can
            // only be active during Combat, at the same time reinforcement taps normally are. One
            // real player lane, one tap, so the armed spell has to win outright rather than
            // needing its own separately-wired listener that could drift out of sync with this
            // method over time.
            //
            // Only when the ARMED spell actually targets a friendly lane, though - if Firestorm
            // (enemy-targeted) is armed and the player taps their own board instead of a
            // highlighted enemy lane, that is a tap outside the highlighted set and should cancel
            // targeting ("tapping anywhere else cancels"), not cast at the wrong side.
            if (_armedSpellIndex >= 0)
            {
                if (IsArmedSpellFriendlyTargeted()) OnSpellTargetLanePressed(lane);
                else CancelSpellTargeting();
                return;
            }

            // Lanes are always tappable now. Previously RefreshLaneButtons() left them
            // non-interactable whenever no card was selected, and with Transition.None a
            // non-interactable Button gives no feedback whatsoever - so tapping a slot did
            // nothing, looked like nothing, and read as broken ("I should be able to load the
            // card by clicking the space holder at the centre but it's not registering").
            // Every rejected tap now says why instead of silently no-op'ing.
            // During Formation, a lane tap with nothing selected opens that lane's picker - the
            // "lane first, then card" flow (2026-08-06), preserved as-is. Direct-selection fix
            // (Command Centre, 2026-08-15): this used to reopen the picker unconditionally, even
            // with a card already selected via SelectOrDeselectFormationHandCard - discarding the
            // selection and making direct hand-card placement impossible ("cards cannot be
            // selected/placed reliably"). With a card selected, Formation now falls through to
            // the same TryPlayCard deploy path below already used for Combat's non-reinforcement
            // case - same room-check and affordability messages, no new behavior invented.
            if (_battleController.Phase == BattlePhase.Formation && _selectedCard == null)
            {
                OpenLanePicker(lane);
                return;
            }

            if (_selectedCard == null)
            {
                ShowLaneHint("Tap a card in your hand first, then tap a lane to deploy it.");
                return;
            }

            if (!_battleController.PlayerState.Lanes[lane].HasRoomFor(_selectedCard))
            {
                ShowLaneHint($"No room in {lane} for {_selectedCard.DisplayName} (needs {_selectedCard.SlotWeight} slot(s)). Try another lane.");
                return;
            }

            // Mid-combat reinforcement goes through its own path, which enforces the window and
            // recalculates synergy for the arriving unit.
            if (_battleController.Phase == BattlePhase.Combat)
            {
                if (!_battleController.IsReinforcementWindowOpen)
                {
                    ShowLaneHint("Reinforcements are closed. The next window opens at Clash 4 and Clash 8.");
                    return;
                }

                Card reinforcement = _selectedCard;
                if (_battleController.TryDeployReinforcement(reinforcement, lane))
                {
                    _selectedCard = null;
                    RefreshAll();
                    PlayEffect(_playerLaneSlots[lane], ElementEffectSprite(reinforcement.Element), Vector2.zero, 80f, 0.6f);
                    SlideNewestCardIntoLane(lane);
                }
                else
                {
                    ShowLaneHint($"Not enough Resource to reinforce with {reinforcement.DisplayName} " +
                                 $"(needs {reinforcement.ResourceCost}).");
                }
                return;
            }

            Card playedCard = _selectedCard;
            bool played = _battleController.TryPlayCard(_battleController.PlayerState, playedCard, lane);
            if (played)
            {
                _selectedCard = null;
                AdvanceTutorialStepAfterPlacement(playedCard, lane);
                RefreshAll();
                PlayEffect(_playerLaneSlots[lane], ElementEffectSprite(playedCard.Element), Vector2.zero, 60f, 0.6f);
                SlideNewestCardIntoLane(lane);
            }
            else
            {
                ShowLaneHint($"Not enough Resource for {playedCard.DisplayName} (needs {playedCard.ResourceCost}).");
            }
        }

        /// <summary>Reuses the hand's existing hint label to explain a rejected lane tap, rather
        /// than adding a second competing message area to an already-busy screen.</summary>
        private void ShowLaneHint(string message)
        {
            if (_handHintText == null) return;
            _handHintText.text = message;
            _handHintText.gameObject.SetActive(true);
        }

        /// <summary>
        /// The single primary button. During Formation it locks the squad in and starts the
        /// fight; during Combat it's disabled, because combat advances on its own timer and
        /// there is no longer a turn for the player to end. This replaces the old
        /// "deploy two cards, press End Turn, repeat a dozen times" loop.
        /// </summary>
        private void OnPrimaryActionPressed()
        {
            if (_battleController.Phase != BattlePhase.Formation) return;

            // Start Battle's own gate is the pre-existing, path-independent one directly below
            // (HasAllApprovedTutorialStarterCardsDeployed) - it only cares whether all three
            // starters are actually on the board, not which sequence of taps put them there.
            // A stricter "must have reached guided step BeginBattle" gate used to live here too,
            // but that made Start Battle depend on the guided sequence's own bookkeeping (which
            // only advances through OnHandCardPressed/OnLanePressed) rather than on the real
            // board state - redundant with, and strictly narrower than, the check below.

            // Approved tutorial encounter, Command Centre decision 2026-08-15: the fixed
            // enemy hand (4 cards, SimpleAIOpponent deploys all of it) only produces the
            // validated, teachable encounter against a COMPLETE player formation - the normal
            // "at least one card" rule below is enough for a normal match but lets a tutorial
            // player start against a full enemy board with only one or two of their three
            // starter cards placed. Checked, and the enemy left undeployed, before
            // SimpleAIOpponent.TakeTurn runs at all - not just before ConfirmFormation.
            // Front/Middle/Back stays pure guidance (TutorialLaneGuidance) - this only requires
            // all three to be deployed SOMEWHERE, not a specific lane each.
            if (IsTutorialMatch && !HasAllApprovedTutorialStarterCardsDeployed())
            {
                ShowLaneHint("Place all three starter cards - Front, Middle, and Back - before starting the battle.");
                return;
            }

            // The opponent commits its whole formation at once too, so both squads are locked
            // in before a single clash happens - same simultaneous-commit principle the lane
            // clash already used, applied to deployment.
            SimpleAIOpponent.TakeTurn(_battleController, _aiProfile.Archetype);

            if (!_battleController.ConfirmFormation())
            {
                ShowLaneHint("Deploy at least one card into a lane before starting the battle.");
                return;
            }

            _selectedCard = null;

            if (_tutorialStep == TutorialStep.BeginBattle)
            {
                // Guided step 5 -> 6: the tutorial paces combat entirely through its own Continue
                // control (see OnTutorialContinuePressed), never the real-time CombatLoop - the
                // first exchange resolves synchronously right here, the instant Combat begins,
                // so step 6 has real numbers to show immediately rather than waiting on a timer.
                AdvanceTutorialStep(TutorialStep.FirstCombatResult);
                if (_battleController.Phase == BattlePhase.Combat)
                {
                    TurnResolutionResult result = _battleController.AdvanceCombatTick();
                    RefreshAll();
                    ShowTurnDamage(result);
                    ShowClashEffects(result);
                }
                else
                {
                    RefreshAll();
                }
                return;
            }

            RefreshAll();
            StartCombatLoop();
        }

        /// <summary>Exposed for tests: the real "Start Battle" button calls the private
        /// OnPrimaryActionPressed() directly - EditMode tests have no way to click a UI Button,
        /// so this is the only way to exercise the real handler (and the new tutorial
        /// all-three-starters gate) rather than reimplementing its behavior in a test.
        /// EndTurnForTests deliberately does not cover this - it calls ConfirmFormation()
        /// directly, bypassing this method (and its gate) entirely.</summary>
        public void StartBattleForTests() => OnPrimaryActionPressed();

        /// <summary>Exposed for tests: the real Auto Formation control (the relabeled Recommended
        /// button - see BuildPrimaryActionAndSpells) calls the private OnAutoFormationPressed()
        /// directly.</summary>
        public void AutoFormationForTests() => OnAutoFormationPressed();

        /// <summary>Release feature: true only for a normal (non-tutorial) match, in Formation,
        /// with a valid confirmed deck actually loaded, and no card placed in any player lane
        /// yet - the exact window the relabeled Auto Formation control (formerly Recommended
        /// Lineup) is visible in (see RefreshPhaseControls). The tutorial is completely excluded
        /// (IsTutorialMatch) - it always drives its own fixed starter placement through the
        /// guided TutorialStep sequence instead, never this path.</summary>
        private bool ShouldOfferAutoFormation()
        {
            return !IsTutorialMatch
                && _battleController.Phase == BattlePhase.Formation
                && _normalMatchStartError == null
                && !AnyPlayerLaneOccupied();
        }

        /// <summary>The relabeled Recommended control's real click handler (release feature:
        /// "turn the existing normal-battle Recommended control into AUTO FORMATION"). Re-checks
        /// the same conditions as ShouldOfferAutoFormation (the button's own Button.interactable/
        /// active state already blocks a real tap outside them, but every other handler in this
        /// file re-checks its own gate directly too, so a direct test call - or a stale click
        /// that lands after a state change - can never bypass it). Deliberately does not call
        /// OnLineupButtonPressed/StartNewMatch/AutoDeployRecommendedFormation - those remain
        /// fully intact and still reachable via UseRecommendedLineupForTests(), just no longer
        /// wired to any visible control.</summary>
        private void OnAutoFormationPressed()
        {
            if (!ShouldOfferAutoFormation()) return;
            PerformAutoFormation();
        }

        private bool AnyPlayerLaneOccupied() =>
            _battleController.PlayerState.Lanes.Values.Any(l => l.Cards.Count > 0);

        private bool AllPlayerLanesOccupied() =>
            _battleController.PlayerState.Lanes.Values.All(l => l.Cards.Count > 0);

        /// <summary>
        /// One-tap onboarding: places a sensible legal three-card opening formation - one card
        /// each in Front, Middle, and Back - using BattleController.TryPlayCard, the exact same
        /// production placement route a manual hand-card-then-lane tap already uses (see
        /// OnLanePressed's own non-reinforcement deploy call). No new placement rule, room check,
        /// or affordability rule is introduced; a lane is simply skipped if nothing in the
        /// current hand both fits it and is currently affordable, rather than than forcing an
        /// illegal placement. Manual placement remains fully available afterward through the
        /// unchanged OnHandCardPressed/OnLanePressed flow - this does not lock, consume, or mark
        /// any UI state beyond the cards it actually plays.
        /// </summary>
        private void PerformAutoFormation()
        {
            Lane[] lanes = { Lane.Front, Lane.Middle, Lane.Back };
            PlayerBattleState player = _battleController.PlayerState;

            foreach (Lane lane in lanes)
            {
                // One card per lane: prefer single-slot cards so all three lanes fill, then the
                // strongest affordable option (cost, then Attack, then Id). This is the beginner
                // "basic squad" path - competent Front/Middle/Back without dumping the three
                // cheapest leftovers, and without a rarity-5+ card claiming a whole lane early.
                Card candidate = player.Hand
                    .Where(c => c.ResourceCost <= player.Resource && player.Lanes[lane].HasRoomFor(c))
                    .OrderBy(c => c.SlotWeight)
                    .ThenByDescending(c => c.ResourceCost)
                    .ThenByDescending(c => c.Attack)
                    .ThenBy(c => c.Id)
                    .FirstOrDefault();

                if (candidate == null) continue;

                _battleController.TryPlayCard(player, candidate, lane);
            }

            _selectedCard = null;
            RefreshAll();
        }

        /// <summary>Exposed for tests: the real hand-card tap calls the private
        /// OnHandCardPressed() directly - the only way an EditMode test can exercise the
        /// direct-selection fix (or the Combat detail-overlay path it leaves unchanged)
        /// without simulating a UI Button click.</summary>
        public void HandCardPressedForTests(Card card) => OnHandCardPressed(card);

        /// <summary>Exposed for tests: the real player-lane tap calls the private
        /// OnLanePressed() directly - needed to prove a lane tap with a card already selected
        /// deploys it (Formation direct-selection fix), and that a lane tap with nothing
        /// selected still opens the lane-first picker flow, unchanged.</summary>
        public void LanePressedForTests(Lane lane) => OnLanePressed(lane);

        /// <summary>Exposed for tests: the id of the currently-selected hand card, or null -
        /// _selectedCard itself is private.</summary>
        public string SelectedCardIdForTests => _selectedCard?.Id;

        /// <summary>Exposed for tests: whether the lane picker overlay is currently open -
        /// _lanePickerOverlay itself is private.</summary>
        public bool IsLanePickerOpenForTests => _lanePickerOverlay != null && _lanePickerOverlay.activeSelf;

        /// <summary>See OnPrimaryActionPressed's own comment. Checks presence anywhere on the
        /// player's board, not any particular lane.</summary>
        private bool HasAllApprovedTutorialStarterCardsDeployed()
        {
            var deployedIds = new HashSet<string>(
                _battleController.PlayerState.Lanes.Values
                    .SelectMany(lane => lane.Cards)
                    .Select(card => card.Definition.Id));

            return deployedIds.Contains("warrior")
                && deployedIds.Contains("novice_knight")
                && deployedIds.Contains("goblin_caster");
        }

        /// <summary>Drives AdvanceCombatTick() on a timer once formation is locked. All the
        /// actual combat logic lives in BattleController so it stays testable - this coroutine
        /// only supplies the passage of time.</summary>
        private void StartCombatLoop()
        {
            if (!Application.isPlaying) return;
            if (_combatLoop != null) StopCoroutine(_combatLoop);
            _combatLoop = StartCoroutine(CombatLoop());
        }

        private IEnumerator CombatLoop()
        {
            while (_battleController.Phase == BattlePhase.Combat)
            {
                yield return new WaitForSeconds(CombatTickSeconds);
                if (_battleController.Phase != BattlePhase.Combat) break;

                TurnResolutionResult result = _battleController.AdvanceCombatTick();
                RefreshAll();
                ShowTurnDamage(result);
                ShowClashEffects(result);
            }
            _combatLoop = null;
        }

        /// <summary>
        /// Fires an impact effect over every lane that actually traded damage this tick, so a
        /// clash is something you watch rather than just a number that changes. Without this the
        /// automated phase had no visible activity between health-bar updates - "there should be
        /// animation during the battle and the mid game play can't be seen" (2026-08-06).
        /// </summary>
        private void ShowClashEffects(TurnResolutionResult result)
        {
            if (result.LaneResults == null) return;

            foreach (LaneClashResult lane in result.LaneResults)
            {
                bool contested = _battleController.PlayerState.Lanes[lane.Lane].Cards.Count > 0
                    || _battleController.EnemyState.Lanes[lane.Lane].Cards.Count > 0;
                if (!contested) continue;

                // A cleared lane gets the heavier effect - that's the moment damage actually
                // breaks through to an Avatar, so it should look different from a normal trade.
                bool broke = lane.SideACleared || lane.SideBCleared;
                Sprite sprite = Resources.Load<Sprite>(broke ? "UI/VFX/Critical_Slash" : "UI/VFX/Blood_Splash");
                PlayEffect(_playerLaneSlots[lane.Lane], sprite, Vector2.zero, broke ? 90f : 64f, 0.5f);
                PlayEffect(_enemyLaneSlots[lane.Lane], sprite, Vector2.zero, broke ? 90f : 64f, 0.5f);
            }
        }

        /// <summary>
        /// Floating damage numbers over each side's health bar - the concrete payoff of the
        /// PlayEffect/PlayFloatingText animation template, not just a static log entry.
        ///
        /// Parented two levels up from the fill Image, landing on the Enemy/PlayerPanel rather
        /// than the small corner HP badge itself (2026-08-06 board rework) - centering the
        /// floating number on a badge that small would cramp it against the portrait art. Two
        /// hops still resolves to the panel post-rework by coincidence of matching nesting depth
        /// (badge -> portraitBox -> panel, same depth the old bar's fill -> bg -> panel had) -
        /// verified by tracing it, not assumed, since the badge rework changed what each hop
        /// actually is. If either CreateHealthBadge's or CreatePortrait's nesting ever changes,
        /// re-check this.
        /// </summary>
        private void ShowTurnDamage(TurnResolutionResult result)
        {
            if (result.DamageDealtToSideA > 0)
            {
                PlayFloatingText(_playerHealthFill.transform.parent.parent, $"-{result.DamageDealtToSideA}", HealthBarFillColor);
            }
            if (result.DamageDealtToSideB > 0)
            {
                PlayFloatingText(_enemyHealthFill.transform.parent.parent, $"-{result.DamageDealtToSideB}", HealthBarFillColor);
            }
        }

        /// <summary>False only for AvatarStrike, which bypasses lanes entirely and hits the
        /// enemy Avatar directly - see the SpellEffect enum's own doc comment. Every other effect
        /// type needs a lane picked before it can be cast.</summary>
        private static bool RequiresLaneTargeting(SpellEffect effect) => effect != SpellEffect.AvatarStrike;

        private bool IsArmedSpellFriendlyTargeted()
        {
            if (_armedSpellIndex < 0 || _armedSpellIndex >= _battleController.Spellbook.Count) return false;
            SpellEffect effect = _battleController.Spellbook[_armedSpellIndex].Effect;
            return effect is SpellEffect.LaneHeal or SpellEffect.LaneAttackBuff;
        }

        /// <summary>
        /// The spell action bar's quick-tap entry point (2026-08-06 rework - see
        /// SpellIconPointerHandler for why this is no longer wired through Button.onClick
        /// directly). Replaces the old always-auto-target-the-busiest-enemy-lane behaviour with
        /// real targeting: affordability/cooldown are checked up front so lanes never highlight
        /// for a spell that cannot actually be cast, AvatarStrike casts immediately since it has
        /// no lane to pick, and everything else arms targeting mode instead of casting outright.
        /// </summary>
        /// <summary>The one spell (Firestorm, index 0) the guided tutorial unlocks for its
        /// step-7 lesson - see TutorialStep.SpellLesson's own doc comment.</summary>
        private const int TutorialLessonSpellIndex = 0;

        private void OnSpellTapped(int spellIndex)
        {
            // Guided-tutorial gate: no spell is usable outside step 7, and only the one approved
            // spell is usable within it - real UI-level block is the button's own gating in
            // RefreshPhaseControls; this is what makes a direct call honor the same rule.
            if (_tutorialStep != null
                && (_tutorialStep != TutorialStep.SpellLesson || spellIndex != TutorialLessonSpellIndex))
            {
                ShowLaneHint("Follow the tutorial: tap the highlighted spell.");
                return;
            }

            if (spellIndex >= _battleController.Spellbook.Count) return;
            AvatarSpell spell = _battleController.Spellbook[spellIndex];

            if (!spell.IsOffCooldown || spell.EnergyCost > _battleController.Energy)
            {
                // Was ShowLaneHint alone, which writes into the hand-hint text at the bottom of
                // the hand panel - reasonable when the rejection is about a card, invisible when
                // it's about a spell, since that whole area is empty and unwatched during Combat
                // (2026-08-06, reported as "the fire spell did not light up any lane" - most
                // likely just a rejected tap on a spell still on cooldown, but the rejection
                // itself gave no visible feedback worth noticing). Now also floats right at the
                // action bar the player just tapped, using the same mechanism damage numbers and
                // spell-cast names already use, so a rejection cannot be mistaken for nothing
                // having happened at all.
                ShowLaneHint("That spell isn't ready yet - not enough Energy, or still cooling down.");
                if (_spellBar != null)
                {
                    string reason = !spell.IsOffCooldown
                        ? $"{spell.CooldownRemaining} tick(s) left"
                        : $"needs {spell.EnergyCost} Energy";
                    PlayFloatingText(_spellBar, $"{spell.Name}: not ready ({reason})",
                        ButtonTextDisabledColor, 1.3f);
                }
                return;
            }

            if (!RequiresLaneTargeting(spell.Effect))
            {
                // Lane.Front is a required argument TryCastSpell never actually reads for this
                // effect type - confirmed by
                // BattleLogicTests.Spells_AvatarStrikeDamagesTheEnemyAvatarDirectly, which casts
                // an AvatarStrike spell with an arbitrary lane and asserts it still lands on the
                // Avatar. Any Lane value would do; Front is not meaningful here.
                CastSpellAt(spellIndex, Lane.Front);
                return;
            }

            ArmSpellTargeting(spellIndex);
        }

        /// <summary>Exposed for tests: the real spell-tile quick-tap calls the private
        /// OnSpellTapped() directly - EditMode tests have no way to simulate
        /// SpellIconPointerHandler's own pointer-down/up timing.</summary>
        public void SpellTappedForTests(int spellIndex) => OnSpellTapped(spellIndex);

        /// <summary>Exposed for tests: the real armed-spell enemy-lane tap calls the private
        /// OnSpellTargetLanePressed() directly.</summary>
        public void SpellTargetLanePressedForTests(Lane lane) => OnSpellTargetLanePressed(lane);

        /// <summary>
        /// Enters targeting mode for `spellIndex`: highlights the side its effect actually
        /// targets (the enemy board for LaneDamage, the player's own board for LaneHeal/
        /// LaneAttackBuff - see PlayCastImpact's identical friendlyTarget split, which this
        /// mirrors so a cast's impact effect always lands on the same side its targeting UI
        /// highlighted) and arms a full-screen catcher so a tap anywhere else cancels cleanly.
        /// </summary>
        private void ArmSpellTargeting(int spellIndex)
        {
            CancelSpellTargeting(); // defensive - clears any previous arm state first
            _armedSpellIndex = spellIndex;

            AvatarSpell spell = _battleController.Spellbook[spellIndex];
            bool friendlyTarget = spell.Effect is SpellEffect.LaneHeal or SpellEffect.LaneAttackBuff;

            if (!friendlyTarget)
            {
                // Player-side highlighting is handled by RefreshLaneButtons (it already runs
                // every tick and needs to know about armed-spell state regardless - see its own
                // comment on why highlight can't be set once and left alone). Only the enemy side
                // needs explicit wiring here, since nothing else ever makes it interactable.
                // Guided-tutorial gate: only the one scripted target lane highlights/accepts a
                // tap during the step-7 spell lesson - the other two enemy lanes stay exactly as
                // CancelSpellTargeting already left them (non-interactable, unhighlighted).
                bool tutorialGatesTarget = _tutorialStep == TutorialStep.SpellLesson;

                foreach (Lane lane in System.Enum.GetValues(typeof(Lane)).Cast<Lane>())
                {
                    if (tutorialGatesTarget && lane != TutorialLessonTargetLane) continue;

                    Button enemyButton = _enemyLaneButtons[lane];
                    enemyButton.interactable = true;
                    enemyButton.onClick.RemoveAllListeners();
                    Lane capturedLane = lane; // captured per iteration, not shared across closures
                    enemyButton.onClick.AddListener(() => OnSpellTargetLanePressed(capturedLane));

                    var background = enemyButton.GetComponent<Image>();
                    if (background != null)
                    {
                        background.color = new Color(SelectedColor.r, SelectedColor.g, SelectedColor.b, 0.32f);
                    }
                }
            }

            RefreshLaneButtons(); // picks up armedSpellIsFriendly immediately, not next tick
            _spellTargetCancelCatcher.SetActive(true);

            // Neither this nor CancelSpellTargeting below calls the full RefreshAll() (this
            // method deliberately avoids it - see the comment on RefreshLaneButtons just above),
            // but the teaching overlay's spotlight must still jump from the spell tile to the
            // enemy target lane the instant it arms - a stale spotlight here would leave it
            // pointing at a tile the player already tapped instead of what to tap next.
            RefreshTutorialTeachingOverlay();
        }

        /// <summary>Leaves targeting mode without casting - used both for an explicit cancel (tap
        /// anywhere outside a highlighted lane) and defensively before arming a new spell or
        /// casting the armed one, so stale highlight/listeners never survive past the moment
        /// they're relevant.</summary>
        private void CancelSpellTargeting()
        {
            _armedSpellIndex = -1;

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)).Cast<Lane>())
            {
                Button enemyButton = _enemyLaneButtons[lane];
                enemyButton.interactable = false;
                enemyButton.onClick.RemoveAllListeners();
                var background = enemyButton.GetComponent<Image>();
                if (background != null) background.color = new Color(0f, 0f, 0f, 0f);
            }

            if (_spellTargetCancelCatcher != null) _spellTargetCancelCatcher.SetActive(false);
            RefreshLaneButtons(); // clears the player-side highlight now that nothing is armed
            RefreshTutorialTeachingOverlay(); // reverts the spotlight to the spell tile after a real cancel
        }

        /// <summary>The one enemy lane the guided tutorial's spell lesson allows as a target -
        /// the sole survivor of the scripted first exchange (see StartApprovedTutorialBattle's
        /// own comment on enemyDeck for why that's deterministic).</summary>
        private const Lane TutorialLessonTargetLane = Lane.Middle;

        private void OnSpellTargetLanePressed(Lane lane)
        {
            if (_armedSpellIndex < 0) return;

            // Guided-tutorial gate: only the one scripted enemy lane is a legal target - the
            // real UI-level block is ArmSpellTargeting's own restriction (see its own comment);
            // this is what makes a direct call honor the same rule.
            if (_tutorialStep == TutorialStep.SpellLesson && lane != TutorialLessonTargetLane)
            {
                ShowLaneHint("Follow the tutorial: tap the highlighted enemy lane.");
                return;
            }

            int spellIndex = _armedSpellIndex;
            // Cleared before casting, not after - CastSpellAt calls RefreshAll(), and
            // RefreshLaneButtons reading a stale _armedSpellIndex mid-cast would re-highlight a
            // spell that has already been spent.
            CancelSpellTargeting();
            CastSpellAt(spellIndex, lane);
        }

        private void CastSpellAt(int spellIndex, Lane lane)
        {
            int energyBefore = _battleController.Energy;

            // Firestorm (and every guided-lesson spell) targets a *lane*, not the enemy Avatar
            // directly - EnemyState.AvatarHealth only ever changes from combat damage getting
            // through, never from this cast itself, so it was always going to read "22 -> 22" and
            // misleadingly imply the spell did nothing (reported 2026-08-16). Capture the actual
            // targeted unit's own identity/Health before the cast (it may be destroyed by it, so
            // this must happen before TryCastSpell, not after).
            BattleCardInstance tutorialTargetBefore = null;
            int tutorialTargetHealthBefore = 0;
            if (_tutorialStep == TutorialStep.SpellLesson && lane == TutorialLessonTargetLane)
            {
                tutorialTargetBefore = _battleController.EnemyState.Lanes[lane].Cards.FirstOrDefault();
                tutorialTargetHealthBefore = tutorialTargetBefore?.CurrentHealth ?? 0;
            }

            if (!_battleController.TryCastSpell(spellIndex, lane, out int avatarDamage))
            {
                ShowLaneHint("That spell isn't ready yet - not enough Energy, or still cooling down.");
                return;
            }

            AvatarSpell cast = _battleController.Spellbook[spellIndex];

            // Guided step 7 -> 8: build the "before/after" summary (task requirement: "Show
            // Energy before/after and the changed damage/HP number") from the exact same values
            // just used to cast, then advance - see TutorialStepCaption's own use of this string.
            if (_tutorialStep == TutorialStep.SpellLesson)
            {
                bool stillPresent = tutorialTargetBefore != null
                    && _battleController.EnemyState.Lanes[lane].Cards.Contains(tutorialTargetBefore);
                string outcome = tutorialTargetBefore == null
                    ? "No enemy unit was there to strike."
                    : stillPresent
                        ? $"{tutorialTargetBefore.Definition.DisplayName} Health {tutorialTargetHealthBefore} -> {tutorialTargetBefore.CurrentHealth}."
                        : $"{tutorialTargetBefore.Definition.DisplayName} in the Middle lane destroyed!";
                _tutorialSpellCastSummary =
                    $"{cast.Name} cast! Energy {energyBefore} -> {_battleController.Energy}. {outcome}";
                AdvanceTutorialStep(TutorialStep.Finish);
            }

            RefreshAll();
            PlayCastImpact(cast, lane);

            if (avatarDamage > 0)
            {
                PlayFloatingText(_enemyHealthFill.transform.parent.parent, $"-{avatarDamage}", HealthBarFillColor);
            }
        }

        /// <summary>Floating tooltip for a held spell icon - the name/cost/effect text that no
        /// longer fits the 64px tile itself (2026-08-06 icon-bar rework). Positioned above the
        /// held tile rather than at a fixed screen location, so it always reads next to the
        /// thing it describes regardless of which of the four tiles was held.</summary>
        private void ShowSpellTooltip(int spellIndex, RectTransform tileRect)
        {
            if (spellIndex >= _battleController.Spellbook.Count || _spellTooltip == null) return;
            AvatarSpell spell = _battleController.Spellbook[spellIndex];

            _spellTooltipText.text = $"{spell.Name}\n{spell.EnergyCost} Energy\n{spell.Description}";

            var tooltipRect = (RectTransform)_spellTooltip.transform;
            tooltipRect.position = tileRect.position + new Vector3(0f, tileRect.rect.height * 1.4f, 0f);
            _spellTooltip.SetActive(true);
        }

        private void HideSpellTooltip()
        {
            if (_spellTooltip != null) _spellTooltip.SetActive(false);
        }

        /// <summary>
        /// The full-screen invisible catcher that cancels spell targeting when the player taps
        /// anywhere that isn't a highlighted lane - the exact same "tap the dim layer to close"
        /// pattern CardDetailOverlay and TutorialOverlay already use, reused here rather than
        /// invented fresh.
        ///
        /// Built EARLY (right after BuildBattleBackdrop, before any panel that contains a lane
        /// button) so it sits at a low sibling index and every lane button - built after it -
        /// naturally wins the raycast over it. Unity's GraphicRaycaster checks the TOPMOST
        /// (highest sibling index) hit first, so building this any later would make it the
        /// topmost element on screen and it would swallow every tap, including ones landing
        /// directly on a highlighted lane button underneath it - the opposite of its job.
        /// Battle Release Layout pass: BuildBattleBackdrop's arena backdrop + dim overlay now
        /// both set raycastTarget = false (a real bug fixed in this same pass - see that
        /// method's own comment), so this ordering is no longer load-bearing against them
        /// specifically, only against the real interactive panels built after it.
        /// </summary>
        private void BuildSpellTargetCancelCatcher(Transform canvasTransform)
        {
            _spellTargetCancelCatcher = new GameObject("SpellTargetCancelCatcher", typeof(RectTransform));
            _spellTargetCancelCatcher.transform.SetParent(canvasTransform, false);
            StretchFull((RectTransform)_spellTargetCancelCatcher.transform);
            Image catcherImage = _spellTargetCancelCatcher.AddComponent<Image>();
            catcherImage.color = new Color(0f, 0f, 0f, 0f); // invisible, still a raycast target
            Button catcherButton = _spellTargetCancelCatcher.AddComponent<Button>();
            catcherButton.transition = Selectable.Transition.None;
            catcherButton.onClick.AddListener(CancelSpellTargeting);
            _spellTargetCancelCatcher.SetActive(false);
        }

        /// <summary>The held-spell-icon tooltip. Unlike the cancel catcher, sibling order does
        /// not matter here - both its background and text are explicitly raycastTarget = false,
        /// so it never competes for a tap regardless of where it sits in the hierarchy.</summary>
        private void BuildSpellTooltip(Transform canvasTransform, Font font)
        {
            _spellTooltip = new GameObject("SpellTooltip", typeof(RectTransform));
            _spellTooltip.transform.SetParent(canvasTransform, false);
            var tooltipRect = (RectTransform)_spellTooltip.transform;
            tooltipRect.sizeDelta = new Vector2(280f, 110f);
            tooltipRect.pivot = new Vector2(0.5f, 0f);

            Image tooltipBg = _spellTooltip.AddComponent<Image>();
            tooltipBg.sprite = CreateRoundedGradientSprite(new Color(0.05f, 0.05f, 0.08f, 0.96f),
                new Color(0.02f, 0.02f, 0.04f, 0.96f), cornerRadius: 14);
            tooltipBg.type = Image.Type.Sliced;
            tooltipBg.raycastTarget = false;

            _spellTooltipText = CreateText(_spellTooltip.transform, "", 14, Color.white, font);
            _spellTooltipText.raycastTarget = false;
            StretchFull(_spellTooltipText.rectTransform);
            _spellTooltipText.rectTransform.offsetMin = new Vector2(10f, 8f);
            _spellTooltipText.rectTransform.offsetMax = new Vector2(-10f, -8f);

            _spellTooltip.SetActive(false);
        }

        /// <summary>
        /// The visual payoff for casting a spell. A single small sprite fading over one lane was
        /// reported as "not appealing at all - the animation should show up impactful when the
        /// skill is activated" (2026-08-06), so a cast now lands as three layered beats:
        /// a full-screen colour flash, a large effect burst over the targeted lane, and the
        /// spell's name punched over the board. All three use the existing PlayEffect /
        /// PlayFloatingText / coroutine template rather than a new animation system.
        ///
        /// A heal targets the player's own lane, everything else the enemy's - firing a heal
        /// effect over the enemy board would read as damage.
        /// </summary>
        private void PlayCastImpact(AvatarSpell spell, Lane targetLane)
        {
            bool friendlyTarget = spell.Effect is SpellEffect.LaneHeal or SpellEffect.LaneAttackBuff;
            Transform anchor = friendlyTarget ? _playerLaneSlots[targetLane] : _enemyLaneSlots[targetLane];

            PlayEffect(anchor, SpellEffectSprite(spell.Effect), Vector2.zero, 190f, 0.85f);
            PlayScreenFlash(SpellFlashColor(spell.Effect));
            PlayFloatingText(anchor, spell.Name.ToUpperInvariant(), SpellFlashColor(spell.Effect), 1.0f);
        }

        private static Color SpellFlashColor(SpellEffect effect) => effect switch
        {
            SpellEffect.LaneDamage => new Color(1f, 0.45f, 0.15f),
            SpellEffect.LaneHeal => new Color(0.4f, 1f, 0.55f),
            SpellEffect.LaneAttackBuff => new Color(1f, 0.85f, 0.3f),
            SpellEffect.AvatarStrike => new Color(0.6f, 0.8f, 1f),
            _ => Color.white,
        };

        /// <summary>A brief full-screen colour wash - the cheapest way to make an action read as
        /// "big" without any new art. Non-raycast so it never swallows a tap mid-fade.</summary>
        private void PlayScreenFlash(Color color)
        {
            if (!Application.isPlaying || _canvasTransform == null) return;

            Image flash = CreateImage(_canvasTransform, new Color(color.r, color.g, color.b, 0.34f));
            flash.raycastTarget = false;
            StretchFull(flash.rectTransform);
            flash.transform.SetAsLastSibling();
            StartCoroutine(FadeAndDestroy(flash, 0.45f));
        }

        private static IEnumerator FadeAndDestroy(Image image, float duration)
        {
            Color start = image.color;
            float elapsed = 0f;
            while (elapsed < duration && image != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                image.color = new Color(start.r, start.g, start.b, Mathf.Lerp(start.a, 0f, t));
                yield return null;
            }
            if (image != null) Destroy(image.gameObject);
        }

        private static Sprite SpellEffectSprite(SpellEffect effect) => effect switch
        {
            SpellEffect.LaneDamage => Resources.Load<Sprite>("UI/VFX/Fire_Explosion"),
            SpellEffect.LaneHeal => Resources.Load<Sprite>("UI/VFX/Heal_Ring"),
            SpellEffect.LaneAttackBuff => Resources.Load<Sprite>("UI/VFX/Magic_Circle"),
            SpellEffect.AvatarStrike => Resources.Load<Sprite>("UI/VFX/Lightning_Strike"),
            _ => null,
        };

        private static Sprite ElementEffectSprite(CardElement element) => element switch
        {
            CardElement.Andras => Resources.Load<Sprite>("UI/VFX/Holy_Beam"),
            CardElement.Ktini => Resources.Load<Sprite>("UI/VFX/Shadow_Explosion"),
            CardElement.Pnevmas => Resources.Load<Sprite>("UI/VFX/Magic_Circle"),
            _ => null,
        };

        /// <summary>
        /// Exposed for tests: locks formation and steps combat once, exercising a full
        /// RefreshAll() cycle a second time. Uses AdvanceCombatTick() directly rather than the
        /// coroutine loop, which can't run outside Play Mode.
        /// </summary>
        public void EndTurnForTests()
        {
            if (_battleController.Phase == BattlePhase.Formation)
            {
                SimpleAIOpponent.TakeTurn(_battleController, _aiProfile.Archetype);
                _battleController.ConfirmFormation();
            }
            _battleController.AdvanceCombatTick();
            RefreshAll();
        }

        /// <summary>Exposed for tests: restarts the match on the "Recommended" deck path, the
        /// same thing the Recommended button does.</summary>
        public void UseRecommendedLineupForTests() => OnLineupButtonPressed(useRecommendedDeck: true);

        /// <summary>Exposed for tests: restarts the match on the "Reset Lineup" path (fresh
        /// random deck, saved deck bypassed), the same thing the Reset button does.</summary>
        public void ResetLineupForTests() => OnLineupButtonPressed(useRecommendedDeck: false);

        private void OnCardDetailActionPressed()
        {
            if (_previewedCard == null) return;
            bool nowSelected = _selectedCard != _previewedCard;
            _selectedCard = nowSelected ? _previewedCard : null;
            Card justChosen = _previewedCard;
            CloseCardDetail();
            RefreshAll();

            // "The card should animate when chosen" (2026-08-06). RefreshAll() rebuilds the hand
            // from scratch, so the pop has to be started on the *new* button afterwards - the one
            // this card had before the refresh no longer exists.
            if (nowSelected) PopSelectedHandCard(justChosen);
        }

        private void HandleMatchEnded(bool playerWon)
        {
            // First-time Campaign onboarding, requirement 5: captured before anything else runs
            // (including this same event's other subscriber, HomePagePresenter.
            // HandleMatchCompleted, which clears _pendingCampaignStage on a victory) so the
            // Campaign-specific next-action line below is correct regardless of subscriber order.
            // Reward/unlock logic itself is untouched - this only reads the field, never writes it.
            bool wasCampaignMatch = _pendingCampaignStage != null;

            // The approved offline tutorial (StartApprovedTutorialBattle) must have no
            // progression effect - "makes no server call, confirms no victory, advances no
            // checkpoint" per that method's own comment. RecordMatchResult mutates
            // avatarLevel/totalMatches/totalWins/winStreak; even though it does not call Save()
            // itself, that in-memory mutation can leak to disk later via any unrelated save
            // (Shop/DeckBuilder/Collection returning home, or a subsequent normal match win) -
            // skipping the call entirely for a tutorial match is what actually keeps the result
            // local rather than merely deferring when it gets written. HomePagePresenter's own
            // reward guard (HandleMatchCompleted's IsTutorialMatch check) already does the same
            // for gold/gems/stage-unlocks; this is that same rule applied to this call site.
            if (!IsTutorialMatch)
            {
                // The actual "how do I get stronger and beat the Avatar" loop: winning raises
                // Avatar level more than losing does, and that level feeds directly into the next
                // match's ResourceCap/HP/deck size (see PlayerEmpireData.ApplyMatchResult). Applied
                // immediately so the level shown below already reflects the match that just ended.
                _profile.RecordMatchResult(playerWon);
            }

            if (IsTutorialMatch)
            {
                // Approved tutorial-only copy and single-button flow (Command Centre decision,
                // 2026-08-15): the tutorial never shows the normal match's Avatar Level/Resource/
                // HP progression line - that reflects RecordMatchResult, which the guard above
                // never runs for a tutorial match, so showing it here would be reporting numbers
                // this match had no part in changing.
                _resultText.text = playerWon
                    ? "Victory. The first threat has been driven back."
                    : "Defeat. Adjust your formation and try again.";
                _returnToCityLabel.text = "Return to Empire";
                _playAgainLabel.text = "Retry Battle";
                // Exactly one of the two exits applies to a tutorial outcome - victory returns to
                // Home (the existing Return to City handler, unchanged), defeat retries the
                // approved encounter (see OnPlayAgainOrRetryPressed). Both must never show
                // together outside normal play.
                _returnToCityButton.gameObject.SetActive(playerWon);
                _playAgainButton.gameObject.SetActive(!playerWon);
            }
            else
            {
                // A match decided on the tick cap explains itself rather than claiming an Avatar
                // fell when neither did - BattleController.OutcomeReason carries that wording.
                string headline = !string.IsNullOrEmpty(_battleController.OutcomeReason)
                    ? _battleController.OutcomeReason
                    : playerWon
                        ? "VICTORY - the enemy Avatar has fallen."
                        : "DEFEAT - your Avatar has fallen.";

                // First-time Campaign onboarding, requirement 5: the one next-action line, on
                // this same existing result-overlay text - no new panel/text element. Reward and
                // unlock logic itself lives entirely in HomePagePresenter.HandleMatchCompleted,
                // unchanged by this addition.
                string campaignNextAction = wasCampaignMatch
                    ? (playerWon
                        ? "\nReturn home to continue to the next unlocked stage."
                        : "\nRetry costs 1 Stamina, or return home.")
                    : string.Empty;

                _resultText.text = $"{headline}\n" +
                    $"Avatar Level {_empireData.AvatarLevel} - next match: " +
                    $"{_empireData.ResourceCap} Resource, {_empireData.StartingAvatarHealth} HP." +
                    campaignNextAction;
                // Restores the normal, always-both-visible/normally-labelled state - covers a
                // normal match starting right after a tutorial one, whose HandleMatchEnded call
                // would otherwise have left the tutorial's single-button state in place.
                _returnToCityLabel.text = "Return to City";
                _playAgainLabel.text = "Play Again";
                _returnToCityButton.gameObject.SetActive(true);
                _playAgainButton.gameObject.SetActive(true);
            }

            _resultOverlay.SetActive(true);

            // Purely additive, same reasoning as StartApprovedTutorialBattle's own opening-
            // cinematic call: the result overlay above is already fully configured and active
            // exactly as before (unconditionally, for every outcome) - this only shows a
            // blocking cinematic on top of it in Play Mode, for a confirmed tutorial victory
            // only. Never for a normal match, never for tutorial defeat (see the handoff's own
            // "Tutorial defeat: no cinematic" and "Never show this sequence for normal battles").
            if (IsTutorialMatch && playerWon) BeginVictoryCinematic();
        }

        private void OnPlayAgainPressed()
        {
            _resultOverlay.SetActive(false);
            _selectedCard = null;
            StartNewMatch();
            RefreshAll();
        }

        /// <summary>Exposed for tests: restarts the match on the "Play Again" path (ordinary
        /// replay, saved deck allowed - see StartNewMatch's own comment), the same thing the
        /// Play Again button does.</summary>
        public void PlayAgainForTests() => OnPlayAgainPressed();

        /// <summary>
        /// What the same physical button (labelled "Play Again" normally, "Retry Battle" for a
        /// tutorial defeat - see HandleMatchEnded) actually does depends on IsTutorialMatch: a
        /// tutorial retry must restart the approved offline encounter via
        /// StartApprovedTutorialBattle(), never StartNewMatch() - that path picks a normal deck,
        /// a normal opponent, and (per HandleMatchEnded's own guard) would silently start
        /// recording real progression again. OnPlayAgainPressed's own normal-match body is
        /// reused as-is for the non-tutorial case.
        /// </summary>
        private void OnPlayAgainOrRetryPressed()
        {
            if (IsTutorialMatch)
            {
                _resultOverlay.SetActive(false);
                _selectedCard = null;
                // A retry after tutorial defeat re-enters Formation directly - the opening
                // cinematic is a one-time introduction (Start Tutorial), not something a retry
                // should replay. Tutorial defeat itself never shows a cinematic at all (see
                // HandleMatchEnded), so this call is reached only from the defeat result screen.
                StartApprovedTutorialBattle(showOpeningCinematic: false);
                RefreshAll();
            }
            else
            {
                // Campaign stamina-entry contract, requirement 4/5: a retry of a Campaign stage
                // (a non-tutorial Play Again/Retry while a stage is still the pending battle
                // configuration) is a new attempt and costs 1 Stamina again - a plain normal-
                // match "Play Again" (_pendingCampaignStage == null) costs nothing, matching
                // requirement 6. Checked and spent BEFORE OnPlayAgainPressed/StartNewMatch runs,
                // so an insufficient balance blocks the retry outright: the current (already-
                // resolved) result screen simply stays up, no new match is built, and nothing
                // else is touched - the existing Campaign status/log surface (requirement 10) is
                // a Debug.LogError here since Battle is already on-screen with no caption slot
                // free to reuse mid-result.
                if (_pendingCampaignStage != null && !TrySpendCampaignStaminaForAttempt())
                {
                    Debug.LogError($"Campaign stage {_pendingCampaignStage.stageId}: insufficient Stamina - retry blocked.");
                    return;
                }

                OnPlayAgainPressed();
            }
        }

        /// <summary>Exposed for tests: triggers the real button the result overlay's
        /// "Play Again"/"Retry Battle" slot actually calls - the only way an EditMode test can
        /// exercise the tutorial-vs-normal branch in OnPlayAgainOrRetryPressed without
        /// simulating a UI click.</summary>
        public void RetryForTests() => OnPlayAgainOrRetryPressed();

        /// <summary>
        /// The battle/metagame handoff (2026-08-06): hides this entire battle screen so a
        /// non-battle system (campaign map, home screen) can take over. Deliberately does NOT
        /// touch BattleController.OnMatchCompleted - that already fired the moment the match
        /// resolved (see BattleController.RaiseMatchEnded's own reasoning for why), carrying
        /// everything a listener needs. This button's only job is getting the battle UI itself
        /// out of the way once the player is done looking at the result.
        ///
        /// RESOLVED 2026-08-15: OnReturnToCityRequested (see its own comment) is the other half
        /// of this handoff this comment used to flag as missing - fired here, after the canvas
        /// is already hidden, so any listener's own show-my-screen-again logic runs against a
        /// battle screen that's already out of the way.
        /// </summary>
        private void OnReturnToCityPressed()
        {
            _canvasTransform.gameObject.SetActive(false);
            // Explicit Stop(), not left to the canvas SetActive(false) above - the music
            // AudioSource lives under the canvas today, but relying on deactivation alone to
            // silence it would silently break if that ever changed, and Home must never hear
            // battle music under any circumstance.
            _musicSource?.Stop();

            // Campaign match-context lifecycle contract, requirement 4: returning to Home from
            // EITHER a campaign victory or a campaign defeat clears the pending battle
            // configuration - this is the ONLY clear point for the defeat case (a victory already
            // cleared it in HandleMatchCompleted, so this is a harmless no-op there). Fires
            // unconditionally, same as this whole method already does for both outcomes.
            _pendingCampaignStage = null;

            OnReturnToCityRequested?.Invoke();
        }

        /// <summary>Exposed for tests: the result overlay's "Return to City" button calls the
        /// private OnReturnToCityPressed() directly - EditMode tests have no way to click a UI
        /// Button, so this is the only way to exercise the real handler (and the real
        /// OnReturnToCityRequested firing) rather than reimplementing its behavior in a test.
        /// Same narrow, ...ForTests()-suffixed pattern as UseRecommendedLineupForTests below.</summary>
        public void ReturnToCityForTests() => OnReturnToCityPressed();

        /// <summary>The other half of the battle/metagame handoff: lets a non-battle system
        /// (home screen, campaign map) show this battle screen again via <see cref="Instance"/>
        /// without needing a Canvas-hierarchy lookup of its own - see OnReturnToCityPressed's own
        /// note on why that lookup can't work (BattleController has no Canvas ancestor).
        ///
        /// Bug fix: GameBootstrap.Initialize() runs once, at app boot, before the player has
        /// ever visited Deck Builder in this session - Home starts with the battle canvas hidden
        /// (SetBattleCanvasVisible(false)) and only reveals it later via this method's own
        /// "To Battle" call site. Without a refresh here, that reveal showed the SAME match
        /// StartNewMatch() built at boot time, dealt from whatever activeDeckCardIds existed
        /// then - so confirming a new deck in Deck Builder and going straight to Battle dealt
        /// the old (or no) deck instead of the just-confirmed one ("Deck Builder saves a
        /// confirmed 10-card deck, but normal Battle deals unrelated cards"). A hidden-to-visible
        /// transition for a NORMAL entry now rebuilds the match from the current saved deck
        /// first, so TryBuildSavedPlayerDeck always reads whatever is confirmed at the moment
        /// the player actually enters Battle, not whatever existed at process start.
        ///
        /// Skipped when IsTutorialMatch is already true: StartApprovedTutorialBattle() always
        /// runs immediately before this call on the tutorial entry path (see HomePagePresenter's
        /// own call order) and must not be immediately stomped by a normal-match refresh right
        /// after setting itself up - the tutorial stays completely isolated on its fixed starter
        /// deck either way (StartApprovedTutorialBattle never reads activeDeckCardIds), but
        /// refreshing here would still needlessly discard the tutorial's own already-correct
        /// state and restart the combat loop coroutine a second time.
        /// </summary>
        public void SetBattleCanvasVisible(bool visible)
        {
            bool wasHidden = _canvasTransform != null && !_canvasTransform.gameObject.activeSelf;
            if (_canvasTransform != null) _canvasTransform.gameObject.SetActive(visible);

            if (visible && wasHidden && !IsTutorialMatch)
            {
                StartNewMatch();
                RefreshAll();
            }
        }

        private void OnLineupButtonPressed(bool useRecommendedDeck)
        {
            _resultOverlay.SetActive(false);
            _selectedCard = null;
            // allowSavedDeck: false - "Reset Lineup" (useRecommendedDeck: false, from here) must
            // stay a true fresh random reset, matching its own name and the player-facing UI;
            // it must never silently become "reload my saved deck" once one exists. Has no
            // effect on the useRecommendedDeck: true ("Recommended") path, which never reads
            // the saved deck regardless.
            StartNewMatch(useRecommendedDeck, allowSavedDeck: false);

            if (useRecommendedDeck)
            {
                // 2026-08-06, reported as "the recommendation is not working whereby it should
                // select the card for the players": this only ever rebuilt the *deck*, which is
                // invisible - the board still came up empty and you still had to place all nine
                // cards by hand. Under the formation model the useful thing to recommend is the
                // formation itself, so it now actually deploys the squad for you.
                AutoDeployRecommendedFormation();
            }

            RefreshAll();
        }

        /// <summary>
        /// Fills the board with the strongest affordable squad, placing each card in the lane
        /// that suits it: Knights (Taunt) to Middle where the +1 Health compounds their job of
        /// soaking damage, Strategists and Perfects to Back where Perfect's draw hook fires,
        /// everything else to Front for the +1 Attack. Strongest cards go down first so that if
        /// resource runs out, it's the weakest cards that get left behind.
        /// </summary>
        private void AutoDeployRecommendedFormation()
        {
            if (_battleController.Phase != BattlePhase.Formation) return;

            PlayerBattleState player = _battleController.PlayerState;

            static List<Card> ByStrength(IEnumerable<Card> cards) => cards
                .OrderByDescending(c => c.Attack + c.Health)
                .ThenBy(c => c.ResourceCost)
                .ToList();

            // Two passes, not one. A single strongest-first pass with per-card lane fallback
            // lets an early, powerful Warrior spill into the Back lane before any Strategist has
            // been considered - and once slot weighting arrived (a rarity 5+ card eats two of a
            // lane's three slots) the preferred lanes fill fast enough that this happened often.
            // Placing every card in its *ideal* lane first, then filling the remainder anywhere,
            // makes class fit beat raw ordering, which is what "recommended" should mean.
            foreach (Card card in ByStrength(player.Hand.ToList()))
            {
                Lane preferred = PreferredLanesFor(card).First();
                if (player.Lanes[preferred].HasRoomFor(card))
                {
                    _battleController.TryPlayCard(player, card, preferred);
                }
            }

            foreach (Card card in ByStrength(player.Hand.ToList()))
            {
                foreach (Lane lane in PreferredLanesFor(card))
                {
                    if (!player.Lanes[lane].HasRoomFor(card)) continue;
                    if (_battleController.TryPlayCard(player, card, lane)) break;
                }
            }
        }

        /// <summary>Lane preference order for auto-deployment, most-suitable first, with the
        /// remaining lanes as fallbacks so a card is never left in hand just because its ideal
        /// lane happened to be full.</summary>
        private static IEnumerable<Lane> PreferredLanesFor(Card card) => card.Class switch
        {
            CardClass.Knight => new[] { Lane.Middle, Lane.Front, Lane.Back },
            CardClass.Strategist => new[] { Lane.Back, Lane.Middle, Lane.Front },
            CardClass.Perfect => new[] { Lane.Back, Lane.Front, Lane.Middle },
            _ => new[] { Lane.Front, Lane.Middle, Lane.Back },
        };

        // ---------- Refresh ----------

        /// <summary>Exposed for tests: production always reaches RefreshAll() through some real
        /// action (AdvanceCombatTick is normally driven by the CombatLoop coroutine, which then
        /// calls this) - a test that calls BattleController.AdvanceCombatTick() directly skips
        /// that coroutine entirely, so the caption/UI surfaces this file owns (including the
        /// Campaign guidance caption) never update unless a test calls this too.</summary>
        public void RefreshAllForTests() => RefreshAll();

        private void RefreshAll()
        {
            RefreshLaneSlots(_battleController.EnemyState, _enemyLaneSlots, _enemyLaneTotalTexts, isEnemySide: true);
            RefreshLaneSlots(_battleController.PlayerState, _playerLaneSlots, _playerLaneTotalTexts, isEnemySide: false);
            RefreshLaneButtons();
            PlayerBattleState enemy = _battleController.EnemyState;
            PlayerBattleState player = _battleController.PlayerState;

            _enemyHealthFill.fillAmount = enemy.MaxAvatarHealth > 0 ? (float)enemy.AvatarHealth / enemy.MaxAvatarHealth : 0f;
            // Badge text is HP only now (2026-08-06, corner-badge rework) - the old long form
            // ("Wyvern Tamer Kaelen (Veteran) - HP: 299/299") was sized for a full-width bar and
            // does not fit a compact badge. Name + difficulty moved to the portrait's own name
            // label instead (_enemyNameLabel), refreshed here for the same reason the old text
            // was refreshed every tick: _aiProfile is re-derived every match (see
            // SoloAIScalingSystem), so the opponent shown must be able to change without a full
            // Initialize() - a name set once at BuildHeaderBar time would go stale after "Play
            // Again" faced a different opponent.
            _enemyAvatarText.text = $"{enemy.AvatarHealth}/{enemy.MaxAvatarHealth}";
            if (_enemyNameLabel != null)
            {
                _enemyNameLabel.text = $"{_aiProfile.DisplayName} ({_aiProfile.DifficultyTier})".ToUpperInvariant();
            }

            _playerHealthFill.fillAmount = player.MaxAvatarHealth > 0 ? (float)player.AvatarHealth / player.MaxAvatarHealth : 0f;
            _playerAvatarText.text = $"{player.AvatarHealth}/{player.MaxAvatarHealth}";

            // During Combat the resource bar becomes the Energy bar - resource is spent entirely
            // in Formation and is meaningless afterwards, whereas Energy is the only economy
            // that matters once the squad is locked in. Reusing the one bar keeps the HUD from
            // carrying a second meter that's dead half the time.
            bool inCombat = _battleController.Phase == BattlePhase.Combat;
            if (inCombat)
            {
                _resourceFill.fillAmount = _battleController.MaxEnergy > 0
                    ? (float)_battleController.Energy / _battleController.MaxEnergy : 0f;
                _resourceText.text = $"Energy: {_battleController.Energy}/{_battleController.MaxEnergy}";
                // Shows the tick cap so the player can see the fight is finite, and calls out a
                // reinforcement window while it's open - it lasts one tick, so it has to be
                // impossible to miss.
                _turnText.text = _battleController.IsReinforcementWindowOpen
                    ? $"REINFORCE! {_battleController.TickCount}/{BattleController.MaxCombatTicks}"
                    : $"Clash {_battleController.TickCount}/{BattleController.MaxCombatTicks}";
            }
            else
            {
                _resourceFill.fillAmount = player.ResourceCap > 0 ? (float)player.Resource / player.ResourceCap : 0f;
                _resourceText.text = $"Resource: {player.Resource}/{player.ResourceCap}";
                _turnText.text = "Formation";
            }
            _deckCountText.text = $"Deck: {player.DrawPile.Count}";

            if (_synergyText != null)
            {
                // Before formation locks, preview what the currently-deployed squad would earn,
                // so composition can be adjusted while it still can be. After locking, show what
                // was actually applied.
                SynergyBonus bonus = _battleController.Phase == BattlePhase.Formation
                    ? FormationSynergy.Calculate(player.Lanes.Values.SelectMany(l => l.Cards).Select(c => c.Definition))
                    : _battleController.PlayerSynergy;
                _synergyText.text = FormationSynergy.Describe(bonus);
            }

            // V3 header addition: enemy Resource, always shown (existing EnemyState.Resource -
            // see the field's own comment for why this wasn't surfaced before).
            if (_enemyResourceFill != null)
            {
                _enemyResourceFill.fillAmount = enemy.ResourceCap > 0 ? (float)enemy.Resource / enemy.ResourceCap : 0f;
                _enemyResourceText.text = $"{enemy.Resource}/{enemy.ResourceCap}";
            }

            // V3 header addition: HAND n, next to the player's own Resource/Hand cluster -
            // existing data (PlayerState.Hand.Count), previously only shown as card thumbnails
            // themselves, never as its own number.
            if (_handCountText != null)
            {
                _handCountText.text = $"HAND {player.Hand.Count}";
            }

            // V3 hand/placement panel addition: passive "Selected Card / Place In" status text -
            // reports _selectedCard, the same field OnHandCardPressed/OnLanePressed already read
            // and write; this never itself changes selection (see BuildHandAndPlacementPanel).
            if (_selectedCardText != null)
            {
                _selectedCardText.text = _selectedCard == null
                    ? "SELECTED CARD\n\n(none - tap a hand\ncard or an empty lane)"
                    : $"SELECTED CARD\n{_selectedCard.DisplayName}\n\nPLACE IN\nFRONT / MIDDLE / BACK";
            }

            RefreshActivityLog();
            RefreshPhaseControls();
            RefreshHand();
            RefreshTutorialStepControls();
        }

        /// <summary>
        /// Combat Tick Feed (2026-08-22, owner: "combat after Formation feels like autopilot,
        /// I can't tell what happened each tick") - replaces this rail's previous terse numeric
        /// dump ("CLASH 3 - Dmg P0 E12 (HP P188 E76)") with CombatFeedFormatter's plain-language
        /// lines (lane deaths, overflow, siege, spell casts), built from the same real, already-
        /// resolved BattleController.CombatLedger/SpellCastLog this rail always read - no new data
        /// source, no combat-math change, purely a readability pass on the same facts. Still the
        /// latest up to 6 lines, newest first. Shows a phase status line instead when no tick has
        /// resolved yet (Formation), rather than leaving the rail visually empty - matching the
        /// mockup, which shows "FORMATION PHASE / CLASH 0/12" as the rail's first line even before
        /// Combat.
        /// </summary>
        private void RefreshActivityLog()
        {
            if (_activityLogText == null) return;

            IReadOnlyList<CombatTickRecord> ledger = _battleController.CombatLedger;
            if (ledger.Count == 0)
            {
                _activityLogText.text = _battleController.Phase == BattlePhase.Formation
                    ? $"FORMATION PHASE\nCLASH 0/{BattleController.MaxCombatTicks}"
                    : "No ticks resolved yet.";
                return;
            }

            List<string> lines = CombatFeedFormatter.BuildFeedLines(ledger, _battleController.SpellCastLog, maxLines: 6);
            _activityLogText.text = string.Join("\n", lines);
        }

        /// <summary>
        /// Swaps the bottom action row between the Formation button and the Combat spell bar,
        /// and keeps each spell's label showing why it can or can't be cast right now (cost,
        /// cooldown) rather than leaving the player to guess at a dead button.
        ///
        /// The label used to carry the spell's name and effect ("Firestorm / 4 dmg to a lane /
        /// 30 Energy") - that doesn't fit a 64px icon tile (2026-08-06 rework, see
        /// BuildEndTurnButton). It now carries only the number that matters this instant: what it
        /// costs while ready, or how many ticks until it is. The full name/effect is a real loss
        /// from the tile itself - a compact icon bar trades that away for size, the same tradeoff
        /// CreateCardButton's cost chip made for hand cards. SpellIconSprite is what's meant to
        /// carry "which spell is this" now, and the card detail-style full description doesn't
        /// have anywhere to live yet; that's a gap worth a real tooltip/hold-to-inspect later.
        /// </summary>
        private void RefreshPhaseControls()
        {
            if (_primaryActionButton == null || _spellBar == null) return;

            bool formation = _battleController.Phase == BattlePhase.Formation;
            bool inCombat = _battleController.Phase == BattlePhase.Combat;
            bool resolved = _battleController.Phase == BattlePhase.Resolved;

            // First-normal-battle onboarding, release repair: with no confirmed valid deck at
            // all, there is nothing to auto-format or start with (RefreshHand already shows no
            // cards - see StartNewMatch's own comment) - the primary action is hidden entirely
            // and RefreshNormalMatchGuidanceCaption (below, via RefreshTutorialStepControls)
            // carries the only instruction the player needs: return to Deck Builder.
            bool normalDeckBlocked = !IsTutorialMatch && _normalMatchStartError != null;
            _primaryActionButton.gameObject.SetActive(formation && !normalDeckBlocked);
            // Guided-tutorial gate: real UI-level block for Start Battle, mirroring the logical
            // gate already in OnPrimaryActionPressed itself.
            _primaryActionButton.interactable = _tutorialStep == null || _tutorialStep == TutorialStep.BeginBattle;

            // Visible during Formation and Combat both now - V3's activity rail is "a permanent
            // in-shell rail" per the handoff, and the mockup shows the spell list fully populated
            // (dimmed - nothing is castable with 0 Formation Energy) even before combat starts.
            // Hidden only once the match is Resolved, preserving the original reason it used to
            // hide outside Combat entirely ("four unexplained buttons sat below a finished match
            // with nothing to cast at", 2026-08-06) without also hiding it during Formation.
            _spellBar.gameObject.SetActive(!resolved);

            // The guided tutorial's own captions (RefreshTutorialStepControls, called at the end
            // of RefreshAll()) fully replace the old static "Ready.../Hold your formation..."
            // pair that used to live here - every tutorial match now always has a _tutorialStep,
            // so that replacement is unconditional, not an addition alongside this.

            // Readiness-audit fix, 2026-08-15: Reset/Recommended Lineup were the one remaining
            // way to silently leave a tutorial match mid-Formation - both route to
            // OnLineupButtonPressed -> StartNewMatch, which is not IsTutorialMatch-aware and
            // resets the flag to false, dropping the player into a real match with no warning.
            // Hidden entirely (not just disabled) for the whole duration of any tutorial match -
            // Formation and Combat both, not just Formation, since inCombat means formation is
            // also false and both buttons would otherwise still be sitting there inert but
            // visible. A normal match is completely unaffected - same buttons, same handlers,
            // always visible exactly as before.
            if (_resetLineupButton != null) _resetLineupButton.gameObject.SetActive(!IsTutorialMatch);
            // Release feature: Auto Formation lives on the relabeled Recommended control now
            // (see BuildPrimaryActionAndSpells/ShouldOfferAutoFormation), not on this button -
            // additionally hidden without a valid confirmed deck, so an invalid/incomplete
            // normal deck can never permit Auto Formation.
            if (_recommendedLineupButton != null)
            {
                _recommendedLineupButton.gameObject.SetActive(ShouldOfferAutoFormation());
            }

            if (!inCombat)
            {
                // A spell can be armed (Targeting Mode) right up to the tick the match resolves -
                // without this, the enemy lane buttons that arming made interactable/highlighted
                // would stay that way underneath the result screen, since nothing else clears
                // spell-targeting state on a phase change.
                if (_armedSpellIndex >= 0) CancelSpellTargeting();
                if (formation) _primaryActionLabel.text = "START BATTLE";

                // Spell affordability hint (2026-08-22): reset here so Formation and Resolved
                // both read the plain "SPELLS" heading - Resolved returns below before the loop
                // that would otherwise naturally recompute this, and Formation's own Energy is
                // always 0 so the loop below would compute the same false/false result anyway;
                // set explicitly rather than relying on that coincidence.
                if (_spellsTitleText != null)
                {
                    _spellsTitleText.text = "SPELLS";
                    _spellsTitleText.color = GoldTextColor;
                }
            }

            if (resolved) return; // rail already hidden above; nothing left to refresh in it.

            bool anySpellReady = false;
            for (int i = 0; i < _spellButtons.Count; i++)
            {
                bool exists = i < _battleController.Spellbook.Count;
                _spellButtons[i].gameObject.SetActive(exists);
                if (!exists) continue;

                AvatarSpell spell = _battleController.Spellbook[i];
                // Never "ready" outside Combat - Formation Energy is always 0 (never accrued
                // until BattleController.AdvanceCombatTick runs), so this is otherwise already
                // true by construction, but stating it directly here keeps the dimmed-during-
                // Formation visual from ever silently depending on that coincidence.
                //
                // Guided-tutorial gate: outside step 7, or for any spell but the one approved
                // lesson spell within it, dimmed exactly like an on-cooldown/unaffordable spell -
                // SpellIconPointerHandler bypasses Button.interactable entirely (see its own
                // class comment), so OnSpellTapped's own check is the real block; this is only
                // the matching visual.
                bool tutorialAllowsThisSpell = _tutorialStep == null
                    || (_tutorialStep == TutorialStep.SpellLesson && i == TutorialLessonSpellIndex);
                bool ready = inCombat && tutorialAllowsThisSpell
                    && SpellAffordability.IsCastable(spell, _battleController.Energy);
                anySpellReady |= ready;

                if (i < _spellNameLabels.Count) _spellNameLabels[i].text = spell.Name;

                _spellLabels[i].text = spell.IsOffCooldown
                    ? $"{spell.EnergyCost} COST"
                    : $"CD {spell.CooldownRemaining}";
                _spellLabels[i].color = ready ? ButtonTextNormalColor : ButtonTextDisabledColor;

                if (i < _spellIcons.Count)
                {
                    Sprite iconSprite = SpellIconSprite(spell.Effect);
                    _spellIcons[i].sprite = iconSprite;
                    _spellIcons[i].enabled = iconSprite != null;
                    // Dimmed rather than hidden while unavailable, so the bar's shape stays
                    // stable and a spell doesn't appear to vanish when it goes on cooldown.
                    _spellIcons[i].color = ready ? Color.white : new Color(1f, 1f, 1f, 0.35f);
                }
            }

            // Spell affordability hint (2026-08-22, owner: "help casting, not watch numbers and
            // guess") - the SpellRail's existing "SPELLS" heading is the clear existing-UI cue:
            // once any one spell is really castable (same tutorial-gated `ready` this loop already
            // computes for the dimming, not a second rule), the heading itself says so, in
            // addition to the existing per-button dimming - a glance at the rail, not a read of
            // every button.
            if (_spellsTitleText != null)
            {
                _spellsTitleText.text = anySpellReady ? "SPELLS - READY TO CAST" : "SPELLS";
                _spellsTitleText.color = anySpellReady ? Color.white : GoldTextColor;
            }
        }

        /// <summary>Icon for a spell button, from the uploaded StatusIcons set.</summary>
        private static Sprite SpellIconSprite(SpellEffect effect) => effect switch
        {
            SpellEffect.LaneDamage => Resources.Load<Sprite>("UI/StatusIcons/Burn"),
            SpellEffect.LaneHeal => Resources.Load<Sprite>("UI/StatusIcons/Regeneration"),
            SpellEffect.LaneAttackBuff => Resources.Load<Sprite>("UI/StatusIcons/Rage"),
            SpellEffect.AvatarStrike => Resources.Load<Sprite>("UI/StatusIcons/Lightning"),
            _ => null,
        };

        /// <summary>One-line "what does this do" for a spell button.</summary>
        private static string SpellShortEffect(AvatarSpell spell) => spell.Effect switch
        {
            SpellEffect.LaneDamage => $"{spell.Magnitude} dmg to a lane",
            SpellEffect.LaneHeal => $"heal {spell.Magnitude} a lane",
            SpellEffect.LaneAttackBuff => $"+{spell.Magnitude} ATK a lane",
            SpellEffect.AvatarStrike => $"{spell.Magnitude} to Avatar",
            _ => string.Empty,
        };

        private void RefreshLaneSlots(PlayerBattleState side, Dictionary<Lane, Transform> slotContainers,
            Dictionary<Lane, Text> totalTexts, bool isEnemySide)
        {
            Font font = GetDefaultFont();
            IReadOnlyList<CombatTickRecord> ledger = _battleController.CombatLedger;

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Transform container = slotContainers[lane];
                for (int i = container.childCount - 1; i >= 0; i--)
                {
                    // DestroyImmediate, not Destroy - RefreshAll() (and so this) is callable
                    // from Initialize() outside Play Mode, e.g. from EditMode tests.
                    DestroyImmediate(container.GetChild(i).gameObject);
                }

                int totalAttack = 0;
                foreach (BattleCardInstance instance in side.Lanes[lane].Cards)
                {
                    CreateMiniCardDisplay(container, instance, font);
                    // Two-slot cards render double width so the board reads honestly - see the
                    // matching note in RefreshLanePicker.
                    if (instance.Definition.SlotWeight > 1 && container.childCount > 0)
                    {
                        SetPreferredWidth(container.GetChild(container.childCount - 1).gameObject,
                            BoardSlotWidth * instance.Definition.SlotWeight);
                    }
                    if (instance.IsAlive) totalAttack += instance.Attack;
                }

                // Fill remaining open slots with empty-slot art, so all 3 slots per lane are
                // always visible (matching the reference's slot-grid look) instead of the lane
                // just reading as empty space until something's actually played into it.
                // Counts free *slots*, not free card positions - a two-slot card leaves one
                // empty marker behind it, not two.
                for (int i = 0; i < side.Lanes[lane].FreeSlots; i++)
                {
                    CreateEmptySlotDisplay(container);
                }

                // V3 lane-total column: sum of living cards' live Attack, plus this lane's own
                // latest-resolved-tick overflow, shown as an explicit number (0 before any tick
                // has resolved) - never a rating, per the handoff's hard-number rule.
                if (totalTexts.TryGetValue(lane, out Text totalText) && totalText != null)
                {
                    int overflow = GetLatestLaneOverflow(ledger, lane, isEnemySide);
                    totalText.text = $"ATK {totalAttack}\nOVERFLOW {overflow}";
                }
            }
        }

        /// <summary>
        /// This lane's overflow toward the OPPOSING avatar from the most recently resolved tick -
        /// 0 before any tick has resolved. LaneClashResult.OverflowToA is overflow landing on the
        /// player (PlayerState is LaneBattleResolver.ResolveTurn's own sideA, EnemyState sideB -
        /// see BattleController.ResolveTurnAndAdvance's call), OverflowToB on the enemy, so the
        /// enemy's own row shows OverflowToA (damage IT sent to the player) and the player's own
        /// row shows OverflowToB (damage IT sent to the enemy).
        /// </summary>
        private static int GetLatestLaneOverflow(IReadOnlyList<CombatTickRecord> ledger, Lane lane, bool isEnemySide)
        {
            if (ledger.Count == 0) return 0;
            CombatTickRecord latest = ledger[ledger.Count - 1];
            foreach (LaneClashResult result in latest.LaneResults)
            {
                if (result.Lane == lane) return isEnemySide ? result.OverflowToA : result.OverflowToB;
            }
            return 0;
        }

        private static void CreateEmptySlotDisplay(Transform parent)
        {
            Sprite emptySlotSprite = Resources.Load<Sprite>("UI/Slots/Empty_Slot");
            if (emptySlotSprite == null) return;

            var cell = new GameObject("EmptySlot", typeof(RectTransform));
            cell.transform.SetParent(parent, false);
            SetPreferredWidth(cell, BoardSlotWidth);
            SetPreferredHeight(cell, BoardSlotHeight);

            // Empty_Slot.png's own real aspect (170x200 = 0.85 w/h, asset audit 2026-08-18) -
            // its own shape, not a rarity frame's.
            RectTransform go = CreateBoardCardTile(cell.transform, 170f / 200f);
            var image = go.gameObject.AddComponent<Image>();
            image.sprite = emptySlotSprite;
            image.type = Image.Type.Sliced; // see CreateCardButton's rarity-frame comment
            image.raycastTarget = false;
            // Explicit rather than relying on Image's own default (which is already white/no
            // tint) - the dark bracket-frame look every screenshot shows for an empty slot is
            // Empty_Slot.png's own baked artwork, not a color tint. Made explicit so there is no
            // ambiguity left in code about whether a tint is being applied here.
            image.color = Color.white;
        }

        /// <summary>
        /// Lane buttons stay interactable at all times so a tap always produces a response (see
        /// OnLanePressed, which explains every rejection). What changes here is only the
        /// *highlight*: a lane that the currently-selected card could actually go into is
        /// brightened, so the valid targets are obvious before you tap rather than after.
        /// </summary>
        private void RefreshLaneButtons()
        {
            // Folds in spell-targeting highlight (2026-08-06) rather than leaving it as a
            // separately-maintained highlight applied once by ArmSpellTargeting - this method
            // already runs on every RefreshAll(), including every combat tick (~2.2s), and was
            // unconditionally resetting every player lane's background back to transparent each
            // time based on card-selection state alone. A highlight ArmSpellTargeting set once
            // would have been silently wiped by the very next tick while a friendly spell (Mend,
            // War Cry) was still armed and waiting for a tap. One method owning both concerns is
            // what keeps them from fighting over the same Image.color.
            bool armedSpellIsFriendly = IsArmedSpellFriendlyTargeted();

            // Guided-tutorial lock: outside the tutorial (or once its own lane-gated steps are
            // done - BeginBattle onward), null here restores the normal "always tappable, every
            // rejected tap explains why" behaviour untouched.
            Lane? tutorialAllowedLane = TutorialAllowedLane();
            bool tutorialGatesLanes = _tutorialStep != null && IsTutorialFormationGateStep();

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Button button = _playerLaneButtons[lane];
                button.interactable = !tutorialGatesLanes || lane == tutorialAllowedLane;

                bool isValidCardTarget = _selectedCard != null
                    && _battleController.PlayerState.Lanes[lane].HasRoomFor(_selectedCard)
                    && _selectedCard.ResourceCost <= _battleController.PlayerState.Resource;

                var background = button.GetComponent<Image>();
                if (background != null)
                {
                    // Invisible unless this lane is a legal target for the selected card or the
                    // currently armed spell - the highlight is the only time a row tint earns the
                    // space it takes up.
                    background.color = (isValidCardTarget || armedSpellIsFriendly)
                        ? new Color(SelectedColor.r, SelectedColor.g, SelectedColor.b, 0.28f)
                        : new Color(0f, 0f, 0f, 0f);
                }
            }
        }

        /// <summary>
        /// Hand cards are always interactable now (regardless of affordability) so tapping one
        /// always opens the detail view - being unable to click a card was previously read as a
        /// bug even when it was just "not enough resource yet." Affordability now only gates the
        /// action button inside the detail overlay, with an explicit "Need X, have Y" reason.
        /// </summary>
        private void RefreshHand()
        {
            foreach (Button button in _handButtons)
            {
                // DestroyImmediate, not Destroy - see the note in RefreshLaneSlots above.
                DestroyImmediate(button.gameObject);
            }
            _handButtons.Clear();

            Font font = GetDefaultFont();
            int resource = _battleController.PlayerState.Resource;

            // The hand is shown during Formation, hidden during ordinary combat ticks, and shown
            // again during a reinforcement window - see BattleController.ReinforcementTicks.
            // Leftover cards used to be dead weight for the entire fight; now they have exactly
            // two moments where they matter, and they're only on screen for those.
            bool canDeploy = _battleController.Phase == BattlePhase.Formation
                || _battleController.IsReinforcementWindowOpen;

            if (!canDeploy)
            {
                _handHintText.gameObject.SetActive(false);
                return;
            }

            int handIndex = 0;
            string tutorialAllowedCardId = TutorialAllowedCardId();
            foreach (Card card in _battleController.PlayerState.Hand)
            {
                Card capturedCard = card;
                bool affordable = card.ResourceCost <= resource;
                bool isSelected = _selectedCard == card;

                Button button = CreateCardButton(_handRow, card, font, affordable, isSelected);
                button.onClick.AddListener(() => OnHandCardPressed(capturedCard));

                // Guided-tutorial lock: Button.interactable genuinely blocks onClick (unlike the
                // spell tiles' raw pointer handler - see OnSpellTapped's own note), so this alone
                // stops a real tap; OnHandCardPressed/SelectOrDeselectFormationHandCard still
                // re-checks the same allowance for direct (test) calls that bypass the UI layer.
                if (_tutorialStep != null)
                {
                    bool allowed = tutorialAllowedCardId != null && card.Id == tutorialAllowedCardId;
                    button.interactable = allowed;
                    if (!allowed)
                    {
                        Image cardBg = button.GetComponent<Image>();
                        if (cardBg != null) cardBg.color = new Color(0.3f, 0.3f, 0.3f, 0.6f);
                    }
                }

                StartCardShimmer(button.gameObject, handIndex);

                _handButtons.Add(button);
                handIndex++;
            }

            bool anyAffordable = _battleController.PlayerState.Hand.Any(c => c.ResourceCost <= resource);
            _handHintText.gameObject.SetActive(!anyAffordable && _battleController.PlayerState.Hand.Count > 0);
        }

        private void ShowCardDetail(Card card)
        {
            _previewedCard = card;

            Sprite art = _cardDatabase.GetArt(card);
            _cardDetailArt.sprite = art;
            _cardDetailArt.enabled = art != null;

            // The AspectRatioFitter has to be re-pointed at each card's own aspect, since a
            // single popup shows every card in turn and they aren't all the same shape.
            var fitter = _cardDetailArt.GetComponent<AspectRatioFitter>();
            if (fitter != null && art != null && art.rect.height > 0f)
            {
                fitter.aspectRatio = art.rect.width / art.rect.height;
            }

            Sprite rarityFrame = GetRarityFrameSprite(card.Rarity);
            _cardDetailArtFrame.sprite = rarityFrame;
            _cardDetailArtFrame.enabled = rarityFrame != null;

            _cardDetailClassTag.text = $"{card.Element} - {card.Class}";
            _cardDetailName.text = card.DisplayName;
            _cardDetailSkill.text = CardSkillDescription(card);

            int resource = _battleController.PlayerState.Resource;
            int cap = _battleController.PlayerState.ResourceCap;
            bool affordable = card.ResourceCost <= resource;
            bool isSelected = _selectedCard == card;

            // Shows the resource point spend before/after playing this card, so the tradeoff is
            // visible up front rather than something the player has to compute themselves.
            string resourceLine = affordable
                ? $"Resource: {resource}/{cap} -> {resource - card.ResourceCost}/{cap} after playing"
                : $"Resource: {resource}/{cap} (not enough)";

            _cardDetailStats.text = $"Cost {card.ResourceCost}   Attack {card.Attack}   Health {card.Health}\n{resourceLine}";

            if (isSelected)
            {
                _cardDetailActionLabel.text = "Deselect";
                _cardDetailActionButton.interactable = true;
            }
            else if (affordable)
            {
                _cardDetailActionLabel.text = "Select for Play";
                _cardDetailActionButton.interactable = true;
            }
            else
            {
                _cardDetailActionLabel.text = $"Need {card.ResourceCost}, have {resource}";
                _cardDetailActionButton.interactable = false;
            }

            _cardDetailOverlay.SetActive(true);
        }

        private void CloseCardDetail()
        {
            _cardDetailOverlay.SetActive(false);
        }

        // ---------- Low-level UI construction ----------

        /// <summary>A full-width panel anchored to a fixed fraction of the canvas height.</summary>
        private static RectTransform CreateBandPanel(Transform parent, string name, Color background, float y0, float y1)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, y0);
            rect.anchorMax = new Vector2(1f, y1);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            if (background.a > 0f)
            {
                var image = go.AddComponent<Image>();
                image.color = background;
            }

            return rect;
        }

        /// <summary>Same as CreateBandPanel, but filled with a top-to-bottom gradient instead of
        /// a flat color, plus a thin accent stripe along the top edge - what actually reads as
        /// "designed" rather than a plain colored box.</summary>
        private static RectTransform CreateGradientBandPanel(Transform parent, string name,
            Color topColor, Color bottomColor, float y0, float y1)
        {
            RectTransform rect = CreateBandPanel(parent, name, Color.clear, y0, y1);

            // Fully transparent panels draw nothing at all - see HudPanelAlpha. An invisible
            // full-width Image would still cost a raycast target and a batch for no visual gain.
            if (topColor.a <= 0f && bottomColor.a <= 0f) return rect;

            Image bg = rect.gameObject.AddComponent<Image>();
            bg.sprite = CreateGradientSprite(topColor, bottomColor);
            bg.type = Image.Type.Simple;

            Image accentStripe = CreateImage(rect, AccentBorderColor);
            accentStripe.rectTransform.anchorMin = new Vector2(0f, 1f);
            accentStripe.rectTransform.anchorMax = new Vector2(1f, 1f);
            accentStripe.rectTransform.pivot = new Vector2(0.5f, 1f);
            accentStripe.rectTransform.sizeDelta = new Vector2(0, 2);
            accentStripe.rectTransform.anchoredPosition = Vector2.zero;

            return rect;
        }

        /// <summary>Generates a small vertical top-to-bottom gradient texture at runtime, since
        /// no gradient/beveled art assets exist to import - this is the entire "beautify the
        /// flat single-tone boxes" mechanism, achievable in legacy uGUI without commissioned art.</summary>
        private static Sprite CreateGradientSprite(Color top, Color bottom, int height = 48)
        {
            var tex = new Texture2D(1, height, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int y = 0; y < height; y++)
            {
                float t = (float)y / (height - 1);
                tex.SetPixel(0, y, Color.Lerp(bottom, top, t));
            }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, 1, height), new Vector2(0.5f, 0.5f));
        }

        /// <summary>
        /// Generates a rounded-rectangle gradient texture and 9-slices it, so panels/buttons/bars
        /// get real rounded corners at any size without a custom art asset - direct answer to
        /// "I don't want boxes": a flat Image is a rectangle no matter what color it's filled
        /// with, so getting an actual non-rectangular silhouette needs either an imported sprite
        /// or a generated one, and no art has been supplied yet, so this is the generated route.
        /// </summary>
        private static Sprite CreateRoundedGradientSprite(Color top, Color bottom, int size = 56, int cornerRadius = 18)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int y = 0; y < size; y++)
            {
                float t = (float)y / (size - 1);
                Color rowColor = Color.Lerp(bottom, top, t);
                for (int x = 0; x < size; x++)
                {
                    float alpha = rowColor.a;
                    bool nearEdgeX = x < cornerRadius || x >= size - cornerRadius;
                    bool nearEdgeY = y < cornerRadius || y >= size - cornerRadius;
                    if (nearEdgeX && nearEdgeY)
                    {
                        float cx = x < cornerRadius ? cornerRadius : size - cornerRadius - 1;
                        float cy = y < cornerRadius ? cornerRadius : size - cornerRadius - 1;
                        float dist = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                        // 1.5px soft edge instead of a hard cutoff, so the curve doesn't look jagged.
                        alpha *= Mathf.Clamp01(cornerRadius - dist + 1.5f);
                    }
                    tex.SetPixel(x, y, new Color(rowColor.r, rowColor.g, rowColor.b, alpha));
                }
            }
            tex.Apply();
            var border = new Vector4(cornerRadius, cornerRadius, cornerRadius, cornerRadius);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, border);
        }

        private static RectTransform CreatePanel(Transform parent, string name, Color background)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();

            if (background.a > 0f)
            {
                var image = go.AddComponent<Image>();
                image.color = background;
            }

            return rect;
        }

        /// <summary>Like CreatePanel, but with real rounded corners (see CreateRoundedGradientSprite) -
        /// used for modal/popup panels (card detail, match result), which aren't full-bleed to a
        /// screen edge the way the main HUD bands are, so rounding always looks intentional here.</summary>
        private static RectTransform CreateRoundedPanel(Transform parent, string name, Color topColor, Color bottomColor)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();

            var image = go.AddComponent<Image>();
            // The uploaded "popup window frame" art (cropped clean of its reference-sheet
            // caption - see CardArtImportSettings' NineSlice border handling), used as the
            // "card holder" for the card-detail/result modals in place of the flat procedural
            // panel once real ornate frame art existed - full white tint so its own painted
            // gold/stone coloring isn't dulled by a topColor/bottomColor multiply.
            //
            // 2026-08-06: this is the *only* background these modals draw. The request was to
            // "make the popout a PNG whereby the purple area does not show up" - the purple
            // marks were the panel's own rectangular fill showing outside the frame's ornate
            // silhouette. The frame PNG already has transparent corners, so no extra fill is
            // drawn behind it and the popup now reads as the frame shape rather than a box.
            Sprite ornateFrame = Resources.Load<Sprite>("UI/Frames/NineSlice/Popup_Frame");
            if (ornateFrame != null)
            {
                image.sprite = ornateFrame;
                image.type = Image.Type.Sliced;
                image.color = Color.white;
            }
            else
            {
                image.sprite = CreateRoundedGradientSprite(topColor, bottomColor, size: 72, cornerRadius: 26);
                image.type = Image.Type.Sliced;
            }

            return rect;
        }

        private static Text CreateText(Transform parent, string content, int fontSize, Color color, Font font)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<Text>();
            text.font = font;
            text.text = content;
            text.fontSize = fontSize;
            text.color = color;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.supportRichText = true;
            return text;
        }

        /// <summary>
        /// Card art that fills its box completely without distorting it.
        ///
        /// 2026-08-06, third attempt at this. `preserveAspect = true` fits the art *inside* the
        /// box and leaves blank margins (reported as "unused space"); `preserveAspect = false`
        /// fills the box but squashes the picture (reported as "way too overstretched"). Neither
        /// is what's wanted. The correct tool is Unity's own AspectRatioFitter in EnvelopeParent
        /// mode - it scales the image until it *covers* the box, keeping the aspect ratio, and a
        /// Mask on the container crops whatever hangs over the edge. That's fill AND undistorted,
        /// which is what every card game actually does with portrait art in a non-portrait frame.
        /// </summary>
        private static Image CreateCroppedArt(Transform parent, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var frameGo = new GameObject("ArtCrop", typeof(RectTransform));
            frameGo.transform.SetParent(parent, false);
            var frameRect = (RectTransform)frameGo.transform;
            frameRect.anchorMin = anchorMin;
            frameRect.anchorMax = anchorMax;
            frameRect.offsetMin = Vector2.zero;
            frameRect.offsetMax = Vector2.zero;

            // The Mask needs a Graphic to define its shape. showMaskGraphic = false keeps that
            // graphic itself invisible - only its silhouette is used, as the crop boundary.
            var maskImage = frameGo.AddComponent<Image>();
            maskImage.color = Color.white;
            maskImage.raycastTarget = false;
            frameGo.AddComponent<Mask>().showMaskGraphic = false;

            var artGo = new GameObject("Art", typeof(RectTransform));
            artGo.transform.SetParent(frameGo.transform, false);
            var art = artGo.AddComponent<Image>();
            art.sprite = sprite;
            art.enabled = sprite != null;
            art.raycastTarget = false;
            art.preserveAspect = false; // the fitter below owns sizing; both would fight

            var artRect = (RectTransform)artGo.transform;
            artRect.anchorMin = new Vector2(0.5f, 0.5f);
            artRect.anchorMax = new Vector2(0.5f, 0.5f);
            artRect.pivot = new Vector2(0.5f, 0.5f);
            artRect.anchoredPosition = Vector2.zero;

            var fitter = artGo.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
            fitter.aspectRatio = sprite != null && sprite.rect.height > 0f
                ? sprite.rect.width / sprite.rect.height
                : 1f;

            return art;
        }

        /// <summary>
        /// V4 hard requirement: "never crop-to-fill" for card illustrations in hand/board slots
        /// (unlike full-bleed backgrounds, which still use CreateCroppedArt above). Simple
        /// letterbox/pillarbox fit via Image.preserveAspect - no Mask, no AspectRatioFitter,
        /// nothing hangs over the frame's edge.
        /// </summary>
        private static Image CreateFittedArt(Transform parent, Sprite sprite,
            Vector2 anchorMin, Vector2 anchorMax)
        {
            var artGo = new GameObject("ArtFit", typeof(RectTransform));
            artGo.transform.SetParent(parent, false);
            var artRect = (RectTransform)artGo.transform;
            artRect.anchorMin = anchorMin;
            artRect.anchorMax = anchorMax;
            artRect.offsetMin = Vector2.zero;
            artRect.offsetMax = Vector2.zero;

            var art = artGo.AddComponent<Image>();
            art.sprite = sprite;
            art.enabled = sprite != null;
            art.raycastTarget = false;
            art.preserveAspect = true;
            return art;
        }

        /// <summary>Forces a RectTransform to a fixed aspect ratio that fits inside its parent,
        /// so two sprites with different native aspects still end up exactly concentric.</summary>
        private static void AddAspectFitter(GameObject go, float aspect)
        {
            var fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = aspect;
        }

        private static Image CreateImage(Transform parent, Color color)
        {
            var go = new GameObject("Image", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.color = color;
            return image;
        }

        /// <summary>
        /// Button's built-in ColorTint transition silently overwrites any manually-assigned
        /// Image.color on every state check (creation, interactable changes) - that's what
        /// caused the "blank hand cards" bug (cards rendering with no visible color/art
        /// distinction). transition = None makes manual colors the only source of truth; the
        /// interactable/selected/disabled visuals below are all handled explicitly instead.
        /// </summary>
        private static Button CreateButton(Transform parent, string label, Font font, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(parent, false);

            // A thin gold rim showing around a slightly-inset fill - two stacked rounded-rect
            // sprites rather than a flat single-color box, and rounder corners (26 of 56, close
            // to a full pill/stadium shape) - the "slick, not boxy" pass on buttons/lane bars
            // (2026-08-05), matching the pill-shaped buttons in the reference mockup.
            var rim = go.AddComponent<Image>();
            rim.sprite = CreateRoundedGradientSprite(AccentBorderColor, AccentBorderColor, cornerRadius: 26);
            rim.type = Image.Type.Sliced;

            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var fillGo = new GameObject("Fill", typeof(RectTransform));
            fillGo.transform.SetParent(go.transform, false);
            var fillRect = (RectTransform)fillGo.transform;
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = new Vector2(3, 3);
            fillRect.offsetMax = new Vector2(-3, -3);
            var fill = fillGo.AddComponent<Image>();
            fill.sprite = CreateRoundedGradientSprite(ButtonNormalTop, ButtonNormalBottom, cornerRadius: 24);
            fill.type = Image.Type.Sliced;
            fill.raycastTarget = false;

            Text text = CreateText(go.transform, label, 14, ButtonTextNormalColor, font);
            StretchFull(text.rectTransform);

            return button;
        }

        /// <summary>A hand card: rarity frame behind, art inset within its border, name/cost
        /// text below. Always interactable - see RefreshHand.</summary>
        private Button CreateCardButton(Transform parent, Card card, Font font, bool affordable, bool isSelected)
        {
            var go = new GameObject($"Card_{card.Id}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            // Capped at 196 tall, not pushed further: V3's HandAndPlacementPanel (handoff anchor
            // (.02,.02)-(.70,.23), ~227px tall at 1080) gives the hand row ~212px, and the row
            // does not mask its children, so a card taller than that bleeds into neighbouring UI
            // rather than clipping cleanly. 196 leaves ~16px of slack.
            //
            // Width, asset audit 2026-08-18 (docs/Battle_Screen_Landscape_Asset_Audit_2026-08-18.md):
            // used to be a flat 130 regardless of rarity, forcing Sliced to stretch the frame's
            // real aspect (0.739 Common/Rare/Epic, 0.870 Legendary) to whatever 130/196 happened
            // to be - "the frame's native aspect doesn't match the card button's rect" was a
            // known, accepted defect, not a non-issue. Computed from this card's own frame aspect
            // instead, so the Sliced border no longer has to stretch at all.
            float cardHeight = 196f;
            SetPreferredWidth(go, cardHeight * GetRarityFrameAspect(card.Rarity));
            SetPreferredHeight(go, cardHeight);

            var bg = go.AddComponent<Image>();
            Sprite rarityFrame = GetRarityFrameSprite(card.Rarity);
            if (rarityFrame != null)
            {
                // The frame art's fill is opaque, not a transparent cutout (checked directly -
                // center pixel alpha is 255) - it's meant to sit *behind* the card art, which
                // then covers the fill within an inset margin, not composited through it.
                // Sliced (not Simple) - even at this card's own native aspect, the button's exact
                // pixel size still won't equal the source texture's own pixel size, and Simple
                // would stretch the whole image non-uniformly to fit. See CardArtImportSettings
                // for the spriteBorder this depends on.
                bg.sprite = rarityFrame;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else
            {
                bg.sprite = CreateRoundedGradientSprite(PanelColor, PanelColor, cornerRadius: 14);
                bg.type = Image.Type.Sliced;
            }
            if (isSelected) bg.color = SelectedColor;

            var button = go.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.interactable = true;

            // V4 hard requirement: never crop-to-fill card illustrations. Preserve-aspect fit
            // instead (see CreateFittedArt) - letterboxes rather than cropping or squashing.
            CreateFittedArt(go.transform, _cardDatabase.GetArt(card),
                new Vector2(0.10f, 0.10f), new Vector2(0.90f, 0.95f));

            // Class corner badge (e.g. "Perfect") - the card's type wasn't visible anywhere on
            // the hand thumbnail before, only in the detail overlay after tapping it.
            // Narrowed to start at x=0.33 (was 0.10) so it sits beside the new Cost chip in the
            // literal top-left corner (below) rather than the two overlapping - both occupy the
            // same top strip, side by side.
            var classBadgeBg = CreateImage(go.transform, new Color(0, 0, 0, 0.6f));
            classBadgeBg.rectTransform.anchorMin = new Vector2(0.33f, 0.86f);
            classBadgeBg.rectTransform.anchorMax = new Vector2(0.97f, 0.98f);
            classBadgeBg.rectTransform.offsetMin = Vector2.zero;
            classBadgeBg.rectTransform.offsetMax = Vector2.zero;
            Text classBadge = CreateText(classBadgeBg.transform, card.Class.ToString(), 11, GoldTextColor, font);
            classBadge.raycastTarget = false;
            StretchFull(classBadge.rectTransform);

            // The card's name, back on the thumbnail and actually readable. It was dropped last
            // round to buy font size for the stats, but a hand of cards you can't identify by
            // name is worse than tight numbers ("the white area shows the name of the card but
            // it is too small to be viewed", 2026-08-06). Cards are wider now, so both fit.
            var nameBg = CreateImage(go.transform, new Color(0, 0, 0, 0.6f));
            nameBg.raycastTarget = false;
            nameBg.rectTransform.anchorMin = new Vector2(0.10f, 0.30f);
            nameBg.rectTransform.anchorMax = new Vector2(0.90f, 0.43f);
            nameBg.rectTransform.offsetMin = Vector2.zero;
            nameBg.rectTransform.offsetMax = Vector2.zero;
            Text nameLabel = CreateText(nameBg.transform, card.DisplayName, 13, Color.white, font);
            nameLabel.fontStyle = FontStyle.Bold;
            nameLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            nameLabel.raycastTarget = false;
            StretchFull(nameLabel.rectTransform);

            // Cost/ATK/HP moved into three corner chips (2026-08-06, "Cost top-left gem, ATK
            // bottom-left, HP bottom-right") instead of one combined stat line, reusing
            // CreateStatChip so a hand card matches the same badge language a deployed card on
            // the board already uses (see CreateMiniCardDisplay) rather than two different stat
            // conventions for the same numbers.
            //
            // The full "Need X, have Y" affordability reason is no longer spelled out on the
            // thumbnail itself - a badge this small cannot fit a sentence. It is not lost: the
            // card is always interactable (see RefreshHand) and tapping it opens the detail
            // overlay, which already states the exact reason. The cost chip tints red as the
            // at-a-glance cue instead.
            Color costChipColor = affordable
                ? new Color(0.2f, 0.35f, 0.85f, 0.9f)
                : new Color(0.75f, 0.15f, 0.1f, 0.9f);
            CreateStatChip(go.transform, card.ResourceCost.ToString(), font,
                new Vector2(0.03f, 0.86f), new Vector2(0.30f, 0.98f), costChipColor);
            CreateStatChip(go.transform, card.Attack.ToString(), font,
                new Vector2(0.03f, 0.02f), new Vector2(0.32f, 0.16f), new Color(0.85f, 0.55f, 0.2f, 0.92f));
            CreateStatChip(go.transform, card.Health.ToString(), font,
                new Vector2(0.68f, 0.02f), new Vector2(0.97f, 0.16f), new Color(0.8f, 0.2f, 0.2f, 0.92f));

            if (!affordable && !isSelected)
            {
                bg.color = ButtonDisabledColor;
            }

            return button;
        }

        /// <summary>
        /// The fixed rect a board card (or empty-slot marker) actually renders at, explicitly
        /// centered inside its (deliberately wider) cell, sized to a shared row HEIGHT at the
        /// caller-supplied aspect ratio - never a shared box. Asset audit, 2026-08-18: forcing
        /// every rarity into one fixed box was the actual root cause of the "flattened/stretched"
        /// board-card defect, since Common/Rare/Epic (0.739 w/h) and Legendary (0.870) card
        /// frames are genuinely different shapes, not a single "card aspect". A Legendary tile
        /// therefore renders visibly wider than a Common one at the same height - intentional,
        /// not a bug. Returns the tile's own RectTransform so the caller can parent its
        /// frame/art/chips onto it exactly as it used to onto the cell itself.
        /// </summary>
        private static RectTransform CreateBoardCardTile(Transform cell, float aspect)
        {
            var go = new GameObject("Tile", typeof(RectTransform));
            go.transform.SetParent(cell, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            float tileHeight = BoardSlotHeight - 8f;
            rect.sizeDelta = new Vector2(tileHeight * aspect, tileHeight);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        /// <summary>
        /// A deployed unit on the board: rarity-framed art with its Attack and current Health in
        /// the bottom corners.
        ///
        /// Reworked 2026-08-06 ("the blue circle shows how crap the cards are"). The previous
        /// version used the flat Friendly_Slot/Enemy_Slot plate as its background with the art
        /// letterboxed into the top 70% and a tiny 9pt "3/5" strip underneath - at 66x78 that
        /// read as a smudge in a box. It now uses the same rarity frame as the hand card so a
        /// deployed unit looks like the card it came from, the art fills the frame, and the two
        /// numbers that matter sit in opposite corners on their own dark chips rather than
        /// competing with the art behind them. Ownership is carried by a coloured rim instead of
        /// the slot plate, which is clearer anyway now the art covers the whole tile.
        /// </summary>
        private void CreateMiniCardDisplay(Transform parent, BattleCardInstance instance, Font font)
        {
            // The V4 handoff's own slot geometry (316x95) is deliberately a wide, short CELL -
            // it defines the tap-target/spacing math for three-across lane rows, not the shape a
            // card should render at. Stretching the rarity frame and art to fill that cell
            // outright (the pre-fix behavior) squashed a portrait frame into a landscape box,
            // which is exactly the "flattened/stretched" distortion visual review flagged. Fixed
            // by keeping the cell purely as a LayoutElement-sized, invisible spacing box, and
            // rendering the actual card tile as a fixed rect explicitly centered inside it via
            // CreateBoardCardTile, at THIS card's own rarity-frame aspect (see
            // GetRarityFrameAspect) rather than one shared aspect.
            var cell = new GameObject($"Mini_{instance.Definition.Id}", typeof(RectTransform));
            cell.transform.SetParent(parent, false);
            SetPreferredWidth(cell, BoardSlotWidth);
            SetPreferredHeight(cell, BoardSlotHeight);

            RectTransform go = CreateBoardCardTile(cell.transform, GetRarityFrameAspect(instance.Definition.Rarity));

            var bg = go.gameObject.AddComponent<Image>();
            Sprite rarityFrame = GetRarityFrameSprite(instance.Definition.Rarity);
            if (rarityFrame != null)
            {
                bg.sprite = rarityFrame;
                bg.type = Image.Type.Sliced;
                bg.color = Color.white;
            }
            else
            {
                bg.sprite = CreateRoundedGradientSprite(PanelColor, PanelColor, size: 32, cornerRadius: 6);
                bg.type = Image.Type.Sliced;
            }
            bg.raycastTarget = false;

            // V4 hard requirement: never crop-to-fill card illustrations. Preserve-aspect fit
            // instead (see CreateFittedArt) - letterboxes rather than cropping or squashing.
            CreateFittedArt(go.transform, _cardDatabase.GetArt(instance.Definition),
                new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));

            // Ownership rim - a thin tinted overlay, since the slot plate that used to carry
            // this information is no longer the background.
            Image rim = CreateImage(go.transform, instance.IsPlayerOwned
                ? new Color(0.3f, 0.8f, 0.9f, 0.22f)
                : new Color(0.9f, 0.3f, 0.25f, 0.22f));
            rim.raycastTarget = false;
            StretchFull(rim.rectTransform);

            // Cost badge, top-left - V4 spec: "cost at top-left; ATK at bottom-left; HP at
            // bottom-right" for every card on the board, matching the hand card's own chip
            // layout (CreateCardButton) rather than only showing ATK/HP as before.
            CreateStatChip(go.transform, instance.Definition.ResourceCost.ToString(), font,
                new Vector2(0.02f, 0.70f), new Vector2(0.42f, 0.98f), new Color(0.2f, 0.35f, 0.85f, 0.9f));
            CreateStatChip(go.transform, instance.Attack.ToString(), font,
                new Vector2(0.02f, 0.02f), new Vector2(0.42f, 0.30f), new Color(0.85f, 0.55f, 0.2f, 0.9f));
            CreateStatChip(go.transform, instance.CurrentHealth.ToString(), font,
                new Vector2(0.58f, 0.02f), new Vector2(0.98f, 0.30f), new Color(0.8f, 0.2f, 0.2f, 0.9f));
        }

        /// <summary>
        /// Attack/Health/Cost corner badge on a card - a deployed unit's stats, or (2026-08-06,
        /// hand card rework) a hand card's Cost/ATK/HP.
        ///
        /// A rounded procedural badge (CreateRoundedGradientSprite), not a flat rectangle - the
        /// original version used a plain Image.color rectangle, which read as "raw colored
        /// blocks" against every other badge/frame in the game already using rounded corners
        /// (buttons, health bars, rarity frames). Reuses the same generator those already render
        /// correctly with, rather than an unverified sprite asset - see CreateHealthBadge's own
        /// note on why that specific shortcut just failed for the HP badge.
        /// </summary>
        private static void CreateStatChip(Transform parent, string value, Font font,
            Vector2 anchorMin, Vector2 anchorMax, Color background)
        {
            Image chip = CreateImage(parent, Color.white);
            chip.sprite = CreateRoundedGradientSprite(background, background, size: 32, cornerRadius: 14);
            chip.type = Image.Type.Sliced;
            chip.raycastTarget = false;
            chip.rectTransform.anchorMin = anchorMin;
            chip.rectTransform.anchorMax = anchorMax;
            chip.rectTransform.offsetMin = Vector2.zero;
            chip.rectTransform.offsetMax = Vector2.zero;

            Text label = CreateText(chip.transform, value, 12, Color.white, font);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            StretchFull(label.rectTransform);

            // Outline for legibility over the busy card art behind it - matches every other
            // number-on-art label in this file (portrait names, HP badges, floating damage).
            var outline = label.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.85f);
            outline.effectDistance = new Vector2(1f, -1f);
        }

        /// <summary>
        /// Unity's built-in-resource font name changed from "Arial.ttf" to "LegacyRuntime.ttf"
        /// in newer Editor versions (a font-licensing change, not a deprecation of the API) -
        /// try the current name first, fall back to the old one for older Editor versions.
        /// </summary>
        private static Font GetDefaultFont()
        {
            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            if (font == null)
            {
                font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            }
            return font;
        }

        /// <summary>
        /// ENDORALT.ttf has been sitting in Resources/Fonts unused since the art pass that
        /// pulled it from Drawing/Reign/Card border - a real thematic display font already in
        /// the project, no new upload needed. Used for the title only, not body/stat text -
        /// a stylized display face is exactly the wrong choice for small numbers that need to
        /// stay legible (Attack/Health/Cost), which is what GetDefaultFont() is still for.
        /// </summary>
        private static Font GetDisplayFont()
        {
            Font font = Resources.Load<Font>("Fonts/ENDORALT");
            return font != null ? font : GetDefaultFont();
        }

        private static void Shuffle<T>(List<T> list)
        {
            var rng = new System.Random();
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        private static void SetPreferredHeight(GameObject go, float height)
        {
            LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.preferredHeight = height;
            element.minHeight = height;
        }

        private static void SetPreferredWidth(GameObject go, float width)
        {
            LayoutElement element = go.GetComponent<LayoutElement>() ?? go.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.minWidth = width;
        }
    }
}
