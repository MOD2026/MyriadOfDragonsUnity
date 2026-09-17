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
    /// CR-BATTLE-PRESENTATION-VISUAL-PASS-002, 2026-09-17 - "unused space" in the always-visible
    /// spell rail turned out to hide a real reachability bug, not just a cosmetic gap: the rail
    /// only ever built SpellLoadoutAutoEquip.EquipSlotOrder.Length-worth of rows (hardcoded 4),
    /// but SpellLoadoutAutoEquip.RequiredSlotCount grants 5 slots at Avatar L10 and 6 at L20 - a
    /// player's real, equipped 5th/6th spell had no rail row to appear in at all, so it could
    /// never be tapped, armed, or cast through the live Battle UI. The rail now builds
    /// SpellLoadoutAutoEquip.MaxSlotCount (6) rows; a 4-or-fewer loadout (every player below L10)
    /// renders identically to before - the extra rows simply stay inactive and take no layout
    /// space, proven here alongside the fixed case.
    /// </summary>
    public class BattleSpellRailSlotCountTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDSpellRailSlotCount_" + System.Guid.NewGuid().ToString("N"));
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

        private static BattleController StartSurvivableMatchWithSpellbook(GameBootstrap bootstrap, params string[] equippedSpellIds)
        {
            BattleController controller = bootstrap.Battle;
            var data = new CardData
            {
                id = "spell_rail_slot_card", name = "Spell Rail Slot Card", art_file = "x.png",
                element = "Andras", type = "warrior", rarity = 1,
            };
            Card card = Card.FromData(data);
            var economy = new BattleController.MatchEconomy(resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 100000);
            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy,
                equippedSpellIds: equippedSpellIds);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);

            Card handCard = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, handCard, Lane.Front), "Setup: expected a legal placement.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
            return controller;
        }

        [Test]
        public void RailBuildsExactlyMaxSlotCountRows()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellRailSlotCount_RowCountBootstrap");
            Assert.AreEqual(SpellLoadoutAutoEquip.MaxSlotCount, bootstrap.SpellRailSlotCountForTests,
                "STATE UNREACHED: the rail must build one row per SpellLoadoutAutoEquip.MaxSlotCount, not a smaller hardcoded number.");
        }

        [Test]
        public void FourSpellLoadout_RendersExactlyAsBefore_ExtraRowsInactive()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellRailSlotCount_FourSlotBootstrap");
            BattleController controller = StartSurvivableMatchWithSpellbook(
                bootstrap, "firestorm", "mend", "war_cry", "divine_bolt");
            Assert.AreEqual(4, controller.Spellbook.Count, "Setup: expected exactly the 4 equipped spells.");

            bootstrap.RefreshAllForTests();

            for (int i = 0; i < 4; i++)
                Assert.IsTrue(bootstrap.SpellRailRowActiveForTests(i), $"Row {i} must be active for a 4-spell loadout.");
            for (int i = 4; i < SpellLoadoutAutoEquip.MaxSlotCount; i++)
                Assert.IsFalse(bootstrap.SpellRailRowActiveForTests(i), $"Row {i} must stay inactive - no 5th/6th spell is equipped.");
        }

        [Test]
        public void SixSpellLoadout_TheFifthAndSixthSpell_AreReachableOnTheLiveRail()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellRailSlotCount_SixSlotBootstrap");
            BattleController controller = StartSurvivableMatchWithSpellbook(
                bootstrap, "firestorm", "mend", "war_cry", "divine_bolt", "ember_guard", "cleansing_root");
            Assert.AreEqual(6, controller.Spellbook.Count, "Setup: expected all 6 equipped spells to resolve.");

            bootstrap.RefreshAllForTests();

            Assert.IsTrue(bootstrap.SpellRailRowActiveForTests(4),
                "STATE UNREACHED: the 5th equipped spell (ember_guard) must have a real, active rail row.");
            Assert.IsTrue(bootstrap.SpellRailRowActiveForTests(5),
                "STATE UNREACHED: the 6th equipped spell (cleansing_root) must have a real, active rail row.");
        }

        [Test]
        public void SixthEquippedSpell_IsActuallyCastableThroughTheRealInputPath()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SpellRailSlotCount_SixthCastableBootstrap");
            BattleController controller = StartSurvivableMatchWithSpellbook(
                bootstrap, "firestorm", "mend", "war_cry", "divine_bolt", "ember_guard", "cleansing_root");
            Assert.AreEqual(SpellEffect.Cleanse, controller.Spellbook[5].Effect, "Setup: expected slot 5 to be Cleansing Root.");
            controller.SetEnergyForTutorial(100);

            bootstrap.SpellTappedForTests(5); // Previously unreachable - no rail row existed for index 5.
            bootstrap.LanePressedForTests(Lane.Front); // Cleanse is friendly-targeted.

            Assert.AreEqual(1, controller.SpellCastLog.Count,
                "STATE UNREACHED: the 6th equipped spell must be castable through the real Battle UI input path.");
        }
    }
}
