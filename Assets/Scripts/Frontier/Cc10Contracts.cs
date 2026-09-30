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
    /// Superseded again at BE 5ca919f6 (continues from b49d1bd6): a real GetGuildState endpoint now
    /// returns GuildSnapshotResult (this guild's own territory contribution/ownership rows joined
    /// from the shared board, guild research, membership epoch/count) - the per-guild
    /// TerritoryDto gap is closed. The research catalog is now the real approved BS rows
    /// (IND_*/GUILD_* ids, phase/top-three-guild gates), minigame gained Verified/Claimed/
    /// Abandoned states plus dedicated ClaimMinigameResult/AbandonMinigameSession endpoints, and
    /// FrontierSnapshotResult additionally carries ThreatBands (read-only encounter-band catalog
    /// data for Battle's own encounter binding - never a client combat rule) and TavernCatalog
    /// (the full 1-10 level track, not just the current level).
    ///
    /// Remaining GAP: GetRankingView still needs a chosen season id the shell has no source for -
    /// Individual/Guild Rankings still render "Not available yet" until a season picker exists.
    ///
    /// Superseded again at BE 253834ab (continues from 5ca919f6): the approved 10-pool NPC catalog
    /// (MissionDto.NpcPool, FrontierSnapshotResult.NpcPools - deterministic pick from the mission's
    /// own EncounterSeed, never client-chosen); MinigameStatus expanded to the packet's full 9-state
    /// list (Available/Started/Active/Submitted/Verified/Claimed/Failed/Expired/Abandoned -
    /// Available is client-inferred when no session exists, Started is what a real session begins
    /// in); and CancelGuildResearch (mirrors CancelResearch's now-genuine refund - Cancel credits
    /// back the exact Start Gold/Materials via the receipt, it no longer forfeits the reserved cost).
    ///
    /// Superseded again at BE 4deee700 (continues from d99b6c6d, which itself resolved the "Tavern
    /// active mission-slot rows" gap flagged above): TavernDto.ActiveMissionSlots is now real -
    /// server-derived from the caller's own Tavern level every command/read, never client-set. This
    /// client renders it as-is and never re-implements the packet's per-level cap table itself; the
    /// server alone enforces it (rejecting AssignMission with the new MISSION_SLOTS_FULL). d99b6c6d
    /// also corrected the Vein Convoy mission's target node/threat-band/intercept-rate to genuinely
    /// agree on the Central band (30% intercept) - a server-side row fix with no client DTO change
    /// (MissionDto/CargoDto/ThreatBandDto shapes are unchanged; this client already displays
    /// whatever band/intercept the server reports, never its own copy of the node graph's phase).
    /// 4deee700 adds BattleAttestation.ReplayTranscriptBlob (the actual serialized replay transcript
    /// for the new headless-replay verifier) - built and populated by the Battle/CR adapter, not
    /// this shell; kept on Cc10BattleAttestation for wire-shape completeness only.
    ///
    /// Superseded again at BE 6e93d10d ("Vein Relay" minigame packet, continues from a5e17638): the
    /// score/proof-hash minigame is replaced entirely by a real, fully server-authoritative
    /// minigame. The client never submits a score or a verdict - it submits SessionId + exactly 12
    /// MinigameActionDto entries (RoundIndex/SelectedLane/ClientTick, ClientTick informational
    /// only) + a client-claimed TranscriptHash; the server independently replays the stream against
    /// its own deterministic target sequence and computes the score itself (correctSelections x
    /// 100), and recomputes the canonical hash to catch a tampered stream (MINIGAME_INVALID_STREAM).
    /// CORRECTION to the prior pass's MinigameStatus doc comment: Available and Active are BOTH
    /// real server statuses in this packet (CreateMinigameSession sets Available; the new
    /// StartMinigameSession call - not Create - transitions Available -&gt; Active and starts the
    /// real 30-second deadline). Started/Submitted are declared in the enum for wire compatibility
    /// but the server never actually produces either value in this pass.
    ///
    /// SUPERSEDED at BE bab7aab1 (approved private per-player occupancy decision, 2026-09-28):
    /// every per-node occupant/display-id field, the caller display id, raw guild pseudonyms and
    /// integer guild color keys are REMOVED from the client contract. Beta supports per-player ExpandNode territory
    /// only: no base placement, wells, relocation/teleport or cross-player occupancy.
    /// GetWorldMapSnapshot (pure read) returns the canonical Cc10WorldMapSnapshotDto. Season
    /// gating: contest rows appear only after Central unlock during an active season (the latest
    /// non-Archived season); GuildOwned counts only for that season, so prior colors disappear on
    /// rollover; [AMENDED at BE bef39415: display season = newest Accepting, else newest
    /// Frozen/Published; only SETTLED districts are GuildOwned with a season-unique token, an enrolled one
    /// is still Unclaimed/null]; GuildTerritory emergency-disable hides all contest rows while own nodes stay
    /// readable. MapNodeDto.LayoutX/LayoutY are unchanged.
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
        public const string GetGuildState = "GetGuildState";
        public const string GetCallerGuildIdentity = "GetCallerGuildIdentity";
        public const string GetWorldMapSnapshot = "GetWorldMapSnapshot";
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
        public const string CancelGuildResearch = "CancelGuildResearch";
        public const string ContributeTerritory = "ContributeTerritory";
        public const string SettleTerritoryWindow = "SettleTerritoryWindow";
        public const string GetRankingView = "GetRankingView";
        public const string ProjectRankingEvents = "ProjectRankingEvents";
        public const string CreateMinigameSession = "CreateMinigameSession";
        public const string StartMinigameSession = "StartMinigameSession";
        public const string SubmitMinigameResult = "SubmitMinigameResult";
        public const string ClaimMinigameResult = "ClaimMinigameResult";
        public const string AbandonMinigameSession = "AbandonMinigameSession";
        // BE-CC11-005 (0b822dcd): real, registered CloudCodeFunctions - not seams anymore.
        public const string GetWorldMapBaseSnapshot = "GetWorldMapBaseSnapshot";
        public const string PlaceWorldMapBase = "PlaceWorldMapBase";
        public const string RelocateWorldMapBase = "RelocateWorldMapBase";
        public const string GetCargoSelectionCatalog = "GetCargoSelectionCatalog";
        public const string AcceptCargoParticipants = "AcceptCargoParticipants";
        public const string GetGuildHallManagement = "GetGuildHallManagement";
        public const string GetRankingSeasonSource = "GetRankingSeasonSource";
    }

    public static class Cc10Errors
    {
        // Mirrors CC10Errors in the published contract - only the ones the client branches on.
        // GetCallerGuildIdentity fail-closed cases (BE ec4b49b3):
        public const string AuthenticationRequired = "AUTHENTICATION_REQUIRED";
        public const string AuthorityUnavailable = "AUTHORITY_UNAVAILABLE";
        public const string StorageUnavailable = "STORAGE_UNAVAILABLE";
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
        // Central contest color allocation (BE 7718db3): season-scoped, one-to-one tokens.
        public const string ColorTokenCollision = "COLOR_TOKEN_COLLISION";
        public const string ColorPaletteExhausted = "COLOR_PALETTE_EXHAUSTED";
        // New with the reconciled catalog (5ca919f6):
        public const string MinigameCoolingDown = "MINIGAME_COOLING_DOWN";
        public const string PrerequisiteNotMet = "PREREQUISITE_NOT_MET";
        // New with Tavern active-mission slots (d99b6c6d):
        public const string MissionSlotsFull = "MISSION_SLOTS_FULL";
        // New with the Vein Relay minigame packet (6e93d10d):
        public const string MinigameInvalidStream = "MINIGAME_INVALID_STREAM";
        // BE-CC11-005 (0b822dcd): World Map base placement/relocation and Cargo participant lock.
        public const string InvalidRequest = "INVALID_REQUEST";
        public const string InvalidState = "INVALID_STATE";
        public const string RateLimited = "RATE_LIMITED"; // relocation cooldown not elapsed yet
        public const string AlreadyOwned = "ALREADY_OWNED";
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
        /// <summary>Read-only catalog data (one row per phase) for Battle's encounter binding -
        /// never a client combat rule.</summary>
        public Cc10ThreatBandDto[] threatBands = Array.Empty<Cc10ThreatBandDto>();
        /// <summary>The full 1-10 Tavern cost/duration track, for rendering the whole ladder, not
        /// just the current level/active project.</summary>
        public Cc10TavernLevelRowDto[] tavernCatalog = Array.Empty<Cc10TavernLevelRowDto>();
        /// <summary>The full approved 10-pool NPC catalog, for rendering pool flavor without
        /// waiting on a mission assignment.</summary>
        public Cc10NpcPoolDto[] npcPools = Array.Empty<Cc10NpcPoolDto>();

        public bool IsSystemDisabled(string systemId) => Array.IndexOf(disabledSystems, systemId) >= 0;
    }

    /// <summary>GetGuildState's result: this guild's own territory contribution/ownership rows
    /// (joined server-side from the shared cross-guild board - never another guild's), guild
    /// research state, and membership epoch/count.</summary>
    [Serializable]
    public sealed class Cc10GuildSnapshot : Cc10ResultBase
    {
        public string[] disabledSystems = Array.Empty<string>();
        public Cc10TerritoryDto[] territories = Array.Empty<Cc10TerritoryDto>();
        public Cc10ResearchDto[] research = Array.Empty<Cc10ResearchDto>();
        public long membershipEpoch;
        public int memberCount;
    }

    [Serializable]
    public sealed class Cc10RankingResult : Cc10ResultBase
    {
        public Cc10RankingViewDto view;
    }

    /// <summary>GetCallerGuildIdentity's result (BE ec4b49b3, zero-arg): the server-owned answer to
    /// "what guild is the caller currently in". The client never supplies, hashes, or derives a
    /// guild id or pseudonym - this is the ONLY source for one. BE re-verifies live against the
    /// guild's own document before ever reporting a membership, so a revoked, expired, or otherwise
    /// stale entry can never come back as current: hasGuild=false, guildId=null IS the valid
    /// no-guild case (never-joined, left, revoked or expired all look identical here, by design -
    /// this DTO carries no reason code to distinguish them), and success is still true for it.
    /// success=false (AUTHENTICATION_REQUIRED/AUTHORITY_UNAVAILABLE/STORAGE_UNAVAILABLE/transport)
    /// is the only case this client must fail closed on - never treat a failed call as "no guild".</summary>
    [Serializable]
    public sealed class Cc10GuildIdentityResult : Cc10ResultBase
    {
        public bool hasGuild;
        /// <summary>The caller's OWN current guild id, plaintext (it is the caller's own
        /// membership, never another player's data), or null/empty when hasGuild is false.</summary>
        public string guildId;
        public long membershipEpoch;
        public long membershipExpiresUtcMs;
    }

    // ---- CC11 contract (BE 7ac011a1 seams, wired live at BE-CC11-005 / 0b822dcd) -----------
    // WH-CC11-001 added these as DTO-only mirrors (7ac011a1 registered no endpoint for any of
    // them). BE-CC11-005 (0b822dcd) now registers real CloudCodeFunctions for all seven -
    // GetWorldMapBaseSnapshot/PlaceWorldMapBase/RelocateWorldMapBase/GetCargoSelectionCatalog/
    // AcceptCargoParticipants/GetGuildHallManagement/GetRankingSeasonSource - see
    // Cc10FrontierClient for the gateway methods. formationId was added to
    // CargoParticipantSelectionDto/Request at 0b822dcd; mirrored below.
    // Note also: docs/AI_CONTRIBUTING and this file's own bab7aab1 note record Beta as
    // ExpandNode-only territory - base PLACEMENT itself (choosing/relocating a base node) is a
    // separate mechanic from ExpandNode's per-player node ownership and is still not surfaced
    // in any Beta UI; this pass wires the real, published gateway contract only, per task scope
    // ("Do not invent ... UI").

    public static class Cc10WorldMapBaseOperation
    {
        public const string None = "None";
        public const string Place = "Place";
        public const string Relocate = "Relocate";
    }

    public static class Cc10WorldMapBaseStatus
    {
        public const string Unplaced = "Unplaced";
        public const string Placed = "Placed";
        public const string RelocationPending = "RelocationPending";
        public const string Relocated = "Relocated";
    }

    [Serializable]
    public class Cc10WorldMapBasePlacementDto
    {
        public string status = string.Empty;
        public string operation = string.Empty;
        public string baseNodeId;
        public string pendingNodeId;
        public long changedUtcMs;
    }

    [Serializable]
    public class Cc10CargoParticipantSelectionDto
    {
        public string avatarId;
        public string armyId;
        public string missionId;
        public string formationId;
        public long selectedUtcMs;
    }

    [Serializable]
    public class Cc10CargoParticipantSelectionState
    {
        public Cc10CargoParticipantSelectionDto selection;
        public long stateVersion;
    }

    [Serializable]
    public class Cc10GuildMemberDto
    {
        public string memberId = string.Empty;
        public string displayName = string.Empty;
        public string positionId;
        public long joinedUtcMs;
        public bool online;
    }

    [Serializable]
    public class Cc10GuildMemberSearchDto
    {
        public string query = string.Empty;
        public Cc10GuildMemberDto[] members = Array.Empty<Cc10GuildMemberDto>();
        public string nextCursor;
    }

    [Serializable]
    public class Cc10GuildStoreItemDto
    {
        public string itemId = string.Empty;
        public string name = string.Empty;
        public bool available;
    }

    [Serializable]
    public class Cc10GuildOfficeDto
    {
        public string officeId = string.Empty;
        public string positionId;
        public string holderMemberId;
    }

    [Serializable]
    public class Cc10GuildPositionDto
    {
        public string positionId = string.Empty;
        public string name = string.Empty;
        public string[] permissions = Array.Empty<string>();
    }

    /// <summary>status is Cc10ResearchStatus (InProgress/Completed) - reuses the same vocabulary
    /// as individual/guild research, not a new status set.</summary>
    [Serializable]
    public class Cc10GuildResearchStateDto
    {
        public string nodeId = string.Empty;
        public string status = string.Empty;
        public int progress;
        public int required;
        public long readyUtcMs;
    }

    [Serializable]
    public class Cc10GuildAnnouncementDto
    {
        public string announcementId = string.Empty;
        public string authorMemberId = string.Empty;
        public string body = string.Empty;
        public long publishedUtcMs;
    }

    [Serializable]
    public class Cc10GuildManagementSnapshotDto
    {
        public Cc10GuildMemberDto[] members = Array.Empty<Cc10GuildMemberDto>();
        public Cc10GuildStoreItemDto[] store = Array.Empty<Cc10GuildStoreItemDto>();
        public Cc10GuildOfficeDto[] offices = Array.Empty<Cc10GuildOfficeDto>();
        public Cc10GuildPositionDto[] positions = Array.Empty<Cc10GuildPositionDto>();
        public Cc10GuildResearchStateDto[] research = Array.Empty<Cc10GuildResearchStateDto>();
        public Cc10GuildAnnouncementDto[] announcements = Array.Empty<Cc10GuildAnnouncementDto>();
    }

    /// <summary>state is Cc10SeasonState (Created/Accepting/Frozen/Published/Archived) - the same
    /// vocabulary Cc10RankingViewDto.state already uses, not a new status set.</summary>
    [Serializable]
    public class Cc10RankingSeasonSourceDto
    {
        public string source = string.Empty;
        public string seasonId = string.Empty;
        public string state = string.Empty;
        public long createdUtcMs;
        public long serverUtcMs;
    }

    [Serializable]
    public sealed class Cc10RankingSeasonSourceResult : Cc10ResultBase
    {
        public Cc10RankingSeasonSourceDto season;
    }

    /// <summary>GetWorldMapBaseSnapshot's result (BE-CC11-005). cells is the server-owned list of
    /// placement-eligible node ids (own-phase, non-start, non-contest-district nodes) - this
    /// client never computes eligible cells itself. mapVersion/occupancyVersion are the CAS
    /// tokens PlaceWorldMapBase/RelocateWorldMapBase must echo back.</summary>
    [Serializable]
    public sealed class Cc10WorldMapBaseSnapshotResult : Cc10ResultBase
    {
        public Cc10WorldMapBasePlacementDto basePlacement;
        public string[] cells = Array.Empty<string>();
        public string mapVersion = string.Empty;
        public int occupancyVersion;
    }

    [Serializable]
    public sealed class Cc10WorldMapBasePlacementResult : Cc10ResultBase
    {
        public Cc10WorldMapBasePlacementDto basePlacement;
    }

    /// <summary>GetCargoSelectionCatalog's result (BE-CC11-005): the server-owned eligible
    /// mission/avatar/formation lists for cargo participant selection - avatars/formations
    /// already locked into another cargo's selection are excluded server-side; this client
    /// never computes eligibility itself.</summary>
    [Serializable]
    public sealed class Cc10CargoSelectionCatalogResult : Cc10ResultBase
    {
        public Cc10MissionDto[] missions = Array.Empty<Cc10MissionDto>();
        public string[] avatars = Array.Empty<string>();
        public string[] formations = Array.Empty<string>();
    }

    [Serializable]
    public sealed class Cc10CargoParticipantSelectionResult : Cc10ResultBase
    {
        public Cc10CargoParticipantSelectionDto selection;
    }

    [Serializable]
    public sealed class Cc10GuildManagementResult : Cc10ResultBase
    {
        public Cc10GuildManagementSnapshotDto guild;
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
        /// <summary>Server-derived, never client-set: the caller's current per-level active-mission
        /// cap (recomputed from the real Tavern level on every command/read). Display only - the
        /// server alone enforces it, rejecting AssignMission with MISSION_SLOTS_FULL.</summary>
        public int activeMissionSlots;
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
        /// <summary>Additive, never null in a real response: the approved threat band this
        /// mission's encounter is bound to. Display/Battle-binding only - never a client combat
        /// rule or a client-computed multiplier.</summary>
        public Cc10ThreatBandDto threatBand;
        /// <summary>Additive, never null in a real response: the named NPC pool this mission's
        /// encounter deterministically draws from (a pure function of the mission's own
        /// EncounterSeed) - display/Battle-binding only, never client-chosen or client-computed.</summary>
        public Cc10NpcPoolDto npcPool;
    }

    /// <summary>Read-only catalog data for one approved named NPC pool. Shares its band's
    /// Cc10ThreatBandDto HP/damage/reward multiplier and AiDifficultyTier exactly - a pool adds
    /// only encounter flavor (name/type/archetype) and deck lane composition.</summary>
    [Serializable]
    public class Cc10NpcPoolDto
    {
        public string poolName = string.Empty;
        public string band = Cc10MapPhase.HomeOutpost;
        public string encounterType = string.Empty;
        public string archetype = string.Empty;
        public int laneFront;
        public int laneMiddle;
        public int laneBack;
    }

    /// <summary>Read-only World Map threat-band catalog row. RewardMultiplier is always 1.0 - it
    /// changes no existing Gold/Materials contract; HP/Damage multipliers feed Battle's own
    /// encounter binding, never resolved by this client.</summary>
    [Serializable]
    public class Cc10ThreatBandDto
    {
        public string phase = Cc10MapPhase.HomeOutpost;
        public string bandName = string.Empty;
        public double hpMultiplier;
        public double damageMultiplier;
        public double rewardMultiplier;
        public string aiDifficultyTier = string.Empty;
    }

    /// <summary>One row of the read-only 1-10 Tavern cost/duration track.</summary>
    [Serializable]
    public class Cc10TavernLevelRowDto
    {
        public int fromLevel;
        public int toLevel;
        public int goldCost;
        public int materialsCost;
        public long durationMs;
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
        /// <summary>Server-authoritative, hand-authored layout key - never computed or invented
        /// client-side. The only source of a node's on-screen position; identical for every
        /// player and every read.</summary>
        public int layoutX;
        public int layoutY;
        // Occupancy is PRIVATE per player (BE bab7aab1): `owned` is the caller's own state only. There is
        // deliberately NO occupant/display-id field of any kind on this DTO and no per-node guild data.
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

    /// <summary>One of the Central Realm's three fixed contest districts (enroll/resolve command
    /// results only - the legacy GetFrontierState.contestDistricts list is intentionally not
    /// modelled: BE never populates it). Raw guild pseudonyms and integer color keys
    /// were removed from every read DTO (BE bab7aab1): the only guild-identifying data returned is
    /// an opaque presentation color TOKEN ("GC01".."GC12") that carries no guild id, name or
    /// member list.</summary>
    [Serializable]
    public class Cc10ContestDistrictDto
    {
        public string districtId = string.Empty;
        public string status = Cc10ContestStatus.NotEnrolled;
        /// <summary>True only when the caller's own guild is presently a top-3 season guild.</summary>
        public bool callerGuildEligible;
        /// <summary>Opaque color token for the enrolled guild; null when none is enrolled.</summary>
        public string enrolledGuildColorToken;
        /// <summary>Opaque color token for the resolved owner; null when unowned.</summary>
        public string ownerGuildColorToken;
    }

    // ---- canonical privacy-safe World Map snapshot (GetWorldMapSnapshot, BE bab7aab1) ----------

    /// <summary>Snapshot schema id the server stamps; a client must refuse an unknown schema.</summary>
    public static class Cc10WorldMapSnapshotSchema
    {
        public const string V1 = "cc10.worldmap.v1";
    }

    /// <summary>Phase tokens used inside the snapshot: Tutorial, Outer, Inner, Central (these are the
    /// band names, NOT the MapPhase names used elsewhere - HomeOutpost=Tutorial, OuterMarches=Outer,
    /// InnerReach=Inner, CentralRealm=Central).</summary>
    public static class Cc10WorldMapPhaseToken
    {
        public const string Tutorial = "Tutorial";
        public const string Outer = "Outer";
        public const string Inner = "Inner";
        public const string Central = "Central";

        private static readonly string[] Order = { Tutorial, Outer, Inner, Central };
        public static int Rank(string token) => Array.IndexOf(Order, token);
    }

    /// <summary>ownershipState values a client must distinguish (BE 7302a996): Unclaimed (open, or
    /// enrolled-but-unsettled - token null), GuildOwned (settled to a guild - the ONLY state with a
    /// token), ExplicitlyUnowned (an operator-recorded no-owner result - token null, never a color, not
    /// open). "Enrolled" is BS's Option A value; BE does NOT emit it (open BS/BE conflict, MS E1) and it is
    /// handled only defensively. Any other value is unknown and renders neutral with no action.</summary>
    public static class Cc10ContestOwnershipState
    {
        public const string Unclaimed = "Unclaimed";
        public const string Enrolled = "Enrolled";
        public const string GuildOwned = "GuildOwned";
        public const string ExplicitlyUnowned = "ExplicitlyUnowned";

        public static bool IsKnown(string state) =>
            state == Unclaimed || state == Enrolled || state == GuildOwned || state == ExplicitlyUnowned;
    }

    [Serializable]
    public class Cc10WorldMapOwnNodeDto
    {
        public string nodeId = string.Empty;
        /// <summary>Tutorial | Outer | Inner | Central.</summary>
        public string phaseId = string.Empty;
    }

    [Serializable]
    public class Cc10WorldMapContestDto
    {
        public string districtId = string.Empty;
        /// <summary>Per-row season id (there is deliberately no top-level seasonId).</summary>
        public string seasonId = string.Empty;
        /// <summary>Unclaimed | GuildOwned | ExplicitlyUnowned.</summary>
        public string ownershipState = Cc10ContestOwnershipState.Unclaimed;
        /// <summary>Presentation token only ("GC01".."GC12"); null unless GuildOwned. Never a guild
        /// id, name or member list. A client ignores it for any other state.</summary>
        public string guildColorToken;
    }

    /// <summary>FINAL Central contest lifecycle (BE bef39415), as it appears in centralContest:
    /// display season = newest Accepting, else newest Frozen/Published (Created/Archived expose nothing).
    /// Enrolling while Accepting confers NO ownership and NO color - the row stays Unclaimed, token null.
    /// ResolveContestDistrict (operator-gated, once per district, only while Frozen; a second call is
    /// AlreadyCommitted) settles it: GuildOwned + the guild's season-unique token. Settled districts are
    /// immutable and stay visible through Frozen/Published, vanishing at Archived. Rejections:
    /// COLOR_PALETTE_EXHAUSTED (no free color) and COLOR_TOKEN_COLLISION (non-unique/missing allocation),
    /// both atomic. The snapshot carries no season state, so an Unclaimed row in Frozen/Published is
    /// indistinguishable from an open one: enrollment there is rejected WINDOW_CLOSED by the server.
    /// Exactly the approved snapshot shape - nothing more. There is deliberately NO top-level seasonId /
    /// season-epoch field: BE has not confirmed one belongs in the DTO (MS finding B1), so none is modelled;
    /// season changes are detected client-side from the per-row seasonId values (see
    /// Cc10FrontierClient.RefreshWorldMapAsync). serverUtc is epoch
    /// milliseconds from the server clock (display only on the client).</summary>
    [Serializable]
    public class Cc10WorldMapSnapshotDto
    {
        public string schemaVersion = Cc10WorldMapSnapshotSchema.V1;
        public string mapVersion = string.Empty;
        public long serverUtc;
        public string unlockedPhase = Cc10WorldMapPhaseToken.Tutorial;
        /// <summary>Per-player monotonic counter, +1 only on a successful ExpandNode.</summary>
        public int occupancyVersion;
        /// <summary>Caller's OWN occupied nodes only, sorted by phase then ordinal nodeId.</summary>
        public Cc10WorldMapOwnNodeDto[] ownOccupiedNodes = Array.Empty<Cc10WorldMapOwnNodeDto>();
        /// <summary>Central rows appear only after Central unlock during an active season; sorted by
        /// ordinal districtId. Empty otherwise.</summary>
        public Cc10WorldMapContestDto[] centralContest = Array.Empty<Cc10WorldMapContestDto>();
    }

    [Serializable]
    public sealed class Cc10WorldMapSnapshotResult : Cc10ResultBase
    {
        public Cc10WorldMapSnapshotDto snapshot;
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

    /// <summary>Vein Relay's real lifecycle (BE 6e93d10d): CreateMinigameSession sets Available
    /// (real, no cost); StartMinigameSession - a distinct, explicit call, NOT Create - transitions
    /// Available -&gt; Active and starts the real 30-second deadline (ExpiresUtcMs from
    /// StartedUtcMs, not from CreatedUtcMs - a created-but-not-started session never expires);
    /// SubmitMinigameResult moves Active straight to Verified or Failed (no observable Submitted
    /// state is ever produced); Verified -&gt; Claimed via the separate, receipt-protected
    /// ClaimMinigameResult (a verified result is not itself a claim). Started/Submitted remain in
    /// this enum only for wire compatibility with the request/result shape - the server never
    /// actually assigns either as a session's Status in this pass.</summary>
    public static class Cc10MinigameStatus
    {
        public const string Available = "Available";
        public const string Started = "Started"; // declared for wire compatibility; server never produces this
        public const string Active = "Active";
        public const string Submitted = "Submitted"; // declared for wire compatibility; server never produces this
        public const string Verified = "Verified";
        public const string Claimed = "Claimed";
        public const string Failed = "Failed";
        public const string Expired = "Expired";
        public const string Abandoned = "Abandoned";

        public static bool IsPlayable(string status) => status == Active || status == Started;

        public static bool IsTerminal(string status) =>
            status == Claimed || status == Failed || status == Expired || status == Abandoned;
    }

    /// <summary>0=Front, 1=Middle, 2=Back - Vein Relay's lane vocabulary (mirrors Battle's existing
    /// three-lane concept without resolving cards/damage/Energy).</summary>
    public static class Cc10MinigameLane
    {
        public const string Front = "Front";
        public const string Middle = "Middle";
        public const string Back = "Back";
    }

    /// <summary>One accepted Vein Relay round input. ClientTick is informational ONLY - the
    /// server's own receipt tick (its authoritative NowMs) is the real UTC authority; this field is
    /// never read by any server rule and never sent as, or treated as, a score or verdict.</summary>
    [Serializable]
    public class Cc10MinigameActionDto
    {
        public int roundIndex;
        public string selectedLane = Cc10MinigameLane.Front;
        public long clientTick;
    }

    [Serializable]
    public class Cc10MinigameSessionDto
    {
        public string sessionId = string.Empty;
        public string seed = string.Empty;
        /// <summary>Server-issued at creation; the target-sequence formula
        /// (HMAC-SHA256(seed, rulesetVersion+sessionId+round) mod 3) needs it, and a client
        /// re-deriving pulses for display must use the SAME version the server verifies against.</summary>
        public string rulesetVersion = string.Empty;
        public string status = string.Empty;
        public long createdUtcMs;
        public long startedUtcMs;
        /// <summary>The 30-second deadline from startedUtcMs - 0/unset while still Available.</summary>
        public long expiresUtcMs;
        /// <summary>Final score (correctSelections x 100, 0-1200) once Verified - server-computed
        /// only, never client-submitted.</summary>
        public int? verifiedScore;
        public int? correctSelections;
        public string submittedTranscriptHash;
        public string startReceiptId;
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

    /// <summary>Minigame is its OWN ranking track, separate from Individual - Vein Relay's own
    /// ranking rule ("if a later verified score is higher, it atomically replaces the player's
    /// prior minigame score") is max-replace, not the additive per-claim policy Individual uses.</summary>
    public static class Cc10RankingScope
    {
        public const string Individual = "Individual";
        public const string Guild = "Guild";
        public const string Minigame = "Minigame";
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
        /// <summary>The actual serialized replay transcript the headless replay verifier re-hashes
        /// and forwards - built by the Battle/CR attestation adapter, not this shell. Present here
        /// only for wire-shape completeness; WH never populates or reads it.</summary>
        public string replayTranscriptBlob;
        public string outcome = string.Empty; // "Victory" | "Loss"
    }
}
