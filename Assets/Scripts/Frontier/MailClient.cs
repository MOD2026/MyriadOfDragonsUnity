using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace MyriadOfDragons.Frontier
{
    /// <summary>Outcome of the most recent FetchMailInbox attempt. Failure keeps the last accepted
    /// inbox visible, read-only - the offline cached state the task requires - it never clears it.</summary>
    public enum MailInboxRefresh { NotAttempted, Accepted, Offline, Malformed }

    public enum MailOutcome { Applied, AlreadyRead, Offline, Rejected, InFlight, Failed }

    public sealed class MailMarkReadOutcome
    {
        public MailOutcome Outcome;
        public string Message = string.Empty;
        public MarkMailReadResult Response;
    }

    /// <summary>
    /// Presentation-side state holder for the beta Mail client (BE c2e7831b, WH-CC11-042). Holds
    /// the last authoritative inbox, gates mutation while offline, and never computes expiry,
    /// eligibility, or unread count itself - every one of those is server-owned and only ever
    /// echoed. No attachments/rewards/player-mail/threads/delete-restore/reporting exist on the
    /// real contract, so none exist here.
    /// </summary>
    public sealed class MailClient
    {
        private readonly IMailGateway _gateway;
        private readonly HashSet<string> _inFlight = new HashSet<string>();

        public MailClient(IMailGateway gateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        /// <summary>Last accepted FetchMailInbox result, or null if never loaded. Kept as-is
        /// (read-only) on any later refresh failure - this is the offline cached state.</summary>
        public FetchMailInboxResult Inbox { get; private set; }
        public MailInboxRefresh LastInboxRefresh { get; private set; } = MailInboxRefresh.NotAttempted;
        public Cc10Connection Connection { get; private set; } = Cc10Connection.Unknown;

        /// <summary>Server-reported unread count from the last accepted inbox - never counted
        /// locally.</summary>
        public int UnreadCount => Inbox?.unreadCount ?? 0;

        public event Action Changed;
        private void RaiseChanged() => Changed?.Invoke();

        /// <summary>Reload the inbox. Returns false when offline, malformed, or unsuccessful; the
        /// last accepted inbox stays visible, read-only.</summary>
        public async Task<bool> RefreshInboxAsync(CancellationToken cancellationToken = default)
        {
            FetchMailInboxResult fresh;
            try
            {
                fresh = await _gateway.CallAsync<FetchMailInboxResult>(MailEndpoints.FetchMailInbox, null, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Connection = Cc10Connection.Offline;
                LastInboxRefresh = MailInboxRefresh.Offline;
                RaiseChanged();
                return false;
            }

            if (fresh == null || !fresh.success)
            {
                LastInboxRefresh = fresh == null ? MailInboxRefresh.Malformed : MailInboxRefresh.Offline;
                RaiseChanged();
                return false;
            }

            Connection = Cc10Connection.Online;
            Inbox = fresh;
            LastInboxRefresh = MailInboxRefresh.Accepted;
            RaiseChanged();
            return true;
        }

        /// <summary>Fetches one message's full body. Offline fails closed - there is no cached
        /// detail to fall back to (only summaries are cached from the inbox) - never serves a
        /// stale or invented body. MAIL_EXPIRED/MAIL_NOT_FOUND/STORAGE_UNAVAILABLE are returned to
        /// the caller verbatim, exactly as the server reported them.</summary>
        public async Task<FetchMailDetailResult> FetchMailDetailAsync(string messageId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(messageId))
                return new FetchMailDetailResult { success = false, errorCode = MailErrors.InvalidRequest };
            if (Connection != Cc10Connection.Online)
                return new FetchMailDetailResult { success = false, errorCode = string.Empty };

            var request = new Dictionary<string, object> { ["messageId"] = messageId };
            FetchMailDetailResult response;
            try
            {
                response = await _gateway.CallAsync<FetchMailDetailResult>(MailEndpoints.FetchMailDetail, request, cancellationToken);
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                Connection = Cc10Connection.Offline;
                RaiseChanged();
                return new FetchMailDetailResult { success = false, errorCode = string.Empty };
            }

            if (response == null)
            {
                Connection = Cc10Connection.Offline;
                RaiseChanged();
                return new FetchMailDetailResult { success = false, errorCode = string.Empty };
            }

            return response;
        }

        /// <summary>Marks one message read. Offline, in-flight, or a blank messageId all fail
        /// closed before any request is sent - never an optimistic local flip. A repeat call on an
        /// already-read message is idempotent: the server answers success + alreadyRead, which
        /// this client surfaces as <see cref="MailOutcome.AlreadyRead"/>, never as an error. Either
        /// outcome reloads the inbox so UnreadCount stays server-authoritative.</summary>
        public async Task<MailMarkReadOutcome> MarkMailReadAsync(string messageId, CancellationToken cancellationToken = default)
        {
            if (Connection != Cc10Connection.Online)
                return new MailMarkReadOutcome { Outcome = MailOutcome.Offline, Message = MailCopy.Offline };
            if (string.IsNullOrEmpty(messageId))
                return new MailMarkReadOutcome { Outcome = MailOutcome.Rejected, Message = MailCopy.ForRejection(MailErrors.InvalidRequest) };

            string key = "MarkMailRead|" + messageId;
            if (!_inFlight.Add(key))
                return new MailMarkReadOutcome { Outcome = MailOutcome.InFlight, Message = MailCopy.InFlight };

            try
            {
                var request = new Dictionary<string, object> { ["messageId"] = messageId };
                MarkMailReadResult response;
                try
                {
                    response = await _gateway.CallAsync<MarkMailReadResult>(MailEndpoints.MarkMailRead, request, cancellationToken);
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception)
                {
                    Connection = Cc10Connection.Offline;
                    RaiseChanged();
                    return new MailMarkReadOutcome { Outcome = MailOutcome.Failed, Message = MailCopy.ConnectionLost };
                }

                if (response == null)
                {
                    Connection = Cc10Connection.Offline;
                    RaiseChanged();
                    return new MailMarkReadOutcome { Outcome = MailOutcome.Failed, Message = MailCopy.ConnectionLost };
                }

                if (response.success)
                {
                    await RefreshInboxAsync(cancellationToken); // authoritative unreadCount/read flag, never set locally
                    return new MailMarkReadOutcome
                    {
                        Outcome = response.alreadyRead ? MailOutcome.AlreadyRead : MailOutcome.Applied,
                        Response = response,
                    };
                }

                return new MailMarkReadOutcome { Outcome = MailOutcome.Rejected, Message = MailCopy.ForRejection(response.errorCode), Response = response };
            }
            finally
            {
                _inFlight.Remove(key);
            }
        }
    }

    /// <summary>Player-facing copy. Never surfaces a raw MailErrors.* code.</summary>
    public static class MailCopy
    {
        public const string Offline = "You're offline. Reconnect to continue.";
        public const string ConnectionLost = "Connection lost. Retry to continue.";
        public const string InFlight = "Working on it...";
        public const string InvalidRequestMessage = "That request wasn't valid.";
        public const string MailNotFoundMessage = "That message is no longer available.";
        public const string MailExpiredMessage = "That message has expired.";
        public const string RateLimitedMessage = "Slow down a little before marking more as read.";
        public const string Generic = "That didn't go through.";

        public static string ForRejection(string errorCode)
        {
            if (errorCode == MailErrors.InvalidRequest) return InvalidRequestMessage;
            if (errorCode == MailErrors.MailNotFound) return MailNotFoundMessage;
            if (errorCode == MailErrors.MailExpired) return MailExpiredMessage;
            if (errorCode == MailErrors.RateLimited) return RateLimitedMessage;
            return Generic;
        }
    }
}
