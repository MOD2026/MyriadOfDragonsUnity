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
    /// Materials ladder amounts are locked in the register and may be shown.
    /// Build duration / v2 persist / Castle interlock table stay OPEN — never invented.
    /// </summary>
    public static class EmpireBuildingDetailCopy
    {
        public const string RuntimePlaceholder = "[runtime]";

        public const string DurationOpenNote =
            "Build duration is still OPEN (v2 pacing timers not locked) — " +
            "docs/LOCKED_DECISIONS_REGISTER.md Empire construction.";

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

            int materialsLevel = 1;
            string goldPart = null;
            if (profile != null)
            {
                if (kind == EmpireBuildingKind.Castle)
                {
                    materialsLevel = System.Math.Max(1, profile.castleLevel);
                    int gold = PlayerEmpireData.GoldCostForCastleUpgrade(profile.castleLevel);
                    if (gold > 0) goldPart = $"{gold:N0} Gold (Castle v1)";
                }
                else if (kind == EmpireBuildingKind.Barracks)
                {
                    materialsLevel = System.Math.Max(1, profile.barracksLevel);
                    int target = PlayerEmpireData.NextPaidBarracksMilestone(profile.barracksLevel);
                    int gold = target == 0 ? 0 : PlayerEmpireData.GoldCostForBarracksUpgrade(profile.barracksLevel, target);
                    if (gold > 0) goldPart = $"{gold:N0} Gold (Barracks v1)";
                }
                else if (kind == EmpireBuildingKind.Gate)
                {
                    materialsLevel = System.Math.Max(1, profile.gateLevel);
                    int target = PlayerEmpireData.NextPaidGateMilestone(profile.gateLevel);
                    int gold = target == 0 ? 0 : PlayerEmpireData.GoldCostForGateUpgrade(profile.gateLevel, target);
                    if (gold > 0) goldPart = $"{gold:N0} Gold (Gate v1)";
                }
            }

            int materials = EmpireMaterialsLadder.CostToUpgrade(materialsLevel);
            string materialsPart = materials > 0
                ? $"{materials:N0} Materials (locked v2 ladder from L{materialsLevel})"
                : "MAX (Materials ladder)";

            return goldPart != null ? $"{goldPart} · {materialsPart}" : materialsPart;
        }

        public static string FormatDurationLine() => RuntimePlaceholder + " — " + DurationOpenNote;

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

            if (kind == EmpireBuildingKind.Castle)
                return $"LEVEL {profile.castleLevel}";
            if (kind == EmpireBuildingKind.Barracks)
                return $"LEVEL {profile.barracksLevel}";
            if (kind == EmpireBuildingKind.Gate)
                return $"LEVEL {profile.gateLevel}";

            return "LEVEL " + RuntimePlaceholder + " (v2 level field not on save)";
        }
    }
}
