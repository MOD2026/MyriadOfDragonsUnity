using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Combat
{
    /// <summary>The four presentation targets of the vertical slice (register:
    /// "Vertical-slice parameter spec", 2026-08-25).</summary>
    public enum CombatPresentationSubject
    {
        BasicCardAttack,
        DamageSpell,
        HealSpell,
        AvatarStrike,
    }

    /// <summary>Named beats. Basic attacks and spells share Impact/Resolve; AvatarStrike has its
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

    /// <summary>Which school palette drives the particle burst. Locked 3-layer model.</summary>
    public enum CombatPresentationPalette
    {
        None,
        Andras,     // ember / crimson
        Ktini,      // jade / earthen
        Pnevmas,    // ivory / gold / cyan
        Bespoke,    // AvatarStrike only - never reused from an effect-type template
    }

    /// <summary>Particle scale tier. Mirrors the existing small/medium/large effect-magnitude
    /// tiers rather than inventing a new scale.</summary>
    public enum CombatPresentationParticleTier
    {
        None,
        Small,
        Medium,
        Large,
    }

    /// <summary>
    /// One beat's presentation contract: how long it runs, and which cues fire at its start.
    /// Deliberately data, not behaviour - the sinks below decide how to realise it.
    /// </summary>
    public sealed class CombatPresentationCue
    {
        public CombatPresentationBeat Beat;
        public int DurationMs;

        /// <summary>Peak camera shake in pixels (0 = no shake).</summary>
        public float CameraShakePixels;

        /// <summary>How long the shake runs; never longer than the beat itself.</summary>
        public int CameraShakeMs;

        /// <summary>Camera scale at this beat. 1.0 = no zoom.</summary>
        public float CameraZoom = 1f;

        /// <summary>Logical audio cue id. NOT an asset path - no SFX assets exist yet and the
        /// spec lists sourcing as an open question, so this stays a symbolic name.</summary>
        public string AudioCueId;

        public CombatPresentationParticleTier ParticleTier = CombatPresentationParticleTier.None;

        public bool HasCameraMove => CameraZoom != 1f || CameraShakePixels > 0f;
    }

    /// <summary>Where a particle burst would be spawned. Stub - no particle system is bound yet
    /// (Unity ParticleSystem vs pre-rendered flipbook is explicitly still undecided).</summary>
    public interface ICombatParticleSink
    {
        void Emit(CombatPresentationPalette palette, CombatPresentationParticleTier tier, int laneIndex);
    }

    /// <summary>Where camera moves would be applied. Stub - whether this needs a virtual camera
    /// rig or a canvas-scale tween is explicitly still undecided.</summary>
    public interface ICombatCameraSink
    {
        void Apply(float zoom, float shakePixels, int shakeMs, int laneIndex);
    }

    /// <summary>Where audio cues would fire. Stub - no SFX assets exist yet.</summary>
    public interface ICombatAudioSink
    {
        void Play(string audioCueId);
    }

    /// <summary>
    /// Beat/timing/cue scaffolding for the vertical slice's four presentation targets.
    ///
    /// Plain, testable, no MonoBehaviour: EditMode cannot run Update() or coroutines, so the beat
    /// SEQUENCE and its cue contract live here where they can be asserted, and a MonoBehaviour
    /// supplies only wall-clock timing later (CLAUDE.md's "real logic in plain testable methods"
    /// rule). Nothing here loads an asset or touches a renderer.
    ///
    /// The numbers come from the register's "Vertical-slice parameter spec", which is explicitly a
    /// DRAFT for GPT to refine - so they are centralised here as named data rather than scattered
    /// through call sites, and a later retune is a one-file change. Three things the spec leaves
    /// OPEN are deliberately NOT decided here: particle technology, whether the camera needs a rig
    /// or a tween, and SFX sourcing. Those are why the sinks are interfaces and the audio cue is a
    /// symbolic id rather than an asset path.
    /// </summary>
    public static class CombatPresentation
    {
        public const string CueCommitWhoosh = "combat.commit.whoosh";
        public const string CueCastWhoosh = "combat.cast.whoosh";
        public const string CueImpactHit = "combat.impact.hit";
        public const string CueImpactThud = "combat.impact.thud";
        public const string CueResolveChime = "combat.resolve.chime";
        public const string CueResolveLowTone = "combat.resolve.lowtone";
        public const string CueAvatarStrikeStinger = "avatarstrike.release.stinger";

        /// <summary>The beat sequence for one subject, in play order.</summary>
        public static IReadOnlyList<CombatPresentationCue> SequenceFor(
            CombatPresentationSubject subject, bool outcomeIsPositive = false)
        {
            switch (subject)
            {
                case CombatPresentationSubject.BasicCardAttack:
                    // "no camera move, impact micro-shake only (2-4px, 80ms)"; no particles, just
                    // the sprite clash.
                    return new[]
                    {
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Commit, DurationMs = 150,
                            AudioCueId = CueCommitWhoosh },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Impact, DurationMs = 100,
                            CameraShakePixels = 3f, CameraShakeMs = 80, AudioCueId = CueImpactThud },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Resolve, DurationMs = 150,
                            AudioCueId = outcomeIsPositive ? CueResolveChime : CueResolveLowTone },
                    };

                case CombatPresentationSubject.DamageSpell:
                case CombatPresentationSubject.HealSpell:
                    // "slight zoom toward target lane, hold through Impact." The spec's zoom range
                    // reads "015-1.08x" - a typo. 1.05x is used as the low end and flagged for the
                    // owner rather than silently inventing a value.
                    return new[]
                    {
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Cast, DurationMs = 200,
                            CameraZoom = 1.05f, AudioCueId = CueCastWhoosh },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.TravelOrChannel, DurationMs = 250,
                            CameraZoom = 1.05f },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Impact, DurationMs = 150,
                            CameraZoom = 1.08f, AudioCueId = CueImpactHit,
                            ParticleTier = CombatPresentationParticleTier.Medium },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Resolve, DurationMs = 200,
                            AudioCueId = outcomeIsPositive ? CueResolveChime : CueResolveLowTone },
                    };

                case CombatPresentationSubject.AvatarStrike:
                    // Its own locked 4-beat sequence, deliberately the longest - it is the
                    // commitment spell. Camera reticles on the Avatar panel, never a lane.
                    return new[]
                    {
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Commit, DurationMs = 300,
                            CameraZoom = 1.15f, AudioCueId = CueCommitWhoosh },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Lock, DurationMs = 200,
                            CameraZoom = 1.15f },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Release, DurationMs = 150,
                            CameraZoom = 1.15f, CameraShakePixels = 7f, CameraShakeMs = 120,
                            AudioCueId = CueAvatarStrikeStinger,
                            ParticleTier = CombatPresentationParticleTier.Large },
                        new CombatPresentationCue { Beat = CombatPresentationBeat.Consequence, DurationMs = 350,
                            AudioCueId = outcomeIsPositive ? CueResolveChime : CueResolveLowTone },
                    };
            }
            return new CombatPresentationCue[0];
        }

        public static int TotalDurationMs(CombatPresentationSubject subject) =>
            SequenceFor(subject).Sum(c => c.DurationMs);

        /// <summary>AvatarStrike's palette is bespoke by lock; every other subject takes the
        /// caster's school palette. A basic attack has no particles at all.</summary>
        public static CombatPresentationPalette PaletteFor(
            CombatPresentationSubject subject, CombatPresentationPalette casterSchool)
        {
            if (subject == CombatPresentationSubject.AvatarStrike) return CombatPresentationPalette.Bespoke;
            if (subject == CombatPresentationSubject.BasicCardAttack) return CombatPresentationPalette.None;
            return casterSchool;
        }

        /// <summary>
        /// Drives the cues for one beat into whichever sinks are bound. Null sinks are skipped, so
        /// this is callable today with nothing wired - which is the point of the scaffolding: the
        /// trigger points exist and are testable before any asset does.
        /// </summary>
        public static void FireBeat(
            CombatPresentationCue cue,
            CombatPresentationPalette palette,
            int laneIndex,
            ICombatParticleSink particles = null,
            ICombatCameraSink camera = null,
            ICombatAudioSink audio = null)
        {
            if (cue == null) return;

            if (audio != null && !string.IsNullOrEmpty(cue.AudioCueId)) audio.Play(cue.AudioCueId);
            if (camera != null && cue.HasCameraMove)
                camera.Apply(cue.CameraZoom, cue.CameraShakePixels, cue.CameraShakeMs, laneIndex);
            if (particles != null && cue.ParticleTier != CombatPresentationParticleTier.None
                && palette != CombatPresentationPalette.None)
                particles.Emit(palette, cue.ParticleTier, laneIndex);
        }

        /// <summary>
        /// Fast-forward: "a second tap/input during any beat immediately jumps to Resolve's end
        /// state - no animation ever blocks the next decision." Returns the final beat so a caller
        /// can settle straight to its end state, skipping everything in between.
        /// </summary>
        public static CombatPresentationCue FinalBeatFor(
            CombatPresentationSubject subject, bool outcomeIsPositive = false)
        {
            IReadOnlyList<CombatPresentationCue> seq = SequenceFor(subject, outcomeIsPositive);
            return seq.Count == 0 ? null : seq[seq.Count - 1];
        }
    }
}
