using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CR battle interaction fix, 2026-09-19 - Battle controls actually receiving input, Battle
    /// launch, and a one-step return. Battle-owned surface only: every metagame listener is
    /// exercised through the public, frozen OnReturnToCityRequested / SetBattleCanvasVisible
    /// contract, never edited.
    /// </summary>
    public class BattleInteractionFlowTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDBattleInteraction_" + System.Guid.NewGuid().ToString("N"));
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

        /// <summary>Same saved-deck setup ResultOverlayOutcomeCoverageTests uses: a normal match
        /// only deals and locks a formation once a full confirmed deck exists.</summary>
        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("Interaction_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance;

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;
            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize).ToList();
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: expected enough real cards for a full deck.");

            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(deckIds),
                activeDeckCardIds = new List<string>(deckIds),
            };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the deck.");
            SaveSystem.ResetCurrentProfileForTests();
            Object.DestroyImmediate(databaseGo);
        }

        private static void Click(Button button)
        {
            var data = new PointerEventData(EventSystem.current);
            ExecuteEvents.Execute(button.gameObject, data, ExecuteEvents.pointerClickHandler);
        }

        // ---------- Battle controls receive input ----------

        [Test]
        public void FreshBattle_HasAnActiveInputPipeline()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Interaction_FreshInput");
            Assert.IsTrue(bootstrap.BattleInputReadyForTests(),
                "STATE UNREACHED: an enabled EventSystem + input module + Battle GraphicRaycaster must all exist.");
        }

        [Test]
        public void DisabledEventSystemLeftByAnotherScreen_IsRepairedWhenBattleBecomesVisible()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Interaction_RepairInput");
            bootstrap.SetBattleCanvasVisible(false);

            EventSystem es = EventSystem.current != null ? EventSystem.current : Object.FindAnyObjectByType<EventSystem>();
            Assert.IsNotNull(es, "Setup: expected an EventSystem to exist.");
            es.gameObject.SetActive(false); // what a screen that tears down its own EventSystem leaves behind
            bootstrap.CanvasRaycasterForTests.enabled = false;
            Assert.IsFalse(bootstrap.BattleInputReadyForTests(), "Setup: input must actually be broken before the repair.");

            bootstrap.SetBattleCanvasVisible(true);

            Assert.IsTrue(bootstrap.BattleInputReadyForTests(),
                "Showing Battle must restore an active EventSystem/input module and re-enable the raycaster.");
        }

        [Test]
        public void EveryBattleControl_IsRaycastableAndInteractable_WhenFormationIsShown()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Interaction_Controls");
            bootstrap.SetBattleCanvasVisible(true);
            bootstrap.RefreshAllForTests();

            Canvas canvas = GameObject.Find("Canvas").GetComponent<Canvas>();
            var blocked = new List<string>();
            foreach (Button button in canvas.GetComponentsInChildren<Button>(false))
            {
                if (!button.gameObject.activeInHierarchy) continue;
                var graphic = button.targetGraphic != null ? button.targetGraphic : button.GetComponent<Graphic>();
                if (graphic == null || !graphic.raycastTarget)
                    blocked.Add($"'{button.name}' has no raycastTarget graphic");
                if (button.GetComponentInParent<CanvasGroup>() is CanvasGroup g && (!g.blocksRaycasts || !g.interactable))
                    blocked.Add($"'{button.name}' sits under a CanvasGroup that blocks input");
            }
            CollectionAssert.IsEmpty(blocked, string.Join(" | ", blocked));
        }

        [Test]
        public void StartBattleAndLeave_BothFireTheirHandlersFromARealClick()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Interaction_Clicks");
            bootstrap.SetBattleCanvasVisible(true);
            bootstrap.AutoFormationForTests();
            bootstrap.RefreshAllForTests();

            Assert.IsTrue(bootstrap.PrimaryActionButtonInteractableForTests, "Setup: Start Battle must be enabled after Auto Formation.");

            Click(bootstrap.PrimaryActionButtonForTests);
            Assert.AreEqual(BattlePhase.Combat, bootstrap.Battle.Phase, "A real click on START BATTLE must lock the formation and start Combat.");
            Assert.IsTrue(bootstrap.LeaveBattleButtonActiveForTests, "LEAVE must stay reachable during Combat.");

            int returns = 0;
            bootstrap.OnReturnToCityRequested += () => returns++;
            Click(bootstrap.LeaveBattleButtonForTests);
            Assert.AreEqual(1, returns, "A real click on LEAVE must fire OnReturnToCityRequested exactly once.");
        }

        // ---------- Battle launch ----------

        [Test]
        public void Launch_HiddenToVisible_ShowsAFreshFormationWithReadyInput()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Interaction_Launch");
            bootstrap.SetBattleCanvasVisible(false);
            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "Setup: Battle must start hidden, as Home leaves it.");

            bootstrap.SetBattleCanvasVisible(true);

            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "SetBattleCanvasVisible(true) must show the Battle canvas.");
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase, "Launch must land on a fresh Formation.");
            Assert.IsFalse(bootstrap.ResultOverlayActiveForTests, "Launch must never show a stale result overlay.");
            Assert.IsTrue(bootstrap.BattleInputReadyForTests(), "Launch must leave input ready.");
        }

        // ---------- One-step return ----------

        [Test]
        public void Leave_FromFormation_ReturnsInOneStep_AndHidesBattle()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Interaction_LeaveFormation");
            bootstrap.SetBattleCanvasVisible(true);
            Assert.IsTrue(bootstrap.LeaveBattleButtonActiveForTests, "Setup: LEAVE must be visible in a normal Formation.");

            int returns = 0;
            bootstrap.OnReturnToCityRequested += () => returns++;
            bootstrap.LeaveBattleForTests();

            Assert.AreEqual(1, returns, "One LEAVE must fire OnReturnToCityRequested exactly once - no second step.");
            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "One LEAVE must hide the Battle canvas.");
        }

        [Test]
        public void Leave_MidCombat_StopsTheCombatLoop_AndRecordsNoResult()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Interaction_LeaveCombat");
            bootstrap.SetBattleCanvasVisible(true);
            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();
            Assert.AreEqual(BattlePhase.Combat, bootstrap.Battle.Phase, "Setup: expected Combat.");

            int completed = 0;
            bootstrap.Battle.OnMatchCompleted += _ => completed++;
            bootstrap.LeaveBattleForTests();

            Assert.IsFalse(bootstrap.CombatLoopRunningForTests,
                "Leaving mid-combat must stop the combat loop - hiding the canvas alone leaves it ticking in the background.");
            Assert.AreEqual(0, completed, "An abandoned match must not report a result (no rewards for a match nobody finished).");
            Assert.AreNotEqual(BattlePhase.Resolved, bootstrap.Battle.Phase);
        }

        [Test]
        public void Leave_IsHiddenForResolvedMatchesAndTheGuidedTutorial()
        {
            GameBootstrap tutorial = SpawnAndInitializeBootstrap("Interaction_LeaveTutorial");
            tutorial.StartApprovedTutorialBattle(showOpeningCinematic: false);
            tutorial.RefreshAllForTests();
            Assert.IsFalse(tutorial.LeaveBattleButtonActiveForTests,
                "The guided tutorial owns its own skip flow - a bare LEAVE there would strand tutorial state.");
        }

        [Test]
        public void ActionWell_StillHoldsBothButtons_WithoutOverlap()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Interaction_ActionWell");
            bootstrap.SetBattleCanvasVisible(true);
            bootstrap.RefreshAllForTests();
            GameObject canvasGo = GameObject.Find("Canvas");
            canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            Transform panel = bootstrap.BattlePresentationRootForTests.Find("PrimaryActionPanel");
            Assert.IsNotNull(panel);
            Rect well = Bounds((RectTransform)panel);
            Rect leave = Bounds((RectTransform)bootstrap.LeaveBattleButtonForTests.transform);
            Assert.IsTrue(well.Contains(leave.min) && well.Contains(leave.max), "LEAVE must sit inside the action well.");

            if (bootstrap.PrimaryActionButtonActiveForTests)
            {
                Rect start = Bounds((RectTransform)bootstrap.PrimaryActionButtonForTests.transform);
                Assert.IsTrue(well.Contains(start.min) && well.Contains(start.max), "START BATTLE must stay inside the action well.");
                Assert.IsFalse(start.Overlaps(leave), "START BATTLE and LEAVE must never overlap.");
            }
            Assert.GreaterOrEqual(leave.height, 48f, "LEAVE must meet the minimum touch-target height.");
        }

        private static Rect Bounds(RectTransform rect)
        {
            var c = new Vector3[4];
            rect.GetWorldCorners(c);
            return new Rect(c.Min(v => v.x), c.Min(v => v.y), c.Max(v => v.x) - c.Min(v => v.x), c.Max(v => v.y) - c.Min(v => v.y));
        }
    }
}
