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
    /// CHAPTER 1 FRESH-PLAYER PLAYABILITY CONTRACT (release feature) - proves each Chapter 1
    /// stage is beatable by a fresh player's approved starter deck under a deterministic, legal
    /// FULL manual formation (not Auto Formation's deliberately minimal one-card-per-lane
    /// onboarding policy). Deployment uses BattleController.TryPlayCard directly - the exact same
    /// production placement route both a manual hand-card-then-lane tap and Auto Formation itself
    /// already use (see GameBootstrap.PerformAutoFormation's own doc comment) - repeated across
    /// every lane until nothing further is affordable or room-fitting, so as many of the ten
    /// starter cards are deployed as the real Formation resource and board room legally allow.
    /// The enemy is deployed by the real, unchanged SimpleAIOpponent.TakeTurn (via
    /// GameBootstrap.StartBattleForTests), and combat resolves through the real, unchanged
    /// AdvanceCombatTick() - no health edit, no forced victory, no test-only shortcut.
    /// </summary>
    public class Chapter1FullFormationPlayabilityTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        private static readonly string[] ApprovedStarterCollectionCardIds =
        {
            "warrior", "novice_knight", "goblin_caster",
            "cleric", "archer_elf", "fox", "bunny", "forest", "tribal_warrior", "undead_soldier",
        };

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsFullFormation_" + System.Guid.NewGuid().ToString("N"));
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

        private static void SaveFreshStarterProfile(List<string> unlockedStages = null)
        {
            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(ApprovedStarterCollectionCardIds),
                activeDeckCardIds = new List<string>(ApprovedStarterCollectionCardIds),
                stamina = 100,
            };
            if (unlockedStages != null) profile.unlockedStageIds = unlockedStages;
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the fresh starter profile to save.");
            SaveSystem.ResetCurrentProfileForTests();
        }

        private GameBootstrap SpawnAndInitializeBootstrap(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null) _spawned.Add(spawned);
            }
            bootstrap.SetBattleCanvasVisible(false);
            return bootstrap;
        }

        private HomePagePresenter SpawnHomePagePresenter(GameBootstrap bootstrap)
        {
            var go = new GameObject("HomePagePresenterUnderTest_FullFormation");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        /// <summary>
        /// The deterministic, legal, FULL manual formation policy: repeatedly walks Front/Middle/
        /// Back, each pass placing the cheapest, room-fitting, currently-affordable remaining hand
        /// card into that lane via the real BattleController.TryPlayCard - identical selection
        /// criteria to GameBootstrap.PerformAutoFormation (deterministic, no ties broken by
        /// chance), just not stopping after one card per lane. Ends only once a full pass places
        /// nothing anywhere - i.e. every remaining hand card is either unaffordable or has no room
        /// left in any lane, the real legal limit, not an arbitrary cutoff.
        /// </summary>
        private static void DeployFullLegalFormation(BattleController controller)
        {
            PlayerBattleState player = controller.PlayerState;
            Lane[] lanes = { Lane.Front, Lane.Middle, Lane.Back };

            bool playedAny;
            do
            {
                playedAny = false;
                foreach (Lane lane in lanes)
                {
                    Card candidate = player.Hand
                        .Where(c => c.ResourceCost <= player.Resource && player.Lanes[lane].HasRoomFor(c))
                        .OrderBy(c => c.ResourceCost)
                        .ThenBy(c => c.SlotWeight)
                        .FirstOrDefault();

                    if (candidate == null) continue;
                    if (controller.TryPlayCard(player, candidate, lane)) playedAny = true;
                }
            } while (playedAny);
        }

        private static MatchResult RunFullFormationPolicy(GameBootstrap bootstrap, HomePagePresenter home, CampaignStageData stage)
        {
            home.LaunchCampaignStageForTests(stage);
            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, $"Setup: expected Stage {stage.stageId} to launch successfully.");
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, $"Setup: expected Stage {stage.stageId} to be a valid, unblocked campaign match.");

            DeployFullLegalFormation(bootstrap.Battle);
            Assert.Greater(bootstrap.Battle.PlayerState.Lanes.Values.Sum(l => l.Cards.Count), 3,
                $"Setup: expected the full manual formation to deploy more than Auto Formation's own 3-card minimum for Stage {stage.stageId}.");

            bootstrap.StartBattleForTests();

            MatchResult? result = null;
            bootstrap.Battle.OnMatchCompleted += r => result = r;

            int ticksRun = 0;
            while (bootstrap.Battle.Phase == BattlePhase.Combat)
            {
                bootstrap.Battle.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks,
                    $"Requirement 4: Stage {stage.stageId} must resolve within the normal combat safety limit.");
            }

            Assert.IsTrue(result.HasValue, $"Requirement 4: Stage {stage.stageId} must actually resolve (OnMatchCompleted must fire).");
            Debug.Log($"[Chapter1FullFormationPlayabilityTests] Stage {stage.stageId}: {(result.Value.IsVictory ? "VICTORY" : "DEFEAT")} in {ticksRun} tick(s).");
            return result.Value;
        }

        [Test]
        public void Stage1_1_IsWinnable_UnderTheFullManualFormationPolicy()
        {
            var databaseGo = new GameObject("FullFormation_CardDatabase_1_1");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            SaveFreshStarterProfile();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("FullFormation_Bootstrap_1_1");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");

            MatchResult result = RunFullFormationPolicy(bootstrap, home, stage);

            Assert.IsTrue(result.IsVictory, "Requirement 4: Stage 1-1 must resolve as a player victory under the full manual formation policy.");
        }

        [Test]
        public void Stage1_2_IsWinnable_UnderTheFullManualFormationPolicy()
        {
            var databaseGo = new GameObject("FullFormation_CardDatabase_1_2");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            SaveFreshStarterProfile(new List<string> { "1-1", "1-2", "1-3" });
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("FullFormation_Bootstrap_1_2");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-2");

            MatchResult result = RunFullFormationPolicy(bootstrap, home, stage);

            Assert.IsTrue(result.IsVictory, "Requirement 4: Stage 1-2 must resolve as a player victory under the full manual formation policy.");
        }

        [Test]
        public void Stage1_3_IsWinnable_UnderTheFullManualFormationPolicy()
        {
            var databaseGo = new GameObject("FullFormation_CardDatabase_1_3");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            SaveFreshStarterProfile(new List<string> { "1-1", "1-2", "1-3" });
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("FullFormation_Bootstrap_1_3");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-3");

            MatchResult result = RunFullFormationPolicy(bootstrap, home, stage);

            Assert.IsTrue(result.IsVictory, "Requirement 4: Stage 1-3 must resolve as a player victory under the full manual formation policy.");
        }

        [Test]
        public void Stage1_2_And1_3_RemainDistinctConfiguredEncounters()
        {
            CampaignStageData stage1_1 = CampaignMapPresenter.GetStageForTests("1-1");
            CampaignStageData stage1_2 = CampaignMapPresenter.GetStageForTests("1-2");
            CampaignStageData stage1_3 = CampaignMapPresenter.GetStageForTests("1-3");

            CollectionAssert.IsEmpty(stage1_1.enemyDeckCardIds.Intersect(stage1_2.enemyDeckCardIds), "Requirement 6: Stage 1-1 and 1-2 must remain distinct configured encounters.");
            CollectionAssert.IsEmpty(stage1_1.enemyDeckCardIds.Intersect(stage1_3.enemyDeckCardIds), "Requirement 6: Stage 1-1 and 1-3 must remain distinct configured encounters.");
            CollectionAssert.IsEmpty(stage1_2.enemyDeckCardIds.Intersect(stage1_3.enemyDeckCardIds), "Requirement 6: Stage 1-2 and 1-3 must remain distinct configured encounters.");
        }

        [Test]
        public void FullFormationPolicy_RespectsRealResourceAndPlacementRules()
        {
            var databaseGo = new GameObject("FullFormation_CardDatabase_Legality");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            SaveFreshStarterProfile();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("FullFormation_LegalityBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            home.LaunchCampaignStageForTests(stage);

            int startingBudget = bootstrap.Battle.PlayerState.Resource;
            DeployFullLegalFormation(bootstrap.Battle);

            List<Card> deployed = bootstrap.Battle.PlayerState.Lanes.Values
                .SelectMany(l => l.Cards)
                .Select(instance => instance.Definition)
                .ToList();
            int totalSpent = deployed.Sum(c => c.ResourceCost);

            Assert.AreEqual(startingBudget - totalSpent, bootstrap.Battle.PlayerState.Resource,
                "Requirement 2: the full formation's total spend must exactly equal the sum of every deployed card's real ResourceCost.");
            Assert.GreaterOrEqual(bootstrap.Battle.PlayerState.Resource, 0, "Requirement 2: Resource must never go negative.");
            Assert.AreEqual(deployed.Count, deployed.Distinct().Count(), "Setup: expected no card instance to be deployed twice.");

            foreach (var lane in bootstrap.Battle.PlayerState.Lanes.Values)
            {
                Assert.LessOrEqual(lane.Cards.Count, 3, "Requirement 2: no lane may exceed its real room limit.");
            }

            foreach (Card remaining in bootstrap.Battle.PlayerState.Hand)
            {
                bool couldStillBeDeployed = remaining.ResourceCost <= bootstrap.Battle.PlayerState.Resource
                    && System.Enum.GetValues(typeof(Lane)).Cast<Lane>().Any(lane => bootstrap.Battle.PlayerState.Lanes[lane].HasRoomFor(remaining));
                Assert.IsFalse(couldStillBeDeployed,
                    $"Requirement 2: card '{remaining.Id}' was left undeployed despite being affordable and room-fitting - the policy must deploy as many legal cards as resource/room allow.");
            }
        }
    }
}
