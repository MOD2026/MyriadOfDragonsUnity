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
    /// CHAPTER 1 CINEMATIC, Phase A (2026-08-16) - proves the opening/victory cinematic overlay
    /// through the real production paths (StartApprovedTutorialBattle, HandleMatchEnded,
    /// OnCinematicSkipPressed): it shows only for the tutorial opening and a confirmed tutorial
    /// victory, never for a normal match or tutorial defeat, blocks input as a purely additive
    /// overlay on top of the already-fully-built Formation/result state (never gating or
    /// mutating that state itself), and Skip clears it without side effects.
    /// </summary>
    public class ChapterOneCinematicTests
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

        /// <summary>
        /// Normal/Campaign matches now require a confirmed valid deck (deck-block onboarding).
        /// Seed before Initialize so StartNewMatch actually deals a hand - same pattern as
        /// TutorialGuidanceTests.SaveValidDeckForNormalMatch after that contract shipped.
        /// </summary>
        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("ChapterOneCinematic_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

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

            Object.DestroyImmediate(databaseGo);
        }

        private static void RunToResolutionWithUndefendedEnemy(BattleController controller)
        {
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a knockout well inside the tick cap.");
            }
        }

        [Test]
        public void TutorialOpening_ShowsImmediatelyOverAnAlreadyFullyBuiltFormation()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_OpeningBootstrap");

            bootstrap.StartApprovedTutorialBattle();

            Assert.IsTrue(bootstrap.CinematicActiveForTests, "The opening cinematic must show immediately after Start Tutorial.");
            Assert.AreEqual(CinematicKind.Opening, bootstrap.CinematicKindForTests);
            // Purely additive: Formation itself must already be fully real underneath the
            // cinematic, not deferred until the cinematic finishes.
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase);
            Assert.AreEqual(3, bootstrap.Battle.PlayerState.Hand.Count, "Setup: expected the three tutorial starter cards already dealt.");
        }

        [Test]
        public void SkipOpening_ClearsTheCinematicWithoutAlteringFormationOrDoubleSkipping()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_SkipOpeningBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattleController controller = bootstrap.Battle;
            int handCountBefore = controller.PlayerState.Hand.Count;
            int frontLaneCountBefore = controller.PlayerState.Lanes[Lane.Front].Cards.Count;

            bootstrap.SkipCinematicForTests();

            Assert.IsFalse(bootstrap.CinematicActiveForTests, "Skip must immediately clear the cinematic.");
            Assert.AreEqual(BattlePhase.Formation, controller.Phase, "Skip must reach the same Formation state as natural completion, never start combat.");
            Assert.AreEqual(handCountBefore, controller.PlayerState.Hand.Count, "Skip must not place any card.");
            Assert.AreEqual(frontLaneCountBefore, controller.PlayerState.Lanes[Lane.Front].Cards.Count, "Skip must not choose a lane.");

            // "First accepted Skip request wins. Later Skip inputs are ignored" - a second Skip
            // once nothing is active must be a harmless no-op, not an error.
            Assert.DoesNotThrow(() => bootstrap.SkipCinematicForTests());
            Assert.IsFalse(bootstrap.CinematicActiveForTests);
        }

        [Test]
        public void TutorialRetryAfterDefeat_DoesNotReplayTheOpeningCinematic()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_RetryNoReplayBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            // Deterministic tutorial defeat: only the weakest card deployed to Back, enemy
            // deploys its whole hand into Front/Middle - same setup TutorialGuidanceTests uses.
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
            Assert.AreEqual("Retry Battle", bootstrap.PlayAgainLabelForTests, "Setup: expected a tutorial defeat result.");
            Assert.IsFalse(bootstrap.CinematicActiveForTests, "Tutorial defeat must never show a cinematic.");

            bootstrap.RetryForTests();

            Assert.IsFalse(bootstrap.CinematicActiveForTests, "A retry after defeat must re-enter Formation directly, not replay the opening.");
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase);
        }

        [Test]
        public void TutorialVictory_ShowsTheVictoryCinematicOverTheAlreadyActiveResultOverlay()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_TutorialVictoryBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

            foreach (Card card in controller.PlayerState.Hand.ToList())
            {
                Lane lane = card.Id switch { "warrior" => Lane.Front, "novice_knight" => Lane.Middle, _ => Lane.Back };
                controller.TryPlayCard(controller.PlayerState, card, lane);
            }
            RunToResolutionWithUndefendedEnemy(controller);

            Assert.IsTrue(bootstrap.CinematicActiveForTests, "A confirmed tutorial victory must show the victory cinematic.");
            Assert.AreEqual(CinematicKind.Victory, bootstrap.CinematicKindForTests);
            // Purely additive: the real result overlay (Return to Empire, approved victory copy)
            // must already be fully configured underneath the cinematic, unchanged from before
            // this feature existed - proves the cinematic never gates or replaces it.
            Assert.AreEqual("Victory. The first threat has been driven back.", bootstrap.ResultTextForTests);
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests);
            Assert.IsFalse(bootstrap.PlayAgainButtonActiveForTests);

            bootstrap.SkipCinematicForTests();
            Assert.IsFalse(bootstrap.CinematicActiveForTests);
            Assert.AreEqual("Victory. The first threat has been driven back.", bootstrap.ResultTextForTests,
                "Skip must reveal the same already-correct result screen, not alter it.");
        }

        [Test]
        public void TutorialDefeat_NeverShowsAnyCinematic()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_TutorialDefeatBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();
            BattleController controller = bootstrap.Battle;

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
            Assert.IsFalse(bootstrap.CinematicActiveForTests, "Tutorial defeat must never enter a cinematic.");
            Assert.IsNull(bootstrap.CinematicKindForTests);
        }

        [Test]
        public void NormalMatch_NeverShowsACinematic()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_NormalMatchBootstrap");
            // Initialize() already ran the normal (non-tutorial) StartNewMatch path.
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a normal match.");
            Assert.IsFalse(bootstrap.CinematicActiveForTests, "A normal match must never show a cinematic at start.");
            Assert.Greater(bootstrap.Battle.PlayerState.Hand.Count, 0,
                "Setup: expected a dealt hand from the confirmed deck (deck-block onboarding).");

            BattleController controller = bootstrap.Battle;
            Card card = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, card, Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat && ticksRun < BattleController.MaxCombatTicks)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.IsFalse(bootstrap.CinematicActiveForTests, "A normal match must never show a cinematic mid-combat.");
            }

            Assert.IsFalse(bootstrap.CinematicActiveForTests, "A normal match's result, win or lose, must never show a cinematic.");
        }

        /// <summary>
        /// GROUP B/A ISOLATION LOCK, 2026-08-21 (Command Centre vertical-slice task) - the
        /// cinematic system was already correctly gated behind IsTutorialMatch (BeginOpeningCinematic
        /// only fires from StartApprovedTutorialBattle; BeginVictoryCinematic only fires when
        /// IsTutorialMatch && playerWon), so a Campaign match was already structurally unable to
        /// reach it - but that was never directly proven for the Campaign path specifically before
        /// this task; NormalMatch_NeverShowsACinematic above only covers the ordinary Home "To
        /// Battle" entry. This closes that coverage gap so the live Chapter 1 vertical slice
        /// (Group A) can never silently start depending on the parked cinematic feature (Group B)
        /// without a test catching it.
        /// </summary>
        [Test]
        public void CampaignMatch_NeverShowsACinematic()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_CampaignMatchBootstrap");

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            bootstrap.SetPendingCampaignStageForNextMatch(stage);
            // SetBattleCanvasVisible(true) only re-runs StartNewMatch on a hidden->visible
            // transition - the canvas starts already visible after Initialize(), so it must be
            // hidden first for the pending Campaign stage to actually take effect.
            bootstrap.SetBattleCanvasVisible(false);
            bootstrap.SetBattleCanvasVisible(true);

            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a Campaign match, not Tutorial.");
            Assert.IsFalse(bootstrap.CinematicActiveForTests, "A Campaign match must never show a cinematic at start.");
            Assert.Greater(bootstrap.Battle.PlayerState.Hand.Count, 0,
                "Setup: expected a dealt hand from the confirmed deck (deck-block onboarding).");

            BattleController controller = bootstrap.Battle;
            Card card = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, card, Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());
            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat && ticksRun < BattleController.MaxCombatTicks)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.IsFalse(bootstrap.CinematicActiveForTests, "A Campaign match must never show a cinematic mid-combat.");
            }

            Assert.IsFalse(bootstrap.CinematicActiveForTests, "A Campaign match's result, win or lose, must never show a cinematic.");
        }
    }
}
