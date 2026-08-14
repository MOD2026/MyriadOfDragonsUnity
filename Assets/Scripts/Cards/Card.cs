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
            card.ComputeStats(data);
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
        /// FALLBACK ONLY as of the data-authored stats migration (2026-08-14) - used solely when
        /// a card has no valid authored Attack/Health yet (see ComputeStats). Deterministic
        /// per-card variance within its rarity's stat range, hash-based rather than random so a
        /// given card's stats are at least stable within one process. Kept deliberately unchanged
        /// (not deleted) so a not-yet-authored or corrupted-data card still gets a valid stat
        /// instead of breaking - it must never be the source of a populated card's live stats,
        /// since it is not stable across runtimes (see docs on the cross-runtime card-stat
        /// investigation this migration resolves).
        /// </summary>
        private int VarianceIndex(int rangeSize)
        {
            if (rangeSize <= 1) return 0;
            int hash = Id.GetHashCode();
            int positive = hash & 0x7FFFFFFF;
            return positive % rangeSize;
        }

        /// <summary>
        /// Checks whether <paramref name="authoredValue"/> is a usable, data-authored stat: not
        /// the "unauthored" sentinel (0 - every RarityTable range starts at 1, so 0 can never be
        /// a legitimate computed value), and within the legal range for this card's rarity/class.
        /// Logs a clear warning and reports "not usable" for either failure, rather than silently
        /// accepting bad data or silently reverting to the formula with no trace.
        /// </summary>
        private static bool TryGetValidAuthoredValue(int authoredValue, int minValid, int maxValid, string statName, string cardId)
        {
            if (authoredValue == 0)
            {
                Debug.LogWarning($"Card '{cardId}': no authored {statName} value yet - falling back to the hash-derived formula.");
                return false;
            }

            if (authoredValue < minValid || authoredValue > maxValid)
            {
                Debug.LogWarning($"Card '{cardId}': authored {statName} {authoredValue} is outside the legal range " +
                    $"[{minValid}, {maxValid}] for its rarity/class - falling back to the hash-derived formula.");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Prefers data.attack/data.health - the values transferred from the approved
        /// Android/Unity Editor baseline, treated as FINAL (the Warrior +1 bonus below is not
        /// re-applied on top of an authored Attack; it is already included in that number, exactly
        /// as captured). Falls back to the hash-derived formula only when a value is missing,
        /// zero, or outside the legal range for this card's rarity/class - see
        /// TryGetValidAuthoredValue.
        /// </summary>
        private void ComputeStats(CardData data)
        {
            var (cost, atkMin, atkMax, hpMin, hpMax) = RarityTable[Rarity];

            // The valid range for an authored Attack includes the Warrior bonus, since an
            // authored value already has it baked in - the raw RarityTable range alone would
            // wrongly reject every legitimately-authored Warrior card as "out of range".
            int maxAuthoredAttack = atkMax + (Class == CardClass.Warrior ? 1 : 0);

            int attack;
            if (TryGetValidAuthoredValue(data.attack, atkMin, maxAuthoredAttack, "Attack", Id))
            {
                attack = data.attack;
            }
            else
            {
                attack = atkMin + VarianceIndex(atkMax - atkMin + 1);
                if (Class == CardClass.Warrior)
                {
                    attack += 1;
                }
            }

            int health;
            if (TryGetValidAuthoredValue(data.health, hpMin, hpMax, "Health", Id))
            {
                health = data.health;
            }
            else
            {
                health = hpMin + VarianceIndex(hpMax - hpMin + 1);
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

    /// <summary>Plain data shape matching data/card_data.json, for JsonUtility deserialization.
    /// attack/health are data-authored final stats (added 2026-08-14) - 0 means "not yet
    /// authored" (every RarityTable range starts at 1, so 0 is never a real value); see
    /// Card.ComputeStats for how a missing or invalid value falls back safely.</summary>
    [System.Serializable]
    public class CardData
    {
        public string id;
        public string name;
        public string art_file;
        public string element;
        public string type;
        public int rarity;
        public int attack;
        public int health;
    }

    /// <summary>JsonUtility can't parse a top-level JSON array directly - this wraps it.</summary>
    [System.Serializable]
    public class CardDataList
    {
        public CardData[] cards;
    }
}
