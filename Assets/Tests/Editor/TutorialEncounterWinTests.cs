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
    /// TUTORIAL ENCOUNTER WIN, 2026-08-16, updated for the guided step sequence - regression
    /// guard for the "lost the tutorial three times deploying all three starter cards" report.
    /// Proves that the exact approved starter Formation (warrior/Front, novice_knight/Middle,
    /// goblin_caster/Back), placed through the real guided-tutorial handlers (not a direct
    /// BattleController bypass - see TutorialGuidedSequenceTests for the full step-by-step
    /// version of this same proof), reaches an actual player victory via real combat resolution.
    /// </summary>
    public class TutorialEncounterWinTests
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
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null) _spawned.Add(spawned);
            }
            return bootstrap;
        }

        [Test]
        public void ApprovedThreeCardFormation_ReliablyWinsTheTutorialEncounter()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("TutorialWin_Bootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            // Real guided-tutorial handlers, in the only order the step gate permits - see
            // TutorialGuidedSequenceTests for a test asserting each individual step's gate.
            bootstrap.HandCardPressedForTests(controller.PlayerState.Hand.First(c => c.Id == "warrior"));
            bootstrap.LanePressedForTests(Lane.Front);
            bootstrap.HandCardPressedForTests(controller.PlayerState.Hand.First(c => c.Id == "novice_knight"));
            bootstrap.LanePressedForTests(Lane.Middle);
            bootstrap.HandCardPressedForTests(controller.PlayerState.Hand.First(c => c.Id == "goblin_caster"));
            bootstrap.LanePressedForTests(Lane.Back);

            bootstrap.StartBattleForTests();
            Assert.AreEqual(BattlePhase.Combat, controller.Phase,
                "Setup: expected the approved 3-card Formation to actually enter Combat.");
            Assert.AreEqual(TutorialStep.FirstCombatResult, bootstrap.TutorialStepForTests);

            bootstrap.TutorialContinueForTests(); // -> SpellLesson, grants the scripted Energy
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
                    "The approved tutorial encounter must resolve within the existing combat tick cap.");
            }

            Assert.AreEqual(BattlePhase.Resolved, controller.Phase);
            Assert.AreEqual("Victory. The first threat has been driven back.", bootstrap.ResultTextForTests,
                "The intended guided sequence must reliably win the tutorial encounter.");
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests, "A tutorial victory must show Return to Empire.");
            Assert.IsFalse(bootstrap.PlayAgainButtonActiveForTests, "A tutorial victory must not show Retry Battle.");
        }
    }
}
