using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// COMBAT TICK FEED, 2026-08-22 - owner report: "combat after Formation feels like autopilot,
    /// I can't tell what happened each tick". Proves the data layer (LaneClashResult's new
    /// DefeatedCardNamesA/B, TurnResolutionResult/CombatTickRecord's new siege breakout,
    /// BattleController.SpellCastLog) and the plain-language formatter (CombatFeedFormatter)
    /// through real, already-tested combat resolution - no invented numbers, no UI/Play Mode
    /// dependency. Mirrors BattleLogicTests' own direct-construction patterns (Card.FromData,
    /// BattleCardInstance, LaneState) rather than reimplementing them.
    /// </summary>
    public class CombatTickFeedTests
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

        private BattleController CreateController()
        {
            var go = new GameObject("TestBattleController_TickFeed");
            _spawned.Add(go);
            return go.AddComponent<BattleController>();
        }

        private static Card MakeCard(string id, string displayName, int rarity)
        {
            var data = new CardData
            {
                id = id, name = displayName, art_file = "x.png", element = "Andras", type = "knight", rarity = rarity,
            };
            return Card.FromData(data);
        }

        [Test]
        public void ResolveLaneClash_CapturesTheDisplayNameOfEveryCardThatDiedThisClash()
        {
            Card weakDefender = MakeCard("feed_weak_defender", "Feed Weak Defender", rarity: 1);
            Card strongAttacker = MakeCard("feed_strong_attacker", "Feed Strong Attacker", rarity: 7);
            Assert.Greater(strongAttacker.Attack, weakDefender.Health,
                "Test setup invariant broken: rarity 7 Attack (7-12) should exceed rarity 1 Health (1-2).");

            var defendingLane = new LaneState(Lane.Front);
            defendingLane.Cards.Add(new BattleCardInstance(weakDefender, true, 0, 0));
            var attackingLane = new LaneState(Lane.Front);
            attackingLane.Cards.Add(new BattleCardInstance(strongAttacker, false, 0, 0));

            LaneClashResult result = LaneBattleResolver.ResolveLaneClash(defendingLane, attackingLane);

            Assert.IsTrue(result.SideACleared, "Test setup invariant broken: expected the weak defender's lane to clear.");
            CollectionAssert.AreEqual(new[] { "Feed Weak Defender" }, result.DefeatedCardNamesA,
                "The defeated defender's exact display name must be captured for this clash.");
            CollectionAssert.IsEmpty(result.DefeatedCardNamesB, "The surviving attacker must not be reported as defeated.");
        }

        [Test]
        public void DescribeTick_DeathLine_NamesTheRealDefeatedCard()
        {
            Card weakDefender = MakeCard("feed_weak_defender2", "Feed Weak Defender Two", rarity: 1);
            Card strongAttacker = MakeCard("feed_strong_attacker2", "Feed Strong Attacker Two", rarity: 7);
            var defendingLane = new LaneState(Lane.Front);
            defendingLane.Cards.Add(new BattleCardInstance(weakDefender, true, 0, 0));
            var attackingLane = new LaneState(Lane.Front);
            attackingLane.Cards.Add(new BattleCardInstance(strongAttacker, false, 0, 0));
            LaneClashResult laneResult = LaneBattleResolver.ResolveLaneClash(defendingLane, attackingLane);
            Assert.IsTrue(laneResult.SideACleared, "Test setup invariant broken: expected the defender's lane to clear.");

            var record = new CombatTickRecord(
                tickNumber: 1, damageToPlayerAvatar: 0, damageToEnemyAvatar: 0,
                playerAvatarHealthAfter: 100, enemyAvatarHealthAfter: 100,
                laneResults: new List<LaneClashResult> { laneResult });

            List<string> lines = CombatFeedFormatter.DescribeTick(record);

            Assert.IsTrue(lines.Any(l => l.Contains("Feed Weak Defender Two") && l.Contains("fell")),
                $"Expected a plain-language line naming the defeated card. Got: {string.Join(" | ", lines)}");
        }

        [Test]
        public void RealCombatTick_WithOverflow_ProducesANonEmptyFeedLine_MatchingTheRealDamageNumber()
        {
            // Same undefended-enemy-lane technique other test files use for a deterministic,
            // guaranteed overflow: the enemy has nothing deployed at all, so the player's Front
            // lane attack goes straight through to the enemy Avatar.
            var attackerCard = MakeCard("feed_overflow_attacker", "Feed Overflow Attacker", rarity: 4);

            BattleController controller = CreateController();
            controller.StartMatch(new List<Card> { attackerCard }, new List<Card>(),
                new BattleController.MatchEconomy(20, 20, 1000), new BattleController.MatchEconomy(20, 20, 1000));

            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);
            Card realCard = controller.PlayerState.Hand.FirstOrDefault();
            Assert.IsNotNull(realCard, "Setup: expected at least one card in the starting hand.");
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, realCard, Lane.Front),
                "Setup: expected to be able to deploy a card into Front.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");

            controller.AdvanceCombatTick();

            Assert.AreEqual(1, controller.CombatLedger.Count, "Setup: expected exactly one resolved tick.");
            CombatTickRecord record = controller.CombatLedger[0];
            Assert.Greater(record.DamageToEnemyAvatar, 0, "Setup: expected real overflow damage against the undefended enemy.");

            List<string> lines = CombatFeedFormatter.BuildFeedLines(controller.CombatLedger, controller.SpellCastLog);

            Assert.IsNotEmpty(lines, "A tick with overflow must produce at least one feed line.");
            Assert.IsTrue(lines.Any(l => l.Contains(record.DamageToEnemyAvatar.ToString()) && l.Contains("enemy Avatar")),
                $"Expected a feed line reporting the real overflow damage number ({record.DamageToEnemyAvatar}) against the enemy Avatar. Got: {string.Join(" | ", lines)}");
        }

        [Test]
        public void StartingANewMatch_ClearsBothTheCombatLedgerAndTheSpellCastLog()
        {
            var attackerCard = MakeCard("feed_reset_attacker", "Feed Reset Attacker", rarity: 4);
            BattleController controller = CreateController();
            controller.StartMatch(new List<Card> { attackerCard }, new List<Card>(),
                new BattleController.MatchEconomy(20, 20, 1000), new BattleController.MatchEconomy(20, 20, 1000));

            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);
            Card realCard = controller.PlayerState.Hand.FirstOrDefault();
            Assert.IsNotNull(realCard, "Setup: expected at least one card in the starting hand.");
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, realCard, Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());
            for (int i = 0; i < BattleController.MinimumCombatTickForAvatarStrike; i++) controller.AdvanceCombatTick(); // clear the clash-3 AvatarStrike gate
            controller.SetEnergyForTutorial(60);
            Assert.IsTrue(controller.TryCastSpell(3, Lane.Front, out _), "Setup: expected Divine Bolt (index 3, cost 60) to cast legally.");

            Assert.Greater(controller.CombatLedger.Count, 0, "Setup: expected at least one resolved tick before the reset.");
            Assert.Greater(controller.SpellCastLog.Count, 0, "Setup: expected at least one logged spell cast before the reset.");

            // A fresh match (Play Again / Retry / Reset Lineup all route through StartMatch) must
            // never carry the previous fight's feed data into the new one.
            controller.StartMatch(new List<Card>(), new List<Card>(),
                new BattleController.MatchEconomy(20, 20, 1000), new BattleController.MatchEconomy(20, 20, 1000));

            Assert.AreEqual(0, controller.CombatLedger.Count, "A new match must reset the combat ledger to empty.");
            Assert.AreEqual(0, controller.SpellCastLog.Count, "A new match must reset the spell cast log to empty.");
            CollectionAssert.IsEmpty(CombatFeedFormatter.BuildFeedLines(controller.CombatLedger, controller.SpellCastLog),
                "The feed itself must be empty immediately after a reset, before any tick of the new match has resolved.");
        }

        [Test]
        public void TryCastSpell_LogsARecordThatFormatsIntoAPlainLanguageLineWithTheRealDamage()
        {
            var attackerCard = MakeCard("feed_spell_attacker", "Feed Spell Attacker", rarity: 4);
            BattleController controller = CreateController();
            controller.StartMatch(new List<Card> { attackerCard }, new List<Card>(),
                new BattleController.MatchEconomy(20, 20, 1000), new BattleController.MatchEconomy(20, 20, 1000));
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);
            Card realCard = controller.PlayerState.Hand.FirstOrDefault();
            Assert.IsNotNull(realCard, "Setup: expected at least one card in the starting hand.");
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, realCard, Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());
            for (int i = 0; i < BattleController.MinimumCombatTickForAvatarStrike; i++) controller.AdvanceCombatTick(); // clear the clash-3 AvatarStrike gate

            controller.SetEnergyForTutorial(60);
            bool cast = controller.TryCastSpell(3, Lane.Front, out int avatarDamageDealt);

            Assert.IsTrue(cast, "Setup: expected Divine Bolt to cast legally with 60 Energy available.");
            Assert.AreEqual(1, controller.SpellCastLog.Count, "Expected exactly one logged spell cast.");
            SpellCastRecord logged = controller.SpellCastLog[0];
            Assert.AreEqual("Divine Bolt", logged.SpellName);
            Assert.AreEqual(avatarDamageDealt, logged.AvatarDamageDealt, "The logged damage must match what TryCastSpell actually reported.");

            string line = CombatFeedFormatter.DescribeSpellCast(logged);
            Assert.IsTrue(line.Contains("Divine Bolt"), $"Expected the feed line to name the spell. Got: {line}");
            Assert.IsTrue(line.Contains(avatarDamageDealt.ToString()), $"Expected the feed line to report the real damage number ({avatarDamageDealt}). Got: {line}");
        }

        [Test]
        public void CombatFeedWork_DidNotChangeAnyLockedConstitutionNumber()
        {
            // Locked 2026-08-21, docs/MVP_COMBAT_PROGRESSION_CONSTITUTION_2026-08-21.md - this
            // task only adds reporting of already-computed values, so these must read exactly as
            // Chapter1CombatBalanceAuditTests already asserts elsewhere in the full suite.
            Assert.AreEqual(4, LaneBattleResolver.AvatarDamageMultiplier);
            Assert.AreEqual(0.06f, LaneBattleResolver.ExposedAvatarSiegeFraction);
            Assert.IsTrue(LaneBattleResolver.ExposedAvatarSiegeEnabled);
            Assert.AreEqual(7, LaneBattleResolver.OvertimeStartTick);
            Assert.AreEqual(10, LaneBattleResolver.LateOvertimeStartTick);
        }
    }
}
