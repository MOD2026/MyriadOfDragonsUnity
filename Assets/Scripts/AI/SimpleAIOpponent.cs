using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;

namespace MyriadOfDragons.AI
{
    /// <summary>
    /// The solo opponent's turn logic.
    ///
    /// This was previously a pure placeholder: it walked the hand in whatever order the cards
    /// happened to be in and dropped each one into the first lane with a free slot. That made
    /// every opponent play identically and, worse, spend its resource on whichever cheap card
    /// sat earliest in hand while the board's real constraint - 3 lanes x LaneState.MaxSlots -
    /// filled up with weak cards.
    ///
    /// It now does two things a player would recognise as decisions:
    ///  - spends resource on the strongest card it can afford, because slots are scarcer than
    ///    resource (see the ordering note in PlayNextCard), and
    ///  - places according to an <see cref="AIArchetype"/>, which is what makes an Aggressive
    ///    opponent actually feel different from a Defensive one rather than just differently
    ///    labelled.
    ///
    /// Still deliberately not a deep AI: no multi-turn planning, no bluffing, no reading the
    /// player's hand. It is a competent turn, not a strong player.
    /// </summary>
    public static class SimpleAIOpponent
    {
        /// <summary>
        /// Plays out the AI's whole turn. `archetype` defaults to Balanced so existing callers
        /// and tests that predate archetypes keep their previous "spread out" behaviour.
        /// </summary>
        public static void TakeTurn(BattleController controller, AIArchetype archetype = AIArchetype.Balanced)
        {
            while (PlayNextCard(controller, archetype)) { }
        }

        /// <summary>
        /// Plays the single best affordable card this turn, into the archetype's preferred open
        /// lane. Returns false when nothing further can be played, which ends the turn.
        /// </summary>
        private static bool PlayNextCard(BattleController controller, AIArchetype archetype)
        {
            PlayerBattleState enemy = controller.EnemyState;

            // Highest resource cost first. Cost tracks card strength, and board slots run out
            // before resource does under the current economy (a mid-level side can afford far
            // more than 9 cards' worth of cost per turn), so filling a scarce slot with the
            // strongest available card beats filling it with whatever was cheapest.
            List<Cards.Card> affordable = enemy.Hand
                .Where(c => c.ResourceCost <= enemy.Resource)
                .OrderByDescending(c => c.ResourceCost)
                .ThenBy(c => c.Id)
                .ToList();

            foreach (Cards.Card card in affordable)
            {
                foreach (Lane lane in LanePreference(controller, archetype))
                {
                    if (!enemy.Lanes[lane].HasRoomFor(card)) continue;
                    if (controller.TryPlayCard(enemy, card, lane)) return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The order this archetype wants to fill lanes in. Front grants +1 Attack and Middle
        /// grants +1 Health (BattleController.TryPlayCard), which is what makes these orderings
        /// mean something mechanically rather than just thematically.
        /// </summary>
        private static IEnumerable<Lane> LanePreference(BattleController controller, AIArchetype archetype)
        {
            PlayerBattleState enemy = controller.EnemyState;

            switch (archetype)
            {
                case AIArchetype.Aggressive:
                    // Stack the +1 Attack lane and accept the fragility.
                    return new[] { Lane.Front, Lane.Middle, Lane.Back };

                case AIArchetype.Defensive:
                    // Bank the +1 Health lane first, keep Front (the lane that eats the player's
                    // attack first) as a last resort.
                    return new[] { Lane.Middle, Lane.Back, Lane.Front };

                case AIArchetype.Tactical:
                    // Contest wherever the player has committed most. A lane only overflows into
                    // Avatar Health once it is fully cleared (LaneBattleResolver), so meeting the
                    // player's biggest stack is what actually denies them damage - an empty lane
                    // of theirs threatens nothing worth blocking.
                    return System.Enum.GetValues(typeof(Lane))
                        .Cast<Lane>()
                        .OrderByDescending(l => controller.PlayerState.Lanes[l].AliveCards.Count())
                        .ToList();

                case AIArchetype.Balanced:
                default:
                    // Emptiest lane first, so no single lane is left undefended.
                    return System.Enum.GetValues(typeof(Lane))
                        .Cast<Lane>()
                        .OrderBy(l => enemy.Lanes[l].Cards.Count)
                        .ToList();
            }
        }
    }
}
