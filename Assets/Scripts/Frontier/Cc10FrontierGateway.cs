using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;

namespace MyriadOfDragons.Frontier
{
    /// <summary>Transport seam for MyriadOfDragons.CloudCode.CC10Frontier.CC10FrontierModule. Tests
    /// substitute a fake; production calls the real, deployed module. Every request dictionary the
    /// client builds mirrors CC10Request's base fields (requestId, expectedStateVersion,
    /// expectedAuthorityGeneration, clientDisplayUtcMs - display-only, never read by a rule -
    /// offlineQueued) plus that endpoint's own fields.</summary>
    public interface ICc10FrontierGateway
    {
        Task<T> CallAsync<T>(string endpoint, Dictionary<string, object> request, CancellationToken cancellationToken)
            where T : Cc10ResultBase;
    }

    public sealed class UnityCloudCodeCc10FrontierGateway : ICc10FrontierGateway
    {
        public async Task<T> CallAsync<T>(string endpoint, Dictionary<string, object> request, CancellationToken cancellationToken)
            where T : Cc10ResultBase
        {
            await UnityServices.InitializeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var args = new Dictionary<string, object>();
            if (request != null && request.Count > 0) args["request"] = request;
            return await CloudCodeService.Instance.CallModuleEndpointAsync<T>(
                Cc10Endpoints.ModuleName, endpoint, args).ConfigureAwait(false);
        }
    }
}
