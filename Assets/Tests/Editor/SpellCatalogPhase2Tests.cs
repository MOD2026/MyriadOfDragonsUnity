using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Full 36-Spell Catalogue Diagnosis (LOCKED 2026-08-24), Wave 2: "expand to 19 using already-
    /// implemented effects" - Magma Rend, Blood Price, Grave Mend, Celestial Verdict, Aegis Return.
    /// All five use only the four live SpellEffect values (LaneDamage/LaneHeal/AvatarStrike here),
    /// so no new Cast() case was needed. Proves the locked corrections were actually applied
    /// (Celestial Verdict 110-&gt;100 Energy, Blood Price 38-&gt;55 Energy), not just documented.
    /// </summary>
    public class SpellCatalogPhase2Tests
    {
        [Test]
        public void CreateCatalog_ContainsAllNineteenWave1AndWave2Spells()
        {
            // Not an exact-count assertion any more - Wave 3 (SpellCatalogPhase3Tests) added 8
            // more spells on top of these 19, and that test file now owns the catalog's real
            // total count. This test's own job stays narrower: every Wave 1/2 spell is still
            // present and nothing here regressed when Wave 3 landed.
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "Firestorm", "Mend", "War Cry", "Divine Bolt",
                    "Cinder Lash", "Ember Wave", "Fault Line", "Vital Spark", "Renewal",
                    "Rallying Gale", "Banner of Ashes", "Sun Lance", "Stone Judgment", "Tempest Brand",
                    "Magma Rend", "Blood Price", "Grave Mend", "Celestial Verdict", "Aegis Return",
                },
                catalog.Select(s => s.Name).ToList());
        }

        [TestCase("Magma Rend", "magma_rend", SpellSchool.Andras, 62, 6, SpellEffect.LaneDamage, 6)]
        [TestCase("Blood Price", "blood_price", SpellSchool.Andras, 55, 4, SpellEffect.AvatarStrike, 90)] // locked correction: Energy 38->55
        [TestCase("Grave Mend", "grave_mend", SpellSchool.Ktini, 68, 7, SpellEffect.LaneHeal, 9)]
        [TestCase("Celestial Verdict", "celestial_verdict", SpellSchool.Pnevmas, 100, 9, SpellEffect.AvatarStrike, 150)] // locked correction: Energy 110->100
        [TestCase("Aegis Return", "aegis_return", SpellSchool.Ktini, 52, 5, SpellEffect.LaneHeal, 7)]
        public void NewSpell_MatchesTheLockedCatalogRow_WithCorrectionsApplied(
            string name, string id, SpellSchool school, int energyCost, int cooldownTicks, SpellEffect effect, int magnitude)
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Name == name);
            Assert.AreEqual(id, spell.Id, $"{name}: catalog id.");
            Assert.AreEqual(school, spell.School, $"{name}: school.");
            Assert.AreEqual(energyCost, spell.EnergyCost, $"{name}: Energy cost.");
            Assert.AreEqual(cooldownTicks, spell.CooldownTicks, $"{name}: cooldown ticks.");
            Assert.AreEqual(effect, spell.Effect, $"{name}: effect type.");
            Assert.AreEqual(magnitude, spell.Magnitude, $"{name}: magnitude.");
        }

        [Test]
        public void CelestialVerdict_EnergyCost_NeverExceedsMaxEnergy()
        {
            // The whole reason for the 110->100 correction: BattleController.MaxEnergy is
            // hard-capped at 100 - a spell costing more than that is permanently uncastable.
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Name == "Celestial Verdict");
            Assert.LessOrEqual(spell.EnergyCost, 100);
        }

        [Test]
        public void EveryCatalogSpellId_IsUniqueAndNeverEmpty()
        {
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            Assert.IsTrue(catalog.All(s => !string.IsNullOrEmpty(s.Id)));
            Assert.AreEqual(catalog.Count, catalog.Select(s => s.Id).Distinct().Count(), "No two catalog spells may share an id.");
        }

        // ---------- Actually castable through the real Cast() path ----------

        [Test]
        public void MagmaRend_DamagesEveryLivingEnemyUnitInTheLane()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "magma_rend");
            var caster = new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);
            var opponent = new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);
            // Rarity 7 (Godlike): real hpMin/hpMax is [7, 12] (RarityTable), comfortably above
            // Magma Rend's own magnitude (6) - the unit is guaranteed to survive with health left,
            // so the delta assertion below can't be masked by an unrelated death-clamp-to-0.
            BattleCardInstance unit = MakeUnit(rarity: 7);
            int before = unit.CurrentHealth;
            opponent.Lanes[Lane.Front].Cards.Add(unit);

            spell.Cast(caster, opponent, Lane.Front);

            Assert.AreEqual(before - 6, unit.CurrentHealth);
        }

        [Test]
        public void GraveMend_HealsEveryLivingFriendlyUnitInTheLane()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "grave_mend");
            var caster = new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);
            var opponent = new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);
            BattleCardInstance unit = MakeUnit(rarity: 7); // real MaxHealth in [7, 12] - always > 1.
            unit.ApplyDamage(1);
            caster.Lanes[Lane.Front].Cards.Add(unit);

            spell.Cast(caster, opponent, Lane.Front);

            // Only 1 missing Health, Grave Mend heals 9 - always fully tops back up to MaxHealth
            // regardless of the real hash-derived value.
            Assert.AreEqual(unit.MaxHealth, unit.CurrentHealth);
        }

        [Test]
        public void BloodPrice_StrikesTheEnemyAvatarDirectly()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "blood_price");
            var caster = new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);
            var opponent = new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);

            int dealt = spell.Cast(caster, opponent, Lane.Front);

            Assert.AreEqual(90, dealt);
            Assert.AreEqual(910, opponent.AvatarHealth);
        }

        [Test]
        public void CelestialVerdict_StrikesTheEnemyAvatarDirectly()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "celestial_verdict");
            var caster = new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);
            var opponent = new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);

            int dealt = spell.Cast(caster, opponent, Lane.Front);

            Assert.AreEqual(150, dealt);
            Assert.AreEqual(850, opponent.AvatarHealth);
        }

        private static BattleCardInstance MakeUnit(int rarity)
        {
            var data = new CardData
            {
                id = "phase2_test_card_" + System.Guid.NewGuid().ToString("N"),
                name = "Phase 2 Catalog Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = rarity,
            };
            return new BattleCardInstance(Card.FromData(data), isPlayerOwned: true, laneAttackBonus: 0, laneHealthBonus: 0);
        }
    }
}
