using MyriadOfDragons.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// The Combat Resolution Stage - the Battle screen's top-rail VFX surface.
    ///
    /// REPLACES the scrolling COMBAT ACTIVITY text log. The owner's call: that space should carry
    /// animation, not sentences players are meant to read. This reverses my own 2026-08-22
    /// readability pass on the same rail, which is worth stating rather than quietly overwriting -
    /// the earlier work was correct for the request it answered.
    ///
    /// PASSIVE BY CONSTRUCTION. It visualises events the controller has already resolved. It never
    /// determines a result, never delays simulation, and never receives input:
    ///   - every child is created with raycastTarget = false, so the spell rail beneath stays fully
    ///     tappable (the doc's acceptance check, and the exact bug class that made Guild Hall's
    ///     letterboxed background swallow taps);
    ///   - a RectMask2D clips everything, so no particle can escape into the board or spell buttons;
    ///   - the queue drops beats rather than asking combat to wait.
    ///
    /// This MonoBehaviour owns ONLY pixels and timing. Every decision about what plays, what merges
    /// and what must never be merged lives in CombatResolutionQueue, which is a plain class with
    /// its own tests (CLAUDE.md non-negotiable #6).
    /// </summary>
    public class CombatResolutionStage : MonoBehaviour
    {
        /// <summary>Held for ~0.8s after a beat, per the doc's outcome band.</summary>
        public const float OutcomeBandHoldSeconds = 0.8f;

        /// <summary>Doc minimum: important result numbers hold at least 650 ms. Stated as a
        /// constant so a future timing tweak cannot silently drop below the legibility floor.</summary>
        public const float MinimumResultHoldSeconds = 0.65f;

        private readonly CombatResolutionQueue _queue = new CombatResolutionQueue();

        private RectTransform _root;
        private Text _resultValue;
        private Image _resultIcon;
        private Image _sourceProxy;
        private readonly Image[] _queueDiamonds = new Image[3];
        private readonly Image[] _lanePips = new Image[3];
        private Image _strikeLayer;
        private Sprite _particleMedium;
        private Sprite _particleHeavy;
        private float _activeBeatElapsed;

        public CombatResolutionQueue QueueForTests => _queue;
        public RectTransform RootForTests => _root;
        public string ResultTextForTests => _resultValue != null ? _resultValue.text : null;
        public Color ResultIconColorForTests => _resultIcon != null ? _resultIcon.color : Color.clear;
        public Vector2 ResultIconAnchorMinForTests =>
            _resultIcon != null ? _resultIcon.rectTransform.anchorMin : Vector2.zero;
        public bool StrikeLayerEnabledForTests => _strikeLayer != null && _strikeLayer.enabled;
        public bool LanePipsVisibleForTests => _lanePips[0] != null && _lanePips[0].enabled;
        public Sprite SourceProxySpriteForTests => _sourceProxy != null ? _sourceProxy.sprite : null;
        public Sprite StrikeSpriteForTests => _strikeLayer != null ? _strikeLayer.sprite : null;

        /// <summary>Faction colours. Motion direction, icon and sign must agree with these - the doc
        /// is explicit that colour alone never carries meaning.</summary>
        private static readonly Color EnemyRed = new Color(0.55f, 0.13f, 0.13f);
        private static readonly Color PlayerEmerald = new Color(0.18f, 0.55f, 0.35f);
        private static readonly Color NeutralCyan = new Color(0.35f, 0.70f, 0.78f);

        public void Initialize(RectTransform parent)
        {
            BuildUI(parent);
        }

        private void BuildUI(RectTransform parent)
        {
            var stageObj = new GameObject("CombatResolutionStage",
                typeof(RectTransform), typeof(RectMask2D));
            stageObj.transform.SetParent(parent, false);
            _root = stageObj.GetComponent<RectTransform>();
            _root.anchorMin = Vector2.zero;
            _root.anchorMax = Vector2.one;
            _root.offsetMin = Vector2.zero;
            _root.offsetMax = Vector2.zero;

            // Aperture: opaque recess so the stage never shows whatever sits behind the rail.
            Image aperture = CreateChild("ApertureBackground", _root);
            aperture.color = new Color(0.07f, 0.08f, 0.12f, 1f);
            Stretch(aperture.rectTransform, 0f, 0.30f, 1f, 1f);

            _sourceProxy = CreateChild("SourceProxy", _root);
            _sourceProxy.color = NeutralCyan;
            Stretch(_sourceProxy.rectTransform, 0.04f, 0.55f, 0.22f, 0.92f);

            _resultIcon = CreateChild("ResultIcon", _root);
            _resultIcon.color = NeutralCyan;
            Stretch(_resultIcon.rectTransform, 0.26f, 0.55f, 0.40f, 0.92f);

            var valueObj = new GameObject("SignedNumericValue", typeof(RectTransform), typeof(Text));
            valueObj.transform.SetParent(_root, false);
            _resultValue = valueObj.GetComponent<Text>();
            _resultValue.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Doc floor: 32 px minimum at 1920x1080, bold, so the number stays readable through
            // particles. Set here rather than inherited, because the old log's 15 px would be
            // illegible against a lit aperture.
            _resultValue.fontSize = 34;
            _resultValue.fontStyle = FontStyle.Bold;
            _resultValue.alignment = TextAnchor.MiddleLeft;
            _resultValue.color = Color.white;
            _resultValue.raycastTarget = false;
            _resultValue.text = string.Empty;
            Stretch(_resultValue.rectTransform, 0.44f, 0.55f, 0.98f, 0.92f);

            // Lane pips: three slots, shown ONLY for lane-bearing beats. The doc asks for three
            // slot pips on lane/defeat beats specifically; showing them on every beat would make
            // an avatar hit look like a lane event.
            for (int i = 0; i < _lanePips.Length; i++)
            {
                Image pip = CreateChild("LanePip" + i, _root);
                float pipLeft = 0.05f + i * 0.06f;
                Stretch(pip.rectTransform, pipLeft, 0.06f, pipLeft + 0.045f, 0.20f);
                _lanePips[i] = pip;
            }

            // AvatarStrike uses its own exclusive flipbook - the doc forbids reusing it for any
            // other effect, so it sits on a layer only that beat enables.
            _strikeLayer = CreateChild("AvatarStrikeLayer", _root);
            _strikeLayer.sprite = Resources.Load<Sprite>("VFX/avatarstrike_bespoke_sheet");

            // Loaded ONCE here rather than per beat. Resources.Load is not free and a beat can
            // fire several times a second during a fast tick sequence; caching also means a
            // missing asset degrades identically every time instead of intermittently.
            //
            // These are only usable at all because the three VFX assets were re-imported as
            // Sprites (textureType 8) - they were plain Textures until now, so every
            // Resources.Load<Sprite> against them returned null regardless of folder.
            _particleMedium = Resources.Load<Sprite>("VFX/particle_medium");
            _particleHeavy = Resources.Load<Sprite>("VFX/particle_heavy");
            _strikeLayer.preserveAspect = true;
            _strikeLayer.enabled = false;
            Stretch(_strikeLayer.rectTransform, 0.26f, 0.35f, 0.70f, 0.95f);

            for (int i = 0; i < _queueDiamonds.Length; i++)
            {
                Image diamond = CreateChild("QueueDiamond" + i, _root);
                diamond.color = new Color(1f, 1f, 1f, 0.18f);
                float left = 0.40f + i * 0.07f;
                Stretch(diamond.rectTransform, left, 0.06f, left + 0.05f, 0.20f);
                _queueDiamonds[i] = diamond;
            }

            RefreshQueueIndicator();
        }

        /// <summary>
        /// Creates a stage child with raycasts OFF.
        ///
        /// Centralised so no future child can be added with raycasts left on by accident - that is
        /// the single mistake that would break the spell rail below, and it is invisible until
        /// someone taps and nothing happens.
        /// </summary>
        private static Image CreateChild(string name, Transform parent)
        {
            var obj = new GameObject(name, typeof(RectTransform), typeof(Image));
            obj.transform.SetParent(parent, false);
            Image image = obj.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private static void Stretch(RectTransform rect, float minX, float minY, float maxX, float maxY)
        {
            rect.anchorMin = new Vector2(minX, minY);
            rect.anchorMax = new Vector2(maxX, maxY);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        /// <summary>Queues resolved beats. Never blocks; the queue merges when it falls behind.</summary>
        public void Enqueue(CombatResolutionEvent beat) => _queue.Enqueue(beat);

        /// <summary>
        /// Advances presentation by one step.
        ///
        /// Takes deltaTime rather than reading Time.deltaTime so an EditMode test can drive a full
        /// beat without a running player loop - the same reason every Circuit entry point takes
        /// nowUtc. EditMode cannot run Update(), so anything that only worked there would be
        /// permanently untestable.
        /// </summary>
        public void Tick(float deltaTime)
        {
            if (!_queue.HasActive)
            {
                if (!_queue.TryAdvance())
                {
                    RefreshQueueIndicator();
                    return;
                }

                _activeBeatElapsed = 0f;
                Present(_queue.Active);
                RefreshQueueIndicator();
                return;
            }

            _activeBeatElapsed += deltaTime;
            if (_activeBeatElapsed >= OutcomeBandHoldSeconds)
            {
                _queue.CompleteActive();
                RefreshQueueIndicator();
            }
        }

        /// <summary>
        /// Paints one beat.
        ///
        /// FALLBACK BY DEFAULT: proxy + icon + exact signed number. The doc requires that when a
        /// particle or bespoke asset is unavailable the stage still shows those three things and
        /// never reverts to text. Building the fallback as the BASE and layering effects on top
        /// means a missing asset degrades instead of blanking.
        /// </summary>
        private void Present(CombatResolutionEvent beat)
        {
            Color side = SideColor(beat.Source);
            if (_sourceProxy != null) _sourceProxy.color = side;

            // PER-TYPE COMPOSITION. Without this, every beat rendered identically and only the side
            // colour changed - clash, spell, defeat and avatar-health were indistinguishable, which
            // fails the design doc acceptance check "distinguishable without reading prose". Colour
            // alone is explicitly not permitted to carry meaning, so each type differs in SHAPE as
            // well as tint and stays readable in greyscale.
            ApplyIconForType(beat, side);
            ApplyLanePips(beat, side);
            ApplyParticleTier(beat);

            if (_strikeLayer != null)
                _strikeLayer.enabled = beat.Type == CombatResolutionEventType.AvatarStrikeResolved;

            if (_resultValue == null) return;

            // Explicit sign, always. An unsigned "6" cannot be told from "-6" at a glance, and the
            // number must agree with the icon and motion direction.
            _resultValue.text = beat.SignedValue == 0
                ? "0"
                : (beat.SignedValue > 0 ? "+" : "") + beat.SignedValue;
        }

        private static Color SideColor(CombatResolutionSide source)
        {
            if (source == CombatResolutionSide.Player) return PlayerEmerald;
            if (source == CombatResolutionSide.Enemy) return EnemyRed;
            return NeutralCyan;
        }

        /// <summary>
        /// Puts real particle art behind the beat, chosen by its already-mapped visual tier.
        ///
        /// TIER IS NEVER DERIVED FROM DAMAGE - the doc forbids it, because computing weight from a
        /// number invents a combat classification the game does not have. Heavy is reserved for
        /// effects already classified heavy, and AvatarStrike has its own exclusive sheet which is
        /// never reused for anything else.
        ///
        /// Falls back to NO sprite rather than a wrong one: a null sprite renders as the flat tinted
        /// proxy the doc specifies as the degraded state (proxy + icon + exact number), which stays
        /// readable. Substituting whatever art happened to load would misreport the effect's weight.
        /// </summary>
        private void ApplyParticleTier(CombatResolutionEvent beat)
        {
            if (_sourceProxy == null) return;

            Sprite particle = beat.Tier switch
            {
                CombatResolutionTier.Heavy => _particleHeavy,
                CombatResolutionTier.AvatarStrike => _particleHeavy,
                _ => _particleMedium,
            };

            _sourceProxy.sprite = particle;
            // preserveAspect only matters once a sprite exists; setting it unconditionally keeps a
            // later asset swap from silently stretching the art.
            _sourceProxy.preserveAspect = particle != null;
        }

        /// <summary>
        /// Shapes the result icon per beat type.
        ///
        /// Aspect AND tint both vary, so beats stay distinguishable for a colour-blind player and in
        /// a still screenshot - a wide bar reads differently from a narrow block regardless of hue.
        /// </summary>
        private void ApplyIconForType(CombatResolutionEvent beat, Color side)
        {
            if (_resultIcon == null) return;

            switch (beat.Type)
            {
                case CombatResolutionEventType.ClashResolved:
                    _resultIcon.color = side;
                    Stretch(_resultIcon.rectTransform, 0.26f, 0.60f, 0.40f, 0.88f);
                    break;
                case CombatResolutionEventType.SpellResolved:
                    _resultIcon.color = SchoolTint(beat);
                    Stretch(_resultIcon.rectTransform, 0.27f, 0.58f, 0.39f, 0.90f);
                    break;
                case CombatResolutionEventType.CardDefeated:
                    _resultIcon.color = new Color(side.r * 0.45f, side.g * 0.45f, side.b * 0.45f, 1f);
                    Stretch(_resultIcon.rectTransform, 0.28f, 0.62f, 0.38f, 0.84f);
                    break;
                case CombatResolutionEventType.LaneStateChanged:
                    _resultIcon.color = NeutralCyan;
                    Stretch(_resultIcon.rectTransform, 0.24f, 0.66f, 0.42f, 0.80f);
                    break;
                default:
                    _resultIcon.color = side;
                    Stretch(_resultIcon.rectTransform, 0.25f, 0.55f, 0.41f, 0.92f);
                    break;
            }
        }

        /// <summary>
        /// School palette for spell beats, per the doc.
        ///
        /// Per-school particle SPRITES do not exist - only particle_medium and particle_heavy - so
        /// the school is expressed by tinting the shared sprite with the palette the doc states,
        /// rather than inventing an asset naming convention nobody agreed to. Flagged to CC.
        /// </summary>
        private static Color SchoolTint(CombatResolutionEvent beat)
        {
            if (!beat.HasSchool) return NeutralCyan;
            if (beat.School == MyriadOfDragons.Cards.CardElement.Andras)
                return new Color(0.78f, 0.28f, 0.18f);
            if (beat.School == MyriadOfDragons.Cards.CardElement.Ktini)
                return new Color(0.24f, 0.55f, 0.30f);
            return new Color(0.88f, 0.82f, 0.55f);
        }

        /// <summary>
        /// Lights the three lane pips, extinguishing the ones the beat reports gone.
        ///
        /// Hidden entirely for beats with no lane. RemainingSlots is -1 when the beat says nothing
        /// about slot state, which is why that is distinct from 0 - treating unknown as zero would
        /// black out the pips on every ordinary clash.
        /// </summary>
        private void ApplyLanePips(CombatResolutionEvent beat, Color side)
        {
            for (int i = 0; i < _lanePips.Length; i++)
            {
                if (_lanePips[i] == null) continue;

                if (!beat.HasLane)
                {
                    _lanePips[i].enabled = false;
                    continue;
                }

                _lanePips[i].enabled = true;
                bool lit = beat.RemainingSlots < 0 || i < beat.RemainingSlots;
                _lanePips[i].color = lit ? side : new Color(0.20f, 0.20f, 0.24f, 1f);
            }
        }

        private void RefreshQueueIndicator()
        {
            int lit = _queue.VisibleQueueIndicators;
            for (int i = 0; i < _queueDiamonds.Length; i++)
            {
                if (_queueDiamonds[i] == null) continue;
                _queueDiamonds[i].color = i < lit
                    ? new Color(1f, 1f, 1f, 0.75f)
                    : new Color(1f, 1f, 1f, 0.18f);
            }
        }

        /// <summary>Stops everything for scene exit, result transition or replay skip.</summary>
        public void ClearAll()
        {
            _queue.Clear();
            if (_resultValue != null) _resultValue.text = string.Empty;
            RefreshQueueIndicator();
        }

        private void OnDestroy() => _queue.Clear();
    }
}
