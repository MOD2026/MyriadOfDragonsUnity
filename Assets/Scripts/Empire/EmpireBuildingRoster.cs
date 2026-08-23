using System;
using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Empire
{
    /// <summary>
    /// Empire construction v2 - the 11-building roster locked in
    /// docs/LOCKED_DECISIONS_REGISTER.md ("Empire construction — STRUCTURE LOCKED 2026-08-23").
    /// Pure data/logic only, deliberately not wired into BattleController/GameBootstrap or any UI,
    /// and does not touch PlayerProfile/SaveSystem/SaveMigration/SaveManager - none of those have a
    /// field for a v2 construction project, a Materials balance, or per-building levels yet, and
    /// adding one needs a save-shape conversation with the owner first (frozen files, per
    /// CLAUDE.md). Building/upgrading anything through this layer therefore has nowhere to persist
    /// yet - see the "still open" list at the bottom of this file's own comments.
    ///
    /// This is a separate system from the currently-shipped Castle/Barracks/Gate Gold-based
    /// single-slot flow in EmpireConstructionRules.cs/PlayerEmpireData.cs (EmpireBuildingId there
    /// has only Castle/Barracks/Gate, uses Gold not Materials, and is already wired end to end into
    /// EmpirePresenter). That v1 system is untouched here - this file does not modify it, extend
    /// it, or share its enum, so the two never collide.
    /// </summary>
    public enum EmpireBuildingKind
    {
        Castle,
        Barracks,
        Storage,
        TrainingGrounds,
        Quarry,
        Gate,
        Academy,
        Embassy,
        TreeOfKnowledge,
        Prison,
        GuildHall,
    }

    /// <summary>One row of the locked 11-building roster table.</summary>
    public class EmpireBuildingDefinition
    {
        public readonly EmpireBuildingKind Kind;
        public readonly string DisplayName;
        public readonly string Phase1Function;
        public readonly string ServerStatus;

        /// <summary>False only for Guild Hall - "flat, single-level, no upgrade ladder; entry
        /// point only" per the locked roster table. Every other building uses the Materials
        /// ladder (see <see cref="EmpireMaterialsLadder"/>).</summary>
        public readonly bool HasUpgradeLadder;

        public EmpireBuildingDefinition(EmpireBuildingKind kind, string displayName, string phase1Function,
            string serverStatus, bool hasUpgradeLadder)
        {
            Kind = kind;
            DisplayName = displayName;
            Phase1Function = phase1Function;
            ServerStatus = serverStatus;
            HasUpgradeLadder = hasUpgradeLadder;
        }
    }

    public static class EmpireBuildingRoster
    {
        /// <summary>The exact 11 rows from the locked roster table, in the doc's own order.</summary>
        public static readonly IReadOnlyList<EmpireBuildingDefinition> Buildings = new[]
        {
            new EmpireBuildingDefinition(EmpireBuildingKind.Castle, "Castle",
                "Progression spine, capacity milestones", "Fully functional", hasUpgradeLadder: true),
            new EmpireBuildingDefinition(EmpireBuildingKind.Barracks, "Barracks",
                "Recruits new soldiers; deck-slot/Resource-regen/replenishment (milestone levels only: 1/5/10/15/20/25/30)",
                "Fully functional", hasUpgradeLadder: true),
            new EmpireBuildingDefinition(EmpireBuildingKind.Storage, "Storage",
                "Gold/Materials capacity; production pauses at cap, never silently deletes",
                "Fully functional", hasUpgradeLadder: true),
            new EmpireBuildingDefinition(EmpireBuildingKind.TrainingGrounds, "Training Grounds",
                "Upgrades existing soldiers/cards via deterministic Collection/Evolution rules",
                "Fully functional", hasUpgradeLadder: true),
            new EmpireBuildingDefinition(EmpireBuildingKind.Quarry, "Quarry",
                "Merged Barn+Gold Mine; sole passive Materials producer, never Gold",
                "Fully functional", hasUpgradeLadder: true),
            new EmpireBuildingDefinition(EmpireBuildingKind.Gate, "Gate",
                "World-map defence, protected-loot floor", "Solo stand-in; raid enforcement needs server",
                hasUpgradeLadder: true),
            new EmpireBuildingDefinition(EmpireBuildingKind.Academy, "Academy",
                "Personal research, codex, recipes", "Solo works; guild research later", hasUpgradeLadder: true),
            new EmpireBuildingDefinition(EmpireBuildingKind.Embassy, "Embassy",
                "Construction-help; scales both charges/day and reduction/help by level; safety cap is a lifetime cap per project (not daily), min(30% of timer, approved max hours)",
                "Personal stand-in; guild help needs server", hasUpgradeLadder: true),
            new EmpireBuildingDefinition(EmpireBuildingKind.TreeOfKnowledge, "Tree of Knowledge",
                "Evolution/XP home - grandfather rule: existing accounts keep live Evolution access, no migration may invalidate progress",
                "Fully functional", hasUpgradeLadder: true),
            new EmpireBuildingDefinition(EmpireBuildingKind.Prison, "Prison",
                "Captive/sacrifice placeholder, non-destructive", "Server-dependent stand-in", hasUpgradeLadder: true),
            new EmpireBuildingDefinition(EmpireBuildingKind.GuildHall, "Guild Hall",
                "Flat, single-level, no upgrade ladder; entry point only", "Server-dependent stand-in",
                hasUpgradeLadder: false),
        };

        public static EmpireBuildingDefinition Get(EmpireBuildingKind kind) =>
            Buildings.Single(b => b.Kind == kind);

        /// <summary>The 10 buildings that use the Materials ladder (every building except Guild
        /// Hall, which is flat/single-level per its own roster row).</summary>
        public static IEnumerable<EmpireBuildingDefinition> LaddedBuildings =>
            Buildings.Where(b => b.HasUpgradeLadder);

        /// <summary>Locked: "Barracks/Gate/Castle/Academy/Embassy available Phase-1 start, ungated
        /// by Castle level" - the 5-building core spine. The register does not state a start-gate
        /// condition for the other 6 (Storage/Training Grounds/Quarry/Tree of Knowledge/Prison/
        /// Guild Hall) either way, so this reports only the roster's own confirmed claim - it does
        /// not invent a gate for the other six.</summary>
        public static readonly IReadOnlyList<EmpireBuildingKind> CoreSpineUngatedAtPhase1Start = new[]
        {
            EmpireBuildingKind.Castle, EmpireBuildingKind.Barracks, EmpireBuildingKind.Gate,
            EmpireBuildingKind.Academy, EmpireBuildingKind.Embassy,
        };
    }

    /// <summary>
    /// The locked Materials cost ladder ("RATES LOCKED 2026-08-23 — full packet closed"): per
    /// building, 5×1,100 (L1-5) + 5×2,200 (L6-10) + 5×4,400 (L11-15) + 5×7,700 (L16-20) +
    /// 5×11,000 (L21-25) + 4×12,375 (L26-30) = 181,500 Materials exactly to take one building from
    /// L1 to L30 (29 upgrade steps, banded into 6 pricing tiers - the last tier is only 4 steps,
    /// not 5, since a building starts at L1 not L0). ×10 laddered buildings = 1,815,000, matching
    /// the existing campaign Materials faucet exactly (locked, CC-verified, no faucet recompute
    /// needed). Applies identically to all 10 laddered buildings - the roster locks one shared
    /// ladder, not a per-building curve.
    /// </summary>
    public static class EmpireMaterialsLadder
    {
        public const int MaxLevel = 30;

        /// <summary>(band start level inclusive, band end level exclusive, Materials cost per
        /// upgrade step within the band).</summary>
        private static readonly (int startLevel, int endLevelExclusive, int costPerStep)[] Bands =
        {
            (1, 6, 1_100),
            (6, 11, 2_200),
            (11, 16, 4_400),
            (16, 21, 7_700),
            (21, 26, 11_000),
            (26, 30, 12_375),
        };

        /// <summary>Materials cost to upgrade a building from <paramref name="currentLevel"/> to
        /// currentLevel + 1. Returns 0 for an out-of-range or already-maxed level.</summary>
        public static int CostToUpgrade(int currentLevel)
        {
            if (currentLevel < 1 || currentLevel >= MaxLevel) return 0;
            foreach ((int startLevel, int endLevelExclusive, int costPerStep) in Bands)
            {
                if (currentLevel >= startLevel && currentLevel < endLevelExclusive)
                    return costPerStep;
            }

            return 0;
        }

        /// <summary>Total Materials to take one building from <paramref name="fromLevel"/> (default
        /// L1) all the way to <see cref="MaxLevel"/>. 181,500 from L1, matching the locked total.</summary>
        public static int TotalMaterialsToMax(int fromLevel = 1)
        {
            int total = 0;
            for (int level = Math.Max(1, fromLevel); level < MaxLevel; level++)
                total += CostToUpgrade(level);
            return total;
        }
    }

    /// <summary>
    /// Embassy's level-scaling construction-help curve. Only the two band endpoints are locked in
    /// docs/LOCKED_DECISIONS_REGISTER.md: "1/10min at L1-5 up to 6/90min at L26-30" (charges/day
    /// and reduction-minutes/charge both scale by level, same 6-band structure as the Materials
    /// ladder). The four middle bands (L6-10/L11-15/L16-20/L21-25) are NOT given anywhere in the
    /// locked packet - <see cref="ChargesPerDayForBand"/>/<see cref="ReductionMinutesPerChargeForBand"/>
    /// intentionally only resolve the two confirmed bands and throw for the other four rather than
    /// invent numbers; see this task's completion report for the open item.
    ///
    /// The lifetime-per-project safety cap IS fully locked and implemented here:
    /// min(30% of the project's own timer, 6 hours).
    /// </summary>
    public static class EmpireEmbassyHelp
    {
        public const int ChargesPerDayAtBandL1To5 = 1;
        public const int ReductionMinutesPerChargeAtBandL1To5 = 10;
        public const int ChargesPerDayAtBandL26To30 = 6;
        public const int ReductionMinutesPerChargeAtBandL26To30 = 90;

        private static readonly TimeSpan LifetimeCapCeiling = TimeSpan.FromHours(6);
        private const double LifetimeCapFractionOfTimer = 0.30;

        /// <summary>Locked: "lifetime cap per project (not daily), min(30% of timer, approved max
        /// hours)" - approved max is 6 hours.</summary>
        public static TimeSpan LifetimeCapPerProject(TimeSpan projectTimer)
        {
            TimeSpan thirtyPercentOfTimer = TimeSpan.FromTicks((long)(projectTimer.Ticks * LifetimeCapFractionOfTimer));
            return thirtyPercentOfTimer < LifetimeCapCeiling ? thirtyPercentOfTimer : LifetimeCapCeiling;
        }

        /// <summary>Only L1-5 and L26-30 are locked. Throws for any other level - see class doc.</summary>
        public static int ChargesPerDayForBand(int embassyLevel)
        {
            if (embassyLevel >= 1 && embassyLevel <= 5) return ChargesPerDayAtBandL1To5;
            if (embassyLevel >= 26 && embassyLevel <= 30) return ChargesPerDayAtBandL26To30;
            throw new NotSupportedException(
                $"Embassy level {embassyLevel}'s charges/day is not locked anywhere yet - only L1-5 and L26-30 are confirmed. " +
                "Do not guess an interpolated value; get the real L6-10/L11-15/L16-20/L21-25 numbers first.");
        }

        /// <summary>Only L1-5 and L26-30 are locked. Throws for any other level - see class doc.</summary>
        public static int ReductionMinutesPerChargeForBand(int embassyLevel)
        {
            if (embassyLevel >= 1 && embassyLevel <= 5) return ReductionMinutesPerChargeAtBandL1To5;
            if (embassyLevel >= 26 && embassyLevel <= 30) return ReductionMinutesPerChargeAtBandL26To30;
            throw new NotSupportedException(
                $"Embassy level {embassyLevel}'s reduction/charge is not locked anywhere yet - only L1-5 and L26-30 are confirmed. " +
                "Do not guess an interpolated value; get the real L6-10/L11-15/L16-20/L21-25 numbers first.");
        }
    }

    /// <summary>
    /// Castle-level interlock: NOT implemented. docs/LOCKED_DECISIONS_REGISTER.md's only numeric
    /// artifact for this ("e.g. Castle 15 → Barracks max 15 / Gate max 13") lives entirely inside a
    /// section the same doc explicitly labels "Superseded — prior reopened/partial versions (kept
    /// for history only)" and "STALE until the 5-building recompute lands." One qualitative rule
    /// ("Barracks/Gate max level tracks Castle level") plus one stale illustrative example is not
    /// enough to derive a real 1-30 table - Barracks' relationship could plausibly be read as 1:1
    /// from that one example, but Gate's cannot (15→13 is a single point; a flat offset, a ratio,
    /// and a milestone-banded curve like the *already-shipped* v1 Gate system's
    /// PlayerEmpireData.MinimumCastleForGateLevel all fit that one point equally well, and v1's own
    /// real numbers do not match 15→13 either). Building a function here would be inventing a
    /// design number, not implementing a locked one - flagged in this task's completion report
    /// instead. This class exists as the explicit placeholder for that gap.
    /// </summary>
    public static class EmpireCastleInterlock
    {
        public const string StatusNote =
            "No locked 1-30 interlock table exists for the 11-building v2 roster - only one stale " +
            "illustrative example (Castle 15 -> Barracks 15 / Gate 13) predating the 5-building reopen. " +
            "Needs a real recompute before this can be implemented.";
    }
}
