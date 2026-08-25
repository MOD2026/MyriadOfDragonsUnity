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
    /// REINFORCEMENT WINDOW CUE, 2026-08-22 - owner: make it obvious on-screen that ticks 4 and 8
    /// let the player deploy from hand. The cue already existed in production (GameBootstrap's
    /// existing turn/clash text, "REINFORCE! N/12" vs "Clash N/12", driven directly by
    /// BattleController.IsReinforcementWindowOpen - a plain bool property, already testable with
    /// no UI dependency) but had zero EditMode coverage locking it in. This file is that coverage:
    /// no production behavior changed beyond adding the one read-only GameBootstrap.TurnTextForTests
    /// accessor needed to observe it.
    ///
    /// Every combat test here overrides BattleController.StartMatch with a deliberately huge
    /// Avatar Health (100000) after the real Initialize()/StartApprovedTutorialBattle/Campaign
    /// entry already ran - the same direct-override technique CombatTickFeedTests already uses -
    /// so the match is guaranteed to still be in Combat at ticks 4 and 8 regardless of the real
    /// production HP/AI formulas, which is not this task's concern.
    /// </summary>
    public class ReinforcementWindowCueTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsReinforceCue_" + System.Guid.NewGuid().ToString("N"));
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

        private static Card MakeWeakCard(string id)
        {
            var data = new CardData
            {
                id = id, name = "Reinforce Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = 1,
            };
            return Card.FromData(data);
        }

        /// <summary>Overrides whatever match Initialize()/StartApprovedTutorialBattle/the Campaign
        /// entry already started with a huge-Health economy, deploys one weak card into Front (the
        /// only thing ConfirmFormation requires), and locks Formation - so the match survives at
        /// least 8 ticks regardless of real HP/AI formulas, which are not this task's concern.</summary>
        private static void OverrideWithSurvivableMatchAndConfirm(BattleController controller)
        {
            Card card = MakeWeakCard("reinforce_cue_card_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 100000);
            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);

            Card handCard = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, handCard, Lane.Front), "Setup: expected a legal placement.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
        }

        private static void AdvanceTo(BattleController controller, int tick)
        {
            while (controller.TickCount < tick)
            {
                controller.AdvanceCombatTick();
                Assert.AreEqual(BattlePhase.Combat, controller.Phase,
                    $"Setup: expected the huge-Health override to keep the match in Combat through tick {tick}.");
            }
        }

        [Test]
        public void Formation_ShowsThePlainFormationLabel_NeverTheReinforceCue()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReinforceCue_FormationBootstrap");
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase, "Setup: expected a fresh match to start in Formation.");

            bootstrap.RefreshAllForTests();

            Assert.AreEqual("Formation", bootstrap.TurnTextForTests);
        }

        [Test]
        public void Combat_AtNonReinforcementTick_ShowsThePlainClashText()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReinforceCue_PlainClashBootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            AdvanceTo(controller, 1);
            bootstrap.RefreshAllForTests();

            Assert.AreEqual("Clash 1/12", bootstrap.TurnTextForTests,
                "Tick 1 is not a reinforcement tick and must show the plain clash counter.");
        }

        [Test]
        public void Combat_AtTick4_ShowsTheReinforceCue()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReinforceCue_Tick4Bootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            AdvanceTo(controller, 4);
            bootstrap.RefreshAllForTests();

            Assert.IsTrue(controller.IsReinforcementWindowOpen, "Setup: expected tick 4 to be a real reinforcement window per BattleController.ReinforcementTicks.");
            Assert.AreEqual("REINFORCE! 4/12", bootstrap.TurnTextForTests,
                "Tick 4 must show the unmissable reinforcement cue on the existing turn text.");
        }

        [Test]
        public void Combat_AtTick8_ShowsTheReinforceCue()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReinforceCue_Tick8Bootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            AdvanceTo(controller, 8);
            bootstrap.RefreshAllForTests();

            Assert.IsTrue(controller.IsReinforcementWindowOpen, "Setup: expected tick 8 to be a real reinforcement window per BattleController.ReinforcementTicks.");
            Assert.AreEqual("REINFORCE! 8/12", bootstrap.TurnTextForTests);
        }

        [Test]
        public void Combat_ImmediatelyAfterTick4_RevertsToThePlainClashText()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReinforceCue_AfterTick4Bootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            AdvanceTo(controller, 4);
            bootstrap.RefreshAllForTests();
            Assert.AreEqual("REINFORCE! 4/12", bootstrap.TurnTextForTests, "Setup: expected the cue showing at tick 4.");

            controller.AdvanceCombatTick();
            bootstrap.RefreshAllForTests();

            Assert.IsFalse(controller.IsReinforcementWindowOpen, "Setup: expected tick 5 to not be a reinforcement tick.");
            Assert.AreEqual("Clash 5/12", bootstrap.TurnTextForTests,
                "The cue lasts exactly one tick - it must clear the very next tick, not linger.");
        }

        [Test]
        public void ReinforceCue_ClearsWhenANewMatchReturnsToFormation()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReinforceCue_ResetBootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            AdvanceTo(controller, 4);
            bootstrap.RefreshAllForTests();
            Assert.AreEqual("REINFORCE! 4/12", bootstrap.TurnTextForTests, "Setup: expected the cue showing before the reset.");

            // Reset Lineup routes through StartNewMatch (a "new match"), returning to a fresh
            // Formation - the production path, not the direct StartMatch override used above.
            bootstrap.ResetLineupForTests();
            bootstrap.RefreshAllForTests();

            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase, "Setup: expected Reset Lineup to return to Formation.");
            Assert.AreEqual("Formation", bootstrap.TurnTextForTests,
                "A new match must clear the reinforcement cue, not carry it over from the previous fight.");
        }

        [Test]
        public void TutorialMatch_AtTick4_AlsoShowsTheReinforceCue_ViaTheSameSharedRefreshPath()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReinforceCue_TutorialBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            Assert.IsTrue(bootstrap.IsTutorialMatch, "Setup: expected a tutorial match.");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            AdvanceTo(controller, 4);
            bootstrap.RefreshAllForTests();

            Assert.AreEqual("REINFORCE! 4/12", bootstrap.TurnTextForTests,
                "Tutorial must show the same reinforcement cue as a normal match - RefreshAll sets this unconditionally, with no tutorial-specific branch.");
        }

        [Test]
        public void CampaignMatch_AtTick8_AlsoShowsTheReinforceCue_ViaTheSameSharedRefreshPath()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReinforceCue_CampaignBootstrap");
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            bootstrap.SetPendingCampaignStageForNextMatch(stage);
            // SetBattleCanvasVisible(true) only re-runs StartNewMatch on a hidden->visible
            // transition - the canvas starts already visible after Initialize().
            bootstrap.SetBattleCanvasVisible(false);
            bootstrap.SetBattleCanvasVisible(true);
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a Campaign match, not Tutorial.");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            AdvanceTo(controller, 8);
            bootstrap.RefreshAllForTests();

            Assert.AreEqual("REINFORCE! 8/12", bootstrap.TurnTextForTests,
                "Campaign must show the same reinforcement cue as a normal match - RefreshAll sets this unconditionally, with no Campaign-specific branch.");
        }
    }
}
