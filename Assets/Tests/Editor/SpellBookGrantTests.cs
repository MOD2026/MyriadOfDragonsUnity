using System.Collections.Generic;
using System.IO;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Save;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Spell-Book Acquisition + Ownership Sync (LOCKED 2026-08-24) - the real Ch2/Ch3 finale grant
    /// transaction: "verify finale victory -> verify not already claimed -> resolve book definition
    /// -> add spell ids not already owned -> record claim -> save once -> report unlocked names."
    /// UnknownSpellId is not exercised here - the real GrantedSpellIdsByFinaleStageId table only
    /// ever names real catalog ids ("sun_lance", "tempest_brand"), so that branch is unreachable
    /// through the public TryGrant API as shipped; it exists purely as defense against a future
    /// edit to that table naming a typo'd id, which SpellBookGrant.cs's own comment documents.
    /// </summary>
    public class SpellBookGrantTests
    {
        private string _scratchDirectory;

        [SetUp]
        public void SetUp()
        {
            _scratchDirectory = Path.Combine(Path.GetTempPath(), "MOD_SpellBookGrantTests_" + Path.GetRandomFileName());
            Directory.CreateDirectory(_scratchDirectory);
            SaveSystem.OverrideRootDirectoryForTests(_scratchDirectory);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            try
            {
                if (Directory.Exists(_scratchDirectory)) Directory.Delete(_scratchDirectory, recursive: true);
            }
            catch (IOException) { }
        }

        [Test]
        public void TryGrant_FinaleCleared_GrantsTheBooksSpellAndReportsItsName()
        {
            var profile = new PlayerProfile { claimedStageRewardIds = new List<string> { "2-21" } };

            SpellBookGrantResult result = SpellBookGrant.TryGrant(profile, "2-21", persist: false);

            Assert.AreEqual(SpellBookGrantStatus.Applied, result.Status);
            CollectionAssert.Contains(result.UnlockedSpellNames, "Sun Lance");
            CollectionAssert.Contains(profile.ownedSpellIds, "sun_lance");
        }

        [Test]
        public void TryGrant_Ch3Finale_GrantsTempestBrand()
        {
            var profile = new PlayerProfile { claimedStageRewardIds = new List<string> { "3-30" } };

            SpellBookGrantResult result = SpellBookGrant.TryGrant(profile, "3-30", persist: false);

            Assert.AreEqual(SpellBookGrantStatus.Applied, result.Status);
            CollectionAssert.Contains(result.UnlockedSpellNames, "Tempest Brand");
            CollectionAssert.Contains(profile.ownedSpellIds, "tempest_brand");
        }

        [Test]
        public void TryGrant_FinaleNotCleared_RefusesAndOwnsNothing()
        {
            var profile = new PlayerProfile { claimedStageRewardIds = new List<string>() };

            SpellBookGrantResult result = SpellBookGrant.TryGrant(profile, "2-21", persist: false);

            Assert.AreEqual(SpellBookGrantStatus.FinaleNotCleared, result.Status);
            CollectionAssert.DoesNotContain(profile.ownedSpellIds, "sun_lance");
            Assert.AreEqual(0, result.UnlockedSpellNames.Count);
        }

        [Test]
        public void TryGrant_AlreadyClaimed_RefusesASecondGrant()
        {
            var profile = new PlayerProfile { claimedStageRewardIds = new List<string> { "2-21" } };
            SpellBookGrant.TryGrant(profile, "2-21", persist: false);

            SpellBookGrantResult second = SpellBookGrant.TryGrant(profile, "2-21", persist: false);

            Assert.AreEqual(SpellBookGrantStatus.AlreadyClaimed, second.Status);
            Assert.AreEqual(0, second.UnlockedSpellNames.Count, "A repeat claim must never re-report the spell as newly unlocked.");
        }

        [Test]
        public void TryGrant_UnknownFinaleStageId_Refuses()
        {
            var profile = new PlayerProfile { claimedStageRewardIds = new List<string> { "9-99" } };

            SpellBookGrantResult result = SpellBookGrant.TryGrant(profile, "9-99", persist: false);

            Assert.AreEqual(SpellBookGrantStatus.UnknownFinaleStage, result.Status);
        }

        [Test]
        public void TryGrant_NullOrEmptyFinaleStageId_Refuses()
        {
            var profile = new PlayerProfile();

            Assert.AreEqual(SpellBookGrantStatus.UnknownFinaleStage, SpellBookGrant.TryGrant(profile, null, persist: false).Status);
            Assert.AreEqual(SpellBookGrantStatus.UnknownFinaleStage, SpellBookGrant.TryGrant(profile, "", persist: false).Status);
        }

        [Test]
        public void TryGrant_NullProfile_RefusesRatherThanThrowing()
        {
            Assert.DoesNotThrow(() =>
            {
                SpellBookGrantResult result = SpellBookGrant.TryGrant(null, "2-21", persist: false);
                Assert.AreEqual(SpellBookGrantStatus.UnknownFinaleStage, result.Status);
            });
        }

        [Test]
        public void TryGrant_AppliedGrant_AlsoRunsOwnershipSync_SoOtherEligibleSpellsCatchUpInTheSamePass()
        {
            var profile = new PlayerProfile
            {
                avatarLevel = 5, // Ember Wave's own real Avatar-level gate.
                claimedStageRewardIds = new List<string> { "2-21" },
                ownedSpellIds = new List<string> { "firestorm", "mend", "war_cry", "divine_bolt" },
            };

            SpellBookGrant.TryGrant(profile, "2-21", persist: false);

            CollectionAssert.Contains(profile.ownedSpellIds, "sun_lance");
            CollectionAssert.Contains(profile.ownedSpellIds, "ember_wave",
                "The grant transaction's own required SpellOwnershipSync call should backfill any other newly-eligible spell in the same pass.");
        }

        [Test]
        public void TryGrant_WithPersistTrue_ActuallySavesTheGrantToDisk()
        {
            var profile = new PlayerProfile { claimedStageRewardIds = new List<string> { "2-21" } };

            SpellBookGrant.TryGrant(profile, "2-21", persist: true);

            PlayerProfile reloaded = SaveSystem.Load();
            CollectionAssert.Contains(reloaded.ownedSpellIds, "sun_lance");
        }

        [Test]
        public void TryGrant_WithPersistFalse_DoesNotTouchDisk()
        {
            var profile = new PlayerProfile { claimedStageRewardIds = new List<string> { "2-21" } };

            SpellBookGrant.TryGrant(profile, "2-21", persist: false);

            Assert.IsFalse(File.Exists(SaveSystem.SavePath), "persist:false must not write anything - the caller controls when the save happens.");
        }
    }
}
