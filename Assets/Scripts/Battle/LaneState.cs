using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Cards;

namespace MyriadOfDragons.Battle
{
    public enum Lane
    {
        Front,
        Middle,
        Back,
    }

    /// <summary>One lane's live cards (max 3 slots), per Game Mechanics v2, Part II §2.1.</summary>
    public class LaneState
    {
        public const int MaxSlots = 3;

        public readonly Lane Lane;
        public readonly List<BattleCardInstance> Cards = new List<BattleCardInstance>();

        public LaneState(Lane lane)
        {
            Lane = lane;
        }

        /// <summary>
        /// Slots currently occupied. Not the same as Cards.Count - a rarity 5+ card takes two
        /// slots (see Card.SlotWeight), so three cards do not necessarily fill a lane and two
        /// sometimes do.
        /// </summary>
        public int SlotsUsed => Cards.Sum(c => c.Definition.SlotWeight);

        public int FreeSlots => MaxSlots - SlotsUsed;

        /// <summary>True when anything at all could still be played here. Callers placing a
        /// specific card should use <see cref="HasRoomFor"/> instead - a lane with one free slot
        /// has room for a 1-star and no room for a 7-star.</summary>
        public bool HasOpenSlot => FreeSlots > 0;

        public bool HasRoomFor(Card card) => card != null && card.SlotWeight <= FreeSlots;

        public IEnumerable<BattleCardInstance> AliveCards => Cards.Where(c => c.IsAlive);

        public bool IsFullyCleared => Cards.Count > 0 && Cards.All(c => !c.IsAlive);
    }
}
