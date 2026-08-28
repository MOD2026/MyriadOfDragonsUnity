using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Interaction-state tokens and pure resolution logic (register: "Interaction states -
    /// LOCKED 2026-08-27"). States COMPOSE (e.g. Selected+Pending, Locked+New) rather than each
    /// getting a separate art system - this is a bitmask, not a single enum value.
    ///
    /// Everything here is a plain, deterministic function of (flags, tier, milliseconds-since-
    /// last-change) - no Update() dependency, matching this project's "real logic in plain
    /// testable methods; MonoBehaviours supply only timing" rule (CLAUDE.md, BattleController.
    /// AdvanceCombatTick() pattern). <see cref="InteractionStateController"/> is the thin
    /// MonoBehaviour that calls Resolve() from Update() and applies the result; every EditMode
    /// test calls Resolve() directly with an explicit time value instead.
    /// </summary>
    [System.Flags]
    public enum InteractionStateFlags
    {
        None = 0,
        Pressed = 1 << 0,
        Disabled = 1 << 1,
        Focused = 1 << 2,
        Selected = 1 << 3,
        Pending = 1 << 4,
        Locked = 1 << 5,
        New = 1 << 6,
        Error = 1 << 7,
    }

    /// <summary>The resolved visual to apply to a control RIGHT NOW - everything a driver needs to
    /// push onto RectTransform.localScale, a Graphic's colour/alpha, an optional CanvasGroup, and
    /// an accent glow/underline element, in one struct so a single Resolve() call is enough.</summary>
    public struct InteractionVisual
    {
        public float Scale;
        public float Brightness;          // multiplies the control's base colour RGB
        public float Opacity;             // multiplies alpha (CanvasGroup.alpha or Graphic.a)
        public float AccentGlowStrength;  // 0-1: focus/selected accent line-glow intensity
        public bool AccentIsUnderlineOnly; // Selected: underline/glow, explicitly NO box
        public float ShadowStrength;      // multiplies the base shadow token's opacity
        public bool ShowNewBadge;
        public Color? ErrorFlashTint;     // non-null only during an active red-shift flash frame

        public static InteractionVisual Base => new InteractionVisual
        {
            Scale = 1f,
            Brightness = 1f,
            Opacity = 1f,
            AccentGlowStrength = 0f,
            AccentIsUnderlineOnly = false,
            ShadowStrength = 1f,
            ShowNewBadge = false,
            ErrorFlashTint = null,
        };
    }

    public static class UIInteractionStateTokens
    {
        public struct PressSpec
        {
            public float Scale;
            public float Darken;
            public float DurationMs;
        }

        /// <summary>Feedback amplitude by frame tier (register: "Feedback amplitude by tier, one
        /// language throughout"). Tier 4 gets no scale change on small icons - a highlight/tint
        /// only, per the lock's own carve-out.</summary>
        public static PressSpec PressSpecFor(UIDesignTokens.FrameTier tier)
        {
            switch (tier)
            {
                case UIDesignTokens.FrameTier.Tier1Hero: return new PressSpec { Scale = 0.96f, Darken = 0.125f, DurationMs = 80f };
                case UIDesignTokens.FrameTier.Tier2Section: return new PressSpec { Scale = 0.97f, Darken = 0.09f, DurationMs = 70f };
                case UIDesignTokens.FrameTier.Tier3Utility: return new PressSpec { Scale = 0.98f, Darken = 0.065f, DurationMs = 60f };
                case UIDesignTokens.FrameTier.Tier4Surface:
                default: return new PressSpec { Scale = 1f, Darken = 0.065f, DurationMs = 60f };
            }
        }

        public const float DisabledBrightness = 0.52f;   // mid of locked 45-60%
        public const float DisabledOpacity = 0.52f;
        public const float LockedBrightness = 0.65f;      // mid of 60-70%
        public const float LockedOpacity = 0.70f;         // mid of 65-75%
        public const float PendingOpacityMin = 0.80f;
        public const float PendingOpacityMax = 0.90f;
        public const float PendingPulsePeriodMs = 800f;
        public const float FocusedBrightness = 1.08f;
        public const float SelectedBrightness = 1.10f;
        public const float NewBrightness = 1.10f;
        public const float NewRevealDurationMs = 600f;
        public const float ErrorFlashDurationMs = 150f;   // mid of 120-180
        public const int ErrorFlashCount = 2;
        public static readonly Color ErrorRedShift = new Color(0.85f, 0.22f, 0.2f);

        /// <summary>Touch-feedback acknowledgement latency target (register: "visual feedback
        /// latency should be 30-85ms, our 50-70ms target sits inside it"). Not a timer this code
        /// runs - a budget for whoever wires the actual pointer-down callback to respect.</summary>
        public const float AckLatencyTargetMs = 60f;
        public const float AckLatencyMaxMs = 100f;

        /// <summary>A control must not accept a second activation within this window of its last
        /// one - the real bug class CC flagged ("several of our actions grant currency"). Chosen
        /// as the Pending pulse period: an action that takes long enough to need Pending feedback
        /// is exactly the case a fast double-tap would otherwise double-fire.</summary>
        public const float ActivationCooldownMs = PendingPulsePeriodMs;

        /// <summary>Pure resolution: given the current composed state and how long it has held
        /// that state, returns the visual to apply. No randomness, no hidden state - the same
        /// inputs always produce the same output, which is what makes this testable without
        /// Update(). When <paramref name="reduceMotion"/> is true, decorative loops/reveals
        /// resolve immediately to a static look; press/disabled/selected/error feedback stay.</summary>
        public static InteractionVisual Resolve(InteractionStateFlags flags, UIDesignTokens.FrameTier tier, float msSinceChange,
            bool reduceMotion = false)
        {
            InteractionVisual v = InteractionVisual.Base;
            bool disabled = (flags & InteractionStateFlags.Disabled) != 0;
            bool locked = (flags & InteractionStateFlags.Locked) != 0;
            bool inert = disabled || locked; // neither may ever animate as if it accepted input

            // Disabled/Locked are the base look; they suppress Pressed/Focused/Pending feedback
            // (a disabled or locked control must never look like it is responding to touch) but
            // do not suppress Selected/New/Error, which describe WHAT the control is, not
            // whether it currently accepts input - real composition (register: "locked+new").
            if (disabled)
            {
                v.Brightness = DisabledBrightness;
                v.Opacity = DisabledOpacity;
                v.ShadowStrength = 0f;
            }
            else if (locked)
            {
                v.Brightness = LockedBrightness;
                v.Opacity = LockedOpacity;
                v.ShadowStrength = 0.5f;
                v.AccentGlowStrength = 0.3f; // "Tier-3 equivalent" border language
            }

            if (!inert && (flags & InteractionStateFlags.Pressed) != 0)
            {
                // Press acknowledgement is essential state feedback — keep under reduced motion.
                PressSpec spec = PressSpecFor(tier);
                float t = reduceMotion
                    ? 1f
                    : Mathf.Clamp01(msSinceChange / Mathf.Max(1f, spec.DurationMs));
                v.Scale = Mathf.Lerp(1f, spec.Scale, t);
                v.Brightness *= Mathf.Lerp(1f, 1f - spec.Darken, t);
                v.ShadowStrength *= Mathf.Lerp(1f, 0.8f, t); // "-20%" shadow token
            }

            if (!inert && (flags & InteractionStateFlags.Focused) != 0)
            {
                v.Brightness = Mathf.Max(v.Brightness, FocusedBrightness);
                v.AccentGlowStrength = Mathf.Max(v.AccentGlowStrength, 1f);
                v.ShadowStrength *= 1.1f;
            }

            if ((flags & InteractionStateFlags.Selected) != 0)
            {
                v.Brightness = Mathf.Max(v.Brightness, SelectedBrightness);
                v.AccentGlowStrength = Mathf.Max(v.AccentGlowStrength, 1f);
                v.AccentIsUnderlineOnly = true; // "accent underline or glow, NO box"
            }

            if (!inert && (flags & InteractionStateFlags.Pending) != 0)
            {
                if (reduceMotion)
                {
                    // Static pending look — no decorative opacity pulse.
                    float mid = (PendingOpacityMin + PendingOpacityMax) * 0.5f;
                    v.Opacity = Mathf.Min(v.Opacity, mid);
                }
                else
                {
                    float phase = Mathf.Repeat(msSinceChange, PendingPulsePeriodMs) / PendingPulsePeriodMs;
                    float pulse = 0.5f - 0.5f * Mathf.Cos(phase * Mathf.PI * 2f); // 0..1..0, smooth
                    v.Opacity = Mathf.Min(v.Opacity, Mathf.Lerp(PendingOpacityMin, PendingOpacityMax, pulse));
                }
            }

            if ((flags & InteractionStateFlags.New) != 0)
            {
                v.ShowNewBadge = true;
                if (!reduceMotion && msSinceChange < NewRevealDurationMs)
                {
                    float t = Mathf.Clamp01(msSinceChange / NewRevealDurationMs);
                    v.Brightness = Mathf.Max(v.Brightness, Mathf.Lerp(NewBrightness, 1f, t));
                    v.Scale = Mathf.Max(v.Scale, Mathf.Lerp(1.06f, 1f, t));
                }
            }

            if ((flags & InteractionStateFlags.Error) != 0)
            {
                float cycleMs = ErrorFlashDurationMs;
                float totalMs = cycleMs * ErrorFlashCount * 2f; // on/off per flash
                if (reduceMotion)
                {
                    // One solid error tint — essential feedback without a decorative flash loop.
                    v.ErrorFlashTint = ErrorRedShift;
                }
                else if (msSinceChange < totalMs)
                {
                    int cycleIndex = (int)(msSinceChange / cycleMs);
                    bool flashOn = cycleIndex % 2 == 0;
                    if (flashOn) v.ErrorFlashTint = ErrorRedShift;
                }
            }

            return v;
        }
    }

    /// <summary>
    /// Thin driver: owns the touch-down/up/drag-cancel state machine and calls
    /// <see cref="UIInteractionStateTokens.Resolve"/> from Update() to paint the result onto this
    /// control's RectTransform/Graphic/CanvasGroup. No Animator Controller (register: "NO Animator
    /// Controller... One reusable tween layer"), and NOTHING here that a plain method call cannot
    /// exercise directly - every method below is callable and assertable without Update() ever
    /// running (EditMode cannot run Update() - CLAUDE.md), matching BattleController.
    /// AdvanceCombatTick()'s testability pattern. Update() itself is the ONLY piece Play Mode-only
    /// timing supplies; it does nothing Resolve() doesn't already do given an explicit time.
    ///
    /// Momentary localScale changes here (0.96-0.98 during a press) are the locked press-feedback
    /// tokens, not a "layout reflow" - the register's "must not change hit rectangle and layout
    /// position" rule is about state persisting or displacing siblings, not the transient squish
    /// every mobile button already does on press.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class InteractionStateController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private UIDesignTokens.FrameTier tier = UIDesignTokens.FrameTier.Tier3Utility;

        private InteractionStateFlags _flags;
        private float _lastChangeTimeSec = float.NegativeInfinity;
        private float _lastActivationTimeSec = float.NegativeInfinity;
        private bool _pointerDown;
        private bool _pointerInside;

        private RectTransform _rect;
        private Graphic _graphic;
        private Color _baseColor = Color.white;
        private CanvasGroup _canvasGroup;
        private Button _button;

        /// <summary>What ApplyAt itself last wrote (or observed) on `_button.interactable` -
        /// distinguishes an EXTERNAL change (GameBootstrap/a presenter set .interactable directly)
        /// from our own write-back reflecting straight back at us, so the two-way sync below can't
        /// turn into a feedback loop that clobbers an explicit SetDisabled() call made this same
        /// frame (see ApplyAt's own comment).</summary>
        private bool _lastObservedInteractable = true;

        /// <summary>Fires once per valid activation - pointer went down inside, stayed inside (or
        /// re-entered before release) through touch-up, and the idempotent-activation cooldown has
        /// elapsed. Never fires twice for one fast double-tap within that cooldown.</summary>
        public event System.Action Activated;

        private bool _cached;

        private void Awake() => EnsureCached();

        /// <summary>Lazy + idempotent rather than Awake-only: a component built via
        /// AddComponent from a plain EditMode test is not guaranteed to have had Awake() run
        /// before test code calls its public methods (Awake timing is a Unity lifecycle detail,
        /// not something this project's own test convention should depend on) - every public
        /// entry point below calls this first instead.</summary>
        private void EnsureCached()
        {
            if (_cached) return;
            _cached = true;
            _rect = GetComponent<RectTransform>();
            _graphic = GetComponent<Graphic>();
            if (_graphic != null) _baseColor = _graphic.color;
            _canvasGroup = GetComponent<CanvasGroup>();
            _button = GetComponent<Button>();
            // Seed from the button's REAL starting state (some callers, e.g. GameBootstrap's
            // enemy lane buttons, start non-interactable by design) - without this, frame one's
            // sync in ApplyAt would misread that starting false as an "external change" against
            // the hardcoded _lastObservedInteractable default of true and flip it right back.
            if (_button != null)
            {
                _lastObservedInteractable = _button.interactable;
                if (!_button.interactable) _flags |= InteractionStateFlags.Disabled;
            }
        }

        public UIDesignTokens.FrameTier Tier { get => tier; set => tier = value; }
        public InteractionStateFlags FlagsForTests => _flags;

        public void SetDisabled(bool value) => SetFlag(InteractionStateFlags.Disabled, value);
        public void SetLocked(bool value) => SetFlag(InteractionStateFlags.Locked, value);
        public void SetSelected(bool value) => SetFlag(InteractionStateFlags.Selected, value);
        public void SetFocused(bool value) => SetFlag(InteractionStateFlags.Focused, value);
        public void SetPending(bool value) => SetFlag(InteractionStateFlags.Pending, value);

        /// <summary>One-shot: marks New now (starts the 600ms reveal from this call); does not
        /// auto-clear - caller clears it once the player has plausibly seen it (e.g. on first
        /// open of the screen that contains it), matching "New" being a persistent badge state
        /// rather than a transient animation flag.</summary>
        public void SetNew(bool value) => SetFlag(InteractionStateFlags.New, value);

        /// <summary>One-shot: starts the red-shift flash now. Auto-clears itself once the flash
        /// sequence's total duration has elapsed - callers don't need to remember to clear it.</summary>
        public void TriggerError() => SetFlag(InteractionStateFlags.Error, true);

        private void SetFlag(InteractionStateFlags flag, bool value) => SetFlagAt(flag, value, Time.unscaledTime);

        private void SetFlagAt(InteractionStateFlags flag, bool value, float nowSec)
        {
            InteractionStateFlags updated = value ? (_flags | flag) : (_flags & ~flag);
            if (updated == _flags) return;
            _flags = updated;
            _lastChangeTimeSec = nowSec;
        }

        /// <summary>Directly sets the composed flags with an explicit "changed at" timestamp -
        /// for tests that want full control over both without going through the real engine
        /// clock or one flag at a time.</summary>
        public void SetFlagsForTests(InteractionStateFlags flags, float atSec)
        {
            _flags = flags;
            _lastChangeTimeSec = atSec;
        }

        /// <summary>Whether a touch-up right now would be accepted as a real activation - not in
        /// Disabled/Locked, and outside the idempotent-activation cooldown since the last one.
        /// Exposed so a caller can check before wiring destructive/currency-granting logic to
        /// Activated, though Activated itself already enforces this.</summary>
        public bool CanActivateForTests => CanActivateAt(Time.unscaledTime);

        public bool CanActivateAt(float nowSec)
        {
            if ((_flags & (InteractionStateFlags.Disabled | InteractionStateFlags.Locked)) != 0) return false;
            float msSinceLastActivation = (nowSec - _lastActivationTimeSec) * 1000f;
            return msSinceLastActivation >= UIInteractionStateTokens.ActivationCooldownMs;
        }

        // Real Unity event-interface entry points - thin wrappers around the *At(nowSec)
        // methods below, which carry all the real logic and are what EditMode tests call
        // directly with a fabricated time (Time.unscaledTime is not test-controllable and, per
        // CLAUDE.md, EditMode cannot run Update() anyway - same reasoning as BattleController.
        // AdvanceCombatTick()).
        public void OnPointerDown(PointerEventData eventData) => OnPointerDownAt(Time.unscaledTime);
        public void OnPointerUp(PointerEventData eventData) => OnPointerUpAt(Time.unscaledTime);
        public void OnPointerExit(PointerEventData eventData) => OnPointerExitAt(Time.unscaledTime);
        public void OnPointerEnter(PointerEventData eventData) => OnPointerEnterAt(Time.unscaledTime);

        public void OnPointerDownAt(float nowSec)
        {
            _pointerDown = true;
            _pointerInside = true;
            SetFlagAt(InteractionStateFlags.Pressed, true, nowSec);
        }

        public void OnPointerUpAt(float nowSec)
        {
            bool wasInside = _pointerInside;
            _pointerDown = false;
            SetFlagAt(InteractionStateFlags.Pressed, false, nowSec);

            // Commits ONLY if the pointer is still inside the hit rect on release (register:
            // "Action commits on touch-UP only if the pointer is still inside the hit rect. Drag
            // outside cancels the press and does not activate; re-entering before release
            // restores it") - OnPointerExitAt/EnterAt below track _pointerInside through the drag.
            // CanActivateAt is also the idempotent-activation guard: a fast double-tap's second
            // touch-up lands inside the cooldown window and is silently ignored, not queued.
            if (wasInside && CanActivateAt(nowSec))
            {
                _lastActivationTimeSec = nowSec;
                Activated?.Invoke();
            }
        }

        public void OnPointerExitAt(float nowSec)
        {
            _pointerInside = false;
            if (_pointerDown) SetFlagAt(InteractionStateFlags.Pressed, false, nowSec); // drag-cancel
        }

        public void OnPointerEnterAt(float nowSec)
        {
            _pointerInside = true;
            if (_pointerDown) SetFlagAt(InteractionStateFlags.Pressed, true, nowSec); // re-entry restores press
        }

        /// <summary>Applies the resolved visual for an explicit point in time - the exact method
        /// Update() calls with Time.unscaledTime, and the exact method an EditMode test calls
        /// with a fabricated time instead. Real logic; Update() below supplies nothing but the
        /// timing loop.</summary>
        public void ApplyAt(float nowSec)
        {
            EnsureCached();

            // Passive sync FROM Button.interactable, not just the reverse write-back below (CR,
            // 2026-08-27, Battle/Empire/Avatar wiring pass): GameBootstrap and the metagame
            // presenters already set `.interactable` directly at dozens of call sites across their
            // Refresh methods - rewiring every one of those to call SetDisabled() instead would be
            // a huge, risky sweep for no real gain. Mirroring the existing property here means
            // every one of those call sites gets disabled-state visual feedback for free, with zero
            // change to how they already manage interactable. Self-healing: this runs every frame,
            // so a control disabled then re-enabled elsewhere picks the flag back up automatically.
            //
            // Gated on _lastObservedInteractable, not a plain comparison against the current flag -
            // without that guard this would fight an explicit SetDisabled() call made the same
            // frame (own regression, caught before landing: SetDisabled(true) followed immediately
            // by ApplyAt(0f) would see Button.interactable still true - because the write-back below
            // hasn't run yet this frame - and incorrectly clear the flag it was just told to set).
            // Comparing against what WE last wrote instead of the live flag means this only fires on
            // a genuinely external change, never on our own write-back reflecting back at us.
            if (_button != null && _button.interactable != _lastObservedInteractable)
            {
                SetFlagAt(InteractionStateFlags.Disabled, !_button.interactable, nowSec);
            }

            float msSinceChange = (nowSec - _lastChangeTimeSec) * 1000f;
            InteractionVisual v = UIInteractionStateTokens.Resolve(
                _flags, tier, msSinceChange, MotionPolicy.ReduceMotion);

            if (_rect != null) _rect.localScale = new Vector3(v.Scale, v.Scale, 1f);

            if (_graphic != null)
            {
                Color c = v.ErrorFlashTint ?? new Color(_baseColor.r * v.Brightness, _baseColor.g * v.Brightness, _baseColor.b * v.Brightness, _baseColor.a);
                c.a *= v.Opacity;
                _graphic.color = c;
            }

            if (_canvasGroup != null) _canvasGroup.alpha = v.Opacity;

            if (_button != null)
            {
                _button.interactable = (_flags & (InteractionStateFlags.Disabled | InteractionStateFlags.Locked)) == 0;
                _lastObservedInteractable = _button.interactable;
            }

            // Error is one-shot and self-clearing once its flash sequence has fully played.
            if ((_flags & InteractionStateFlags.Error) != 0 &&
                msSinceChange >= UIInteractionStateTokens.ErrorFlashDurationMs * UIInteractionStateTokens.ErrorFlashCount * 2f)
            {
                SetFlag(InteractionStateFlags.Error, false);
            }
        }

        private void Update() => ApplyAt(Time.unscaledTime);
    }
}
