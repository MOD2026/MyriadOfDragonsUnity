using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyriadOfDragons.Frontier
{
    public enum Cc10Connection { Unknown, Online, Offline }

    public enum Cc10Outcome { Applied, Replayed, Conflict, Disabled, Offline, Rejected, InFlight, Failed }

    public sealed class Cc10CommandOutcome
    {
        public Cc10Outcome Outcome;
        public string Message = string.Empty;
        public Cc10CommandResult Response;
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
                return await SettleAsync(key, response, cancellationToken);
            }
            finally
            {
                _inFlight.Remove(key);
            }
        }

        private async Task<Cc10CommandOutcome> SettleAsync(string key, Cc10CommandResult response, CancellationToken cancellationToken)
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
                return new Cc10CommandOutcome
                {
                    Outcome = (firstSight && !response.replayed) ? Cc10Outcome.Applied : Cc10Outcome.Replayed,
                    Message = (firstSight && !response.replayed) ? string.Empty : Cc10Copy.AlreadyRecorded,
                    Response = response,
                };
            }

            _pendingRequestIds.Remove(key);
            if (response.errorCode == Cc10Errors.Conflict || response.errorCode == Cc10Errors.AuthorityGenerationMismatch)
            {
                await RefreshAsync(cancellationToken); // first valid CAS wins; reload and show the latest
                return new Cc10CommandOutcome { Outcome = Cc10Outcome.Conflict, Message = Cc10Copy.Conflict, Response = response };
            }

            return new Cc10CommandOutcome
            {
                Outcome = Cc10Outcome.Rejected,
                Message = Cc10Copy.ForRejection(response.errorCode),
                Response = response,
            };
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
        public const string Generic = "That didn't go through.";

        public static string ForRejection(string errorCode)
        {
            if (errorCode == Cc10Errors.GoldCapReached) return CapReached;
            if (errorCode == Cc10Errors.OfflineClaimRejected) return OfflineClaimRejected;
            return Generic;
        }
    }
}
