using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// SPELL_CATALOG_v1.md §2 "Direct-strike safety rule": an AvatarStrike spell cannot be cast
    /// before combat tick/clash 3 - it prevents a direct spell from ending an early fight before
    /// lane counterplay exists. This was a locked rule with no implementation anywhere (not even
    /// for the already-shipped Divine Bolt) until now. Proves the gate applies uniformly through
    /// BattleController.TryCastSpell/TryCastEnemySpell and the SpellAffordability hint mirror, and
    /// that it does not change any non-AvatarStrike spell's behavior at any tick.
    /// </summary>
    public class AvatarStrikeClashGateTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private static Card MakeWeakCard(string id) => Card.FromData(new CardData
        {
            id = id, name = "Clash Gate Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = 1,
        });

        private BattleController CreateController()
        {
            var go = new GameObject("ClashGateTestBattleController");
            _spawned.Add(go);
            return go.AddComponent<BattleController>();
        }

        /// <summary>Same direct-override technique other Combat-tick test files already use: a
        /// deliberately huge Avatar Health so the match survives well past clash 3 regardless of
        /// real production HP/AI formulas, which are not this task's concern.</summary>
        private BattleController StartSurvivableCombat()
        {
            BattleController controller = CreateController();
            Card card = MakeWeakCard("clash_gate_card_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 100000);
            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Front), "Setup: expected a legal placement.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
            return controller;
        }

        // ---------- BattleController.TryCastSpell (player path) ----------

        [Test]
        public void TryCastSpell_AvatarStrikeBeforeClash3_IsRejected()
        {
            BattleController controller = StartSurvivableCombat();
            int strikeIndex = controller.Spellbook.FindIndex(s => s.Effect == SpellEffect.AvatarStrike);
            Assert.GreaterOrEqual(strikeIndex, 0, "Setup: the default spellbook should contain an AvatarStrike spell (Divine Bolt).");
            controller.SetEnergyForTutorial(controller.Spellbook[strikeIndex].EnergyCost);

            Assert.Less(controller.TickCount, BattleController.MinimumCombatTickForAvatarStrike, "Setup: expected TickCount 0 immediately on Combat entry.");
            int healthBefore = controller.EnemyState.AvatarHealth;
            int energyBefore = controller.Energy;

            Assert.IsFalse(controller.TryCastSpell(strikeIndex, Lane.Front, out int dealt),
                "Divine Bolt must not be castable before clash 3, per SPELL_CATALOG_v1.md §2.");

            Assert.AreEqual(0, dealt, "A rejected cast must report zero damage dealt.");
            Assert.AreEqual(healthBefore, controller.EnemyState.AvatarHealth, "A rejected cast must not change the enemy Avatar's Health.");
            Assert.AreEqual(energyBefore, controller.Energy, "A rejected cast must not spend Energy.");
            Assert.IsTrue(controller.Spellbook[strikeIndex].IsOffCooldown, "A rejected cast must not put the spell on cooldown.");
        }

        [Test]
        public void TryCastSpell_AvatarStrikeAtClash3_Succeeds()
        {
            BattleController controller = StartSurvivableCombat();
            int strikeIndex = controller.Spellbook.FindIndex(s => s.Effect == SpellEffect.AvatarStrike);
            Assert.GreaterOrEqual(strikeIndex, 0, "Setup: the default spellbook should contain an AvatarStrike spell (Divine Bolt).");

            for (int i = 0; i < BattleController.MinimumCombatTickForAvatarStrike; i++) controller.AdvanceCombatTick();
            Assert.AreEqual(BattleController.MinimumCombatTickForAvatarStrike, controller.TickCount, "Setup: expected TickCount exactly at the gate boundary.");
            controller.SetEnergyForTutorial(controller.Spellbook[strikeIndex].EnergyCost);
            int healthBefore = controller.EnemyState.AvatarHealth;

            Assert.IsTrue(controller.TryCastSpell(strikeIndex, Lane.Front, out int dealt),
                "Divine Bolt must be castable exactly at clash 3, not only strictly after it.");

            Assert.Greater(dealt, 0, "A successful AvatarStrike should report the damage it dealt.");
            Assert.AreEqual(healthBefore - dealt, controller.EnemyState.AvatarHealth);
        }

        [Test]
        public void TryCastSpell_AvatarStrikeAfterClash3_Succeeds()
        {
            BattleController controller = StartSurvivableCombat();
            int strikeIndex = controller.Spellbook.FindIndex(s => s.Effect == SpellEffect.AvatarStrike);

            for (int i = 0; i < BattleController.MinimumCombatTickForAvatarStrike + 2; i++) controller.AdvanceCombatTick();
            controller.SetEnergyForTutorial(controller.Spellbook[strikeIndex].EnergyCost);

            Assert.IsTrue(controller.TryCastSpell(strikeIndex, Lane.Front, out int dealt),
                "Divine Bolt must remain castable well after clash 3.");
            Assert.Greater(dealt, 0);
        }

        // ---------- BattleController.TryCastEnemySpell (mirrored AI path) ----------

        [Test]
        public void TryCastEnemySpell_AvatarStrikeBeforeClash3_IsRejected()
        {
            BattleController controller = StartSurvivableCombat();
            int strikeIndex = controller.EnemySpellbook.FindIndex(s => s.Effect == SpellEffect.AvatarStrike);
            Assert.GreaterOrEqual(strikeIndex, 0, "Setup: the mirrored enemy spellbook should contain an AvatarStrike spell too.");
            controller.SetEnemyEnergyForTests(controller.EnemySpellbook[strikeIndex].EnergyCost);

            Assert.IsFalse(controller.TryCastEnemySpell(strikeIndex, Lane.Front, out int dealt),
                "The mirrored AI cast path must obey the same clash-3 gate as the player path.");
            Assert.AreEqual(0, dealt);
        }

        [Test]
        public void TryCastEnemySpell_AvatarStrikeAtClash3_Succeeds()
        {
            BattleController controller = StartSurvivableCombat();
            int strikeIndex = controller.EnemySpellbook.FindIndex(s => s.Effect == SpellEffect.AvatarStrike);

            for (int i = 0; i < BattleController.MinimumCombatTickForAvatarStrike; i++) controller.AdvanceCombatTick();
            controller.SetEnemyEnergyForTests(controller.EnemySpellbook[strikeIndex].EnergyCost);

            Assert.IsTrue(controller.TryCastEnemySpell(strikeIndex, Lane.Front, out int dealt),
                "The mirrored AI cast path must allow AvatarStrike from clash 3 onward, same as the player.");
            Assert.Greater(dealt, 0);
        }

        // ---------- Non-AvatarStrike spells are unaffected at any tick ----------

        [TestCase(SpellEffect.LaneDamage)]
        [TestCase(SpellEffect.LaneHeal)]
        [TestCase(SpellEffect.LaneAttackBuff)]
        public void TryCastSpell_NonAvatarStrikeSpell_IsCastableBeforeClash3(SpellEffect effect)
        {
            BattleController controller = StartSurvivableCombat();
            int index = controller.Spellbook.FindIndex(s => s.Effect == effect);
            Assert.GreaterOrEqual(index, 0, $"Setup: the default spellbook should contain a {effect} spell.");
            controller.SetEnergyForTutorial(controller.Spellbook[index].EnergyCost);

            Assert.Less(controller.TickCount, BattleController.MinimumCombatTickForAvatarStrike, "Setup: expected TickCount 0 immediately on Combat entry.");

            Assert.IsTrue(controller.TryCastSpell(index, Lane.Front, out _),
                $"A {effect} spell must remain castable before clash 3 - the gate is AvatarStrike-only.");
        }

        // ---------- SpellAffordability hint mirror ----------

        [Test]
        public void GetRejectReason_AvatarStrikeBeforeClash3_ReturnsTooEarlyForAvatarStrike()
        {
            var spell = new AvatarSpell("Test Bolt", "d", energyCost: 10, cooldownTicks: 1, SpellEffect.AvatarStrike, magnitude: 1);
            Assert.AreEqual(SpellAffordability.SpellCastRejectReason.TooEarlyForAvatarStrike,
                SpellAffordability.GetRejectReason(spell, BattlePhase.Combat, energy: 1000, tickCount: 0));
        }

        [Test]
        public void GetRejectReason_AvatarStrikeAtOrAfterClash3_ReturnsNone()
        {
            var spell = new AvatarSpell("Test Bolt", "d", energyCost: 10, cooldownTicks: 1, SpellEffect.AvatarStrike, magnitude: 1);
            Assert.AreEqual(SpellAffordability.SpellCastRejectReason.None,
                SpellAffordability.GetRejectReason(spell, BattlePhase.Combat, energy: 1000, tickCount: BattleController.MinimumCombatTickForAvatarStrike));
        }

        [Test]
        public void GetRejectReason_NonAvatarStrikeSpell_IgnoresTickCountEntirely()
        {
            var spell = new AvatarSpell("Test Bolt", "d", energyCost: 10, cooldownTicks: 1, SpellEffect.LaneDamage, magnitude: 1);
            Assert.AreEqual(SpellAffordability.SpellCastRejectReason.None,
                SpellAffordability.GetRejectReason(spell, BattlePhase.Combat, energy: 1000, tickCount: 0));
        }

        [Test]
        public void GetRejectReason_OmittedTickCount_DefaultsToAlwaysPastTheGate()
        {
            // Backward compatibility: every caller written before this rule existed must keep its
            // exact prior behaviour unless it explicitly opts in by passing a real tick count.
            var spell = new AvatarSpell("Test Bolt", "d", energyCost: 10, cooldownTicks: 1, SpellEffect.AvatarStrike, magnitude: 1);
            Assert.AreEqual(SpellAffordability.SpellCastRejectReason.None,
                SpellAffordability.GetRejectReason(spell, BattlePhase.Combat, energy: 1000));
        }

        [Test]
        public void DescribeRejectReason_TooEarlyForAvatarStrike_NamesTheSpellAndTheClashNumber()
        {
            var spell = new AvatarSpell("Divine Bolt", "d", energyCost: 60, cooldownTicks: 5, SpellEffect.AvatarStrike, magnitude: 100);
            string message = SpellAffordability.DescribeRejectReason(spell, SpellAffordability.SpellCastRejectReason.TooEarlyForAvatarStrike, energy: 1000);
            StringAssert.Contains("Divine Bolt", message);
            StringAssert.Contains($"{BattleController.MinimumCombatTickForAvatarStrike}", message);
        }

        [Test]
        public void IsCastable_AvatarStrikeBeforeClash3_IsFalse()
        {
            var spell = new AvatarSpell("Test Bolt", "d", energyCost: 10, cooldownTicks: 1, SpellEffect.AvatarStrike, magnitude: 1);
            Assert.IsFalse(SpellAffordability.IsCastable(spell, energy: 1000, tickCount: 0));
        }

        [Test]
        public void IsCastable_AvatarStrikeAtClash3_IsTrue()
        {
            var spell = new AvatarSpell("Test Bolt", "d", energyCost: 10, cooldownTicks: 1, SpellEffect.AvatarStrike, magnitude: 1);
            Assert.IsTrue(SpellAffordability.IsCastable(spell, energy: 1000, tickCount: BattleController.MinimumCombatTickForAvatarStrike));
        }

        [Test]
        public void IsCastable_OmittedTickCount_DefaultsToAlwaysPastTheGate()
        {
            var spell = new AvatarSpell("Test Bolt", "d", energyCost: 10, cooldownTicks: 1, SpellEffect.AvatarStrike, magnitude: 1);
            Assert.IsTrue(SpellAffordability.IsCastable(spell, energy: 1000));
        }
    }
}
