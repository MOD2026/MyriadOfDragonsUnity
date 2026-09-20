using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Combat;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Battle runtime presentation slice: beat timing, hit/critical/defeat classification, the
    /// data-driven 36-spell cast-impact resolver with its fallback tiers, the reduced-motion static
    /// hold, and the single teardown that keeps replay / retry / return-to-Empire free of stale
    /// overlays. Coroutines never run in EditMode (CLAUDE.md #6), so timing and classification are
    /// proven on the pure BattleBeatPolicy and the resulting scene state is proven directly; what a
    /// fade looks like frame-to-frame is a Play Mode concern this file does not claim.
    /// </summary>
    public class BattleBeatPolicyTests
    {
        private static readonly BattleBeat[] AllBeats = (BattleBeat[])Enum.GetValues(typeof(BattleBeat));
        private static readonly BattleBeat[] Informational =
            { BattleBeat.Hit, BattleBeat.CriticalHit, BattleBeat.Defeat, BattleBeat.SpellImpact };

        [Test]
        public void ReducedMotion_NoBeatEverAnimatesOrFlashes()
        {
            foreach (BattleBeat beat in AllBeats)
            {
                BattleBeatTiming t = BattleBeatPolicy.Resolve(beat, reducedMotion: true);
                Assert.IsFalse(t.Animated, $"{beat} must not animate under Reduced Motion.");
                Assert.AreEqual(0, t.AnimatedMs, $"{beat} must have no animated duration under Reduced Motion.");
                Assert.IsFalse(t.AllowsFlash, $"{beat} must never flash under Reduced Motion.");
            }
        }

        [Test]
        public void ReducedMotion_InformationalBeatsHoldStatically_TransitionsLandImmediately()
        {
            foreach (BattleBeat beat in AllBeats)
            {
                BattleBeatTiming t = BattleBeatPolicy.Resolve(beat, reducedMotion: true);
                if (Informational.Contains(beat))
                {
                    Assert.Greater(t.StaticHoldMs, 0, $"{beat} tells the player something happened - it must stay visible, not vanish.");
                    Assert.LessOrEqual(t.StaticHoldMs, BattleBeatPolicy.InformationalHoldMs);
                }
                else
                {
                    Assert.AreEqual(0, t.StaticHoldMs, $"{beat} is a pure transition - immediate, with nothing to hold.");
                }
            }
        }

        [Test]
        public void NormalMotion_EveryBeatIsShortAndBounded()
        {
            foreach (BattleBeat beat in AllBeats)
            {
                BattleBeatTiming t = BattleBeatPolicy.Resolve(beat, reducedMotion: false);
                Assert.IsTrue(t.Animated, $"{beat} should animate under normal motion.");
                Assert.Greater(t.AnimatedMs, 0, beat.ToString());
                Assert.LessOrEqual(t.AnimatedMs, BattleBeatPolicy.MaxAnimatedMs, $"{beat} must stay short and controlled.");
                Assert.AreEqual(0, t.StaticHoldMs, $"{beat}: the static hold is a Reduced-Motion-only value.");
            }
        }

        [Test]
        public void OnlyStartAndSpellBeatsMayFlash_UnderNormalMotion()
        {
            foreach (BattleBeat beat in AllBeats)
            {
                bool flashes = BattleBeatPolicy.Resolve(beat, false).AllowsFlash;
                bool expected = beat == BattleBeat.BattleStart || beat == BattleBeat.SpellImpact;
                Assert.AreEqual(expected, flashes, $"{beat} flash permission.");
            }
        }

        [TestCase(false, 0, false, 0, ClashCue.None)]
        [TestCase(false, 3, true, 9, ClashCue.None)]   // an uncontested lane has no cue even with stale flags
        [TestCase(true, 0, false, 0, ClashCue.Hit)]
        [TestCase(true, 0, true, 0, ClashCue.CriticalHit)]
        [TestCase(true, 0, false, 4, ClashCue.CriticalHit)]
        [TestCase(true, 1, false, 0, ClashCue.Defeat)]
        [TestCase(true, 2, true, 7, ClashCue.Defeat)]  // defeat outranks a critical hit
        public void ClassifyClash_FollowsThePrecedence(bool contested, int defeated, bool cleared, int overflow, ClashCue expected)
        {
            Assert.AreEqual(expected, BattleBeatPolicy.ClassifyClash(contested, defeated, cleared, overflow));
        }

        [Test]
        public void BeatFor_MapsEveryCueToItsBeat()
        {
            Assert.AreEqual(BattleBeat.Defeat, BattleBeatPolicy.BeatFor(ClashCue.Defeat));
            Assert.AreEqual(BattleBeat.CriticalHit, BattleBeatPolicy.BeatFor(ClashCue.CriticalHit));
            Assert.AreEqual(BattleBeat.Hit, BattleBeatPolicy.BeatFor(ClashCue.Hit));
        }
    }

    public class SpellVfxCatalogTests
    {
        [TearDown]
        public void TearDown() => SpellVfxCatalog.ResetForTests();

        private static Sprite MakeSprite(string name)
        {
            var texture = new Texture2D(4, 4) { name = name };
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 4, 4), Vector2.one * 0.5f);
            sprite.name = name;
            return sprite;
        }

        [Test]
        public void All36RealSpells_ResolveFromTheManifestRow_WithARealSprite_InBothMotionModes()
        {
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            Assert.AreEqual(36, catalog.Count, "Setup: the real catalog is 36 spells.");

            foreach (bool reduced in new[] { false, true })
            {
                foreach (AvatarSpell spell in catalog)
                {
                    SpellVfxCatalog.Resolution r = SpellVfxCatalog.Resolve(spell, reduced);
                    Assert.IsNotNull(r.Sprite, $"{spell.Id} (reduced={reduced}) resolved no sprite at all.");
                    Assert.AreEqual(SpellVfxCatalog.Tier.ManifestRow, r.Tier,
                        $"{spell.Id} (reduced={reduced}) should resolve from its own manifest row, not a fallback ({r.AssetPath}).");
                    Assert.IsNotEmpty(r.Family, spell.Id);
                    Assert.AreEqual(reduced, r.AssetPath.EndsWith(SpellVfxCatalog.StaticSuffix),
                        $"{spell.Id}: reduced-motion must use the _Static asset and normal motion must not.");
                }
            }
        }

        [Test]
        public void Firestorm_ResolvesItsOwnFamily_DistinctFromGenericLaneDamage()
        {
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            SpellVfxCatalog.Resolution fire = SpellVfxCatalog.Resolve(catalog.First(s => s.Id == "firestorm"), false);
            SpellVfxCatalog.Resolution other = SpellVfxCatalog.Resolve(
                catalog.First(s => s.Effect == SpellEffect.LaneDamage && s.Id != "firestorm"), false);

            Assert.AreEqual("FirestormImpact", fire.Family);
            Assert.AreNotEqual(fire.AssetPath, other.AssetPath);
        }

        [Test]
        public void EveryEffectFamily_HasARealNormalAndStaticAsset()
        {
            foreach (SpellEffect effect in Enum.GetValues(typeof(SpellEffect)))
            {
                string baseName = SpellVfxCatalog.FamilyAssetBaseName(effect);
                Assert.IsNotNull(Resources.Load<Sprite>(SpellVfxCatalog.VfxResourceRoot + baseName), $"{effect}: normal asset {baseName} missing.");
                Assert.IsNotNull(Resources.Load<Sprite>(SpellVfxCatalog.VfxResourceRoot + baseName + SpellVfxCatalog.StaticSuffix),
                    $"{effect}: static asset {baseName}{SpellVfxCatalog.StaticSuffix} missing.");
            }
        }

        [Test]
        public void SpellMissingFromTheManifest_FallsBackToItsEffectFamily_AndSaysSo()
        {
            SpellVfxCatalog.SetManifestJsonForTests("{\"spells\":[]}");
            AvatarSpell spell = AvatarSpell.CreateCatalog().First(s => s.Effect == SpellEffect.LaneHeal);

            SpellVfxCatalog.Resolution r = SpellVfxCatalog.Resolve(spell, false);

            Assert.AreEqual(SpellVfxCatalog.Tier.EffectFamily, r.Tier);
            Assert.IsTrue(r.UsedFallback, "A fallback must be reported, never passed off as the manifest's own art.");
            Assert.IsNotNull(r.Sprite);
            StringAssert.EndsWith("Heal_Ring", r.AssetPath);
        }

        [Test]
        public void ManifestRowWhoseAssetFailsToLoad_FallsBackToTheEffectFamily()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().First(s => s.Id == "mend");
            SpellVfxCatalog.SetManifestJsonForTests(
                "{\"spells\":[{\"spellId\":\"mend\",\"family\":\"Heal\",\"normalMotionAsset\":\"UI/VFX/Does_Not_Exist\",\"reducedMotionAsset\":\"UI/VFX/Does_Not_Exist_Static\"}]}");

            SpellVfxCatalog.Resolution r = SpellVfxCatalog.Resolve(spell, false);

            Assert.AreEqual(SpellVfxCatalog.Tier.EffectFamily, r.Tier, "A dangling manifest path must degrade, not return null.");
            Assert.IsNotNull(r.Sprite);
        }

        [Test]
        public void WhenNothingElseLoads_TheGenericNeutralAssetIsUsed_AndNullSpellIsSafe()
        {
            Sprite generic = MakeSprite("generic");
            try
            {
                SpellVfxCatalog.SetManifestJsonForTests("{\"spells\":[]}");
                SpellVfxCatalog.SetSpriteLoaderForTests(path =>
                    path == SpellVfxCatalog.VfxResourceRoot + SpellVfxCatalog.GenericFallbackBaseName ? generic : null);

                AvatarSpell spell = AvatarSpell.CreateCatalog().First(s => s.Effect == SpellEffect.AvatarStrike);
                SpellVfxCatalog.Resolution r = SpellVfxCatalog.Resolve(spell, false);
                Assert.AreEqual(SpellVfxCatalog.Tier.Generic, r.Tier);
                Assert.AreSame(generic, r.Sprite);

                SpellVfxCatalog.Resolution nullSpell = default;
                Assert.DoesNotThrow(() => nullSpell = SpellVfxCatalog.Resolve(null, false), "A null spell must never throw.");
                Assert.AreEqual(SpellVfxCatalog.Tier.Generic, nullSpell.Tier);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(generic.texture);
                UnityEngine.Object.DestroyImmediate(generic);
            }
        }

        [Test]
        public void MalformedOrEmptyManifest_DoesNotThrow_AndStillResolvesEverySpell()
        {
            foreach (string bad in new[] { "", "not json", "{\"spells\":null}", "{}" })
            {
                SpellVfxCatalog.SetManifestJsonForTests(bad);
                foreach (AvatarSpell spell in AvatarSpell.CreateCatalog())
                {
                    SpellVfxCatalog.Resolution r = default;
                    Assert.DoesNotThrow(() => r = SpellVfxCatalog.Resolve(spell, true), bad);
                    Assert.IsNotNull(r.Sprite, $"{spell.Id} lost its effect entirely under manifest '{bad}'.");
                }
            }
        }
    }

    public class BattleRuntimePresentationTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private bool _reduceMotionBefore;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDBattleRuntimeAnim_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            _reduceMotionBefore = MotionPolicy.ReduceMotion;
            MotionPolicy.ReduceMotion = false;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) UnityEngine.Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            MotionPolicy.ReduceMotion = _reduceMotionBefore;
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        private GameBootstrap SpawnBootstrap(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawned in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
                foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                    if (candidate.name == spawned && !_spawned.Contains(candidate)) _spawned.Add(candidate);
            return bootstrap;
        }

        private static List<Image> EffectImages() =>
            (GameObject.Find("Canvas")?.GetComponentsInChildren<Image>(true) ?? new Image[0])
                .Where(i => i.gameObject.name == "Effect").ToList();

        [Test]
        public void ReducedMotion_CriticalHit_ShowsAStaticMarker_NotNothing_AndNeverScaledOrFaded()
        {
            MotionPolicy.ReduceMotion = true;
            GameBootstrap bootstrap = SpawnBootstrap("Anim_ReducedCrit");

            bootstrap.PlayClashCueForTests(ClashCue.CriticalHit, Lane.Front, false, false);

            List<Image> effects = EffectImages();
            Assert.AreEqual(2, effects.Count, "A lane break marks both sides of the lane.");
            foreach (Image effect in effects)
            {
                Assert.AreEqual(Vector3.one, effect.transform.localScale, "A held marker must not be scaled.");
                Assert.AreEqual(1f, effect.color.a, "A held marker must not be faded.");
                Assert.IsNotNull(effect.sprite);
            }
        }

        [Test]
        public void EachCue_PlacesTheRightMarkers_OnTheRightSides()
        {
            GameBootstrap bootstrap = SpawnBootstrap("Anim_Cues");

            bootstrap.PlayClashCueForTests(ClashCue.Hit, Lane.Middle, false, false);
            Assert.AreEqual(2, EffectImages().Count, "A plain hit shows on both sides of the lane.");
            bootstrap.CancelPresentationEffectsForTests();

            bootstrap.PlayClashCueForTests(ClashCue.Defeat, Lane.Middle, playerLostCards: true, enemyLostCards: false);
            Assert.AreEqual(1, EffectImages().Count, "A defeat marks only the side that lost cards.");
            bootstrap.CancelPresentationEffectsForTests();

            bootstrap.PlayClashCueForTests(ClashCue.Defeat, Lane.Middle, playerLostCards: true, enemyLostCards: true);
            Assert.AreEqual(2, EffectImages().Count);
        }

        [Test]
        public void CancelPresentationEffects_LeavesNoEffectObjectsBehind_InEditMode()
        {
            GameBootstrap bootstrap = SpawnBootstrap("Anim_Cancel");
            bootstrap.PlayClashCueForTests(ClashCue.CriticalHit, Lane.Back, false, false);
            Assert.Greater(bootstrap.PresentationObjectCountForTests, 0, "Setup: expected queued presentation.");

            bootstrap.CancelPresentationEffectsForTests();

            Assert.AreEqual(0, bootstrap.PresentationObjectCountForTests);
            Assert.IsEmpty(EffectImages(), "Cancelling must remove the effect GameObjects themselves, not just forget them.");
        }

        [TestCase(false)]
        [TestCase(true)]
        public void SpellCastImpact_UsesTheSpellsOwnAsset_AndItsStaticVariantUnderReducedMotion(bool reduced)
        {
            MethodInfo playCast = typeof(GameBootstrap).GetMethod("PlayCastImpact", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(playCast, "Expected GameBootstrap.PlayCastImpact.");
            AvatarSpell firestorm = AvatarSpell.CreateCatalog().First(s => s.Id == "firestorm");

            MotionPolicy.ReduceMotion = reduced;
            GameBootstrap bootstrap = SpawnBootstrap("Anim_Spell_" + reduced);
            playCast.Invoke(bootstrap, new object[] { firestorm, Lane.Middle });

            List<Image> effects = EffectImages();
            Assert.AreEqual(1, effects.Count, $"reduced={reduced}: a cast shows exactly one impact marker.");
            Assert.AreEqual(reduced ? "Firestorm_Impact_Static" : "Firestorm_Impact", effects[0].sprite.name,
                $"reduced={reduced}: wrong asset shown.");
        }

        [Test]
        public void SpellWithNoBespokeArt_StillShowsAFallbackMarker_NotSilence()
        {
            MethodInfo playCast = typeof(GameBootstrap).GetMethod("PlayCastImpact", BindingFlags.Instance | BindingFlags.NonPublic);
            GameBootstrap bootstrap = SpawnBootstrap("Anim_SpellFallback");
            try
            {
                SpellVfxCatalog.SetManifestJsonForTests("{\"spells\":[]}");
                AvatarSpell spell = AvatarSpell.CreateCatalog().First(s => s.Effect == SpellEffect.LaneShield);

                playCast.Invoke(bootstrap, new object[] { spell, Lane.Front });

                Assert.AreEqual(1, EffectImages().Count, "A spell the manifest does not list must still produce an impact.");
            }
            finally
            {
                SpellVfxCatalog.ResetForTests();
            }
        }

        [Test]
        public void ResetBattlePresentationState_RestoresAStrandedFade_AndClearsEffects()
        {
            GameBootstrap bootstrap = SpawnBootstrap("Anim_Reset");
            bootstrap.PlayFormationEnterBeatForTests();
            bootstrap.RevealResultOverlayForTests();
            Assert.IsNotNull(bootstrap.ResultOverlayGroupForTests, "Setup: the reveal must have created the overlay's fade group.");
            Assert.IsNotNull(bootstrap.BattleRootGroupForTests, "Setup: the formation-enter beat must have created the board's fade group.");
            bootstrap.ResultOverlayGroupForTests.alpha = 0f;   // a fade interrupted at its start
            bootstrap.BattleRootGroupForTests.alpha = 0f;
            bootstrap.PlayClashCueForTests(ClashCue.Hit, Lane.Front, false, false);

            bootstrap.ResetBattlePresentationStateForTests();

            Assert.AreEqual(1f, bootstrap.ResultOverlayAlphaForTests, "An interrupted reveal must not leave the result invisible next time.");
            Assert.AreEqual(1f, bootstrap.BattleRootAlphaForTests, "An interrupted formation-enter must not leave the board invisible.");
            Assert.AreEqual(0, bootstrap.PresentationObjectCountForTests);
            Assert.IsFalse(bootstrap.ResultRevealRunningForTests);
            Assert.IsFalse(bootstrap.FormationEnterRunningForTests);
            Assert.DoesNotThrow(() => bootstrap.ResetBattlePresentationStateForTests(), "Reset must be safe to call twice.");
        }

        [Test]
        public void ReducedMotionAndEditMode_ResultReveal_IsImmediatelyFullyVisible()
        {
            foreach (bool reduced in new[] { false, true })
            {
                MotionPolicy.ReduceMotion = reduced;
                GameBootstrap bootstrap = SpawnBootstrap("Anim_Reveal_" + reduced);
                bootstrap.RevealResultOverlayForTests();
                Assert.AreEqual(1f, bootstrap.ResultOverlayAlphaForTests, $"reduced={reduced}");
                Assert.IsFalse(bootstrap.ResultRevealRunningForTests, "No fade coroutine may be running in EditMode/reduced motion.");
            }
        }

        [Test]
        public void ReturnToEmpire_WhileTheOpeningCinematicIsStillUp_LeavesNoCinematicOverlay()
        {
            GameBootstrap bootstrap = SpawnBootstrap("Anim_ReturnCinematic");
            bootstrap.StartApprovedTutorialBattle();
            Assert.IsTrue(bootstrap.CinematicActiveForTests, "Setup: the opening cinematic must be up.");

            bootstrap.ReturnToCityForTests();

            Assert.IsFalse(bootstrap.CinematicActiveForTests, "Returning must cancel a live cinematic.");
            Assert.IsNull(GameObject.Find("Canvas")?.transform.Find("Chapter1Cinematic"),
                "The cinematic overlay must not stay parented under the hidden Battle canvas.");
        }

        [Test]
        public void TutorialRetry_ClearsQueuedEffects_AndRestoresTheBoard()
        {
            GameBootstrap bootstrap = SpawnBootstrap("Anim_Retry");
            bootstrap.StartApprovedTutorialBattle(showOpeningCinematic: false);
            bootstrap.PlayClashCueForTests(ClashCue.CriticalHit, Lane.Front, false, false);
            Assert.Greater(bootstrap.PresentationObjectCountForTests, 0, "Setup: expected queued presentation before retry.");

            bootstrap.RetryForTests();

            Assert.AreEqual(0, bootstrap.PresentationObjectCountForTests, "Retry must not carry the last fight's effects into the new one.");
            Assert.IsEmpty(EffectImages());
            Assert.AreEqual(1f, bootstrap.BattleRootAlphaForTests);
            Assert.IsFalse(bootstrap.ResultOverlayActiveForTests, "Retry must hide the result overlay.");
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase);
        }

        [Test]
        public void RepeatedReturnToEmpire_IsIdempotent_NoDoubleCleanupErrors()
        {
            GameBootstrap bootstrap = SpawnBootstrap("Anim_ReturnTwice");
            bootstrap.StartApprovedTutorialBattle(showOpeningCinematic: false);
            bootstrap.PlayClashCueForTests(ClashCue.Hit, Lane.Front, false, false);

            Assert.DoesNotThrow(() => { bootstrap.ReturnToCityForTests(); bootstrap.ReturnToCityForTests(); });
            Assert.AreEqual(0, bootstrap.PresentationObjectCountForTests);
        }
    }
}
