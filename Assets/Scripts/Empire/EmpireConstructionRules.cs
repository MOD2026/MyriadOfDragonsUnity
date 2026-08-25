namespace MyriadOfDragons.Empire
{
    /// <summary>
    /// Empire construction v2: Gold + Materials + client-clock pacing timer
    /// (Idle → Building → ReadyToCollect → CompleteClaimed). Timers are pacing-only —
    /// no speed-up purchase. Additive timer fields on <see cref="EmpireConstructionState"/>
    /// are JsonUtility-default-safe for old saves (0 → treated as already Ready when status says so).
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
        /// <summary>Additive — Materials charged at start (v2). Old saves deserialize as 0.</summary>
        public int costMaterials;
        /// <summary>Additive — Unix ms when Building started. Old saves: 0.</summary>
        public long startedUtcMs;
        /// <summary>Additive — Unix ms when Building becomes ReadyToCollect. Old saves: 0.</summary>
        public long endsAtUtcMs;
    }

    public struct EmpireConstructionStartRequest
    {
        public EmpireBuildingId BuildingId;
        public int CurrentBuildingLevel;
        public int CastleLevel;
        public int AvailableGold;
        public int AvailableMaterials;
        public string ProjectId;
        /// <summary>Wall clock for timer start; 0 → <see cref="EmpireConstructionTimer.UtcNowMs"/>.</summary>
        public long NowUtcMs;
    }

    public struct EmpireConstructionStartResult
    {
        public bool Ok;
        public string Error;
        public int GoldCharged;
        public int MaterialsCharged;
        public EmpireConstructionState State;
    }

    public static class EmpireConstructionRules
    {
        /// <summary>
        /// Charge Gold + Materials and enter Building with a pacing timer (locked 30min–14d curve).
        /// ReadyToCollect is reached only via <see cref="TryAdvanceToReady"/>.
        /// </summary>
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
            int costGold;
            int costMaterials;
            switch (request.BuildingId)
            {
                case EmpireBuildingId.Castle:
                    target = request.CurrentBuildingLevel + 1;
                    if (target < 2 || target > PlayerEmpireData.MaxCastleLevel)
                        return Fail("Castle is at cap.");
                    costGold = PlayerEmpireData.GoldCostForCastleUpgrade(request.CurrentBuildingLevel);
                    costMaterials = EmpireMaterialsLadder.CostBetween(
                        request.CurrentBuildingLevel, target);
                    break;
                case EmpireBuildingId.Barracks:
                    target = PlayerEmpireData.NextPaidBarracksMilestone(request.CurrentBuildingLevel);
                    if (target == 0)
                        return Fail("Barracks is at cap.");
                    if (request.CastleLevel < PlayerEmpireData.MinimumCastleForBarracksLevel(target))
                        return Fail("Castle prerequisite not met.");
                    costGold = PlayerEmpireData.GoldCostForBarracksUpgrade(request.CurrentBuildingLevel, target);
                    costMaterials = EmpireMaterialsLadder.CostBetween(
                        request.CurrentBuildingLevel, target);
                    break;
                case EmpireBuildingId.Gate:
                    target = PlayerEmpireData.NextPaidGateMilestone(request.CurrentBuildingLevel);
                    if (target == 0)
                        return Fail("Gate is at cap.");
                    if (request.CastleLevel < PlayerEmpireData.MinimumCastleForGateLevel(target))
                        return Fail("Castle prerequisite not met.");
                    costGold = PlayerEmpireData.GoldCostForGateUpgrade(request.CurrentBuildingLevel, target);
                    costMaterials = EmpireMaterialsLadder.CostBetween(
                        request.CurrentBuildingLevel, target);
                    break;
                default:
                    return Fail("Unknown building.");
            }

            if (costGold <= 0)
                return Fail("Invalid upgrade cost.");
            if (request.AvailableGold < costGold)
                return Fail("Insufficient Gold.");
            if (request.AvailableMaterials < costMaterials)
                return Fail("Insufficient Materials.");

            int durationSec = EmpireConstructionTimer.DurationSecondsForTargetLevel(target);
            if (durationSec <= 0)
                return Fail("Invalid build duration.");

            long now = request.NowUtcMs > 0 ? request.NowUtcMs : EmpireConstructionTimer.UtcNowMs;

            var state = new EmpireConstructionState
            {
                projectId = request.ProjectId,
                buildingId = request.BuildingId,
                targetLevel = target,
                status = EmpireConstructionStatus.Building,
                costCharged = true,
                costGold = costGold,
                costMaterials = costMaterials,
                startedUtcMs = now,
                endsAtUtcMs = now + durationSec * 1000L,
            };

            return new EmpireConstructionStartResult
            {
                Ok = true,
                GoldCharged = costGold,
                MaterialsCharged = costMaterials,
                State = state,
            };
        }

        /// <summary>
        /// Client-clock advance: Building → ReadyToCollect when now ≥ endsAt.
        /// Rollback rule: if now &lt; startedUtcMs (clock went backwards), do not complete —
        /// leave Building until a forward clock catches endsAt again.
        /// </summary>
        public static bool TryAdvanceToReady(EmpireConstructionState state, long nowUtcMs)
        {
            if (state == null || state.status != EmpireConstructionStatus.Building)
                return false;
            if (state.endsAtUtcMs <= 0)
                return false;
            if (nowUtcMs < state.startedUtcMs)
                return false;
            if (nowUtcMs < state.endsAtUtcMs)
                return false;

            state.status = EmpireConstructionStatus.ReadyToCollect;
            return true;
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
                costMaterials = 0,
                startedUtcMs = 0,
                endsAtUtcMs = 0,
            };
    }
}
