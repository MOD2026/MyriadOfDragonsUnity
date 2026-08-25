using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Full 36-Spell Catalogue Diagnosis (LOCKED 2026-08-24), Wave 3: Shields, Cleanse, Dispel,
    /// Vulnerability, Thunder Decree's all-lane buff - Ember Guard, Earthward, Stonewall,
    /// Veil of Zeus, Cleansing Root, Gale Break, Infernal Mark, Thunder Decree. Five new
    /// SpellEffect values, real BattleCardInstance state (Shield/Vulnerability mark/capped
    /// spell-Attack-buff tracking) - proves the mechanics actually run through Cast(), not just
    /// that the catalog data matches the locked table.
    /// </summary>
    public class SpellCatalogPhase3Tests
    {
        [Test]
        public void CreateCatalog_ContainsAllTwentySevenWave1Through3Spells()
        {
            // Not an exact-count assertion any more - Wave 4 (SpellCatalogPhase4Tests) added more
            // spells on top of these 27, and that test file now owns the catalog's real total.
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            CollectionAssert.IsSubsetOf(
                new[]
                {
                    "Firestorm", "Mend", "War Cry", "Divine Bolt",
                    "Cinder Lash", "Ember Wave", "Fault Line", "Vital Spark", "Renewal",
                    "Rallying Gale", "Banner of Ashes", "Sun Lance", "Stone Judgment", "Tempest Brand",
                    "Magma Rend", "Blood Price", "Grave Mend", "Celestial Verdict", "Aegis Return",
                    "Ember Guard", "Earthward", "Stonewall", "Veil of Zeus",
                    "Cleansing Root", "Gale Break", "Infernal Mark", "Thunder Decree",
                },
                catalog.Select(s => s.Name).ToList());
        }

        [TestCase("Ember Guard", "ember_guard", SpellSchool.Andras, 30, 4, SpellEffect.LaneShield, 5)]
        [TestCase("Earthward", "earthward", SpellSchool.Ktini, 42, 5, SpellEffect.LaneShield, 7)]
        [TestCase("Stonewall", "stonewall", SpellSchool.Ktini, 50, 6, SpellEffect.LaneShield, 8)]
        [TestCase("Veil of Zeus", "veil_of_zeus", SpellSchool.Pnevmas, 58, 6, SpellEffect.LaneShield, 10)] // locked correction: shield 6->10/unit
        [TestCase("Cleansing Root", "cleansing_root", SpellSchool.Ktini, 20, 3, SpellEffect.Cleanse, 0)]
        [TestCase("Gale Break", "gale_break", SpellSchool.Pnevmas, 26, 3, SpellEffect.Dispel, 0)]
        [TestCase("Infernal Mark", "infernal_mark", SpellSchool.Andras, 24, 3, SpellEffect.Vulnerability, 0)]
        [TestCase("Thunder Decree", "thunder_decree", SpellSchool.Pnevmas, 65, 7, SpellEffect.AllLaneAttackBuff, 1)]
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
        public void EveryCatalogSpellId_IsUniqueAndNeverEmpty()
        {
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            Assert.IsTrue(catalog.All(s => !string.IsNullOrEmpty(s.Id)));
            Assert.AreEqual(catalog.Count, catalog.Select(s => s.Id).Distinct().Count(), "No two catalog spells may share an id.");
        }

        // ---------- Shield: grant, absorb-before-health, replace-not-stack ----------

        [Test]
        public void EmberGuard_GrantsShieldToEveryLivingFriendlyUnitInTheLane()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "ember_guard");
            var caster = MakeState();
            var opponent = MakeState();
            BattleCardInstance unit = MakeUnit(rarity: 7);
            caster.Lanes[Lane.Front].Cards.Add(unit);

            spell.Cast(caster, opponent, Lane.Front);

            Assert.AreEqual(5, unit.Shield);
        }

        [Test]
        public void Shield_AbsorbsDamageBeforeHealth()
        {
            BattleCardInstance unit = MakeUnit(rarity: 7); // real Health in [7, 12]
            unit.ApplyShield(5);
            int healthBefore = unit.CurrentHealth;

            unit.ApplyDamage(3); // fully absorbed by shield

            Assert.AreEqual(2, unit.Shield);
            Assert.AreEqual(healthBefore, unit.CurrentHealth);
        }

        [Test]
        public void Shield_OverflowSpillsIntoHealth_OnceShieldIsExhausted()
        {
            BattleCardInstance unit = MakeUnit(rarity: 7);
            unit.ApplyShield(3);
            int healthBefore = unit.CurrentHealth;

            unit.ApplyDamage(5); // 3 absorbed, 2 spills to Health

            Assert.AreEqual(0, unit.Shield);
            Assert.AreEqual(healthBefore - 2, unit.CurrentHealth);
        }

        [Test]
        public void Shield_StrongerReplacesWeaker_NeverStacksAdditively()
        {
            BattleCardInstance unit = MakeUnit(rarity: 7);
            unit.ApplyShield(5);
            unit.ApplyShield(10);
            Assert.AreEqual(10, unit.Shield, "A stronger Shield replaces the weaker one, not 5+10=15.");

            unit.ApplyShield(3);
            Assert.AreEqual(10, unit.Shield, "A weaker Shield must not downgrade an existing stronger one.");
        }

        // ---------- Dispel: strips enemy Shield ----------

        [Test]
        public void GaleBreak_RemovesShieldFromEveryLivingEnemyUnitInTheLane()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "gale_break");
            var caster = MakeState();
            var opponent = MakeState();
            BattleCardInstance unit = MakeUnit(rarity: 7);
            unit.ApplyShield(8);
            opponent.Lanes[Lane.Front].Cards.Add(unit);

            spell.Cast(caster, opponent, Lane.Front);

            Assert.AreEqual(0, unit.Shield);
        }

        // ---------- Vulnerability: mark, +1 on trigger, consumed, expires unused ----------

        [Test]
        public void InfernalMark_MarksEveryLivingEnemyUnitInTheLane_NextHitDealsPlusOne()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "infernal_mark");
            var caster = MakeState();
            var opponent = MakeState();
            BattleCardInstance unit = MakeUnit(rarity: 7);
            opponent.Lanes[Lane.Front].Cards.Add(unit);
            int before = unit.CurrentHealth;

            spell.Cast(caster, opponent, Lane.Front);
            unit.ApplyDamage(2);

            Assert.AreEqual(before - 3, unit.CurrentHealth, "A marked unit's next hit deals +1.");
        }

        [Test]
        public void Vulnerability_IsConsumedOnTrigger_DoesNotApplyTwice()
        {
            BattleCardInstance unit = MakeUnit(rarity: 7);
            unit.MarkVulnerable();
            int before = unit.CurrentHealth;

            unit.ApplyDamage(2); // consumes the mark, deals 3
            unit.ApplyDamage(2); // mark already gone, deals 2

            Assert.AreEqual(before - 5, unit.CurrentHealth);
        }

        [Test]
        public void Cleanse_RemovesVulnerabilityMark_BeforeItCanTrigger()
        {
            AvatarSpell cleanse = AvatarSpell.CreateCatalog().Single(s => s.Id == "cleansing_root");
            var caster = MakeState();
            var opponent = MakeState();
            BattleCardInstance unit = MakeUnit(rarity: 7);
            unit.MarkVulnerable();
            caster.Lanes[Lane.Front].Cards.Add(unit);
            int before = unit.CurrentHealth;

            cleanse.Cast(caster, opponent, Lane.Front);
            unit.ApplyDamage(2);

            Assert.AreEqual(before - 2, unit.CurrentHealth, "Cleanse must strip the mark before it can add +1.");
        }

        [Test]
        public void Vulnerability_ExpiresUnusedAtEndOfTheClashItWasCastInto()
        {
            var sideA = MakeState();
            var sideB = MakeState();
            BattleCardInstance marked = MakeUnit(rarity: 7);
            marked.MarkVulnerable();
            sideB.Lanes[Lane.Front].Cards.Add(marked);
            // Front lane has no attacker on side A this clash, so `marked` is never actually hit -
            // the mark must expire unused rather than surviving to a later clash.
            sideB.Lanes[Lane.Middle].Cards.Clear();
            sideB.Lanes[Lane.Back].Cards.Clear();
            sideA.Lanes[Lane.Front].Cards.Clear();

            LaneBattleResolver.ResolveTurn(sideA, sideB, tickNumber: 1);

            int before = marked.CurrentHealth;
            marked.ApplyDamage(2);
            Assert.AreEqual(before - 2, marked.CurrentHealth,
                "The mark had its one clash window and must be gone by the next hit, not still armed.");
        }

        // ---------- AllLaneAttackBuff (Thunder Decree) + the shared +3/unit spell-buff cap ----------

        [Test]
        public void ThunderDecree_BuffsEveryLivingFriendlyUnitAcrossAllThreeLanes()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "thunder_decree");
            var caster = MakeState();
            var opponent = MakeState();
            BattleCardInstance front = MakeUnit(rarity: 7);
            BattleCardInstance middle = MakeUnit(rarity: 7);
            BattleCardInstance back = MakeUnit(rarity: 7);
            int frontBefore = front.Attack, middleBefore = middle.Attack, backBefore = back.Attack;
            caster.Lanes[Lane.Front].Cards.Add(front);
            caster.Lanes[Lane.Middle].Cards.Add(middle);
            caster.Lanes[Lane.Back].Cards.Add(back);

            spell.Cast(caster, opponent, Lane.Front); // targetLane is irrelevant for an all-lane effect

            Assert.AreEqual(frontBefore + 1, front.Attack);
            Assert.AreEqual(middleBefore + 1, middle.Attack);
            Assert.AreEqual(backBefore + 1, back.Attack);
        }

        [Test]
        public void SpellAttackBuff_CapsAtThreePerUnit_AcrossMultipleSpellsCombined()
        {
            BattleCardInstance unit = MakeUnit(rarity: 7);
            int before = unit.Attack;

            unit.BuffAttack(2); // War Cry-sized
            unit.BuffAttack(2); // another buff - only 1 of Attack should land (room is 3-2=1)

            Assert.AreEqual(before + 3, unit.Attack, "Cumulative spell-sourced Attack buff must cap at +3/unit.");
        }

        private static PlayerBattleState MakeState() =>
            new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);

        private static BattleCardInstance MakeUnit(int rarity)
        {
            var data = new CardData
            {
                id = "phase3_test_card_" + System.Guid.NewGuid().ToString("N"),
                name = "Phase 3 Catalog Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = rarity,
            };
            return new BattleCardInstance(Card.FromData(data), isPlayerOwned: true, laneAttackBonus: 0, laneHealthBonus: 0);
        }
    }
}
