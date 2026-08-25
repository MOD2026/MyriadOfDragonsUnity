using System;
using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Bound Captive Fodder (Prison) + Academy research choice, against the locked design
    /// (register: "Academy + Prison real design LOCKED", 2026-08-25).
    ///
    /// The Prison tests are mostly about what Fodder must NOT be able to do. Full-card capture was
    /// rejected for specific abuse vectors, so the guardrails ARE the feature: one capture per day,
    /// same-opponent cooldown, no friend/guildmate farming, nothing from bot or practice matches,
    /// idempotent grants, and exactly one consumption path.
    /// </summary>
    public class PrisonAndAcademyTests
    {
        private static readonly DateTime Day1 = new DateTime(2026, 8, 25, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Day2 = new DateTime(2026, 8, 26, 12, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Day4 = new DateTime(2026, 8, 28, 12, 0, 0, DateTimeKind.Utc);

        private static BoundCaptiveMatchContext EligibleWin(string opponent = "opp-1") =>
            new BoundCaptiveMatchContext
            {
                AttackerWon = true,
                IsRankedAsyncLadder = true,
                IsTutorialOrBotOrPractice = false,
                IsPrivateOrRematch = false,
                OpponentIsGuildmateOrFriend = false,
                OpponentAccountId = opponent,
                OpponentDefenseSnapshotCardIds = new List<string> { "warrior", "cleric" },
            };

        // ---------- Prison: the happy path ----------

        [Test]
        public void AnEligibleWin_GrantsExactlyOneFodderItem()
        {
            var state = new BoundCaptivePrisonState();

            BoundCaptiveCaptureResult r = BoundCaptiveFodderRules.TryCapture(state, EligibleWin(), "cap-1", Day1);

            Assert.IsTrue(r.Granted);
            Assert.AreEqual(1, state.Items.Count);
            Assert.IsFalse(r.Item.Consumed);
        }

        [Test]
        public void AFodderItem_CarriesNoCombatStats_OnlyProvenance()
        {
            var state = new BoundCaptivePrisonState();
            BoundCaptiveFodderRules.TryCapture(state, EligibleWin(), "cap-1", Day1);

            // The type itself is the assertion: if a combat stat were ever added, this reflection
            // check fails and forces a conversation rather than shipping a playable capture.
            string[] fields = typeof(BoundCaptiveFodderItem).GetFields().Select(f => f.Name).ToArray();
            foreach (string forbidden in new[] { "Attack", "Health", "Hp", "Power", "Damage" })
                CollectionAssert.DoesNotContain(fields, forbidden,
                    "Fodder must never carry a combat stat - full-card capture was rejected by design.");
        }

        // ---------- Prison: guardrails ----------

        [Test]
        public void AFailedAttack_GrantsNothing()
        {
            var state = new BoundCaptivePrisonState();
            BoundCaptiveMatchContext ctx = EligibleWin();
            ctx.AttackerWon = false;

            BoundCaptiveCaptureResult r = BoundCaptiveFodderRules.TryCapture(state, ctx, "cap-1", Day1);

            Assert.AreEqual(BoundCaptiveCaptureStatus.AttackDidNotWin, r.Status);
            CollectionAssert.IsEmpty(state.Items);
        }

        [Test]
        public void TutorialBotAndPracticeMatches_NeverCapture()
        {
            foreach (Action<BoundCaptiveMatchContext> spoil in new Action<BoundCaptiveMatchContext>[]
                     {
                         c => c.IsTutorialOrBotOrPractice = true,
                         c => c.IsPrivateOrRematch = true,
                         c => c.IsRankedAsyncLadder = false,
                     })
            {
                var state = new BoundCaptivePrisonState();
                BoundCaptiveMatchContext ctx = EligibleWin();
                spoil(ctx);

                BoundCaptiveCaptureResult r = BoundCaptiveFodderRules.TryCapture(state, ctx, "cap-1", Day1);

                Assert.AreEqual(BoundCaptiveCaptureStatus.NotAnEligibleMatchType, r.Status);
                CollectionAssert.IsEmpty(state.Items);
            }
        }

        [Test]
        public void GuildmatesAndFriends_CannotBeFarmed()
        {
            var state = new BoundCaptivePrisonState();
            BoundCaptiveMatchContext ctx = EligibleWin();
            ctx.OpponentIsGuildmateOrFriend = true;

            BoundCaptiveCaptureResult r = BoundCaptiveFodderRules.TryCapture(state, ctx, "cap-1", Day1);

            Assert.AreEqual(BoundCaptiveCaptureStatus.ExcludedRelationship, r.Status);
            CollectionAssert.IsEmpty(state.Items);
        }

        [Test]
        public void OnlyOneCapture_PerUtcDay()
        {
            var state = new BoundCaptivePrisonState();
            BoundCaptiveFodderRules.TryCapture(state, EligibleWin("opp-1"), "cap-1", Day1);

            BoundCaptiveCaptureResult second =
                BoundCaptiveFodderRules.TryCapture(state, EligibleWin("opp-2"), "cap-2", Day1);

            Assert.AreEqual(BoundCaptiveCaptureStatus.DailyCaptureCapReached, second.Status);
            Assert.AreEqual(1, state.Items.Count);
        }

        [Test]
        public void ANewUtcDay_AllowsAnotherCapture()
        {
            var state = new BoundCaptivePrisonState();
            BoundCaptiveFodderRules.TryCapture(state, EligibleWin("opp-1"), "cap-1", Day1);

            BoundCaptiveCaptureResult next =
                BoundCaptiveFodderRules.TryCapture(state, EligibleWin("opp-2"), "cap-2", Day2);

            Assert.IsTrue(next.Granted);
            Assert.AreEqual(2, state.Items.Count);
        }

        [Test]
        public void ARepeatAgainstTheSameOpponent_IsBlocked_ByTheDailyCapOrTheCooldown()
        {
            var state = new BoundCaptivePrisonState();
            BoundCaptiveFodderRules.TryCapture(state, EligibleWin("opp-1"), "cap-1", Day1);

            BoundCaptiveCaptureResult repeat =
                BoundCaptiveFodderRules.TryCapture(state, EligibleWin("opp-1"), "cap-2", Day1.AddHours(1));

            // Deliberately accepts EITHER guard: same-day repeats are caught by the daily cap
            // first, and pinning which one fires would be asserting the ORDER of two independent
            // rules rather than the outcome that matters - that a same-day repeat grants nothing.
            Assert.IsFalse(repeat.Granted);
            Assert.AreEqual(1, state.Items.Count);
        }

        /// <summary>Documents a REAL GAP rather than hiding it: at the current placeholder value the
        /// same-opponent cooldown cannot ever fire, because the daily cap already blocks everything
        /// it would block. The locked design asks for this guardrail but never states a duration.
        /// This test passes today and will fail the moment someone sets a real (>= 2 day) value -
        /// at which point the redundancy is gone and this test should be deleted.</summary>
        [Test]
        public void TheSameOpponentCooldown_IsCurrentlyRedundant_AndTheNumberIsStillOpen()
        {
            Assert.IsTrue(BoundCaptiveFodderRules.SameOpponentCooldownIsRedundant,
                "If this now fails, a real cooldown value was set - good. Delete this test and " +
                "replace it with one asserting the cooldown actually blocks a cross-day repeat.");
            Assert.AreEqual(1, BoundCaptiveFodderRules.MaxCapturesPerUtcDay);
        }

        [Test]
        public void TheSameOpponent_BecomesAvailableAgainAfterTheCooldown()
        {
            var state = new BoundCaptivePrisonState();
            BoundCaptiveFodderRules.TryCapture(state, EligibleWin("opp-1"), "cap-1", Day1);

            BoundCaptiveCaptureResult later =
                BoundCaptiveFodderRules.TryCapture(state, EligibleWin("opp-1"), "cap-2", Day4);

            Assert.IsTrue(later.Granted);
        }

        [Test]
        public void AnOpponentWithNoEligibleCard_GrantsNothing()
        {
            var state = new BoundCaptivePrisonState();
            BoundCaptiveMatchContext ctx = EligibleWin();
            ctx.OpponentDefenseSnapshotCardIds = new List<string>();

            BoundCaptiveCaptureResult r = BoundCaptiveFodderRules.TryCapture(state, ctx, "cap-1", Day1);

            Assert.AreEqual(BoundCaptiveCaptureStatus.OpponentHasNoEligibleCard, r.Status);
        }

        [Test]
        public void ReplayingTheSameCaptureId_IsIdempotent()
        {
            var state = new BoundCaptivePrisonState();
            BoundCaptiveFodderRules.TryCapture(state, EligibleWin(), "cap-1", Day1);

            BoundCaptiveCaptureResult replay =
                BoundCaptiveFodderRules.TryCapture(state, EligibleWin(), "cap-1", Day1);

            Assert.AreEqual(BoundCaptiveCaptureStatus.AlreadyGranted, replay.Status);
            Assert.AreEqual(1, state.Items.Count, "A replayed grant must never duplicate the item.");
        }

        // ---------- Prison: the single consumption path ----------

        [Test]
        public void ConsumingAFodderItem_GrantsSacrificeCreditsOnTheExistingWallet()
        {
            var state = new BoundCaptivePrisonState();
            BoundCaptiveFodderRules.TryCapture(state, EligibleWin(), "cap-1", Day1);
            var profile = new PlayerProfile();
            int before = profile.collectionWallet.genericSacrificeCredits;

            BoundCaptiveConsumeResult r =
                BoundCaptiveFodderRules.TryConsumeForSacrificeCredit(state, "cap-1", profile);

            Assert.AreEqual(BoundCaptiveConsumeStatus.Consumed, r.Status);
            Assert.AreEqual(before + r.SacrificeCreditsGranted,
                profile.collectionWallet.genericSacrificeCredits,
                "Fodder must feed the EXISTING credit wallet, not a parallel currency.");
        }

        [Test]
        public void ItsYield_MatchesTheExistingBurnTable_SoPrisonIsNotABalanceLever()
        {
            for (int rarity = 0; rarity <= 5; rarity++)
                Assert.AreEqual(CollectionBurnRules.GetGenericSacrificeYield(rarity),
                    BoundCaptiveFodderRules.SacrificeYieldFor(rarity),
                    "Fodder must never be a better or worse credit source than burning a real card.");
        }

        [Test]
        public void AConsumedItem_CannotBeConsumedTwice()
        {
            var state = new BoundCaptivePrisonState();
            BoundCaptiveFodderRules.TryCapture(state, EligibleWin(), "cap-1", Day1);
            var profile = new PlayerProfile();
            BoundCaptiveFodderRules.TryConsumeForSacrificeCredit(state, "cap-1", profile);
            int after = profile.collectionWallet.genericSacrificeCredits;

            BoundCaptiveConsumeResult second =
                BoundCaptiveFodderRules.TryConsumeForSacrificeCredit(state, "cap-1", profile);

            Assert.AreEqual(BoundCaptiveConsumeStatus.AlreadyConsumed, second.Status);
            Assert.AreEqual(after, profile.collectionWallet.genericSacrificeCredits);
        }

        [Test]
        public void ConsumedItems_DropOutOfTheUnconsumedList()
        {
            var state = new BoundCaptivePrisonState();
            BoundCaptiveFodderRules.TryCapture(state, EligibleWin(), "cap-1", Day1);
            Assert.AreEqual(1, BoundCaptiveFodderRules.UnconsumedItems(state).Count);

            BoundCaptiveFodderRules.TryConsumeForSacrificeCredit(state, "cap-1", new PlayerProfile());

            CollectionAssert.IsEmpty(BoundCaptiveFodderRules.UnconsumedItems(state));
        }

        // ---------- Academy ----------

        private static List<AcademyResearchOption> ThreeOptions() => new List<AcademyResearchOption>
        {
            new AcademyResearchOption { OptionId = "a", Branch = AcademyResearchBranch.CapacitySupport },
            new AcademyResearchOption { OptionId = "b", Branch = AcademyResearchBranch.CodexAccess },
            new AcademyResearchOption { OptionId = "c", Branch = AcademyResearchBranch.ExpeditionUtility },
        };

        [Test]
        public void ThereIsNoCombatBranch_SoResearchCannotBoostAttackOrHealth()
        {
            string[] branches = Enum.GetNames(typeof(AcademyResearchBranch));
            foreach (string forbidden in new[] { "Combat", "Attack", "Health", "Power", "Damage" })
                CollectionAssert.DoesNotContain(branches, forbidden,
                    "Locked: Academy never directly increases card Attack/HP.");
            Assert.AreEqual(4, branches.Length, "The four locked branches, no more.");
        }

        [Test]
        public void AFreshAcademy_IsIdleWithNothingOffered()
        {
            var state = new AcademyResearchState();
            Assert.AreEqual(AcademyResearchPhase.Idle, state.Phase);
            CollectionAssert.IsEmpty(state.AvailableChoices);
        }

        [Test]
        public void OfferingChoices_MovesToAwaitingChoice()
        {
            var state = new AcademyResearchState();

            AcademyResearch.OfferChoices(state, ThreeOptions());

            Assert.AreEqual(AcademyResearchPhase.AwaitingChoice, state.Phase);
            Assert.AreEqual(3, state.AvailableChoices.Count);
        }

        [Test]
        public void CommittingOneOption_DiscardsTheOthers_BecauseItIsARealChoice()
        {
            var state = new AcademyResearchState();
            AcademyResearch.OfferChoices(state, ThreeOptions());

            AcademyChoiceResult r = AcademyResearch.Commit(state, "b");

            Assert.AreEqual(AcademyChoiceStatus.Committed, r.Status);
            Assert.AreEqual(AcademyResearchBranch.CodexAccess, state.CommittedBranch);
            CollectionAssert.IsEmpty(state.AvailableChoices,
                "Taking one branch must mean not taking the others - otherwise it is a queue, not a choice.");
        }

        [Test]
        public void AnUnofferedOption_CannotBeCommitted()
        {
            var state = new AcademyResearchState();
            AcademyResearch.OfferChoices(state, ThreeOptions());

            AcademyChoiceResult r = AcademyResearch.Commit(state, "not-offered");

            Assert.AreEqual(AcademyChoiceStatus.UnknownOption, r.Status);
            Assert.AreEqual(AcademyResearchPhase.AwaitingChoice, state.Phase);
        }

        [Test]
        public void OnlyOneResearch_MayRunAtATime()
        {
            var state = new AcademyResearchState();
            AcademyResearch.OfferChoices(state, ThreeOptions());
            AcademyResearch.Commit(state, "a");

            AcademyChoiceResult second = AcademyResearch.OfferChoices(state, ThreeOptions());

            Assert.AreEqual(AcademyChoiceStatus.AlreadyInProgress, second.Status);
        }

        [Test]
        public void CollectingFinishedResearch_RecordsTheBranchAndReturnsToIdle()
        {
            var state = new AcademyResearchState();
            AcademyResearch.OfferChoices(state, ThreeOptions());
            AcademyResearch.Commit(state, "c");
            AcademyResearch.MarkReadyToCollect(state);

            AcademyChoiceResult r = AcademyResearch.Collect(state);

            Assert.AreEqual(AcademyChoiceStatus.Collected, r.Status);
            Assert.IsTrue(AcademyResearch.HasCompleted(state, AcademyResearchBranch.ExpeditionUtility));
            Assert.AreEqual(AcademyResearchPhase.Idle, state.Phase);
        }

        [Test]
        public void CollectingTwice_NeverDoubleCreditsABranch()
        {
            var state = new AcademyResearchState();
            AcademyResearch.OfferChoices(state, ThreeOptions());
            AcademyResearch.Commit(state, "a");
            AcademyResearch.MarkReadyToCollect(state);
            AcademyResearch.Collect(state);

            AcademyChoiceResult second = AcademyResearch.Collect(state);

            Assert.AreEqual(AcademyChoiceStatus.NotReadyToCollect, second.Status);
            Assert.AreEqual(1, AcademyResearch.CompletedCount(state, AcademyResearchBranch.CapacitySupport));
        }

        [Test]
        public void UnfinishedResearch_CannotBeCollected()
        {
            var state = new AcademyResearchState();
            AcademyResearch.OfferChoices(state, ThreeOptions());
            AcademyResearch.Commit(state, "a");

            AcademyChoiceResult r = AcademyResearch.Collect(state);

            Assert.AreEqual(AcademyChoiceStatus.NotReadyToCollect, r.Status);
            Assert.AreEqual(AcademyResearchPhase.InProgress, state.Phase);
        }

        [Test]
        public void PastChoices_Persist_SoTheBranchingIsMeaningful()
        {
            var state = new AcademyResearchState();
            foreach (string id in new[] { "a", "b" })
            {
                AcademyResearch.OfferChoices(state, ThreeOptions());
                AcademyResearch.Commit(state, id);
                AcademyResearch.MarkReadyToCollect(state);
                AcademyResearch.Collect(state);
            }

            Assert.IsTrue(AcademyResearch.HasCompleted(state, AcademyResearchBranch.CapacitySupport));
            Assert.IsTrue(AcademyResearch.HasCompleted(state, AcademyResearchBranch.CodexAccess));
            Assert.IsFalse(AcademyResearch.HasCompleted(state, AcademyResearchBranch.ConstructionPlanning));
        }
    }
}
