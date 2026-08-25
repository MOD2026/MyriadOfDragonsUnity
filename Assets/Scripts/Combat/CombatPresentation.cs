using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Combat
{
    /// <summary>The four presentation targets of the vertical slice (register:
    /// "Vertical-slice spec REFINED and LOCKED", 2026-08-25).</summary>
    public enum CombatPresentationSubject
    {
        BasicCardAttack,
        DamageSpell,
        HealSpell,
        AvatarStrike,
    }

    /// <summary>Named beats. Basic attacks and spells share Impact/Resolve; AvatarStrike keeps its
    /// own locked 4-beat sequence and deliberately does not reuse the spell beats.</summary>
    public enum CombatPresentationBeat
    {
        Commit,
        Cast,
        TravelOrChannel,
        Impact,
        Resolve,
        Lock,
        Release,
        Consequence,
    }

    /// <summary>School palette driving the particle burst. Locked 3-layer model.</summary>
    public enum CombatPresentationPalette
    {
        None,
        Andras,     // ember / crimson
        Ktini,      // jade / earthen
        Pnevmas,    // ivory / gold / cyan
        Bespoke,    // AvatarStrike only - never reused from an effect-type template
    }

    /// <summary>
    /// Visual weight tier. LOCKED: sized by visual tier, NOT derived from raw damage magnitude -
    /// "balance tuning must never force visual reauthoring". A magnitude retune must never change
    /// which art plays.
    /// </summary>
    public enum CombatPresentationVisualTier
    {
        None,
        Light,
        Medium,
        Heavy,
    }

    /// <summary>
    /// One beat's presentation contract: how long it runs and which cues fire at its start.
    /// Deliberately data, not behaviour - the sinks decide how to realise it.
    /// </summary>
    public sealed class CombatPresentationCue
    {
        public CombatPresentationBeat Beat;
        public int DurationMs;

        /// <summary>Peak shake in NORMALIZED units, not pixels - the spec specifies normalized
        /// units so the feel survives varied device sizes.</summary>
        public float CameraShakeUnits;

        /// <summary>How long the shake runs; never longer than the beat itself.</summary>
        public int CameraShakeMs;

        /// <summary>Scale applied to the PRESENTATION ROOT, never the full Canvas (LOCKED: HUD and
        /// resource text must stay stable across device sizes). 1.0 = no zoom.</summary>
        public float PresentationRootZoom = 1f;

        /// <summary>Logical audio cue ids firing at this beat. NOT asset paths - the base
        /// vocabulary is still to be licensed/commissioned, so these stay symbolic.</summary>
        public string[] AudioCueIds = new string[0];

        public CombatPresentationVisualTier VisualTier = CombatPresentationVisualTier.None;

        public bool HasCameraMove => PresentationRootZoom != 1f || CameraShakeUnits > 0f;
    }

    /// <summary>Where a particle burst would spawn. Hybrid model locked (ParticleSystem for
    /// embers/dust/sparks, flipbooks for authored silhouettes, simple sprites for basic attacks) -
    /// which of those a tier maps to is the sink's concern, not this scaffolding's.</summary>
    public interface ICombatParticleSink
    {
        void Emit(CombatPresentationPalette palette, CombatPresentationVisualTier tier, int laneIndex);
    }

    /// <summary>Applies a SCOPED PRESENTATION-ROOT tween. LOCKED: no virtual-camera system for
    /// this slice, and never a full-Canvas scale.</summary>
    public interface ICombatPresentationRootSink
    {
        void Apply(float zoom, float shakeUnits, int shakeMs, int laneIndex);
    }

    /// <summary>Where audio cues fire. No final SFX source exists yet.</summary>
    public interface ICombatAudioSink
    {
        void Play(string audioCueId);
    }

    /// <summary>Applies the actual gameplay outcome. Kept behind an interface so the playback
    /// below can guarantee it runs EXACTLY ONCE per presentation, skip or no skip.</summary>
    public interface ICombatResolveSink
    {
        void ApplyResolve();
    }

    /// <summary>
    /// Beat/timing/cue scaffolding for the vertical slice's four presentation targets, built to
    /// the LOCKED spec (register: "Vertical-slice spec REFINED and LOCKED", 2026-08-25).
    ///
    /// Plain static C#, no MonoBehaviour: EditMode cannot run Update() or coroutines, so the beat
    /// SEQUENCE, its cue contract and the resolve-once guarantee live here where they can be
    /// asserted, and a MonoBehaviour supplies only wall-clock timing. Nothing here loads an asset
    /// or touches a renderer.
    /// </summary>
    public static class CombatPresentation
    {
        public const string CueCommit = "combat.commit";
        public const string CueCast = "combat.cast";
        public const string CueImpact = "combat.impact";
        public const string CueSoftResolve = "combat.resolve.soft";
        public const string CueAvatarStrikeReleaseImpact = "avatarstrike.release.impact";
        public const string CueAvatarStrikeStinger = "avatarstrike.release.stinger";

        /// <summary>Hard ceiling from the spec: no presentation may exceed one second.</summary>
        public const int HardCeilingMs = 1000;

        public static IReadOnlyList<CombatPresentationCue> SequenceFor(CombatPresentationSubject subject)
        {
            switch (subject)
            {
                case CombatPresentationSubject.BasicCardAttack:
                    // 350-400ms. No zoom, 2-4 normalized shake units. AUDIO: impact ONLY.
                    return new[]
                    {
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Commit, DurationMs = 130 },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Impact, DurationMs = 100,
                            CameraShakeUnits = 3f, CameraShakeMs = 80,
                            AudioCueIds = new[] { CueImpact } },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Resolve, DurationMs = 140 },
                    };

                case CombatPresentationSubject.DamageSpell:
                    // Reduced to 600-700ms (was 800 - repeat-cast fatigue). Zoom 1.04-1.06x.
                    // AUDIO: cast + impact. No resolve cue - that is heal/buff only.
                    return new[]
                    {
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Cast, DurationMs = 150,
                            PresentationRootZoom = 1.05f, AudioCueIds = new[] { CueCast } },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.TravelOrChannel, DurationMs = 200,
                            PresentationRootZoom = 1.05f },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Impact, DurationMs = 120,
                            PresentationRootZoom = 1.06f, AudioCueIds = new[] { CueImpact },
                            VisualTier = CombatPresentationVisualTier.Medium },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Resolve, DurationMs = 180 },
                    };

                case CombatPresentationSubject.HealSpell:
                    // Same envelope; the OPTIONAL soft resolve cue is heal/buff only.
                    return new[]
                    {
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Cast, DurationMs = 150,
                            PresentationRootZoom = 1.05f, AudioCueIds = new[] { CueCast } },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.TravelOrChannel, DurationMs = 200,
                            PresentationRootZoom = 1.05f },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Impact, DurationMs = 120,
                            PresentationRootZoom = 1.06f, AudioCueIds = new[] { CueImpact },
                            VisualTier = CombatPresentationVisualTier.Medium },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Resolve, DurationMs = 180,
                            AudioCueIds = new[] { CueSoftResolve } },
                    };

                case CombatPresentationSubject.AvatarStrike:
                    // 800-1000ms, hard ceiling 1s. Zoom 1.08-1.12x (NOT 1.15x - real clipping risk
                    // on varied 16:9 devices). AUDIO: commit + release-impact + bespoke stinger.
                    return new[]
                    {
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Commit, DurationMs = 300,
                            PresentationRootZoom = 1.10f, AudioCueIds = new[] { CueCommit } },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Lock, DurationMs = 200,
                            PresentationRootZoom = 1.10f },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Release, DurationMs = 150,
                            PresentationRootZoom = 1.12f, CameraShakeUnits = 6f, CameraShakeMs = 120,
                            AudioCueIds = new[] { CueAvatarStrikeReleaseImpact, CueAvatarStrikeStinger },
                            VisualTier = CombatPresentationVisualTier.Heavy },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Consequence, DurationMs = 350 },
                    };
            }
            return new CombatPresentationCue[0];
        }

        public static int TotalDurationMs(CombatPresentationSubject subject) =>
            SequenceFor(subject).Sum(c => c.DurationMs);

        /// <summary>AvatarStrike is bespoke by lock; a basic attack has no particles at all;
        /// everything else takes the caster's school palette.</summary>
        public static CombatPresentationPalette PaletteFor(
            CombatPresentationSubject subject, CombatPresentationPalette casterSchool)
        {
            if (subject == CombatPresentationSubject.AvatarStrike) return CombatPresentationPalette.Bespoke;
            if (subject == CombatPresentationSubject.BasicCardAttack) return CombatPresentationPalette.None;
            return casterSchool;
        }

        public static void FireBeat(
            CombatPresentationCue cue,
            CombatPresentationPalette palette,
            int laneIndex,
            ICombatParticleSink particles = null,
            ICombatPresentationRootSink presentationRoot = null,
            ICombatAudioSink audio = null)
        {
            if (cue == null) return;

            if (audio != null)
                foreach (string id in cue.AudioCueIds)
                    if (!string.IsNullOrEmpty(id)) audio.Play(id);

            if (presentationRoot != null && cue.HasCameraMove)
                presentationRoot.Apply(cue.PresentationRootZoom, cue.CameraShakeUnits, cue.CameraShakeMs, laneIndex);

            if (particles != null && cue.VisualTier != CombatPresentationVisualTier.None
                && palette != CombatPresentationPalette.None)
                particles.Emit(palette, cue.VisualTier, laneIndex);
        }
    }

    /// <summary>How a playback was ended.</summary>
    public enum CombatPresentationEnd
    {
        StillPlaying,
        PlayedThrough,
        Skipped,
    }

    /// <summary>
    /// Drives one presentation and guarantees the gameplay outcome applies EXACTLY ONCE.
    ///
    /// This is the safety-critical half of the locked spec: "Resolve state must be IDEMPOTENT -
    /// skipping can never duplicate damage, healing, SFX, or rewards." Skipping mid-sequence and
    /// then letting the sequence finish, or two skip inputs racing, must still resolve once. That
    /// is enforced here rather than trusted to every call site.
    ///
    /// Skip is deliberately NOT "any second tap" - the spec tightened this because a stray tap on
    /// a card/lane/rail/button would otherwise skip the presentation as a side effect. Only
    /// <see cref="SkipFromDedicatedControl"/> ends a playback early, and a caller must have
    /// already decided the input came from the skip control or a non-interactive battle area.
    /// </summary>
    public sealed class CombatPresentationPlayback
    {
        private readonly IReadOnlyList<CombatPresentationCue> _sequence;
        private readonly ICombatResolveSink _resolve;
        private int _nextBeatIndex;
        private bool _resolveApplied;

        public CombatPresentationSubject Subject { get; }
        public CombatPresentationEnd State { get; private set; } = CombatPresentationEnd.StillPlaying;

        /// <summary>True once the outcome has been applied. Never becomes false again.</summary>
        public bool ResolveApplied => _resolveApplied;

        public int BeatsPlayed => _nextBeatIndex;
        public int BeatCount => _sequence.Count;

        public CombatPresentationPlayback(CombatPresentationSubject subject, ICombatResolveSink resolve = null)
        {
            Subject = subject;
            _sequence = CombatPresentation.SequenceFor(subject);
            _resolve = resolve;
        }

        /// <summary>Advances one beat. Returns null once the sequence is finished or skipped.</summary>
        public CombatPresentationCue AdvanceBeat()
        {
            if (State != CombatPresentationEnd.StillPlaying) return null;
            if (_nextBeatIndex >= _sequence.Count)
            {
                ApplyResolveOnce();
                State = CombatPresentationEnd.PlayedThrough;
                return null;
            }
            return _sequence[_nextBeatIndex++];
        }

        /// <summary>
        /// Ends the presentation early and settles straight to the resolved end state. Only ever
        /// call this for input from the dedicated skip control or a non-interactive battle area -
        /// never from a card/lane/rail/button tap.
        /// Safe to call repeatedly: the outcome still applies exactly once.
        /// </summary>
        public void SkipFromDedicatedControl()
        {
            if (State == CombatPresentationEnd.StillPlaying) State = CombatPresentationEnd.Skipped;
            _nextBeatIndex = _sequence.Count;
            ApplyResolveOnce();
        }

        /// <summary>Plays every remaining beat in order, then resolves once.</summary>
        public void PlayToEnd(
            CombatPresentationPalette palette = CombatPresentationPalette.None,
            int laneIndex = 0,
            ICombatParticleSink particles = null,
            ICombatPresentationRootSink presentationRoot = null,
            ICombatAudioSink audio = null)
        {
            CombatPresentationCue cue;
            while ((cue = AdvanceBeat()) != null)
                CombatPresentation.FireBeat(cue, palette, laneIndex, particles, presentationRoot, audio);
        }

        private void ApplyResolveOnce()
        {
            if (_resolveApplied) return;
            _resolveApplied = true;
            _resolve?.ApplyResolve();
        }
    }
}
