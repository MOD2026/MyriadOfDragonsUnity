using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.AI;
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
    /// AI FORMATION-RESOURCE PARITY CONTRACT (release feature) - proves the AI's Formation-phase
    /// resource budget is exactly the player's own (GameBootstrap.StartNewMatch's enemyEconomy
    /// construction), and that every AI deployment still checks/deducts its real ResourceCost
    /// through the real, unchanged BattleController.TryPlayCard/SimpleAIOpponent.TakeTurn - no
    /// second deployment or affordability system. Exercised through the real production handlers:
    /// HomePagePresenter.OnToBattleClickedForTests/LaunchCampaignStageForTests and
    /// GameBootstrap.StartBattleForTests (which itself calls the real SimpleAIOpponent.TakeTurn).
    /// </summary>
    public class AIFormationResourceParityTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsAIParity_" + System.Guid.NewGuid().ToString("N"));
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

        private static void SaveValidDeckForNormalMatch(CardDatabase database)
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
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
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
            var go = new GameObject("HomePagePresenterUnderTest_AIParity");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        [Test]
        public void NormalMatch_EnemyFormationBudget_ExactlyEqualsPlayerBudget()
        {
            var databaseGo = new GameObject("AIParity_CardDatabase_Normal");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AIParity_NormalBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            home.OnToBattleClickedForTests();

            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "Setup: expected a valid saved deck to enter normal Battle.");
            Assert.AreEqual(bootstrap.Battle.PlayerState.ResourceCap, bootstrap.Battle.EnemyState.ResourceCap,
                "Requirement 1: the AI's Formation resource cap must exactly equal the player's, in a normal match.");
            Assert.AreEqual(bootstrap.Battle.PlayerState.Resource, bootstrap.Battle.EnemyState.Resource,
                "Requirement 1: the AI's starting Formation resource must exactly equal the player's, in a normal match.");
        }

        [Test]
        public void CampaignMatch_EnemyFormationBudget_ExactlyEqualsPlayerBudget()
        {
            var databaseGo = new GameObject("AIParity_CardDatabase_Campaign");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AIParity_CampaignBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            home.LaunchCampaignStageForTests(stage);

            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "Setup: expected a valid Campaign launch to enter Battle.");
            Assert.AreEqual(bootstrap.Battle.PlayerState.ResourceCap, bootstrap.Battle.EnemyState.ResourceCap,
                "Requirement 1: the AI's Formation resource cap must exactly equal the player's, in a Campaign match.");
            Assert.AreEqual(bootstrap.Battle.PlayerState.Resource, bootstrap.Battle.EnemyState.Resource,
                "Requirement 1: the AI's starting Formation resource must exactly equal the player's, in a Campaign match.");
        }

        [Test]
        public void AIDeployment_DeductsRealResourceCost_AndNeverOverspendsOrOverdeploys()
        {
            var databaseGo = new GameObject("AIParity_CardDatabase_Deployment");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AIParity_DeploymentBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            home.LaunchCampaignStageForTests(stage);

            int startingBudget = bootstrap.Battle.EnemyState.Resource;
            List<Card> enemyHandBeforeDeploy = bootstrap.Battle.EnemyState.Hand.ToList();

            bootstrap.StartBattleForTests(); // real Start Battle handler - deploys the enemy via the real SimpleAIOpponent.TakeTurn

            List<Card> deployedCards = bootstrap.Battle.EnemyState.Lanes.Values
                .SelectMany(lane => lane.Cards)
                .Select(instance => instance.Definition)
                .ToList();
            int totalSpent = deployedCards.Sum(c => c.ResourceCost);

            Assert.AreEqual(startingBudget - totalSpent, bootstrap.Battle.EnemyState.Resource,
                "Requirement 2: the AI's remaining Resource must equal its starting budget minus the exact sum of every deployed card's real ResourceCost.");
            Assert.GreaterOrEqual(bootstrap.Battle.EnemyState.Resource, 0, "Requirement 2: the AI's Resource must never go negative.");

            // Requirement 3: it must have stopped because nothing affordable-and-room-fitting was
            // left, not arbitrarily - every remaining hand card is either unaffordable or has no
            // room in every lane it could legally enter.
            foreach (Card remaining in bootstrap.Battle.EnemyState.Hand)
            {
                bool couldStillBeDeployed = remaining.ResourceCost <= bootstrap.Battle.EnemyState.Resource
                    && System.Enum.GetValues(typeof(Lane)).Cast<Lane>().Any(lane => bootstrap.Battle.EnemyState.Lanes[lane].HasRoomFor(remaining));
                Assert.IsFalse(couldStillBeDeployed,
                    $"Requirement 3: card '{remaining.Id}' was left undeployed in hand despite being affordable and room-fitting - the AI must never stop early while a legal, affordable play remains.");
            }

            Assert.Greater(enemyHandBeforeDeploy.Count, 0, "Setup: expected the enemy to have a real hand to deploy from.");
        }

        [Test]
        public void PlayerFormationBudgetAndPlacement_RemainUnchanged()
        {
            var databaseGo = new GameObject("AIParity_CardDatabase_PlayerUnchanged");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            SaveValidDeckForNormalMatch(database);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AIParity_PlayerUnchangedBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            home.OnToBattleClickedForTests();

            PlayerEmpireData empire = bootstrap.Profile.Empire;
            Assert.AreEqual(empire.ResourceCap, bootstrap.Battle.PlayerState.ResourceCap,
                "Requirement 5: the player's own Formation resource cap must remain exactly PlayerEmpireData.ResourceCap, unaffected by this change.");
            Assert.AreEqual(System.Math.Min(empire.ResourceCap, empire.Turn1Resource), bootstrap.Battle.PlayerState.Resource,
                "Requirement 5: the player's own starting Formation resource must remain unaffected by this change.");

            bootstrap.AutoFormationForTests();
            int occupiedLanes = bootstrap.Battle.PlayerState.Lanes.Values.Count(l => l.Cards.Count > 0);
            Assert.AreEqual(3, occupiedLanes, "Requirement 5: Auto Formation's own placement policy (one card per lane) must remain unchanged.");
        }

        [Test]
        public void Tutorial_EnemyEconomy_RemainsCompletelyUnaffected()
        {
            var databaseGo = new GameObject("AIParity_CardDatabase_Tutorial");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AIParity_TutorialBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SetBattleCanvasVisible(true);

            // Tutorial builds its own fixed, symmetric economy directly in
            // StartApprovedTutorialBattle - never touches GameBootstrap.StartNewMatch's
            // enemyEconomy construction (the one call site this contract changed) at all.
            Assert.AreEqual(bootstrap.Battle.PlayerState.ResourceCap, bootstrap.Battle.EnemyState.ResourceCap,
                "Requirement 6: Tutorial's already-symmetric fixed economy must remain exactly as it was.");
            HashSet<string> tutorialPlayerIds = new HashSet<string>(bootstrap.Battle.PlayerState.Hand
                .Concat(bootstrap.Battle.PlayerState.DrawPile)
                .Select(c => c.Id));
            CollectionAssert.AreEquivalent(new[] { "warrior", "novice_knight", "goblin_caster" }, tutorialPlayerIds,
                "Requirement 6: Tutorial's scripted encounter (player side) must remain exactly unchanged.");
        }
    }
}
