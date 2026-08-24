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
    public sealed class GuildExpeditionAttemptResult
    {
        public bool success;
        public int remaining;
        public string errorCode;
    }

    [Serializable]
    public sealed class GuildExpeditionObjectiveResult
    {
        public bool success;
        public int pointsAwarded;
        public int totalPoints;
        public string weekKey;
        public bool alreadyScored;
        public string errorCode;
    }

    [Serializable]
    public sealed class GuildExpeditionMilestoneResult
    {
        public bool success;
        public int threshold;
        public int guildContributionGranted;
        public bool alreadyClaimed;
        public string errorCode;
    }

    /// <summary>Real client gateway for the live, deployed GuildExpedition CloudCode module
    /// (nonprod-validation, see docs/LOCKED_DECISIONS_REGISTER.md) - no local stopgap exists for
    /// this system's state (unlike PermitWeekKey's CollectionAscensionPermits), so this is a clean
    /// first wiring, not a migration decision. Request shape matches the module's real parameter
    /// name ("request", wrapping each call's fields) - the same shape bug found and fixed in
    /// SocialSafety's own gateway this session.</summary>
    public interface IGuildExpeditionGateway
    {
        Task<GuildExpeditionAttemptResult> ConsumeAttemptAsync(CancellationToken cancellationToken);
        Task<GuildExpeditionObjectiveResult> SubmitObjectiveResultAsync(string objectiveId, CancellationToken cancellationToken);
        Task<GuildExpeditionMilestoneResult> ClaimMilestoneAsync(int threshold, CancellationToken cancellationToken);
    }

    public sealed class UnityCloudCodeGuildExpeditionGateway : IGuildExpeditionGateway
    {
        private const string ModuleName = "GuildExpedition";

        public async Task<GuildExpeditionAttemptResult> ConsumeAttemptAsync(CancellationToken cancellationToken)
        {
            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<GuildExpeditionAttemptResult>(
                ModuleName, "ConsumeExpeditionAttempt", new Dictionary<string, object>()).ConfigureAwait(false);
        }

        public async Task<GuildExpeditionObjectiveResult> SubmitObjectiveResultAsync(string objectiveId, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(objectiveId))
                return new GuildExpeditionObjectiveResult { errorCode = "INVALID_REQUEST" };

            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<GuildExpeditionObjectiveResult>(
                ModuleName, "SubmitExpeditionObjectiveResult",
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "objectiveId", objectiveId } } } }).ConfigureAwait(false);
        }

        public async Task<GuildExpeditionMilestoneResult> ClaimMilestoneAsync(int threshold, CancellationToken cancellationToken)
        {
            await EnsureSignedInAsync(cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            return await CloudCodeService.Instance.CallModuleEndpointAsync<GuildExpeditionMilestoneResult>(
                ModuleName, "ClaimExpeditionMilestone",
                new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "threshold", threshold } } } }).ConfigureAwait(false);
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
