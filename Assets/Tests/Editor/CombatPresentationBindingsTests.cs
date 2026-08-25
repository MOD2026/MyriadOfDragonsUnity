using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Combat;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// The cue-id -> asset binding layer. The point of these tests is that a missing binding is
    /// otherwise INVISIBLE - it shows up as silence or a missing effect in a build, not as an
    /// error. So the load-bearing assertion is that every cue CombatPresentation can actually emit
    /// has a binding, checked against the sequences themselves rather than a hand-copied list.
    ///
    /// Real audio assets landed 2026-08-25 (Resources/Audio/Combat/) and real VFX prefabs landed
    /// the same day (Resources/VFX/Combat/ - andras_medium/ktini_medium/pnevmas_medium share one
    /// static RawImage texture, bespoke_heavy shows a single cropped frame of the AvatarStrike
    /// sheet). Genuine per-frame flipbook animation for AvatarStrike is NOT built yet - that is a
    /// real follow-up, not silently claimed done here. Tests below that used to rely on "nothing
    /// resolves" now use ids/combos that were never given a binding, so they keep testing the
    /// missing-asset path without depending on any real cue/combo staying unwired forever.
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
            // Real .wav assets landed under Resources/Audio/Combat/ on 2026-08-25 for every cue
            // CombatPresentation emits, so this deliberately uses a cue id with NO binding at all
            // (rather than a real cue like CueImpact, which now resolves) to keep testing the
            // missing-asset path without depending on any real cue staying unwired forever.
            const string cueWithNoBinding = "combat.no_such_cue_for_test";
            Assert.IsFalse(CombatPresentationAssetMap.HasAudioBinding(cueWithNoBinding),
                "Setup: this id must not collide with a real binding.");

            var sink = new ResourcesCombatAudioSink(null);
            sink.Play(cueWithNoBinding);
            sink.Play(cueWithNoBinding);

            // The value is that it is REPORTED rather than silently swallowed, and not duplicated
            // per call.
            CollectionAssert.Contains(sink.UnresolvedCueIds, cueWithNoBinding);
            Assert.AreEqual(1, sink.UnresolvedCueIds.Count(id => id == cueWithNoBinding),
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
        public void TheParticleSink_IsSafeAndSpawnsNothing_ForACombinationWithNoAsset()
        {
            // Andras/Medium now has a real prefab (2026-08-25) - Light tier was never part of the
            // vertical slice and has no binding for any palette, so it still exercises the
            // missing-asset path without depending on Andras/Medium staying unwired forever.
            var host = new GameObject("ParticleSinkHost");
            _spawned.Add(host);
            var sink = new ResourcesCombatParticleSink(host.transform);

            Assert.DoesNotThrow(() => sink.Emit(CombatPresentationPalette.Andras,
                CombatPresentationVisualTier.Light, laneIndex: 1));
            CollectionAssert.IsEmpty(sink.Spawned, "Nothing to instantiate yet, so nothing should appear.");
        }

        [Test]
        public void TheParticleSink_ResolvesRealPrefabs_ForEverySchoolAtMediumTierAndAvatarStrikeBespoke()
        {
            var host = new GameObject("ParticleSinkRealAssetHost");
            _spawned.Add(host);
            var sink = new ResourcesCombatParticleSink(host.transform);

            foreach (CombatPresentationPalette school in new[]
                     {
                         CombatPresentationPalette.Andras, CombatPresentationPalette.Ktini,
                         CombatPresentationPalette.Pnevmas,
                     })
            {
                GameObject prefab = sink.Resolve(school, CombatPresentationVisualTier.Medium);
                Assert.IsNotNull(prefab, $"{school}/Medium should resolve to the real shared particle prefab.");
                Assert.IsNotNull(prefab.GetComponent<RawImage>(), $"{school}/Medium prefab must carry a RawImage.");
            }

            GameObject bespoke = sink.Resolve(CombatPresentationPalette.Bespoke, CombatPresentationVisualTier.Heavy);
            Assert.IsNotNull(bespoke, "AvatarStrike's bespoke_heavy prefab should resolve.");
            RawImage bespokeImage = bespoke.GetComponent<RawImage>();
            Assert.IsNotNull(bespokeImage, "bespoke_heavy prefab must carry a RawImage.");
            Assert.IsNotNull(bespokeImage.texture, "bespoke_heavy must reference the real flipbook sheet texture.");
        }

        /// <summary>Real per-frame flipbook animation follow-up (was flagged as not-yet-built in
        /// this class' own doc comment) - bespoke_heavy must now carry a real FlipbookRawImagePlayer
        /// wired to the real 4x4/16-frame sheet, referencing the SAME RawImage the prefab already
        /// has, not a duplicate. Configured to match CombatPresentation.AvatarStrike's own Release
        /// beat duration (150ms) - the beat that actually spawns this particle.</summary>
        [Test]
        public void TheParticleSink_BespokeHeavyPrefab_CarriesARealWiredFlipbookPlayer()
        {
            var host = new GameObject("ParticleSinkFlipbookHost");
            _spawned.Add(host);
            var sink = new ResourcesCombatParticleSink(host.transform);

            GameObject bespoke = sink.Resolve(CombatPresentationPalette.Bespoke, CombatPresentationVisualTier.Heavy);
            Assert.IsNotNull(bespoke, "AvatarStrike's bespoke_heavy prefab should resolve.");

            RawImage bespokeImage = bespoke.GetComponent<RawImage>();
            FlipbookRawImagePlayer player = bespoke.GetComponent<FlipbookRawImagePlayer>();
            Assert.IsNotNull(player, "bespoke_heavy must carry a FlipbookRawImagePlayer for real per-frame animation.");
            Assert.AreSame(bespokeImage, player.Image, "FlipbookRawImagePlayer must reference the prefab's own RawImage, not a separate/missing one.");
            Assert.AreEqual(4, player.Columns, "AvatarStrike's real sheet is 4 columns.");
            Assert.AreEqual(4, player.Rows, "AvatarStrike's real sheet is 4 rows.");
            Assert.AreEqual(150f, player.DurationMs, 0.01f,
                "Configured duration should match CombatPresentation.AvatarStrike's own Release beat (150ms) - the real referenced value, not an invented one.");

            // Real cross-check against CombatPresentation.cs itself, not a hand-copied constant -
            // if the Release beat's duration ever changes, this test fails loudly instead of the
            // flipbook silently drifting out of sync with the beat that actually spawns it.
            CombatPresentationCue releaseBeat = CombatPresentation.SequenceFor(CombatPresentationSubject.AvatarStrike)
                .First(c => c.Beat == CombatPresentationBeat.Release);
            Assert.AreEqual(releaseBeat.DurationMs, player.DurationMs, 0.01f,
                "FlipbookRawImagePlayer's durationMs must stay in sync with CombatPresentation's real Release beat duration.");
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
    
        // ------------------------------------------------------------------ do the assets LOAD?
        //
        // Everything above tests the PATH STRING. A correct path is not a loadable asset, and this
        // project has now been bitten by that distinction three times in one session: the .opus
        // audio (real files, correct paths, Resources.Load returns null because the codec is not
        // importable), the Empire structure tiles (renders existed, nothing loaded them), and the
        // puzzle art roles (a reserved path that was never applied). ee95210 hand-authored these
        // four prefabs and verified the RawImage script GUID against the installed package - which
        // is the right check and still not the same as Unity importing the prefab.

        /// <summary>The four combinations ee95210 actually delivered. Others intentionally have no
        /// prefab: a missing particle must be silence, not an exception.</summary>
        private static readonly (CombatPresentationPalette palette, CombatPresentationVisualTier tier)[]
            DeliveredParticles =
            {
                (CombatPresentationPalette.Andras, CombatPresentationVisualTier.Medium),
                (CombatPresentationPalette.Ktini, CombatPresentationVisualTier.Medium),
                (CombatPresentationPalette.Pnevmas, CombatPresentationVisualTier.Medium),
                (CombatPresentationPalette.Bespoke, CombatPresentationVisualTier.Heavy),
            };

        [Test]
        public void EveryDeliveredParticlePrefab_ActuallyLoads()
        {
            var missing = new List<string>();
            foreach (var (palette, tier) in DeliveredParticles)
            {
                string path = CombatPresentationAssetMap.ParticlePathFor(palette, tier);
                Assert.IsFalse(string.IsNullOrEmpty(path), palette + "/" + tier + " has no path.");
                if (Resources.Load<GameObject>(path) == null) missing.Add(path);
            }

            CollectionAssert.IsEmpty(missing,
                "These particle prefabs exist on disk but Resources.Load returns null - a " +
                "hand-authored prefab that Unity cannot import looks exactly like a correct one in " +
                "the file listing: " + string.Join(", ", missing));
        }

        [Test]
        public void AnUndeliveredCombination_LoadsAsNull_AndThatIsNotAFailure()
        {
            // Guards the test above from being read as "every combination must have art". Light and
            // Heavy tiers for the three schools were never authored, and the sink is null-tolerant
            // by design - a missing particle is no VFX, never an exception mid-combat.
            string path = CombatPresentationAssetMap.ParticlePathFor(
                CombatPresentationPalette.Andras, CombatPresentationVisualTier.Light);

            Assert.IsFalse(string.IsNullOrEmpty(path), "A path is still produced for undelivered art.");
            Assert.IsNull(Resources.Load<GameObject>(path),
                "Setup: andras_light is not authored yet. If it now exists, add it to " +
                "DeliveredParticles so it is load-checked too.");
        }
}
}
