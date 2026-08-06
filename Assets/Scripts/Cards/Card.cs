using UnityEngine;

namespace MyriadOfDragons.Cards
{
    public enum CardClass
    {
        Warrior,
        Knight,
        Strategist,
        Perfect,
    }

    public enum CardElement
    {
        Andras,
        Ktini,
        Pnevmas,
    }

    /// <summary>
    /// A single card's identity and derived combat stats, per Game Mechanics v2, Part II §3
    /// (Integer Model) - replaces the old compounding-multiplier formula from the original
    /// design with small, mentally-computable integers.
    /// </summary>
    [System.Serializable]
    public class Card
    {
        public string Id;
        public string DisplayName;
        public string ArtFile;
        public CardElement Element;
        public CardClass Class;
        public int Rarity; // 1-7 stars

        public int ResourceCost { get; private set; }
        public int Attack { get; private set; }
        public int Health { get; private set; }

        /// <summary>
        /// How many of a lane's 3 slots this card occupies. Rarity 1-4 takes one, rarity 5-7
        /// takes two.
        ///
        /// This exists to stop high rarity being a strict upgrade. Board space (9 slots), not
        /// Resource, is the binding constraint in this game - over a full match there is plenty
        /// of Resource but never more than 9 slots - so before slot weighting the correct play
        /// was always "fill every slot with the highest rarity you own", and a deeper collection
        /// simply won. Weighting changes attack-per-slot from monotonically increasing with
        /// rarity to peaking in the middle:
        ///
        ///   rarity 4: ~5.0 Attack in 1 slot = 5.00 per slot   (the efficiency peak)
        ///   rarity 7: ~9.5 Attack in 2 slots = 4.75 per slot  (concentrated, ~2x the Health)
        ///
        /// A wide board of 4-stars out-damages a tall board of 7-stars, but has half the Health
        /// per body and dies faster - a real trade rather than a dominant strategy.
        /// </summary>
        public int SlotWeight => Rarity >= 5 ? 2 : 1;

        // Rarity -> (cost, attack min/max, health min/max), straight from the v2 stat table.
        private static readonly (int cost, int atkMin, int atkMax, int hpMin, int hpMax)[] RarityTable =
        {
            default,                 // index 0 unused, rarity is 1-based
            (1, 1, 2, 1, 2),          // 1 star
            (2, 2, 3, 2, 3),          // 2 star
            (3, 3, 4, 3, 4),          // 3 star
            (4, 4, 6, 4, 6),          // 4 star
            (5, 5, 7, 5, 7),          // 5 star
            (6, 6, 9, 6, 9),          // 6 star
            (7, 7, 12, 7, 12),        // 7 star (Godlike)
        };

        public static Card FromData(CardData data)
        {
            var card = new Card
            {
                Id = data.id,
                DisplayName = data.name,
                ArtFile = data.art_file,
                Element = System.Enum.Parse<CardElement>(data.element),
                Class = ParseClass(data.type),
                Rarity = Mathf.Clamp(data.rarity, 1, 7),
            };
            card.ComputeStats();
            return card;
        }

        private static CardClass ParseClass(string typeValue)
        {
            // The source data still uses the original "type" vocabulary (warrior/knight/
            // strategist/perfect) - Part II §3.2 renamed this to Class conceptually, the
            // values themselves didn't change.
            return typeValue.ToLowerInvariant() switch
            {
                "warrior" => CardClass.Warrior,
                "knight" => CardClass.Knight,
                "strategist" => CardClass.Strategist,
                "perfect" => CardClass.Perfect,
                _ => CardClass.Warrior,
            };
        }

        /// <summary>
        /// Deterministic per-card variance within its rarity's stat range, so same-rarity
        /// cards aren't all mechanically identical. Hash-based rather than random so a given
        /// card's stats are stable across sessions without needing to be hand-authored or saved.
        /// </summary>
        private int VarianceIndex(int rangeSize)
        {
            if (rangeSize <= 1) return 0;
            int hash = Id.GetHashCode();
            int positive = hash & 0x7FFFFFFF;
            return positive % rangeSize;
        }

        private void ComputeStats()
        {
            var (cost, atkMin, atkMax, hpMin, hpMax) = RarityTable[Rarity];

            int attack = atkMin + VarianceIndex(atkMax - atkMin + 1);
            int health = hpMin + VarianceIndex(hpMax - hpMin + 1);

            if (Class == CardClass.Warrior)
            {
                attack += 1;
            }
            // Knight (Taunt), Strategist (draw/resource on play), and Perfect (adopts whatever
            // lane bonus it's played into, instead of a flat cost tax - see the hardcore-CCG
            // rework in Part II §3.2) are all behavioral/placement hooks, not static stat
            // changes - handled in BattleController.TryPlayCard, not here, since Card has no
            // notion of which lane it'll be played into.

            ResourceCost = cost;
            Attack = attack;
            Health = health;
        }

        public string ResourcePath() => $"CardArt/{System.IO.Path.GetFileNameWithoutExtension(ArtFile)}";
    }

    /// <summary>Plain data shape matching data/card_data.json, for JsonUtility deserialization.</summary>
    [System.Serializable]
    public class CardData
    {
        public string id;
        public string name;
        public string art_file;
        public string element;
        public string type;
        public int rarity;
    }

    /// <summary>JsonUtility can't parse a top-level JSON array directly - this wraps it.</summary>
    [System.Serializable]
    public class CardDataList
    {
        public CardData[] cards;
    }
}
