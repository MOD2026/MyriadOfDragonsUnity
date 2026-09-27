using System;

namespace MyriadOfDragons.Frontier
{
    /// <summary>
    /// CC10 client DTOs (docs/CC10_BETA_AUTHORITY_2026-09-27.md). Every type here is a read-only
    /// projection of state owned by the server-side CC10FrontierService. The client never authors
    /// ids, times, rewards, ranks, guild state, cargo state or receipts; it only echoes the request
    /// id it generated and displays what the server returns. Field names are the proposed wire
    /// contract and stay OPEN until the BE Frontier owner publishes the module.
    /// </summary>
    public enum Cc10System
    {
        CardTraining,
        Tavern,
        Missions,
        WorldMap,
        NpcSpots,
        GuildTerritory,
        IndividualResearch,
        GuildResearch,
        IndividualRankings,
        GuildRankings,
        Minigame,
        Cargo,
    }

    public static class Cc10Status
    {
        public const string Ok = "Ok";
        public const string AlreadyCommitted = "AlreadyCommitted";
        public const string Conflict = "Conflict";
        public const string Disabled = "Disabled";
        public const string Rejected = "Rejected";
        public const string CapExceeded = "CapExceeded";
        public const string NotFound = "NotFound";
    }

    public static class Cc10Endpoints
    {
        public const string ModuleName = "CC10Frontier";
        public const string GetState = "GetFrontierState";
        public const string TrainCard = "TrainCard";
        public const string UpgradeTavern = "UpgradeTavern";
        public const string AssignMission = "AssignMission";
        public const string AbandonMission = "AbandonMission";
        public const string ClaimMission = "ClaimMission";
        public const string TravelToNode = "TravelToNode";
        public const string ContributeTerritory = "ContributeTerritory";
        public const string StartResearch = "StartResearch";
        public const string CancelResearch = "CancelResearch";
        public const string CompleteResearch = "CompleteResearch";
        public const string ContributeGuildResearch = "ContributeGuildResearch";
        public const string GetRankings = "GetRankings";
        public const string StartMinigame = "StartMinigame";
        public const string SubmitMinigameResult = "SubmitMinigameResult";
        public const string AcceptCargo = "AcceptCargo";
        public const string ClaimCargo = "ClaimCargo";
    }

    /// <summary>Common envelope on every server response.</summary>
    [Serializable]
    public class Cc10Response
    {
        public string status = string.Empty;
        public string errorCode = string.Empty;
        public string requestId = string.Empty;
        public string receiptId = string.Empty;
        public long serverUtcMs;
        public int stateVersion;
        public int disableGeneration;
        public bool systemDisabled;

        public bool IsSuccess => status == Cc10Status.Ok || status == Cc10Status.AlreadyCommitted;
    }

    /// <summary>Authoritative wallet/card projection, display-only. The client hands it to the
    /// host via <see cref="Cc10FrontierClient.ProjectionReceived"/>; this layer never writes it
    /// into PlayerProfile (no new CC10 PlayerProfile field is permitted).</summary>
    [Serializable]
    public class Cc10Projection
    {
        public int gold;
        public int materials;
        public int dailyGoldUsed;
        public int dailyGoldCap;
        public string cardId = string.Empty;
        public int cardLevel;
        public int trainingXp;
    }

    [Serializable]
    public class Cc10TavernState
    {
        public int level;
        public bool upgradeActive;
        public long upgradeCompleteUtcMs;
        public int nextCostGold;
        public int nextCostMaterials;
        public int nextDurationMinutes;
    }

    [Serializable]
    public class Cc10MissionRow
    {
        public string missionId = string.Empty;
        public string kind = string.Empty;
        public string state = string.Empty;
        public int staminaCost;
        public int durationMinutes;
        public int goldReward;
        public int materialReward;
        public int dailyLimit;
        public int dailyRemaining;
        public long refreshUtcMs;
        public long completeUtcMs;
        public int attempt;
        public string receiptId = string.Empty;
    }

    [Serializable]
    public class Cc10MapNode
    {
        public string nodeId = string.Empty;
        public string regionId = string.Empty;
        public bool discovered;
        public int travelCost;
        public string missionId = string.Empty;
    }

    [Serializable]
    public class Cc10NpcSpot
    {
        public string spotId = string.Empty;
        public string kind = string.Empty;
        public string state = string.Empty;
        public string missionId = string.Empty;
    }

    [Serializable]
    public class Cc10Territory
    {
        public string territoryId = string.Empty;
        public string ownerGuildId = string.Empty;
        public bool contested;
        public int contribution;
        public long windowEndUtcMs;
        public string membershipSnapshotSig = string.Empty;
    }

    [Serializable]
    public class Cc10ResearchNode
    {
        public string nodeId = string.Empty;
        public string scope = string.Empty; // "Individual" | "Guild"
        public string[] prerequisiteIds = new string[0];
        public string state = string.Empty;
        public long completeUtcMs;
        public int costGold;
        public int costMaterials;
    }

    [Serializable]
    public class Cc10RankEntry
    {
        public int rank;
        public string displayName = string.Empty;
        public int score;
    }

    [Serializable]
    public class Cc10RankingBoard
    {
        public string scope = string.Empty; // "Individual" | "Guild"
        public string seasonId = string.Empty;
        public string phase = string.Empty;
        public string cursor = string.Empty;
        public Cc10RankEntry[] entries = new Cc10RankEntry[0];
    }

    [Serializable]
    public class Cc10MinigameSession
    {
        public string sessionId = string.Empty;
        public string seed = string.Empty;
        public string state = string.Empty;
        public bool resultVerified;
        public int goldReward;
        public int materialReward;
    }

    [Serializable]
    public class Cc10CargoRecord
    {
        public string cargoId = string.Empty;
        public string state = string.Empty;
        public long expiryUtcMs;
        public string receiptId = string.Empty;
    }

    /// <summary>Full authoritative snapshot returned by GetFrontierState.</summary>
    [Serializable]
    public class Cc10Snapshot : Cc10Response
    {
        public Cc10Projection projection = new Cc10Projection();
        public Cc10TavernState tavern = new Cc10TavernState();
        public Cc10MissionRow[] missions = new Cc10MissionRow[0];
        public Cc10MapNode[] nodes = new Cc10MapNode[0];
        public Cc10NpcSpot[] spots = new Cc10NpcSpot[0];
        public Cc10Territory[] territories = new Cc10Territory[0];
        public Cc10ResearchNode[] research = new Cc10ResearchNode[0];
        public Cc10RankingBoard[] rankings = new Cc10RankingBoard[0];
        public Cc10MinigameSession minigame = new Cc10MinigameSession();
        public Cc10CargoRecord[] cargo = new Cc10CargoRecord[0];
        /// <summary>Server emergency-disable generation per system name (Cc10System.ToString()).</summary>
        public string[] disabledSystems = new string[0];
    }
}
