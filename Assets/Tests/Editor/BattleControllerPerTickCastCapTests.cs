using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Full 36-Spell Catalogue Diagnosis (LOCKED 2026-08-24): "max 1 successful cast per side per
    /// combat tick (player path currently has no equivalent guard to AI's - needs adding)." Proves
    /// the new guard on TryCastSpell/TryCastEnemySpell: a second successful cast in the same tick
    /// is rejected regardless of Energy/cooldown being otherwise satisfied, and the guard releases
    /// again once AdvanceCombatTick() moves TickCount forward.
    /// </summary>
    public class BattleControllerPerTickCastCapTests
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

        private static Card MakeWeakCard(string id)
        {
            var data = new CardData
            {
                id = id, name = "Cast Cap Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = 1,
            };
            return Card.FromData(data);
        }

        private BattleController CreateSurvivableMatch()
        {
            var go = new GameObject("CastCapTestBattleController");
            _spawned.Add(go);
            BattleController controller = go.AddComponent<BattleController>();
            Card card = MakeWeakCard("cast_cap_card_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 100000);
            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);

            Card handCard = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, handCard, Lane.Front), "Setup: expected a legal placement.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");

            for (int i = 0; i < BattleController.MinimumCombatTickForAvatarStrike; i++) controller.AdvanceCombatTick();
            return controller;
        }

        // ---------- Player path ----------

        [Test]
        public void TryCastSpell_SecondCastInSameTick_IsRejected()
        {
            BattleController controller = CreateSurvivableMatch();
            controller.SetEnergyForTutorial(200);

            Assert.IsTrue(controller.TryCastSpell(3, Lane.Front, out _), "Setup: the first cast (Divine Bolt) should succeed.");
            bool secondCastSucceeded = controller.TryCastSpell(3, Lane.Front, out _);

            Assert.IsFalse(secondCastSucceeded, "A second successful cast in the same combat tick must be rejected.");
        }

        [Test]
        public void TryCastSpell_AfterAdvancingATick_CastingIsAllowedAgain()
        {
            BattleController controller = CreateSurvivableMatch();
            controller.SetEnergyForTutorial(200);
            Assert.IsTrue(controller.TryCastSpell(3, Lane.Front, out _));
            Assert.IsFalse(controller.TryCastSpell(3, Lane.Front, out _));

            // Divine Bolt's own CooldownTicks (5) - advance past it too, so a leftover cooldown
            // can't be mistaken for the per-tick cast cap still being in effect.
            for (int i = 0; i < 5; i++) controller.AdvanceCombatTick();
            controller.SetEnergyForTutorial(200);

            Assert.IsTrue(controller.TryCastSpell(3, Lane.Front, out _), "A new combat tick must release the per-tick cast cap.");
        }

        [Test]
        public void TryCastSpell_RejectedAttemptDoesNotConsumeTheCastForTheTick()
        {
            BattleController controller = CreateSurvivableMatch();
            controller.SetEnergyForTutorial(0); // Firestorm (index 0) unaffordable - this attempt must fail on Energy, not the cap.

            Assert.IsFalse(controller.TryCastSpell(0, Lane.Front, out _), "Setup: expected the unaffordable cast to fail.");

            controller.SetEnergyForTutorial(200);
            Assert.IsTrue(controller.TryCastSpell(3, Lane.Front, out _),
                "A failed cast attempt must not itself count against the per-tick cap - only a successful cast may.");
        }

        // ---------- Mirrored enemy path ----------

        [Test]
        public void TryCastEnemySpell_SecondCastInSameTick_IsRejected()
        {
            BattleController controller = CreateSurvivableMatch();
            controller.SetEnemyEnergyForTests(200);

            Assert.IsTrue(controller.TryCastEnemySpell(3, Lane.Front, out _), "Setup: the first enemy cast should succeed.");
            bool secondCastSucceeded = controller.TryCastEnemySpell(3, Lane.Front, out _);

            Assert.IsFalse(secondCastSucceeded, "A second successful enemy cast in the same combat tick must be rejected.");
        }

        [Test]
        public void TryCastEnemySpell_AfterAdvancingATick_CastingIsAllowedAgain()
        {
            BattleController controller = CreateSurvivableMatch();
            controller.SetEnemyEnergyForTests(200);
            Assert.IsTrue(controller.TryCastEnemySpell(3, Lane.Front, out _));
            Assert.IsFalse(controller.TryCastEnemySpell(3, Lane.Front, out _));

            // Divine Bolt's own CooldownTicks (5) - advance past it too, so a leftover cooldown
            // can't be mistaken for the per-tick cast cap still being in effect.
            for (int i = 0; i < 5; i++) controller.AdvanceCombatTick();
            controller.SetEnemyEnergyForTests(200);

            Assert.IsTrue(controller.TryCastEnemySpell(3, Lane.Front, out _), "A new combat tick must release the per-tick cast cap for the enemy path too.");
        }

        [Test]
        public void PlayerAndEnemyCastCaps_AreIndependent()
        {
            BattleController controller = CreateSurvivableMatch();
            controller.SetEnergyForTutorial(200);
            controller.SetEnemyEnergyForTests(200);

            Assert.IsTrue(controller.TryCastSpell(3, Lane.Front, out _), "Setup: player cast should succeed.");
            Assert.IsTrue(controller.TryCastEnemySpell(3, Lane.Front, out _),
                "The player having already cast this tick must not block the enemy's own first cast of the same tick.");
        }
    }
}
