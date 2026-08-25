using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Combat;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Vertical-slice presentation scaffolding, against the LOCKED spec (register:
    /// "Vertical-slice spec REFINED and LOCKED", 2026-08-25).
    ///
    /// Timing is asserted as the locked RANGES (350-400 / 600-700 / 800-1000ms) rather than exact
    /// values - the ranges are the contract, the specific numbers inside them are tuning. Pinning a
    /// tunable is what turns a legitimate retune into a fake regression.
    ///
    /// The idempotent-resolve tests are the safety-critical ones: a non-idempotent resolve would
    /// double-apply damage, healing or rewards the moment a player skips.
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

        private sealed class CountingResolve : ICombatResolveSink
        {
            public int Applied;
            public void ApplyResolve() => Applied++;
        }

        private sealed class RecordingSink : ICombatParticleSink, ICombatPresentationRootSink, ICombatAudioSink
        {
            public readonly List<string> Audio = new List<string>();
            public int ParticleEmits;
            public int RootApplies;
            public float MaxZoom = 1f;

            public void Emit(CombatPresentationPalette palette, CombatPresentationVisualTier tier, int laneIndex) => ParticleEmits++;

            public void Apply(float zoom, float shakeUnits, int shakeMs, int laneIndex)
            {
                RootApplies++;
                if (zoom > MaxZoom) MaxZoom = zoom;
            }

            public void Play(string audioCueId) => Audio.Add(audioCueId);
        }

        // ---------- locked timing ranges ----------

        [Test]
        public void BasicAttack_FallsInTheLocked350To400msRange()
        {
            int total = CombatPresentation.TotalDurationMs(CombatPresentationSubject.BasicCardAttack);
            Assert.GreaterOrEqual(total, 350);
            Assert.LessOrEqual(total, 400);
        }

        [Test]
        public void Spells_FallInTheLocked600To700msRange_ReducedFromTheDrafts800()
        {
            foreach (CombatPresentationSubject spell in new[] { CombatPresentationSubject.DamageSpell, CombatPresentationSubject.HealSpell })
            {
                int total = CombatPresentation.TotalDurationMs(spell);
                Assert.GreaterOrEqual(total, 600, spell + " must not drop below the locked floor.");
                Assert.LessOrEqual(total, 700, spell + " was reduced from 800ms for repeat-cast fatigue.");
            }
        }

        [Test]
        public void AvatarStrike_FallsInTheLocked800To1000msRange()
        {
            int total = CombatPresentation.TotalDurationMs(CombatPresentationSubject.AvatarStrike);
            Assert.GreaterOrEqual(total, 800);
            Assert.LessOrEqual(total, 1000);
        }

        [Test]
        public void NoPresentation_ExceedsTheOneSecondHardCeiling()
        {
            foreach (CombatPresentationSubject subject in AllSubjects)
                Assert.LessOrEqual(CombatPresentation.TotalDurationMs(subject), CombatPresentation.HardCeilingMs,
                    subject + " breaches the spec's hard ceiling of one second.");
        }

        [Test]
        public void AvatarStrike_IsTheLongest_AndABasicAttackTheShortest()
        {
            int strike = CombatPresentation.TotalDurationMs(CombatPresentationSubject.AvatarStrike);
            int basic = CombatPresentation.TotalDurationMs(CombatPresentationSubject.BasicCardAttack);

            foreach (CombatPresentationSubject other in AllSubjects.Where(s => s != CombatPresentationSubject.AvatarStrike))
                Assert.Greater(strike, CombatPresentation.TotalDurationMs(other), "AvatarStrike is the commitment spell.");
            foreach (CombatPresentationSubject other in AllSubjects.Where(s => s != CombatPresentationSubject.BasicCardAttack))
                Assert.Less(basic, CombatPresentation.TotalDurationMs(other), "The most frequent action must be the fastest.");
        }

        [Test]
        public void EveryBeat_HasAPositiveDuration_AndEveryShakeFitsInsideItsBeat()
        {
            foreach (CombatPresentationSubject subject in AllSubjects)
                foreach (CombatPresentationCue cue in CombatPresentation.SequenceFor(subject))
                {
                    Assert.Greater(cue.DurationMs, 0, subject + "/" + cue.Beat);
                    Assert.LessOrEqual(cue.CameraShakeMs, cue.DurationMs,
                        subject + "/" + cue.Beat + ": a shake outrunning its beat bleeds into the next one.");
                }
        }

        // ---------- locked camera ----------

        [Test]
        public void ABasicAttack_NeverZooms_AndShakesOnlyOnImpact_WithinLockedUnits()
        {
            List<CombatPresentationCue> seq = CombatPresentation.SequenceFor(CombatPresentationSubject.BasicCardAttack).ToList();

            foreach (CombatPresentationCue cue in seq)
                Assert.AreEqual(1f, cue.PresentationRootZoom, "Locked: basic attack takes no zoom.");

            List<CombatPresentationCue> shaking = seq.Where(c => c.CameraShakeUnits > 0f).ToList();
            Assert.AreEqual(1, shaking.Count);
            Assert.AreEqual(CombatPresentationBeat.Impact, shaking[0].Beat);
            Assert.GreaterOrEqual(shaking[0].CameraShakeUnits, 2f, "Locked range is 2-4 normalized units.");
            Assert.LessOrEqual(shaking[0].CameraShakeUnits, 4f, "Locked range is 2-4 normalized units.");
        }

        [Test]
        public void OrdinarySpells_StayInTheLocked1_04To1_06ZoomBand()
        {
            foreach (CombatPresentationSubject spell in new[] { CombatPresentationSubject.DamageSpell, CombatPresentationSubject.HealSpell })
                foreach (CombatPresentationCue cue in CombatPresentation.SequenceFor(spell).Where(c => c.PresentationRootZoom != 1f))
                {
                    Assert.GreaterOrEqual(cue.PresentationRootZoom, 1.04f, spell + "/" + cue.Beat);
                    Assert.LessOrEqual(cue.PresentationRootZoom, 1.06f, spell + "/" + cue.Beat);
                }
        }

        [Test]
        public void AvatarStrike_StaysInTheLocked1_08To1_12ZoomBand_NotTheDrafts1_15()
        {
            foreach (CombatPresentationCue cue in CombatPresentation.SequenceFor(CombatPresentationSubject.AvatarStrike)
                         .Where(c => c.PresentationRootZoom != 1f))
            {
                Assert.GreaterOrEqual(cue.PresentationRootZoom, 1.08f, cue.Beat.ToString());
                Assert.LessOrEqual(cue.PresentationRootZoom, 1.12f,
                    "1.15x was rejected for real clipping risk on varied 16:9 devices.");
            }
        }

        [Test]
        public void AvatarStrike_ShakesHarderThanABasicAttack()
        {
            float strike = CombatPresentation.SequenceFor(CombatPresentationSubject.AvatarStrike).Max(c => c.CameraShakeUnits);
            float basic = CombatPresentation.SequenceFor(CombatPresentationSubject.BasicCardAttack).Max(c => c.CameraShakeUnits);
            Assert.Greater(strike, basic);
        }

        // ---------- locked audio reduction ----------

        [Test]
        public void ABasicAttack_HasExactlyOneAudioCue_AndItIsImpact()
        {
            List<CombatPresentationCue> withAudio = CombatPresentation.SequenceFor(CombatPresentationSubject.BasicCardAttack)
                .Where(c => c.AudioCueIds.Length > 0).ToList();

            Assert.AreEqual(1, withAudio.Count, "Locked: basic attack is impact-only audio.");
            Assert.AreEqual(CombatPresentationBeat.Impact, withAudio[0].Beat);
        }

        [Test]
        public void ADamageSpell_HasCastAndImpact_ButNoResolveCue()
        {
            IReadOnlyList<CombatPresentationCue> seq = CombatPresentation.SequenceFor(CombatPresentationSubject.DamageSpell);
            List<string> cues = seq.SelectMany(c => c.AudioCueIds).ToList();

            CollectionAssert.Contains(cues, CombatPresentation.CueCast);
            CollectionAssert.Contains(cues, CombatPresentation.CueImpact);
            Assert.IsEmpty(seq.First(c => c.Beat == CombatPresentationBeat.Resolve).AudioCueIds,
                "Locked: the soft resolve cue is heal/buff ONLY.");
        }

        [Test]
        public void AHealSpell_MayCarryTheOptionalSoftResolveCue()
        {
            CombatPresentationCue resolve = CombatPresentation.SequenceFor(CombatPresentationSubject.HealSpell)
                .First(c => c.Beat == CombatPresentationBeat.Resolve);

            CollectionAssert.Contains(resolve.AudioCueIds, CombatPresentation.CueSoftResolve);
        }

        [Test]
        public void AvatarStrike_CarriesItsBespokeStinger_NotAGenericImpactSound()
        {
            List<string> cues = CombatPresentation.SequenceFor(CombatPresentationSubject.AvatarStrike)
                .SelectMany(c => c.AudioCueIds).ToList();

            CollectionAssert.Contains(cues, CombatPresentation.CueCommit);
            CollectionAssert.Contains(cues, CombatPresentation.CueAvatarStrikeStinger);
            CollectionAssert.DoesNotContain(cues, CombatPresentation.CueImpact,
                "AvatarStrike must not reuse the generic impact sound.");
        }

        // ---------- palette / visual tier ----------

        [Test]
        public void AvatarStrike_PaletteIsBespoke_RegardlessOfCasterSchool()
        {
            foreach (CombatPresentationPalette school in new[]
                     { CombatPresentationPalette.Andras, CombatPresentationPalette.Ktini, CombatPresentationPalette.Pnevmas })
                Assert.AreEqual(CombatPresentationPalette.Bespoke,
                    CombatPresentation.PaletteFor(CombatPresentationSubject.AvatarStrike, school));
        }

        [Test]
        public void ABasicAttack_HasNoParticlesAtAll()
        {
            foreach (CombatPresentationCue cue in CombatPresentation.SequenceFor(CombatPresentationSubject.BasicCardAttack))
                Assert.AreEqual(CombatPresentationVisualTier.None, cue.VisualTier,
                    "Locked: simple sprites/tweens only - no particle burst.");
            Assert.AreEqual(CombatPresentationPalette.None,
                CombatPresentation.PaletteFor(CombatPresentationSubject.BasicCardAttack, CombatPresentationPalette.Ktini));
        }

        // ---------- SAFETY CRITICAL: idempotent resolve ----------

        [Test]
        public void PlayingThrough_AppliesTheOutcomeExactlyOnce()
        {
            var resolve = new CountingResolve();
            var playback = new CombatPresentationPlayback(CombatPresentationSubject.DamageSpell, resolve);

            playback.PlayToEnd();

            Assert.AreEqual(1, resolve.Applied);
            Assert.AreEqual(CombatPresentationEnd.PlayedThrough, playback.State);
        }

        [Test]
        public void SkippingMidSequence_AppliesTheOutcomeExactlyOnce()
        {
            var resolve = new CountingResolve();
            var playback = new CombatPresentationPlayback(CombatPresentationSubject.AvatarStrike, resolve);
            playback.AdvanceBeat();

            playback.SkipFromDedicatedControl();

            Assert.AreEqual(1, resolve.Applied, "A skip must apply the outcome, once.");
            Assert.AreEqual(CombatPresentationEnd.Skipped, playback.State);
        }

        [Test]
        public void TwoSkipInputsRacing_StillApplyTheOutcomeOnlyOnce()
        {
            var resolve = new CountingResolve();
            var playback = new CombatPresentationPlayback(CombatPresentationSubject.DamageSpell, resolve);

            playback.SkipFromDedicatedControl();
            playback.SkipFromDedicatedControl();
            playback.SkipFromDedicatedControl();

            Assert.AreEqual(1, resolve.Applied,
                "Locked: skipping can never duplicate damage, healing, SFX or rewards.");
        }

        [Test]
        public void SkippingThenPlayingOn_NeverDoubleApplies()
        {
            var resolve = new CountingResolve();
            var playback = new CombatPresentationPlayback(CombatPresentationSubject.HealSpell, resolve);
            playback.AdvanceBeat();

            playback.SkipFromDedicatedControl();
            playback.PlayToEnd();
            playback.AdvanceBeat();

            Assert.AreEqual(1, resolve.Applied, "Healing must never be applied twice.");
        }

        [Test]
        public void AfterASkip_NoFurtherBeatsPlay()
        {
            var playback = new CombatPresentationPlayback(CombatPresentationSubject.AvatarStrike);
            playback.SkipFromDedicatedControl();

            Assert.IsNull(playback.AdvanceBeat(), "A skipped presentation must not keep animating.");
        }

        [Test]
        public void ResolveApplied_NeverRevertsToFalse()
        {
            var playback = new CombatPresentationPlayback(CombatPresentationSubject.DamageSpell, new CountingResolve());
            playback.SkipFromDedicatedControl();
            Assert.IsTrue(playback.ResolveApplied);

            playback.PlayToEnd();
            Assert.IsTrue(playback.ResolveApplied);
        }

        [Test]
        public void APlaybackWithNoResolveSink_DoesNotThrow()
        {
            var playback = new CombatPresentationPlayback(CombatPresentationSubject.BasicCardAttack);
            Assert.DoesNotThrow(() => playback.PlayToEnd());
            Assert.DoesNotThrow(() => playback.SkipFromDedicatedControl());
        }

        // ---------- sink dispatch ----------

        [Test]
        public void FiringABeat_WithNoSinksBound_DoesNotThrow()
        {
            foreach (CombatPresentationSubject subject in AllSubjects)
                foreach (CombatPresentationCue cue in CombatPresentation.SequenceFor(subject))
                    Assert.DoesNotThrow(() => CombatPresentation.FireBeat(cue, CombatPresentationPalette.Andras, 0));
        }

        [Test]
        public void PlayingASpell_DispatchesAudioParticlesAndTheRootTween()
        {
            var sink = new RecordingSink();
            var playback = new CombatPresentationPlayback(CombatPresentationSubject.DamageSpell);

            playback.PlayToEnd(CombatPresentationPalette.Andras, 1, sink, sink, sink);

            Assert.AreEqual(2, sink.Audio.Count, "Cast + impact, no resolve cue for a damage spell.");
            Assert.AreEqual(1, sink.ParticleEmits, "One burst, at Impact.");
            Assert.Greater(sink.RootApplies, 0);
            Assert.LessOrEqual(sink.MaxZoom, 1.06f, "The root tween must stay inside the locked band.");
        }

        [Test]
        public void ABasicAttack_NeverEmitsParticles_ButStillPlaysItsImpactCue()
        {
            var sink = new RecordingSink();
            var playback = new CombatPresentationPlayback(CombatPresentationSubject.BasicCardAttack);

            playback.PlayToEnd(CombatPresentationPalette.None, 0, sink, sink, sink);

            Assert.AreEqual(0, sink.ParticleEmits);
            Assert.AreEqual(1, sink.Audio.Count);
        }
    }
}
