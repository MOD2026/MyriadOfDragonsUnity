using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        private bool _reduceMotionBefore;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            _reduceMotionBefore = MotionPolicy.ReduceMotion;
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
            MotionPolicy.ReduceMotion = _reduceMotionBefore;
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
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

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
        public void TutorialOpening_UsesCanonicalGameplayCut_AndCompletesThroughVideoCallback()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_CanonicalGameplayOpeningBootstrap");

            bootstrap.StartApprovedTutorialBattle();

            Assert.AreEqual(GameBootstrap.OpeningGameplayVideoResourcePath,
                bootstrap.OpeningVideoResourcePathForTests,
                "Opening must bind the canonical gameplay cut, never the marketing master.");
            Assert.IsNotNull(Resources.Load<UnityEngine.Video.VideoClip>(GameBootstrap.OpeningGameplayVideoResourcePath),
                "The canonical gameplay cut must be imported at the stable Resources path.");
            Assert.IsNotNull(Resources.Load<Sprite>(GameBootstrap.OpeningLogoEndCardResourcePath),
                "The approved logo end card must be imported at the stable Resources path.");
            Assert.IsNotNull(GameObject.Find("CanonicalGameplayOpeningVideo"),
                "Opening must present the canonical gameplay cut through the runtime video surface.");

            bootstrap.CompleteOpeningVideoForTests();

            Assert.IsTrue(bootstrap.OpeningBridgeActiveForTests,
                "The additive bridge must begin only after the canonical gameplay video callback.");
            Assert.AreEqual(GameBootstrap.OpeningOptionalBridgeVideoResourcePath,
                bootstrap.OpeningVideoResourcePathForTests);
            Assert.IsNotNull(Resources.Load<UnityEngine.Video.VideoClip>(GameBootstrap.OpeningOptionalBridgeVideoResourcePath),
                "The approved optional bridge must be imported at its stable Resources path.");
            Assert.IsFalse(bootstrap.OpeningLogoEndCardActiveForTests,
                "The logo end card must wait for the optional bridge completion callback.");

            bootstrap.CompleteOpeningBridgeForTests();

            Assert.IsTrue(bootstrap.CinematicActiveForTests,
                "Bridge completion must enter the bounded logo end state before revealing Formation.");
            Assert.IsTrue(bootstrap.OpeningLogoEndCardActiveForTests);
            Assert.IsNotNull(GameObject.Find("Chapter1LogoEndCard"));

            bootstrap.CompleteOpeningLogoEndCardForTests();

            Assert.IsFalse(bootstrap.CinematicActiveForTests,
                "Logo end-card completion must reveal the existing Formation destination.");
            Assert.IsNull(GameObject.Find("Chapter1Cinematic"),
                "Logo end-card completion must tear down the cinematic overlay.");
        }

        [Test]
        public void TutorialOpening_OptionalBridge_SkipSuppressesLateCompletionAndReleasesOverlay()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_OptionalBridgeSkipBootstrap");
            bootstrap.StartApprovedTutorialBattle();

            bootstrap.CompleteOpeningVideoForTests();
            Assert.IsTrue(bootstrap.OpeningBridgeActiveForTests);

            bootstrap.SkipCinematicForTests();
            bootstrap.CompleteOpeningBridgeForTests();

            Assert.IsFalse(bootstrap.CinematicActiveForTests,
                "Skip must cancel the optional bridge instead of allowing a stale callback to continue the route.");
            Assert.IsFalse(bootstrap.OpeningLogoEndCardActiveForTests);
            Assert.IsNull(GameObject.Find("Chapter1Cinematic"),
                "Skipping the optional bridge must remove its input-blocking overlay.");
        }

        [Test]
        public void TutorialOpening_SkipAfterVideoCompletion_ClearsLogoEndCardWithoutChangingFormation()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_LogoEndCardSkipBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            BattlePhase phaseBefore = bootstrap.Battle.Phase;
            int handCountBefore = bootstrap.Battle.PlayerState.Hand.Count;

            bootstrap.CompleteOpeningVideoForTests();
            bootstrap.CompleteOpeningBridgeForTests();
            Assert.IsTrue(bootstrap.OpeningLogoEndCardActiveForTests);

            bootstrap.SkipCinematicForTests();

            Assert.IsFalse(bootstrap.CinematicActiveForTests);
            Assert.IsFalse(bootstrap.OpeningLogoEndCardActiveForTests);
            Assert.IsNull(GameObject.Find("Chapter1Cinematic"));
            Assert.AreEqual(phaseBefore, bootstrap.Battle.Phase);
            Assert.AreEqual(handCountBefore, bootstrap.Battle.PlayerState.Hand.Count);
        }

        [Test]
        public void TutorialOpening_VideoGraphicsDoNotInterceptControls()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_CanonicalGameplayInputBootstrap");
            bootstrap.StartApprovedTutorialBattle();

            GameObject cinematic = GameObject.Find("Chapter1Cinematic");
            Assert.IsNotNull(cinematic);
            UnityEngine.UI.RawImage videoImage = cinematic.GetComponentInChildren<UnityEngine.UI.RawImage>(true);
            Assert.IsNotNull(videoImage);
            Assert.IsFalse(videoImage.raycastTarget,
                "The canonical video surface must remain decorative; Skip owns the explicit input target.");
        }

        [Test]
        public void ReturningToCityDuringOpening_CancelsVideoAndLeavesNoStaleOverlay()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_ReturnDuringOpeningBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.CompleteOpeningVideoForTests();
            bootstrap.CompleteOpeningBridgeForTests();
            Assert.IsTrue(bootstrap.OpeningLogoEndCardActiveForTests);

            bootstrap.ReturnToCityForTests();

            Assert.IsFalse(bootstrap.CinematicActiveForTests);
            Assert.IsFalse(bootstrap.OpeningLogoEndCardActiveForTests);
            Assert.IsNull(GameObject.Find("Chapter1Cinematic"),
                "Return to City must tear down the video/logo overlay before hiding Battle.");
            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests);
        }

        [Test]
        public void DestroyingBootstrapDuringOpening_CancelsVideoAndLeavesNoStaleOverlay()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_DestroyDuringOpeningBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.CompleteOpeningVideoForTests();
            bootstrap.CompleteOpeningBridgeForTests();
            Assert.IsTrue(bootstrap.OpeningLogoEndCardActiveForTests);

            Assert.DoesNotThrow(() => Object.DestroyImmediate(bootstrap.gameObject));

            Assert.IsNull(GameObject.Find("Chapter1Cinematic"),
                "Destroying the owner must release the opening video overlay immediately.");
        }

        [Test]
        public void ReplayIntro_CancelsOpeningVideoBeforeReplayingNarrative()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_ReplayOpeningBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.CompleteOpeningVideoForTests();
            bootstrap.CompleteOpeningBridgeForTests();
            Assert.IsTrue(bootstrap.OpeningLogoEndCardActiveForTests);

            bootstrap.ReplayIntro();

            Assert.IsFalse(bootstrap.CinematicActiveForTests);
            Assert.IsFalse(bootstrap.OpeningLogoEndCardActiveForTests);
            Assert.IsNull(GameObject.Find("Chapter1Cinematic"),
                "Replay must not retain the previous opening video/logo overlay or callback.");
        }

        [Test]
        public void TutorialOpening_ReducedMotionCompletesImmediately_AndHandsOffToFormationGuidance()
        {
            bool previousReduceMotion = MotionPolicy.ReduceMotion;
            try
            {
                MotionPolicy.ReduceMotion = true;
                GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_ReducedMotionBootstrap");

                bootstrap.StartApprovedTutorialBattle();

                Assert.IsFalse(bootstrap.CinematicActiveForTests,
                    "Reduced motion must not leave a completed blocking cinematic active.");
                Assert.AreEqual(TutorialStep.CardCost, bootstrap.TutorialStepForTests,
                    "Reduced motion must hand control to the existing first tutorial step.");
                Assert.IsTrue(bootstrap.TutorialTeachingOverlayForTests.activeSelf,
                    "Formation guidance must be available immediately after the reduced-motion handoff.");
                Assert.IsNull(GameObject.Find("Chapter1Cinematic"),
                    "Reduced motion must not leave a cinematic overlay or input blocker behind.");
                Assert.IsFalse(bootstrap.OpeningLogoEndCardActiveForTests,
                    "Reduced motion must not enter the logo end-card state.");
            }
            finally
            {
                MotionPolicy.ReduceMotion = previousReduceMotion;
            }
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

        // ---------- Remaining Phase A behavior: reduced motion / determinism / reset / cleanup ----------

        private static GameObject FindCinematicOverlayUnderCanvas()
        {
            GameObject canvasGo = GameObject.Find("Canvas");
            if (canvasGo == null) return null;
            Transform found = canvasGo.transform.Find("Chapter1Cinematic");
            return found != null ? found.gameObject : null;
        }

        /// <summary>Drives the private CinematicSequence directly to natural completion the same
        /// way RunCinematic's coroutine would (Advance past the duration, then invoke the private
        /// completion handler it calls next) - coroutines never tick in EditMode (CLAUDE.md rule
        /// 6), so this is how a plain test proves the natural-completion path without Play Mode.</summary>
        private static void DriveActiveCinematicToNaturalCompletion(GameBootstrap bootstrap)
        {
            FieldInfo activeField = typeof(GameBootstrap).GetField("_activeCinematic", BindingFlags.Instance | BindingFlags.NonPublic);
            var active = (CinematicSequence)activeField.GetValue(bootstrap);
            Assert.IsNotNull(active, "Setup: expected an active cinematic to drive to completion.");
            active.Advance(active.DurationSeconds + 1f);
            Assert.IsTrue(active.IsComplete, "Setup: expected Advance past the full duration to complete the sequence.");

            MethodInfo complete = typeof(GameBootstrap).GetMethod("CompleteActiveCinematic", BindingFlags.Instance | BindingFlags.NonPublic);
            complete.Invoke(bootstrap, null);
        }

        [Test]
        public void NaturalCompletion_SkipAndReducedMotion_AllReachTheIdenticalFormationDestination()
        {
            MotionPolicy.ReduceMotion = false;
            GameBootstrap natural = SpawnAndInitializeBootstrap("Cinematic_NaturalCompletionBootstrap");
            natural.StartApprovedTutorialBattle();
            int handBefore = natural.Battle.PlayerState.Hand.Count;
            int frontLaneBefore = natural.Battle.PlayerState.Lanes[Lane.Front].Cards.Count;

            DriveActiveCinematicToNaturalCompletion(natural);

            Assert.IsFalse(natural.CinematicActiveForTests, "Natural completion must clear the cinematic.");
            Assert.AreEqual(BattlePhase.Formation, natural.Battle.Phase, "Natural completion must reach Formation.");
            Assert.AreEqual(handBefore, natural.Battle.PlayerState.Hand.Count, "Natural completion must not place any card.");
            Assert.AreEqual(frontLaneBefore, natural.Battle.PlayerState.Lanes[Lane.Front].Cards.Count, "Natural completion must not choose a lane.");

            MotionPolicy.ReduceMotion = false;
            GameBootstrap skipped = SpawnAndInitializeBootstrap("Cinematic_SkipCompletionBootstrap");
            skipped.StartApprovedTutorialBattle();
            skipped.SkipCinematicForTests();

            MotionPolicy.ReduceMotion = true;
            GameBootstrap reduced = SpawnAndInitializeBootstrap("Cinematic_ReducedMotionCompletionBootstrap");
            reduced.StartApprovedTutorialBattle();

            // All three completion paths must be indistinguishable from outside: the same
            // Formation phase with the same untouched hand/lane state, and no cinematic left
            // active - exactly what "same Formation destination" means for the player.
            foreach (GameBootstrap bootstrap in new[] { natural, skipped, reduced })
            {
                Assert.IsFalse(bootstrap.CinematicActiveForTests);
                Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase);
                Assert.AreEqual(handBefore, bootstrap.Battle.PlayerState.Hand.Count);
                Assert.AreEqual(frontLaneBefore, bootstrap.Battle.PlayerState.Lanes[Lane.Front].Cards.Count);
            }
        }

        [Test]
        public void SkipTutorial_WhileCinematicIsActivelyPlaying_StopsItAndHidesTheOverlay()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_InterruptedBySkipTutorialBootstrap");
            bootstrap.StartApprovedTutorialBattle();

            Assert.IsTrue(bootstrap.CinematicActiveForTests, "Setup: expected the opening cinematic to still be playing.");
            Assert.IsNotNull(FindCinematicOverlayUnderCanvas(), "Setup: expected the cinematic overlay to exist under Canvas.");

            bootstrap.SkipTutorialForTests();

            Assert.IsFalse(bootstrap.CinematicActiveForTests, "SKIP TUTORIAL must stop an actively-playing cinematic, not just a finished one.");
            Assert.IsNull(FindCinematicOverlayUnderCanvas(), "SKIP TUTORIAL must destroy the cinematic overlay, leaving nothing behind under Canvas.");
            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "SKIP TUTORIAL must still hide the battle canvas as normal.");
        }

        [Test]
        public void ReplayIntro_ClearsAnyCinematicAndLeavesNoOverlayBehind()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_ReplayResetBootstrap");
            bootstrap.StartApprovedTutorialBattle();

            Assert.IsTrue(bootstrap.CinematicActiveForTests, "Setup: expected the opening cinematic to still be playing.");
            Assert.IsNotNull(FindCinematicOverlayUnderCanvas(), "Setup: expected the cinematic overlay to exist under Canvas.");

            Assert.DoesNotThrow(() => bootstrap.ReplayIntro(),
                "ReplayIntro must clear a stale cinematic without throwing, even though other systems (narrative overlay) are being reset in the same call.");

            Assert.IsFalse(bootstrap.CinematicActiveForTests, "Replaying the intro must clear any stale cinematic.");
            Assert.IsNull(FindCinematicOverlayUnderCanvas(), "Replaying the intro must destroy any stale cinematic overlay.");
        }

        /// <summary>
        /// GameBootstrap.OnDestroy is a MonoBehaviour lifecycle hook, not a place for new logic
        /// (CLAUDE.md rule 6: "MonoBehaviours supply only timing"; every other Presenter in this
        /// codebase already follows the identical `OnDestroy() => TeardownUI()` shape). Its own
        /// body is two lines of glue calling already-tested logic (CancelPresentationEffects,
        /// CancelActiveCinematic - the latter separately proven correct by
        /// ReplayIntro_ClearsAnyCinematicAndLeavesNoOverlayBehind and every Skip/SkipTutorial test
        /// above). Real Play Mode dispatches OnDestroy synchronously on DestroyImmediate, but a
        /// headless EditMode test run does not: empirically confirmed here (during this task) with
        /// a call counter incremented only inside OnDestroy, which stayed at 0 immediately after
        /// DestroyImmediate(bootstrap.gameObject) returned - the same reason CLAUDE.md rule 6 calls
        /// coroutines "permanently untestable" in EditMode extends to this callback too. Invoking
        /// the hook directly proves its own glue is correct without depending on that dispatch
        /// timing, the same reflection-based pattern DriveActiveCinematicToNaturalCompletion above
        /// already uses for the coroutine Unity itself won't tick in EditMode.
        /// </summary>
        [Test]
        public void OnDestroy_ClearsAnyActiveCinematicAndLeavesNoOverlayBehind()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Cinematic_OnDestroyCleanupBootstrap");
            bootstrap.StartApprovedTutorialBattle();

            Assert.IsTrue(bootstrap.CinematicActiveForTests, "Setup: expected the opening cinematic to still be playing.");
            Assert.IsNotNull(FindCinematicOverlayUnderCanvas(), "Setup: expected the cinematic overlay to exist under Canvas.");

            MethodInfo onDestroy = typeof(GameBootstrap).GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic);
            onDestroy.Invoke(bootstrap, null);

            Assert.IsFalse(bootstrap.CinematicActiveForTests, "OnDestroy must clear an active cinematic.");
            Assert.IsNull(FindCinematicOverlayUnderCanvas(),
                "OnDestroy must destroy the cinematic overlay, leaving nothing behind under Canvas.");
        }
    }
}
