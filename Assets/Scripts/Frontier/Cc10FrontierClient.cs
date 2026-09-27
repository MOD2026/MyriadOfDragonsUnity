using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyriadOfDragons.Frontier
{
    public enum Cc10Connection { Unknown, Online, Offline }

    public enum Cc10Outcome { Applied, Replayed, Conflict, Disabled, Offline, Rejected, InFlight, Failed }

    public sealed class Cc10CommandResult
    {
        public Cc10Outcome Outcome;
        public string Message = string.Empty;
        public Cc10Response Response;
    }

    /// <summary>
    /// Presentation-side state holder for CC10. It holds the last authoritative snapshot, gates
    /// input (offline / disabled / already in flight), replays the ORIGINAL request id after a lost
    /// response, and de-duplicates receipts. It never mutates PlayerProfile, wallet, cards, ranks,
    /// guild or cargo state: authoritative projections are surfaced through
    /// <see cref="ProjectionReceived"/> for the host (Collection/Save owner) to apply.
    /// </summary>
    public sealed class Cc10FrontierClient
    {
        private readonly ICc10FrontierGateway _gateway;
        private readonly Dictionary<string, string> _pendingRequestIds = new Dictionary<string, string>();
        private readonly HashSet<string> _inFlight = new HashSet<string>();
        private readonly HashSet<string> _seenReceipts = new HashSet<string>();
        private readonly HashSet<Cc10System> _locallyDisabled = new HashSet<Cc10System>();
        private readonly Func<string> _newRequestId;
        private readonly List<string> _anomalies = new List<string>();

        public Cc10FrontierClient(ICc10FrontierGateway gateway, Cc10ServerClock clock = null, Func<string> requestIdFactory = null)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            Clock = clock ?? new Cc10ServerClock();
            _newRequestId = requestIdFactory ?? (() => Guid.NewGuid().ToString("N"));
        }

        public Cc10ServerClock Clock { get; }
        public Cc10Snapshot Snapshot { get; private set; }
        public Cc10Connection Connection { get; private set; } = Cc10Connection.Unknown;
        public IReadOnlyList<string> StateAnomalies => _anomalies;
        public int PendingRequestCount => _pendingRequestIds.Count;

        public event Action Changed;
        /// <summary>Raised once per new receipt with the server's wallet/card projection.</summary>
        public event Action<Cc10Projection> ProjectionReceived;

        public bool HasState => Snapshot != null;

        public bool IsSystemDisabled(Cc10System system)
        {
            if (_locallyDisabled.Contains(system)) return true;
            if (Snapshot?.disabledSystems == null) return false;
            return Array.IndexOf(Snapshot.disabledSystems, system.ToString()) >= 0;
        }

        /// <summary>True when the screen for <paramref name="system"/> must not offer any mutation.</summary>
        public bool IsReadOnly(Cc10System system) =>
            Connection != Cc10Connection.Online || !HasState || IsSystemDisabled(system);

        /// <summary>Reload authoritative state. Returns false when offline or the response was stale.
        /// On failure the last snapshot stays visible, read-only.</summary>
        public async Task<bool> RefreshAsync(CancellationToken cancellationToken = default)
        {
            Cc10Snapshot fresh;
            try
            {
                fresh = await _gateway.CallAsync<Cc10Snapshot>(Cc10Endpoints.GetState, null, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Connection = Cc10Connection.Offline;
                RaiseChanged();
                return false;
            }

            if (fresh == null || !fresh.IsSuccess)
            {
                Connection = Cc10Connection.Offline;
                RaiseChanged();
                return false;
            }

            Connection = Cc10Connection.Online;
            Clock.Sample(fresh.serverUtcMs);

            if (Snapshot != null && fresh.stateVersion < Snapshot.stateVersion)
            {
                // Older than what is already on screen (out-of-order reconnect response).
                RaiseChanged();
                return false;
            }

            RecordTransitionAnomalies(Snapshot, fresh);
            Snapshot = fresh;
            _locallyDisabled.Clear();
            RaiseChanged();
            return true;
        }

        public async Task<Cc10CommandResult> ExecuteAsync(
            Cc10System system, string endpoint, Dictionary<string, object> payload,
            string entityKey, CancellationToken cancellationToken = default)
        {
            if (Connection != Cc10Connection.Online || !HasState)
                return Blocked(Cc10Outcome.Offline, Cc10Copy.Offline);
            if (IsSystemDisabled(system))
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

                var body = payload != null
                    ? new Dictionary<string, object>(payload)
                    : new Dictionary<string, object>();
                body["requestId"] = requestId;
                body["expectedStateVersion"] = Snapshot.stateVersion;

                Cc10CommandResponse response;
                try
                {
                    response = await _gateway.CallAsync<Cc10CommandResponse>(endpoint, body, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                    // Lost response: the server may or may not have committed. Keep the request id so
                    // the retry replays the original request instead of creating a second one.
                    Connection = Cc10Connection.Offline;
                    RaiseChanged();
                    return new Cc10CommandResult { Outcome = Cc10Outcome.Failed, Message = Cc10Copy.ConnectionLost };
                }

                if (response == null)
                {
                    Connection = Cc10Connection.Offline;
                    RaiseChanged();
                    return new Cc10CommandResult { Outcome = Cc10Outcome.Failed, Message = Cc10Copy.ConnectionLost };
                }

                Clock.Sample(response.serverUtcMs);
                return await SettleAsync(system, key, response, cancellationToken);
            }
            finally
            {
                _inFlight.Remove(key);
            }
        }

        private async Task<Cc10CommandResult> SettleAsync(
            Cc10System system, string key, Cc10CommandResponse response, CancellationToken cancellationToken)
        {
            if (response.systemDisabled || response.status == Cc10Status.Disabled)
            {
                _pendingRequestIds.Remove(key);
                _locallyDisabled.Add(system);
                RaiseChanged();
                return new Cc10CommandResult { Outcome = Cc10Outcome.Disabled, Message = Cc10Copy.SystemDisabled, Response = response };
            }

            if (response.IsSuccess)
            {
                _pendingRequestIds.Remove(key);
                bool firstSight = string.IsNullOrEmpty(response.receiptId) || _seenReceipts.Add(response.receiptId);
                if (firstSight && response.projection != null)
                    ProjectionReceived?.Invoke(response.projection);
                await RefreshAsync(cancellationToken);
                return new Cc10CommandResult
                {
                    Outcome = firstSight ? Cc10Outcome.Applied : Cc10Outcome.Replayed,
                    Message = firstSight ? string.Empty : Cc10Copy.AlreadyRecorded,
                    Response = response,
                };
            }

            _pendingRequestIds.Remove(key);
            if (response.status == Cc10Status.Conflict)
            {
                await RefreshAsync(cancellationToken);
                return new Cc10CommandResult { Outcome = Cc10Outcome.Conflict, Message = Cc10Copy.Conflict, Response = response };
            }

            return new Cc10CommandResult
            {
                Outcome = Cc10Outcome.Rejected,
                Message = Cc10Copy.ForRejection(response.status, response.errorCode),
                Response = response,
            };
        }

        private static Cc10CommandResult Blocked(Cc10Outcome outcome, string message) =>
            new Cc10CommandResult { Outcome = outcome, Message = message };

        private void RecordTransitionAnomalies(Cc10Snapshot before, Cc10Snapshot after)
        {
            if (before?.missions == null || after?.missions == null) return;
            foreach (Cc10MissionRow next in after.missions)
            {
                foreach (Cc10MissionRow prev in before.missions)
                {
                    if (prev.missionId != next.missionId || prev.attempt != next.attempt) continue;
                    if (!Cc10StateMachine.IsMissionTransitionLegal(prev.state, next.state))
                        _anomalies.Add(next.missionId + ":" + prev.state + ">" + next.state);
                }
            }
        }

        private void RaiseChanged() => Changed?.Invoke();
    }

    /// <summary>Player-facing copy. Never surfaces raw backend codes.</summary>
    public static class Cc10Copy
    {
        public const string Offline = "You're offline. Reconnect to continue.";
        public const string ConnectionLost = "Connection lost. Retry to continue.";
        public const string SystemDisabled = "This feature is paused right now.";
        public const string InFlight = "Working on it...";
        public const string Conflict = "Something changed. Showing the latest.";
        public const string AlreadyRecorded = "Already recorded.";
        public const string CapReached = "Daily Gold limit reached. Nothing was claimed.";
        public const string Generic = "That didn't go through.";
        public const string NotReady = "Not ready yet.";

        public static string ForRejection(string status, string errorCode)
        {
            if (status == Cc10Status.CapExceeded) return CapReached;
            if (status == Cc10Status.NotFound) return Conflict;
            return Generic;
        }
    }
}
