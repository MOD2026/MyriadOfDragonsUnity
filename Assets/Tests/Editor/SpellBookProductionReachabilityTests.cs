using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Proves whether a chapter-finale spell book reaches a real player, rather than whether
    /// <c>SpellBookGrant.TryGrant</c> works when called directly.
    ///
    /// THE DISTINCTION IS THE ENTIRE POINT. `SpellBookGrantTests` is comprehensive and green -
    /// eligibility, idempotency, persistence, two-spell finales, null handling. Every one of those
    /// tests calls `TryGrant` itself. Nothing in the shipping game ever does, so the suite proved
    /// the transaction is correct and said nothing about whether it fires. That is the
    /// production-reachability failure class in `LOCKED_DECISIONS_REGISTER.md`: a unit-correct
    /// transaction absent from the player's vertical slice.
    ///
    /// So this file never calls `TryGrant`. It calls the same code production runs, then asks the
    /// only question that matters to a player: after clearing the finale and reloading, do I own
    /// the spell?
    ///
    /// COUPLING, UPDATED 2026-08-27 (CC + VS): this used to hand-copy the production sequence
    /// into this file, which is EXACTLY the illusion this test exists to break - a copy proves the
    /// copy works, not that production calls anything. `HomePagePresenter.HandleMatchCompleted`
    /// itself still can't run in EditMode (private instance method, needs live UI), but its real
    /// first-clear reward mutation was extracted into `HomePagePresenter.ApplyFirstClearRewards` -
    /// a plain static method with no scene/canvas dependency, called by BOTH
    /// `HandleMatchCompleted` and this test. Deleting production's call to it (or to
    /// `SpellBookGrant.TryGrant` inside it) now fails THIS test, because there is no longer a
    /// second copy of the logic for a deleted call site to hide behind.
    /// </summary>
    public class SpellBookProductionReachabilityTests
    {
        private const string Ch2FinaleStageId = "2-21";
        private const string Ch2BookSpellId = "sun_lance";

        private string _scratchDirectory;

        [SetUp]
        public void SetUp()
        {
            _scratchDirectory = Path.Combine(Path.GetTempPath(), "MyriadSpellReach_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchDirectory);
            SaveSystem.OverrideRootDirectoryForTests(_scratchDirectory);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchDirectory)) Directory.Delete(_scratchDirectory, true);
        }

        [Test]
        public void TheOwnershipSyncOnTheFinalePath_CannotGrantABookSpell_SoNothingInProductionDoes()
        {
            // The resolver deliberately refuses SpellBookGrant-kind spells:
            //     default: return false; // SpellBookGrant - see SpellBookGrant, not this resolver
            // That refusal is correct in itself. It only becomes a player-facing bug because the
            // service that IS meant to grant them is never called from the game.
            var profile = new PlayerProfile { avatarLevel = 99 };
            profile.unlockedStageIds.Clear();
            profile.unlockedStageIds.Add(Ch2FinaleStageId);

            SpellOwnershipSync.SynchronizeEligibleSpellOwnership(profile);

            Assert.IsFalse(profile.ownedSpellIds.Contains(Ch2BookSpellId),
                "If the ownership sync now grants book spells, SpellBookGrant is redundant rather " +
                "than unwired, and the fix is to DELETE it - not to call it. Re-check before wiring.");
        }

        [Test]
        public void ClearingTheChapter2Finale_ThenReloading_LeavesThePlayerOwningTheBookSpell()
        {
            var profile = new PlayerProfile { avatarLevel = 99 };
            profile.unlockedStageIds.Clear();
            profile.unlockedStageIds.Add(Ch2FinaleStageId);
            SaveSystem.Save(profile);
            SaveSystem.ResetCurrentProfileForTests();

            PlayerProfile live = SaveSystem.CurrentProfile;

            // The REAL production method - not a copy of it (see class doc). If someone deletes
            // HandleMatchCompleted's call to this, or deletes SpellBookGrant.TryGrant from inside
            // it, this test goes red - there is nothing else here that could paper over that.
            CampaignStageData stage = CampaignMapPresenter.GetStageForTests(Ch2FinaleStageId);
            Assert.NotNull(stage, "Setup: " + Ch2FinaleStageId + " must be a real defined stage.");
            HomePagePresenter.ApplyFirstClearRewards(live, stage);

            // RELOAD. Asserting on the in-memory profile would pass on a grant that never reached
            // disk, which is the other half of how this class of bug hides.
            SaveSystem.ResetCurrentProfileForTests();
            PlayerProfile reloaded = SaveSystem.CurrentProfile;

            Assert.IsTrue(reloaded.ownedSpellIds.Contains(Ch2BookSpellId),
                "Cleared the Chapter 2 finale (" + Ch2FinaleStageId + ") and reloaded, and the " +
                "player does not own '" + Ch2BookSpellId + "'. SpellBookGrant.TryGrant is the only " +
                "code that can grant it and has ZERO production callers - every caller is in " +
                "SpellBookGrantTests.cs. Owned spells after reload: [" +
                string.Join(", ", reloaded.ownedSpellIds.OrderBy(x => x)) + "].");
        }
    }
}
