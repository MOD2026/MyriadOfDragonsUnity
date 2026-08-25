using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Full 36-Spell Catalogue Diagnosis (LOCKED 2026-08-24), Wave 4: CrossLane/AllLane damage +
    /// DrawCards. Five of the wave's seven catalog spells (Ashfall, Stormchain, Scorched Sky,
    /// Leyline Draw, Oracle Sight) - Windstep/Seismic Swap (Reposition) are deliberately not part
    /// of this wave, see AvatarSpell.CreateCatalog's own doc comment for why.
    /// </summary>
    public class SpellCatalogPhase4Tests
    {
        [Test]
        public void CreateCatalog_ContainsExactlyThirtyTwoSpells_TwentySevenPlusFiveWave4()
        {
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            Assert.AreEqual(32, catalog.Count);
            CollectionAssert.IsSubsetOf(
                new[] { "Ashfall", "Stormchain", "Scorched Sky", "Leyline Draw", "Oracle Sight" },
                catalog.Select(s => s.Name).ToList());
        }

        [TestCase("Ashfall", "ashfall", SpellSchool.Andras, 70, 7, SpellEffect.CrossLaneDamage, 4, 1)]
        [TestCase("Stormchain", "stormchain", SpellSchool.Pnevmas, 58, 6, SpellEffect.CrossLaneDamage, 3, 2)]
        [TestCase("Scorched Sky", "scorched_sky", SpellSchool.Andras, 95, 8, SpellEffect.AllLaneDamage, 3, 0)]
        [TestCase("Leyline Draw", "leyline_draw", SpellSchool.Ktini, 36, 4, SpellEffect.DrawCards, 2, 0)]
        [TestCase("Oracle Sight", "oracle_sight", SpellSchool.Pnevmas, 30, 4, SpellEffect.DrawCards, 2, 0)] // locked correction: Energy 42->30
        public void NewSpell_MatchesTheLockedCatalogRow_WithCorrectionsApplied(
            string name, string id, SpellSchool school, int energyCost, int cooldownTicks,
            SpellEffect effect, int magnitude, int secondaryMagnitude)
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Name == name);
            Assert.AreEqual(id, spell.Id, $"{name}: catalog id.");
            Assert.AreEqual(school, spell.School, $"{name}: school.");
            Assert.AreEqual(energyCost, spell.EnergyCost, $"{name}: Energy cost.");
            Assert.AreEqual(cooldownTicks, spell.CooldownTicks, $"{name}: cooldown ticks.");
            Assert.AreEqual(effect, spell.Effect, $"{name}: effect type.");
            Assert.AreEqual(magnitude, spell.Magnitude, $"{name}: magnitude.");
            Assert.AreEqual(secondaryMagnitude, spell.SecondaryMagnitude, $"{name}: secondary magnitude.");
        }

        [Test]
        public void EveryCatalogSpellId_IsUniqueAndNeverEmpty()
        {
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            Assert.IsTrue(catalog.All(s => !string.IsNullOrEmpty(s.Id)));
            Assert.AreEqual(catalog.Count, catalog.Select(s => s.Id).Distinct().Count(), "No two catalog spells may share an id.");
        }

        // ---------- CrossLaneDamage: primary lane + adjacent lane(s), no wrap-around ----------

        [Test]
        public void Ashfall_DamagesTargetLaneAtMagnitude_AndOnlyAdjacentLaneAtSecondaryMagnitude()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "ashfall");
            var caster = MakeState();
            var opponent = MakeState();
            BattleCardInstance front = MakeUnit(rarity: 7);
            BattleCardInstance middle = MakeUnit(rarity: 7);
            BattleCardInstance back = MakeUnit(rarity: 7);
            int frontBefore = front.CurrentHealth, middleBefore = middle.CurrentHealth, backBefore = back.CurrentHealth;
            opponent.Lanes[Lane.Front].Cards.Add(front);
            opponent.Lanes[Lane.Middle].Cards.Add(middle);
            opponent.Lanes[Lane.Back].Cards.Add(back);

            spell.Cast(caster, opponent, Lane.Front);

            Assert.AreEqual(frontBefore - 4, front.CurrentHealth, "Primary lane takes full Magnitude.");
            Assert.AreEqual(middleBefore - 1, middle.CurrentHealth, "Middle is adjacent to Front - takes SecondaryMagnitude.");
            Assert.AreEqual(backBefore, back.CurrentHealth, "Back is not adjacent to Front (no wrap-around) - untouched.");
        }

        [Test]
        public void Stormchain_TargetingMiddleLane_HitsBothAdjacentLanes()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "stormchain");
            var caster = MakeState();
            var opponent = MakeState();
            BattleCardInstance front = MakeUnit(rarity: 7);
            BattleCardInstance middle = MakeUnit(rarity: 7);
            BattleCardInstance back = MakeUnit(rarity: 7);
            int frontBefore = front.CurrentHealth, middleBefore = middle.CurrentHealth, backBefore = back.CurrentHealth;
            opponent.Lanes[Lane.Front].Cards.Add(front);
            opponent.Lanes[Lane.Middle].Cards.Add(middle);
            opponent.Lanes[Lane.Back].Cards.Add(back);

            spell.Cast(caster, opponent, Lane.Middle);

            Assert.AreEqual(middleBefore - 3, middle.CurrentHealth, "Primary lane takes full Magnitude.");
            Assert.AreEqual(frontBefore - 2, front.CurrentHealth, "Front is adjacent to Middle - takes SecondaryMagnitude.");
            Assert.AreEqual(backBefore - 2, back.CurrentHealth, "Back is adjacent to Middle - takes SecondaryMagnitude.");
        }

        [Test]
        public void CrossLaneDamage_UndefendedPrimaryLane_LetsDamageThroughToAvatar()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "ashfall");
            var caster = MakeState();
            var opponent = MakeState(); // every lane starts empty
            int before = opponent.AvatarHealth;

            int dealt = spell.Cast(caster, opponent, Lane.Front);

            // Front (undefended, Magnitude 4) + Middle (undefended, SecondaryMagnitude 1), Back
            // untouched (not adjacent to Front) - matches the same fixed AvatarDamageMultiplier
            // LaneDamage already uses for an undefended lane.
            int expected = (4 + 1) * LaneBattleResolver.AvatarDamageMultiplier;
            Assert.AreEqual(expected, dealt);
            Assert.AreEqual(before - expected, opponent.AvatarHealth);
        }

        // ---------- AllLaneDamage: all three enemy lanes, independently ----------

        [Test]
        public void ScorchedSky_DamagesEveryLivingEnemyUnit_IndependentlyAcrossAllThreeLanes()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "scorched_sky");
            var caster = MakeState();
            var opponent = MakeState();
            BattleCardInstance front = MakeUnit(rarity: 7);
            BattleCardInstance middle = MakeUnit(rarity: 7);
            BattleCardInstance back = MakeUnit(rarity: 7);
            int frontBefore = front.CurrentHealth, middleBefore = middle.CurrentHealth, backBefore = back.CurrentHealth;
            opponent.Lanes[Lane.Front].Cards.Add(front);
            opponent.Lanes[Lane.Middle].Cards.Add(middle);
            opponent.Lanes[Lane.Back].Cards.Add(back);

            spell.Cast(caster, opponent, Lane.Front); // targetLane is irrelevant for an all-lane effect

            Assert.AreEqual(frontBefore - 3, front.CurrentHealth);
            Assert.AreEqual(middleBefore - 3, middle.CurrentHealth);
            Assert.AreEqual(backBefore - 3, back.CurrentHealth);
        }

        [Test]
        public void ScorchedSky_EveryUndefendedLane_LetsDamageThroughToAvatar()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "scorched_sky");
            var caster = MakeState();
            var opponent = MakeState(); // every lane starts empty
            int before = opponent.AvatarHealth;

            int dealt = spell.Cast(caster, opponent, Lane.Front);

            int expected = 3 * 3 * LaneBattleResolver.AvatarDamageMultiplier; // 3 lanes x 3 magnitude
            Assert.AreEqual(expected, dealt);
            Assert.AreEqual(before - expected, opponent.AvatarHealth);
        }

        // ---------- DrawCards ----------

        [Test]
        public void LeylineDraw_DrawsTwoCards_FromDrawPileIntoHand()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "leyline_draw");
            // 6 total cards: PlayerBattleState's constructor already draws StartingHandSize (4)
            // into Hand, leaving exactly 2 in DrawPile - enough for Leyline Draw's own Magnitude
            // (2) to have real cards to move rather than immediately hitting the empty-pile no-op.
            var caster = MakeStateWithDrawPile(6);
            var opponent = MakeState();
            int handBefore = caster.Hand.Count;
            int drawPileBefore = caster.DrawPile.Count;

            spell.Cast(caster, opponent, Lane.Front);

            Assert.AreEqual(handBefore + 2, caster.Hand.Count);
            Assert.AreEqual(drawPileBefore - 2, caster.DrawPile.Count);
        }

        [Test]
        public void DrawCards_NeverCreatesCards_SafelyNoOpsOnceThePileIsEmpty()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "oracle_sight");
            var caster = MakeStateWithDrawPile(1); // fewer cards than Magnitude (2)
            var opponent = MakeState();
            int startingCardCount = caster.Hand.Count + caster.DrawPile.Count;

            spell.Cast(caster, opponent, Lane.Front);

            Assert.AreEqual(0, caster.DrawPile.Count);
            Assert.AreEqual(startingCardCount, caster.Hand.Count + caster.DrawPile.Count,
                "Total cards must be conserved - DrawCards never manufactures a card that wasn't already in the deck.");
        }

        private static PlayerBattleState MakeState() =>
            new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);

        private static PlayerBattleState MakeStateWithDrawPile(int extraCards)
        {
            var deck = new List<Card>();
            for (int i = 0; i < extraCards; i++)
            {
                deck.Add(Card.FromData(new CardData
                {
                    id = "phase4_deck_card_" + System.Guid.NewGuid().ToString("N"),
                    name = "Phase 4 Deck Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = 5,
                }));
            }
            // Constructor already draws up to StartingHandSize into Hand - remaining sit in DrawPile.
            return new PlayerBattleState(deck, resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);
        }

        private static BattleCardInstance MakeUnit(int rarity)
        {
            var data = new CardData
            {
                id = "phase4_test_card_" + System.Guid.NewGuid().ToString("N"),
                name = "Phase 4 Catalog Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = rarity,
            };
            return new BattleCardInstance(Card.FromData(data), isPlayerOwned: true, laneAttackBonus: 0, laneHealthBonus: 0);
        }
    }
}
