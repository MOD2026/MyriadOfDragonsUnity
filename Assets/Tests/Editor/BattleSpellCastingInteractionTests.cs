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
    /// CR spell-casting interaction pass, 2026-09-19. Drives the REAL spell rows (the Buttons that
    /// carry SpellIconPointerHandler) with real pointer events, so what is proven is the actual
    /// input binding -> OnSpellTapped -> BattleController.TryCastSpell path, not a test-only
    /// shortcut. Spell balance is never asserted as a magnitude: only relationships (Energy went
    /// down by the spell's own cost, cooldown was applied, nothing changed on a rejected tap).
    /// MOS section 14: UI owns presentation/input only - these tests fail if the UI ever starts
    /// deciding castability for itself instead of asking the controller.
    /// </summary>
    public class BattleSpellCastingInteractionTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDSpellInteraction_" + System.Guid.NewGuid().ToString("N"));
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

        /// <summary>Starts a survivable match with exactly these equipped spells, locks a legal
        /// formation, and lands in Combat with plenty of Energy - then refreshes the UI so the
        /// real spell rows reflect it.</summary>
        private static BattleController EnterCombat(GameBootstrap bootstrap, int energy, int ticks, params string[] spellIds)
        {
            BattleController controller = bootstrap.Battle;
            var data = new CardData
            {
                id = "spell_interaction_card", name = "Spell Interaction Card", art_file = "x.png",
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

        private static void PointerDown(Button row) =>
            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerDownHandler);

        private static void PointerUp(Button row) =>
            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerUpHandler);

        private static void PointerEnter(Button row) =>
            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerEnterHandler);

        private static void PointerExit(Button row) =>
            ExecuteEvents.Execute(row.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerExitHandler);

        /// <summary>A normal quick tap as a real pointer delivers it: down, up.</summary>
        private static void Tap(Button row)
        {
            PointerDown(row);
            PointerUp(row);
        }

        private static void ClickLane(Button lane) =>
            ExecuteEvents.Execute(lane.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);

        private static Button EnemyLaneButton(GameBootstrap bootstrap, Lane lane) =>
            bootstrap.EnemyLaneButtonRectForTests(lane).GetComponent<Button>();

        // ---------- Valid spell click invokes the real execution path ----------

        [Test]
        public void ValidAvatarStrikeTap_RunsTheRealCast_EnergySpent_CooldownApplied()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellInteraction_AvatarStrike");
            BattleController controller = EnterCombat(bootstrap, 100, BattleController.MinimumCombatTickForAvatarStrike, "divine_bolt");
            AvatarSpell spell = controller.Spellbook[0];
            int energyBefore = controller.Energy;

            Tap(bootstrap.SpellRowButtonForTests(0));

            Assert.AreEqual(1, controller.SpellCastLog.Count, "A tap on a castable spell row must run the real TryCastSpell path.");
            Assert.AreEqual(energyBefore - spell.EnergyCost, controller.Energy, "The cast must spend exactly the spell's own Energy cost.");
            Assert.Greater(spell.CooldownRemaining, 0, "The cast must apply the spell's cooldown.");
        }

        [Test]
        public void ValidLaneSpell_TapArmsTargeting_ThenEnemyLaneClickCasts()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellInteraction_LaneSpell");
            BattleController controller = EnterCombat(bootstrap, 100, 0, "firestorm");
            int energyBefore = controller.Energy;

            Tap(bootstrap.SpellRowButtonForTests(0));
            Assert.AreEqual(0, bootstrap.ArmedSpellIndexForTests, "Tapping a lane spell must arm targeting, not cast yet.");
            Assert.AreEqual(0, controller.SpellCastLog.Count, "Arming must not cast.");

            ClickLane(EnemyLaneButton(bootstrap, Lane.Front));

            Assert.AreEqual(1, controller.SpellCastLog.Count, "Clicking the highlighted lane must run the real cast.");
            Assert.AreEqual(-1, bootstrap.ArmedSpellIndexForTests, "Targeting must clear once the cast resolves.");
            Assert.AreEqual(energyBefore - controller.Spellbook[0].EnergyCost, controller.Energy);
        }

        // ---------- Invalid / insufficient-resource clicks do not mutate state ----------

        [Test]
        public void InsufficientEnergyTap_DoesNotArm_DoesNotCast_DoesNotChangeEnergyOrCooldown()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellInteraction_NoEnergy");
            BattleController controller = EnterCombat(bootstrap, 0, 0, "firestorm");

            Tap(bootstrap.SpellRowButtonForTests(0));

            Assert.AreEqual(-1, bootstrap.ArmedSpellIndexForTests, "An unaffordable spell must never enter targeting mode.");
            Assert.AreEqual(0, controller.SpellCastLog.Count);
            Assert.AreEqual(0, controller.Energy, "A rejected tap must not change Energy.");
            Assert.AreEqual(0, controller.Spellbook[0].CooldownRemaining, "A rejected tap must not start a cooldown.");
            StringAssert.Contains("Not enough Energy", bootstrap.HandHintTextForTests, "A rejection must explain itself.");
        }

        [Test]
        public void OnCooldownTap_DoesNotCastASecondTime_OrChangeEnergy()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellInteraction_Cooldown");
            BattleController controller = EnterCombat(bootstrap, 100, BattleController.MinimumCombatTickForAvatarStrike, "divine_bolt");
            Tap(bootstrap.SpellRowButtonForTests(0));
            Assert.AreEqual(1, controller.SpellCastLog.Count, "Setup: the first cast must succeed.");
            controller.SetEnergyForTutorial(100);
            int energyBefore = controller.Energy;

            Tap(bootstrap.SpellRowButtonForTests(0));

            Assert.AreEqual(1, controller.SpellCastLog.Count, "A spell on cooldown must not cast again.");
            Assert.AreEqual(energyBefore, controller.Energy, "A rejected tap must not spend Energy.");
            StringAssert.Contains("cooling down", bootstrap.HandHintTextForTests);
        }

        [Test]
        public void TapBeforeFormationLocks_DoesNotArmOrCast()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellInteraction_Formation");
            BattleController controller = bootstrap.Battle;
            Assert.AreEqual(BattlePhase.Formation, controller.Phase, "Setup: a fresh match starts in Formation.");
            controller.SetEnergyForTutorial(100);
            bootstrap.RefreshAllForTests();

            Tap(bootstrap.SpellRowButtonForTests(0));

            Assert.AreEqual(-1, bootstrap.ArmedSpellIndexForTests, "Spells are Combat-only: Formation must never arm targeting even with Energy available.");
            Assert.AreEqual(0, controller.SpellCastLog.Count);
            Assert.AreEqual(100, controller.Energy);
        }

        [Test]
        public void AvatarStrikeTapBeforeTheClashThreeGate_DoesNotCastOrSpendEnergy()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellInteraction_TooEarly");
            BattleController controller = EnterCombat(bootstrap, 100, 0, "divine_bolt");

            Tap(bootstrap.SpellRowButtonForTests(0));

            Assert.AreEqual(0, controller.SpellCastLog.Count, "The direct-strike safety rule must hold through the UI.");
            Assert.AreEqual(100, controller.Energy);
            Assert.AreEqual(0, controller.Spellbook[0].CooldownRemaining);
        }

        // ---------- Pointer semantics: hover / drag-off / exactly-one-tap ----------

        [Test]
        public void HoverWithoutPressing_NeverTapsTheSpell()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellInteraction_Hover");
            BattleController controller = EnterCombat(bootstrap, 100, 0, "firestorm");

            PointerEnter(bootstrap.SpellRowButtonForTests(0));
            PointerExit(bootstrap.SpellRowButtonForTests(0));

            Assert.AreEqual(-1, bootstrap.ArmedSpellIndexForTests, "Moving a mouse over and off a spell must never arm it.");
            Assert.AreEqual(0, controller.SpellCastLog.Count);
        }

        [Test]
        public void TapThenPointerLeaves_IsExactlyOneTap_NotTwo()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellInteraction_OneTap");
            BattleController controller = EnterCombat(bootstrap, 100, BattleController.MinimumCombatTickForAvatarStrike, "divine_bolt");

            Tap(bootstrap.SpellRowButtonForTests(0));
            PointerExit(bootstrap.SpellRowButtonForTests(0)); // a mouse leaving the tile after the click

            Assert.AreEqual(1, controller.SpellCastLog.Count);
            StringAssert.DoesNotContain("cooling down", bootstrap.HandHintTextForTests ?? string.Empty,
                "The pointer leaving after a click must not register a second tap (which would show a bogus cooldown rejection).");
        }

        [Test]
        public void PressThenDragOffThenRelease_IsCancelled()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellInteraction_DragOff");
            BattleController controller = EnterCombat(bootstrap, 100, BattleController.MinimumCombatTickForAvatarStrike, "divine_bolt");
            Button row = bootstrap.SpellRowButtonForTests(0);

            PointerDown(row);
            PointerExit(row);
            PointerUp(row);

            Assert.AreEqual(0, controller.SpellCastLog.Count, "Dragging off a spell before releasing must cancel the tap, as every other button does.");
        }

        // ---------- Disabled / unavailable controls never intercept ----------

        [Test]
        public void UnequippedSpellRows_AreInactive_AndActiveRowsStayInsideTheRail_WithoutOverlappingOtherButtons()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellInteraction_HitRegions");
            EnterCombat(bootstrap, 0, 0, "firestorm", "mend", "war_cry", "divine_bolt");
            GameObject canvasGo = GameObject.Find("Canvas");
            canvasGo.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);

            var spellRows = new List<Button>();
            for (int i = 0; i < bootstrap.SpellRailSlotCountForTests; i++)
            {
                Button row = bootstrap.SpellRowButtonForTests(i);
                if (i < 4) { Assert.IsTrue(row.gameObject.activeInHierarchy, $"Row {i} is equipped and must be active."); spellRows.Add(row); }
                else Assert.IsFalse(row.gameObject.activeInHierarchy, $"Row {i} has no equipped spell: it must be inactive so it can never be hit.");
            }

            Transform rail = bootstrap.BattlePresentationRootForTests.Find("SpellRail");
            Rect railBounds = Bounds((RectTransform)rail);
            foreach (Button row in spellRows)
            {
                Rect r = Bounds((RectTransform)row.transform);
                Assert.IsTrue(railBounds.Contains(r.min) && railBounds.Contains(r.max), $"'{row.name}' must sit inside the SpellRail.");
                foreach (Button other in bootstrap.BattlePresentationRootForTests.GetComponentsInChildren<Button>(false))
                {
                    if (spellRows.Contains(other) || other.transform.IsChildOf(rail)) continue;
                    if (!other.gameObject.activeInHierarchy) continue;
                    Assert.IsFalse(r.Overlaps(Bounds((RectTransform)other.transform)),
                        $"Spell row '{row.name}' must not overlap unrelated button '{other.name}'.");
                }
            }

            Image viewportImage = rail.Find("SpellRailViewport").GetComponent<Image>();
            Assert.IsFalse(viewportImage.raycastTarget, "The scroll viewport's mask graphic must never be a hit target of its own.");
        }

        [Test]
        public void ResolvedMatch_HidesTheWholeSpellRail_SoNoRowCanBeHit()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellInteraction_Resolved");
            BattleController controller = EnterCombat(bootstrap, 0, 0, "firestorm");
            controller.EnemyState.AvatarHealth = 1;
            int guard = 0;
            while (controller.Phase == BattlePhase.Combat && guard++ < BattleController.MaxCombatTicks) controller.AdvanceCombatTick();
            Assert.AreEqual(BattlePhase.Resolved, controller.Phase, "Setup: match must resolve.");
            bootstrap.RefreshAllForTests();

            for (int i = 0; i < bootstrap.SpellRailSlotCountForTests; i++)
                Assert.IsFalse(bootstrap.SpellRowButtonForTests(i).gameObject.activeInHierarchy, $"Row {i} must not be hittable once the match is resolved.");
        }

        // ---------- Spell controls remain usable across Battle canvas hide/show ----------

        [Test]
        public void HidingBattleWhileTargetingIsArmed_ClearsIt_AndSpellsWorkAfterShow()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellInteraction_HideShow");
            EnterCombat(bootstrap, 100, 0, "firestorm");
            Tap(bootstrap.SpellRowButtonForTests(0));
            Assert.AreEqual(0, bootstrap.ArmedSpellIndexForTests, "Setup: targeting armed.");

            bootstrap.SetBattleCanvasVisible(false);
            bootstrap.SetBattleCanvasVisible(true);

            Assert.AreEqual(-1, bootstrap.ArmedSpellIndexForTests, "A stale armed spell must not survive hide/show - it would hijack the next lane tap.");
            BattleController controller = EnterCombat(bootstrap, 100, BattleController.MinimumCombatTickForAvatarStrike, "divine_bolt");
            Tap(bootstrap.SpellRowButtonForTests(0));
            Assert.AreEqual(1, controller.SpellCastLog.Count, "Spell controls must work normally after the canvas is shown again.");
        }

        [Test]
        public void HidingBattleWhileHoldingASpell_DoesNotLeaveAStuckTooltip()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellInteraction_Tooltip");
            EnterCombat(bootstrap, 100, 0, "firestorm");
            bootstrap.SpellRowButtonForTests(0).GetComponent<SpellIconPointerHandler>().OnHoldStart?.Invoke();
            Assert.IsTrue(bootstrap.SpellTooltipActiveForTests, "Setup: holding a spell shows its tooltip.");

            bootstrap.SetBattleCanvasVisible(false);
            bootstrap.SetBattleCanvasVisible(true);

            Assert.IsFalse(bootstrap.SpellTooltipActiveForTests, "The hold-to-inspect tooltip must not reappear stuck on screen after hide/show.");
        }

        private static Rect Bounds(RectTransform rect)
        {
            var c = new Vector3[4];
            rect.GetWorldCorners(c);
            return new Rect(c.Min(v => v.x), c.Min(v => v.y), c.Max(v => v.x) - c.Min(v => v.x), c.Max(v => v.y) - c.Min(v => v.y));
        }
    }
}
