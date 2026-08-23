namespace MyriadOfDragons.Empire
{
    /// <summary>
    /// Offline construction A: Gold + instant ReadyToCollect. No PlayerProfile field yet —
    /// Save additive wiring is a separate CC step. Callers own wallet mutation.
    /// </summary>
    public enum EmpireBuildingId
    {
        None = 0,
        Castle = 1,
        Barracks = 2,
        Gate = 3,
    }

    public enum EmpireConstructionStatus
    {
        Idle = 0,
        Building = 1,
        ReadyToCollect = 2,
        CompleteClaimed = 3,
    }

    [System.Serializable]
    public class EmpireConstructionState
    {
        public string projectId = "";
        public EmpireBuildingId buildingId = EmpireBuildingId.None;
        public int targetLevel;
        public EmpireConstructionStatus status = EmpireConstructionStatus.Idle;
        public bool costCharged;
        public int costGold;
    }

    public struct EmpireConstructionStartRequest
    {
        public EmpireBuildingId BuildingId;
        public int CurrentBuildingLevel;
        public int CastleLevel;
        public int AvailableGold;
        public string ProjectId;
    }

    public struct EmpireConstructionStartResult
    {
        public bool Ok;
        public string Error;
        public int GoldCharged;
        public EmpireConstructionState State;
    }

    public static class EmpireConstructionRules
    {
        /// <summary>
        /// Offline A: charge Gold and land on ReadyToCollect in one transition (no local timer).
        /// </summary>
        /// After claim the slot must accept a new start — treat CompleteClaimed like cleared.
        public static EmpireConstructionStartResult TryStart(
            EmpireConstructionState current,
            EmpireConstructionStartRequest request)
        {
            if (current != null &&
                (current.status == EmpireConstructionStatus.Building ||
                 current.status == EmpireConstructionStatus.ReadyToCollect))
            {
                return Fail("A project is already active.");
            }

            if (string.IsNullOrEmpty(request.ProjectId))
                return Fail("projectId required.");

            int target;
            int cost;
            switch (request.BuildingId)
            {
                case EmpireBuildingId.Castle:
                    target = request.CurrentBuildingLevel + 1;
                    if (target < 2 || target > PlayerEmpireData.MaxCastleLevel)
                        return Fail("Castle is at cap.");
                    cost = PlayerEmpireData.GoldCostForCastleUpgrade(request.CurrentBuildingLevel);
                    break;
                case EmpireBuildingId.Barracks:
                    target = PlayerEmpireData.NextPaidBarracksMilestone(request.CurrentBuildingLevel);
                    if (target == 0)
                        return Fail("Barracks is at cap.");
                    cost = PlayerEmpireData.GoldCostForBarracksUpgrade(request.CurrentBuildingLevel, target);
                    break;
                case EmpireBuildingId.Gate:
                    target = PlayerEmpireData.NextPaidGateMilestone(request.CurrentBuildingLevel);
                    if (target == 0)
                        return Fail("Gate is at cap.");
                    if (request.CastleLevel < PlayerEmpireData.MinimumCastleForGateLevel(target))
                        return Fail("Castle prerequisite not met.");
                    cost = PlayerEmpireData.GoldCostForGateUpgrade(request.CurrentBuildingLevel, target);
                    break;
                default:
                    return Fail("Unknown building.");
            }

            if (cost <= 0)
                return Fail("Invalid upgrade cost.");
            if (request.AvailableGold < cost)
                return Fail("Insufficient Gold.");

            var state = new EmpireConstructionState
            {
                projectId = request.ProjectId,
                buildingId = request.BuildingId,
                targetLevel = target,
                status = EmpireConstructionStatus.ReadyToCollect,
                costCharged = true,
                costGold = cost,
            };

            return new EmpireConstructionStartResult
            {
                Ok = true,
                GoldCharged = cost,
                State = state,
            };
        }

        /// <summary>
        /// Idempotent claim: only ReadyToCollect + costCharged + matching projectId raises a level.
        /// Returns the new building level, or the same level on duplicate/no-op.
        /// </summary>
        public static bool TryClaimComplete(
            EmpireConstructionState state,
            string projectId,
            int currentBuildingLevel,
            out int newBuildingLevel,
            out EmpireConstructionState nextState)
        {
            newBuildingLevel = currentBuildingLevel;
            nextState = state;

            if (state == null || state.status != EmpireConstructionStatus.ReadyToCollect)
                return false;
            if (!state.costCharged || state.costGold <= 0)
                return false;
            if (state.projectId != projectId)
                return false;
            if (state.targetLevel <= currentBuildingLevel)
            {
                // Duplicate claim after level already applied.
                nextState = IdleClone(state);
                return false;
            }

            newBuildingLevel = state.targetLevel;
            nextState = IdleClone(state);
            nextState.status = EmpireConstructionStatus.CompleteClaimed;
            return true;
        }

        private static EmpireConstructionStartResult Fail(string error) =>
            new EmpireConstructionStartResult { Ok = false, Error = error };

        private static EmpireConstructionState IdleClone(EmpireConstructionState prior) =>
            new EmpireConstructionState
            {
                projectId = prior.projectId,
                buildingId = EmpireBuildingId.None,
                targetLevel = 0,
                status = EmpireConstructionStatus.Idle,
                costCharged = false,
                costGold = 0,
            };
    }
}
