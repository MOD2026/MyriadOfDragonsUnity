using MyriadOfDragons.Data;
using MyriadOfDragons.Save;

namespace MyriadOfDragons.Empire
{
    public enum EmpireBuildingDetailUpgradeStatus
    {
        StartedV1,
        NonUpgradeBuilding,
        OpenValuesNotLocked,
        V2PersistNotWired,
        Failed,
    }

    public sealed class EmpireBuildingDetailUpgradeResult
    {
        public EmpireBuildingDetailUpgradeStatus Status;
        public string Message;
    }

    /// <summary>
    /// Copy + upgrade-cost wells for Empire Building Detail Popup V1.
    /// Materials ladder amounts and build-duration bands are locked in the register.
    /// </summary>
    public static class EmpireBuildingDetailCopy
    {
        public const string RuntimePlaceholder = "[runtime]";

        public const string DurationOpenNote =
            "Build duration uses the locked Empire pacing curve (30min–14d by target band).";

        /// <summary>
        /// Locked tooltip for the three interlock-only buildings (BS, 2026-08-26).
        ///
        /// CAPTURED BUT NOT YET DISPLAYED: this popup has no tooltip mechanism at all - no hover
        /// host, no long-press handler, nothing to attach it to. Inventing one as a side effect of
        /// a copy change would be a UI feature nobody asked for, so the string lives here, locked
        /// and ready, and whoever builds tooltip support wires it up. Flagged rather than silently
        /// dropped, because locked copy with no host is exactly the kind of thing that gets
        /// "delivered" and never appears.
        /// </summary>
        public const string StructureLevelTooltip =
            "Structure Level affects Empire interlocks and construction progression. It does not " +
            "imply that this building's online feature is currently active.";

        /// <summary>The three buildings whose level is real but whose online feature is not live.</summary>
        public static bool UsesStructureLevelWording(EmpireBuildingKind kind) =>
            kind == EmpireBuildingKind.GuildHall || kind == EmpireBuildingKind.Embassy ||
            kind == EmpireBuildingKind.Prison;

        public static string FormatVariantFraming(EmpireBuildingKind kind)
        {
            switch (kind)
            {
                // Status lines locked by BS 2026-08-26, alongside the STRUCTURE LEVEL wording.
                // Each says the same two things in the building's own terms: the structure
                // progression is REAL, and the online feature is NOT live yet.
                //
                // Guild Hall's previous "NON-UPGRADE BUILDING" was the same inconsistency I flagged
                // on the level line, in a second place - it now has a real persisted level, so
                // calling it a non-upgrade building was already untrue here.
                case EmpireBuildingKind.GuildHall:
                    return "Supports Embassy interlock progression. Guild functions coming later.";
                case EmpireBuildingKind.Prison:
                    return "Structure progression active. Capture systems unavailable until " +
                           "online services ship.";
                case EmpireBuildingKind.Embassy:
                    return "Structure progression active. Player-help network unavailable until " +
                           "online services ship.";
                default:
                    return EmpireBuildingRoster.Get(kind).ServerStatus;
            }
        }

        public static string FormatUpgradeCostLine(EmpireBuildingKind kind, PlayerProfile profile)
        {
            EmpireBuildingDefinition def = EmpireBuildingRoster.Get(kind);
            if (!def.HasUpgradeLadder)
                return "NON-UPGRADE BUILDING";

            int fromLevel = 1;
            int toLevelExclusive = 2;
            string goldPart = null;
            if (profile != null)
            {
                if (kind == EmpireBuildingKind.Castle)
                {
                    fromLevel = System.Math.Max(1, profile.castleLevel);
                    toLevelExclusive = fromLevel + 1;
                    int gold = PlayerEmpireData.GoldCostForCastleUpgrade(profile.castleLevel);
                    if (gold > 0) goldPart = $"{gold:N0} Gold";
                }
                else if (kind == EmpireBuildingKind.Barracks)
                {
                    fromLevel = System.Math.Max(1, profile.barracksLevel);
                    int target = PlayerEmpireData.NextPaidBarracksMilestone(profile.barracksLevel);
                    toLevelExclusive = target > 0 ? target : fromLevel;
                    int gold = target == 0 ? 0 : PlayerEmpireData.GoldCostForBarracksUpgrade(profile.barracksLevel, target);
                    if (gold > 0) goldPart = $"{gold:N0} Gold";
                }
                else if (kind == EmpireBuildingKind.Gate)
                {
                    fromLevel = System.Math.Max(1, profile.gateLevel);
                    int target = PlayerEmpireData.NextPaidGateMilestone(profile.gateLevel);
                    toLevelExclusive = target > 0 ? target : fromLevel;
                    int gold = target == 0 ? 0 : PlayerEmpireData.GoldCostForGateUpgrade(profile.gateLevel, target);
                    if (gold > 0) goldPart = $"{gold:N0} Gold";
                }
            }

            int materials = EmpireMaterialsLadder.CostBetween(fromLevel, toLevelExclusive);
            string materialsPart = materials > 0
                ? $"{materials:N0} Materials"
                : "MAX (Materials ladder)";

            return goldPart != null ? $"{goldPart} · {materialsPart}" : materialsPart;
        }

