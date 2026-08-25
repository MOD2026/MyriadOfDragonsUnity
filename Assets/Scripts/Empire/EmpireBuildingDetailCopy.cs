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

        public static string FormatVariantFraming(EmpireBuildingKind kind)
        {
            switch (kind)
            {
                case EmpireBuildingKind.GuildHall:
                    return "NON-UPGRADE BUILDING";
                case EmpireBuildingKind.Prison:
                    return "SOLO FUNCTION LIMITED\nGUILD FEATURES PENDING SERVER";
                case EmpireBuildingKind.Embassy:
                    return "SOLO FUNCTION AVAILABLE\nGUILD MODE PENDING SERVER";
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
            if (kind == EmpireBuildingKind.GuildHall)
                return "LEVEL — (flat)";

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
