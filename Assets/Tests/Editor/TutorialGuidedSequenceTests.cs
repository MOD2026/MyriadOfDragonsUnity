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
    /// CHAPTER 1 GUIDED TUTORIAL, 2026-08-16 - proves the TutorialStep state machine through the
    /// real production handlers (HandCardPressedForTests/LanePressedForTests/StartBattleForTests/
    /// SpellTappedForTests/SpellTargetLanePressedForTests/TutorialContinueForTests): each step
    /// allows only its one intended action, every wrong card/lane/spell/target is rejected and
    /// never advances the step, the intended sequence reaches a real engine-resolved victory, a
    /// normal match is completely unaffected, and the tutorial's existing no-reward/no-
    /// progression guarantee still holds.
    /// </summary>
    public class TutorialGuidedSequenceTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsTests_" + System.Guid.NewGuid().ToString("N"));
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

        private static Card Find(BattleController controller, string id) =>
            controller.PlayerState.Hand.First(c => c.Id == id);

        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("GuidedSequence_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            var sizing = new PlayerProfile();
            sizing.ApplyDataToEmpire();
            int deckSize = sizing.Empire.DeckSlotCount;
            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();
            Assert.AreEqual(deckSize, deckIds.Count);

            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(deckIds),
                activeDeckCardIds = new List<string>(deckIds),
            };
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();
            Object.DestroyImmediate(databaseGo);
        }

        [Test]
        public void Step1_OnlyWarriorSelectable_WrongCardBlockedAndStepUnchanged()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guided_Step1Bootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;
            Assert.AreEqual(TutorialStep.CardCost, bootstrap.TutorialStepForTests);

            bootstrap.HandCardPressedForTests(Find(controller, "novice_knight"));
            Assert.AreEqual(TutorialStep.CardCost, bootstrap.TutorialStepForTests, "A wrong card must not advance the step.");
            Assert.IsNull(bootstrap.SelectedCardIdForTests, "A wrong card must not even become selected.");

            bootstrap.LanePressedForTests(Lane.Front);
            Assert.AreEqual(TutorialStep.CardCost, bootstrap.TutorialStepForTests, "No lane is legal in step 1 - a lane tap must not advance it.");
            Assert.AreEqual(0, controller.PlayerState.Lanes[Lane.Front].Cards.Count, "No lane tap in step 1 may place a card.");

            bootstrap.HandCardPressedForTests(Find(controller, "warrior"));
            Assert.AreEqual(TutorialStep.FrontLane, bootstrap.TutorialStepForTests, "The one approved card must advance the step.");
            Assert.AreEqual("warrior", bootstrap.SelectedCardIdForTests);
        }

        [Test]
        public void Step2_OnlyFrontLaneAccepts_WrongLaneBlockedAndStepUnchanged()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guided_Step2Bootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;
            bootstrap.HandCardPressedForTests(Find(controller, "warrior"));
            Assert.AreEqual(TutorialStep.FrontLane, bootstrap.TutorialStepForTests);

            bootstrap.LanePressedForTests(Lane.Back);
            Assert.AreEqual(TutorialStep.FrontLane, bootstrap.TutorialStepForTests, "A wrong lane must not advance step 2.");
            Assert.AreEqual(0, controller.PlayerState.Lanes[Lane.Back].Cards.Count, "A wrong lane must not place the card.");

            bootstrap.LanePressedForTests(Lane.Front);
            Assert.AreEqual(TutorialStep.MiddleLane, bootstrap.TutorialStepForTests, "The one approved lane must advance the step.");
            Assert.AreEqual(1, controller.PlayerState.Lanes[Lane.Front].Cards.Count);
            Assert.AreEqual("warrior", controller.PlayerState.Lanes[Lane.Front].Cards[0].Definition.Id);
        }

        [Test]
        public void Step3_OnlyNoviceKnightThenMiddle_WrongCardAndWrongLaneBlocked()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guided_Step3Bootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;
            bootstrap.HandCardPressedForTests(Find(controller, "warrior"));
            bootstrap.LanePressedForTests(Lane.Front);
            Assert.AreEqual(TutorialStep.MiddleLane, bootstrap.TutorialStepForTests);

            bootstrap.HandCardPressedForTests(Find(controller, "goblin_caster"));
            Assert.AreEqual(TutorialStep.MiddleLane, bootstrap.TutorialStepForTests, "A wrong card must not advance step 3.");
            Assert.IsNull(bootstrap.SelectedCardIdForTests);

            bootstrap.HandCardPressedForTests(Find(controller, "novice_knight"));
            Assert.AreEqual("novice_knight", bootstrap.SelectedCardIdForTests);

            bootstrap.LanePressedForTests(Lane.Back);
            Assert.AreEqual(TutorialStep.MiddleLane, bootstrap.TutorialStepForTests, "A wrong lane must not advance step 3.");
            Assert.AreEqual(0, controller.PlayerState.Lanes[Lane.Back].Cards.Count);

            bootstrap.LanePressedForTests(Lane.Middle);
            Assert.AreEqual(TutorialStep.BackLane, bootstrap.TutorialStepForTests);
            Assert.AreEqual("novice_knight", controller.PlayerState.Lanes[Lane.Middle].Cards[0].Definition.Id);
        }

        [Test]
        public void Step4_OnlyGoblinCasterThenBack_WrongCardAndWrongLaneBlocked()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guided_Step4Bootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;
            bootstrap.HandCardPressedForTests(Find(controller, "warrior"));
            bootstrap.LanePressedForTests(Lane.Front);
            bootstrap.HandCardPressedForTests(Find(controller, "novice_knight"));
            bootstrap.LanePressedForTests(Lane.Middle);
            Assert.AreEqual(TutorialStep.BackLane, bootstrap.TutorialStepForTests);

            bootstrap.HandCardPressedForTests(Find(controller, "goblin_caster"));
            bootstrap.LanePressedForTests(Lane.Middle); // wrong lane - already occupied and not Back anyway
            Assert.AreEqual(TutorialStep.BackLane, bootstrap.TutorialStepForTests, "A wrong lane must not advance step 4.");

            bootstrap.LanePressedForTests(Lane.Back);
            Assert.AreEqual(TutorialStep.BeginBattle, bootstrap.TutorialStepForTests);
            Assert.AreEqual("goblin_caster", controller.PlayerState.Lanes[Lane.Back].Cards[0].Definition.Id);
            Assert.AreEqual(0, controller.PlayerState.Hand.Count, "Setup: all three starter cards should now be placed.");
        }

        private static GameBootstrap DeployApprovedFormation(GameBootstrap bootstrap, BattleController controller)
        {
            bootstrap.HandCardPressedForTests(Find(controller, "warrior"));
            bootstrap.LanePressedForTests(Lane.Front);
            bootstrap.HandCardPressedForTests(Find(controller, "novice_knight"));
            bootstrap.LanePressedForTests(Lane.Middle);
            bootstrap.HandCardPressedForTests(Find(controller, "goblin_caster"));
            bootstrap.LanePressedForTests(Lane.Back);
            return bootstrap;
        }

        [Test]
        public void Step5_OnlyStartBattleAccepts_NoLaneOrCardActionAvailable()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guided_Step5Bootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;
            DeployApprovedFormation(bootstrap, controller);
            Assert.AreEqual(TutorialStep.BeginBattle, bootstrap.TutorialStepForTests);
            Assert.AreEqual(BattlePhase.Formation, controller.Phase, "Setup: must still be in Formation before Start Battle.");

            bootstrap.LanePressedForTests(Lane.Front); // nothing left to select/place - must be a no-op
            Assert.AreEqual(BattlePhase.Formation, controller.Phase);
            Assert.AreEqual(TutorialStep.BeginBattle, bootstrap.TutorialStepForTests);

            bootstrap.StartBattleForTests();
            Assert.AreEqual(BattlePhase.Combat, controller.Phase, "Start Battle must be the one action step 5 accepts.");
            Assert.AreEqual(TutorialStep.FirstCombatResult, bootstrap.TutorialStepForTests);
        }

        [Test]
        public void SpellLesson_OnlyApprovedSpellAndTarget_WrongSpellAndWrongTargetBlocked()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guided_SpellLessonBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;
            DeployApprovedFormation(bootstrap, controller);
            bootstrap.StartBattleForTests();
            bootstrap.TutorialContinueForTests(); // -> SpellLesson
            Assert.AreEqual(TutorialStep.SpellLesson, bootstrap.TutorialStepForTests);
            int energyBefore = controller.Energy;
            Assert.AreEqual(30, energyBefore, "Setup: expected the scripted step-6->7 Energy grant.");

            // Wrong spell (any index other than 0/Firestorm) must be rejected outright.
            bootstrap.SpellTappedForTests(1);
            Assert.AreEqual(TutorialStep.SpellLesson, bootstrap.TutorialStepForTests, "A wrong spell must not advance the step.");
            Assert.AreEqual(energyBefore, controller.Energy, "A rejected spell tap must not spend Energy.");

            bootstrap.SpellTappedForTests(0); // arms Firestorm
            // Wrong target (Front/Back - already-cleared enemy lanes) must be rejected.
            bootstrap.SpellTargetLanePressedForTests(Lane.Front);
            Assert.AreEqual(TutorialStep.SpellLesson, bootstrap.TutorialStepForTests, "A wrong target must not advance the step.");
            Assert.AreEqual(energyBefore, controller.Energy, "A rejected target must not spend Energy.");

            bootstrap.SpellTargetLanePressedForTests(Lane.Middle); // the one scripted valid target
            Assert.AreEqual(TutorialStep.Finish, bootstrap.TutorialStepForTests, "The one approved spell+target must advance the step.");
            Assert.Less(controller.Energy, energyBefore, "The approved cast must actually spend Energy.");
        }

        [Test]
        public void IntendedSequence_ReachesGuaranteedVictoryThroughRealCombatResolution()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guided_FullSequenceBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            DeployApprovedFormation(bootstrap, controller);
            bootstrap.StartBattleForTests();
            Assert.AreEqual(TutorialStep.FirstCombatResult, bootstrap.TutorialStepForTests);
            Assert.AreEqual(BattlePhase.Combat, controller.Phase, "Setup: the scripted encounter must survive the first exchange.");

            bootstrap.TutorialContinueForTests();
            Assert.AreEqual(TutorialStep.SpellLesson, bootstrap.TutorialStepForTests);

            bootstrap.SpellTappedForTests(0);
            bootstrap.SpellTargetLanePressedForTests(Lane.Middle);
            Assert.AreEqual(TutorialStep.Finish, bootstrap.TutorialStepForTests);

            int continuesRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                bootstrap.TutorialContinueForTests();
                continuesRun++;
                Assert.LessOrEqual(continuesRun, BattleController.MaxCombatTicks,
                    "The scripted encounter must resolve within the existing tick cap.");
            }

            Assert.AreEqual(BattlePhase.Resolved, controller.Phase);
            Assert.AreEqual("Victory. The first threat has been driven back.", bootstrap.ResultTextForTests,
                "The intended guided sequence must reach a real victory, not merely a resolved match.");
        }

        [Test]
        public void NormalMatch_IsNeverGatedByTheTutorialStepMachine()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guided_NormalMatchBootstrap");
            // Initialize() already ran the normal (non-tutorial) StartNewMatch path.
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a normal match.");
            Assert.IsNull(bootstrap.TutorialStepForTests, "A normal match must never have a tutorial step.");

            BattleController controller = bootstrap.Battle;
            Assert.Greater(controller.PlayerState.Hand.Count, 0, "Setup: expected a dealt hand from the confirmed deck.");
            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
                {
                    if (controller.PlayerState.Lanes[lane].HasRoomFor(card))
                    {
                        bootstrap.HandCardPressedForTests(card);
                        bootstrap.LanePressedForTests(lane);
                        break;
                    }
                }
            }
            Assert.Greater(controller.PlayerState.Lanes.Values.Sum(l => l.Cards.Count), 0,
                "A normal match must accept ordinary card placement, unrestricted by any tutorial gate.");
            Assert.IsNull(bootstrap.TutorialStepForTests, "A normal match must still never acquire a tutorial step.");
        }

        [Test]
        public void TutorialDefeatAndRetry_StillWorkSafely_NoRewardOrProgressionChange()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Guided_DefeatRetryBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            PlayerProfile progressionProfile = bootstrap.Profile;
            int avatarLevelBefore = progressionProfile.avatarLevel;
            int totalMatchesBefore = progressionProfile.totalMatches;

            // Deliberately poor/invalid play, bypassing the guided gate the same way an
            // existing-behavior regression test would (direct BattleController calls) - proves
            // the underlying defeat/Retry mechanic the guided flow sits on top of is untouched,
            // even though the guided UI itself no longer permits reaching this by legitimate taps.
            Card weakestPlayerCard = controller.PlayerState.Hand.OrderBy(c => c.Attack).First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, weakestPlayerCard, Lane.Back));
            foreach (Card enemyCard in controller.EnemyState.Hand.ToList())
            {
                Lane lane = controller.EnemyState.Lanes[Lane.Front].Cards.Count < LaneState.MaxSlots ? Lane.Front : Lane.Middle;
                controller.TryPlayCard(controller.EnemyState, enemyCard, lane);
            }
            Assert.IsTrue(controller.ConfirmFormation());
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks);
            }

            Assert.AreEqual("Defeat. Adjust your formation and try again.", bootstrap.ResultTextForTests, "Setup: expected a tutorial defeat result.");
            Assert.AreEqual(avatarLevelBefore, progressionProfile.avatarLevel, "Tutorial defeat must not change progression.");
            Assert.AreEqual(totalMatchesBefore, progressionProfile.totalMatches, "Tutorial defeat must not change progression.");

            bootstrap.RetryForTests();

            Assert.AreEqual(BattlePhase.Formation, controller.Phase, "Retry must re-enter Formation.");
            Assert.AreEqual(TutorialStep.CardCost, bootstrap.TutorialStepForTests, "Retry must restart the guided sequence from step 1.");
            Assert.IsFalse(bootstrap.CinematicActiveForTests, "Retry must not replay the opening cinematic.");
            Assert.AreEqual(avatarLevelBefore, progressionProfile.avatarLevel, "Retry itself must not change progression.");
        }
    }
}
