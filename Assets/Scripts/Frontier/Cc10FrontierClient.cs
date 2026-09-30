using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyriadOfDragons.Frontier
{
    public enum Cc10Connection { Unknown, Online, Offline }

    /// <summary>Outcome of the most recent GetWorldMapSnapshot attempt (the last good snapshot is
    /// always retained on anything other than Accepted).</summary>
    public enum Cc10WorldMapRefresh { NotAttempted, Accepted, AcceptedAfterReset, SchemaRejected, Malformed, Stale, Offline }

    public enum Cc10Outcome { Applied, Replayed, Conflict, Disabled, Offline, Rejected, InFlight, Failed }

    /// <summary>Outcome of the most recent GetCallerGuildIdentity attempt. Only Accepted ever updates
    /// <see cref="Cc10FrontierClient.GuildId"/>/<see cref="Cc10FrontierClient.GuildIdentity"/> - every
    /// other value fails closed (clears both), including a malformed positive response
    /// (hasGuild=true with an empty guildId, which this client never trusts).</summary>
    public enum Cc10GuildIdentityRefresh { NotAttempted, Accepted, AuthenticationRequired, AuthorityUnavailable, Malformed, Offline }

    /// <summary>Outcome of a pure read against the CC11 (BE-CC11-005) surfaces - World Map base
    /// snapshot, Cargo selection catalog, Guild Hall management, ranking-season source. Failure
    /// keeps the last accepted value (read-only degrade), matching RefreshWorldMapAsync's own
    /// keep-last-good convention - these are not identity-sensitive like GetCallerGuildIdentity.</summary>
    public enum Cc10ReadRefresh { NotAttempted, Accepted, Offline, Malformed }

    public sealed class Cc10CommandOutcome
    {
        public Cc10Outcome Outcome;
        public string Message = string.Empty;
        public Cc10CommandResult Response;
    }

    public sealed class Cc10BaseCommandOutcome
    {
        public Cc10Outcome Outcome;
        public string Message = string.Empty;
        public Cc10WorldMapBasePlacementResult Response;
    }

    public sealed class Cc10CargoSelectionOutcome
    {
        public Cc10Outcome Outcome;
        public string Message = string.Empty;
        public Cc10CargoParticipantSelectionResult Response;
    }

    /// <summary>
    /// Presentation-side state holder for CC10, built against the real, published server contract
    /// (CloudCode/CC10Frontier). It holds the last authoritative FrontierSnapshotResult, gates
    /// input while offline/disabled/in-flight, replays the ORIGINAL requestId after a lost
    /// response (server dedups by requestId per authority rule 5), reloads on
    /// AUTHORITY_GENERATION_MISMATCH (authority rule 8: reconnect after emergency-disable), and
    /// de-duplicates by receiptId. It never writes PlayerProfile/SaveSystem directly - a command's
    /// settlement (Cc10Receipt) surfaces through <see cref="ReceiptReceived"/> for the host
    /// (Collection/Save owner) to apply. This layer never authors phase, threat, reward, cooldown,
    /// or ranking values - every number here is either echoed from the server or a locked preview
    /// constant from <see cref="Cc10Rules"/>.
    /// </summary>
    public sealed class Cc10FrontierClient
    {
        private readonly ICc10FrontierGateway _gateway;
        private readonly Dictionary<string, string> _pendingRequestIds = new Dictionary<string, string>();
        private readonly HashSet<string> _inFlight = new HashSet<string>();
        private readonly HashSet<string> _seenReceipts = new HashSet<string>();
        private readonly Func<string> _newRequestId;
        private readonly List<string> _anomalies = new List<string>();

        public Cc10FrontierClient(ICc10FrontierGateway gateway, Cc10ServerClock clock = null, Func<string> requestIdFactory = null)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            Clock = clock ?? new Cc10ServerClock();
            _newRequestId = requestIdFactory ?? (() => Guid.NewGuid().ToString("N"));
        }

        public Cc10ServerClock Clock { get; }
        public Cc10FrontierSnapshot Snapshot { get; private set; }
        /// <summary>Last authoritative GetGuildState result, or null if never loaded / the caller
        /// has no guild. Loaded separately from <see cref="Snapshot"/> because it needs a guild id
        /// the frontier snapshot does not carry.</summary>
        public Cc10GuildSnapshot GuildSnapshot { get; private set; }
        /// <summary>Last accepted GetWorldMapSnapshot (private per-player occupancy + Central contest
        /// color tokens), or null. Only a snapshot with the known schema and a non-regressing
        /// occupancyVersion is ever accepted.</summary>
        public Cc10WorldMapSnapshotDto WorldMap { get; private set; }
        public Cc10WorldMapRefresh LastWorldMapRefresh { get; private set; } = Cc10WorldMapRefresh.NotAttempted;
        /// <summary>The caller's own current guild id, or null. Set ONLY from a successfully
        /// Accepted <see cref="RefreshGuildIdentityAsync"/> (the server-owned answer to "what guild
        /// am I in" - BE ec4b49b3) or from an explicit <see cref="RefreshGuildStateAsync"/> call;
        /// this layer never supplies, hashes, or derives one itself. Enrolling in a contest needs it.</summary>
        public string GuildId { get; private set; }
        /// <summary>Last accepted GetCallerGuildIdentity result, or null if never accepted. Carries
        /// hasGuild/membershipEpoch/membershipExpiresUtcMs alongside guildId for a future UI to
        /// render membership detail - this layer only reads guildId out of it.</summary>
        public Cc10GuildIdentityResult GuildIdentity { get; private set; }
        public Cc10GuildIdentityRefresh LastGuildIdentityRefresh { get; private set; } = Cc10GuildIdentityRefresh.NotAttempted;
        /// <summary>Last accepted GetWorldMapBaseSnapshot's own base placement (BE-CC11-005) -
        /// server-owned status/operation/nodeId, never client-derived.</summary>
        public Cc10WorldMapBasePlacementDto BasePlacement { get; private set; }
        /// <summary>Server-owned list of placement-eligible node ids from the same snapshot - this
        /// client never computes eligible cells itself.</summary>
        public string[] BaseCells { get; private set; } = Array.Empty<string>();
        /// <summary>occupancyVersion from the last accepted base snapshot - the CAS token
        /// PlaceWorldMapBase/RelocateWorldMapBase echo back; refreshed after every accepted or
        /// conflicting base command so a retry always carries the latest token.</summary>
        public int BaseOccupancyVersion { get; private set; }
        public Cc10ReadRefresh LastBaseSnapshotRefresh { get; private set; } = Cc10ReadRefresh.NotAttempted;
        /// <summary>Last accepted GetWorldMapRegion result (BE 0784b04b) - a bounded
        /// logical-coordinate window for visible-region loading and marker data. cells/
        /// hotspots/validPlacement/occupiedByYou are entirely server-computed.</summary>
        public Cc10WorldMapRegionSnapshotResult WorldMapRegion { get; private set; }
        public Cc10ReadRefresh LastWorldMapRegionRefresh { get; private set; } = Cc10ReadRefresh.NotAttempted;
        private (int minX, int minY, int maxX, int maxY)? _lastRegionQuery;
        public Cc10CargoSelectionCatalogResult CargoCatalog { get; private set; }
        public Cc10ReadRefresh LastCargoCatalogRefresh { get; private set; } = Cc10ReadRefresh.NotAttempted;
        /// <summary>Last accepted GetGuildHallManagement snapshot, keyed off <see cref="GuildId"/> -
        /// this layer never supplies a guildId of its own.</summary>
        public Cc10GuildManagementSnapshotDto GuildManagement { get; private set; }
        public Cc10ReadRefresh LastGuildManagementRefresh { get; private set; } = Cc10ReadRefresh.NotAttempted;
        /// <summary>Last accepted SearchGuildMembers page, or null before the first search.</summary>
        public Cc10GuildMemberSearchResult GuildMemberSearch { get; private set; }
        public Cc10ReadRefresh LastGuildMemberSearchRefresh { get; private set; } = Cc10ReadRefresh.NotAttempted;
        /// <summary>Last accepted GetRankingSeasonSource result. Null season is a valid answer -
        /// no Accepting/Frozen/Published season currently exists.</summary>
        public Cc10RankingSeasonSourceDto RankingSeasonSource { get; private set; }
        public Cc10ReadRefresh LastRankingSeasonSourceRefresh { get; private set; } = Cc10ReadRefresh.NotAttempted;
        public Cc10Connection Connection { get; private set; } = Cc10Connection.Unknown;
        public IReadOnlyList<string> StateAnomalies => _anomalies;
        public int PendingRequestCount => _pendingRequestIds.Count;
        public bool HasState => Snapshot != null;

        public event Action Changed;
        /// <summary>Raised once per new receiptId with the server's settlement instruction. The host
        /// applies goldCredit/materialsCredit/goldDebit/materialsDebit/staminaDebit to the wallet;
        /// this layer never touches PlayerProfile.</summary>
        public event Action<Cc10Receipt> ReceiptReceived;

        public bool IsSystemDisabled(string systemId) => HasState && Snapshot.IsSystemDisabled(systemId);

        /// <summary>True when the screen for <paramref name="systemId"/> must not offer any mutation.</summary>
        public bool IsReadOnly(string systemId) =>
            Connection != Cc10Connection.Online || !HasState || IsSystemDisabled(systemId);

        /// <summary>Reload authoritative state. Returns false when offline or the response was stale
        /// or unsuccessful; the last snapshot stays visible, read-only.</summary>
        public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
        {
            Cc10FrontierSnapshot fresh;
            try
            {
                fresh = await _gateway.CallAsync<Cc10FrontierSnapshot>(Cc10Endpoints.GetFrontierState, null, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Connection = Cc10Connection.Offline;
                RaiseChanged();
                return false;
            }

            if (fresh == null || !fresh.success)
            {
                Connection = Cc10Connection.Offline;
                RaiseChanged();
                return false;
            }

            Connection = Cc10Connection.Online;
            Clock.Sample(fresh.serverUtcMs);

            if (Snapshot != null && fresh.stateVersion < Snapshot.stateVersion)
            {
                RaiseChanged(); // stale/out-of-order response; keep what's on screen
                return false;
            }

            RecordTransitionAnomalies(Snapshot, fresh);
            Snapshot = fresh;
            RaiseChanged();
            return true;
        }

        /// <summary>Reload this player's own guild state (territory contribution/ownership rows
        /// joined server-side, guild research, membership). Returns false on any failure; the last
        /// GuildSnapshot (if any) stays visible, read-only.</summary>
        public async Task<bool> RefreshGuildStateAsync(string guildId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(guildId)) return false;
            Cc10GuildSnapshot fresh;
            try
            {
                fresh = await _gateway.CallAsync<Cc10GuildSnapshot>(Cc10Endpoints.GetGuildState,
                    new Dictionary<string, object> { { "guildId", guildId } }, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Connection = Cc10Connection.Offline;
                RaiseChanged();
                return false;
            }

            if (fresh == null || !fresh.success)
            {
                Connection = Cc10Connection.Offline;
                RaiseChanged();
                return false;
            }

            Connection = Cc10Connection.Online;
            GuildSnapshot = fresh;
            GuildId = guildId;
            RaiseChanged();
            return true;
        }

        /// <summary>The server-owned, zero-argument answer to "what guild is the caller currently
        /// in" (BE ec4b49b3). Sends no guildId and performs no client-side hashing - GetServerUtc-
        /// style identity, not a query. hasGuild=false/guildId=null is a VALID, successful no-guild
        /// state (never-joined, left, revoked or expired all look identical here by design - BE
        /// does not distinguish them) and updates <see cref="GuildId"/> to null exactly like any
        /// other Accepted result. Every other outcome - a transport failure, an unsuccessful result
        /// (AUTHENTICATION_REQUIRED/AUTHORITY_UNAVAILABLE/STORAGE_UNAVAILABLE/anything else), or a
        /// malformed positive response (hasGuild=true with an empty guildId) - fails CLOSED: both
        /// <see cref="GuildId"/> and <see cref="GuildIdentity"/> are cleared, never left holding a
        /// stale or guessed value from a previous successful call.</summary>
        public async Task<bool> RefreshGuildIdentityAsync(CancellationToken cancellationToken = default)
        {
            Cc10GuildIdentityResult fresh;
            try
            {
                fresh = await _gateway.CallAsync<Cc10GuildIdentityResult>(Cc10Endpoints.GetCallerGuildIdentity, null, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Connection = Cc10Connection.Offline;
                FailClosedGuildIdentity(Cc10GuildIdentityRefresh.Offline);
                return false;
            }

            if (fresh == null)
            {
                FailClosedGuildIdentity(Cc10GuildIdentityRefresh.Malformed);
                return false;
            }

            if (!fresh.success)
            {
                Cc10GuildIdentityRefresh status =
                    fresh.errorCode == Cc10Errors.AuthenticationRequired ? Cc10GuildIdentityRefresh.AuthenticationRequired
                    : fresh.errorCode == Cc10Errors.AuthorityUnavailable ? Cc10GuildIdentityRefresh.AuthorityUnavailable
                    : Cc10GuildIdentityRefresh.Malformed; // STORAGE_UNAVAILABLE / anything else - still fail closed
                FailClosedGuildIdentity(status);
                return false;
            }

            if (fresh.hasGuild && string.IsNullOrEmpty(fresh.guildId))
            {
                // Self-contradictory positive response (claims membership but names no guild) -
                // never trusted, same as any other malformed result.
                FailClosedGuildIdentity(Cc10GuildIdentityRefresh.Malformed);
                return false;
            }

            Connection = Cc10Connection.Online;
            Clock.Sample(fresh.serverUtcMs);
            GuildIdentity = fresh;
            GuildId = fresh.hasGuild ? fresh.guildId : null;
            LastGuildIdentityRefresh = Cc10GuildIdentityRefresh.Accepted;
            RaiseChanged();
            return true;
        }

        private void FailClosedGuildIdentity(Cc10GuildIdentityRefresh status)
        {
            GuildIdentity = null;
            GuildId = null;
            LastGuildIdentityRefresh = status;
            RaiseChanged();
        }

        // ---- BE-CC11-005: World Map base placement, Cargo participant selection, Guild Hall
        // management, ranking-season source. Reads keep the last accepted value on failure
        // (matching RefreshWorldMapAsync); the two mutating commands below never optimistically
        // apply anything and fail closed whenever this client is offline or has no state loaded -
        // they only ever reflect what a subsequent successful response actually said. ----

        /// <summary>The server-owned base-placement cell list and CAS tokens. Never computes
        /// eligible cells or a mapVersion/occupancyVersion itself - both are echoed straight from
        /// the response.</summary>
        public async Task<bool> RefreshWorldMapBaseSnapshotAsync(CancellationToken cancellationToken = default)
        {
            Cc10WorldMapBaseSnapshotResult fresh;
            try
            {
                fresh = await _gateway.CallAsync<Cc10WorldMapBaseSnapshotResult>(Cc10Endpoints.GetWorldMapBaseSnapshot, null, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Connection = Cc10Connection.Offline;
                LastBaseSnapshotRefresh = Cc10ReadRefresh.Offline;
                RaiseChanged();
                return false;
            }

            if (fresh == null || !fresh.success)
            {
                LastBaseSnapshotRefresh = fresh == null ? Cc10ReadRefresh.Malformed : Cc10ReadRefresh.Offline;
                RaiseChanged();
                return false;
            }

            Connection = Cc10Connection.Online;
            Clock.Sample(fresh.serverUtcMs);
            BasePlacement = fresh.basePlacement;
            BaseCells = fresh.cells ?? Array.Empty<string>();
            BaseOccupancyVersion = fresh.occupancyVersion;
            LastBaseSnapshotRefresh = Cc10ReadRefresh.Accepted;
            RaiseChanged();
            return true;
        }

        /// <summary>Loads a bounded logical-coordinate window of the map (BE 0784b04b) - visible-
        /// region loading and marker data for a viewport MS owns. minX/minY/maxX/maxY are passed
        /// through verbatim; this client never computes or clamps the window itself - the server
        /// rejects an oversized or inverted window with INVALID_REQUEST. Remembers the last
        /// queried bounds so an accepted or conflicting base command (see ChangeBaseAsync) can
        /// silently refresh the same visible region afterward.</summary>
        public async Task<bool> RefreshWorldMapRegionAsync(int minX, int minY, int maxX, int maxY, CancellationToken cancellationToken = default)
        {
            _lastRegionQuery = (minX, minY, maxX, maxY);
            var request = new Dictionary<string, object> { ["minX"] = minX, ["minY"] = minY, ["maxX"] = maxX, ["maxY"] = maxY };

            Cc10WorldMapRegionSnapshotResult fresh;
            try
            {
                fresh = await _gateway.CallAsync<Cc10WorldMapRegionSnapshotResult>(Cc10Endpoints.GetWorldMapRegion, request, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Connection = Cc10Connection.Offline;
                LastWorldMapRegionRefresh = Cc10ReadRefresh.Offline;
                RaiseChanged();
                return false;
            }

            if (fresh == null || !fresh.success)
            {
                LastWorldMapRegionRefresh = fresh == null ? Cc10ReadRefresh.Malformed : Cc10ReadRefresh.Offline;
                RaiseChanged();
                return false;
            }

            Connection = Cc10Connection.Online;
            Clock.Sample(fresh.serverUtcMs);
            WorldMapRegion = fresh;
            LastWorldMapRegionRefresh = Cc10ReadRefresh.Accepted;
            RaiseChanged();
            return true;
        }

        /// <summary>Reloads the last-queried region window, if any (called after an accepted or
        /// conflicting base command so a visible region already on screen picks up the new
        /// occupancy/base marker without the caller having to remember the bounds itself).</summary>
        private Task RefreshLastWorldMapRegionIfAnyAsync(CancellationToken cancellationToken) =>
            _lastRegionQuery.HasValue
                ? RefreshWorldMapRegionAsync(_lastRegionQuery.Value.minX, _lastRegionQuery.Value.minY, _lastRegionQuery.Value.maxX, _lastRegionQuery.Value.maxY, cancellationToken)
                : Task.CompletedTask;

        /// <summary>Server-owned eligible mission/avatar/formation lists for Cargo participant
        /// selection - avatars/formations already locked elsewhere are excluded server-side.</summary>
        public async Task<bool> RefreshCargoSelectionCatalogAsync(CancellationToken cancellationToken = default)
        {
            Cc10CargoSelectionCatalogResult fresh;
            try
            {
                fresh = await _gateway.CallAsync<Cc10CargoSelectionCatalogResult>(Cc10Endpoints.GetCargoSelectionCatalog, null, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Connection = Cc10Connection.Offline;
                LastCargoCatalogRefresh = Cc10ReadRefresh.Offline;
                RaiseChanged();
                return false;
            }

            if (fresh == null || !fresh.success)
            {
                LastCargoCatalogRefresh = fresh == null ? Cc10ReadRefresh.Malformed : Cc10ReadRefresh.Offline;
                RaiseChanged();
                return false;
            }

            Connection = Cc10Connection.Online;
            Clock.Sample(fresh.serverUtcMs);
            CargoCatalog = fresh;
            LastCargoCatalogRefresh = Cc10ReadRefresh.Accepted;
            RaiseChanged();
            return true;
        }

        /// <summary>Guild Hall management snapshot for <see cref="GuildId"/> - the caller's own,
        /// already-known guild id from <see cref="RefreshGuildIdentityAsync"/>/
        /// <see cref="RefreshGuildStateAsync"/>. Never calls with an invented guildId: if this
        /// client has none yet, the call is skipped entirely rather than sent with a guess.</summary>
        public async Task<bool> RefreshGuildHallManagementAsync(CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(GuildId))
                return false;

            Cc10GuildManagementResult fresh;
            var request = new Dictionary<string, object> { ["guildId"] = GuildId };
            try
            {
                fresh = await _gateway.CallAsync<Cc10GuildManagementResult>(Cc10Endpoints.GetGuildHallManagement, request, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Connection = Cc10Connection.Offline;
                LastGuildManagementRefresh = Cc10ReadRefresh.Offline;
                RaiseChanged();
                return false;
            }

            if (fresh == null || !fresh.success)
            {
                LastGuildManagementRefresh = fresh == null ? Cc10ReadRefresh.Malformed : Cc10ReadRefresh.Offline;
                RaiseChanged();
                return false;
            }

            Connection = Cc10Connection.Online;
            Clock.Sample(fresh.serverUtcMs);
            GuildManagement = fresh.guild;
            LastGuildManagementRefresh = Cc10ReadRefresh.Accepted;
            RaiseChanged();
            return true;
        }

        /// <summary>Server-paged, server-filtered search of the caller's own guild's member
        /// roster (BE 73793beb). Uses <see cref="GuildId"/> - never an invented guildId; skipped
        /// entirely, no request sent, when this client has none yet, exactly like
        /// <see cref="RefreshGuildHallManagementAsync"/>. query/cursor/limit are passed through
        /// verbatim - limit is NOT clamped here: the server rejects an out-of-range value (1-50)
        /// with INVALID_REQUEST rather than silently adjusting it, and this client does not paper
        /// over that with a local clamp. cursor is the opaque token the server itself returned
        /// from a prior page (nextCursor) - this client never parses or computes it.</summary>
        public async Task<bool> SearchGuildMembersAsync(string query = null, string cursor = null, int limit = 20, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(GuildId))
                return false;

            var request = new Dictionary<string, object>
            {
                ["guildId"] = GuildId,
                ["query"] = query ?? string.Empty,
                ["cursor"] = cursor ?? string.Empty,
                ["limit"] = limit,
            };

            Cc10GuildMemberSearchResult fresh;
            try
            {
                fresh = await _gateway.CallAsync<Cc10GuildMemberSearchResult>(Cc10Endpoints.SearchGuildMembers, request, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Connection = Cc10Connection.Offline;
                LastGuildMemberSearchRefresh = Cc10ReadRefresh.Offline;
                RaiseChanged();
                return false;
            }

            if (fresh == null || !fresh.success)
            {
                LastGuildMemberSearchRefresh = fresh == null ? Cc10ReadRefresh.Malformed : Cc10ReadRefresh.Offline;
                RaiseChanged();
                return false;
            }

            Connection = Cc10Connection.Online;
            Clock.Sample(fresh.serverUtcMs);
            GuildMemberSearch = fresh;
            LastGuildMemberSearchRefresh = Cc10ReadRefresh.Accepted;
            RaiseChanged();
            return true;
        }

        public async Task<bool> RefreshRankingSeasonSourceAsync(CancellationToken cancellationToken = default)
        {
            Cc10RankingSeasonSourceResult fresh;
            try
            {
                fresh = await _gateway.CallAsync<Cc10RankingSeasonSourceResult>(Cc10Endpoints.GetRankingSeasonSource, null, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Connection = Cc10Connection.Offline;
                LastRankingSeasonSourceRefresh = Cc10ReadRefresh.Offline;
                RaiseChanged();
                return false;
            }

            if (fresh == null || !fresh.success)
            {
                LastRankingSeasonSourceRefresh = fresh == null ? Cc10ReadRefresh.Malformed : Cc10ReadRefresh.Offline;
                RaiseChanged();
                return false;
            }

            Connection = Cc10Connection.Online;
            Clock.Sample(fresh.serverUtcMs);
            RankingSeasonSource = fresh.season; // null is a valid answer - no live season
            LastRankingSeasonSourceRefresh = Cc10ReadRefresh.Accepted;
            RaiseChanged();
            return true;
        }

        /// <param name="x">Optional integrity check only - the logical x coordinate this client
        /// read off a server-supplied cell (GetWorldMapRegion/GetWorldMapBaseSnapshot), never a
        /// client-invented value. Omit when not available; the server rejects a mismatch with
        /// this node's real coordinate as Conflict rather than silently ignoring it.</param>
        public Task<Cc10BaseCommandOutcome> PlaceWorldMapBaseAsync(string nodeId, int? x = null, int? y = null, CancellationToken cancellationToken = default) =>
            ChangeBaseAsync(Cc10WorldMapBaseOperation.Place, nodeId, x, y, cancellationToken);

        public Task<Cc10BaseCommandOutcome> RelocateWorldMapBaseAsync(string nodeId, int? x = null, int? y = null, CancellationToken cancellationToken = default) =>
            ChangeBaseAsync(Cc10WorldMapBaseOperation.Relocate, nodeId, x, y, cancellationToken);

        /// <summary>Places or relocates the caller's base on a server-owned eligible cell. Offline,
        /// no-state, disabled-system, or a blank nodeId all fail closed before any request is
        /// sent - never an optimistic local placement. mapVersion/occupancyVersion are echoed
        /// straight from the last accepted base snapshot, never computed here; x/y (BE 0784b04b)
        /// are passed through only if the caller supplied them, never invented.</summary>
        private async Task<Cc10BaseCommandOutcome> ChangeBaseAsync(string operation, string nodeId, int? x, int? y, CancellationToken cancellationToken)
        {
            if (Connection != Cc10Connection.Online || !HasState)
                return new Cc10BaseCommandOutcome { Outcome = Cc10Outcome.Offline, Message = Cc10Copy.Offline };
            if (IsSystemDisabled(Cc10SystemId.WorldMap))
                return new Cc10BaseCommandOutcome { Outcome = Cc10Outcome.Disabled, Message = Cc10Copy.SystemDisabled };
            if (string.IsNullOrEmpty(nodeId))
                return new Cc10BaseCommandOutcome { Outcome = Cc10Outcome.Rejected, Message = Cc10Copy.ForRejection(Cc10Errors.InvalidRequest) };

            string key = "WorldMapBase|" + operation;
            if (!_inFlight.Add(key))
                return new Cc10BaseCommandOutcome { Outcome = Cc10Outcome.InFlight, Message = Cc10Copy.InFlight };

            try
            {
                if (!_pendingRequestIds.TryGetValue(key, out string requestId))
                {
                    requestId = _newRequestId();
                    _pendingRequestIds[key] = requestId;
                }

                var request = new Dictionary<string, object>
                {
                    ["operation"] = operation,
                    ["nodeId"] = nodeId,
                    ["mapVersion"] = WorldMap != null ? WorldMap.mapVersion : string.Empty,
                    ["occupancyVersion"] = BaseOccupancyVersion,
                    ["requestId"] = requestId,
                    ["expectedStateVersion"] = Snapshot.stateVersion,
                    ["expectedAuthorityGeneration"] = Snapshot.authorityGeneration,
                };
                if (x.HasValue) request["x"] = x.Value;
                if (y.HasValue) request["y"] = y.Value;

                Cc10WorldMapBasePlacementResult response;
                try
                {
                    string endpoint = operation == Cc10WorldMapBaseOperation.Place ? Cc10Endpoints.PlaceWorldMapBase : Cc10Endpoints.RelocateWorldMapBase;
                    response = await _gateway.CallAsync<Cc10WorldMapBasePlacementResult>(endpoint, request, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                    // Lost response: keep the request id so a retry replays the original request.
                    Connection = Cc10Connection.Offline;
                    RaiseChanged();
                    return new Cc10BaseCommandOutcome { Outcome = Cc10Outcome.Failed, Message = Cc10Copy.ConnectionLost };
                }

                if (response == null)
                {
                    Connection = Cc10Connection.Offline;
                    RaiseChanged();
                    return new Cc10BaseCommandOutcome { Outcome = Cc10Outcome.Failed, Message = Cc10Copy.ConnectionLost };
                }

                Clock.Sample(response.serverUtcMs);

                if (response.errorCode == Cc10Errors.SystemDisabled)
                {
                    _pendingRequestIds.Remove(key);
                    await RefreshAsync(cancellationToken);
                    return new Cc10BaseCommandOutcome { Outcome = Cc10Outcome.Disabled, Message = Cc10Copy.SystemDisabled, Response = response };
                }

                if (response.success)
                {
                    _pendingRequestIds.Remove(key);
                    BasePlacement = response.basePlacement;
                    await RefreshWorldMapBaseSnapshotAsync(cancellationToken); // reloads the canonical cells/occupancyVersion
                    await RefreshLastWorldMapRegionIfAnyAsync(cancellationToken); // picks up the new base marker if a region is on screen
                    return new Cc10BaseCommandOutcome { Outcome = Cc10Outcome.Applied, Response = response };
                }

                _pendingRequestIds.Remove(key);
                if (response.errorCode == Cc10Errors.Conflict || response.errorCode == Cc10Errors.AuthorityStale)
                {
                    await RefreshWorldMapBaseSnapshotAsync(cancellationToken); // first valid CAS wins; reload the latest token
                    await RefreshLastWorldMapRegionIfAnyAsync(cancellationToken);
                    return new Cc10BaseCommandOutcome { Outcome = Cc10Outcome.Conflict, Message = Cc10Copy.Conflict, Response = response };
                }

                return new Cc10BaseCommandOutcome { Outcome = Cc10Outcome.Rejected, Message = Cc10Copy.ForRejection(response.errorCode), Response = response };
            }
            finally
            {
                _inFlight.Remove(key);
            }
        }

        /// <summary>Locks an avatar/army/formation onto one cargo's mission. Offline, no-state,
        /// disabled-system, or any blank required field all fail closed before any request is
        /// sent - never an optimistic local lock.</summary>
        public async Task<Cc10CargoSelectionOutcome> AcceptCargoParticipantsAsync(
            string cargoId, string avatarId, string armyId, string formationId, string missionId,
            CancellationToken cancellationToken = default)
        {
            if (Connection != Cc10Connection.Online || !HasState)
                return new Cc10CargoSelectionOutcome { Outcome = Cc10Outcome.Offline, Message = Cc10Copy.Offline };
            if (IsSystemDisabled(Cc10SystemId.Cargo))
                return new Cc10CargoSelectionOutcome { Outcome = Cc10Outcome.Disabled, Message = Cc10Copy.SystemDisabled };
            if (string.IsNullOrEmpty(cargoId) || string.IsNullOrEmpty(avatarId) || string.IsNullOrEmpty(formationId) || string.IsNullOrEmpty(missionId))
                return new Cc10CargoSelectionOutcome { Outcome = Cc10Outcome.Rejected, Message = Cc10Copy.ForRejection(Cc10Errors.InvalidRequest) };

            string key = "AcceptCargoParticipants|" + cargoId;
            if (!_inFlight.Add(key))
                return new Cc10CargoSelectionOutcome { Outcome = Cc10Outcome.InFlight, Message = Cc10Copy.InFlight };

            try
            {
                if (!_pendingRequestIds.TryGetValue(key, out string requestId))
                {
                    requestId = _newRequestId();
                    _pendingRequestIds[key] = requestId;
                }

                var request = new Dictionary<string, object>
                {
                    ["cargoId"] = cargoId,
                    ["avatarId"] = avatarId,
                    ["armyId"] = armyId,
                    ["formationId"] = formationId,
                    ["missionId"] = missionId,
                    ["requestId"] = requestId,
                    ["expectedStateVersion"] = Snapshot.stateVersion,
                    ["expectedAuthorityGeneration"] = Snapshot.authorityGeneration,
                };

                Cc10CargoParticipantSelectionResult response;
                try
                {
                    response = await _gateway.CallAsync<Cc10CargoParticipantSelectionResult>(Cc10Endpoints.AcceptCargoParticipants, request, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                    Connection = Cc10Connection.Offline;
                    RaiseChanged();
                    return new Cc10CargoSelectionOutcome { Outcome = Cc10Outcome.Failed, Message = Cc10Copy.ConnectionLost };
                }

                if (response == null)
                {
                    Connection = Cc10Connection.Offline;
                    RaiseChanged();
                    return new Cc10CargoSelectionOutcome { Outcome = Cc10Outcome.Failed, Message = Cc10Copy.ConnectionLost };
                }

                Clock.Sample(response.serverUtcMs);

                if (response.errorCode == Cc10Errors.SystemDisabled)
                {
                    _pendingRequestIds.Remove(key);
                    await RefreshAsync(cancellationToken);
                    return new Cc10CargoSelectionOutcome { Outcome = Cc10Outcome.Disabled, Message = Cc10Copy.SystemDisabled, Response = response };
                }

                if (response.success)
                {
                    _pendingRequestIds.Remove(key);
                    await RefreshCargoSelectionCatalogAsync(cancellationToken); // eligible avatar/formation lists change once locked
                    return new Cc10CargoSelectionOutcome { Outcome = Cc10Outcome.Applied, Response = response };
                }

                _pendingRequestIds.Remove(key);
                if (response.errorCode == Cc10Errors.Conflict || response.errorCode == Cc10Errors.AuthorityStale)
                {
                    await RefreshCargoSelectionCatalogAsync(cancellationToken);
                    return new Cc10CargoSelectionOutcome { Outcome = Cc10Outcome.Conflict, Message = Cc10Copy.Conflict, Response = response };
                }

                return new Cc10CargoSelectionOutcome { Outcome = Cc10Outcome.Rejected, Message = Cc10Copy.ForRejection(response.errorCode), Response = response };
            }
            finally
            {
                _inFlight.Remove(key);
            }
        }

        /// <summary>Reload everything a screen reads: the frontier snapshot AND the canonical World Map
        /// snapshot. Call on open and on every reconnect. Both loads always run; the result is true
        /// only if both were accepted.</summary>
        public async Task<bool> RefreshAllAsync(CancellationToken cancellationToken = default)
        {
            bool frontier = await RefreshAsync(cancellationToken);
            bool map = await RefreshWorldMapAsync(cancellationToken);
            return frontier && map;
        }

        /// <summary>Reload the canonical World Map snapshot (a pure read). The last good snapshot is
        /// ALWAYS retained unless a strictly acceptable one arrives. Rejected (see
        /// <see cref="LastWorldMapRefresh"/>): a transport failure (Offline), a failed/missing snapshot
        /// or one with no mapVersion (Malformed), an unknown schemaVersion (SchemaRejected), or an
        /// occupancyVersion lower than the one already held (Stale - it is per-player monotonic) UNLESS the map epoch changed (see
        /// MapEpochChanged) and the snapshot is not older on the server clock - then it is an accepted reset
        /// (AcceptedAfterReset) that replaces the cache. A
        /// changed mapVersion is accepted (the server re-authored the map); an equal occupancyVersion
        /// is an idempotent re-read.</summary>
        public async Task<bool> RefreshWorldMapAsync(CancellationToken cancellationToken = default)
        {
            Cc10WorldMapSnapshotResult fresh;
            try
            {
                fresh = await _gateway.CallAsync<Cc10WorldMapSnapshotResult>(Cc10Endpoints.GetWorldMapSnapshot, null, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Connection = Cc10Connection.Offline;
                LastWorldMapRefresh = Cc10WorldMapRefresh.Offline;
                RaiseChanged();
                return false;
            }

            Connection = Cc10Connection.Online;
            if (fresh == null || !fresh.success || fresh.snapshot == null || string.IsNullOrEmpty(fresh.snapshot.mapVersion))
            {
                LastWorldMapRefresh = Cc10WorldMapRefresh.Malformed;
                RaiseChanged();
                return false;
            }

            if (fresh.snapshot.schemaVersion != Cc10WorldMapSnapshotSchema.V1)
            {
                LastWorldMapRefresh = Cc10WorldMapRefresh.SchemaRejected;
                RaiseChanged();
                return false;
            }

            Clock.Sample(fresh.serverUtcMs);
            bool reset = false;
            if (WorldMap != null && fresh.snapshot.occupancyVersion < WorldMap.occupancyVersion)
            {
                // A lower occupancyVersion is normally a stale/out-of-order response. It is a legitimate
                // server-side occupancy reset only when the map epoch changed (different mapVersion or a
                // different set of contest seasonIds) AND the snapshot is not older on the server clock -
                // an out-of-order older response can never pass the clock check.
                reset = MapEpochChanged(WorldMap, fresh.snapshot) && fresh.snapshot.serverUtc >= WorldMap.serverUtc;
                if (!reset)
                {
                    LastWorldMapRefresh = Cc10WorldMapRefresh.Stale;
                    RaiseChanged();
                    return false;
                }
            }

            WorldMap = fresh.snapshot; // an accepted reset replaces the whole cache, never merges
            LastWorldMapRefresh = reset ? Cc10WorldMapRefresh.AcceptedAfterReset : Cc10WorldMapRefresh.Accepted;
            RaiseChanged();
            return true;
        }

        /// <summary>Executes one mutating command. <paramref name="body"/> carries only that
        /// endpoint's own fields (e.g. missionId, nodeId) - requestId/expectedStateVersion are
        /// added here, matching CC10Request's base shape.</summary>
        public async Task<Cc10CommandOutcome> ExecuteAsync(
            string systemId, string endpoint, Dictionary<string, object> body,
            string entityKey, CancellationToken cancellationToken = default)
        {
            if (Connection != Cc10Connection.Online || !HasState)
                return Blocked(Cc10Outcome.Offline, Cc10Copy.Offline);
            if (IsSystemDisabled(systemId))
                return Blocked(Cc10Outcome.Disabled, Cc10Copy.SystemDisabled);

            string key = endpoint + "|" + (entityKey ?? string.Empty);
            if (!_inFlight.Add(key))
                return Blocked(Cc10Outcome.InFlight, Cc10Copy.InFlight);

            try
            {
                if (!_pendingRequestIds.TryGetValue(key, out string requestId))
                {
                    requestId = _newRequestId();
                    _pendingRequestIds[key] = requestId;
                }

                var request = body != null ? new Dictionary<string, object>(body) : new Dictionary<string, object>();
                request["requestId"] = requestId;
                request["expectedStateVersion"] = Snapshot.stateVersion;
                request["expectedAuthorityGeneration"] = Snapshot.authorityGeneration;

                Cc10CommandResult response;
                try
                {
                    response = await _gateway.CallAsync<Cc10CommandResult>(endpoint, request, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                    // Lost response: the server may or may not have committed. Keep the request id so
                    // the retry replays the original request instead of creating a second one.
                    Connection = Cc10Connection.Offline;
                    RaiseChanged();
                    return new Cc10CommandOutcome { Outcome = Cc10Outcome.Failed, Message = Cc10Copy.ConnectionLost };
                }

                if (response == null)
                {
                    Connection = Cc10Connection.Offline;
                    RaiseChanged();
                    return new Cc10CommandOutcome { Outcome = Cc10Outcome.Failed, Message = Cc10Copy.ConnectionLost };
                }

                Clock.Sample(response.serverUtcMs);
                return await SettleAsync(systemId, key, response, cancellationToken);
            }
            finally
            {
                _inFlight.Remove(key);
            }
        }

        private async Task<Cc10CommandOutcome> SettleAsync(string systemId, string key, Cc10CommandResult response, CancellationToken cancellationToken)
        {
            if (response.errorCode == Cc10Errors.SystemDisabled)
            {
                _pendingRequestIds.Remove(key);
                await RefreshAsync(cancellationToken); // pick up the fresh disabledSystems list
                return new Cc10CommandOutcome { Outcome = Cc10Outcome.Disabled, Message = Cc10Copy.SystemDisabled, Response = response };
            }

            if (response.success)
            {
                _pendingRequestIds.Remove(key);
                Cc10Receipt receipt = response.receipt;
                bool firstSight = receipt == null || string.IsNullOrEmpty(receipt.receiptId) || _seenReceipts.Add(receipt.receiptId);
                if (firstSight && receipt != null)
                    ReceiptReceived?.Invoke(receipt);
                await RefreshAsync(cancellationToken);
                await RefreshWorldMapIfAffectedAsync(systemId, cancellationToken);
                return new Cc10CommandOutcome
                {
                    Outcome = (firstSight && !response.replayed) ? Cc10Outcome.Applied : Cc10Outcome.Replayed,
                    Message = (firstSight && !response.replayed) ? string.Empty : Cc10Copy.AlreadyRecorded,
                    Response = response,
                };
            }

            _pendingRequestIds.Remove(key);
            if (response.errorCode == Cc10Errors.Conflict || response.errorCode == Cc10Errors.AuthorityStale)
            {
                await RefreshAsync(cancellationToken); // first valid CAS wins; reload and show the latest
                await RefreshWorldMapIfAffectedAsync(systemId, cancellationToken);
                return new Cc10CommandOutcome { Outcome = Cc10Outcome.Conflict, Message = Cc10Copy.Conflict, Response = response };
            }

            return new Cc10CommandOutcome
            {
                Outcome = Cc10Outcome.Rejected,
                Message = Cc10Copy.ForRejection(response.errorCode),
                Response = response,
            };
        }

        /// <summary>True when two snapshots belong to different map epochs: the mapVersion differs, or the set
        /// of contest seasonIds differs (BE exposes the season only per contest row, there is no top-level
        /// season/epoch field).</summary>
        internal static bool MapEpochChanged(Cc10WorldMapSnapshotDto before, Cc10WorldMapSnapshotDto after)
        {
            if (before.mapVersion != after.mapVersion) return true;
            return !SeasonIds(before).SetEquals(SeasonIds(after));
        }

        private static HashSet<string> SeasonIds(Cc10WorldMapSnapshotDto s)
        {
            var ids = new HashSet<string>();
            if (s.centralContest != null)
                foreach (Cc10WorldMapContestDto d in s.centralContest)
                    if (!string.IsNullOrEmpty(d.seasonId)) ids.Add(d.seasonId);
            return ids;
        }

        /// <summary>A World Map / contest command changes the canonical snapshot, so reload it - but
        /// only if this client already holds one (a host that never loaded it is not surprised by a
        /// new read).</summary>
        private async Task RefreshWorldMapIfAffectedAsync(string systemId, CancellationToken cancellationToken)
        {
            if (WorldMap == null) return;
            if (systemId != Cc10SystemId.WorldMap && systemId != Cc10SystemId.GuildTerritory) return;
            await RefreshWorldMapAsync(cancellationToken);
        }

        private static Cc10CommandOutcome Blocked(Cc10Outcome outcome, string message) =>
            new Cc10CommandOutcome { Outcome = outcome, Message = message };

        private void RecordTransitionAnomalies(Cc10FrontierSnapshot before, Cc10FrontierSnapshot after)
        {
            if (before?.missions == null || after?.missions == null) return;
            foreach (Cc10MissionDto next in after.missions)
            {
                foreach (Cc10MissionDto prev in before.missions)
                {
                    if (prev.missionId != next.missionId) continue;
                    if (!Cc10StateMachine.IsMissionTransitionLegal(prev.status, next.status))
                        _anomalies.Add(next.missionId + ":" + prev.status + ">" + next.status);
                }
            }
        }

        private void RaiseChanged() => Changed?.Invoke();
    }

    /// <summary>Player-facing copy. Never surfaces a raw CC10Errors.* code.</summary>
    public static class Cc10Copy
    {
        public const string Offline = "You're offline. Reconnect to continue.";
        public const string ConnectionLost = "Connection lost. Retry to continue.";
        public const string SystemDisabled = "This feature is paused right now.";
        public const string InFlight = "Working on it...";
        public const string Conflict = "Something changed. Showing the latest.";
        public const string AlreadyRecorded = "Already recorded.";
        public const string CapReached = "Daily Gold limit reached. Nothing was claimed.";
        public const string OfflineClaimRejected = "That can't be claimed while offline.";
        public const string PhaseLocked = "That area isn't unlocked yet.";
        public const string NotAdjacent = "Not adjacent to an owned or discovered location.";
        public const string NotEligible = "Your guild isn't eligible right now.";
        public const string DistrictTaken = "Another guild already enrolled there.";
        public const string AlreadyEnrolledElsewhere = "Your guild already enrolled a different district this season.";
        public const string WindowClosed = "That window has closed.";
        public const string MinigameCoolingDown = "Give it a little longer before playing again.";
        public const string PrerequisiteNotMet = "You need to finish an earlier step first.";
        public const string MissionSlotsFull = "All your mission slots are full right now.";
        public const string MinigameInvalidStream = "That result couldn't be verified. Try again.";
        public const string ColorUnavailable = "No guild color is free right now. Try again later.";
        public const string InvalidRequest = "That request wasn't valid.";
        public const string InvalidState = "That's not available from here right now.";
        public const string BaseRelocationCoolingDown = "Your base was moved recently. Try again later.";
        public const string AlreadyOwned = "You already own that location.";
        public const string Generic = "That didn't go through.";

        public static string ForRejection(string errorCode)
        {
            if (errorCode == Cc10Errors.GoldCapExceeded) return CapReached;
            if (errorCode == Cc10Errors.OfflineClaimRejected) return OfflineClaimRejected;
            if (errorCode == Cc10Errors.PhaseLocked) return PhaseLocked;
            if (errorCode == Cc10Errors.NotAdjacent) return NotAdjacent;
            if (errorCode == Cc10Errors.NotEligible) return NotEligible;
            if (errorCode == Cc10Errors.DistrictTaken) return DistrictTaken;
            if (errorCode == Cc10Errors.AlreadyEnrolledElsewhere) return AlreadyEnrolledElsewhere;
            if (errorCode == Cc10Errors.WindowClosed) return WindowClosed;
            if (errorCode == Cc10Errors.ColorTokenCollision || errorCode == Cc10Errors.ColorPaletteExhausted) return ColorUnavailable;
            if (errorCode == Cc10Errors.MinigameCoolingDown) return MinigameCoolingDown;
            if (errorCode == Cc10Errors.PrerequisiteNotMet) return PrerequisiteNotMet;
            if (errorCode == Cc10Errors.MissionSlotsFull) return MissionSlotsFull;
            if (errorCode == Cc10Errors.MinigameInvalidStream) return MinigameInvalidStream;
            if (errorCode == Cc10Errors.InvalidRequest) return InvalidRequest;
            if (errorCode == Cc10Errors.InvalidState) return InvalidState;
            if (errorCode == Cc10Errors.RateLimited) return BaseRelocationCoolingDown;
            if (errorCode == Cc10Errors.AlreadyOwned) return AlreadyOwned;
            return Generic;
        }
    }
}
