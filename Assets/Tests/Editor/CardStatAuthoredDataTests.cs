using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// DATA-AUTHORED CARD STATS, 2026-08-14 - focused coverage for the migration off
    /// Card.VarianceIndex's Id.GetHashCode() as the source of a populated card's live stats.
    /// Covers exactly two things: (1) every shipped card's authored Attack/Health matches the
    /// approved Android/Unity Editor baseline this migration was built from, and (2) the fallback
    /// path (missing/zero/out-of-range authored data) still produces a valid, in-range stat
    /// rather than breaking. Does not duplicate BattleLogicTests/BalanceSimulationTests, which
    /// already cover combat behavior built on top of whatever Attack/Health a card resolves to.
    /// </summary>
    public class CardStatAuthoredDataTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private CardDatabase LoadDatabase()
        {
            var go = new GameObject("AuthoredDataCardDatabase");
            _spawned.Add(go);
            CardDatabase db = go.AddComponent<CardDatabase>();
            db.Initialize();
            db = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            return db;
        }

        /// <summary>Reads Assets/Tests/Editor/Fixtures/ApprovedCardStatBaseline.txt directly off
        /// disk - a checked-in fixture, not shipped game content, so it is read by path rather
        /// than through Resources.Load like CardDatabase's own real content.</summary>
        private static Dictionary<string, (int rarity, int attack, int health)> LoadApprovedBaseline()
        {
            string path = Path.Combine(Application.dataPath, "Tests/Editor/Fixtures/ApprovedCardStatBaseline.txt");
            var baseline = new Dictionary<string, (int, int, int)>();

            foreach (string rawLine in File.ReadAllLines(path))
            {
                string line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith("#")) continue;

                string[] fields = line.Split('\t');
                Assert.AreEqual(4, fields.Length, $"Fixture setup: malformed baseline line '{rawLine}'.");
                baseline[fields[0]] = (int.Parse(fields[1]), int.Parse(fields[2]), int.Parse(fields[3]));
            }

            return baseline;
        }

        [Test]
        public void AllCards_MatchTheApprovedBaselineExactly()
        {
            Dictionary<string, (int rarity, int attack, int health)> baseline = LoadApprovedBaseline();
            Assert.AreEqual(85, baseline.Count, "Setup check: the approved baseline fixture must contain exactly 85 cards.");

            CardDatabase db = LoadDatabase();
            Assert.AreEqual(baseline.Count, db.AllCards.Count,
                "CardDatabase must load exactly as many cards as the approved baseline covers.");

            foreach (Card card in db.AllCards)
            {
                Assert.IsTrue(baseline.TryGetValue(card.Id, out var expected),
                    $"Card '{card.Id}' has no entry in the approved baseline fixture.");
                Assert.AreEqual(expected.rarity, card.Rarity, $"Card '{card.Id}': rarity does not match the approved baseline.");
                Assert.AreEqual(expected.attack, card.Attack,
                    $"Card '{card.Id}': live Attack ({card.Attack}) does not match the approved baseline ({expected.attack}) - " +
                    "the data-authored value is not being used as the final stat.");
                Assert.AreEqual(expected.health, card.Health,
                    $"Card '{card.Id}': live Health ({card.Health}) does not match the approved baseline ({expected.health}) - " +
                    "the data-authored value is not being used as the final stat.");
            }
        }

        private static CardData MakeCardData(string id, string type, int rarity, int attack, int health)
        {
            return new CardData
            {
                id = id,
                name = id,
                art_file = "placeholder.jpg",
                element = "Andras",
                type = type,
                rarity = rarity,
                attack = attack,
                health = health,
            };
        }

        [Test]
        public void ComputeStats_MissingAuthoredValues_FallsBackToAValidInRangeStat()
        {
            // Rarity 3, Knight (no Attack bonus to complicate the range): RarityTable's own
            // documented range is Attack 3-4, Health 3-4 (Card.cs's RarityTable comment).
            CardData data = MakeCardData("fallback_missing_test", "knight", rarity: 3, attack: 0, health: 0);

            Card card = Card.FromData(data);

            Assert.That(card.Attack, Is.InRange(3, 4), "A missing (0) authored Attack must fall back to a valid in-range stat, not 0.");
            Assert.That(card.Health, Is.InRange(3, 4), "A missing (0) authored Health must fall back to a valid in-range stat, not 0.");
        }

        [Test]
        public void ComputeStats_InvalidAuthoredAttack_FallsBackButKeepsTheValidHealth()
        {
            CardData data = MakeCardData("fallback_invalid_attack_test", "knight", rarity: 3, attack: 99, health: 3);

            Card card = Card.FromData(data);

            Assert.That(card.Attack, Is.InRange(3, 4),
                "An out-of-range authored Attack (99) must fall back to a valid in-range stat, not be accepted as-is.");
            Assert.AreEqual(3, card.Health, "A valid authored Health must be used even when Attack falls back independently.");
        }

        [Test]
        public void ComputeStats_InvalidAuthoredHealth_FallsBackButKeepsTheValidAttack()
        {
            CardData data = MakeCardData("fallback_invalid_health_test", "knight", rarity: 3, attack: 4, health: -7);

            Card card = Card.FromData(data);

            Assert.AreEqual(4, card.Attack, "A valid authored Attack must be used even when Health falls back independently.");
            Assert.That(card.Health, Is.InRange(3, 4),
                "A negative authored Health (-7) must fall back to a valid in-range stat, not be accepted as-is.");
        }

        [Test]
        public void ComputeStats_AuthoredWarriorAttack_IsTreatedAsFinalNotBonusedAgain()
        {
            // Rarity 3's raw Attack range is 3-4, so 5 is only a legal authored value for a
            // Warrior because the +1 class bonus is already baked into it. If ComputeStats added
            // the bonus a second time, this card would resolve to 6, not the authored 5.
            CardData data = MakeCardData("warrior_bonus_test", "warrior", rarity: 3, attack: 5, health: 3);

            Card card = Card.FromData(data);

            Assert.AreEqual(5, card.Attack,
                "An authored Warrior Attack must be used exactly as given - the class bonus must not be added on top of it.");
        }
    }
}
