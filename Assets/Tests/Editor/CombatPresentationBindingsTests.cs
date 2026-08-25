using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Combat;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// The cue-id -> asset binding layer. The point of these tests is that a missing binding is
    /// otherwise INVISIBLE - it shows up as silence or a missing effect in a build, not as an
    /// error. So the load-bearing assertion is that every cue CombatPresentation can actually emit
    /// has a binding, checked against the sequences themselves rather than a hand-copied list.
    ///
    /// No audio or VFX assets exist yet, so every resolve legitimately returns null here. That is
    /// the expected state, and the sinks must degrade to silence rather than throw.
    /// </summary>
    public class CombatPresentationBindingsTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private static readonly CombatPresentationSubject[] AllSubjects =
        {
            CombatPresentationSubject.BasicCardAttack,
            CombatPresentationSubject.DamageSpell,
            CombatPresentationSubject.HealSpell,
            CombatPresentationSubject.AvatarStrike,
        };

        /// <summary>Every cue id any sequence can actually emit, derived from the sequences rather
        /// than restated - so a new cue added to a beat cannot silently escape the check.</summary>
        private static IEnumerable<string> EveryEmittableCueId() =>
            AllSubjects
                .SelectMany(CombatPresentation.SequenceFor)
                .SelectMany(c => c.AudioCueIds)
                .Where(id => !string.IsNullOrEmpty(id))
                .Distinct();

        [Test]
        public void EveryCueTheGameCanEmit_HasAnAudioBinding()
        {
            List<string> unbound = EveryEmittableCueId()
                .Where(id => !CombatPresentationAssetMap.HasAudioBinding(id))
                .ToList();

            CollectionAssert.IsEmpty(unbound,
                "These cue ids are emitted by a beat sequence but have no asset binding, so they would " +
                "be silent in a build with no error: " + string.Join(", ", unbound));
        }

        [Test]
        public void EveryBoundCue_ResolvesToAStablePathUnderTheAudioRoot()
        {
            foreach (string cueId in CombatPresentationAssetMap.KnownAudioCueIds)
            {
                string path = CombatPresentationAssetMap.AudioPathFor(cueId);
                Assert.IsNotNull(path, cueId + " must map to a path.");
                StringAssert.StartsWith(CombatPresentationAssetMap.AudioRoot, path);
                Assert.IsFalse(path.EndsWith("/"), cueId + " maps to a directory, not a file.");
            }
        }

        [Test]
        public void AudioPaths_AreUniquePerCue_SoTwoCuesCannotCollideOnOneAsset()
        {
            List<string> paths = CombatPresentationAssetMap.KnownAudioCueIds
                .Select(CombatPresentationAssetMap.AudioPathFor).ToList();

            CollectionAssert.AllItemsAreUnique(paths,
                "Two cues sharing one asset path would make them indistinguishable in play.");
        }

        [Test]
        public void AnUnknownCueId_ResolvesToNull_RatherThanGuessingAPath()
        {
            Assert.IsNull(CombatPresentationAssetMap.AudioPathFor("not.a.real.cue"));
            Assert.IsNull(CombatPresentationAssetMap.AudioPathFor(null));
            Assert.IsNull(CombatPresentationAssetMap.AudioPathFor(""));
        }

        [Test]
        public void ParticlePaths_CoverEveryPaletteAndTierThatCanActuallyOccur()
        {
            foreach (CombatPresentationPalette palette in new[]
                     { CombatPresentationPalette.Andras, CombatPresentationPalette.Ktini,
                       CombatPresentationPalette.Pnevmas, CombatPresentationPalette.Bespoke })
                foreach (CombatPresentationVisualTier tier in new[]
                         { CombatPresentationVisualTier.Light, CombatPresentationVisualTier.Medium,
                           CombatPresentationVisualTier.Heavy })
                {
                    string path = CombatPresentationAssetMap.ParticlePathFor(palette, tier);
                    Assert.IsNotNull(path, palette + "/" + tier + " must map to a path.");
                    StringAssert.StartsWith(CombatPresentationAssetMap.ParticleRoot, path);
                }
        }

        [Test]
        public void ParticlePaths_AreUniquePerPaletteTierPair()
        {
            var paths = new List<string>();
            foreach (CombatPresentationPalette palette in new[]
                     { CombatPresentationPalette.Andras, CombatPresentationPalette.Ktini,
                       CombatPresentationPalette.Pnevmas, CombatPresentationPalette.Bespoke })
                foreach (CombatPresentationVisualTier tier in new[]
                         { CombatPresentationVisualTier.Light, CombatPresentationVisualTier.Medium,
                           CombatPresentationVisualTier.Heavy })
                    paths.Add(CombatPresentationAssetMap.ParticlePathFor(palette, tier));

            CollectionAssert.AllItemsAreUnique(paths);
        }

        [Test]
        public void NoParticlePath_IsInventedForNoneValues()
        {
            Assert.IsNull(CombatPresentationAssetMap.ParticlePathFor(
                CombatPresentationPalette.None, CombatPresentationVisualTier.Medium));
            Assert.IsNull(CombatPresentationAssetMap.ParticlePathFor(
                CombatPresentationPalette.Andras, CombatPresentationVisualTier.None));
        }

        // ---------- sinks must degrade to nothing, not throw ----------

        [Test]
        public void TheAudioSink_IsSilentAndSafe_WhileNoAssetsExist()
        {
            var sink = new ResourcesCombatAudioSink(null);

            foreach (string cueId in EveryEmittableCueId())
                Assert.DoesNotThrow(() => sink.Play(cueId),
                    "A missing clip must be silence, never an exception mid-combat.");
        }

        [Test]
        public void TheAudioSink_RecordsWhichCuesHadNoAsset_SoSilenceIsNoticeable()
        {
            var sink = new ResourcesCombatAudioSink(null);
            sink.Play(CombatPresentation.CueImpact);
            sink.Play(CombatPresentation.CueImpact);

            // No assets exist yet, so this is the expected state - the value is that it is REPORTED
            // rather than silently swallowed, and not duplicated per call.
            CollectionAssert.Contains(sink.UnresolvedCueIds, CombatPresentation.CueImpact);
            Assert.AreEqual(1, sink.UnresolvedCueIds.Count(id => id == CombatPresentation.CueImpact),
                "An unresolved cue should be reported once, not once per play.");
        }

        [Test]
        public void TheAudioSink_IgnoresEmptyCueIdsWithoutRecordingThem()
        {
            var sink = new ResourcesCombatAudioSink(null);
            sink.Play(null);
            sink.Play("");

            CollectionAssert.IsEmpty(sink.UnresolvedCueIds, "An empty cue is not a missing asset.");
        }

        [Test]
        public void TheParticleSink_IsSafeAndSpawnsNothing_WhileNoAssetsExist()
        {
            var host = new GameObject("ParticleSinkHost");
            _spawned.Add(host);
            var sink = new ResourcesCombatParticleSink(host.transform);

            Assert.DoesNotThrow(() => sink.Emit(CombatPresentationPalette.Andras,
                CombatPresentationVisualTier.Medium, laneIndex: 1));
            CollectionAssert.IsEmpty(sink.Spawned, "Nothing to instantiate yet, so nothing should appear.");
        }

        [Test]
        public void TheParticleSink_NeverEmitsForNoneValues()
        {
            var host = new GameObject("ParticleSinkHostNone");
            _spawned.Add(host);
            var sink = new ResourcesCombatParticleSink(host.transform);

            sink.Emit(CombatPresentationPalette.None, CombatPresentationVisualTier.Medium, 0);
            sink.Emit(CombatPresentationPalette.Andras, CombatPresentationVisualTier.None, 0);

            CollectionAssert.IsEmpty(sink.Spawned);
            CollectionAssert.IsEmpty(sink.UnresolvedParticlePaths,
                "A deliberate None is not a missing asset and must not be reported as one.");
        }

        [Test]
        public void AFullPlayback_DrivesTheRealSinks_WithoutThrowing()
        {
            var host = new GameObject("PlaybackHost");
            _spawned.Add(host);
            var audio = new ResourcesCombatAudioSink(null);
            var particles = new ResourcesCombatParticleSink(host.transform);

            foreach (CombatPresentationSubject subject in AllSubjects)
            {
                var playback = new CombatPresentationPlayback(subject);
                Assert.DoesNotThrow(() => playback.PlayToEnd(
                    CombatPresentation.PaletteFor(subject, CombatPresentationPalette.Andras),
                    laneIndex: 0, particles: particles, presentationRoot: null, audio: audio),
                    subject + " must drive the real sinks safely with no assets present.");
            }
        }
    }
}
