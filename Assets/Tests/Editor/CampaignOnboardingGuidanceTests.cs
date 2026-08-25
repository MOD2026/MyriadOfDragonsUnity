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
    /// FIRST-TIME CAMPAIGN ONBOARDING GUIDANCE CONTRACT (release feature) - proves the whole
    /// message/state sequence (launch -&gt; Formation -&gt; auto/manual placement -&gt; Combat
    /// -&gt; result) through the real production surfaces: GameBootstrap.
    /// TutorialGuidanceCaptionTextForTests (the shared caption RefreshCampaignGuidanceCaption now
    /// drives for a Campaign attempt) and ResultTextForTests (the existing result-overlay text
    /// HandleMatchEnded already builds). No new UI element, panel, or test-only shortcut - every
    /// state is reached through the real LaunchCampaignStageForTests/AutoFormationForTests/
    /// TryPlayCard/StartBattleForTests/AdvanceCombatTick path, with RefreshAllForTests() standing
    /// in only for the CombatLoop coroutine a real Play Mode session drives automatically.
    /// </summary>
    public class CampaignOnboardingGuidanceTests
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
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsCampaignOnboarding_" + System.Guid.NewGuid().ToString("N"));
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

        private static void SaveFreshStarterProfile()
        {
            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(ApprovedStarterCollectionCardIds),
                activeDeckCardIds = new List<string>(ApprovedStarterCollectionCardIds),
                stamina = 100,
            };
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
            var go = new GameObject("HomePagePresenterUnderTest_CampaignOnboarding");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        [Test]
        public void Formation_BeforeAnyPlacement_ExplainsDeckAutoFormationAndManualPlacement()
        {
            var databaseGo = new GameObject("CampaignOnboarding_CardDatabase_Formation");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveFreshStarterProfile();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignOnboarding_FormationBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");

            home.LaunchCampaignStageForTests(stage);

            Assert.IsTrue(bootstrap.TutorialGuidanceCaptionActiveForTests, "Requirement 1: the guidance caption must be visible before any placement.");
            string text = bootstrap.TutorialGuidanceCaptionTextForTests;
            StringAssert.Contains("saved 10-card deck", text, "Requirement 1: must explain the saved deck fills the hand.");
            StringAssert.Contains("Auto Formation", text, "Requirement 1: must explain Auto Formation is available.");
            StringAssert.Contains("optional", text, "Requirement 1: must state Auto Formation is optional.");
            StringAssert.Contains("tap a hand card, then an empty lane slot", text, "Requirement 1: must explain manual placement steps.");
            StringAssert.Contains("Resource is spent as normal", text, "Requirement 1: must explain Resource is spent normally.");
        }

        [Test]
        public void Formation_AfterAutoFormation_ExplainsAddingMoreAndLaneRoles_AndStartBattleStaysUsable()
        {
            var databaseGo = new GameObject("CampaignOnboarding_CardDatabase_Auto");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveFreshStarterProfile();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignOnboarding_AutoBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            home.LaunchCampaignStageForTests(stage);

            bootstrap.AutoFormationForTests();

            string text = bootstrap.TutorialGuidanceCaptionTextForTests;
            StringAssert.Contains("add more cards", text, "Requirement 2: must explain more affordable cards may be added.");
            StringAssert.Contains("Front gives +1 Attack", text, "Requirement 2: must explain lane roles in plain language.");
            StringAssert.Contains("Middle gives +1 Health", text, "Requirement 2: must explain lane roles in plain language.");
            StringAssert.Contains("Start Battle", text, "Requirement 3: must mention Start Battle is available.");

            // Guidance must never block the real control it is describing.
            bootstrap.StartBattleForTests();
            Assert.AreEqual(BattlePhase.Combat, bootstrap.Battle.Phase, "Guidance must never block a legal Start Battle - Auto Formation already deployed a legal squad.");
        }

        [Test]
        public void Formation_AfterManualPlacement_SameGuidanceAsAutoFormation()
        {
            var databaseGo = new GameObject("CampaignOnboarding_CardDatabase_Manual");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveFreshStarterProfile();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignOnboarding_ManualBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            home.LaunchCampaignStageForTests(stage);

            Card anyCard = bootstrap.Battle.PlayerState.Hand.First(c => c.ResourceCost <= bootstrap.Battle.PlayerState.Resource);
            Assert.IsTrue(bootstrap.Battle.TryPlayCard(bootstrap.Battle.PlayerState, anyCard, Lane.Front), "Setup: expected a legal manual placement.");
            bootstrap.RefreshAllForTests();

            string text = bootstrap.TutorialGuidanceCaptionTextForTests;
            StringAssert.Contains("add more cards", text, "Requirement 2: a single manual placement must trigger the same guidance as Auto Formation.");
            StringAssert.Contains("Start Battle", text, "Requirement 3: must mention Start Battle is available.");
        }

        [Test]
        public void Combat_ExplainsAutomaticAttacksAndActiveSpellChoice()
        {
            var databaseGo = new GameObject("CampaignOnboarding_CardDatabase_Combat");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveFreshStarterProfile();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignOnboarding_CombatBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            home.LaunchCampaignStageForTests(stage);

            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();
            bootstrap.RefreshAllForTests();

            Assert.AreEqual(BattlePhase.Combat, bootstrap.Battle.Phase, "Setup: expected Start Battle to enter Combat.");
            string text = bootstrap.TutorialGuidanceCaptionTextForTests;
            StringAssert.Contains("automatic", text, "Requirement 4: must explain cards attack automatically.");
            StringAssert.Contains("spell", text, "Requirement 4: must explain spells remain the player's active choice.");
        }

        [Test]
        public void Resolved_Victory_StatesReturnHomeForNextStage()
        {
            var databaseGo = new GameObject("CampaignOnboarding_CardDatabase_Victory");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveFreshStarterProfile();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignOnboarding_VictoryBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            home.LaunchCampaignStageForTests(stage);

            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();

            int ticksRun = 0;
            while (bootstrap.Battle.Phase == BattlePhase.Combat)
            {
                bootstrap.Battle.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected the match to resolve within the safety cap.");
            }
            bootstrap.RefreshAllForTests();

            // The guidance caption must not fight the result overlay for this screen.
            Assert.IsFalse(bootstrap.TutorialGuidanceCaptionActiveForTests, "Requirement 5: the shared caption must stay hidden once the result overlay owns the screen.");
            StringAssert.Contains("Return home", bootstrap.ResultTextForTests, "Requirement 5: a Campaign victory must state the next meaningful action.");
            StringAssert.Contains("next unlocked stage", bootstrap.ResultTextForTests, "Requirement 5: a Campaign victory must point at the next unlocked stage.");
        }

        [Test]
        public void Resolved_Defeat_StatesRetryCostsOneStamina()
        {
            var databaseGo = new GameObject("CampaignOnboarding_CardDatabase_Defeat");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-2");
            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(ApprovedStarterCollectionCardIds),
                activeDeckCardIds = new List<string>(ApprovedStarterCollectionCardIds),
                stamina = 100,
                unlockedStageIds = new List<string> { "1-1", "1-2" },
            };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the profile to save.");
            SaveSystem.ResetCurrentProfileForTests();

            GameBootstrap bootstrap2 = SpawnAndInitializeBootstrap("CampaignOnboarding_DefeatBootstrap2");
            HomePagePresenter home2 = SpawnHomePagePresenter(bootstrap2);
            home2.LaunchCampaignStageForTests(stage);

            Card weakest = bootstrap2.Battle.PlayerState.Hand.OrderBy(c => c.Attack).First();
            Assert.IsTrue(bootstrap2.Battle.TryPlayCard(bootstrap2.Battle.PlayerState, weakest, Lane.Back), "Setup: expected the player's one card to legally occupy Back.");
            foreach (Card enemyCard in bootstrap2.Battle.EnemyState.Hand.ToList())
            {
                Lane lane = bootstrap2.Battle.EnemyState.Lanes[Lane.Front].Cards.Count < LaneState.MaxSlots ? Lane.Front : Lane.Middle;
                bootstrap2.Battle.TryPlayCard(bootstrap2.Battle.EnemyState, enemyCard, lane);
            }
            Assert.IsTrue(bootstrap2.Battle.ConfirmFormation(), "Setup: expected the Formation to lock legally.");

            int ticksRun = 0;
            while (bootstrap2.Battle.Phase == BattlePhase.Combat)
            {
                bootstrap2.Battle.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a defeat well inside the tick cap.");
            }
            bootstrap2.RefreshAllForTests();

            StringAssert.Contains("Retry costs 1 Stamina", bootstrap2.ResultTextForTests, "Requirement 5: a Campaign defeat must state the retry cost.");
            StringAssert.Contains("return home", bootstrap2.ResultTextForTests, "Requirement 5: a Campaign defeat must also offer returning home.");
        }

        [Test]
        public void Tutorial_NeverShowsCampaignOnboardingGuidance()
        {
            var databaseGo = new GameObject("CampaignOnboarding_CardDatabase_Tutorial");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveFreshStarterProfile();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignOnboarding_TutorialBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            home.LaunchCampaignStageForTests(stage); // sets _pendingCampaignStage, mid-attempt

            bootstrap.StartApprovedTutorialBattle();
            bootstrap.RefreshAllForTests();

            string text = bootstrap.TutorialGuidanceCaptionTextForTests;
            Assert.IsFalse(string.IsNullOrEmpty(text) && text.Contains("saved 10-card deck"), "Requirement: Tutorial must never show the Campaign onboarding copy.");
            StringAssert.DoesNotContain("saved 10-card deck", text ?? string.Empty, "Requirement: Tutorial's own scripted step captions must remain exactly as before, never the Campaign onboarding text.");
        }

        [Test]
        public void OrdinaryNormalBattle_KeepsExistingShorterCaptions_NotCampaignOnboardingText()
        {
            var databaseGo = new GameObject("CampaignOnboarding_CardDatabase_Normal");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            SaveFreshStarterProfile();
            // Grant + confirm a deck first so the normal path doesn't redirect to Deck Builder.
            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(ApprovedStarterCollectionCardIds),
                activeDeckCardIds = new List<string>(ApprovedStarterCollectionCardIds),
                stamina = 100,
            };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the profile to save.");
            SaveSystem.ResetCurrentProfileForTests();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("CampaignOnboarding_NormalBootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);

            home.OnToBattleClickedForTests(); // ordinary "To Battle" - no campaign stage attached

            string text = bootstrap.TutorialGuidanceCaptionTextForTests;
            Assert.AreEqual(
                GameBootstrap.NormalBattleModeLabel + "\nYour saved deck fills the hand. Tap Auto Formation to deploy a starting squad.",
                text,
                "Requirement: the ordinary Home 'To Battle' path must keep Soft mode + existing shorter caption - not the Campaign onboarding copy.");
        }
    }
}