        public static string FormatDurationLine(PlayerProfile profile, EmpireBuildingKind kind)
        {
            int targetLevel = ResolveNextTargetLevel(kind, profile);
            if (targetLevel <= 0)
                return "MAX — no further build timer";

            int seconds = EmpireConstructionTimer.DurationSecondsForTargetLevel(targetLevel);
            return $"{EmpireConstructionTimer.FormatDuration(seconds)} — {DurationOpenNote}";
        }

        /// <summary>Back-compat overload for callers that only need the locked-curve note.</summary>
        public static string FormatDurationLine() =>
            FormatDurationLine(null, EmpireBuildingKind.Castle);

        private static int ResolveNextTargetLevel(EmpireBuildingKind kind, PlayerProfile profile)
        {
            if (profile == null)
                return 2;

            switch (kind)
            {
                case EmpireBuildingKind.Castle:
                    return profile.castleLevel >= PlayerEmpireData.MaxCastleLevel
                        ? 0
                        : profile.castleLevel + 1;
                case EmpireBuildingKind.Barracks:
                    return PlayerEmpireData.NextPaidBarracksMilestone(profile.barracksLevel);
                case EmpireBuildingKind.Gate:
                    return PlayerEmpireData.NextPaidGateMilestone(profile.gateLevel);
                default:
                    return 0;
            }
        }

        public static string FormatCurrentBenefit(EmpireBuildingKind kind, PlayerProfile profile)
        {
            EmpireBuildingDefinition def = EmpireBuildingRoster.Get(kind);
            if (profile == null)
                return def.Phase1Function;

            if (kind == EmpireBuildingKind.Castle)
            {
                profile.ApplyDataToEmpire();
                return $"Cap {profile.Empire.ResourceCap} · Start HP {profile.Empire.StartingAvatarHealth}";
            }

            if (kind == EmpireBuildingKind.Barracks)
            {
                profile.ApplyDataToEmpire();
                return $"{profile.Empire.DeckSlotCount} Deck Slots";
            }

            if (kind == EmpireBuildingKind.Gate)
            {
                int chapter = PlayerEmpireData.GetHighestCampaignChapterAllowed(profile.gateLevel);
                return $"Campaign open: Ch{chapter}";
            }

            return def.Phase1Function;
        }

        public static string FormatNextBenefit(EmpireBuildingKind kind)
        {
            if (kind == EmpireBuildingKind.GuildHall)
                return "NON-UPGRADE BUILDING";

            if (kind == EmpireBuildingKind.Castle || kind == EmpireBuildingKind.Barracks || kind == EmpireBuildingKind.Gate)
                return "Next-tier payoff is on the Empire row before Gold commit (v1).";

            return RuntimePlaceholder + " — v2 next-tier numeric payoff not locked beyond Phase-1 function copy.";
        }

        public static string FormatLevelLine(EmpireBuildingKind kind, PlayerProfile profile)
        {
            // "STRUCTURE LEVEL", never a bare "LEVEL n", for the three buildings whose level is
            // real and persisted but whose online feature is not live (BS, locked 2026-08-26).
            // BS's reasoning, kept because it is the whole point of the wording: "Do not show
            // simply 'LEVEL 12' - that would imply the building has a functioning Level-12 feature
            // set." The level IS real and drives Empire interlocks; the FEATURE is not.
            //
            // Guild Hall's old "LEVEL — (flat)" is deliberately gone rather than reverted to: it
            // now carries a real 1-30 level matching the shipped interlock.
            if (kind == EmpireBuildingKind.GuildHall || kind == EmpireBuildingKind.Embassy ||
                kind == EmpireBuildingKind.Prison)
            {
                if (profile == null) return "STRUCTURE LEVEL " + RuntimePlaceholder;
                return $"STRUCTURE LEVEL {EmpireBuildingLevels.LevelOf(profile, kind)}";
            }

            if (profile == null)
                return "LEVEL " + RuntimePlaceholder;

            // Storage / Training Grounds / Quarry / Academy / Tree of Knowledge used to fall
            // through to the "[runtime]" marker below because no level field existed for them.
            // Those fields are on the save now (owner-locked 2026-08-25), so the marker would be a
            // lie - it is reserved for values that genuinely are not persisted yet.
            if (EmpireBuildingLevels.HasStoredLevel(kind))
                return $"LEVEL {EmpireBuildingLevels.LevelOf(profile, kind)}";

            return "LEVEL " + RuntimePlaceholder + " (v2 level field not on save)";
        }
    }
}
