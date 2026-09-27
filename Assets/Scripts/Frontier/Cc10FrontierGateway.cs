using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;

namespace MyriadOfDragons.Frontier
{
    /// <summary>Transport seam for the CC10FrontierService. Tests substitute a fake; production
    /// uses <see cref="UnityCloudCodeCc10FrontierGateway"/>. One generic call keeps the client free
    /// of per-endpoint transport code.</summary>
    public interface ICc10FrontierGateway
    {
        Task<T> CallAsync<T>(string endpoint, Dictionary<string, object> request, CancellationToken cancellationToken)
            where T : Cc10Response;
    }

    /// <summary>Command response: envelope plus the authoritative wallet/card projection.</summary>
    [System.Serializable]
    public class Cc10CommandResponse : Cc10Response
    {
        public Cc10Projection projection = new Cc10Projection();
    }

    public sealed class UnityCloudCodeCc10FrontierGateway : ICc10FrontierGateway
    {
        public async Task<T> CallAsync<T>(string endpoint, Dictionary<string, object> request, CancellationToken cancellationToken)
            where T : Cc10Response
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
