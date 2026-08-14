using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
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

    public class GameBootstrap : MonoBehaviour
    {
        private const float CanvasWidth = 720f;
        private const float CanvasHeight = 1280f;

        // Fixed anchored Y-bands (fractions of canvas height) instead of a single stacked
        // VerticalLayoutGroup - the original all-in-one-layout-group root let a panel's actual
        // rendered height drift from what its neighbors assumed, which is what caused panels to
        // visually overlap (e.g. the status row bleeding into the lanes panel). Each band here
        // is independently anchored, so one panel's content can never push into another's.
        // Rebalanced 2026-08-05: the hand band was too short for the 180px-tall card buttons, so
        // cards visibly overflowed underneath the End Turn bar ("the bottom left red bar is
        // covering the cards"). The hand now gets the largest single share, and the two button
        // bands below it were tightened to pay for it.
        private const float TitleY0 = 0.96f, TitleY1 = 1.00f; // unused on the battle screen - see Initialize()
        private const float EnemyY0 = 0.72f, EnemyY1 = 1.00f;
        private const float PlayerY0 = 0.43f, PlayerY1 = 0.72f;
        private const float StatusY0 = 0.37f, StatusY1 = 0.43f;
        private const float HandY0 = 0.12f, HandY1 = 0.37f;
        private const float EndTurnY0 = 0.06f, EndTurnY1 = 0.115f;
        private const float LineupButtonsY0 = 0.005f, LineupButtonsY1 = 0.055f;

        // Horizontal inset applied to the full-width bars (both Avatar HP bars, the primary
        // action button, the lineup buttons). 2026-08-06: every one of these was marked as "too
        // long" - a bar pinned edge to edge reads as a UI band rather than a discrete element,
        // and the reference mockup has none of them touching the screen edges.
        private const float BarXInset = 0.30f;

        // Horizontal extent of the lane rows within their panel. Previously each lane row
        // stretched the full panel width while its 3 slots occupied only the left portion, so
        // the remaining ~40% rendered as a long empty tinted bar - the "unwanted/extra bar"
        // marked all down the right side of the battle screen. Rows are now only as wide as the
        // content they hold, with the far side left clear for the Avatar portrait (enemy top
        // left, player bottom right, matching the reference mockup).
        private const float EnemyLaneX0 = 0.20f, EnemyLaneX1 = 0.99f;
        private const float PlayerLaneX0 = 0.01f, PlayerLaneX1 = 0.80f;

        // Battle-board rework, 2026-08-06: the first real play test (this project's EditMode
        // suite cannot execute Update()/coroutines, so Play Mode rendering had literally never
        // been looked at before) showed three stacked per-lane rows reading as a plain 3x3 grid
        // of empty boxes, and two full-width health bars sitting in the middle of the board -
        // exactly what an empty Formation screen looks like, since CreateEmptySlotDisplay's
        // placeholder art is the only thing rendered before any card is deployed.
        //
        // Board zones now hold all 3 lanes side by side in ONE row per side instead of one row
        // PER LANE - matching a Hearthstone/Shadowverse-style board - while keeping Front/Middle/
        // Back as three visually grouped clusters within that row (see CreateLaneGroup), because
        // the lane a card sits in is not cosmetic here: LaneBattleResolver keys elemental
        // advantage, Front's +1 Attack, Middle's +1 Health, and the overflow-on-clear rule
        // entirely off which lane a card occupies. Flattening lanes away in the UI would make the
        // board unreadable rather than cleaner.
        private const float EnemyBoardY0 = 0.08f, EnemyBoardY1 = 0.42f;
        private const float PlayerBoardY0 = 0.42f, PlayerBoardY1 = 0.80f;

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

        private Card _selectedCard;
        private Card _previewedCard;

        private readonly Dictionary<Lane, Transform> _enemyLaneSlots = new Dictionary<Lane, Transform>();
        private readonly Dictionary<Lane, Transform> _playerLaneSlots = new Dictionary<Lane, Transform>();
        private readonly Dictionary<Lane, Button> _playerLaneButtons = new Dictionary<Lane, Button>();
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
        private Text _turnText;
        private Text _deckCountText;
        private Transform _handRow;
        private Text _handHintText;
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

        /// <summary>Number of cards currently rendered in the hand row - exposed for tests.</summary>
        public int HandCardCount => _handButtons.Count;

        /// <summary>Exposed for tests, which can't rely on Awake() firing outside Play Mode.</summary>
        public BattleController Battle => _battleController;

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

            var dbGo = new GameObject("CardDatabase");
            _cardDatabase = dbGo.AddComponent<CardDatabase>();
            _cardDatabase.Initialize();

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
            _profile = Application.isPlaying ? SaveSystem.CurrentProfile : new PlayerProfile();

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
            // gains +3 Avatar levels per win against an opponent that never changed, so the game
            // got monotonically *easier* the longer it was played. SoloAIScalingSystem now derives
            // the opponent from the player's own progression each match - see that class for what
            // it does and does not scale.
            _aiScaling = new SoloAIScalingSystem();

            StartNewMatch();
            _battleController.OnMatchEnded += HandleMatchEnded;

            // Built before any panel with a lane button - see BuildSpellTargetCancelCatcher's own
            // comment for why its position in this call order is load-bearing, not cosmetic.
            BuildSpellTargetCancelCatcher(canvas.transform);

            // Title panel removed 2026-08-06 ("remove MOD"): a permanent game-title banner across
            // the top of the battle screen is menu chrome, not gameplay information, and it was
            // eating a band of screen the board could use. BuildTitlePanel is kept for a future
            // main menu rather than deleted.
            BuildEnemyPanel(canvas.transform, defaultFont);
            BuildPlayerPanel(canvas.transform, defaultFont);
            BuildStatusRow(canvas.transform, defaultFont);
            BuildHandPanel(canvas.transform, defaultFont);
            BuildEndTurnButton(canvas.transform, defaultFont);
            BuildLineupButtons(canvas.transform, defaultFont);
            BuildCardDetailOverlay(canvas.transform, defaultFont);
            BuildLanePickerOverlay(canvas.transform, defaultFont);
            BuildResultOverlay(canvas.transform, defaultFont);
            BuildTutorialOverlay(canvas.transform, defaultFont);
            BuildSpellTooltip(canvas.transform, defaultFont);

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
        /// useRecommendedDeck picks the player's deck with CurveAwareDeck() (a real cost-curve
        /// heuristic - see that method) instead of the default rarity-fair split - the concrete
        /// stand-in for the reference UI's "Recommended Lineup" button, given there's still no
        /// full deck-builder to recommend a selection *from*. Both this and "Reset Lineup"
        /// (useRecommendedDeck: false) restart the current match immediately with a freshly
        /// drawn deck at the player's current level - the only two lineup actions that are
        /// actually meaningful without a deck-builder UI to pick specific cards in.
        /// </summary>
        private void StartNewMatch(bool useRecommendedDeck = false)
        {
            List<Card> fullPool = _cardDatabase.AllCards.ToList();
            List<Card> playerDeck;
            List<Card> enemyDeck;

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
                (playerDeck, enemyDeck) = BuildBalancedDecks(fullPool, deckSize, deckSize);
            }

            // Re-derived every match rather than once at startup, so the level gained from the
            // last result actually changes who shows up for the next one.
            _aiProfile = _aiScaling.GenerateAIOpponent(_empireData);

            var playerEconomy = new BattleController.MatchEconomy(
                _empireData.ResourceCap, _empireData.Turn1Resource, _empireData.StartingAvatarHealth);
            var enemyEconomy = new BattleController.MatchEconomy(
                _aiProfile.StartingResourceCap, _aiProfile.Turn1Resource, _aiProfile.MaxAvatarHealth);
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
        public void StartApprovedTutorialBattle()
        {
            GrantApprovedStarterCardsIfMissing();

            var playerDeck = new List<Card>
            {
                _cardDatabase.GetCard("warrior"),
                _cardDatabase.GetCard("novice_knight"),
                _cardDatabase.GetCard("goblin_caster"),
            };
            var enemyDeck = new List<Card>
            {
                _cardDatabase.GetCard("butcher"),
                _cardDatabase.GetCard("cursed_soldier"),
                _cardDatabase.GetCard("giant_worms"),
                _cardDatabase.GetCard("tribal_warrior"),
            };

            // Fixed level-1 baseline for the approved tutorial content specifically - not
            // derived from the player's real progression, since this encounter's content is
            // the same regardless of how far along the player's own Empire actually is.
            var tutorialEmpire = new PlayerEmpireData();
            tutorialEmpire.SetLevels(avatarLevel: 1, castleLevel: 1, barracksLevel: 1);
            tutorialEmpire.InitializeTCGModifiers();
            var economy = new BattleController.MatchEconomy(
                tutorialEmpire.ResourceCap, tutorialEmpire.Turn1Resource, tutorialEmpire.StartingAvatarHealth);

            _battleController.StartMatch(playerDeck, enemyDeck, economy, economy);
            _battleController.DealFormationHand(_battleController.PlayerState);
            _battleController.DealFormationHand(_battleController.EnemyState);

            if (_combatLoop != null)
            {
                StopCoroutine(_combatLoop);
                _combatLoop = null;
            }

            IsTutorialMatch = true;
            RefreshAll();
        }

        /// <summary>
        /// Idempotent: adds only whichever of the three approved starter ids the local
        /// profile doesn't already own. Local/offline only - this is not a trusted or
        /// server-verified grant, only a direct write to the same cardCollection list
        /// ShopPresenter.cs already writes to elsewhere in the game.
        /// </summary>
        private void GrantApprovedStarterCardsIfMissing()
        {
            if (_profile?.cardCollection == null) return;

            foreach (string cardId in new[] { "warrior", "novice_knight", "goblin_caster" })
            {
                if (!_profile.cardCollection.Contains(cardId))
                {
                    _profile.cardCollection.Add(cardId);
                }
            }
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

            var bgGo = new GameObject("Background", typeof(RectTransform));
            bgGo.transform.SetParent(canvasGo.transform, false);
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
            StretchFull(bg.rectTransform);

            if (backdrop != null)
            {
                // The panels' own gradients already add contrast for their own text, but the
                // gaps between panels sit directly over a busy painted scene now instead of a
                // plain dark background - a dim overlay keeps the whole HUD readable over it.
                Image dim = CreateImage(canvasGo.transform, new Color(0f, 0f, 0f, 0.35f));
                StretchFull(dim.rectTransform);
            }

            return canvas;
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

        private void BuildEnemyPanel(Transform canvasTransform, Font font)
        {
            RectTransform panel = CreateGradientBandPanel(canvasTransform, "EnemyPanel",
                WithAlpha(EnemyPanelTop, HudPanelAlpha), WithAlpha(EnemyPanelBottom, HudPanelAlpha), EnemyY0, EnemyY1);

            // Enemy portrait pinned top-left and made much larger (was a 0.22-height sliver in
            // the corner and reported as "too small to see") - matches the reference mockup,
            // where each side's Avatar is a big circular portrait in its own corner.
            CreatePortrait(panel, "Orc_King", 0.55f, 1f, 0.005f, 0.17f, _aiProfile.DisplayName, font,
                out RectTransform enemyPortraitBox, out _enemyNameLabel);

            // HP moved into a corner badge on the portrait itself (see CreateHealthBadge) -
            // replaces the old full-width CreateBar() health bar, which sat across the middle of
            // the board rather than in either side's own corner. "Enemy - bring their Avatar HP to
            // 0 to win" was removed earlier (2026-08-06) as a permanent instruction banner that
            // belongs in the tutorial instead.
            CreateHealthBadge(enemyPortraitBox, font, out _enemyHealthFill, out _enemyAvatarText);

            // Single board row instead of one row per lane - see the Board zones note by
            // EnemyBoardY0/EnemyBoardY1 for why Front/Middle/Back stay visually grouped rather
            // than a flat run of cards.
            RectTransform boardZone = CreateBandPanel(panel, "EnemyBoardZone", Color.clear, EnemyBoardY0, EnemyBoardY1);
            // AnchorBand's xInsetMin/xInsetMax become anchorMin.x/1-anchorMax.x directly, so the
            // pair here is (EnemyLaneX0, 1-EnemyLaneX1) - NOT (1-EnemyLaneX1, 1-EnemyLaneX0),
            // which would silently invert the board zone's horizontal placement.
            AnchorBand(boardZone, EnemyBoardY0, EnemyBoardY1, EnemyLaneX0, 1f - EnemyLaneX1);
            var zoneLayout = boardZone.gameObject.AddComponent<HorizontalLayoutGroup>();
            zoneLayout.spacing = 20f;
            zoneLayout.childAlignment = TextAnchor.MiddleCenter;
            zoneLayout.childForceExpandWidth = false;
            zoneLayout.childForceExpandHeight = true;
            zoneLayout.childControlWidth = false;
            zoneLayout.childControlHeight = true;

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                // Interactive now (2026-08-06, was false) - Firestorm targets an ENEMY lane, and
                // until now the enemy board had no tap target of any kind. Kept non-interactable
                // by default (set immediately below) so an ordinary tap does nothing outside spell
                // targeting - ArmSpellTargeting is what turns these on, only for the lanes a
                // damage spell can actually hit, only while one is armed.
                CreateLaneGroup(boardZone, lane, font, interactive: true, out Transform slots, out Button laneButton);
                _enemyLaneSlots[lane] = slots;
                _enemyLaneButtons[lane] = laneButton;
                laneButton.interactable = false;
                // CreateLaneGroup always wires an interactive button to OnLanePressed, which is
                // the PLAYER's card-placement/reinforcement handler - meaningless for the enemy
                // board. Stripped here; ArmSpellTargeting adds the real (spell-targeting)
                // listener only while a damage spell is armed, and CancelSpellTargeting removes
                // it again, so an enemy lane is never left holding a stale listener from a spell
                // that's no longer armed.
                laneButton.onClick.RemoveAllListeners();
            }
        }

        private void BuildPlayerPanel(Transform canvasTransform, Font font)
        {
            RectTransform panel = CreateGradientBandPanel(canvasTransform, "PlayerPanel",
                WithAlpha(PlayerPanelTop, HudPanelAlpha), WithAlpha(PlayerPanelBottom, HudPanelAlpha), PlayerY0, PlayerY1);

            // Player portrait pinned bottom-right, mirroring the enemy's top-left - the
            // diagonal-corners arrangement from the reference mockup. Dropped lower (was
            // 0.02-0.52) on 2026-08-06: it sat level with the lane rows and read as part of the
            // board rather than as the player's own corner.
            // "Lightbringer" is a fixed player identity, unlike the AI opponent (see
            // _enemyNameLabel) - it never changes mid-session, so the label from CreatePortrait
            // doesn't need to be captured for a later refresh.
            CreatePortrait(panel, "Paladin", -0.30f, 0.18f, 0.83f, 0.995f, "Lightbringer", font,
                out RectTransform playerPortraitBox, out _);
            CreateHealthBadge(playerPortraitBox, font, out _playerHealthFill, out _playerAvatarText);

            // Resource/Energy gem, moved from the status row to sit beside the player's own
            // portrait ("Mana Gem counter... anchored at the player corner" - matches the
            // reference more closely than a bar shared with Deck/Clash text). RefreshAll's
            // existing fillAmount/text logic is unchanged; only where this Image/Text physically
            // live moved.
            CreateResourceBadge(playerPortraitBox, font, out _resourceFill, out _resourceText);

            RectTransform boardZone = CreateBandPanel(panel, "PlayerBoardZone", Color.clear, PlayerBoardY0, PlayerBoardY1);
            AnchorBand(boardZone, PlayerBoardY0, PlayerBoardY1, PlayerLaneX0, 1f - PlayerLaneX1);
            var zoneLayout = boardZone.gameObject.AddComponent<HorizontalLayoutGroup>();
            zoneLayout.spacing = 20f;
            zoneLayout.childAlignment = TextAnchor.MiddleCenter;
            zoneLayout.childForceExpandWidth = false;
            zoneLayout.childForceExpandHeight = true;
            zoneLayout.childControlWidth = false;
            zoneLayout.childControlHeight = true;

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                CreateLaneGroup(boardZone, lane, font, interactive: true, out Transform slots, out Button laneButton);
                _playerLaneSlots[lane] = slots;
                _playerLaneButtons[lane] = laneButton;
            }
        }

        /// <summary>
        /// One lane's cluster of up to <see cref="LaneState.MaxSlots"/> slots, as a single tap
        /// target within a side's shared board row. Replaces the old CreateLaneRow (each lane its
        /// own full-width row, stacked three deep) with a compact group meant to sit beside its
        /// two siblings in one HorizontalLayoutGroup - see BuildEnemyPanel/BuildPlayerPanel.
        ///
        /// A fixed preferred width (SetPreferredWidth) rather than letting the outer zone infer
        /// one from this group's own nested layout - nested HorizontalLayoutGroups CAN report a
        /// preferred size to their parent's layout pass, but every other sizing decision in this
        /// file already goes through SetPreferredWidth/Height, and there was no working Editor
        /// session to screenshot-verify the alternative against, so this sticks to the pattern
        /// that's already proven to render correctly elsewhere here.
        /// </summary>
        private void CreateLaneGroup(Transform parent, Lane lane, Font font, bool interactive,
            out Transform slotsContainer, out Button laneButton)
        {
            const float slotWidth = 66f;
            const float innerSlotSpacing = 8f;
            float groupWidth = LaneState.MaxSlots * slotWidth + (LaneState.MaxSlots - 1) * innerSlotSpacing;

            var groupGo = new GameObject($"Lane_{lane}", typeof(RectTransform));
            groupGo.transform.SetParent(parent, false);
            SetPreferredWidth(groupGo, groupWidth);
            SetPreferredHeight(groupGo, 78f);

            // Fully transparent, same reasoning as the row tint it replaces (2026-08-06, "take
            // away the opaque shading in all the rows") - an Image with alpha 0 still receives
            // clicks, which is what keeps the group tappable without painting a visible band.
            var groupImage = groupGo.AddComponent<Image>();
            groupImage.color = new Color(0, 0, 0, 0f);

            Button button = null;
            if (interactive)
            {
                button = groupGo.AddComponent<Button>();
                button.transition = Selectable.Transition.None;
                button.onClick.AddListener(() => OnLanePressed(lane));
            }

            var groupLayout = groupGo.AddComponent<HorizontalLayoutGroup>();
            groupLayout.spacing = innerSlotSpacing;
            groupLayout.childAlignment = TextAnchor.MiddleCenter;
            groupLayout.childForceExpandWidth = false;
            groupLayout.childForceExpandHeight = true;
            groupLayout.childControlWidth = false;
            groupLayout.childControlHeight = true;

            slotsContainer = groupGo.transform;
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

        private void BuildStatusRow(Transform canvasTransform, Font font)
        {
            RectTransform panel = CreateBandPanel(canvasTransform, "StatusRow", Color.clear, StatusY0, StatusY1);
            var layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12f;
            layout.childForceExpandWidth = false;
            layout.childControlWidth = false;
            layout.childForceExpandHeight = true;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.padding = new RectOffset(8, 8, 0, 0);
            layout.childControlHeight = true;

            // The resource/Energy bar that used to live here moved to a corner badge beside the
            // player portrait (2026-08-06, see CreateResourceBadge and BuildPlayerPanel) - it
            // reads as the player's own resource there, next to their Avatar, rather than a meter
            // shared with the deck count and clash indicator. _resourceFill/_resourceText are
            // still wired in RefreshAll exactly as before; only where they physically live moved.
            //
            // Font sizes raised across this row (13/15 -> 17) and widths widened to match: "Turn
            // 1" and "Deck: 9" were both called out as too small to read (2026-08-05). These are
            // numbers a player checks often mid-turn, so they should be legible, not the least.
            _turnText = CreateText(panel, "", 17, GoldTextColor, font);
            SetPreferredWidth(_turnText.gameObject, 110);
            _deckCountText = CreateText(panel, "", 17, GoldTextColor, font);
            SetPreferredWidth(_deckCountText.gameObject, 110);

            // The "^ +1 ATK  = +1 HP  v --" legend was removed again 2026-08-06 ("some ATK and HP
            // words which are out of place"). Substituting ASCII glyphs for icons produced
            // something that read as neither - the lane rules are now taught by the tutorial,
            // which is the right place for a rule that never changes.

            // Shows what the squad's tag composition bought - without this the synergy bonus is
            // invisible, and an invisible bonus can't influence how you build a formation.
            _synergyText = CreateText(panel, "", 15, SelectedColor, font);
            SetPreferredWidth(_synergyText.gameObject, 340);
            _synergyText.alignment = TextAnchor.MiddleLeft;
            // Single line, clipped. With Wrap it broke "TacticalCommand" mid-word across three
            // lines and spilled up over the Avatar HP bar above it (2026-08-06). The status row
            // is one line tall by design, so text that doesn't fit should be cut, not reflowed
            // into a neighbour's space.
            _synergyText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _synergyText.verticalOverflow = VerticalWrapMode.Truncate;
            _synergyText.resizeTextForBestFit = true;
            _synergyText.resizeTextMinSize = 9;
            _synergyText.resizeTextMaxSize = 15;
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
            portraitImg.preserveAspect = false;
            portraitImg.raycastTarget = false;
            AddSquareFitter(innerGo); // squares against portraitGo, which is correctly anchored

            Sprite frameSprite = Resources.Load<Sprite>("UI/Frames/Avatar_Circle_Frame");
            if (frameSprite != null)
            {
                // Child of the square, stretched to it - inheriting the square means the ring is
                // concentric with the face by construction, which was the point of the original
                // change and still holds.
                var frameGo = new GameObject("Frame", typeof(RectTransform));
                frameGo.transform.SetParent(innerGo.transform, false);
                var frameImg = frameGo.AddComponent<Image>();
                frameImg.sprite = frameSprite;
                frameImg.preserveAspect = false;
                frameImg.raycastTarget = false;
                StretchFull((RectTransform)frameGo.transform);
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
        /// Compact HP badge straddling a portrait's bottom-right corner - replaces the old
        /// full-width central health bar (removed 2026-08-06; see the Board zones note near
        /// EnemyBoardY0). Parented to `portraitBox` (CreatePortrait's own out-param) rather than
        /// hand-computed against panel space, so the badge tracks wherever that portrait actually
        /// sits instead of needing its own separately-tuned coordinates that could drift out of
        /// sync with it.
        ///
        /// No radial "drains as you take damage" fill: ui_badge_red's exact silhouette hasn't
        /// been visually verified yet (this whole rework was written and compile-tested without a
        /// live Editor to screenshot against - see docs/OFFLINE_TASKS.md T1), and Image.Type.Filled
        /// on an unverified non-circular sprite can crop badly. A plain static badge with the HP
        /// number on it cannot render wrong regardless of the art's real shape; the fraction is
        /// still there in `fill.fillAmount` for a later pass to switch on once someone has
        /// actually looked at the sprite.
        /// </summary>
        private static void CreateHealthBadge(RectTransform portraitBox, Font font, out Image fill, out Text label)
        {
            fill = null;
            label = null;
            if (portraitBox == null) return; // portrait art missing - same guard as CreatePortrait

            var badgeGo = new GameObject("HealthBadge", typeof(RectTransform));
            badgeGo.transform.SetParent(portraitBox, false);
            var badgeRect = (RectTransform)badgeGo.transform;
            // BROKEN 2026-08-06, confirmed by an actual Play Mode screenshot: a stretched-fraction
            // anchor (0.58-1.20 x, -0.16-0.34 y of portraitBox) rendered as a huge elongated red
            // oval spanning past the portrait into the name text, not a small badge. Two compounding
            // causes: (1) portraitBox is CreatePortrait's OUTER box, not the square the portrait
            // actually renders at - CreatePortrait fits a square INSIDE it via AddSquareFitter, so
            // anchoring a fraction of the outer box inherits its real (non-square) proportions
            // instead of the visible circle's; (2) see the cornerRadius note below.
            //
            // Fixed size in pixels, anchored as a POINT (not a stretched fraction) at the
            // portrait's bottom-right corner - this is what actually guarantees a small, correctly
            // proportioned badge regardless of what shape the outer portrait box turns out to be.
            badgeRect.anchorMin = new Vector2(1f, 0f);
            badgeRect.anchorMax = new Vector2(1f, 0f);
            badgeRect.pivot = new Vector2(0.5f, 0.5f);
            badgeRect.sizeDelta = new Vector2(74f, 32f);
            badgeRect.anchoredPosition = new Vector2(-4f, 4f);

            fill = badgeGo.AddComponent<Image>();
            // cornerRadius was 40 against CreateRoundedGradientSprite's default size of 56 - more
            // than half the texture, which leaves Sliced with no valid flat middle to stretch (the
            // 9-slice border exceeds half the source), and IS what turned a wide target rect into
            // a solid elongated pill instead of a rounded badge. 16 against 56 matches the ratio
            // CreateButton's own rim already uses successfully (26 against the same default 56).
            fill.sprite = CreateRoundedGradientSprite(HealthBarFillColor, HealthBarEmptyColor, cornerRadius: 16);
            fill.type = Image.Type.Sliced;

            label = CreateText(badgeGo.transform, "", 13, Color.white, font);
            label.fontStyle = FontStyle.Bold;
            label.raycastTarget = false;
            StretchFull(label.rectTransform);
            var labelOutline = label.gameObject.AddComponent<Outline>();
            labelOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            labelOutline.effectDistance = new Vector2(1.2f, -1.2f);
        }

        /// <summary>
        /// Compact Energy/Resource pill anchored to the player portrait's bottom-left corner,
        /// mirroring CreateHealthBadge on the right ("Mana Gem counter... anchored at the player
        /// corner"). Moved out of the status row (2026-08-06), which now only carries Deck count,
        /// the Clash/Formation indicator, and the synergy line.
        ///
        /// Reuses CreateBar rather than the ui_badge_red approach CreateHealthBadge takes -
        /// Health_Empty/Mana_Fill are bar art already proven correct (they are what the two
        /// Avatar HP bars this rework removed were built from), where ui_badge_red is new and
        /// unverified. A working pill beats a guessed-at badge shape for the resource the player
        /// reads every single tick.
        /// </summary>
        private Image CreateResourceBadge(RectTransform portraitBox, Font font, out Image fill, out Text label)
        {
            fill = null;
            label = null;
            if (portraitBox == null) return null; // portrait art missing - same guard as CreatePortrait

            Image bar = CreateBar(portraitBox, ResourceBarEmptyColor, ResourceBarFillColor, font, 11,
                out fill, out label, "Mana_Fill");

            // CreateBar does not anchor/size its own output - every existing call site positions
            // it afterward (AnchorBand in BuildEnemyPanel/BuildPlayerPanel, SetPreferredWidth in
            // BuildStatusRow's layout group). Mirrors CreateHealthBadge's corner math on the
            // opposite (bottom-left) side; wider than tall since a bar reads better wide than a
            // circular badge does.
            RectTransform rect = bar.rectTransform;
            rect.anchorMin = new Vector2(-0.75f, -0.14f);
            rect.anchorMax = new Vector2(0.42f, 0.20f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            return bar;
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

        private void BuildHandPanel(Transform canvasTransform, Font font)
        {
            RectTransform panel = CreateGradientBandPanel(canvasTransform, "HandPanel",
                WithAlpha(HandPanelTop, HudPanelAlpha), WithAlpha(HandPanelBottom, HudPanelAlpha), HandY0, HandY1);

            Text label = CreateText(panel, "Your Hand", 13, GoldTextColor, font);
            label.rectTransform.anchorMin = new Vector2(0f, 0.85f);
            label.rectTransform.anchorMax = new Vector2(1f, 1f);
            label.rectTransform.offsetMin = new Vector2(8, 0);
            label.rectTransform.offsetMax = new Vector2(-8, 0);

            _handHintText = CreateText(panel, "No affordable cards left - press End Turn", 12, new Color(0.85f, 0.6f, 0.4f), font);
            _handHintText.rectTransform.anchorMin = new Vector2(0f, 0.7f);
            _handHintText.rectTransform.anchorMax = new Vector2(1f, 0.85f);
            _handHintText.rectTransform.offsetMin = new Vector2(8, 0);
            _handHintText.rectTransform.offsetMax = new Vector2(-8, 0);
            _handHintText.gameObject.SetActive(false);

            var rowGo = new GameObject("HandRow", typeof(RectTransform));
            rowGo.transform.SetParent(panel, false);
            var rowRect = (RectTransform)rowGo.transform;
            rowRect.anchorMin = new Vector2(0f, 0f);
            rowRect.anchorMax = new Vector2(1f, 0.7f);
            rowRect.offsetMin = new Vector2(8, 4);
            rowRect.offsetMax = new Vector2(-8, 0);
            var rowLayout = rowGo.AddComponent<HorizontalLayoutGroup>();
            rowLayout.spacing = 18f;
            rowLayout.childForceExpandWidth = false;
            rowLayout.childControlWidth = false;
            rowLayout.childForceExpandHeight = true;
            _handRow = rowRect;
        }

        private void BuildEndTurnButton(Transform canvasTransform, Font font)
        {
            RectTransform panel = CreateBandPanel(canvasTransform, "PrimaryActionPanel", Color.clear, EndTurnY0, EndTurnY1);

            // The spell bar shares this band with the primary button: during Formation the
            // button owns the row, during Combat the spells do. Only one is ever active.
            //
            // Rebuilt 2026-08-06 from four wide horizontal pills (childForceExpandWidth stretched
            // across the row minus 150px side padding) into a dark action bar holding four fixed
            // 64x64 icon slots, centred - the pill shape matched every other button in the game,
            // which read as consistent, but was also flagged twice as too large for what's really
            // a small set of icon toggles. A 3-line label ("Firestorm / 4 dmg to a lane / 30
            // Energy") cannot fit a 64px tile, so the full name and effect description are gone
            // from the tile itself - RefreshPhaseControls now writes just the number that matters
            // in the moment (Energy cost, or ticks left on cooldown). The dimmed-icon-on-cooldown
            // cue (already existing) is what's meant to carry "not ready" at a glance now that the
            // word "ready" no longer fits.
            var actionBarGo = new GameObject("SpellActionBar", typeof(RectTransform));
            actionBarGo.transform.SetParent(panel, false);
            Image actionBarBg = actionBarGo.AddComponent<Image>();
            actionBarBg.sprite = CreateRoundedGradientSprite(new Color(0.05f, 0.05f, 0.08f, 0.85f),
                new Color(0.02f, 0.02f, 0.04f, 0.85f), cornerRadius: 18);
            actionBarBg.type = Image.Type.Sliced;
            AnchorBand((RectTransform)actionBarGo.transform, 0.05f, 0.95f, 0.30f, 0.30f);

            _spellBar = (RectTransform)actionBarGo.transform;
            var spellLayout = actionBarGo.AddComponent<HorizontalLayoutGroup>();
            spellLayout.spacing = 10f;
            spellLayout.childAlignment = TextAnchor.MiddleCenter;
            spellLayout.childForceExpandWidth = false;
            spellLayout.childForceExpandHeight = false;
            spellLayout.childControlWidth = false;
            spellLayout.childControlHeight = false;

            for (int i = 0; i < 4; i++)
            {
                int spellIndex = i; // captured per iteration, not shared across the closures

                // No onClick here (null) - a quick tap and a press-and-hold need to resolve to
                // different, mutually exclusive actions (cast/arm vs. inspect), and Button's own
                // onClick always fires on release regardless of hold duration, which can't
                // express that. SpellIconPointerHandler below owns the distinction instead.
                Button spell = CreateButton(_spellBar, "", font, null);
                SetPreferredWidth(spell.gameObject, 64f);
                SetPreferredHeight(spell.gameObject, 64f);

                var pointerHandler = spell.gameObject.AddComponent<SpellIconPointerHandler>();
                pointerHandler.OnQuickTap = () => OnSpellTapped(spellIndex);
                pointerHandler.OnHoldStart = () => ShowSpellTooltip(spellIndex, spell.GetComponent<RectTransform>());
                pointerHandler.OnHoldEnd = HideSpellTooltip;

                // Icon fills almost the whole tile - a 64px slot has no room left for an icon
                // plus a separate readable label the way the old wide pill did.
                var iconGo = new GameObject("Icon", typeof(RectTransform));
                iconGo.transform.SetParent(spell.transform, false);
                var icon = iconGo.AddComponent<Image>();
                icon.raycastTarget = false;
                icon.preserveAspect = true;
                AnchorBand((RectTransform)iconGo.transform, 0.16f, 0.94f, 0.10f, 0.10f);

                // Cost/cooldown number only, as a small corner chip rather than a text line inside
                // the tile - reuses the same rounded-badge helper the card stat chips use, so this
                // matches the visual language everywhere else numbers-on-art appear in this game.
                Text spellLabel = CreateText(spell.transform, "", 12, Color.white, font);
                spellLabel.fontStyle = FontStyle.Bold;
                spellLabel.raycastTarget = false;
                AnchorBand(spellLabel.rectTransform, -0.02f, 0.20f, 0.05f, 0.05f);
                var labelOutline = spellLabel.gameObject.AddComponent<Outline>();
                labelOutline.effectColor = new Color(0f, 0f, 0f, 0.9f);
                labelOutline.effectDistance = new Vector2(1f, -1f);

                _spellButtons.Add(spell);
                _spellLabels.Add(spellLabel);
                _spellIcons.Add(icon);
            }
            _spellBar.gameObject.SetActive(false);

            // "Compact metallic action button" using the real button art from the icon set
            // (2026-08-06) instead of the procedural pill every other button in this file uses -
            // falls back to the same procedural pill if the sprite is ever missing, matching the
            // defensive pattern used everywhere else art is optional in this file.
            Sprite primaryArt = Resources.Load<Sprite>("UI/Icons/play match button");
            if (primaryArt != null)
            {
                _primaryActionButton = CreateButton(panel, "START BATTLE", font, OnPrimaryActionPressed);
                Image primaryBg = _primaryActionButton.GetComponent<Image>();
                Image primaryFill = _primaryActionButton.transform.Find("Fill")?.GetComponent<Image>();
                primaryBg.sprite = null;
                primaryBg.color = Color.clear;
                if (primaryFill != null)
                {
                    primaryFill.sprite = primaryArt;
                    primaryFill.type = Image.Type.Sliced;
                    primaryFill.color = Color.white;
                }
            }
            else
            {
                _primaryActionButton = CreateButton(panel, "START BATTLE", font, OnPrimaryActionPressed);
            }

            // Bottom-right corner, sized close to a compact 220x60 action button rather than a
            // bar spanning the row - AnchorBand's fractional inset already put this at roughly
            // that size within a 720-wide canvas; the fixed pixel width/height below makes the
            // intended size explicit instead of implicit in the inset math.
            RectTransform primaryRect = _primaryActionButton.GetComponent<RectTransform>();
            primaryRect.anchorMin = new Vector2(1f, 0f);
            primaryRect.anchorMax = new Vector2(1f, 0f);
            primaryRect.pivot = new Vector2(1f, 0f);
            primaryRect.sizeDelta = new Vector2(220f, 60f);
            primaryRect.anchoredPosition = new Vector2(-16f, 6f);
            _primaryActionLabel = _primaryActionButton.GetComponentInChildren<Text>();
            _primaryActionLabel.fontSize = 18;
        }

        /// <summary>
        /// "Reset Lineup" and "Recommended" from the reference UI - both restart the current
        /// match immediately with a freshly drawn deck (see StartNewMatch's useRecommendedDeck
        /// parameter for what "Recommended" actually means without a real deck-builder yet).
        ///
        /// Shrunk further and pushed to the far right (2026-08-06, "remove Start Battle/Reset
        /// Lineup/Recommended from overlapping the card hand"). These stay - they are the only
        /// working way to change the deck without a real deck-builder UI, so removing them would
        /// remove a real capability, not just declutter - but they no longer need to be full-size
        /// pill buttons competing with the primary action for the same row's width. Left-padding
        /// widened to keep them clear of both the primary action button's own corner (see
        /// BuildEndTurnButton) and the taller hand cards now sitting directly above this row.
        /// </summary>
        private void BuildLineupButtons(Transform canvasTransform, Font font)
        {
            RectTransform panel = CreateBandPanel(canvasTransform, "LineupButtonsPanel", Color.clear, LineupButtonsY0, LineupButtonsY1);
            var layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 8f;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = false;
            layout.childAlignment = TextAnchor.MiddleRight;
            layout.padding = new RectOffset(0, 20, 2, 2);

            Button reset = CreateButton(panel, "Reset", font, () => OnLineupButtonPressed(useRecommendedDeck: false));
            reset.GetComponentInChildren<Text>().fontSize = 12;
            SetPreferredWidth(reset.gameObject, 90);

            Button recommended = CreateButton(panel, "Recommended", font, () => OnLineupButtonPressed(useRecommendedDeck: true));
            recommended.GetComponentInChildren<Text>().fontSize = 12;
            SetPreferredWidth(recommended.gameObject, 110);
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

            _resultText = CreateText(panel, "", 18, Color.white, font);
            AnchorBand(_resultText.rectTransform, 0.4f, 0.85f, 0.06f, 0.06f);

            // Side by side rather than stacked (2026-08-06, "RETURN TO CITY" added for the
            // battle/metagame split) - both are single-tap, equally weighted exits from this
            // screen, and stacking two full-width buttons here would just be the "giant pill"
            // problem the spell bar already had.
            Button playAgain = CreateButton(panel, "Play Again", font, OnPlayAgainPressed);
            AnchorBand(playAgain.GetComponent<RectTransform>(), 0.2f, 0.38f, 0.53f, 0.05f);

            Button returnToCity = CreateButton(panel, "Return to City", font, OnReturnToCityPressed);
            AnchorBand(returnToCity.GetComponent<RectTransform>(), 0.2f, 0.38f, 0.05f, 0.53f);

            _resultOverlay.SetActive(false);
        }

        // ---------- Interaction ----------

        private void OnHandCardPressed(Card card)
        {
            ShowCardDetail(card);
        }

        private void OnLanePressed(Lane lane)
        {
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
            // During Formation a lane tap opens that lane's picker - the lane is the decision,
            // and its three cards are chosen together there rather than one at a time from the
            // hand row (2026-08-06).
            if (_battleController.Phase == BattlePhase.Formation)
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
            RefreshAll();
            StartCombatLoop();
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
        private void OnSpellTapped(int spellIndex)
        {
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
                foreach (Lane lane in System.Enum.GetValues(typeof(Lane)).Cast<Lane>())
                {
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
        }

        private void OnSpellTargetLanePressed(Lane lane)
        {
            if (_armedSpellIndex < 0) return;
            int spellIndex = _armedSpellIndex;
            // Cleared before casting, not after - CastSpellAt calls RefreshAll(), and
            // RefreshLaneButtons reading a stale _armedSpellIndex mid-cast would re-highlight a
            // spell that has already been spent.
            CancelSpellTargeting();
            CastSpellAt(spellIndex, lane);
        }

        private void CastSpellAt(int spellIndex, Lane lane)
        {
            if (!_battleController.TryCastSpell(spellIndex, lane, out int avatarDamage))
            {
                ShowLaneHint("That spell isn't ready yet - not enough Energy, or still cooling down.");
                return;
            }

            AvatarSpell cast = _battleController.Spellbook[spellIndex];
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
        /// Built EARLY (called right after BuildCanvas, before any panel that contains a lane
        /// button) so it sits at a low sibling index and every lane button - built after it -
        /// naturally wins the raycast over it. Unity's GraphicRaycaster checks the TOPMOST
        /// (highest sibling index) hit first, so building this any later would make it the
        /// topmost element on screen and it would swallow every tap, including ones landing
        /// directly on a highlighted lane button underneath it - the opposite of its job. Built
        /// AFTER BuildCanvas specifically because BuildCanvas's own arena backdrop + dim overlay
        /// are raycast targets too (Image.raycastTarget defaults to true and neither sets it
        /// false) - building any earlier would put THIS catcher underneath THEM instead.
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

            // A match decided on the tick cap explains itself rather than claiming an Avatar
            // fell when neither did - BattleController.OutcomeReason carries that wording.
            string headline = !string.IsNullOrEmpty(_battleController.OutcomeReason)
                ? _battleController.OutcomeReason
                : playerWon
                    ? "VICTORY - the enemy Avatar has fallen."
                    : "DEFEAT - your Avatar has fallen.";

            _resultText.text = $"{headline}\n" +
                $"Avatar Level {_empireData.AvatarLevel} - next match: " +
                $"{_empireData.ResourceCap} Resource, {_empireData.StartingAvatarHealth} HP.";
            _resultOverlay.SetActive(true);
        }

        private void OnPlayAgainPressed()
        {
            _resultOverlay.SetActive(false);
            _selectedCard = null;
            StartNewMatch();
            RefreshAll();
        }

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
        /// note on why that lookup can't work (BattleController has no Canvas ancestor).</summary>
        public void SetBattleCanvasVisible(bool visible)
        {
            if (_canvasTransform != null) _canvasTransform.gameObject.SetActive(visible);
        }

        private void OnLineupButtonPressed(bool useRecommendedDeck)
        {
            _resultOverlay.SetActive(false);
            _selectedCard = null;
            StartNewMatch(useRecommendedDeck);

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

        private void RefreshAll()
        {
            RefreshLaneSlots(_battleController.EnemyState, _enemyLaneSlots);
            RefreshLaneSlots(_battleController.PlayerState, _playerLaneSlots);
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
            // Initialize() - a name set once at BuildEnemyPanel time would go stale after "Play
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

            RefreshPhaseControls();
            RefreshHand();
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

            _primaryActionButton.gameObject.SetActive(formation);
            // Only during Combat - the spell bar used to stay visible under the victory screen,
            // where four unexplained buttons sat below a finished match with nothing to cast at
            // ("what are the 4 buttons?", 2026-08-06).
            _spellBar.gameObject.SetActive(inCombat);

            if (!inCombat)
            {
                // A spell can be armed (Targeting Mode) right up to the tick the match resolves -
                // without this, the enemy lane buttons that arming made interactable/highlighted
                // would stay that way underneath the result screen, since nothing else clears
                // spell-targeting state on a phase change.
                if (_armedSpellIndex >= 0) CancelSpellTargeting();
                if (formation) _primaryActionLabel.text = "START BATTLE";
                return;
            }

            for (int i = 0; i < _spellButtons.Count; i++)
            {
                bool exists = i < _battleController.Spellbook.Count;
                _spellButtons[i].gameObject.SetActive(exists);
                if (!exists) continue;

                AvatarSpell spell = _battleController.Spellbook[i];
                bool ready = spell.IsOffCooldown && spell.EnergyCost <= _battleController.Energy;

                _spellLabels[i].text = spell.IsOffCooldown
                    ? spell.EnergyCost.ToString()
                    : spell.CooldownRemaining.ToString();
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

        private void RefreshLaneSlots(PlayerBattleState side, Dictionary<Lane, Transform> slotContainers)
        {
            Font font = GetDefaultFont();
            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Transform container = slotContainers[lane];
                for (int i = container.childCount - 1; i >= 0; i--)
                {
                    // DestroyImmediate, not Destroy - RefreshAll() (and so this) is callable
                    // from Initialize() outside Play Mode, e.g. from EditMode tests.
                    DestroyImmediate(container.GetChild(i).gameObject);
                }

                foreach (BattleCardInstance instance in side.Lanes[lane].Cards)
                {
                    CreateMiniCardDisplay(container, instance, font);
                    // Two-slot cards render double width so the board reads honestly - see the
                    // matching note in RefreshLanePicker.
                    if (instance.Definition.SlotWeight > 1 && container.childCount > 0)
                    {
                        SetPreferredWidth(container.GetChild(container.childCount - 1).gameObject,
                            66 * instance.Definition.SlotWeight);
                    }
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
            }
        }

        private static void CreateEmptySlotDisplay(Transform parent)
        {
            Sprite emptySlotSprite = Resources.Load<Sprite>("UI/Slots/Empty_Slot");
            if (emptySlotSprite == null) return;

            var go = new GameObject("EmptySlot", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetPreferredWidth(go, 66);
            SetPreferredHeight(go, 78);
            var image = go.AddComponent<Image>();
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

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Button button = _playerLaneButtons[lane];
                button.interactable = true;

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
            foreach (Card card in _battleController.PlayerState.Hand)
            {
                Card capturedCard = card;
                bool affordable = card.ResourceCost <= resource;
                bool isSelected = _selectedCard == card;

                Button button = CreateCardButton(_handRow, card, font, affordable, isSelected);
                button.onClick.AddListener(() => OnHandCardPressed(capturedCard));

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

        /// <summary>Forces a RectTransform to a 1:1 square that fits inside its parent, so two
        /// sprites with different native aspects still end up exactly concentric.</summary>
        private static void AddSquareFitter(GameObject go)
        {
            var fitter = go.AddComponent<AspectRatioFitter>();
            fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            fitter.aspectRatio = 1f;
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
            // Enlarged from 118x180 (2026-08-06, "render player cards larger so artwork, Cost,
            // ATK and HP are clearly visible") - same aspect ratio, so the rarity frame's Sliced
            // 9-slice border (computed as a fraction of its own source texture, not the target
            // rect - see CardArtImportSettings) stretches cleanly at the new size.
            //
            // Capped at 196 tall, not pushed further: HandRow sits in the bottom 70% of HandPanel
            // (HandY0-HandY1 = 0.12-0.37 of a 1280-tall canvas = 320px, so 224px), and the row
            // does not mask its children, so a card taller than that bleeds into neighbouring UI
            // rather than clipping cleanly. 196 leaves ~28px of slack; verify this against the
            // real render before pushing it any larger.
            SetPreferredWidth(go, 130);
            SetPreferredHeight(go, 196);

            var bg = go.AddComponent<Image>();
            Sprite rarityFrame = GetRarityFrameSprite(card.Rarity);
            if (rarityFrame != null)
            {
                // The frame art's fill is opaque, not a transparent cutout (checked directly -
                // center pixel alpha is 255) - it's meant to sit *behind* the card art, which
                // then covers the fill within an inset margin, not composited through it.
                // Sliced (not Simple) - the frame's native aspect doesn't match the card button's
                // 118x180 rect, and Simple stretched it non-uniformly, warping the painted corner
                // ornaments ("the icons are distorted", 2026-08-05). See CardArtImportSettings
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

            // Art fills the frame, cropped rather than stretched or letterboxed - see
            // CreateCroppedArt for why this needed a third approach.
            CreateCroppedArt(go.transform, _cardDatabase.GetArt(card),
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
            var go = new GameObject($"Mini_{instance.Definition.Id}", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            SetPreferredWidth(go, 66);
            SetPreferredHeight(go, 78);

            var bg = go.AddComponent<Image>();
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

            // Cropped fill, not a stretch: filling by disabling preserveAspect squashed the
            // portrait art badly at this tile size ("I can't see anything after selecting the
            // cards" / "way too overstretched", 2026-08-06).
            CreateCroppedArt(go.transform, _cardDatabase.GetArt(instance.Definition),
                new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f));

            // Ownership rim - a thin tinted overlay, since the slot plate that used to carry
            // this information is no longer the background.
            Image rim = CreateImage(go.transform, instance.IsPlayerOwned
                ? new Color(0.3f, 0.8f, 0.9f, 0.22f)
                : new Color(0.9f, 0.3f, 0.25f, 0.22f));
            rim.raycastTarget = false;
            StretchFull(rim.rectTransform);

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
