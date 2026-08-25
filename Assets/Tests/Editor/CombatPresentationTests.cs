using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Combat;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Vertical-slice presentation scaffolding. Asserts the RELATIONSHIPS the spec actually locks
    /// (AvatarStrike is the longest, basic attacks have no particles, fast-forward lands on
    /// Resolve) rather than pinning the draft millisecond values - the register calls those "a
    /// starting point for GPT to refine, not final numbers", so hardcoding them would turn a
    /// legitimate retune into a fake regression. That is the same non-negotiable that governs the
    /// balance suites.
    /// </summary>
    public class CombatPresentationTests
    {
        private static readonly CombatPresentationSubject[] AllSubjects =
        {
            CombatPresentationSubject.BasicCardAttack,
            CombatPresentationSubject.DamageSpell,
            CombatPresentationSubject.HealSpell,
            CombatPresentationSubject.AvatarStrike,
        };

        [Test]
        public void EverySubject_HasANonEmptySequence_WithPositiveBeatDurations()
        {
            foreach (CombatPresentationSubject subject in AllSubjects)
            {
                IReadOnlyList<CombatPresentationCue> seq = CombatPresentation.SequenceFor(subject);
                Assert.IsNotEmpty((ICollection)seq.ToList(), subject + " must have a beat sequence.");
                foreach (CombatPresentationCue cue in seq)
                    Assert.Greater(cue.DurationMs, 0, subject + "/" + cue.Beat + " must have a real duration.");
            }
        }

        [Test]
        public void AvatarStrike_IsTheLongestSequence_BecauseItIsTheCommitmentSpell()
        {
            int avatarStrike = CombatPresentation.TotalDurationMs(CombatPresentationSubject.AvatarStrike);

            foreach (CombatPresentationSubject other in AllSubjects.Where(s => s != CombatPresentationSubject.AvatarStrike))
            {
                Assert.Greater(avatarStrike, CombatPresentation.TotalDurationMs(other),
                    "AvatarStrike is deliberately the longest beat sequence - it is the commitment spell.");
            }
        }

        [Test]
        public void ABasicAttack_IsTheShortestSequence_AndNeverBlocksLong()
        {
            int basic = CombatPresentation.TotalDurationMs(CombatPresentationSubject.BasicCardAttack);

            foreach (CombatPresentationSubject other in AllSubjects.Where(s => s != CombatPresentationSubject.BasicCardAttack))
            {
                Assert.Less(basic, CombatPresentation.TotalDurationMs(other),
                    "The most frequent action must be the fastest, or ordinary play feels sluggish.");
            }
        }

        [Test]
        public void ABasicAttack_HasNoParticles_AndNoCameraZoom()
        {
            foreach (CombatPresentationCue cue in CombatPresentation.SequenceFor(CombatPresentationSubject.BasicCardAttack))
            {
                Assert.AreEqual(CombatPresentationParticleTier.None, cue.ParticleTier,
                    "Spec: basic attack is 'just the sprite clash', no particles.");
                Assert.AreEqual(1f, cue.CameraZoom,
                    "Spec: basic attack takes no camera move - impact micro-shake only.");
            }
        }

        [Test]
        public void ABasicAttack_ShakesOnlyOnImpact()
        {
            List<CombatPresentationCue> shaking = CombatPresentation
                .SequenceFor(CombatPresentationSubject.BasicCardAttack)
                .Where(c => c.CameraShakePixels > 0f).ToList();

            Assert.AreEqual(1, shaking.Count, "Exactly one beat may shake.");
            Assert.AreEqual(CombatPresentationBeat.Impact, shaking[0].Beat, "...and it must be Impact.");
        }

        [Test]
        public void EveryShake_FitsInsideItsOwnBeat()
        {
            foreach (CombatPresentationSubject subject in AllSubjects)
                foreach (CombatPresentationCue cue in CombatPresentation.SequenceFor(subject))
                    Assert.LessOrEqual(cue.CameraShakeMs, cue.DurationMs,
                        subject + "/" + cue.Beat + ": a shake outrunning its beat would bleed into the next one.");
        }

        [Test]
        public void AvatarStrike_ShakesHarderThanABasicAttack()
        {
            float strike = CombatPresentation.SequenceFor(CombatPresentationSubject.AvatarStrike).Max(c => c.CameraShakePixels);
            float basic = CombatPresentation.SequenceFor(CombatPresentationSubject.BasicCardAttack).Max(c => c.CameraShakePixels);

            Assert.Greater(strike, basic, "AvatarStrike's Release is the strongest impact in the game.");
        }

        [Test]
        public void AvatarStrike_UsesItsOwnStinger_NotAReusedGenericHit()
        {
            List<string> cues = CombatPresentation.SequenceFor(CombatPresentationSubject.AvatarStrike)
                .Select(c => c.AudioCueId).Where(id => !string.IsNullOrEmpty(id)).ToList();

            Assert.Contains(CombatPresentation.CueAvatarStrikeStinger, cues,
                "Spec: a distinct signature stinger on Release, explicitly not a reused generic hit sound.");
            CollectionAssert.DoesNotContain(cues, CombatPresentation.CueImpactHit);
            CollectionAssert.DoesNotContain(cues, CombatPresentation.CueImpactThud);
        }

        [Test]
        public void AvatarStrike_PaletteIsBespoke_RegardlessOfCasterSchool()
        {
            foreach (CombatPresentationPalette school in new[]
                     { CombatPresentationPalette.Andras, CombatPresentationPalette.Ktini, CombatPresentationPalette.Pnevmas })
            {
                Assert.AreEqual(CombatPresentationPalette.Bespoke,
                    CombatPresentation.PaletteFor(CombatPresentationSubject.AvatarStrike, school),
                    "Locked: AvatarStrike is bespoke, never reused from an effect-type template.");
            }
        }

        [Test]
        public void ASpell_TakesTheCastersSchoolPalette()
        {
            Assert.AreEqual(CombatPresentationPalette.Ktini,
                CombatPresentation.PaletteFor(CombatPresentationSubject.DamageSpell, CombatPresentationPalette.Ktini));
            Assert.AreEqual(CombatPresentationPalette.None,
                CombatPresentation.PaletteFor(CombatPresentationSubject.BasicCardAttack, CombatPresentationPalette.Ktini),
                "A basic attack has no particles, so no palette applies.");
        }

        [Test]
        public void ResolveOutcome_SelectsChimeOrLowTone()
        {
            string positive = CombatPresentation.FinalBeatFor(CombatPresentationSubject.DamageSpell, outcomeIsPositive: true).AudioCueId;
            string negative = CombatPresentation.FinalBeatFor(CombatPresentationSubject.DamageSpell, outcomeIsPositive: false).AudioCueId;

            Assert.AreEqual(CombatPresentation.CueResolveChime, positive);
            Assert.AreEqual(CombatPresentation.CueResolveLowTone, negative);
            Assert.AreNotEqual(positive, negative, "A win and a loss must not sound identical.");
        }

        [Test]
        public void FastForward_LandsOnTheFinalBeatOfTheSequence()
        {
            foreach (CombatPresentationSubject subject in AllSubjects)
            {
                CombatPresentationCue last = CombatPresentation.FinalBeatFor(subject);
                IReadOnlyList<CombatPresentationCue> seq = CombatPresentation.SequenceFor(subject);

                Assert.AreEqual(seq[seq.Count - 1].Beat, last.Beat,
                    "Spec: a second input jumps straight to Resolve's end state - no animation blocks the next decision.");
            }
        }

        // ---------- sink dispatch ----------

        private sealed class RecordingSink : ICombatParticleSink, ICombatCameraSink, ICombatAudioSink
        {
            public readonly List<string> Audio = new List<string>();
            public int ParticleEmits;
            public int CameraApplies;

            public void Emit(CombatPresentationPalette palette, CombatPresentationParticleTier tier, int laneIndex) => ParticleEmits++;
            public void Apply(float zoom, float shakePixels, int shakeMs, int laneIndex) => CameraApplies++;
            public void Play(string audioCueId) => Audio.Add(audioCueId);
        }

        [Test]
        public void FiringABeat_WithNoSinksBound_DoesNotThrow()
        {
            foreach (CombatPresentationSubject subject in AllSubjects)
                foreach (CombatPresentationCue cue in CombatPresentation.SequenceFor(subject))
                    Assert.DoesNotThrow(() => CombatPresentation.FireBeat(cue, CombatPresentationPalette.Andras, 0),
                        "The scaffolding must be callable before any asset or sink exists - that is its whole point.");
        }

        [Test]
        public void FiringABeat_DispatchesOnlyTheCuesThatBeatActuallyHas()
        {
            var sink = new RecordingSink();
            CombatPresentationCue impact = CombatPresentation.SequenceFor(CombatPresentationSubject.DamageSpell)
                .First(c => c.Beat == CombatPresentationBeat.Impact);

            CombatPresentation.FireBeat(impact, CombatPresentationPalette.Andras, laneIndex: 1, sink, sink, sink);

            Assert.AreEqual(1, sink.Audio.Count, "Impact has an audio cue.");
            Assert.AreEqual(1, sink.ParticleEmits, "Impact has a particle tier.");
            Assert.AreEqual(1, sink.CameraApplies, "Impact zooms.");
        }

        [Test]
        public void ABeatWithNoParticleTier_NeverEmitsParticles()
        {
            var sink = new RecordingSink();
            foreach (CombatPresentationCue cue in CombatPresentation.SequenceFor(CombatPresentationSubject.BasicCardAttack))
                CombatPresentation.FireBeat(cue, CombatPresentationPalette.None, 0, sink, sink, sink);

            Assert.AreEqual(0, sink.ParticleEmits, "A basic attack must never spawn particles.");
            Assert.Greater(sink.Audio.Count, 0, "...but it still has audio cues.");
        }
    }
}
