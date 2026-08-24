using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// SPELL_CATALOG_v1.md's Phase-1 ship slice (14 spells) - the starter four were already live;
    /// this proves the other ten (Cinder Lash, Ember Wave, Fault Line, Vital Spark, Renewal,
    /// Rallying Gale, Banner of Ashes, Sun Lance, Stone Judgment, Tempest Brand) exist with the
    /// locked cost/cooldown/effect/magnitude and are actually castable through AvatarSpell.Cast -
    /// not just constructed. Where/when each unlocks (Ch1-2, Avatar L5, Ch2-4, spell books, etc.)
    /// is explicitly out of scope - see AvatarSpell.CreatePhase1Catalog's own doc comment.
    /// </summary>
    public class SpellCatalogPhase1Tests
    {
        [Test]
        public void CreateDefaultSpellbook_IsUnaffected_StillExactlyTheStarterFour()
        {
            List<AvatarSpell> starter = AvatarSpell.CreateDefaultSpellbook();
            CollectionAssert.AreEqual(
                new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt" },
                starter.Select(s => s.Name).ToList(),
                "The Phase-1 catalog addition must not change what a new player starts with.");
        }

        [Test]
        public void CreatePhase1Catalog_ContainsExactlyFourteenSpells_StarterFourPlusTenNew()
        {
            List<AvatarSpell> catalog = AvatarSpell.CreatePhase1Catalog();
            Assert.AreEqual(14, catalog.Count);

            CollectionAssert.AreEqual(
                new[]
                {
                    "Firestorm", "Mend", "War Cry", "Divine Bolt",
                    "Cinder Lash", "Ember Wave", "Fault Line", "Vital Spark", "Renewal",
                    "Rallying Gale", "Banner of Ashes", "Sun Lance", "Stone Judgment", "Tempest Brand",
                },
                catalog.Select(s => s.Name).ToList());
        }

        [TestCase("Cinder Lash", 18, 2, SpellEffect.LaneDamage, 2)]
        [TestCase("Ember Wave", 26, 3, SpellEffect.LaneDamage, 3)]
        [TestCase("Fault Line", 44, 5, SpellEffect.LaneDamage, 5)]
        [TestCase("Vital Spark", 18, 2, SpellEffect.LaneHeal, 2)]
        [TestCase("Renewal", 45, 5, SpellEffect.LaneHeal, 6)]
        [TestCase("Rallying Gale", 24, 3, SpellEffect.LaneAttackBuff, 1)]
        [TestCase("Banner of Ashes", 55, 5, SpellEffect.LaneAttackBuff, 3)]
        [TestCase("Sun Lance", 35, 3, SpellEffect.AvatarStrike, 75)]
        [TestCase("Stone Judgment", 80, 7, SpellEffect.AvatarStrike, 120)]
        [TestCase("Tempest Brand", 36, 4, SpellEffect.LaneDamage, 3)]
        public void NewSpell_MatchesTheLockedCatalogRow(string name, int energyCost, int cooldownTicks, SpellEffect effect, int magnitude)
        {
            AvatarSpell spell = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == name);
            Assert.AreEqual(energyCost, spell.EnergyCost, $"{name}: Energy cost.");
            Assert.AreEqual(cooldownTicks, spell.CooldownTicks, $"{name}: cooldown ticks.");
            Assert.AreEqual(effect, spell.Effect, $"{name}: effect type.");
            Assert.AreEqual(magnitude, spell.Magnitude, $"{name}: magnitude.");
        }

        [TestCase("Firestorm", "firestorm", SpellSchool.Andras)]
        [TestCase("Mend", "mend", SpellSchool.Ktini)]
        [TestCase("War Cry", "war_cry", SpellSchool.Pnevmas)] // catalog-verified correction, not Andras
        [TestCase("Divine Bolt", "divine_bolt", SpellSchool.Pnevmas)]
        [TestCase("Cinder Lash", "cinder_lash", SpellSchool.Andras)]
        [TestCase("Ember Wave", "ember_wave", SpellSchool.Andras)]
        [TestCase("Fault Line", "fault_line", SpellSchool.Ktini)]
        [TestCase("Vital Spark", "vital_spark", SpellSchool.Ktini)]
        [TestCase("Renewal", "renewal", SpellSchool.Ktini)]
        [TestCase("Rallying Gale", "rallying_gale", SpellSchool.Pnevmas)]
        [TestCase("Banner of Ashes", "banner_of_ashes", SpellSchool.Andras)]
        [TestCase("Sun Lance", "sun_lance", SpellSchool.Pnevmas)]
        [TestCase("Stone Judgment", "stone_judgment", SpellSchool.Ktini)]
        [TestCase("Tempest Brand", "tempest_brand", SpellSchool.Pnevmas)]
        public void EverySpell_HasTheRealCatalogIdAndSchool(string name, string id, SpellSchool school)
        {
            AvatarSpell spell = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == name);
            Assert.AreEqual(id, spell.Id, $"{name}: catalog id.");
            Assert.AreEqual(school, spell.School, $"{name}: school.");
        }

        [Test]
        public void EveryPhase1SpellId_IsUniqueAndNeverEmpty()
        {
            List<AvatarSpell> catalog = AvatarSpell.CreatePhase1Catalog();
            Assert.IsTrue(catalog.All(s => !string.IsNullOrEmpty(s.Id)), "Every real catalog spell needs a stable id - it's the ownedSpellIds/equippedSpellIds join key.");
            Assert.AreEqual(catalog.Count, catalog.Select(s => s.Id).Distinct().Count(), "No two catalog spells may share an id.");
        }

        [Test]
        public void EveryNewSpell_ExistsExactlyOnce_NoAccidentalDuplicateOrTypo()
        {
            List<AvatarSpell> catalog = AvatarSpell.CreatePhase1Catalog();
            foreach (string name in new[]
            {
                "Cinder Lash", "Ember Wave", "Fault Line", "Vital Spark", "Renewal",
                "Rallying Gale", "Banner of Ashes", "Sun Lance", "Stone Judgment", "Tempest Brand",
            })
            {
                Assert.AreEqual(1, catalog.Count(s => s.Name == name), $"Expected exactly one '{name}'.");
            }
        }

        // ---------- Real execution, not just data construction ----------

        private static Card MakeCard(string id, string element = "Andras") => Card.FromData(new CardData
        {
            id = id, name = "Spell Catalog Test Card", art_file = "x.png", element = element, type = "warrior", rarity = 1,
        });

        /// <summary>Explicit, in-range authored stats (rarity 4's legal Health band is 4-6 per
        /// Card.RarityTable) so the Renewal heal-room test has real headroom to heal into,
        /// regardless of what a rarity-1 placeholder card's Health would otherwise resolve to (its
        /// band is only 1-2, too small to show a +6 heal doing anything).</summary>
        private static Card MakeCardWithHealth(string id, int health) => Card.FromData(new CardData
        {
            id = id, name = "Spell Catalog Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = 4,
            attack = 4, health = health,
        });

        private static PlayerBattleState MakeState() =>
            new PlayerBattleState(new[] { MakeCard("spell_catalog_deck_card") }, resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);

        [Test]
        public void ConeLaneDamageSpell_ThroughAnEmptyEnemyLane_HitsTheAvatarForRealAvatarScaleDamage()
        {
            AvatarSpell tempestBrand = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == "Tempest Brand");
            PlayerBattleState caster = MakeState();
            PlayerBattleState opponent = MakeState();
            int startingHealth = opponent.AvatarHealth;

            int dealt = tempestBrand.Cast(caster, opponent, Lane.Front);

            Assert.Greater(dealt, 0, "An empty enemy lane must let LaneDamage through to the Avatar, not silently do nothing.");
            Assert.AreEqual(startingHealth - dealt, opponent.AvatarHealth);
        }

        [Test]
        public void NewLaneHealSpell_RestoresRealHealthToADamagedFriendlyUnit()
        {
            AvatarSpell renewal = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == "Renewal");
            PlayerBattleState caster = MakeState();
            PlayerBattleState opponent = MakeState();
            var unit = new BattleCardInstance(MakeCardWithHealth("renewal_target", health: 6), isPlayerOwned: true, laneAttackBonus: 0, laneHealthBonus: 0);
            unit.ApplyDamage(5); // leave exactly 1 of 6 Health so a +6 heal has real, unclamped room to matter
            caster.Lanes[Lane.Front].Cards.Add(unit);
            int healthBefore = unit.CurrentHealth;

            renewal.Cast(caster, opponent, Lane.Front);

            Assert.Greater(unit.CurrentHealth, healthBefore, "Renewal must actually restore Health, not just exist as data.");
        }

        [Test]
        public void NewLaneAttackBuffSpell_PermanentlyRaisesAttackOfAFriendlyUnit()
        {
            AvatarSpell bannerOfAshes = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == "Banner of Ashes");
            PlayerBattleState caster = MakeState();
            PlayerBattleState opponent = MakeState();
            var unit = new BattleCardInstance(MakeCard("banner_target"), isPlayerOwned: true, laneAttackBonus: 0, laneHealthBonus: 0);
            caster.Lanes[Lane.Front].Cards.Add(unit);
            int attackBefore = unit.Attack;

            bannerOfAshes.Cast(caster, opponent, Lane.Front);

            Assert.AreEqual(attackBefore + 3, unit.Attack, "Banner of Ashes must grant exactly its catalog +3 Attack.");
        }

        [Test]
        public void NewAvatarStrikeSpell_DealsExactlyItsFlatCatalogMagnitude()
        {
            AvatarSpell sunLance = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == "Sun Lance");
            PlayerBattleState caster = MakeState();
            PlayerBattleState opponent = MakeState();
            int startingHealth = opponent.AvatarHealth;

            int dealt = sunLance.Cast(caster, opponent, Lane.Front);

            Assert.AreEqual(75, dealt);
            Assert.AreEqual(startingHealth - 75, opponent.AvatarHealth);
        }
    }
}
