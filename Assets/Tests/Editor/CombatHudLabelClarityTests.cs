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
    /// COMBAT HUD LABEL CLARITY, 2026-08-22 - owner: current tick and Energy must always be
    /// obvious on the existing turn/energy HUD text, phone players must not guess. Audited both
    /// existing labels (GameBootstrap.RefreshAll's _turnText/_resourceText) and found them already
    /// clear: the Energy label already contains the literal word "Energy" with real numbers, and
    /// the turn label already shows an unambiguous "Clash N/12" tick counter - "Clash" (not
    /// "Tick") is this project's own established vocabulary for a combat turn throughout the MOS
    /// constitution ("12-clash cap", "reinforcement windows on clashes 4 and 8"),
    /// CombatFeedFormatter's own tick-feed lines, and the already-accepted, already-tested
    /// ReinforcementWindowCueTests contract ("Clash N/12" / "REINFORCE! N/12"). Renaming either
    /// label would both contradict that established vocabulary and break two already-shipped,
    /// already-tested contracts for no clarity gain - so per this task's own "or keep existing
    /// labels if already clear, then lock with EditMode tests only" fallback, no label text
    /// changed. This file is that lock: the one gap was zero direct EditMode coverage proving the
    /// Energy label specifically (as opposed to the turn label, already covered by
    /// ReinforcementWindowCueTests) is always clear and correct.
    /// </summary>
    public class CombatHudLabelClarityTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsHudClarity_" + System.Guid.NewGuid().ToString("N"));
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

        private static Card MakeWeakCard(string id)
        {
            var data = new CardData
            {
                id = id, name = "Hud Clarity Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = 1,
            };
            return Card.FromData(data);
        }

        /// <summary>Same direct-override technique ReinforcementWindowCueTests already uses: a
        /// deliberately huge Avatar Health so the match survives well past tick 1 regardless of
        /// real production HP/AI formulas, which are not this task's concern.</summary>
        private static void OverrideWithSurvivableMatchAndConfirm(BattleController controller)
        {
            Card card = MakeWeakCard("hud_clarity_card_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 100000);
            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);

            Card handCard = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, handCard, Lane.Front), "Setup: expected a legal placement.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
        }

        [Test]
        public void Formation_ShowsAClearlyLabeledResourceMeter_NotJustBareNumbers()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("HudClarity_FormationBootstrap");
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase, "Setup: expected a fresh match to start in Formation.");
            bootstrap.RefreshAllForTests();

            string text = bootstrap.ResourceOrEnergyTextForTests;
            StringAssert.Contains("Resource", text, "The Formation meter must name itself, not show bare numbers a phone player has to guess at.");
            StringAssert.Contains($"{bootstrap.Battle.PlayerState.Resource}", text, "The label must show the real current Resource value.");
            StringAssert.Contains($"{bootstrap.Battle.PlayerState.ResourceCap}", text, "The label must show the real Resource cap.");
        }

        [Test]
        public void Combat_ShowsAClearlyLabeledEnergyMeter_WithTheRealCurrentAndMaxValues()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("HudClarity_EnergyBootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            controller.AdvanceCombatTick();
            bootstrap.RefreshAllForTests();

            string text = bootstrap.ResourceOrEnergyTextForTests;
            StringAssert.Contains("Energy", text, "The Combat meter must literally say Energy - a phone player must not have to infer what the number means.");
            StringAssert.Contains($"{controller.Energy}", text, "The label must show the real current Energy value.");
            StringAssert.Contains($"{controller.MaxEnergy}", text, "The label must show the real Energy cap.");
        }

        [Test]
        public void Combat_EnergyLabel_UpdatesAsRealEnergyAccrues_NeverGoesStale()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("HudClarity_EnergyUpdatesBootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            controller.AdvanceCombatTick();
            bootstrap.RefreshAllForTests();
            string afterTick1 = bootstrap.ResourceOrEnergyTextForTests;

            controller.AdvanceCombatTick();
            bootstrap.RefreshAllForTests();
            string afterTick2 = bootstrap.ResourceOrEnergyTextForTests;

            Assert.AreNotEqual(afterTick1, afterTick2,
                "Energy accrues every tick (EnergyPerTick=18) - the label must actually change, not freeze at its first value.");
            StringAssert.Contains($"{controller.Energy}", afterTick2, "The label after the second tick must show that tick's real Energy value.");
        }

        [Test]
        public void Combat_TickLabel_UnambiguouslyShowsTheCurrentTickAndTheTwelveTickCap()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("HudClarity_TickLabelBootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            controller.AdvanceCombatTick();
            controller.AdvanceCombatTick();
            bootstrap.RefreshAllForTests();

            string text = bootstrap.TurnTextForTests;
            Assert.AreEqual(2, controller.TickCount, "Setup: expected exactly two resolved ticks.");
            StringAssert.Contains("2", text, "The tick label must show the real current tick number.");
            StringAssert.Contains($"/{BattleController.MaxCombatTicks}", text, "The tick label must show the fixed 12-tick cap alongside the current tick, so the player can see the fight is finite.");
        }

        [Test]
        public void Resolved_ResourceAndTickLabels_RemainNonEmptyStrings()
        {
            // Not "correct content for the result screen" (out of this task's scope - the result
            // overlay is the real result surface) - just proving neither label goes blank/null the
            // instant the match resolves, which would be a genuinely confusing empty HUD.
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("HudClarity_ResolvedBootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            // Force a resolution via the real tick-cap path rather than a knockout, so the huge
            // Avatar Health override above is irrelevant to reaching Resolved.
            for (int i = 0; i < BattleController.MaxCombatTicks; i++)
            {
                controller.AdvanceCombatTick();
            }
            bootstrap.RefreshAllForTests();

            Assert.AreEqual(BattlePhase.Resolved, controller.Phase, "Setup: expected the tick cap to resolve the match.");
            Assert.IsFalse(string.IsNullOrEmpty(bootstrap.TurnTextForTests), "The tick label must never go blank, even once the match resolves.");
            Assert.IsFalse(string.IsNullOrEmpty(bootstrap.ResourceOrEnergyTextForTests), "The resource/Energy label must never go blank, even once the match resolves.");
        }

        [Test]
        public void TutorialMatch_AlsoShowsTheClearEnergyLabel_ViaTheSameSharedRefreshPath()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("HudClarity_TutorialBootstrap");
            bootstrap.StartApprovedTutorialBattle();
            Assert.IsTrue(bootstrap.IsTutorialMatch, "Setup: expected a tutorial match.");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            controller.AdvanceCombatTick();
            bootstrap.RefreshAllForTests();

            StringAssert.Contains("Energy", bootstrap.ResourceOrEnergyTextForTests,
                "Tutorial must show the same clearly-labeled Energy meter as a normal match - RefreshAll sets this unconditionally, with no tutorial-specific branch.");
        }

        [Test]
        public void CampaignMatch_AlsoShowsTheClearEnergyLabel_ViaTheSameSharedRefreshPath()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("HudClarity_CampaignBootstrap");
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            bootstrap.SetPendingCampaignStageForNextMatch(stage);
            // SetBattleCanvasVisible(true) only re-runs StartNewMatch on a hidden->visible
            // transition - the canvas starts already visible after Initialize().
            bootstrap.SetBattleCanvasVisible(false);
            bootstrap.SetBattleCanvasVisible(true);
            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a Campaign match, not Tutorial.");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            controller.AdvanceCombatTick();
            bootstrap.RefreshAllForTests();

            StringAssert.Contains("Energy", bootstrap.ResourceOrEnergyTextForTests,
                "Campaign must show the same clearly-labeled Energy meter as a normal match - RefreshAll sets this unconditionally, with no Campaign-specific branch.");
        }

        [Test]
        public void HudLabelWork_DidNotChangeAnyLockedConstitutionNumber()
        {
            // Locked 2026-08-21, docs/MVP_COMBAT_PROGRESSION_CONSTITUTION_2026-08-21.md - this
            // task only adds coverage of already-shipped display strings, so these must read
            // exactly as Chapter1CombatBalanceAuditTests already asserts elsewhere.
            Assert.AreEqual(4, LaneBattleResolver.AvatarDamageMultiplier);
            Assert.AreEqual(0.06f, LaneBattleResolver.ExposedAvatarSiegeFraction);
            Assert.IsTrue(LaneBattleResolver.ExposedAvatarSiegeEnabled);
        }
    }
}
