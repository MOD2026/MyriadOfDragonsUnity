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
    /// Numbers still open per LOCKED_DECISIONS_REGISTER.md Empire Expedition section.
    /// Locked structure that IS implemented elsewhere: +10% Gold guild bonus (display/query
    /// hook), fail-closed if guild service unavailable, bonus cannot raise the daily Gold cap,
    /// Gold+Materials paid on clear via one authoritative transaction, Stamina-gated.
    /// </summary>
    public static class EmpireExpeditionOpenValues
    {
        public const string RegisterCitation =
            "docs/LOCKED_DECISIONS_REGISTER.md — Empire Expedition (LOCKED 2026-08-24, structure only)";

        /// <summary>OPEN — Stamina cost per clear. Null means not locked; clear transaction must refuse.</summary>
        public static readonly int? StaminaCostPerClear = null;

        /// <summary>OPEN — base Gold granted on a clear (before guild bonus).</summary>
        public static readonly int? BaseGoldPerClear = null;

        /// <summary>OPEN — Construction Materials granted on a clear. Persist path also blocked
        /// until PlayerProfile gains a Materials balance field (frozen — escalate).</summary>
        public static readonly int? BaseMaterialsPerClear = null;

        /// <summary>OPEN — daily Expedition Gold cap (bonus cannot raise this).</summary>
        public static readonly int? DailyExpeditionGoldCap = null;

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

        public static string StatusNote =>
            "Empire Expedition structure is locked; Stamina cost/clear, tier rewards, daily attempts, " +
            "unlock condition, and rotation cadence are still OPEN — " + RegisterCitation;
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
