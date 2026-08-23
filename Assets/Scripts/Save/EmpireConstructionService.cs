using MyriadOfDragons.Empire;

namespace MyriadOfDragons.Save
{
    /// <summary>
    /// One completion API: mutates profile wallet + levels, then ApplyDataToEmpire once.
    /// Offline A — no server timestamps required.
    /// </summary>
    public static class EmpireConstructionService
    {
        public static bool TryStart(PlayerProfile profile, EmpireBuildingId building, string projectId, out string error)
        {
            error = null;
            if (profile == null)
            {
                error = "No profile.";
                return false;
            }

            profile.empireConstruction ??= new EmpireConstructionState();
            int currentLevel = CurrentLevel(profile, building);
            var result = EmpireConstructionRules.TryStart(profile.empireConstruction,
                new EmpireConstructionStartRequest
                {
                    BuildingId = building,
                    CurrentBuildingLevel = currentLevel,
                    CastleLevel = profile.castleLevel,
                    AvailableGold = profile.gold,
                    ProjectId = projectId,
                });

            if (!result.Ok)
            {
                error = result.Error;
                return false;
            }

            if (!profile.TrySpendGold(result.GoldCharged))
            {
                error = "Insufficient Gold.";
                return false;
            }

            profile.empireConstruction = result.State;
            return true;
        }

        public static bool TryClaimComplete(PlayerProfile profile, string projectId)
        {
            if (profile?.empireConstruction == null)
                return false;

            int current = CurrentLevel(profile, profile.empireConstruction.buildingId);
            if (!EmpireConstructionRules.TryClaimComplete(
                    profile.empireConstruction, projectId, current,
                    out int newLevel, out EmpireConstructionState next))
            {
                return false;
            }

            SetLevel(profile, profile.empireConstruction.buildingId, newLevel);
            profile.empireConstruction = next;
            profile.ApplyDataToEmpire();
            return true;
        }

        private static int CurrentLevel(PlayerProfile profile, EmpireBuildingId building) =>
            building switch
            {
                EmpireBuildingId.Castle => profile.castleLevel,
                EmpireBuildingId.Barracks => profile.barracksLevel,
                EmpireBuildingId.Gate => profile.gateLevel,
                _ => 0,
            };

        private static void SetLevel(PlayerProfile profile, EmpireBuildingId building, int level)
        {
            switch (building)
            {
                case EmpireBuildingId.Castle: profile.castleLevel = level; break;
                case EmpireBuildingId.Barracks: profile.barracksLevel = level; break;
                case EmpireBuildingId.Gate: profile.gateLevel = level; break;
            }
        }
    }
}
