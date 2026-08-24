using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;

namespace MyriadOfDragons.Empire
{
    [Serializable]
    public sealed class PermitStatusResult
    {
        public int balance;
        public string currentWeekKey;
        public bool claimedThisWeek;
        public int weeklyRate;
        public int hoardCap;
        public string errorCode;
    }

    [Serializable]
    public sealed class PermitClaimResult
    {
        public bool success;
        public int granted;
        public int balance;
        public string weekKey;
        public bool alreadyClaimed;
        public string errorCode;
    }

    /// <summary>Real client gateway for the live, deployed PermitWeekKey CloudCode module
    /// (nonprod-validation, see docs/LOCKED_DECISIONS_REGISTER.md). CC decision 2026-08-24: the
    /// real server module is the source of truth going forward - it's the reason the module was
    /// built. The local client-authoritative stopgap (Save/CollectionAscensionPermits.cs, with its
    /// own placeholder trusted week key) still exists but is NOT touched here - PlayerProfile.cs
    /// is a frozen file, and retiring call sites that use the stopgap is a separate task for
    /// whichever seat owns those call sites, not folded into this gateway's own addition. Once this
    /// gateway is wired into real UI, new weekly-permit claims should go through it instead of
    /// CollectionAscensionPermits.TryGrantWeekly - milestone grants (chapter-finale, hoard-capped
    /// only) are a different, unrelated code path and are unaffected either way.</summary>
    public interface IPermitWeekKeyGateway
    {
        Task<PermitStatusResult> GetStatusAsync(string activityId, CancellationToken cancellationToken);
        Task<PermitClaimResult> ClaimWeeklyAsync(string activityId, CancellationToken cancellationToken);
    }

    public sealed class UnityCloudCodePermitWeekKeyGateway : IPermitWeekKeyGateway
    {
        private const string ModuleName = "PermitWeekKey";

        public async Task<PermitStatusResult> GetStatusAsync(string activityId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(activityId))
                return new PermitStatusResult { errorCode = "INVALID_REQUEST" };

            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<PermitStatusResult>(
                ModuleName, "GetPermitStatus",
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "activityId", activityId } } } }).ConfigureAwait(false);
        }

        public async Task<PermitClaimResult> ClaimWeeklyAsync(string activityId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(activityId))
                return new PermitClaimResult { errorCode = "INVALID_REQUEST" };

            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<PermitClaimResult>(
                ModuleName, "ClaimWeeklyPermit",
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "activityId", activityId } } } }).ConfigureAwait(false);
        }

        private static async Task EnsureSignedInAsync(CancellationToken cancellationToken)
        {
            await UnityServices.InitializeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!AuthenticationService.Instance.IsSignedIn)
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync().ConfigureAwait(false);
            }
        }
    }
}
