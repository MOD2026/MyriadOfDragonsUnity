using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Cards;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// One side's live match state - deck, hand, resource, lanes, and Avatar Health. Two of
    /// these (player and enemy) make up a full match in <see cref="BattleController"/>.
    /// </summary>
    public class PlayerBattleState
    {
        public const int StartingHandSize = 4; // placeholder - see design doc Part X open questions

        // Both were shared constants (ResourceCap=10, StartingAvatarHealth=20) for every player
        // regardless of progression - now sourced per-match from PlayerEmpireData, since a
        // level-1 and a leveled-up player's Avatar/Castle levels should not grant the same
        // match economy or the same HP pool. See BattleController.StartMatch.
        public readonly int ResourceCap;
        private readonly int _turn1Resource;

        /// <summary>Starting Avatar Health, retained separately from the live AvatarHealth
        /// value so the UI can render a fill-fraction health bar, not just a raw number.</summary>
        public readonly int MaxAvatarHealth;

        public int AvatarHealth;
        public int Resource;
        public int TurnNumber;

        public readonly List<Card> DrawPile = new List<Card>();
        public readonly List<Card> Hand = new List<Card>();
        public readonly Dictionary<Lane, LaneState> Lanes = new Dictionary<Lane, LaneState>
        {
            { Lane.Front, new LaneState(Lane.Front) },
            { Lane.Middle, new LaneState(Lane.Middle) },
            { Lane.Back, new LaneState(Lane.Back) },
        };

        public bool IsDefeated => AvatarHealth <= 0;

        /// <summary>
        /// True when this side has nothing alive on the board at all. Distinct from any single
        /// lane being cleared: it is the state in which this side can neither deal damage nor
        /// block any, which is the state the match-hanging bug in
        /// <see cref="BattleController.MaxCombatTicks"/> describes.
        /// </summary>
        public bool HasLivingCards =>
            Lanes.Values.Any(lane => lane.Cards.Any(c => c.IsAlive));

        public PlayerBattleState(IEnumerable<Card> deck, int resourceCap, int turn1Resource, int startingAvatarHealth)
        {
            ResourceCap = resourceCap;
            _turn1Resource = turn1Resource;
            MaxAvatarHealth = startingAvatarHealth;
            AvatarHealth = startingAvatarHealth;

            DrawPile.AddRange(deck);
            Shuffle(DrawPile);
            for (int i = 0; i < StartingHandSize && DrawPile.Count > 0; i++)
            {
                DrawCard();
            }
        }

        public void DrawCard()
        {
            if (DrawPile.Count == 0) return;
            Card card = DrawPile[0];
            DrawPile.RemoveAt(0);
            Hand.Add(card);
        }

        // Turn 1 resource is now a caller-supplied baseline (_turn1Resource, ~60% of
        // ResourceCap - see PlayerEmpireData.Turn1Resource) rather than a flat +3 offset shared
        // by every player: a level-1 player's low cap still gets a fair, playable Turn 1 (this
        // was validated against the dead-opening-hand math the flat-offset version used to
        // solve - see BattleLogicTests.PlayerBattleState_Turn1_...), while a leveled-up
        // player's Turn 1 scales up with their progression instead of staying pinned to the
        // same starting point as a brand-new player. Ramps +1/turn after that baseline either way.
        public void GainResourceForTurn()
        {
            TurnNumber++;
            Resource = System.Math.Min(ResourceCap, _turn1Resource + (TurnNumber - 1));
        }

        private static void Shuffle(List<Card> cards)
        {
            // Tests may pin a seed so Chapter 1 / balance audits measure policy, not draw luck.
            // Production keeps an unseeded Random so each match still shuffles freshly.
            var rng = _shuffleSeedForTests.HasValue
                ? new System.Random(_shuffleSeedForTests.Value)
                : new System.Random();
            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (cards[i], cards[j]) = (cards[j], cards[i]);
            }
        }

        private static int? _shuffleSeedForTests;

        /// <summary>EditMode-only: pin both sides' draw shuffles so combat policies are comparable.</summary>
        public static void SetShuffleSeedForTests(int seed) => _shuffleSeedForTests = seed;

        /// <summary>EditMode-only: restore unseeded production shuffle behaviour.</summary>
        public static void ClearShuffleSeedForTests() => _shuffleSeedForTests = null;
    }
}
