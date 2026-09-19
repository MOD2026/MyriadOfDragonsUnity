using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CR exit/cast lifecycle pass, 2026-09-19. A Battle exit must be idempotent: repeated LEAVE,
    /// hide/show, and return callbacks may not resume combat, duplicate cleanup, duplicate casts,
    /// re-fire the return handoff, or record a result after the player has left. The frozen
    /// contract (OnReturnToCityRequested, OnMatchCompleted, MatchResult, SetBattleCanvasVisible)
    /// is exercised as a black box: counts of how often it fires, never its shape.
    /// </summary>
    public class BattleExitLifecycleTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDExitLifecycle_" + System.Guid.NewGuid().ToString("N"));
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

        private static BattleController EnterCombat(GameBootstrap bootstrap, int energy, int ticks, params string[] spellIds)
        {
            BattleController controller = bootstrap.Battle;
            var data = new CardData
            {
                id = "exit_lifecycle_card", name = "Exit Lifecycle Card", art_file = "x.png",
                element = "Andras", type = "warrior", rarity = 1,
            };
            Card card = Card.FromData(data);
            var economy = new BattleController.MatchEconomy(resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 100000);
            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy, equippedSpellIds: spellIds);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);
            Card handCard = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, handCard, Lane.Front), "Setup: legal placement.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: formation must lock.");
            for (int i = 0; i < ticks; i++) controller.AdvanceCombatTick();
            controller.SetEnergyForTutorial(energy);
            bootstrap.RefreshAllForTests();
            return controller;
        }

        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("ExitLifecycle_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance;
            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;
            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize).ToList();
            var profile = new PlayerProfile { cardCollection = new List<string>(deckIds), activeDeckCardIds = new List<string>(deckIds) };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the deck must persist.");
            SaveSystem.ResetCurrentProfileForTests();
            Object.DestroyImmediate(databaseGo);
        }

        private static void Tap(Button row)
        {
            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerUpHandler);
        }

        private sealed class Counters
        {
            public int Returns;
            public int Completed;
        }

        private static Counters Watch(GameBootstrap bootstrap)
        {
            var c = new Counters();
            bootstrap.OnReturnToCityRequested += () => c.Returns++;
            bootstrap.Battle.OnMatchCompleted += _ => c.Completed++;
            return c;
        }

        // ---------- Repeated LEAVE / return callbacks ----------

        [Test]
        public void RepeatedLeaveFromFormation_FiresTheReturnHandoffExactlyOnce()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Exit_RepeatFormation");
            Counters c = Watch(bootstrap);

            bootstrap.LeaveBattleForTests();
            bootstrap.LeaveBattleForTests();
            bootstrap.LeaveBattleForTests();

            Assert.AreEqual(1, c.Returns, "Three LEAVEs must produce one return - Home's handler must not run three times.");
            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests);
        }

        [Test]
        public void RepeatedLeaveMidCombat_AbandonsOnce_AndReturnsOnce()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Exit_RepeatCombat");
            BattleController controller = EnterCombat(bootstrap, 100, 0, "firestorm");
            Counters c = Watch(bootstrap);

            bootstrap.LeaveBattleForTests();
            bootstrap.LeaveBattleForTests();

            Assert.IsTrue(controller.IsAbandoned, "Leaving mid-Combat must mark the fight abandoned.");
            Assert.AreEqual(1, c.Returns);
            Assert.IsFalse(bootstrap.CombatLoopRunningForTests);
        }

        [Test]
        public void StaleReturnCallbacksAfterLeaving_CannotReShowHome()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Exit_StaleReturn");
            Counters c = Watch(bootstrap);
            bootstrap.LeaveBattleForTests();
            Assert.AreEqual(1, c.Returns, "Setup: the real return happened once.");

            bootstrap.ReturnToCityForTests(); // what the result overlay's button or a late listener would call
            bootstrap.ReturnToCityForTests();

            Assert.AreEqual(1, c.Returns,
                "A return callback arriving after the player already left must do nothing - it would otherwise re-show Home over whatever screen they moved on to.");
        }

        [Test]
        public void ResolvedMatch_ReturnedTwice_ReportsOneResultAndOneReturn()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Exit_ResolvedTwice");
            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();
            BattleController controller = bootstrap.Battle;
            Assert.AreEqual(BattlePhase.Combat, controller.Phase, "Setup: expected Combat.");
            Counters c = Watch(bootstrap);
            int matchesBefore = SaveSystem.CurrentProfile.totalMatches;

            controller.EnemyState.AvatarHealth = 1;
            int guard = 0;
            while (controller.Phase == BattlePhase.Combat && guard++ < BattleController.MaxCombatTicks) controller.AdvanceCombatTick();
            Assert.AreEqual(BattlePhase.Resolved, controller.Phase, "Setup: match must resolve.");

            bootstrap.ReturnToCityForTests();
            bootstrap.ReturnToCityForTests();

            Assert.AreEqual(1, c.Completed, "A finished match reports exactly one result.");
            Assert.AreEqual(1, c.Returns, "Pressing Return twice must return once.");
            Assert.AreEqual(matchesBefore + 1, SaveSystem.CurrentProfile.totalMatches, "The match is recorded exactly once.");
            Assert.IsFalse(controller.IsAbandoned, "A resolved match is finished, not abandoned.");
        }

        // ---------- Nothing resumes or records after leaving ----------

        [Test]
        public void AbandonedFight_CannotTickResolveOrRecordAResult()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Exit_NoResult");
            BattleController controller = EnterCombat(bootstrap, 100, 0, "firestorm");
            Counters c = Watch(bootstrap);
            int matchesBefore = SaveSystem.CurrentProfile.totalMatches;
            int winsBefore = SaveSystem.CurrentProfile.totalWins;
            bootstrap.LeaveBattleForTests();
            int tickBefore = controller.TickCount;

            controller.EnemyState.AvatarHealth = 1; // one tick from a win, if anything could still tick
            for (int i = 0; i < 12; i++) controller.AdvanceCombatTick();

            Assert.AreEqual(tickBefore, controller.TickCount, "An abandoned fight must not advance.");
            Assert.AreEqual(BattlePhase.Combat, controller.Phase, "It must not resolve.");
            Assert.AreEqual(0, c.Completed, "It must never report a result.");
            Assert.AreEqual(matchesBefore, SaveSystem.CurrentProfile.totalMatches, "No match may be recorded after leaving.");
            Assert.AreEqual(winsBefore, SaveSystem.CurrentProfile.totalWins);
            Assert.IsFalse(bootstrap.ResultOverlayActiveForTests, "No result overlay may appear after leaving.");
        }

        [Test]
        public void PostReturnSpellInput_IsIgnored_NoCast_NoEnergyChange_NoFeedback()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Exit_PostReturnSpells");
            BattleController controller = EnterCombat(bootstrap, 100, BattleController.MinimumCombatTickForAvatarStrike, "firestorm", "divine_bolt");
            bootstrap.SpellTappedForTests(0); // arm a lane spell, then leave with it still armed
            Assert.AreEqual(0, bootstrap.ArmedSpellIndexForTests, "Setup: targeting armed.");
            bootstrap.LeaveBattleForTests();
            int energyBefore = controller.Energy;
            string hintBefore = bootstrap.HandHintTextForTests;

            bootstrap.SpellTappedForTests(1);                       // a castable Avatar Strike, tapped after leaving
            bootstrap.SpellTappedForTests(0);
            bootstrap.SpellTargetLanePressedForTests(Lane.Front);   // the lane click of the armed spell
            bool controllerAccepted = controller.TryCastSpell(1, Lane.Front, out _);

            Assert.AreEqual(0, controller.SpellCastLog.Count, "No spell may cast after the player left.");
            Assert.AreEqual(energyBefore, controller.Energy, "No Energy may be spent after the player left.");
            Assert.IsFalse(controllerAccepted, "The controller itself must refuse casts on an abandoned fight (defence in depth behind the UI guard).");
            Assert.AreEqual(-1, bootstrap.ArmedSpellIndexForTests, "Nothing may stay armed after leaving.");
            Assert.AreEqual(hintBefore, bootstrap.HandHintTextForTests, "A tap on a match that is gone must not produce rejection feedback either.");
        }

        [Test]
        public void RapidDoubleTapOnACastableSpell_CastsOnce()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Exit_DoubleTap");
            BattleController controller = EnterCombat(bootstrap, 100, BattleController.MinimumCombatTickForAvatarStrike, "divine_bolt");
            int cost = controller.Spellbook[0].EnergyCost;
            int energyBefore = controller.Energy;

            Tap(bootstrap.SpellRowButtonForTests(0));
            Tap(bootstrap.SpellRowButtonForTests(0));

            Assert.AreEqual(1, controller.SpellCastLog.Count, "A duplicate tap must not duplicate the cast.");
            Assert.AreEqual(energyBefore - cost, controller.Energy, "Energy is spent once.");
        }

        // ---------- Hide / show ----------

        [Test]
        public void HidingMidCombat_AbandonsTheFight_AndHidingAgainIsANoOp()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Exit_HideTwice");
            BattleController controller = EnterCombat(bootstrap, 100, 0, "firestorm");
            Counters c = Watch(bootstrap);

            bootstrap.SetBattleCanvasVisible(false);
            Assert.IsTrue(controller.IsAbandoned, "Hiding Battle mid-Combat must stop the fight - nobody is watching it.");
            int tick = controller.TickCount;
            bootstrap.SetBattleCanvasVisible(false);
            controller.EnemyState.AvatarHealth = 1;
            for (int i = 0; i < 8; i++) controller.AdvanceCombatTick();

            Assert.AreEqual(tick, controller.TickCount);
            Assert.AreEqual(0, c.Completed, "A hidden, abandoned fight must not report a result.");
            Assert.AreEqual(0, c.Returns, "Hiding is not a return: it must not fire the return handoff.");
            Assert.IsFalse(bootstrap.CombatLoopRunningForTests);
        }

        [Test]
        public void HideShowCycles_EachShowStartsAFreshPlayableMatch()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Exit_Cycles");
            // A cast leaves a floating-text object; CancelPresentationEffects uses Destroy(), which logs
            // an Editor error outside Play mode (same accepted pattern as CombatPresentationInterruptionSafetyTests).
            // The count varies per cycle, so tolerate it for this test only and restore in finally.
            bool prior = LogAssert.ignoreFailingMessages;
            LogAssert.ignoreFailingMessages = true;
            try
            {
            for (int cycle = 0; cycle < 3; cycle++)
            {
                EnterCombat(bootstrap, 100, 0, "firestorm");
                bootstrap.SpellTappedForTests(0);
                bootstrap.SetBattleCanvasVisible(false);
                bootstrap.SetBattleCanvasVisible(true);

                BattleController controller = bootstrap.Battle;
                Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, $"Cycle {cycle}: Battle must be visible again.");
                Assert.AreEqual(BattlePhase.Formation, controller.Phase, $"Cycle {cycle}: a re-shown Battle is a fresh Formation, not the old fight resumed.");
                Assert.IsFalse(controller.IsAbandoned, $"Cycle {cycle}: the new match must not inherit the abandoned flag.");
                Assert.AreEqual(-1, bootstrap.ArmedSpellIndexForTests, $"Cycle {cycle}: nothing may stay armed.");
                Assert.AreEqual(0, controller.SpellCastLog.Count, $"Cycle {cycle}: no cast may leak across the hide/show.");

                controller = EnterCombat(bootstrap, 100, BattleController.MinimumCombatTickForAvatarStrike, "divine_bolt");
                Tap(bootstrap.SpellRowButtonForTests(0));
                Assert.AreEqual(1, controller.SpellCastLog.Count, $"Cycle {cycle}: spells must work after re-showing Battle.");
            }
            }
            finally { LogAssert.ignoreFailingMessages = prior; }
        }

        [Test]
        public void HideShow_LeavesTheGuidedTutorialResumable()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Exit_Tutorial");
            bootstrap.StartApprovedTutorialBattle(showOpeningCinematic: false);
            Assert.IsTrue(bootstrap.IsTutorialMatch, "Setup: tutorial running.");

            bootstrap.SetBattleCanvasVisible(false);
            bootstrap.SetBattleCanvasVisible(true);

            Assert.IsTrue(bootstrap.IsTutorialMatch, "The tutorial is stepped manually and must survive hide/show, not be abandoned.");
            Assert.IsFalse(bootstrap.Battle.IsAbandoned);
        }
    }
}
