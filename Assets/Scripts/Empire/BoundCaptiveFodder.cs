using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Empire
{
    /// <summary>
    /// One captured Fodder item. Deliberately carries NO combat stats - not Attack, not Health, not
    /// a rarity-derived power value. It records where it came from and nothing that could make it
    /// playable, because the locked design rejected full-card capture: a usable card would open a
    /// new PvP acquisition route and collide with the locked collection-acquisition-channel model.
    ///
    /// SourceRarity exists only to size the sacrifice-credit yield, mirroring how burning a real
    /// card already works. It is not a stat and must never be read as one.
    /// </summary>
    [Serializable]
    public sealed class BoundCaptiveFodderItem
    {
        /// <summary>Stable id for idempotency - a replayed capture grant must not duplicate.</summary>
        public string CaptureId;

        /// <summary>Card the snapshot was taken from. Informational//display only; the opponent
        /// KEEPS their original card, so this is not an ownership record.</summary>
        public string SourceCardId;

        /// <summary>Rarity of the source card, used ONLY to size the sacrifice yield.</summary>
        public int SourceRarity;

        /// <summary>UTC day of capture, for the one-per-day guardrail.</summary>
        public string CapturedUtcDayKey;

        /// <summary>Opponent captured from, for the same-opponent cooldown.</summary>
        public string OpponentAccountId;

        public bool Consumed;
    }

    public enum BoundCaptiveCaptureStatus
    {
        Granted,
        NotAnEligibleMatchType,
        OpponentHasNoEligibleCard,
        DailyCaptureCapReached,
        SameOpponentOnCooldown,
        ExcludedRelationship,
        AttackDidNotWin,
        AlreadyGranted,
    }

    public sealed class BoundCaptiveCaptureResult
    {
        public BoundCaptiveCaptureStatus Status;
        public BoundCaptiveFodderItem Item;
        public string Message;

        public bool Granted => Status == BoundCaptiveCaptureStatus.Granted;
    }

    public enum BoundCaptiveConsumeStatus
    {
        Consumed,
        UnknownItem,
        AlreadyConsumed,
    }

    public sealed class BoundCaptiveConsumeResult
    {
        public BoundCaptiveConsumeStatus Status;
        public int SacrificeCreditsGranted;
        public string Message;
    }

    /// <summary>The match context a capture is evaluated against. Plain data so the rules below
    /// stay testable without a live match.</summary>
    public sealed class BoundCaptiveMatchContext
    {
        public bool AttackerWon;
        public bool IsRankedAsyncLadder;
        public bool IsTutorialOrBotOrPractice;
        public bool IsPrivateOrRematch;
        public bool OpponentIsGuildmateOrFriend;
        public string OpponentAccountId;

        /// <summary>The opponent's revealed defense-formation snapshot. Empty = nothing to pick.</summary>
        public IReadOnlyList<string> OpponentDefenseSnapshotCardIds = new List<string>();
    }

    /// <summary>Persistent Prison state. Self-contained: PlayerProfile.cs is FROZEN, so this carries
    /// the shape and the additive fields go to the owner for sign-off before any save change.</summary>
    [Serializable]
    public sealed class BoundCaptivePrisonState
    {
        public List<BoundCaptiveFodderItem> Items = new List<BoundCaptiveFodderItem>();

        /// <summary>UTC day of the most recent capture, for the 1/day cap.</summary>
        public string LastCaptureUtcDayKey;

        /// <summary>Ledger of granted capture ids - what makes the grant idempotent.</summary>
        public List<string> GrantedCaptureIds = new List<string>();
    }

    /// <summary>
    /// Bound Captive Fodder: the locked Phase-1 Prison reward (register: "Academy + Prison real
    /// design LOCKED", 2026-08-25). Server-authoritative, non-destructive, sacrifice-only.
    ///
    /// Full-card capture was REJECTED for real abuse reasons (new PvP acquisition route,
    /// alt-account farming, whale exploitation, collision with the locked acquisition-channel
    /// model). What survives is deliberately inert: the opponent keeps their card, and the attacker
    /// receives an item whose ONLY use is as an Evolution sacrifice-credit input - reusing the
    /// EXISTING credit system (CollectionBurnService/CollectionEvolutionService), not a new
    /// mechanic.
    ///
    /// Plain static logic, no MonoBehaviour, so every guardrail is assertable in EditMode.
    /// </summary>
    public static class BoundCaptiveFodderRules
    {
        /// <summary>Max captures per player per UTC day. Locked guardrail.</summary>
        public const int MaxCapturesPerUtcDay = 1;

        /// <summary>
        /// Days before the same opponent can be captured from again.
        ///
        /// NUMBER IS OPEN - the locked design states the RULE ("cooldown vs same opponent") but
        /// never a duration, so this is a placeholder, not a locked value. Flagged 2026-08-25:
        /// at 1 day this guardrail is entirely SUBSUMED by MaxCapturesPerUtcDay=1 (a same-day
        /// repeat is already blocked by the cap, and exactly 1.0 days later the cooldown has
        /// expired), so it currently does nothing. It only becomes a real anti-farming rule at
        /// >= 2. Needs an owner/GPT number rather than an invented one - see the standing
        /// "structure locked, numbers open" pattern.
        /// </summary>
        public const int SameOpponentCooldownDays = 1;

        /// <summary>True while the cooldown cannot bite because the daily cap already covers it.
        /// Exists so the gap is assertable rather than only described in a comment.</summary>
        public static bool SameOpponentCooldownIsRedundant =>
            SameOpponentCooldownDays <= MaxCapturesPerUtcDay;

        /// <summary>
        /// Sacrifice yield for a Fodder item. Intentionally delegates to the SAME rarity table a
        /// real burned card uses, so Fodder can never become a better or worse credit source than
        /// the existing path - that would make Prison a balance lever rather than a convenience.
        /// </summary>
        public static int SacrificeYieldFor(int sourceRarity) =>
            MyriadOfDragons.Save.CollectionBurnRules.GetGenericSacrificeYield(sourceRarity);

        public static string DayKeyFor(DateTime utcNow) => utcNow.ToUniversalTime().ToString("yyyy-MM-dd");

        /// <summary>
        /// Evaluates every locked guardrail in order and grants at most one Fodder item.
        /// Idempotent: replaying the same captureId returns AlreadyGranted and grants nothing.
        /// </summary>
        public static BoundCaptiveCaptureResult TryCapture(
            BoundCaptivePrisonState state,
            BoundCaptiveMatchContext context,
            string captureId,
            DateTime utcNow,
            Func<IReadOnlyList<string>, string> serverPickCard = null)
        {
            var result = new BoundCaptiveCaptureResult();

            if (state == null || context == null || string.IsNullOrEmpty(captureId))
            {
                result.Status = BoundCaptiveCaptureStatus.NotAnEligibleMatchType;
                result.Message = "Missing capture context.";
                return result;
            }

            if (state.GrantedCaptureIds.Contains(captureId))
            {
                result.Status = BoundCaptiveCaptureStatus.AlreadyGranted;
                result.Message = "This capture was already granted.";
                return result;
            }

            if (!context.AttackerWon)
            {
                result.Status = BoundCaptiveCaptureStatus.AttackDidNotWin;
                result.Message = "Failed attacks grant nothing.";
                return result;
            }

            if (!context.IsRankedAsyncLadder || context.IsTutorialOrBotOrPractice || context.IsPrivateOrRematch)
            {
                result.Status = BoundCaptiveCaptureStatus.NotAnEligibleMatchType;
                result.Message = "Only eligible ranked async-ladder wins can capture.";
                return result;
            }

            if (context.OpponentIsGuildmateOrFriend)
            {
                result.Status = BoundCaptiveCaptureStatus.ExcludedRelationship;
                result.Message = "No capture from guildmates or friends.";
                return result;
            }

            string today = DayKeyFor(utcNow);
            if (CapturesOn(state, today) >= MaxCapturesPerUtcDay)
            {
                result.Status = BoundCaptiveCaptureStatus.DailyCaptureCapReached;
                result.Message = "Daily Prison capture cap reached.";
                return result;
            }

            if (IsOpponentOnCooldown(state, context.OpponentAccountId, utcNow))
            {
                result.Status = BoundCaptiveCaptureStatus.SameOpponentOnCooldown;
                result.Message = "That opponent is on cooldown.";
                return result;
            }

            IReadOnlyList<string> snapshot = context.OpponentDefenseSnapshotCardIds;
            if (snapshot == null || snapshot.Count == 0)
            {
                result.Status = BoundCaptiveCaptureStatus.OpponentHasNoEligibleCard;
                result.Message = "Opponent had no eligible card.";
                return result;
            }

            string picked = serverPickCard != null ? serverPickCard(snapshot) : snapshot[0];
            if (string.IsNullOrEmpty(picked))
            {
                result.Status = BoundCaptiveCaptureStatus.OpponentHasNoEligibleCard;
                result.Message = "Server selected no card.";
                return result;
            }

            var item = new BoundCaptiveFodderItem
            {
                CaptureId = captureId,
                SourceCardId = picked,
                SourceRarity = 0,
                CapturedUtcDayKey = today,
                OpponentAccountId = context.OpponentAccountId,
                Consumed = false,
            };

            state.Items.Add(item);
            state.GrantedCaptureIds.Add(captureId);
            state.LastCaptureUtcDayKey = today;

            result.Status = BoundCaptiveCaptureStatus.Granted;
            result.Item = item;
            return result;
        }

        private static int CapturesOn(BoundCaptivePrisonState state, string dayKey) =>
            state.Items.FindAll(i => i.CapturedUtcDayKey == dayKey).Count;

        private static bool IsOpponentOnCooldown(BoundCaptivePrisonState state, string opponentId, DateTime utcNow)
        {
            if (string.IsNullOrEmpty(opponentId)) return false;
            foreach (BoundCaptiveFodderItem item in state.Items)
            {
                if (item.OpponentAccountId != opponentId) continue;
                if (!DateTime.TryParse(item.CapturedUtcDayKey, out DateTime captured)) continue;
                if ((utcNow.ToUniversalTime().Date - captured.Date).TotalDays < SameOpponentCooldownDays)
                    return true;
            }
            return false;
        }

        /// <summary>
        /// The ONLY way a Fodder item may leave the Prison: converted to generic sacrifice credits
        /// on the existing wallet. There is deliberately no sell, trade, pack, equip or
        /// Forge/Dust-burn path anywhere in this file.
        /// </summary>
        public static BoundCaptiveConsumeResult TryConsumeForSacrificeCredit(
            BoundCaptivePrisonState state, string captureId, MyriadOfDragons.Save.PlayerProfile profile)
        {
            var result = new BoundCaptiveConsumeResult();
            BoundCaptiveFodderItem item = state?.Items.Find(i => i.CaptureId == captureId);

            if (item == null)
            {
                result.Status = BoundCaptiveConsumeStatus.UnknownItem;
                result.Message = "No such captive.";
                return result;
            }
            if (item.Consumed)
            {
                result.Status = BoundCaptiveConsumeStatus.AlreadyConsumed;
                result.Message = "That captive was already consumed.";
                return result;
            }

            int yield = SacrificeYieldFor(item.SourceRarity);
            item.Consumed = true;

            if (profile?.collectionWallet != null)
                profile.collectionWallet.genericSacrificeCredits += yield;

            result.Status = BoundCaptiveConsumeStatus.Consumed;
            result.SacrificeCreditsGranted = yield;
            return result;
        }

        /// <summary>Unconsumed captives. Fodder never counts toward the collection, so this is
        /// deliberately separate from any collection count.</summary>
        public static List<BoundCaptiveFodderItem> UnconsumedItems(BoundCaptivePrisonState state) =>
            state == null ? new List<BoundCaptiveFodderItem>() : state.Items.FindAll(i => !i.Consumed);
    }
}
