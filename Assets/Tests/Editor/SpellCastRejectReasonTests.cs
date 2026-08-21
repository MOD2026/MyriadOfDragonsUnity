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
    /// SPELL CAST REJECT REASON, 2026-08-22 - owner: tapping a spell that cannot be cast must show
    /// a clear reason (cooldown vs not enough Energy vs wrong phase), not the old blanket "not
    /// ready - not enough Energy, or still cooling down". Proves SpellAffordability's plain,
    /// testable GetRejectReason/DescribeRejectReason (mirroring BattleController.TryCastSpell's
    /// own phase/cooldown/Energy check order - no invented rule) and the real GameBootstrap wiring:
    /// the existing hand-hint/status text (ShowLaneHint -> _handHintText, no new panel) now shows
    /// the specific reason, and a successful cast leaves no stale rejection behind.
    /// </summary>
    public class SpellCastRejectReasonTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsRejectReason_" + System.Guid.NewGuid().ToString("N"));
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
                id = id, name = "Reject Reason Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = 1,
            };
            return Card.FromData(data);
        }

        /// <summary>Same direct-override technique other Combat-tick test files already use: a
        /// deliberately huge Avatar Health so the match survives well past a handful of ticks
        /// regardless of real production HP/AI formulas, which are not this task's concern.</summary>
        private static void OverrideWithSurvivableMatchAndConfirm(BattleController controller)
        {
            Card card = MakeWeakCard("reject_reason_card_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 100000);
            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);

            Card handCard = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, handCard, Lane.Front), "Setup: expected a legal placement.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
        }

        // ---------- Plain predicate ----------

        [Test]
        public void GetRejectReason_WrongPhase_WhenNotInCombat()
        {
            var spell = new AvatarSpell("Test Bolt", "d", energyCost: 10, cooldownTicks: 1, SpellEffect.AvatarStrike, magnitude: 1);
            Assert.AreEqual(SpellAffordability.SpellCastRejectReason.WrongPhase,
                SpellAffordability.GetRejectReason(spell, BattlePhase.Formation, energy: 1000));
        }

        [Test]
        public void GetRejectReason_OnCooldown_WhenNotOffCooldown()
        {
            var spell = new AvatarSpell("Test Bolt", "d", energyCost: 10, cooldownTicks: 1, SpellEffect.AvatarStrike, magnitude: 1);
            spell.PutOnCooldown();
            Assert.AreEqual(SpellAffordability.SpellCastRejectReason.OnCooldown,
                SpellAffordability.GetRejectReason(spell, BattlePhase.Combat, energy: 1000));
        }

        [Test]
        public void GetRejectReason_NotEnoughEnergy_WhenCostExceedsEnergy()
        {
            var spell = new AvatarSpell("Test Bolt", "d", energyCost: 50, cooldownTicks: 1, SpellEffect.AvatarStrike, magnitude: 1);
            Assert.AreEqual(SpellAffordability.SpellCastRejectReason.NotEnoughEnergy,
                SpellAffordability.GetRejectReason(spell, BattlePhase.Combat, energy: 10));
        }

        [Test]
        public void GetRejectReason_None_WhenCastable()
        {
            var spell = new AvatarSpell("Test Bolt", "d", energyCost: 10, cooldownTicks: 1, SpellEffect.AvatarStrike, magnitude: 1);
            Assert.AreEqual(SpellAffordability.SpellCastRejectReason.None,
                SpellAffordability.GetRejectReason(spell, BattlePhase.Combat, energy: 10));
        }

        [Test]
        public void DescribeRejectReason_NamesTheSpellAndTheSpecificReason()
        {
            var spell = new AvatarSpell("Firestorm", "d", energyCost: 30, cooldownTicks: 3, SpellEffect.LaneDamage, magnitude: 4);

            string wrongPhase = SpellAffordability.DescribeRejectReason(spell, SpellAffordability.SpellCastRejectReason.WrongPhase, energy: 0);
            StringAssert.Contains("Firestorm", wrongPhase);
            StringAssert.Contains("Combat", wrongPhase);

            spell.PutOnCooldown();
            string onCooldown = SpellAffordability.DescribeRejectReason(spell, SpellAffordability.SpellCastRejectReason.OnCooldown, energy: 100);
            StringAssert.Contains("Firestorm", onCooldown);
            StringAssert.Contains("cooling down", onCooldown);
            StringAssert.Contains($"{spell.CooldownRemaining}", onCooldown);

            string notEnoughEnergy = SpellAffordability.DescribeRejectReason(spell, SpellAffordability.SpellCastRejectReason.NotEnoughEnergy, energy: 5);
            StringAssert.Contains("Firestorm", notEnoughEnergy);
            StringAssert.Contains("Not enough Energy", notEnoughEnergy);
            StringAssert.Contains("30", notEnoughEnergy); // needs
            StringAssert.Contains("5", notEnoughEnergy);  // have

            // Regression proof: the old blanket message must be gone from every specific reason.
            string oldBlanket = "not ready yet - not enough Energy, or still cooling down";
            StringAssert.DoesNotContain(oldBlanket, wrongPhase);
            StringAssert.DoesNotContain(oldBlanket, onCooldown);
            StringAssert.DoesNotContain(oldBlanket, notEnoughEnergy);
        }

        // ---------- Real GameBootstrap wiring ----------

        [Test]
        public void OnSpellTapped_DuringFormation_ShowsTheWrongPhaseReason()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("RejectReason_FormationBootstrap");
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase, "Setup: expected a fresh match to start in Formation.");

            bootstrap.SpellTappedForTests(0); // Firestorm

            StringAssert.Contains("Firestorm", bootstrap.HandHintTextForTests);
            StringAssert.Contains("Combat", bootstrap.HandHintTextForTests);
            Assert.IsTrue(bootstrap.HandHintActiveForTests, "The reject reason must actually be visible, not just set while hidden.");
        }

        [Test]
        public void OnSpellTapped_WithInsufficientEnergy_ShowsTheNotEnoughEnergyReason()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("RejectReason_EnergyBootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);
            Assert.AreEqual(0, controller.Energy, "Setup: expected 0 Energy immediately on Combat entry.");

            bootstrap.SpellTappedForTests(0); // Firestorm, cost 30

            StringAssert.Contains("Firestorm", bootstrap.HandHintTextForTests);
            StringAssert.Contains("Not enough Energy", bootstrap.HandHintTextForTests);
            StringAssert.Contains("30", bootstrap.HandHintTextForTests);
        }

        [Test]
        public void OnSpellTapped_OnCooldown_ShowsTheCoolingDownReason()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("RejectReason_CooldownBootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            controller.SetEnergyForTutorial(60);
            bootstrap.SpellTappedForTests(3); // Divine Bolt (AvatarStrike) - casts immediately, no lane tap needed.
            Assert.AreEqual(1, controller.SpellCastLog.Count, "Setup: expected the first cast to succeed and go on cooldown.");

            controller.SetEnergyForTutorial(60); // Plenty of Energy again - cooldown is the only remaining blocker.
            bootstrap.SpellTappedForTests(3);

            StringAssert.Contains("Divine Bolt", bootstrap.HandHintTextForTests);
            StringAssert.Contains("cooling down", bootstrap.HandHintTextForTests);
        }

        [Test]
        public void CastSpellAt_ArmedThenEnergyDrainedBeforeTheLaneTap_ShowsTheNotEnoughEnergyReason()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("RejectReason_ArmedDrainedBootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            controller.SetEnergyForTutorial(30);
            bootstrap.SpellTappedForTests(0); // Firestorm (LaneDamage) - arms targeting, does not cast yet.

            // State changes between arming and the lane tap - CastSpellAt must re-derive the
            // reason at cast time, not trust whatever OnSpellTapped saw earlier.
            controller.SetEnergyForTutorial(0);
            bootstrap.SpellTargetLanePressedForTests(Lane.Front);

            StringAssert.Contains("Firestorm", bootstrap.HandHintTextForTests);
            StringAssert.Contains("Not enough Energy", bootstrap.HandHintTextForTests);
        }

        [Test]
        public void SuccessfulCast_LeavesNoStaleRejectHintBehind()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("RejectReason_ClearsBootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);

            // First produce a real reject hint.
            bootstrap.SpellTappedForTests(0); // Firestorm, 0 Energy - rejected.
            StringAssert.Contains("Not enough Energy", bootstrap.HandHintTextForTests);

            // Then a real successful cast (Divine Bolt, AvatarStrike - casts immediately).
            controller.SetEnergyForTutorial(60);
            bootstrap.SpellTappedForTests(3);

            Assert.AreEqual(1, controller.SpellCastLog.Count, "Setup: expected the cast to actually succeed.");
            Assert.IsFalse(bootstrap.HandHintActiveForTests,
                "A successful cast's own RefreshAll must hide the hand-hint surface during ordinary Combat (see RefreshHand), " +
                "so the earlier rejection can never be mistaken for still applying.");
        }

        [Test]
        public void RejectReasonWork_DidNotChangeAnyLockedConstitutionNumber()
        {
            // Locked 2026-08-21, docs/MVP_COMBAT_PROGRESSION_CONSTITUTION_2026-08-21.md - this
            // task only adds a rejection-message clarity pass, so these must read exactly as
            // Chapter1CombatBalanceAuditTests already asserts elsewhere.
            Assert.AreEqual(4, LaneBattleResolver.AvatarDamageMultiplier);
            Assert.AreEqual(0.06f, LaneBattleResolver.ExposedAvatarSiegeFraction);
            Assert.IsTrue(LaneBattleResolver.ExposedAvatarSiegeEnabled);
        }
    }
}
