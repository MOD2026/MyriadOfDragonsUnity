using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;

namespace MyriadOfDragons.Frontier
{
    /// <summary>Transport seam for MyriadOfDragons.CloudCode.Mail.MailModule - a separate CloudCode
    /// module from CC10Frontier (own module name, own result base). Tests substitute a fake;
    /// production calls the real, deployed module.</summary>
    public interface IMailGateway
    {
        Task<T> CallAsync<T>(string endpoint, Dictionary<string, object> request, CancellationToken cancellationToken)
            where T : MailResult;
    }

    public sealed class UnityCloudCodeMailGateway : IMailGateway
    {
        public async Task<T> CallAsync<T>(string endpoint, Dictionary<string, object> request, CancellationToken cancellationToken)
            where T : MailResult
        {
            await UnityServices.InitializeAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync().ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();

            var args = new Dictionary<string, object>();
            if (request != null && request.Count > 0) args["request"] = request;
            return await CloudCodeService.Instance.CallModuleEndpointAsync<T>(
                MailEndpoints.ModuleName, endpoint, args).ConfigureAwait(false);
        }
    }
}
