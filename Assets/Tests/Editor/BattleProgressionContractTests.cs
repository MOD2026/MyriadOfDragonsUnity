using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Empire;
using MyriadOfDragons.UI;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// BATTLE-REMAINING-OWNER-DECISIONS-0.9-SIGNED.md (2026-09-02): the eight-milestone deck curve,
    /// Option A legacy handling, and the 4 + 3 hotkey split. Assertions are owner-signed numbers or
    /// relationships. Nothing here touches Save files: deck sizing is pure.
    /// </summary>
    public class BattleProgressionContractTests
    {
        private static readonly (int level, int slots)[] SignedCurve =
            { (1, 7), (3, 8), (5, 9), (8, 10), (10, 11), (15, 12), (20, 13), (30, 15) };

        [Test]
        public void DeckCurve_AllEightSignedMilestones_GrantTheSignedSlots()
        {
            Assert.AreEqual(8, SignedCurve.Length, "Setup: the contract has eight milestones.");
            foreach ((int level, int slots) in SignedCurve)
                Assert.AreEqual(slots, PlayerEmpireData.DeckSlotsForBarracksLevel(level), $"Barracks L{level}.");
        }

        [Test]
        public void DeckCurve_IsMonotonic_AndNeverExceedsTheBetaCap()
        {
            int previous = 0;
            for (int level = 1; level <= 100; level++)
            {
                int slots = PlayerEmpireData.DeckSlotsForBarracksLevel(level);
                Assert.GreaterOrEqual(slots, previous, $"L{level} must not shrink the deck.");
                Assert.LessOrEqual(slots, PlayerEmpireData.BetaDeckSlotCap, $"L{level} must respect the beta cap.");
                previous = slots;
            }
        }

        [Test]
        public void DeckCurve_DoesNotChangeTheConstructionLadder()
        {
            // No new economy rule: purchasable targets are still the paid milestone ladder.
            Assert.AreEqual(5, PlayerEmpireData.NextPaidBarracksMilestone(1));
            Assert.AreEqual(10, PlayerEmpireData.NextPaidBarracksMilestone(5));
            Assert.AreEqual(0, PlayerEmpireData.NextPaidBarracksMilestone(30));
        }

        [Test]
        public void OptionA_LegacyStoredValues_CapOnlyBattleSizing()
        {
            foreach (int legacy in new[] { 16, 18, 20 })
                Assert.AreEqual(PlayerEmpireData.BetaDeckSlotCap, PlayerEmpireData.BetaDeckSizeFor(legacy),
                    $"Battle sizes a new deck at the cap for a legacy stored {legacy}.");
        }

        [Test]
        public void OptionA_NewBetaDeckSize_IsNeverAboveFifteen_ForAnyStoredValue()
        {
            for (int stored = -3; stored <= 40; stored++)
            {
                int size = PlayerEmpireData.BetaDeckSizeFor(stored);
                Assert.LessOrEqual(size, 15);
                Assert.GreaterOrEqual(size, 0);
                if (stored >= 0 && stored <= 15) Assert.AreEqual(stored, size, "Values inside the cap pass through unchanged.");
            }
        }

        private static List<Card> MakeDeck(int count) =>
            Enumerable.Range(0, count).Select(i => Card.FromData(new CardData
            {
                id = "progression_card_" + i, name = "Progression " + i, art_file = "x.png",
                element = "Andras", type = "warrior", rarity = 1,
            })).ToList();

        [Test]
        public void OptionA_LegacySavedDecks_Of16_18_20_AreFieldedAtTheBetaSize_WithoutPadding()
        {
            foreach (int legacyCount in new[] { 16, 18, 20 })
            {
                List<Card> saved = MakeDeck(legacyCount);
                List<string> idsBefore = saved.Select(c => c.Id).ToList();

                Assert.IsTrue(GameBootstrap.TryFitSavedDeckToBattleSize(saved, PlayerEmpireData.BetaDeckSlotCap));

                Assert.AreEqual(PlayerEmpireData.BetaDeckSlotCap, saved.Count);
                CollectionAssert.AreEqual(idsBefore.Take(PlayerEmpireData.BetaDeckSlotCap).ToList(), saved.Select(c => c.Id).ToList(),
                    "Battle fields the first cards of the saved order, deterministically.");
            }
        }

        [Test]
        public void OptionA_IncompleteSavedDeck_StillFailsClosed()
        {
            List<Card> saved = MakeDeck(6);
            Assert.IsFalse(GameBootstrap.TryFitSavedDeckToBattleSize(saved, 7), "Fewer cards than the deck size is never padded.");
            Assert.AreEqual(6, saved.Count, "A failed fit leaves the list untouched.");
        }

        // ---------- 4 + 3 hotkeys ----------

        private static BattleController StartCombatMatch(int deckCount, int spellSlots)
        {
            var controller = new BattleController();
            List<Card> deck = MakeDeck(deckCount);
            var economy = new BattleController.MatchEconomy(resourceCap: 60, turn1Resource: 60, startingAvatarHealth: 100000);
            List<string> spellIds = AvatarSpell.CreateCatalog().Take(spellSlots).Select(s => s.Id).ToList();
            controller.StartMatch(deck, deck, economy, economy, equippedSpellIds: spellIds, rngSeed: 4242);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand[0], Lane.Front), "Setup: player deploy.");
            Assert.IsTrue(controller.TryPlayCard(controller.EnemyState, controller.EnemyState.Hand[0], Lane.Front), "Setup: enemy deploy.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: formation locks.");
            return controller;
        }

        private static void AdvanceToReinforcementWindow(BattleController controller)
        {
            int guard = 0;
            while (!controller.IsReinforcementWindowOpen && controller.Phase == BattlePhase.Combat && guard++ < 20)
                controller.AdvanceCombatTick();
            Assert.IsTrue(controller.IsReinforcementWindowOpen, "Setup: reach a reinforcement window.");
        }

        [Test]
        public void Hotkeys_LeftSide_ShowsAtMostFour_AndFewerWhenFewerEquipped()
        {
            Assert.AreEqual(4, BattleController.SkillHotkeyCapacity);
            Assert.AreEqual(4, StartCombatMatch(12, 6).SkillHotkeySpellIndices.Count, "Six equipped spells still bind only four hotkeys.");
            Assert.AreEqual(3, StartCombatMatch(12, 3).SkillHotkeySpellIndices.Count, "Three equipped spells bind three.");
        }

        [Test]
        public void Hotkeys_LeftSide_PlayerSelectionOverridesDefault_AndDuplicatesAreRejected()
        {
            BattleController controller = StartCombatMatch(12, 6);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, controller.SkillHotkeySpellIndices, "Default order is equipped order.");

            Assert.IsTrue(controller.TrySetSkillHotkey(0, 5), "Player may put the 6th equipped spell on hotkey 1.");
            Assert.AreEqual(5, controller.SkillHotkeySpellIndices[0]);
            Assert.AreEqual(4, controller.SkillHotkeySpellIndices.Distinct().Count(), "No spell appears on two hotkeys.");
            Assert.IsFalse(controller.TrySetSkillHotkey(1, 5), "A spell already selected on another hotkey is rejected.");
            Assert.IsFalse(controller.TrySetSkillHotkey(4, 1), "There is no fifth hotkey.");
            Assert.IsFalse(controller.TrySetSkillHotkey(0, 99), "Out-of-range spell is rejected.");
        }

        [Test]
        public void Hotkeys_RightSide_ThreeEntriesWhenReserveIsLarge_PlayerSelectable()
        {
            Assert.AreEqual(3, BattleController.ReinforcementHotkeyCapacity);
            BattleController controller = StartCombatMatch(20, 4);
            Assert.GreaterOrEqual(controller.EligibleReinforcementReserve.Count, 3, "Setup: a real reserve of 3+ cards.");
            Assert.AreEqual(3, controller.ReinforcementHotkeyEntries.Count);

            Card pick = controller.EligibleReinforcementReserve[controller.EligibleReinforcementReserve.Count - 1];
            Assert.IsTrue(controller.TrySelectReinforcementHotkey(1, pick));
            Assert.AreSame(pick, controller.ReinforcementHotkeyEntries[1], "The player-selected card occupies the chosen slot.");
            Assert.AreEqual(3, controller.ReinforcementHotkeyEntries.Distinct().Count(), "No card on two hotkeys.");
            Assert.IsFalse(controller.TrySelectReinforcementHotkey(0, pick), "A card already on another hotkey is rejected.");
        }

        [Test]
        public void Hotkeys_RightSide_FewerThanThreeEntries_WhenTheReserveIsSmaller()
        {
            BattleController one = StartCombatMatch(2, 4);
            Assert.AreEqual(1, one.EligibleReinforcementReserve.Count, "Setup: exactly one card left in reserve.");
            Assert.AreEqual(1, one.ReinforcementHotkeyEntries.Count, "One reserve card shows one entry, not three.");
            Assert.IsFalse(one.TrySelectReinforcementHotkey(1, one.EligibleReinforcementReserve[0]), "No slot exists beyond the reserve size.");

            BattleController two = StartCombatMatch(3, 4);
            Assert.AreEqual(2, two.ReinforcementHotkeyEntries.Count);
        }

        [Test]
        public void Hotkeys_RightSide_DeployUsesTheOrdinaryReinforcementRules()
        {
            BattleController controller = StartCombatMatch(12, 4);
            Card target = controller.ReinforcementHotkeyEntries[0];

            Assert.IsFalse(controller.TryDeployReinforcementFromHotkey(0, Lane.Middle), "Outside a window the hotkey deploy is refused, like the ordinary path.");
            Assert.IsTrue(controller.PlayerState.Hand.Contains(target), "A refused deploy changes nothing.");

            AdvanceToReinforcementWindow(controller);
            Assert.IsTrue(controller.TryDeployReinforcementFromHotkey(0, Lane.Middle), "Inside the window it deploys to the explicit lane.");
            Assert.IsFalse(controller.PlayerState.Hand.Contains(target));
            Assert.IsTrue(controller.PlayerState.Lanes[Lane.Middle].Cards.Any(c => c.Definition == target), "It landed in the lane the player chose.");
            Assert.IsFalse(controller.TryDeployReinforcementFromHotkey(9, Lane.Front), "Out-of-range slot is refused.");
        }

        [Test]
        public void Hotkeys_AreMatchScoped_AndResetByStartMatch()
        {
            BattleController controller = StartCombatMatch(12, 6);
            Assert.IsTrue(controller.TrySetSkillHotkey(0, 5));

            var economy = new BattleController.MatchEconomy(resourceCap: 60, turn1Resource: 60, startingAvatarHealth: 100000);
            List<Card> deck = MakeDeck(12);
            controller.StartMatch(deck, deck, economy, economy,
                equippedSpellIds: AvatarSpell.CreateCatalog().Take(6).Select(s => s.Id).ToList(), rngSeed: 1);

            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, controller.SkillHotkeySpellIndices, "A new match starts from the default binding.");
        }

        [Test]
        public void ReplayAndLaneBehavior_ReinforcementLaneChoiceAndWindowsAreUnchanged()
        {
            CollectionAssert.AreEqual(new[] { 4, 8 }, BattleController.ReinforcementTicks, "Reinforcement windows are unchanged.");

            BattleController controller = StartCombatMatch(12, 4);
            AdvanceToReinforcementWindow(controller);
            Card card = controller.PlayerState.Hand[0];
            Assert.IsTrue(controller.TryDeployReinforcement(card, Lane.Back), "The ordinary explicit-lane path still works.");
            Assert.AreEqual(1, controller.PlayerState.Lanes[Lane.Back].Cards.Count(c => c.Definition == card));
        }
    }
}
