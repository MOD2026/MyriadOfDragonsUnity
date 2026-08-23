using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Combat pacing redesign (2026-08-24): the owner wants CombatLoop's per-tick real-time delay
    /// removed, replaced by post-battle review - a replay screen (CombatFeedFormatter.
    /// BuildFullMatchLines) and a stats/analysis screen (MatchAnalyzer). Both are pure consumers of
    /// data BattleController already tracks (CombatLedger/SpellCastLog) - no new tracking
    /// infrastructure, confirmed by these tests driving a real match to completion and checking the
    /// analysis against manually-summed totals from the same real ledger/log, not invented numbers.
    /// </summary>
    public class MatchAnalyzerTests
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
            var go = new GameObject("TestBattleController_MatchAnalyzer");
            _spawned.Add(go);
            return go.AddComponent<BattleController>();
        }

        private static Card MakeCard(string id) => Card.FromData(new CardData
        {
            id = id, name = "Match Analyzer Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = 1,
        });

        private BattleController StartSurvivableCombat()
        {
            BattleController controller = CreateController();
            Card card = MakeCard("match_analyzer_card_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 100000);
            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());
            return controller;
        }

        [Test]
        public void Analyze_EmptyLedger_ReturnsAllZerosAndNoKeyMoments()
        {
            MatchAnalysis analysis = MatchAnalyzer.Analyze(new List<CombatTickRecord>(), new List<SpellCastRecord>());

            Assert.AreEqual(0, analysis.TicksResolved);
            Assert.AreEqual(0, analysis.TotalDamageToPlayerAvatar);
            Assert.AreEqual(0, analysis.TotalDamageToEnemyAvatar);
            CollectionAssert.IsEmpty(analysis.PlayerSpellTally);
            CollectionAssert.IsEmpty(analysis.EnemySpellTally);
            CollectionAssert.IsEmpty(analysis.KeyMoments);
        }

        [Test]
        public void Analyze_NullInputs_DoesNotThrow_TreatedAsEmpty()
        {
            MatchAnalysis analysis = MatchAnalyzer.Analyze(null, null);
            Assert.AreEqual(0, analysis.TicksResolved);
        }

        [Test]
        public void Analyze_RealMatch_DamageTotalsMatchManualSumOfTheRealLedger()
        {
            BattleController controller = StartSurvivableCombat();
            for (int i = 0; i < 6 && controller.Phase == BattlePhase.Combat; i++)
                controller.AdvanceCombatTick();

            Assert.Greater(controller.CombatLedger.Count, 0, "Setup: expected at least one resolved tick.");

            MatchAnalysis analysis = MatchAnalyzer.Analyze(controller.CombatLedger, controller.SpellCastLog);

            int expectedPlayerDamage = controller.CombatLedger.Sum(t => t.DamageToPlayerAvatar);
            int expectedEnemyDamage = controller.CombatLedger.Sum(t => t.DamageToEnemyAvatar);
            Assert.AreEqual(expectedPlayerDamage, analysis.TotalDamageToPlayerAvatar);
            Assert.AreEqual(expectedEnemyDamage, analysis.TotalDamageToEnemyAvatar);
            Assert.AreEqual(controller.CombatLedger.Count, analysis.TicksResolved);
        }

        [Test]
        public void Analyze_RealSpellCast_AppearsInThePlayerTallyWithCorrectCountAndDamage()
        {
            BattleController controller = StartSurvivableCombat();
            for (int i = 0; i < BattleController.MinimumCombatTickForAvatarStrike; i++)
                controller.AdvanceCombatTick();

            int strikeIndex = controller.Spellbook.FindIndex(s => s.Effect == SpellEffect.AvatarStrike);
            Assert.GreaterOrEqual(strikeIndex, 0, "Setup: expected an AvatarStrike spell in the default spellbook.");
            controller.SetEnergyForTutorial(controller.Spellbook[strikeIndex].EnergyCost);
            Assert.IsTrue(controller.TryCastSpell(strikeIndex, Lane.Front, out int dealt));
            Assert.Greater(dealt, 0, "Setup: expected the strike to deal real damage.");

            MatchAnalysis analysis = MatchAnalyzer.Analyze(controller.CombatLedger, controller.SpellCastLog);

            SpellCastTally tally = analysis.PlayerSpellTally.Single();
            Assert.AreEqual(controller.Spellbook[strikeIndex].Name, tally.SpellName);
            Assert.AreEqual(1, tally.TimesCast);
            Assert.AreEqual(dealt, tally.TotalAvatarDamage);
            CollectionAssert.IsEmpty(analysis.EnemySpellTally, "Setup: mirrored AI spells are off by default - no enemy casts.");
        }

        [Test]
        public void Analyze_RealSpellCastThatDealsDamage_ProducesAMatchingKeyMomentLine()
        {
            BattleController controller = StartSurvivableCombat();
            for (int i = 0; i < BattleController.MinimumCombatTickForAvatarStrike; i++)
                controller.AdvanceCombatTick();

            int strikeIndex = controller.Spellbook.FindIndex(s => s.Effect == SpellEffect.AvatarStrike);
            controller.SetEnergyForTutorial(controller.Spellbook[strikeIndex].EnergyCost);
            Assert.IsTrue(controller.TryCastSpell(strikeIndex, Lane.Front, out int dealt));

            MatchAnalysis analysis = MatchAnalyzer.Analyze(controller.CombatLedger, controller.SpellCastLog);

            string expectedLine = CombatFeedFormatter.DescribeSpellCast(controller.SpellCastLog.Single());
            CollectionAssert.Contains(analysis.KeyMoments, expectedLine,
                "A damage-dealing spell cast must appear in KeyMoments using the exact same formatted line as the live feed.");
        }

        [Test]
        public void Analyze_TwoCastsOfTheSameSpell_TalliesBothIntoOneEntry()
        {
            BattleController controller = StartSurvivableCombat();
            int damageIndex = controller.Spellbook.FindIndex(s => s.Effect == SpellEffect.LaneDamage);
            Assert.GreaterOrEqual(damageIndex, 0);
            AvatarSpell spell = controller.Spellbook[damageIndex];

            controller.SetEnergyForTutorial(spell.EnergyCost);
            Assert.IsTrue(controller.TryCastSpell(damageIndex, Lane.Front, out _));
            for (int i = 0; i < spell.CooldownTicks; i++) controller.AdvanceCombatTick();
            controller.SetEnergyForTutorial(spell.EnergyCost);
            Assert.IsTrue(controller.TryCastSpell(damageIndex, Lane.Front, out _));

            MatchAnalysis analysis = MatchAnalyzer.Analyze(controller.CombatLedger, controller.SpellCastLog);

            SpellCastTally tally = analysis.PlayerSpellTally.Single(t => t.SpellName == spell.Name);
            Assert.AreEqual(2, tally.TimesCast);
        }

        // ---------- CombatFeedFormatter.BuildFullMatchLines ----------

        [Test]
        public void BuildFullMatchLines_NullLedger_ReturnsEmpty()
        {
            CollectionAssert.IsEmpty(CombatFeedFormatter.BuildFullMatchLines(null, null));
        }

        [Test]
        public void BuildFullMatchLines_IsChronologicalOldestFirst_UnlikeTheCappedNewestFirstLiveFeed()
        {
            BattleController controller = StartSurvivableCombat();
            for (int i = 0; i < 5 && controller.Phase == BattlePhase.Combat; i++)
                controller.AdvanceCombatTick();
            Assert.GreaterOrEqual(controller.CombatLedger.Count, 5, "Setup: expected at least 5 resolved ticks.");

            List<string> full = CombatFeedFormatter.BuildFullMatchLines(controller.CombatLedger, controller.SpellCastLog);

            string firstTickLine = CombatFeedFormatter.DescribeTick(controller.CombatLedger[0])[0];
            Assert.AreEqual(firstTickLine, full[0], "The replay must start at Clash 1, not the most recent clash.");
        }

        [Test]
        public void BuildFullMatchLines_IsUncapped_IncludesEveryTickTheCappedLiveFeedWouldDrop()
        {
            BattleController controller = StartSurvivableCombat();
            for (int i = 0; i < 12 && controller.Phase == BattlePhase.Combat; i++)
                controller.AdvanceCombatTick();
            Assert.Greater(controller.CombatLedger.Count, 6, "Setup: expected more resolved ticks than the live feed's default 6-line cap.");

            List<string> full = CombatFeedFormatter.BuildFullMatchLines(controller.CombatLedger, controller.SpellCastLog);
            List<string> livefeed = CombatFeedFormatter.BuildFeedLines(controller.CombatLedger, controller.SpellCastLog);

            Assert.Greater(full.Count, livefeed.Count, "The uncapped replay must contain more lines than the capped live feed for a long match.");
            string earliestTickLine = CombatFeedFormatter.DescribeTick(controller.CombatLedger[0])[0];
            CollectionAssert.Contains(full, earliestTickLine, "The replay must retain the earliest clash the capped feed would have dropped.");
        }
    }
}
