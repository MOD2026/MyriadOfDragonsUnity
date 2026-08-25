using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// BLOCK AA — Castle Phase-1 relationship EditMode (docs/CASTLE_PHASE1_IMPLEMENTER_BRIEF_BLOCK_AA_v1.md).
    /// Proves Castle's live Resource/HP readers, the Normal/Campaign economy handoff, AI shared
    /// scaling, and Tutorial isolation - relationships only, no new balance targets. Section A is
    /// pure PlayerEmpireData math (no battle harness); Section B exercises the real production
    /// handoff through GameBootstrap/HomePagePresenter; Section C is the L1/5/10/15/20/25/30
    /// matrix, observations only.
    /// </summary>
    public class CastlePhase1RelationshipTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsCastleAA_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                Directory.Delete(_scratchSaveDir, recursive: true);
            }
        }

        private static PlayerEmpireData BuildEmpire(int avatarLevel, int castleLevel, int barracksLevel, int gateLevel)
        {
            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel, castleLevel, barracksLevel, gateLevel);
            empire.InitializeTCGModifiers();
            return empire;
        }

        // ---------- Section A: pure PlayerEmpireData relationships ----------

        [Test]
        public void LaterCastleBreakpoint_ProducesGreaterResourceCap_AvatarBarracksGateFixed()
        {
            PlayerEmpireData low = BuildEmpire(avatarLevel: 10, castleLevel: 5, barracksLevel: 5, gateLevel: 5);
            PlayerEmpireData high = BuildEmpire(avatarLevel: 10, castleLevel: 10, barracksLevel: 5, gateLevel: 5);

            Assert.Greater(PlayerEmpireData.CastleResourceBonusForLevel(10), PlayerEmpireData.CastleResourceBonusForLevel(5));
            Assert.Greater(high.ResourceCap, low.ResourceCap,
                "A later live Castle breakpoint must produce a greater total ResourceCap with Avatar/Barracks/Gate fixed.");
        }

        [Test]
        public void LaterCastleBreakpoint_ProducesGreaterStartingAvatarHealth_AvatarBarracksGateFixed()
        {
            PlayerEmpireData low = BuildEmpire(avatarLevel: 10, castleLevel: 5, barracksLevel: 5, gateLevel: 5);
            PlayerEmpireData high = BuildEmpire(avatarLevel: 10, castleLevel: 10, barracksLevel: 5, gateLevel: 5);

            Assert.Greater(PlayerEmpireData.CastleHealthBonusForLevel(10), PlayerEmpireData.CastleHealthBonusForLevel(5));
            Assert.Greater(high.StartingAvatarHealth, low.StartingAvatarHealth,
                "A later live Castle breakpoint must produce a greater total StartingAvatarHealth with Avatar/Barracks/Gate fixed.");
        }

        [Test]
        public void ResourceCapDelta_EqualsCastleResourceBonusDelta_NoDoubleApplication()
        {
            PlayerEmpireData low = BuildEmpire(avatarLevel: 12, castleLevel: 5, barracksLevel: 8, gateLevel: 3);
            PlayerEmpireData high = BuildEmpire(avatarLevel: 12, castleLevel: 20, barracksLevel: 8, gateLevel: 3);

            int totalDelta = high.ResourceCap - low.ResourceCap;
            int castleOnlyDelta = PlayerEmpireData.CastleResourceBonusForLevel(20) - PlayerEmpireData.CastleResourceBonusForLevel(5);

            Assert.AreEqual(castleOnlyDelta, totalDelta,
                "The change in total ResourceCap must equal exactly the Castle-only helper's delta - no second application anywhere else.");
        }

        [Test]
        public void StartingAvatarHealthDelta_EqualsCastleHealthBonusDelta_NoDoubleApplication()
        {
            PlayerEmpireData low = BuildEmpire(avatarLevel: 12, castleLevel: 5, barracksLevel: 8, gateLevel: 3);
            PlayerEmpireData high = BuildEmpire(avatarLevel: 12, castleLevel: 20, barracksLevel: 8, gateLevel: 3);

            int totalDelta = high.StartingAvatarHealth - low.StartingAvatarHealth;
            int castleOnlyDelta = PlayerEmpireData.CastleHealthBonusForLevel(20) - PlayerEmpireData.CastleHealthBonusForLevel(5);

            Assert.AreEqual(castleOnlyDelta, totalDelta,
                "The change in total StartingAvatarHealth must equal exactly the Castle-only helper's delta - Avatar level is fixed on both " +
                "sides, so the onboarding taper term cancels and cannot mask a double application.");
        }

        [Test]
        public void Turn1Resource_StaysPositive_NeverExceedsCap_NonDecreasingAsCastleRises()
        {
            int[] castleLevels = { 1, 5, 10, 15, 20, 25, 30 };
            int previousTurn1 = 0;

            foreach (int castleLevel in castleLevels)
            {
                PlayerEmpireData empire = BuildEmpire(avatarLevel: 10, castleLevel: castleLevel, barracksLevel: 10, gateLevel: 5);

                Assert.Greater(empire.Turn1Resource, 0, $"Turn1Resource must stay positive at Castle L{castleLevel}.");
                Assert.LessOrEqual(empire.Turn1Resource, empire.ResourceCap, $"Turn1Resource must never exceed the cap at Castle L{castleLevel}.");
                Assert.GreaterOrEqual(empire.Turn1Resource, previousTurn1,
                    $"Turn1Resource must be non-decreasing as Castle rises (L{castleLevel} regressed below the previous level).");
                previousTurn1 = empire.Turn1Resource;
            }
        }

        [Test]
        public void CastleBonuses_StopGrowingAtMaxCastleLevel_AboveCapInputGrantsNoExtraPower()
        {
            int atCap = PlayerEmpireData.MaxCastleLevel;
            int aboveCap = PlayerEmpireData.MaxCastleLevel + 5;

            Assert.AreEqual(PlayerEmpireData.CastleResourceBonusForLevel(atCap), PlayerEmpireData.CastleResourceBonusForLevel(aboveCap),
                "Castle Resource bonus must stop growing at MaxCastleLevel - an above-cap input must never grant extra power.");
            Assert.AreEqual(PlayerEmpireData.CastleHealthBonusForLevel(atCap), PlayerEmpireData.CastleHealthBonusForLevel(aboveCap),
                "Castle HP bonus must stop growing at MaxCastleLevel - an above-cap input must never grant extra power.");
        }

        [Test]
        public void CastleOnlyChange_DoesNotAlterDeckSlotsAvatarBarracksGateOrGateEligibility()
        {
            PlayerEmpireData low = BuildEmpire(avatarLevel: 12, castleLevel: 5, barracksLevel: 8, gateLevel: 3);
            PlayerEmpireData high = BuildEmpire(avatarLevel: 12, castleLevel: 20, barracksLevel: 8, gateLevel: 3);

            Assert.AreEqual(low.DeckSlotCount, high.DeckSlotCount, "Castle-only change must not alter deck slots.");
            Assert.AreEqual(low.AvatarLevel, high.AvatarLevel, "Castle-only change must not alter Avatar level.");
            Assert.AreEqual(low.BarracksLevel, high.BarracksLevel, "Castle-only change must not alter Barracks level.");
            Assert.AreEqual(low.GateLevel, high.GateLevel, "Castle-only change must not alter Gate level.");

            for (int chapter = 1; chapter <= 12; chapter++)
            {
                Assert.AreEqual(
                    PlayerEmpireData.IsCampaignChapterAllowedByGate(low.GateLevel, chapter),
                    PlayerEmpireData.IsCampaignChapterAllowedByGate(high.GateLevel, chapter),
                    $"Castle-only change must not alter Gate chapter eligibility for chapter {chapter}.");
            }
        }

        // ---------- Section B: Normal/Campaign handoff, AI shared scaling, Tutorial isolation ----------

        private static List<string> SaveValidDeckProfile(CardDatabase database, System.Action<PlayerProfile> configure = null)
        {
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
                stamina = 100,
            };
            configure?.Invoke(profile);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the profile.");
            SaveSystem.ResetCurrentProfileForTests();
            return deckIds;
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
            bootstrap.SetBattleCanvasVisible(false);
            return bootstrap;
        }

        private HomePagePresenter SpawnHomePagePresenter(GameBootstrap bootstrap)
        {
            var go = new GameObject("HomePagePresenterUnderTest_CastleAA");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        [Test]
        public void NormalMatch_PlayerBattleState_ReceivesInitializedEmpireEconomy()
        {
            var databaseGo = new GameObject("CastleAA_CardDatabase_NormalHandoff");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveValidDeckProfile(database, p => p.castleLevel = 15);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CastleAA_NormalHandoffBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            home.OnToBattleClickedForTests();

            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "Setup: expected a valid saved deck to enter normal Battle.");
            PlayerEmpireData empire = bootstrap.Profile.Empire;
            Assert.AreEqual(empire.ResourceCap, bootstrap.Battle.PlayerState.ResourceCap,
                "Normal Battle player state must receive the initialized Empire ResourceCap.");
            Assert.AreEqual(empire.StartingAvatarHealth, bootstrap.Battle.PlayerState.MaxAvatarHealth,
                "Normal Battle player state must receive the initialized Empire StartingAvatarHealth.");
            Assert.AreEqual(System.Math.Min(empire.ResourceCap, empire.Turn1Resource), bootstrap.Battle.PlayerState.Resource,
                "Normal Battle player state must receive the initialized Empire Turn1Resource as its starting Resource.");
        }

        [Test]
        public void CampaignMatch_PlayerBattleState_ReceivesInitializedEmpireEconomy()
        {
            var databaseGo = new GameObject("CastleAA_CardDatabase_CampaignHandoff");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveValidDeckProfile(database, p => p.castleLevel = 15);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CastleAA_CampaignHandoffBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            home.LaunchCampaignStageForTests(stage);

            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "Setup: expected a valid Campaign launch to enter Battle.");
            PlayerEmpireData empire = bootstrap.Profile.Empire;
            Assert.AreEqual(empire.ResourceCap, bootstrap.Battle.PlayerState.ResourceCap,
                "Campaign player state must receive the initialized Empire ResourceCap.");
            Assert.AreEqual(empire.StartingAvatarHealth, bootstrap.Battle.PlayerState.MaxAvatarHealth,
                "Campaign player state must receive the initialized Empire StartingAvatarHealth.");
        }

        [Test]
        public void NormalAndCampaign_AIResourceCap_RemainsEqualToPlayersCastleScaledCap()
        {
            var databaseGo = new GameObject("CastleAA_CardDatabase_AIResource");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveValidDeckProfile(database, p => p.castleLevel = 25);

            GameBootstrap normalBootstrap = SpawnAndInitializeBootstrap("CastleAA_AIResourceNormalBootstrap");
            HomePagePresenter normalHome = SpawnHomePagePresenter(normalBootstrap);
            normalHome.OnToBattleClickedForTests();
            Assert.AreEqual(normalBootstrap.Battle.PlayerState.ResourceCap, normalBootstrap.Battle.EnemyState.ResourceCap,
                "Normal Battle AI Resource cap must remain equal to the player's Castle-scaled cap.");

            GameBootstrap campaignBootstrap = SpawnAndInitializeBootstrap("CastleAA_AIResourceCampaignBootstrap");
            HomePagePresenter campaignHome = SpawnHomePagePresenter(campaignBootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            campaignHome.LaunchCampaignStageForTests(stage);
            Assert.AreEqual(campaignBootstrap.Battle.PlayerState.ResourceCap, campaignBootstrap.Battle.EnemyState.ResourceCap,
                "Campaign AI Resource cap must remain equal to the player's Castle-scaled cap.");
        }

        [Test]
        public void AIHealth_MovesConsistentlyWithPlayersCastleInfluencedHealth_AvatarArchetypeFixed()
        {
            var databaseGo = new GameObject("CastleAA_CardDatabase_AIHealth");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            SaveValidDeckProfile(database, p => { p.castleLevel = 1; });
            GameBootstrap lowBootstrap = SpawnAndInitializeBootstrap("CastleAA_AIHealthLowBootstrap");
            HomePagePresenter lowHome = SpawnHomePagePresenter(lowBootstrap);
            lowHome.OnToBattleClickedForTests();
            int lowPlayerHealth = lowBootstrap.Battle.PlayerState.MaxAvatarHealth;
            int lowEnemyHealth = lowBootstrap.Battle.EnemyState.MaxAvatarHealth;

            SaveValidDeckProfile(database, p => { p.castleLevel = 30; });
            GameBootstrap highBootstrap = SpawnAndInitializeBootstrap("CastleAA_AIHealthHighBootstrap");
            HomePagePresenter highHome = SpawnHomePagePresenter(highBootstrap);
            highHome.OnToBattleClickedForTests();
            int highPlayerHealth = highBootstrap.Battle.PlayerState.MaxAvatarHealth;
            int highEnemyHealth = highBootstrap.Battle.EnemyState.MaxAvatarHealth;

            Assert.Greater(highPlayerHealth, lowPlayerHealth, "Setup: expected Castle L30 to raise player StartingAvatarHealth over L1.");
            Assert.Greater(highEnemyHealth, lowEnemyHealth,
                "AI Health must move consistently (increase) with the player's Castle-influenced Health through the existing scaling path.");
        }

        [Test]
        public void TutorialMatch_IgnoresRealPlayerCastleLevel_UsesFixedTutorialEconomy()
        {
            var databaseGo = new GameObject("CastleAA_CardDatabase_TutorialIsolation");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            // A real player profile at max Castle - if Tutorial ever read this, its economy
            // would shift; it must not.
            var profile = new PlayerProfile { castleLevel = PlayerEmpireData.MaxCastleLevel };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the max-Castle profile to save.");
            SaveSystem.ResetCurrentProfileForTests();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CastleAA_TutorialIsolationBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SetBattleCanvasVisible(true);

            var fixedTutorialEmpire = new PlayerEmpireData();
            fixedTutorialEmpire.SetLevelsForTesting(avatarLevel: 1, castleLevel: 1, barracksLevel: 1);
            fixedTutorialEmpire.InitializeTCGModifiers();

            Assert.AreEqual(fixedTutorialEmpire.ResourceCap, bootstrap.Battle.PlayerState.ResourceCap,
                "Tutorial must use its own fixed Castle L1 economy, never the real player's max-Castle ResourceCap.");
            Assert.AreEqual(fixedTutorialEmpire.StartingAvatarHealth, bootstrap.Battle.PlayerState.MaxAvatarHealth,
                "Tutorial must use its own fixed Castle L1 economy, never the real player's max-Castle StartingAvatarHealth.");
            Assert.AreNotEqual(bootstrap.Profile.castleLevel, 1, "Setup: expected the real saved profile to still show max Castle level.");
        }

        // ---------- Section C: L1/5/10/15/20/25/30 matrix - observations only, no new targets ----------

        [Test]
        public void CastleMatrix_L1_5_10_15_20_25_30_ObservationsOnly()
        {
            int[] levels = { 1, 5, 10, 15, 20, 25, 30 };
            int previousCap = 0;
            int previousHealth = 0;

            foreach (int level in levels)
            {
                PlayerEmpireData empire = BuildEmpire(avatarLevel: 1, castleLevel: level, barracksLevel: 1, gateLevel: 1);
                Debug.Log($"[CastleMatrixAA] L{level}: ResourceCap={empire.ResourceCap} Turn1Resource={empire.Turn1Resource} " +
                          $"StartingAvatarHealth={empire.StartingAvatarHealth} (ResourceBonus={PlayerEmpireData.CastleResourceBonusForLevel(level)}, " +
                          $"HealthBonus={PlayerEmpireData.CastleHealthBonusForLevel(level)})");

                Assert.GreaterOrEqual(empire.ResourceCap, previousCap, $"Observation only: ResourceCap should not regress at L{level}.");
                Assert.GreaterOrEqual(empire.StartingAvatarHealth, previousHealth, $"Observation only: StartingAvatarHealth should not regress at L{level}.");
                previousCap = empire.ResourceCap;
                previousHealth = empire.StartingAvatarHealth;
            }
        }
    }
}
