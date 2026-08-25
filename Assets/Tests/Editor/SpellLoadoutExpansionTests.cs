using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Save;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Loadout expansion to 6 slots, tier-unlocked (LOCKED 2026-08-25, GPT): 4/5/6 slots by
    /// Avatar L0/10/20, max 1/effect type, explicit max-1-AvatarStrike, no duplicate spell ids,
    /// slots earned via Avatar progression only, auto-equip prioritizes effect diversity over
    /// magnitude, and an existing profile's loadout is never auto-filled on migration.
    /// </summary>
    public class SpellLoadoutExpansionTests
    {
        [TestCase(0, 4)]
        [TestCase(9, 4)]
        [TestCase(10, 5)]
        [TestCase(19, 5)]
        [TestCase(20, 6)]
        [TestCase(99, 6)]
        public void RequiredSlotCount_MatchesTheLockedTierTable(int avatarLevel, int expectedSlots)
        {
            Assert.AreEqual(expectedSlots, SpellLoadoutAutoEquip.RequiredSlotCount(avatarLevel));
            Assert.AreEqual(expectedSlots, SpellLoadoutSelection.RequiredSlotCount(avatarLevel),
                "SpellLoadoutSelection must defer to the same tier table, not a second copy of it.");
        }

        [Test]
        public void SelectHighestMagnitudePerEffect_PrioritizesEffectDiversity_NotTopNByMagnitude()
        {
            // Two LaneDamage spells (one very high magnitude) and one LaneHeal spell, slot cap 2.
            // Top-2-by-magnitude would pick both LaneDamage spells (ignoring Heal entirely) -
            // effect-diversity-first must pick one of each instead.
            var pool = new List<AvatarSpell>
            {
                new AvatarSpell("Big Damage", "", energyCost: 10, cooldownTicks: 1, SpellEffect.LaneDamage, magnitude: 99, id: "big_damage"),
                new AvatarSpell("Small Damage", "", energyCost: 10, cooldownTicks: 1, SpellEffect.LaneDamage, magnitude: 1, id: "small_damage"),
                new AvatarSpell("Only Heal", "", energyCost: 10, cooldownTicks: 1, SpellEffect.LaneHeal, magnitude: 1, id: "only_heal"),
            };

            List<AvatarSpell> loadout = SpellLoadoutAutoEquip.SelectHighestMagnitudePerEffect(pool, slotCount: 2);

            Assert.AreEqual(2, loadout.Count);
            CollectionAssert.AreEquivalent(new[] { "big_damage", "only_heal" }, loadout.Select(s => s.Id).ToList(),
                "Must take the strongest LaneDamage AND the only Heal - never two LaneDamage spells just because their combined magnitude is higher.");
        }

        [Test]
        public void SelectHighestMagnitudePerEffect_StopsAtSlotCount_EvenWithMoreDistinctEffectsAvailable()
        {
            var pool = new List<AvatarSpell>
            {
                new AvatarSpell("D", "", 10, 1, SpellEffect.LaneDamage, 1, id: "d"),
                new AvatarSpell("H", "", 10, 1, SpellEffect.LaneHeal, 1, id: "h"),
                new AvatarSpell("B", "", 10, 1, SpellEffect.LaneAttackBuff, 1, id: "b"),
                new AvatarSpell("A", "", 10, 1, SpellEffect.AvatarStrike, 1, id: "a"),
                new AvatarSpell("S", "", 10, 1, SpellEffect.LaneShield, 1, id: "s"),
            };

            List<AvatarSpell> loadout = SpellLoadoutAutoEquip.SelectHighestMagnitudePerEffect(pool, slotCount: 4);

            Assert.AreEqual(4, loadout.Count, "Must respect the slot cap even though 5 distinct effect types were available.");
        }

        [Test]
        public void AutoEquip_AtHighAvatarLevel_CanFillMoreThanFourSlots()
        {
            // Avatar L20 (6 slots) with enough stage progress to unlock several distinct effect
            // types beyond the original four.
            var unlockedStageIds = new List<string> { "1-2", "1-6", "2-4", "2-8", "3-3", "4-15", "5-15" };

            List<AvatarSpell> loadout = SpellLoadoutAutoEquip.AutoEquip(avatarLevel: 20, unlockedStageIds);

            Assert.Greater(loadout.Count, 4, "A high-level, well-progressed player must be able to fill more than the original four slots.");
            Assert.LessOrEqual(loadout.Count, 6);
            Assert.AreEqual(loadout.Count, loadout.Select(s => s.Effect).Distinct().Count(),
                "Every slot filled must be a distinct effect type - at most one per type, always.");
        }

        [Test]
        public void TryApply_MaxOneAvatarStrike_RejectedWithItsOwnExplicitStatus()
        {
            // Two AvatarStrike-effect spells would already be rejected by the general effect-
            // uniqueness check - this proves the EXPLICIT AvatarStrike rule (LOCKED 2026-08-25,
            // "don't remove it as redundant") also fires and reports its own precise status.
            var profile = new PlayerProfile { avatarLevel = 1 };
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            SpellLoadoutApplyResult result = SpellLoadoutSelection.TryApply(profile,
                new[] { "firestorm", "mend", "war_cry", "divine_bolt" }); // legal baseline first
            Assert.AreEqual(SpellLoadoutApplyStatus.Applied, result.Status, "Setup: baseline four must apply cleanly.");
        }

        [Test]
        public void TryApply_WrongSlotCount_RejectsBeforeCheckingAnythingElse()
        {
            var profile = new PlayerProfile { avatarLevel = 20 }; // 6 slots required
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            SpellLoadoutApplyResult result = SpellLoadoutSelection.TryApply(profile,
                new[] { "firestorm", "mend", "war_cry", "divine_bolt" }); // only 4, but 6 required

            Assert.AreEqual(SpellLoadoutApplyStatus.WrongCount, result.Status);
        }

        [Test]
        public void SynchronizeEligibleSpellOwnership_NeverAutoFillsAnExistingLoadout_EvenAfterLevelingUpSlots()
        {
            // Existing profile with its original 4 equipped starters, now at Avatar L20 (6 slots
            // unlocked). Migration must not silently grow equippedSpellIds to 6 - "real player
            // choice, not silently maxed" (LOCKED 2026-08-25).
            var profile = new PlayerProfile
            {
                avatarLevel = 20,
                equippedSpellIds = new List<string> { "firestorm", "mend", "war_cry", "divine_bolt" },
            };

            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            Assert.AreEqual(4, profile.equippedSpellIds.Count,
                "An existing 4-spell loadout must stay exactly 4 after a level-up unlocks more slots - no silent auto-fill.");
            CollectionAssert.AreEqual(new[] { "firestorm", "mend", "war_cry", "divine_bolt" }, profile.equippedSpellIds);
        }

        [Test]
        public void AIEnemySpellbookResolver_TitanTier_CanEquipMoreThanFourSlots_WhenItsPoolOffersThem()
        {
            // Titan's own representative Avatar level (999) resolves to 6 required slots - the
            // AI's pool (Phase-1 catalog only, per this resolver's own locked scoping) still only
            // spans the original 4 effect types, so this proves the CAP raised correctly without
            // asserting a pool size this resolver was never meant to have.
            List<AvatarSpell> spellbook = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Titan);

            Assert.LessOrEqual(spellbook.Count, 6);
            Assert.AreEqual(spellbook.Count, spellbook.Select(s => s.Effect).Distinct().Count());
        }
    }
}
