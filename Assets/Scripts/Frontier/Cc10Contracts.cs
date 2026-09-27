using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Frontier
{
    /// <summary>
    /// CC10 client DTOs, adapted to the real, published server contract:
    /// CloudCode/CC10Frontier/CC10Contracts.cs (MyriadOfDragons.CloudCode.CC10Frontier), module
    /// MyriadOfDragons.CloudCode.CC10Frontier.CC10FrontierModule. Field names are camelCase copies
    /// of the server's C# properties (existing precedent: GuildExpeditionGateway's
    /// success/remaining/errorCode result types use the same casing over CloudCodeService).
    /// A "namespace"-named receipt field is intentionally dropped - Unity's JsonUtility field
    /// binding cannot rename a reserved word and the client does not need it.
    ///
    /// KNOWN GAP (blocker, not invented here): the published contract has no concept of map
    /// "phases", outer/central "threat bands", or a scoped "three-guild" contest - TerritoryDto is
    /// a flat per-territory pseudonym-owned record and MapNodeDto/CC10RowSet.Nodes is a plain
    /// adjacency graph (currently 4 nodes). Those concepts do not exist server-side; this client
    /// does not author them.
    /// </summary>
    public static class Cc10SystemId
    {
        public const string Tavern = "Tavern";
        public const string Missions = "Missions";
        public const string WorldMap = "WorldMap";
        public const string NpcSpots = "NpcSpots";
        public const string GuildTerritory = "GuildTerritory";
        public const string IndividualResearch = "IndividualResearch";
        public const string GuildResearch = "GuildResearch";
        public const string IndividualRankings = "IndividualRankings";
        public const string GuildRankings = "GuildRankings";
        public const string Minigame = "Minigame";
        public const string Cargo = "Cargo";
    }

    public static class Cc10Endpoints
    {
        public const string ModuleName = "CC10Frontier";
        public const string GetServerUtc = "GetServerUtc";
        public const string GetFrontierState = "GetFrontierState";
        public const string StartTavernUpgrade = "StartTavernUpgrade";
        public const string CompleteTavernUpgrade = "CompleteTavernUpgrade";
        public const string ReportExpeditionGold = "ReportExpeditionGold";
        public const string DiscoverNode = "DiscoverNode";
        public const string AssignMission = "AssignMission";
        public const string SubmitMissionResult = "SubmitMissionResult";
        public const string ClaimMission = "ClaimMission";
        public const string AbandonMission = "AbandonMission";
        public const string ScoutCargo = "ScoutCargo";
        public const string DispatchCargo = "DispatchCargo";
        public const string DeliverCargo = "DeliverCargo";
        public const string AbandonCargo = "AbandonCargo";
        public const string StartResearch = "StartResearch";
        public const string CompleteResearch = "CompleteResearch";
        public const string CancelResearch = "CancelResearch";
        public const string InstallGuildMembership = "InstallGuildMembership";
        public const string StartGuildResearch = "StartGuildResearch";
        public const string ContributeGuildResearch = "ContributeGuildResearch";
        public const string CompleteGuildResearch = "CompleteGuildResearch";
        public const string ContributeTerritory = "ContributeTerritory";
        public const string SettleTerritoryWindow = "SettleTerritoryWindow";
        public const string GetRankingView = "GetRankingView";
        public const string ProjectRankingEvents = "ProjectRankingEvents";
        public const string CreateMinigameSession = "CreateMinigameSession";
        public const string SubmitMinigameResult = "SubmitMinigameResult";

        /// <summary>GAP: CC10FrontierModule has no query endpoint returning GuildSnapshotResult
        /// (territories / guild research). Only mutation endpoints exist today.</summary>
        public const string NoGuildStateQueryEndpoint = null;
    }

    public static class Cc10Errors
    {
        // Mirrors CC10Errors in the published contract - only the ones the client branches on.
        public const string SystemDisabled = "SYSTEM_DISABLED";
        public const string Conflict = "CONFLICT";
        public const string AlreadyCommitted = "ALREADY_COMMITTED";
        public const string OfflineClaimRejected = "OFFLINE_CLAIM_REJECTED";
        public const string GoldCapReached = "GOLD_CAP_REACHED";
        public const string AuthorityGenerationMismatch = "AUTHORITY_GENERATION_MISMATCH";
    }

    // ---- results --------------------------------------------------------------------------

    [Serializable]
    public class Cc10ResultBase
    {
        public bool success;
        public string errorCode = string.Empty;
        public long serverUtcMs;
        public string utcDayKey = string.Empty;
        public long authorityGeneration;
        public long stateVersion;
    }

    [Serializable]
    public sealed class Cc10ServerUtcResult : Cc10ResultBase { }

    [Serializable]
    public sealed class Cc10CommandResult : Cc10ResultBase
    {
        public bool replayed;
        public Cc10Receipt receipt;
        public Cc10TavernDto tavern;
        public Cc10MissionDto mission;
        public Cc10MapNodeDto node;
        public Cc10ResearchDto research;
        public Cc10TerritoryDto territory;
        public Cc10CargoDto cargo;
        public Cc10MinigameSessionDto minigame;
        public Cc10GoldLedgerDto gold;
        public int acceptedGold;
        public int projectedEvents;
    }

    [Serializable]
    public sealed class Cc10FrontierSnapshot : Cc10ResultBase
    {
        public string[] disabledSystems = Array.Empty<string>();
        public Cc10TavernDto tavern = new Cc10TavernDto();
        public Cc10MissionDto[] missions = Array.Empty<Cc10MissionDto>();
        public Cc10MapNodeDto[] nodes = Array.Empty<Cc10MapNodeDto>();
        public Cc10NpcSpotDto[] spots = Array.Empty<Cc10NpcSpotDto>();
        public Cc10ResearchDto[] research = Array.Empty<Cc10ResearchDto>();
        public Cc10CargoDto[] cargo = Array.Empty<Cc10CargoDto>();
        public Cc10MinigameSessionDto minigame;
        public Cc10GoldLedgerDto gold = new Cc10GoldLedgerDto();
        public Cc10JournalEntryDto[] journal = Array.Empty<Cc10JournalEntryDto>();

        public bool IsSystemDisabled(string systemId) => Array.IndexOf(disabledSystems, systemId) >= 0;
    }

    [Serializable]
    public sealed class Cc10RankingResult : Cc10ResultBase
    {
        public Cc10RankingViewDto view;
    }

    // ---- DTOs ------------------------------------------------------------------------------

    [Serializable]
    public class Cc10TavernProjectDto
    {
        public int fromLevel;
        public int toLevel;
        public long startedUtcMs;
        public long readyUtcMs;
        public int goldCost;
        public int materialsCost;
        public string receiptId = string.Empty;
    }

    [Serializable]
    public class Cc10TavernDto
    {
        public int level = 1;
        public Cc10TavernProjectDto activeProject;
    }

    /// <summary>MissionStatus per contract: Active, Completed, Claimed, Failed, Abandoned, Expired.</summary>
    public static class Cc10MissionStatus
    {
        public const string Active = "Active";
        public const string Completed = "Completed";
        public const string Claimed = "Claimed";
        public const string Failed = "Failed";
        public const string Abandoned = "Abandoned";
        public const string Expired = "Expired";
    }

    /// <summary>MissionType per contract.</summary>
    public static class Cc10MissionType
    {
        public const string NpcPatrol = "NpcPatrol";
        public const string RelicRescue = "RelicRescue";
        public const string VeinConvoy = "VeinConvoy";
    }

    [Serializable]
    public class Cc10MissionDto
    {
        public string missionId = string.Empty;
        public string type = string.Empty;
        public string status = string.Empty;
        public string spotId = string.Empty;
        public string nodeId = string.Empty;
        public long assignedUtcMs;
        public long readyUtcMs;
        public long expiresUtcMs;
        public string encounterId = string.Empty;
        public string encounterSeed = string.Empty;
        public string deckSnapshotHash = string.Empty;
        public string battleAttemptId;
        public string outcome; // "Victory" | "Loss" | null
        public string cargoId;
        public string claimReceiptId;
    }

    [Serializable]
    public class Cc10MapNodeDto
    {
        public string nodeId = string.Empty;
        public string regionId = string.Empty;
        public bool discovered;
        public long discoveredUtcMs;
        public string[] neighbors = Array.Empty<string>();
    }

    public static class Cc10SpotStatus
    {
        public const string Available = "Available";
        public const string Bound = "Bound";
        public const string Cleared = "Cleared";
        public const string Expired = "Expired";
    }

    [Serializable]
    public class Cc10NpcSpotDto
    {
        public string spotId = string.Empty;
        public string nodeId = string.Empty;
        public string missionType = string.Empty;
        public string dayKey = string.Empty;
        public int slot;
        public string status = string.Empty;
        public string boundMissionId;
    }

    public static class Cc10ResearchScope
    {
        public const string Individual = "Individual";
        public const string Guild = "Guild";
    }

    public static class Cc10ResearchStatus
    {
        public const string InProgress = "InProgress";
        public const string Completed = "Completed";
    }

    [Serializable]
    public class Cc10ResearchDto
    {
        public string nodeId = string.Empty;
        public string scope = string.Empty;
        public string status = string.Empty;
        public long startedUtcMs;
        public long readyUtcMs;
        public long completedUtcMs;
        // Contributions dictionary omitted: JsonUtility cannot deserialize Dictionary<string,int>.
        // Not needed for the read-only guild-research row (see Cc10ViewModels).
    }

    [Serializable]
    public class Cc10TerritoryDto
    {
        public string territoryId = string.Empty;
        public string windowId = string.Empty;
        public long windowStartUtcMs;
        public long windowEndUtcMs;
        public string ownerGuildPseudonym;
        public string[] contributedMembers = Array.Empty<string>();
    }

    /// <summary>CargoStatus per contract: Accepted, Scouting, Active, Delivered, Claimed,
    /// Intercepted, Abandoned, Expired, Failed.</summary>
    public static class Cc10CargoStatus
    {
        public const string Accepted = "Accepted";
        public const string Scouting = "Scouting";
        public const string Active = "Active";
        public const string Delivered = "Delivered";
        public const string Claimed = "Claimed";
        public const string Intercepted = "Intercepted";
        public const string Abandoned = "Abandoned";
        public const string Expired = "Expired";
        public const string Failed = "Failed";
    }

    [Serializable]
    public class Cc10CargoDto
    {
        public string cargoId = string.Empty;
        public string missionId = string.Empty;
        public string status = string.Empty;
        public long acceptedUtcMs;
        public long scoutedUtcMs;
        public long dispatchedUtcMs;
        public long arriveUtcMs;
        public long expiresUtcMs;
        public string targetNodeId = string.Empty;
        public string[] routeNodeIds = Array.Empty<string>();
        public bool encounterRequired;
        public string encounterId = string.Empty;
        public string encounterSeed = string.Empty;
        /// <summary>Temporary custody haul - NOT in the wallet until claimReceiptId settles it.</summary>
        public int haulGold;
        public int haulMaterials;
        public string claimReceiptId;
        public string terminalReason;
    }

    public static class Cc10MinigameStatus
    {
        public const string Created = "Created";
        public const string Submitted = "Submitted";
        public const string Expired = "Expired";
    }

    [Serializable]
    public class Cc10MinigameSessionDto
    {
        public string sessionId = string.Empty;
        public string seed = string.Empty;
        public string status = string.Empty;
        public long createdUtcMs;
        public long expiresUtcMs;
        public int? verifiedScore;
    }

    [Serializable]
    public class Cc10GoldLedgerDto
    {
        public string dayKey = string.Empty;
        public int cc10Gold;
        public int expeditionGold;
        public int cap;
        public int remaining;
    }

    [Serializable]
    public class Cc10JournalEntryDto
    {
        public long serverUtcMs;
        public string kind = string.Empty;
        public string entityId = string.Empty;
        public string receiptId = string.Empty;
    }

    public static class Cc10RankingScope
    {
        public const string Individual = "Individual";
        public const string Guild = "Guild";
    }

    public static class Cc10SeasonState
    {
        public const string Created = "Created";
        public const string Accepting = "Accepting";
        public const string Frozen = "Frozen";
        public const string Published = "Published";
        public const string Archived = "Archived";
    }

    [Serializable]
    public class Cc10RankingEntryDto
    {
        public int rank;
        public string subjectPseudonym = string.Empty;
        public int points;
    }

    [Serializable]
    public class Cc10RankingViewDto
    {
        public string seasonId = string.Empty;
        public string state = string.Empty;
        public string scope = string.Empty;
        public Cc10RankingEntryDto[] entries = Array.Empty<Cc10RankingEntryDto>();
        public Cc10RankingEntryDto you;
    }

    [Serializable]
    public class Cc10Receipt
    {
        public string receiptId = string.Empty;
        public string requestId = string.Empty;
        public string action = string.Empty;
        public string entityId = string.Empty;
        public int settlementVersion = 1;
        public long serverUtcMs;
        public long stateVersionAfter;
        public int goldCredit;
        public int materialsCredit;
        public int goldDebit;
        public int materialsDebit;
        public int staminaDebit;
    }

    /// <summary>Frozen-MatchResult attestation envelope, built by the Battle adapter (CR seat), not
    /// this layer; the client only forwards it opaquely inside AssignMission/DeliverCargo bodies.</summary>
    [Serializable]
    public class Cc10BattleAttestation
    {
        public string missionId = string.Empty;
        public string encounterId = string.Empty;
        public string battleAttemptId = string.Empty;
        public string encounterSeed = string.Empty;
        public string deckSnapshotHash = string.Empty;
        public string rulesetVersion = string.Empty;
        public string matchResultHash = string.Empty;
        public string replayTranscriptHash = string.Empty;
        public string outcome = string.Empty; // "Victory" | "Loss"
    }
}
