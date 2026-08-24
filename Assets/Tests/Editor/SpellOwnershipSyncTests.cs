using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Save;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Spell-Book Acquisition + Ownership Sync (LOCKED 2026-08-24) - SpellOwnershipSync is the
    /// service that keeps PlayerProfile.ownedSpellIds/equippedSpellIds in step with real
    /// Avatar-level/stage progress. Deliberately proves it never grants the two SpellBookGrant-only
    /// spells (Sun Lance, Tempest Brand) - that acquisition channel is a different one, see
    /// SpellBookGrantTests - and that it is safe to call repeatedly (idempotent, per every one of
    /// its real call sites: level-up, stage first-clear, Spell Book grant, new-profile creation,
    /// and the migration repair pass).
    /// </summary>
    public class SpellOwnershipSyncTests
    {
        [Test]
        public void SynchronizeEligibleSpellOwnership_FreshProfile_OwnsExactlyTheStarterFour()
        {
            var profile = new PlayerProfile();

            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            CollectionAssert.AreEquivalent(new[] { "firestorm", "mend", "war_cry", "divine_bolt" }, profile.ownedSpellIds);
        }

        [Test]
        public void SynchronizeEligibleSpellOwnership_RealProgress_AddsEveryReachableSpell_ButNeverTheSpellBookGatedOnes()
        {
            var profile = new PlayerProfile
            {
                avatarLevel = 12,
                unlockedStageIds = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" },
            };

            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            CollectionAssert.Contains(profile.ownedSpellIds, "cinder_lash");
            CollectionAssert.Contains(profile.ownedSpellIds, "ember_wave");
            CollectionAssert.Contains(profile.ownedSpellIds, "fault_line");
            CollectionAssert.Contains(profile.ownedSpellIds, "vital_spark");
            CollectionAssert.Contains(profile.ownedSpellIds, "renewal");
            CollectionAssert.Contains(profile.ownedSpellIds, "rallying_gale");
            CollectionAssert.Contains(profile.ownedSpellIds, "banner_of_ashes");
            CollectionAssert.Contains(profile.ownedSpellIds, "stone_judgment");
            CollectionAssert.DoesNotContain(profile.ownedSpellIds, "sun_lance",
                "Sun Lance is SpellBookGrant-only - progression sync must never grant it.");
            CollectionAssert.DoesNotContain(profile.ownedSpellIds, "tempest_brand",
                "Tempest Brand is SpellBookGrant-only - progression sync must never grant it.");
        }

        [Test]
        public void SynchronizeEligibleSpellOwnership_NeverRemovesAnAlreadyOwnedSpellBookSpell()
        {
            var profile = new PlayerProfile();
            profile.ownedSpellIds.Add("sun_lance"); // as if SpellBookGrant already ran.

            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            CollectionAssert.Contains(profile.ownedSpellIds, "sun_lance",
                "The sync service only ever adds progression-eligible spells - it must never revoke a Spell-Book-granted one.");
        }

        [Test]
        public void SynchronizeEligibleSpellOwnership_CalledTwice_IsIdempotent()
        {
            var profile = new PlayerProfile { avatarLevel = 12, unlockedStageIds = new List<string> { "1-2", "2-4" } };

            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);
            List<string> firstPass = new List<string>(profile.ownedSpellIds);
            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            CollectionAssert.AreEquivalent(firstPass, profile.ownedSpellIds);
            Assert.AreEqual(firstPass.Distinct().Count(), profile.ownedSpellIds.Count, "No duplicate entries from a second sync call.");
        }

        [Test]
        public void SynchronizeEligibleSpellOwnership_EquippedSpellIds_OnlyBackfilledWhenEmpty()
        {
            var profile = new PlayerProfile { avatarLevel = 12, unlockedStageIds = new List<string> { "1-2" } };
            profile.equippedSpellIds = new List<string> { "firestorm" }; // a real, deliberate manual loadout.

            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            CollectionAssert.AreEqual(new[] { "firestorm" }, profile.equippedSpellIds,
                "A non-empty equippedSpellIds is the player's real manual loadout choice - the sync service must never overwrite it.");
        }

        [Test]
        public void SynchronizeEligibleSpellOwnership_EmptyEquippedSpellIds_GetsARealAutoEquippedLoadout()
        {
            var profile = new PlayerProfile { avatarLevel = 1, equippedSpellIds = new List<string>() };

            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            Assert.IsTrue(profile.equippedSpellIds.Count > 0, "An empty equippedSpellIds must never stay empty once real ownedSpellIds exist.");
        }

        [Test]
        public void SynchronizeEligibleSpellOwnership_NullProfile_DoesNotThrow()
        {
            Assert.DoesNotThrow(() => SpellOwnershipSync.SynchronizeEligibleSpellOwnership(null));
        }
    }
}
