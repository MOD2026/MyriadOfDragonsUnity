using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Empire
{
    /// <summary>Per-round shape. Locked 2026-08-25 (GPT brief): 3x4/6/8 -> 4x4/8/7 -> 4x5/10/6.</summary>
    public sealed class MemoryExpeditionRoundRules
    {
        public int Rows;
        public int Columns;
        public int Pairs;
        public int Mistakes;

        public int TileCount => Rows * Columns;
    }

    /// <summary>One reward band, keyed by HIGHEST ROUND CLEARED (not rounds attempted).</summary>
    public sealed class MemoryExpeditionRewardBand
    {
        public int Xp;
        public int Gold;
        public int Stamina;
        public int EventMedals;
        public int ResearchPoints;
    }

    public enum MemoryExpeditionTapStatus
    {
        FirstTileSelected,
        Matched,
        Mismatched,
        RoundCleared,
        RunFailed,
        RunAlreadyOver,
        InvalidTile,
        TileAlreadyResolved,
        SameTileTwice,
    }

    public sealed class MemoryExpeditionTapResult
    {
        public MemoryExpeditionTapStatus Status;
        public int MistakesRemaining;
        public int CurrentRound;
        public bool RunOver;
        public string Message;
    }

    public enum MemoryExpeditionClaimStatus
    {
        Granted,
        AlreadyClaimed,
        WrongDay,
    }

    public sealed class MemoryExpeditionClaimResult
    {
        public MemoryExpeditionClaimStatus Status;
        public int Xp;
        public int Gold;
        public int StaminaGranted;
        public int StaminaLostToCap;
        public int EventMedals;
        public int ResearchPoints;
        public int HighestRoundCleared;
        public string Message;
    }

    /// <summary>
    /// Self-contained persistent state for one daily run. Deliberately NOT PlayerProfile fields:
    /// PlayerProfile.cs is FROZEN, so this type carries the shape and the additive fields are
    /// listed for owner sign-off before any save-shape change happens. Plain data throughout so it
    /// round-trips through whatever persistence the owner approves.
    /// </summary>
    public sealed class MemoryExpeditionState
    {
        public string DayKey;                 // UTC yyyy-MM-dd this run belongs to
        public int Seed;                      // stored so a layout can never re-roll
        public int RulesVersion;
        public int CurrentRound;              // 1-based; 0 = not started
        public long RevealedPairMask;         // bit per tile index, set once permanently face-up
        public int FirstSelectedTile = -1;    // -1 = no tile currently flipped
        public int MistakesRemaining;
        public int HighestRoundCleared;       // 0..3
        public bool RewardClaimed;
        public bool RunFailed;

        public int TemporaryResearchPoints;
        public string TemporaryResearchExpiryDayKey;

        public MemoryExpeditionState Clone() => (MemoryExpeditionState)MemberwiseClone();
    }

    /// <summary>
    /// Memory Expedition core game logic - plain, testable, no MonoBehaviour (EditMode cannot run
    /// Update/coroutines, so nothing real may live there). UI wiring is WH's
    /// MemoryExpeditionPresenter, not this file.
    ///
    /// Locked brief 2026-08-25 (GPT, LOCKED_DECISIONS_REGISTER): deterministic tap-two-to-match,
    /// up to 3 rounds/day, one reward-bearing run per account per UTC day, layout from
    /// accountId + UTC day + rulesVersion, leave/return resumes the SAME round and arrangement,
    /// closing cannot reshuffle or restore mistakes, no timer. A failed round ends the run;
    /// already-cleared rounds stay credited. Practice replay after claiming grants nothing.
    /// </summary>
    public static class MemoryExpedition
    {
        public const int RulesVersion = 1;

        public static readonly MemoryExpeditionRoundRules[] Rounds =
        {
            new MemoryExpeditionRoundRules { Rows = 3, Columns = 4, Pairs = 6,  Mistakes = 8 },
            new MemoryExpeditionRoundRules { Rows = 4, Columns = 4, Pairs = 8,  Mistakes = 7 },
            new MemoryExpeditionRoundRules { Rows = 4, Columns = 5, Pairs = 10, Mistakes = 6 },
        };

        /// <summary>Bands indexed by highest round cleared (0..3). Locked amounts.</summary>
        public static readonly MemoryExpeditionRewardBand[] RewardBands =
        {
            new MemoryExpeditionRewardBand { Xp = 1, Gold =  50, Stamina = 0, EventMedals = 0, ResearchPoints = 0 },
            new MemoryExpeditionRewardBand { Xp = 2, Gold = 100, Stamina = 0, EventMedals = 0, ResearchPoints = 1 },
            new MemoryExpeditionRewardBand { Xp = 3, Gold = 200, Stamina = 1, EventMedals = 1, ResearchPoints = 2 },
            new MemoryExpeditionRewardBand { Xp = 5, Gold = 350, Stamina = 1, EventMedals = 2, ResearchPoints = 3 },
        };

        public static string DayKeyFor(DateTime utcNow) => utcNow.ToUniversalTime().ToString("yyyy-MM-dd");

        /// <summary>
        /// FNV-1a, deliberately NOT string.GetHashCode(): .NET randomises string hashing per
        /// process, so a GetHashCode-derived seed would reshuffle the grid on every app launch -
        /// exactly the "closing the game cannot reshuffle" rule the brief forbids. This is stable
        /// across processes, platforms and runtime versions.
        /// </summary>
        public static int SeedFor(string accountId, string dayKey, int rulesVersion)
        {
            unchecked
            {
                const uint offset = 2166136261;
                const uint prime = 16777619;
                uint hash = offset;
                string material = (accountId ?? string.Empty) + "|" + (dayKey ?? string.Empty) + "|" + rulesVersion;
                foreach (char c in material)
                {
                    hash ^= c;
                    hash *= prime;
                }
                return (int)(hash & 0x7FFFFFFF);
            }
        }

        /// <summary>
        /// Face values per tile index for one round. Deterministic for a given (seed, round): the
        /// same inputs always produce the same arrangement, which is what makes resume safe without
        /// persisting the whole grid.
        /// </summary>
        public static int[] LayoutFor(int seed, int roundNumber)
        {
            MemoryExpeditionRoundRules rules = RulesForRound(roundNumber);
            if (rules == null) return new int[0];

            var tiles = new List<int>(rules.Pairs * 2);
            for (int pair = 0; pair < rules.Pairs; pair++)
            {
                tiles.Add(pair);
                tiles.Add(pair);
            }

            // Round number folded into the stream so round 2 is not a permutation of round 1.
            var rng = new Random(unchecked(seed + roundNumber * 7919));
            for (int i = tiles.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                int swap = tiles[i];
                tiles[i] = tiles[j];
                tiles[j] = swap;
            }
            return tiles.ToArray();
        }

        public static MemoryExpeditionRoundRules RulesForRound(int roundNumber) =>
            roundNumber >= 1 && roundNumber <= Rounds.Length ? Rounds[roundNumber - 1] : null;

        /// <summary>
        /// Returns existing state untouched when it already belongs to this UTC day - that is what
        /// makes leave/return resume the same round, same arrangement and same mistake count. Only
        /// a genuinely new day (or a rules-version change) starts a fresh run.
        /// </summary>
        public static MemoryExpeditionState StartOrResume(MemoryExpeditionState existing, string accountId, DateTime utcNow)
        {
            string dayKey = DayKeyFor(utcNow);
            if (existing != null && existing.DayKey == dayKey && existing.RulesVersion == RulesVersion)
            {
                return ExpireStaleResearchPoints(existing, dayKey);
            }

            var fresh = new MemoryExpeditionState
            {
                DayKey = dayKey,
                RulesVersion = RulesVersion,
                Seed = SeedFor(accountId, dayKey, RulesVersion),
                CurrentRound = 1,
                RevealedPairMask = 0L,
                FirstSelectedTile = -1,
                MistakesRemaining = Rounds[0].Mistakes,
                HighestRoundCleared = 0,
                RewardClaimed = false,
                RunFailed = false,
                TemporaryResearchPoints = existing == null ? 0 : existing.TemporaryResearchPoints,
                TemporaryResearchExpiryDayKey = existing == null ? null : existing.TemporaryResearchExpiryDayKey,
            };
            return ExpireStaleResearchPoints(fresh, dayKey);
        }

        /// <summary>Research points expire at the next UTC reset - never silently carried forward.</summary>
        private static MemoryExpeditionState ExpireStaleResearchPoints(MemoryExpeditionState state, string todayKey)
        {
            if (state.TemporaryResearchExpiryDayKey != null &&
                string.CompareOrdinal(todayKey, state.TemporaryResearchExpiryDayKey) >= 0)
            {
                state.TemporaryResearchPoints = 0;
                state.TemporaryResearchExpiryDayKey = null;
            }
            return state;
        }

        public static bool IsTileResolved(MemoryExpeditionState state, int tileIndex) =>
            (state.RevealedPairMask & (1L << tileIndex)) != 0L;

        /// <summary>
        /// One tap. The first tap of a pair only records the selection; the second resolves it.
        /// A mismatch costs a mistake and never un-reveals an already-matched pair.
        /// </summary>
        public static MemoryExpeditionTapResult Tap(MemoryExpeditionState state, int tileIndex)
        {
            var result = new MemoryExpeditionTapResult
            {
                CurrentRound = state.CurrentRound,
                MistakesRemaining = state.MistakesRemaining,
            };

            if (state.RunFailed || state.CurrentRound < 1 || state.CurrentRound > Rounds.Length)
            {
                result.Status = MemoryExpeditionTapStatus.RunAlreadyOver;
                result.RunOver = true;
                result.Message = "This run is over; start tomorrow's run or replay for practice.";
                return result;
            }

            MemoryExpeditionRoundRules rules = RulesForRound(state.CurrentRound);
            if (tileIndex < 0 || tileIndex >= rules.TileCount)
            {
                result.Status = MemoryExpeditionTapStatus.InvalidTile;
                result.Message = "Tile " + tileIndex + " is outside this round's " + rules.TileCount + "-tile grid.";
                return result;
            }
            if (IsTileResolved(state, tileIndex))
            {
                result.Status = MemoryExpeditionTapStatus.TileAlreadyResolved;
                result.Message = "That tile is already face-up.";
                return result;
            }
            if (state.FirstSelectedTile == tileIndex)
            {
                result.Status = MemoryExpeditionTapStatus.SameTileTwice;
                result.Message = "Pick a different second tile.";
                return result;
            }

            if (state.FirstSelectedTile < 0)
            {
                state.FirstSelectedTile = tileIndex;
                result.Status = MemoryExpeditionTapStatus.FirstTileSelected;
                return result;
            }

            int[] layout = LayoutFor(state.Seed, state.CurrentRound);
            int firstTile = state.FirstSelectedTile;
            bool matched = layout[firstTile] == layout[tileIndex];
            state.FirstSelectedTile = -1;

            if (!matched)
            {
                state.MistakesRemaining--;
                result.MistakesRemaining = state.MistakesRemaining;
                if (state.MistakesRemaining <= 0)
                {
                    state.RunFailed = true;
                    result.Status = MemoryExpeditionTapStatus.RunFailed;
                    result.RunOver = true;
                    result.Message = "Out of mistakes - the run ends here. Rounds already cleared stay credited.";
                    return result;
                }
                result.Status = MemoryExpeditionTapStatus.Mismatched;
                return result;
            }

            state.RevealedPairMask |= (1L << firstTile) | (1L << tileIndex);
            if (CountRevealed(state.RevealedPairMask) < rules.Pairs * 2)
            {
                result.Status = MemoryExpeditionTapStatus.Matched;
                return result;
            }

            state.HighestRoundCleared = Math.Max(state.HighestRoundCleared, state.CurrentRound);
            result.Status = MemoryExpeditionTapStatus.RoundCleared;

            if (state.CurrentRound >= Rounds.Length)
            {
                result.RunOver = true;
                result.CurrentRound = state.CurrentRound;
                result.Message = "All three rounds cleared.";
                return result;
            }

            state.CurrentRound++;
            state.RevealedPairMask = 0L;
            state.MistakesRemaining = RulesForRound(state.CurrentRound).Mistakes;
            result.CurrentRound = state.CurrentRound;
            result.MistakesRemaining = state.MistakesRemaining;
            return result;
        }

        private static int CountRevealed(long mask)
        {
            int count = 0;
            while (mask != 0L)
            {
                mask &= mask - 1;
                count++;
            }
            return count;
        }

        public static MemoryExpeditionRewardBand BandFor(int highestRoundCleared)
        {
            int index = Math.Max(0, Math.Min(RewardBands.Length - 1, highestRoundCleared));
            return RewardBands[index];
        }

        /// <summary>
        /// Single atomic claim on the highest round cleared. Rejects a second claim outright - the
        /// reward-bearing run is once per account per UTC day, so a practice replay after claiming
        /// grants nothing. Stamina respects the cap with NO overflow conversion (the excess is
        /// reported, not paid out in another currency). Event Medals only drop while an eligible
        /// event ledger is active. Research points expire at the next UTC reset.
        /// </summary>
        public static MemoryExpeditionClaimResult Claim(
            MemoryExpeditionState state,
            DateTime utcNow,
            int currentStamina,
            int staminaCap,
            bool eventLedgerActive)
        {
            string dayKey = DayKeyFor(utcNow);
            var result = new MemoryExpeditionClaimResult { HighestRoundCleared = state.HighestRoundCleared };

            if (state.DayKey != dayKey)
            {
                result.Status = MemoryExpeditionClaimStatus.WrongDay;
                result.Message = "That run belongs to a previous UTC day and can no longer be claimed.";
                return result;
            }
            if (state.RewardClaimed)
            {
                result.Status = MemoryExpeditionClaimStatus.AlreadyClaimed;
                result.Message = "Today's Memory Expedition reward has already been claimed.";
                return result;
            }

            MemoryExpeditionRewardBand band = BandFor(state.HighestRoundCleared);
            int staminaRoom = Math.Max(0, staminaCap - currentStamina);
            int staminaGranted = Math.Min(band.Stamina, staminaRoom);

            state.RewardClaimed = true;
            state.TemporaryResearchPoints += band.ResearchPoints;
            state.TemporaryResearchExpiryDayKey = DayKeyFor(utcNow.ToUniversalTime().Date.AddDays(1));

            result.Status = MemoryExpeditionClaimStatus.Granted;
            result.Xp = band.Xp;
            result.Gold = band.Gold;
            result.StaminaGranted = staminaGranted;
            result.StaminaLostToCap = band.Stamina - staminaGranted;
            result.EventMedals = eventLedgerActive ? band.EventMedals : 0;
            result.ResearchPoints = band.ResearchPoints;
            return result;
        }
    }
}
