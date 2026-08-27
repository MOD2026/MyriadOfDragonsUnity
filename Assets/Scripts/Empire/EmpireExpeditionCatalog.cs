using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Empire
{
    /// <summary>
    /// Empire Expedition — post-campaign farm loop (structure locked 2026-08-24 in
    /// docs/LOCKED_DECISIONS_REGISTER.md). Pure catalog + open-value markers only.
    /// Does not invent Stamina cost, tier rewards, daily attempts, unlock condition, or
    /// rotation cadence — those remain OPEN until the owner locks them.
    ///
    /// Separate from Campaign: Expedition stages never share Campaign stage ids, and
    /// Campaign's replay=0 rule is untouched here. Combat / Auto-Fight resolution is
    /// Battle-seat work — this file only describes the metagame stage list shell.
    ///
    /// Persistence: PlayerProfile has no Expedition attempt / daily-gold / Materials fields
    /// yet (frozen save shape). Do not add them here — see completion report.
    /// </summary>
    public sealed class EmpireExpeditionStageDefinition
    {
        public readonly string StageId;
        public readonly string Title;
        public readonly string Description;

        public EmpireExpeditionStageDefinition(string stageId, string title, string description)
        {
            StageId = stageId;
            Title = title;
            Description = description;
        }
    }

    /// <summary>
    /// Clear rewards (Stamina cost, Gold, Materials, daily Gold cap) LOCKED 2026-08-26 per
    /// LOCKED_DECISIONS_REGISTER.md; daily attempt cap / unlock condition / rotation cadence
    /// remain open. Locked structure that IS implemented elsewhere: +10% Gold guild bonus
    /// (display/query hook), fail-closed if guild service unavailable, bonus cannot raise the
    /// daily Gold cap, Gold+Materials paid on clear via one authoritative transaction,
    /// Stamina-gated.
    /// </summary>
    public static class EmpireExpeditionOpenValues
    {
        public const string RegisterCitation =
            "docs/LOCKED_DECISIONS_REGISTER.md — Empire Expedition (LOCKED 2026-08-24, structure only)";

        /// <summary>LOCKED 2026-08-26 (BS, verified against this file's real open slots).</summary>
        public static readonly int? StaminaCostPerClear = 10;

        /// <summary>LOCKED 2026-08-26 — base Gold granted on a clear (before guild bonus).</summary>
        public static readonly int? BaseGoldPerClear = 300;

        /// <summary>LOCKED 2026-08-26 — Construction Materials granted on a clear.
        /// PlayerProfile.constructionMaterials already exists (2026-08-24) and
        /// EmpireExpeditionClearTransaction already persists to it - no frozen-file blocker.</summary>
        public static readonly int? BaseMaterialsPerClear = 200;

        /// <summary>LOCKED 2026-08-26 — daily Expedition Gold cap (3 clears x 300; bonus cannot raise this).</summary>
        public static readonly int? DailyExpeditionGoldCap = 900;

        /// <summary>OPEN — max clears / attempts per UTC day.</summary>
        public static readonly int? DailyAttemptCap = null;

        /// <summary>OPEN — unlock condition (e.g. campaign chapter cleared). Null = not locked.</summary>
        public static readonly string UnlockConditionNote = null;

        /// <summary>OPEN — rotation cadence (how the stage list cycles). Null = not locked.</summary>
        public static readonly string RotationCadenceNote = null;

        /// <summary>Locked structure: active guild members get +10% Gold on Expedition clears.</summary>
        public const float GuildGoldBonusFraction = 0.10f;

        public static bool AreClearRewardsConfigured =>
            StaminaCostPerClear.HasValue
            && BaseGoldPerClear.HasValue
            && BaseMaterialsPerClear.HasValue
            && DailyExpeditionGoldCap.HasValue;

        /// <summary>Short, player-facing status copy for this shell's status label. Deliberately
        /// separate from <see cref="StatusNote"/>: that string is a developer/transaction
        /// diagnostic consumed as the ActionResult Message, and at full length it overflows the
        /// band it is shown in. StatusNote stays byte-identical for every Message consumer; only
        /// the label reads this. Carries no design numbers - a shell line must not become a
        /// second source for live values. Same split as BattlePassOpenValues.PlayerStatus
        /// (02eb9f8) and the three shells in 7e14ab5.</summary>
        public static string PlayerStatus => "Expedition rewards are still being tuned.";

        public static string StatusNote =>
            "Empire Expedition structure and clear rewards (Stamina/Gold/Materials/daily Gold cap) " +
            "are locked; daily attempts, unlock condition, and rotation cadence are still OPEN — " + RegisterCitation;
    }

    /// <summary>Static Expedition stage list — not Campaign stages. Rotation filtering is a no-op
    /// until <see cref="EmpireExpeditionOpenValues.RotationCadenceNote"/> is locked.</summary>
    public static class EmpireExpeditionCatalog
    {
        /// <summary>Shell roster ids — distinct from Campaign "N-M" ids. Content/enemy decks are
        /// Battle-seat; metagame only needs stable ids for the rotation screen and clear transaction.</summary>
        public static readonly IReadOnlyList<EmpireExpeditionStageDefinition> Stages = new[]
        {
            new EmpireExpeditionStageDefinition("exp-1", "Border Sweep",
                "Repeatable Empire Expedition — rewards OPEN until locked."),
            new EmpireExpeditionStageDefinition("exp-2", "Ash Road Patrol",
                "Repeatable Empire Expedition — rewards OPEN until locked."),
            new EmpireExpeditionStageDefinition("exp-3", "Vein Watch",
                "Repeatable Empire Expedition — rewards OPEN until locked."),
        };

        public static EmpireExpeditionStageDefinition Find(string stageId)
        {
            if (string.IsNullOrEmpty(stageId)) return null;
            foreach (EmpireExpeditionStageDefinition stage in Stages)
            {
                if (string.Equals(stage.StageId, stageId, StringComparison.Ordinal))
                    return stage;
            }

            return null;
        }

        /// <summary>Stages shown on the rotation screen. Until rotation cadence is locked, returns
        /// the full shell catalog (no invented filter).</summary>
        public static IReadOnlyList<EmpireExpeditionStageDefinition> GetActiveRotationStages(DateTime utcNow)
        {
            _ = utcNow; // reserved for locked cadence
            if (EmpireExpeditionOpenValues.RotationCadenceNote != null)
            {
                // Cadence locked later — filter would live here. Not inventing one now.
            }

            return Stages;
        }
    }
}
