using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Battle runtime presentation gaps closed on top of the existing effect pipeline:
    /// reduced-motion still effects, a runtime fallback chain for missing spell art, a default
    /// placement effect for every element, the Start Battle banner, the result-overlay entrance
    /// and the visible Stamina-blocked retry message. Presentation only - no combat math, rewards,
    /// Save or contract member is touched or asserted here. (The Stamina-blocked retry message is asserted
    /// in CampaignStaminaEntryContractTests.RetryWithInsufficientStamina_BlocksTheRetry_WithoutStartingANewMatch.)
    /// </summary>
    public class BattlePresentationRuntimeTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private bool _reduceMotionBefore;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDPresentationRuntime_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            _reduceMotionBefore = MotionPolicy.ReduceMotion;
        }

        [TearDown]
        public void TearDown()
        {
            MotionPolicy.ReduceMotion = _reduceMotionBefore;
            foreach (GameObject go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        private GameBootstrap SpawnAndInitializeBootstrap(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (candidate.name == spawnedName && !_spawned.Contains(candidate))
                        _spawned.Add(candidate);
                }
            }
            return bootstrap;
        }

        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("PresentationRuntime_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance;

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;

            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: expected enough real cards to fill a full-size deck.");

            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(deckIds),
                activeDeckCardIds = new List<string>(deckIds),
            };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();
            Object.DestroyImmediate(databaseGo);
        }

        // ---------- spell art fallback chain ----------

        private static AvatarSpell AnyCatalogSpell() => AvatarSpell.CreateCatalog()[0];

        [Test]
        public void SpellArtFallback_OwnArtWins_WhenPresent()
        {
            AvatarSpell spell = AnyCatalogSpell();
            var requested = new List<string>();
            Sprite own = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
            Sprite Load(string path) { requested.Add(path); return own; }

            Assert.AreSame(own, GameBootstrap.ResolveSpellEffectSprite(spell, reduceMotion: false, Load));
            Assert.AreEqual(1, requested.Count, "Own art present: nothing further is requested.");
        }

        [Test]
        public void SpellArtFallback_MissingStaticFrame_FallsBackToTheAnimatedFrame_UnderReducedMotion()
        {
            AvatarSpell spell = AnyCatalogSpell();
            Sprite animated = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
            Sprite Load(string path) => path.EndsWith("_Static") ? null : animated;

            Assert.AreSame(animated, GameBootstrap.ResolveSpellEffectSprite(spell, reduceMotion: true, Load),
                "A missing '_Static' frame must degrade to the family's animated frame, not to nothing.");
        }

        [Test]
        public void SpellArtFallback_MissingFamilyArt_FallsBackToTheGenericEffect()
        {
            AvatarSpell spell = AnyCatalogSpell();
            Sprite generic = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
            Sprite Load(string path) => path.Contains(GameBootstrap.FallbackSpellEffectBaseName) ? generic : null;

            foreach (bool reduce in new[] { false, true })
                Assert.IsNotNull(GameBootstrap.ResolveSpellEffectSprite(spell, reduce, Load),
                    $"Missing spell art must fall back to the generic effect (reduceMotion={reduce}).");
        }

        [Test]
        public void SpellArtFallback_AllArtMissing_ReturnsNull_SoTheCallerCanSkipCleanly()
        {
            Assert.IsNull(GameBootstrap.ResolveSpellEffectSprite(AnyCatalogSpell(), false, _ => null));
        }

        [Test]
        public void SpellArtFallback_EveryCatalogSpell_Resolves_InBothMotionModes_WithRealArt()
        {
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            Assert.AreEqual(36, catalog.Count, "Setup: the catalog has 36 spells.");
            foreach (AvatarSpell spell in catalog)
                foreach (bool reduce in new[] { false, true })
                    Assert.IsNotNull(GameBootstrap.ResolveSpellEffectSprite(spell, reduce, p => Resources.Load<Sprite>(p)),
                        $"{spell.Id} (reduceMotion={reduce}) must resolve to real art.");
        }

        [Test]
        public void ElementPlacementEffect_ExistsForEveryElement()
        {
            foreach (CardElement element in System.Enum.GetValues(typeof(CardElement)))
                Assert.IsNotNull(GameBootstrap.ElementEffectSpriteForTests(element), $"{element} must have a placement effect.");
        }

        // ---------- reduced-motion still effects ----------

        [Test]
        public void CastImpact_UnderReducedMotion_StillDrawsAnEffect_NotSilence()
        {
            MotionPolicy.ReduceMotion = true;
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Presentation_ReducedImpact");

            bootstrap.PlayCastImpactForTests(AnyCatalogSpell(), Lane.Front);

            Assert.GreaterOrEqual(bootstrap.PresentationEffectCountForTests, 1,
                "Reduced Motion must still show the cast impact as a still frame.");
        }

        [Test]
        public void CastImpact_UnderFullMotion_DrawsAnEffect()
        {
            MotionPolicy.ReduceMotion = false;
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Presentation_FullImpact");

            bootstrap.PlayCastImpactForTests(AnyCatalogSpell(), Lane.Front);

            Assert.GreaterOrEqual(bootstrap.PresentationEffectCountForTests, 1);
        }

        [Test]
        public void ReducedMotionEffect_HoldIsAFixedStillTime_NotAFadeDuration()
        {
            Assert.Greater(GameBootstrap.StaticEffectHoldSeconds, 0.2f, "A still frame must stay long enough to read.");
            Assert.Less(GameBootstrap.StaticEffectHoldSeconds, 1.5f, "But never linger.");
        }

        // ---------- Start Battle banner ----------

        [Test]
        public void StartBattle_NormalMatch_ShowsTheStartBanner_InBothMotionModes()
        {
            foreach (bool reduce in new[] { false, true })
            {
                MotionPolicy.ReduceMotion = reduce;
                SaveValidDeckForNormalMatch();
                GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Presentation_StartBanner_" + reduce);
                bootstrap.AutoFormationForTests();
                Assert.AreEqual(0, bootstrap.StartBattleBannerCountForTests, "No banner before Start Battle.");

                bootstrap.StartBattleForTests();

                Assert.AreEqual(BattlePhase.Combat, bootstrap.Battle.Phase, "Setup: Start Battle must enter Combat.");
                Assert.AreEqual(1, bootstrap.StartBattleBannerCountForTests, $"Start Battle banner (reduceMotion={reduce}).");
            }
        }

        [Test]
        public void StartBattle_RefusedWithAnEmptyBoard_ShowsNoBanner()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Presentation_NoBanner");

            bootstrap.StartBattleForTests(); // no cards deployed: the existing gate refuses

            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase);
            Assert.AreEqual(0, bootstrap.StartBattleBannerCountForTests, "A refused start must not show the banner.");
        }

        // ---------- result overlay entrance ----------

        [Test]
        public void ResultOverlay_IsFullyVisible_AfterAResult_WhetherOrNotMotionIsReduced()
        {
            foreach (bool reduce in new[] { false, true })
            {
                MotionPolicy.ReduceMotion = reduce;
                SaveValidDeckForNormalMatch();
                GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Presentation_Result_" + reduce);
                bootstrap.AutoFormationForTests();
                bootstrap.StartBattleForTests();
                BattleController controller = bootstrap.Battle;
                controller.EnemyState.AvatarHealth = 1;
                int guard = 0;
                while (controller.Phase == BattlePhase.Combat && guard++ < BattleController.MaxCombatTicks)
                    controller.AdvanceCombatTick();

                Assert.IsTrue(bootstrap.ResultOverlayActiveForTests, "Setup: the result overlay is shown.");
                Assert.AreEqual(1f, bootstrap.ResultOverlayAlphaForTests, 0.0001f,
                    $"The result overlay must never be left partly transparent (reduceMotion={reduce}).");
            }
        }
    }
}
