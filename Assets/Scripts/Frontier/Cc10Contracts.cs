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
    /// Superseded gap note: as of BE commit 3c4e5816 (settlement-closure error-code rename) and
    /// the World Map phase model on top of it (27c7070f/b49d1bd6), four server-authored phases,
    /// adjacency-only node expansion, deterministic NPC hotspots, outer-to-central encounter bands,
    /// and the Central Realm's three fixed contest districts ARE real, published server contracts
    /// (MapPhase, MapNodeDto.Phase/EncounterBand/IsContestDistrict/Owned/Expanded,
    /// FrontierSnapshotResult.Phases/ContestDistricts). The older flat 4-node/no-phase assumption
    /// this file carried before is replaced below.
    ///
    /// Remaining GAP: CC10FrontierModule still has no query endpoint returning GuildSnapshotResult
    /// (the per-guild TerritoryDto contribution ledger for Ashen Ridge/Ember Hollow/Iron Quarry) or
    /// a ranking view without a chosen season id - only mutation endpoints exist for those. The
    /// Central Realm contest board IS queryable (via GetFrontierState.ContestDistricts), so
    /// GuildTerritory now has partial query support.
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
        public const string ExpandNode = "ExpandNode";
        public const string ReportCampaignChapterComplete = "ReportCampaignChapterComplete";
        public const string EnrollContestDistrict = "EnrollContestDistrict";
        public const string ResolveContestDistrict = "ResolveContestDistrict";
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
        // Renamed at BE 3c4e5816 (settlement-closure contract): GoldCapReached->GoldCapExceeded,
        // AuthorityGenerationMismatch->AuthorityStale, Conflict/AlreadyCommitted->PascalCase.
        public const string SystemDisabled = "SYSTEM_DISABLED";
        public const string Conflict = "Conflict";
        public const string AlreadyCommitted = "AlreadyCommitted";
        public const string OfflineClaimRejected = "OFFLINE_CLAIM_REJECTED";
        public const string GoldCapExceeded = "GoldCapExceeded";
        public const string AuthorityStale = "AuthorityStale";
        // New with the World Map phase model (27c7070f/b49d1bd6):
        public const string PhaseLocked = "PHASE_LOCKED";
        public const string NotAdjacent = "NOT_ADJACENT";
        public const string NotEligible = "NOT_ELIGIBLE";
        public const string DistrictTaken = "DISTRICT_TAKEN";
        public const string AlreadyEnrolledElsewhere = "ALREADY_ENROLLED_ELSEWHERE";
        public const string WindowClosed = "WINDOW_CLOSED";
    }

    /// <summary>Four server-authored World Map phases (numeric order IS unlock order). Mirrors
    /// MapPhase in the published contract.</summary>
    public static class Cc10MapPhase
    {
        public const string HomeOutpost = "HomeOutpost";
        public const string OuterMarches = "OuterMarches";
        public const string InnerReach = "InnerReach";
        public const string CentralRealm = "CentralRealm";

        private static readonly string[] Order = { HomeOutpost, OuterMarches, InnerReach, CentralRealm };
        public static int Rank(string phase) => Array.IndexOf(Order, phase);
    }

    public static class Cc10ContestStatus
    {
        public const string NotEnrolled = "NotEnrolled";
        public const string Eligible = "Eligible";
        public const string Enrolled = "Enrolled";
        public const string Active = "Active";
        public const string Resolving = "Resolving";
        public const string Owned = "Owned";
        public const string Disabled = "Disabled";
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
        public string phase; // MapPhase after ReportCampaignChapterComplete, else null
        public Cc10ContestDistrictDto contestDistrict;
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
        public string phase = Cc10MapPhase.HomeOutpost;
        public Cc10PhaseDto[] phases = Array.Empty<Cc10PhaseDto>();
        public Cc10ContestDistrictDto[] contestDistricts = Array.Empty<Cc10ContestDistrictDto>();

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

    /// <summary>Discovered/Owned/Expanded are independent booleans, per the server's own doc
    /// comment: the true state machine is Locked/Available/Owned/Expanded, collapsed to booleans
    /// because Available and Discovered are the same concept in this beta's node model.
    /// PhaseUnlocked=false means the node's own MapPhase has not been reached yet (server rule,
    /// gates ExpandNode on top of the adjacency check) - the client never computes this itself.</summary>
    [Serializable]
    public class Cc10MapNodeDto
    {
        public string nodeId = string.Empty;
        public string regionId = string.Empty;
        public string phase = Cc10MapPhase.HomeOutpost;
        public string encounterBand = string.Empty;
        public bool isContestDistrict;
        public bool discovered;
        public long discoveredUtcMs;
        public bool phaseUnlocked;
        public bool available;
        public bool owned;
        public long ownedUtcMs;
        public bool expanded;
        public string[] neighbors = Array.Empty<string>();
    }

    /// <summary>One of the four server-authored phases and whether THIS player has unlocked it
    /// (Tavern level + reported Campaign chapter, both server-evaluated).</summary>
    [Serializable]
    public class Cc10PhaseDto
    {
        public string phase = Cc10MapPhase.HomeOutpost;
        public bool unlocked;
        public int requiredTavernLevel;
        public int requiredCampaignChapter;
    }

    /// <summary>One of the Central Realm's three fixed contest districts.</summary>
    [Serializable]
    public class Cc10ContestDistrictDto
    {
        public string districtId = string.Empty;
        public string status = Cc10ContestStatus.NotEnrolled;
        public string enrolledGuildPseudonym;
        public string ownerGuildPseudonym;
        /// <summary>True only when the caller's own guild is presently a top-3 season guild.</summary>
        public bool callerGuildEligible;
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
