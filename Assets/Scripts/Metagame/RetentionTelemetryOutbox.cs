using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace MyriadOfDragons.Metagame
{
    /// <summary>Bounded local outbox for retention telemetry events - queues events until the
    /// next authenticated session can flush them through the real Cloud Code gateway. LOCKED,
    /// deliberately its OWN class, NOT part of PlayerProfile or ordinary Cloud Save (register:
    /// "Retention telemetry architecture - LOCKED, do NOT touch PlayerProfile" - "a local field is
    /// only justified for a bounded offline outbox... and even that should be a separate
    /// telemetry queue, not part of the gameplay save schema").
    ///
    /// Analytics must never block gameplay/rewards/saves (register's own real-failure-mode list):
    /// <see cref="Enqueue"/> is synchronous, fast, and never throws - it does not touch the
    /// network. <see cref="FlushAsync"/> is the only method that talks to the gateway; it is
    /// always safe to call opportunistically (e.g. on session start), and on any failure
    /// (explicit unsuccessful result OR an exception from the gateway itself - network, auth,
    /// timeout) it stops and leaves the remaining queue intact for the next attempt, rather than
    /// throwing into the caller or losing events.
    ///
    /// Persists to its own JSON file (never SaveSystem/PlayerProfile/ordinary Cloud Save) so
    /// queued-but-unsent events survive an app restart between sessions - the whole point of
    /// "queuing until the next authenticated session."</summary>
    public sealed class RetentionTelemetryOutbox
    {
        /// <summary>Bounded per the register's own requirement - past this many unsent events,
        /// the OLDEST is dropped to make room for the newest, rather than growing without limit
        /// or refusing new events. Analytics data is inherently best-effort; losing the very
        /// oldest events under sustained ingestion outage is preferable to an unbounded queue
        /// consuming device storage indefinitely.</summary>
        public const int MaxQueuedEvents = 500;

        /// <summary>Hard ceiling on one FlushAsync attempt. Presenters fire-and-forget with
        /// <c>CancellationToken.None</c>, and the live Cloud Code / auth chain has no timeout of
        /// its own - without this, a stalled network call can hang the EditMode process (and a
        /// player session) indefinitely. Matches the existing "best-effort, never blocks"
        /// contract in this class header.</summary>
        public static readonly TimeSpan FlushDeadline = TimeSpan.FromSeconds(8);

        private const string FileName = "retention_telemetry_outbox.json";

        private readonly string _filePath;
        private readonly IRetentionTelemetryGateway _gateway;
        private List<RetentionTelemetryEvent> _queue;

        public IReadOnlyList<RetentionTelemetryEvent> QueuedEventsForTests => _queue;

        /// <summary>rootDirectory defaults to Application.persistentDataPath for real use; tests
        /// inject a scratch temp directory, the same isolation pattern every other test in this
        /// codebase uses for its own save-adjacent state (never Application.persistentDataPath in
        /// a test, which would pollute real device storage between test runs).</summary>
        public RetentionTelemetryOutbox(IRetentionTelemetryGateway gateway, string rootDirectory = null)
        {
            _gateway = gateway;
            _filePath = Path.Combine(rootDirectory ?? Application.persistentDataPath, FileName);
            Load();
        }

        public void Enqueue(RetentionTelemetryEvent evt)
        {
            if (evt == null) return;
            _queue.Add(evt);
            while (_queue.Count > MaxQueuedEvents) _queue.RemoveAt(0);
            Save();
        }

        /// <summary>Attempts to send every queued event, in order, via the real gateway. Stops at
        /// the first failure (explicit unsuccessful result, thrown exception, or flush deadline)
        /// and leaves that event and everything after it queued - never partially "loses" an event
        /// by removing it before a successful send is confirmed. Returns how many were actually
        /// sent. Caller cancel still propagates; deadline expiry does not.</summary>
        public async Task<int> FlushAsync(CancellationToken cancellationToken)
        {
            int sent = 0;
            while (_queue.Count > 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RetentionTelemetryEvent next = _queue[0];
                RetentionTelemetryGatewayResult result;
                try
                {
                    using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken))
                    {
                        linked.CancelAfter(FlushDeadline);
                        Task<RetentionTelemetryGatewayResult> send =
                            _gateway.SendEventAsync(next, linked.Token);
                        // WhenAny is required even with CancelAfter: the live Cloud Code call does
                        // not honor the token, so CancelAfter alone cannot abort a stalled await.
                        Task winner = await Task.WhenAny(
                                send,
                                Task.Delay(FlushDeadline, cancellationToken))
                            .ConfigureAwait(false);
                        if (winner != send)
                        {
                            cancellationToken.ThrowIfCancellationRequested();
                            break;
                        }

                        result = await send.ConfigureAwait(false);
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    // Deadline (or gateway cancel on the linked token only) - leave the queue.
                    break;
                }
                catch (Exception) when (!cancellationToken.IsCancellationRequested)
                {
                    // Ingestion being down (network, auth, timeout, anything) must never surface
                    // as an exception to whatever called FlushAsync - stop here, the event stays
                    // queued for the next opportunistic flush attempt.
                    break;
                }
                if (result == null || !result.success) break;
                _queue.RemoveAt(0);
                sent++;
                Save();
            }
            return sent;
        }

        private void Load()
        {
            try
            {
                if (File.Exists(_filePath))
                {
                    string json = File.ReadAllText(_filePath);
                    QueueWrapper wrapper = JsonUtility.FromJson<QueueWrapper>(json);
                    _queue = wrapper?.events ?? new List<RetentionTelemetryEvent>();
                    return;
                }
            }
            catch (Exception)
            {
                // A corrupt/unreadable outbox file must never block startup - fall through to a
                // fresh empty queue rather than losing the game session over lost analytics data.
            }
            _queue = new List<RetentionTelemetryEvent>();
        }

        private void Save()
        {
            try
            {
                string directory = Path.GetDirectoryName(_filePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    Directory.CreateDirectory(directory);
                File.WriteAllText(_filePath, JsonUtility.ToJson(new QueueWrapper { events = _queue }));
            }
            catch (Exception)
            {
                // Same principle as Enqueue/FlushAsync's own contract - a failed disk write must
                // never throw into the caller. The event still lives in the in-memory queue for
                // this session even if this particular persist attempt failed.
            }
        }

        [Serializable]
        private sealed class QueueWrapper
        {
            public List<RetentionTelemetryEvent> events = new List<RetentionTelemetryEvent>();
        }
    }
}
