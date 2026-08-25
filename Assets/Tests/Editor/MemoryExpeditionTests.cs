using System;
using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Memory Expedition core logic (locked brief 2026-08-25). Pure logic, no GameObjects and no
    /// save-system touching - the state object is self-contained precisely because
    /// PlayerProfile.cs is frozen.
    ///
    /// Reward assertions are deliberately RELATIONAL (monotonic across bands) rather than
    /// hardcoded magnitudes, per the project non-negotiable: a test pinning a balance constant
    /// once broke on a legitimate tuning change and read as a regression. The structural rules -
    /// determinism, resume, double-claim rejection, cap behaviour - are asserted exactly, because
    /// those are contracts rather than tunables.
    /// </summary>
    public class MemoryExpeditionTests
    {
        private static readonly DateTime Day1 = new DateTime(2026, 8, 25, 9, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime Day1Later = new DateTime(2026, 8, 25, 23, 30, 0, DateTimeKind.Utc);
        private static readonly DateTime Day2 = new DateTime(2026, 8, 26, 0, 30, 0, DateTimeKind.Utc);

        private static MemoryExpeditionState Fresh(string account = "acct-1", DateTime? when = null) =>
            MemoryExpedition.StartOrResume(null, account, when ?? Day1);

        // ---------- Determinism ----------

        [Test]
        public void Layout_IsIdenticalForTheSameAccountAndDay_AcrossSeparateGenerations()
        {
            MemoryExpeditionState a = Fresh();
            MemoryExpeditionState b = Fresh();

            Assert.AreEqual(a.Seed, b.Seed, "Same account + same UTC day must produce the same seed.");
            CollectionAssert.AreEqual(MemoryExpedition.LayoutFor(a.Seed, 1), MemoryExpedition.LayoutFor(b.Seed, 1),
                "Regenerating the layout must reproduce the same arrangement - that is what makes resume safe.");
        }

        [Test]
        public void Seed_DiffersByAccount_AndByDay()
        {
            int accountA = MemoryExpedition.SeedFor("acct-A", "2026-08-25", MemoryExpedition.RulesVersion);
            int accountB = MemoryExpedition.SeedFor("acct-B", "2026-08-25", MemoryExpedition.RulesVersion);
            int nextDay = MemoryExpedition.SeedFor("acct-A", "2026-08-26", MemoryExpedition.RulesVersion);

            Assert.AreNotEqual(accountA, accountB, "Two accounts must not share a daily layout.");
            Assert.AreNotEqual(accountA, nextDay, "A new UTC day must produce a new layout.");
        }

        [Test]
        public void EachRound_UsesADistinctArrangement_NotAReshuffleOfRoundOne()
        {
            MemoryExpeditionState state = Fresh();
            int[] roundOne = MemoryExpedition.LayoutFor(state.Seed, 1);
            int[] roundTwo = MemoryExpedition.LayoutFor(state.Seed, 2);

            Assert.AreNotEqual(roundOne.Length, roundTwo.Length,
                "Round 2 is a bigger grid, so its layout cannot simply be round 1 reordered.");
        }

        [Test]
        public void EveryRoundLayout_ContainsExactlyTwoOfEachFace()
        {
            MemoryExpeditionState state = Fresh();
            for (int round = 1; round <= MemoryExpedition.Rounds.Length; round++)
            {
                MemoryExpeditionRoundRules rules = MemoryExpedition.RulesForRound(round);
                int[] layout = MemoryExpedition.LayoutFor(state.Seed, round);

                Assert.AreEqual(rules.Pairs * 2, layout.Length, "Round " + round + " tile count must equal pairs*2.");
                foreach (IGrouping<int, int> face in layout.GroupBy(v => v))
                {
                    Assert.AreEqual(2, face.Count(), "Round " + round + " face " + face.Key + " must appear exactly twice.");
                }
            }
        }

        // ---------- Resume safety ----------

        [Test]
        public void Resuming_LaterTheSameDay_KeepsRoundArrangementAndMistakes()
        {
            MemoryExpeditionState state = Fresh();
            TapAMismatch(state);
            int mistakesAfterOne = state.MistakesRemaining;
            int seedBefore = state.Seed;

            MemoryExpeditionState resumed = MemoryExpedition.StartOrResume(state, "acct-1", Day1Later);

            Assert.AreEqual(seedBefore, resumed.Seed, "Closing the game must not reshuffle the arrangement.");
            Assert.AreEqual(mistakesAfterOne, resumed.MistakesRemaining, "Closing the game must not restore mistakes.");
            Assert.AreEqual(1, resumed.CurrentRound, "Resume must return to the same round.");
        }

        [Test]
        public void ANewUtcDay_StartsAFreshRun()
        {
            MemoryExpeditionState state = Fresh();
            TapAMismatch(state);

            MemoryExpeditionState nextDay = MemoryExpedition.StartOrResume(state, "acct-1", Day2);

            Assert.AreNotEqual(state.Seed, nextDay.Seed, "A new UTC day must generate a new layout.");
            Assert.AreEqual(MemoryExpedition.Rounds[0].Mistakes, nextDay.MistakesRemaining, "A new day restores mistakes.");
            Assert.IsFalse(nextDay.RewardClaimed, "A new day must be claimable again.");
        }

        // ---------- Tap rules ----------

        [Test]
        public void FirstTap_OnlySelects_AndDoesNotSpendAMistake()
        {
            MemoryExpeditionState state = Fresh();
            int before = state.MistakesRemaining;

            MemoryExpeditionTapResult result = MemoryExpedition.Tap(state, 0);

            Assert.AreEqual(MemoryExpeditionTapStatus.FirstTileSelected, result.Status);
            Assert.AreEqual(before, state.MistakesRemaining, "Selecting a first tile is never a mistake.");
        }

        [Test]
        public void AMismatch_SpendsExactlyOneMistake()
        {
            MemoryExpeditionState state = Fresh();
            int before = state.MistakesRemaining;

            TapAMismatch(state);

            Assert.AreEqual(before - 1, state.MistakesRemaining);
        }

        [Test]
        public void AMatch_RevealsThePair_AndSpendsNoMistake()
        {
            MemoryExpeditionState state = Fresh();
            int before = state.MistakesRemaining;
            (int a, int b) = FindMatchingPair(state);

            MemoryExpedition.Tap(state, a);
            MemoryExpeditionTapResult result = MemoryExpedition.Tap(state, b);

            Assert.AreEqual(MemoryExpeditionTapStatus.Matched, result.Status);
            Assert.AreEqual(before, state.MistakesRemaining, "A correct match must not cost a mistake.");
            Assert.IsTrue(MemoryExpedition.IsTileResolved(state, a) && MemoryExpedition.IsTileResolved(state, b));
        }

        [Test]
        public void AnAlreadyMatchedTile_IsRejected_AndNeverUnreveals()
        {
            MemoryExpeditionState state = Fresh();
            (int a, int b) = FindMatchingPair(state);
            MemoryExpedition.Tap(state, a);
            MemoryExpedition.Tap(state, b);

            MemoryExpeditionTapResult result = MemoryExpedition.Tap(state, a);

            Assert.AreEqual(MemoryExpeditionTapStatus.TileAlreadyResolved, result.Status);
            Assert.IsTrue(MemoryExpedition.IsTileResolved(state, a), "A resolved pair must stay face-up.");
        }

        [Test]
        public void TappingTheSameTileTwice_IsRejectedWithoutCostingAMistake()
        {
            MemoryExpeditionState state = Fresh();
            int before = state.MistakesRemaining;
            MemoryExpedition.Tap(state, 0);

            MemoryExpeditionTapResult result = MemoryExpedition.Tap(state, 0);

            Assert.AreEqual(MemoryExpeditionTapStatus.SameTileTwice, result.Status);
            Assert.AreEqual(before, state.MistakesRemaining);
        }

        [Test]
        public void AnOutOfRangeTile_IsRejected()
        {
            MemoryExpeditionState state = Fresh();
            MemoryExpeditionRoundRules rules = MemoryExpedition.RulesForRound(1);

            Assert.AreEqual(MemoryExpeditionTapStatus.InvalidTile, MemoryExpedition.Tap(state, rules.TileCount).Status);
            Assert.AreEqual(MemoryExpeditionTapStatus.InvalidTile, MemoryExpedition.Tap(state, -1).Status);
        }

        // ---------- Round progression / failure ----------

        [Test]
        public void ClearingARound_AdvancesAndRestoresThatRoundsMistakeAllowance()
        {
            MemoryExpeditionState state = Fresh();
            ClearCurrentRound(state);

            Assert.AreEqual(2, state.CurrentRound, "Clearing round 1 advances to round 2.");
            Assert.AreEqual(1, state.HighestRoundCleared);
            Assert.AreEqual(MemoryExpedition.RulesForRound(2).Mistakes, state.MistakesRemaining,
                "Each round starts on its own mistake allowance.");
        }

        [Test]
        public void ClearingAllThreeRounds_EndsTheRunWithHighestRoundThree()
        {
            MemoryExpeditionState state = Fresh();
            ClearCurrentRound(state);
            ClearCurrentRound(state);
            MemoryExpeditionTapResult last = ClearCurrentRound(state);

            Assert.AreEqual(MemoryExpeditionTapStatus.RoundCleared, last.Status);
            Assert.IsTrue(last.RunOver, "There is no round 4 - the run ends.");
            Assert.AreEqual(3, state.HighestRoundCleared);
        }

        [Test]
        public void RunningOutOfMistakes_EndsTheRun_ButKeepsAlreadyClearedRoundsCredited()
        {
            MemoryExpeditionState state = Fresh();
            ClearCurrentRound(state); // round 1 credited, now on round 2

            MemoryExpeditionTapResult result = null;
            while (!state.RunFailed)
            {
                result = TapAMismatch(state);
            }

            Assert.AreEqual(MemoryExpeditionTapStatus.RunFailed, result.Status);
            Assert.IsTrue(result.RunOver);
            Assert.AreEqual(1, state.HighestRoundCleared, "A failed round must not revoke an already-cleared round.");
        }

        [Test]
        public void AfterTheRunEnds_FurtherTapsAreRejected()
        {
            MemoryExpeditionState state = Fresh();
            while (!state.RunFailed) TapAMismatch(state);

            Assert.AreEqual(MemoryExpeditionTapStatus.RunAlreadyOver, MemoryExpedition.Tap(state, 0).Status);
        }

        // ---------- Rewards ----------

        [Test]
        public void RewardBands_IncreaseMonotonicallyWithRoundsCleared()
        {
            for (int band = 1; band < MemoryExpedition.RewardBands.Length; band++)
            {
                MemoryExpeditionRewardBand lower = MemoryExpedition.BandFor(band - 1);
                MemoryExpeditionRewardBand higher = MemoryExpedition.BandFor(band);

                Assert.Greater(higher.Xp, lower.Xp, "XP must strictly improve with rounds cleared.");
                Assert.Greater(higher.Gold, lower.Gold, "Gold must strictly improve with rounds cleared.");
                Assert.GreaterOrEqual(higher.Stamina, lower.Stamina);
                Assert.GreaterOrEqual(higher.EventMedals, lower.EventMedals);
                Assert.GreaterOrEqual(higher.ResearchPoints, lower.ResearchPoints);
            }
        }

        [Test]
        public void ClaimingWithNoRoundsCleared_StillGrantsTheParticipationBand()
        {
            MemoryExpeditionState state = Fresh();

            MemoryExpeditionClaimResult result = MemoryExpedition.Claim(state, Day1, 0, 100, true);

            Assert.AreEqual(MemoryExpeditionClaimStatus.Granted, result.Status);
            Assert.Greater(result.Gold, 0, "Even a failed run pays the participation band.");
            Assert.AreEqual(0, result.HighestRoundCleared);
        }

        [Test]
        public void ASecondClaimOnTheSameRun_IsRejected_AndGrantsNothing()
        {
            MemoryExpeditionState state = Fresh();
            MemoryExpedition.Claim(state, Day1, 0, 100, true);

            MemoryExpeditionClaimResult second = MemoryExpedition.Claim(state, Day1, 0, 100, true);

            Assert.AreEqual(MemoryExpeditionClaimStatus.AlreadyClaimed, second.Status);
            Assert.AreEqual(0, second.Gold);
            Assert.AreEqual(0, second.Xp);
            Assert.AreEqual(0, second.ResearchPoints);
        }

        [Test]
        public void PracticeReplayAfterClaiming_GrantsNothingEvenIfMoreRoundsAreCleared()
        {
            MemoryExpeditionState state = Fresh();
            MemoryExpedition.Claim(state, Day1, 0, 100, true);

            ClearCurrentRound(state);
            MemoryExpeditionClaimResult replayClaim = MemoryExpedition.Claim(state, Day1, 0, 100, true);

            Assert.AreEqual(MemoryExpeditionClaimStatus.AlreadyClaimed, replayClaim.Status,
                "One reward-bearing run per account per UTC day - practice must never pay again.");
        }

        [Test]
        public void Stamina_RespectsTheCap_AndTheExcessIsNotConvertedToAnythingElse()
        {
            MemoryExpeditionState state = Fresh();
            ClearCurrentRound(state);
            ClearCurrentRound(state); // highest cleared = 2, a band that grants Stamina

            MemoryExpeditionRewardBand band = MemoryExpedition.BandFor(state.HighestRoundCleared);
            Assume.That(band.Stamina, Is.GreaterThan(0), "This test needs a Stamina-granting band.");

            MemoryExpeditionClaimResult atCap = MemoryExpedition.Claim(state, Day1, 100, 100, true);

            Assert.AreEqual(0, atCap.StaminaGranted, "At cap, no Stamina is granted.");
            Assert.AreEqual(band.Stamina, atCap.StaminaLostToCap, "The excess is reported, not silently dropped.");
            Assert.AreEqual(band.Gold, atCap.Gold, "Overflow must NOT be converted into extra Gold.");
        }

        [Test]
        public void EventMedals_OnlyDropWhileAnEligibleLedgerIsActive()
        {
            MemoryExpeditionState withEvent = Fresh();
            ClearCurrentRound(withEvent);
            ClearCurrentRound(withEvent);
            MemoryExpeditionState withoutEvent = withEvent.Clone();

            MemoryExpeditionClaimResult active = MemoryExpedition.Claim(withEvent, Day1, 0, 100, true);
            MemoryExpeditionClaimResult inactive = MemoryExpedition.Claim(withoutEvent, Day1, 0, 100, false);

            Assert.Greater(active.EventMedals, 0, "Medals drop while an eligible event ledger is active.");
            Assert.AreEqual(0, inactive.EventMedals, "No event ledger means no Medals.");
            Assert.AreEqual(active.Gold, inactive.Gold, "Everything else in the band is unaffected by the event.");
        }

        [Test]
        public void ResearchPoints_ExpireAtTheNextUtcReset()
        {
            MemoryExpeditionState state = Fresh();
            ClearCurrentRound(state);
            MemoryExpedition.Claim(state, Day1, 0, 100, true);
            Assume.That(state.TemporaryResearchPoints, Is.GreaterThan(0), "This test needs points to have been granted.");

            MemoryExpeditionState nextDay = MemoryExpedition.StartOrResume(state, "acct-1", Day2);

            Assert.AreEqual(0, nextDay.TemporaryResearchPoints, "Research points must not survive the UTC reset.");
        }

        [Test]
        public void ClaimingARunFromAPreviousDay_IsRejected()
        {
            MemoryExpeditionState state = Fresh();

            MemoryExpeditionClaimResult result = MemoryExpedition.Claim(state, Day2, 0, 100, true);

            Assert.AreEqual(MemoryExpeditionClaimStatus.WrongDay, result.Status);
            Assert.AreEqual(0, result.Gold);
        }

        // ---------- PlayerProfile persistence (12 additive fields, owner-approved 2026-08-25) ----------

        [Test]
        public void AFreshProfile_HasNoStoredRun_AndDoesNotLookLikeTileZeroIsSelected()
        {
            var profile = new PlayerProfile();

            Assert.IsNull(profile.ToMemoryExpeditionState(), "An empty dayKey must read as 'no run today'.");
            Assert.AreEqual(-1, profile.memoryExpeditionFirstSelectedTile,
                "Default must be -1. A 0 default would make an old save look like tile 0 was already flipped.");
        }

        [Test]
        public void StateRoundTripsThroughPlayerProfile_WithoutLosingAnyField()
        {
            MemoryExpeditionState state = Fresh();
            ClearCurrentRound(state);            // highest cleared = 1, now on round 2
            MemoryExpedition.Tap(state, 0);      // leave a tile mid-selection
            MemoryExpedition.Claim(state, Day1, 0, 100, true);

            var profile = new PlayerProfile();
            profile.ApplyMemoryExpeditionState(state);
            MemoryExpeditionState restored = profile.ToMemoryExpeditionState();

            Assert.AreEqual(state.DayKey, restored.DayKey);
            Assert.AreEqual(state.Seed, restored.Seed, "A lost seed would reshuffle the grid on reload.");
            Assert.AreEqual(state.RulesVersion, restored.RulesVersion);
            Assert.AreEqual(state.CurrentRound, restored.CurrentRound);
            Assert.AreEqual(state.RevealedPairMask, restored.RevealedPairMask, "Revealed pairs must survive a reload.");
            Assert.AreEqual(state.FirstSelectedTile, restored.FirstSelectedTile);
            Assert.AreEqual(state.MistakesRemaining, restored.MistakesRemaining, "Reload must not restore mistakes.");
            Assert.AreEqual(state.HighestRoundCleared, restored.HighestRoundCleared);
            Assert.AreEqual(state.RewardClaimed, restored.RewardClaimed, "A reload must not re-enable a spent claim.");
            Assert.AreEqual(state.RunFailed, restored.RunFailed);
            Assert.AreEqual(state.TemporaryResearchPoints, restored.TemporaryResearchPoints);
            Assert.AreEqual(state.TemporaryResearchExpiryDayKey, restored.TemporaryResearchExpiryDayKey);
        }

        [Test]
        public void APersistedRun_ResumesRatherThanRestarting_WhenReloadedTheSameDay()
        {
            MemoryExpeditionState state = Fresh();
            TapAMismatch(state);
            var profile = new PlayerProfile();
            profile.ApplyMemoryExpeditionState(state);

            MemoryExpeditionState resumed =
                MemoryExpedition.StartOrResume(profile.ToMemoryExpeditionState(), "acct-1", Day1Later);

            Assert.AreEqual(state.Seed, resumed.Seed, "Same day must resume the same arrangement.");
            Assert.AreEqual(state.MistakesRemaining, resumed.MistakesRemaining, "Mistakes must not be restored by a reload.");
        }

        [Test]
        public void ClearingTheStoredRun_ResetsTheSelectionSentinel()
        {
            var profile = new PlayerProfile();
            profile.ApplyMemoryExpeditionState(Fresh());
            profile.memoryExpeditionFirstSelectedTile = 4;

            profile.ApplyMemoryExpeditionState(null);

            Assert.IsNull(profile.ToMemoryExpeditionState());
            Assert.AreEqual(-1, profile.memoryExpeditionFirstSelectedTile);
        }

        // ---------- helpers ----------

        private static (int, int) FindMatchingPair(MemoryExpeditionState state)
        {
            int[] layout = MemoryExpedition.LayoutFor(state.Seed, state.CurrentRound);
            for (int i = 0; i < layout.Length; i++)
            {
                if (MemoryExpedition.IsTileResolved(state, i)) continue;
                for (int j = i + 1; j < layout.Length; j++)
                {
                    if (MemoryExpedition.IsTileResolved(state, j)) continue;
                    if (layout[i] == layout[j]) return (i, j);
                }
            }
            throw new InvalidOperationException("No unresolved matching pair remains.");
        }

        private static MemoryExpeditionTapResult TapAMismatch(MemoryExpeditionState state)
        {
            int[] layout = MemoryExpedition.LayoutFor(state.Seed, state.CurrentRound);
            for (int i = 0; i < layout.Length; i++)
            {
                if (MemoryExpedition.IsTileResolved(state, i)) continue;
                for (int j = i + 1; j < layout.Length; j++)
                {
                    if (MemoryExpedition.IsTileResolved(state, j)) continue;
                    if (layout[i] == layout[j]) continue;
                    MemoryExpedition.Tap(state, i);
                    return MemoryExpedition.Tap(state, j);
                }
            }
            throw new InvalidOperationException("No unresolved mismatching pair remains.");
        }

        private static MemoryExpeditionTapResult ClearCurrentRound(MemoryExpeditionState state)
        {
            MemoryExpeditionRoundRules rules = MemoryExpedition.RulesForRound(state.CurrentRound);
            MemoryExpeditionTapResult last = null;
            for (int pair = 0; pair < rules.Pairs; pair++)
            {
                (int a, int b) = FindMatchingPair(state);
                MemoryExpedition.Tap(state, a);
                last = MemoryExpedition.Tap(state, b);
            }
            return last;
        }
    }
}
