using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Unity.Services.Authentication;
using Unity.Services.CloudCode;
using Unity.Services.Core;

namespace MyriadOfDragons.Social
{
    public interface IIdentityBootstrapGateway
    {
        Task<IdentityBootstrapSnapshot> BootstrapAsync(CancellationToken cancellationToken);
    }

    public interface ISocialSafetyGateway
    {
        Task<SocialSafetyGatewayResult> BlockAccountAsync(string targetAccountId, CancellationToken cancellationToken);
        Task<SocialSafetyGatewayResult> UnblockAccountAsync(string targetAccountId, CancellationToken cancellationToken);
        Task<SocialSafetyGatewayResult> MuteAccountAsync(string targetAccountId, CancellationToken cancellationToken);
        Task<SocialSafetyGatewayResult> UnmuteAccountAsync(string targetAccountId, CancellationToken cancellationToken);
    }

    [Serializable]
    public sealed class SocialSafetyGatewayRecord
    {
        public string id;
        public string actorAccountId;
        public string targetAccountId;
        public long createdAtUtcMs;
        public long muteUntilUtcMs;
        public string status;
    }

    [Serializable]
    public sealed class SocialSafetyGatewayResult
    {
        public SocialSafetyGatewayRecord record;
        public bool changed;
    }

    [Serializable]
    public sealed class SocialSafetyGatewayException : Exception
    {
        public SocialSafetyGatewayException(SocialFailureCategory category, string detail = null, Exception innerException = null)
            : base("Social safety gateway request failed.", innerException)
        {
            Category = category;
            Detail = detail;
        }

        public SocialFailureCategory Category { get; }
        public string Detail { get; }
    }

    [Serializable]
    public sealed class IdentityBootstrapSnapshot
    {
        public string PlayerId;
        public bool RestoredCachedSession;
    }

    [Serializable]
    public sealed class IdentityBootstrapException : Exception
    {
        public IdentityBootstrapException(SocialFailureCategory category, string message, string detail = null, Exception innerException = null)
            : base(message, innerException)
        {
            Category = category;
            Detail = detail;
        }

        public SocialFailureCategory Category { get; }
        public string Detail { get; }
    }

    public sealed class UnityAuthenticationSocialService : ISocialService
    {
        private readonly IIdentityBootstrapGateway _gateway;
        private readonly ISocialSafetyGateway _socialSafetyGateway;
        private readonly object _syncRoot = new object();
        private Task<IdentityBootstrapSnapshot> _bootstrapTask;
        private SocialResult<BootstrapIdentityResult> _cachedSuccess;

        public UnityAuthenticationSocialService()
            : this(new UnityAuthenticationIdentityBootstrapGateway(), new UnityCloudCodeSocialSafetyGateway())
        {
        }

        public UnityAuthenticationSocialService(IIdentityBootstrapGateway gateway)
            : this(gateway, new UnityCloudCodeSocialSafetyGateway())
        {
        }

        public UnityAuthenticationSocialService(IIdentityBootstrapGateway gateway, ISocialSafetyGateway socialSafetyGateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _socialSafetyGateway = socialSafetyGateway ?? throw new ArgumentNullException(nameof(socialSafetyGateway));
        }

        public Task<SocialResult<BootstrapIdentityResult>> BootstrapIdentityAsync(BootstrapIdentityRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();

            ValidationResult validation = ValidateBootstrapIdentityRequest(request);
            if (!validation.IsValid)
            {
                return Task.FromResult(CreateFailure<BootstrapIdentityResult>(
                    SocialFailureCategory.Validation,
                    "Identity bootstrap request is invalid.",
                    string.Join(" ", validation.Errors)));
            }

            Task<IdentityBootstrapSnapshot> bootstrapTask;
            IdentityBootstrapException synchronousBootstrapException = null;
            Exception synchronousUnexpectedException = null;
            lock (_syncRoot)
            {
                if (_cachedSuccess != null)
                {
                    return Task.FromResult(Clone(_cachedSuccess));
                }

                if (_bootstrapTask == null || _bootstrapTask.IsCanceled || _bootstrapTask.IsFaulted)
                {
                    _bootstrapTask = null;
                    try
                    {
                        _bootstrapTask = _gateway.BootstrapAsync(CancellationToken.None);
                    }
                    catch (OperationCanceledException)
                    {
                        _bootstrapTask = null;
                        throw;
                    }
                    catch (IdentityBootstrapException exception)
                    {
                        _bootstrapTask = null;
                        synchronousBootstrapException = exception;
                    }
                    catch (Exception exception)
                    {
                        _bootstrapTask = null;
                        synchronousUnexpectedException = exception;
                    }
                }

                bootstrapTask = _bootstrapTask;
            }

            if (synchronousBootstrapException != null)
            {
                return Task.FromResult(CreateFailure<BootstrapIdentityResult>(
                    synchronousBootstrapException.Category,
                    synchronousBootstrapException.Message,
                    synchronousBootstrapException.Detail));
            }

            if (synchronousUnexpectedException != null)
            {
                return Task.FromResult(CreateFailure<BootstrapIdentityResult>(
                    SocialFailureCategory.Unknown,
                    "Unexpected identity bootstrap failure.",
                    IdentityBootstrapFailureDetails.UnexpectedFailure));
            }

            return AwaitBootstrapAsync(bootstrapTask, request, cancellationToken);
        }

        public Task<SocialResult<CreateGuildResult>> CreateGuildAsync(CreateGuildRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<JoinGuildResult>> JoinGuildAsync(JoinGuildRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<LeaveGuildResult>> LeaveGuildAsync(LeaveGuildRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<FetchMembershipResult>> FetchMembershipAsync(FetchMembershipRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<FetchChatHistoryResult>> FetchChatHistoryAsync(FetchChatHistoryRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<SendChatMessageResult>> SendChatMessageAsync(SendChatMessageRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<BlockAccountResult>> BlockAccountAsync(BlockAccountRequest request, CancellationToken cancellationToken)
        {
            return ExecuteBlockAsync(request, cancellationToken);
        }

        public Task<SocialResult<UnblockAccountResult>> UnblockAccountAsync(UnblockAccountRequest request, CancellationToken cancellationToken)
        {
            return ExecuteUnblockAsync(request, cancellationToken);
        }

        public Task<SocialResult<MuteAccountResult>> MuteAccountAsync(MuteAccountRequest request, CancellationToken cancellationToken)
        {
            return ExecuteMuteAsync(request, cancellationToken);
        }

        public Task<SocialResult<UnmuteAccountResult>> UnmuteAccountAsync(UnmuteAccountRequest request, CancellationToken cancellationToken)
        {
            return ExecuteUnmuteAsync(request, cancellationToken);
        }

        public Task<SocialResult<SubmitReportResult>> SubmitReportAsync(SubmitReportRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<TransferGuildOwnershipResult>> TransferGuildOwnershipAsync(TransferGuildOwnershipRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<RemoveGuildMemberResult>> RemoveGuildMemberAsync(RemoveGuildMemberRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<AssignGuildRoleResult>> AssignGuildRoleAsync(AssignGuildRoleRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<DisbandGuildResult>> DisbandGuildAsync(DisbandGuildRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<ModerateChatMessageResult>> ModerateChatMessageAsync(ModerateChatMessageRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<ReviewReportResult>> ReviewReportAsync(ReviewReportRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<DirectMessagePrivacySettingsResult>> GetDirectMessagePrivacySettingsAsync(GetDirectMessagePrivacySettingsRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<CreateDirectMessageRequestResult>> CreateDirectMessageRequestAsync(CreateDirectMessageRequestRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<ListDirectMessageRequestsResult>> ListDirectMessageRequestsAsync(ListDirectMessageRequestsRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<AcceptDirectMessageRequestResult>> AcceptDirectMessageRequestAsync(AcceptDirectMessageRequestRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<RejectDirectMessageRequestResult>> RejectDirectMessageRequestAsync(RejectDirectMessageRequestRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<CancelDirectMessageRequestResult>> CancelDirectMessageRequestAsync(CancelDirectMessageRequestRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<ListDirectMessageConversationsResult>> ListDirectMessageConversationsAsync(ListDirectMessageConversationsRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<FetchDirectMessageHistoryResult>> FetchDirectMessageHistoryAsync(FetchDirectMessageHistoryRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<SendDirectMessageResult>> SendDirectMessageAsync(SendDirectMessageRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<MuteDirectMessageConversationResult>> MuteDirectMessageConversationAsync(MuteDirectMessageConversationRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<UnmuteDirectMessageConversationResult>> UnmuteDirectMessageConversationAsync(UnmuteDirectMessageConversationRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        public Task<SocialResult<HideDirectMessageConversationResult>> HideDirectMessageConversationAsync(HideDirectMessageConversationRequest request, CancellationToken cancellationToken)
        {
            throw new NotImplementedException();
        }

        private async Task<SocialResult<BlockAccountResult>> ExecuteBlockAsync(BlockAccountRequest request, CancellationToken cancellationToken)
        {
            SocialResult<SocialSafetyGatewayResult> result = await ExecuteSafetyCallAsync(request?.blockedAccountId, cancellationToken, _socialSafetyGateway.BlockAccountAsync).ConfigureAwait(false);
            return MapBlockResult(result);
        }

        private async Task<SocialResult<UnblockAccountResult>> ExecuteUnblockAsync(UnblockAccountRequest request, CancellationToken cancellationToken)
        {
            SocialResult<SocialSafetyGatewayResult> result = await ExecuteSafetyCallAsync(request?.blockedAccountId, cancellationToken, _socialSafetyGateway.UnblockAccountAsync).ConfigureAwait(false);
            if (!result.Success)
            {
                return CreateFailure<UnblockAccountResult>(result.Failure.Category, result.Failure.Message, result.Failure.Detail);
            }

            return new SocialResult<UnblockAccountResult>
            {
                Success = true,
                Data = new UnblockAccountResult { unblocked = result.Data != null && (result.Data.record == null || string.Equals(result.Data.record.status, "inactive", StringComparison.Ordinal)) }
            };
        }

        private async Task<SocialResult<MuteAccountResult>> ExecuteMuteAsync(MuteAccountRequest request, CancellationToken cancellationToken)
        {
            SocialResult<SocialSafetyGatewayResult> result = await ExecuteSafetyCallAsync(request?.mutedAccountId, cancellationToken, _socialSafetyGateway.MuteAccountAsync).ConfigureAwait(false);
            if (!result.Success)
            {
                return CreateFailure<MuteAccountResult>(result.Failure.Category, result.Failure.Message, result.Failure.Detail);
            }

            return new SocialResult<MuteAccountResult>
            {
                Success = true,
                Data = new MuteAccountResult { mute = MapMuteRecord(result.Data.record) }
            };
        }

        private async Task<SocialResult<UnmuteAccountResult>> ExecuteUnmuteAsync(UnmuteAccountRequest request, CancellationToken cancellationToken)
        {
            SocialResult<SocialSafetyGatewayResult> result = await ExecuteSafetyCallAsync(request?.mutedAccountId, cancellationToken, _socialSafetyGateway.UnmuteAccountAsync).ConfigureAwait(false);
            if (!result.Success)
            {
                return CreateFailure<UnmuteAccountResult>(result.Failure.Category, result.Failure.Message, result.Failure.Detail);
            }

            return new SocialResult<UnmuteAccountResult>
            {
                Success = true,
                Data = new UnmuteAccountResult { unmuted = result.Data != null && (result.Data.record == null || string.Equals(result.Data.record.status, "inactive", StringComparison.Ordinal)) }
            };
        }

        private async Task<SocialResult<SocialSafetyGatewayResult>> ExecuteSafetyCallAsync(
            string targetAccountId,
            CancellationToken cancellationToken,
            Func<string, CancellationToken, Task<SocialSafetyGatewayResult>> operation)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidationResult validation = SocialValidation.ValidateOpaqueId(targetAccountId, nameof(targetAccountId));
            if (!validation.IsValid)
            {
                return CreateFailure<SocialSafetyGatewayResult>(SocialFailureCategory.Validation, "Social safety request is invalid.", SocialSafetyFailureDetails.ValidationFailure);
            }

            SocialResult<BootstrapIdentityResult> authentication = await BootstrapIdentityAsync(new BootstrapIdentityRequest { requestedAtUtcMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() }, cancellationToken).ConfigureAwait(false);
            if (!authentication.Success)
            {
                return CreateFailure<SocialSafetyGatewayResult>(authentication.Failure.Category, authentication.Failure.Message, authentication.Failure.Detail);
            }

            if (string.Equals(authentication.Data.account.accountId, targetAccountId.Trim(), StringComparison.Ordinal))
            {
                return CreateFailure<SocialSafetyGatewayResult>(SocialFailureCategory.Validation, "Social safety request is invalid.", SocialSafetyFailureDetails.ValidationFailure);
            }

            try
            {
                SocialSafetyGatewayResult result = await operation(targetAccountId.Trim(), cancellationToken).ConfigureAwait(false);
                if (result == null)
                {
                    return CreateFailure<SocialSafetyGatewayResult>(SocialFailureCategory.Unavailable, "Social safety service is unavailable.", SocialSafetyFailureDetails.ServiceUnavailable);
                }

                return new SocialResult<SocialSafetyGatewayResult> { Success = true, Data = result };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SocialSafetyGatewayException exception)
            {
                return CreateFailure<SocialSafetyGatewayResult>(exception.Category, GetFailureMessage(exception.Category), GetFailureDetail(exception.Category));
            }
            catch (Exception)
            {
                return CreateFailure<SocialSafetyGatewayResult>(SocialFailureCategory.Unknown, GetFailureMessage(SocialFailureCategory.Unknown), SocialSafetyFailureDetails.UnexpectedFailure);
            }
        }

        private static string GetFailureDetail(SocialFailureCategory category)
        {
            switch (category)
            {
                case SocialFailureCategory.Authentication: return SocialSafetyFailureDetails.AuthenticationFailure;
                case SocialFailureCategory.Authorization: return SocialSafetyFailureDetails.AuthorizationFailure;
                case SocialFailureCategory.Validation: return SocialSafetyFailureDetails.ValidationFailure;
                case SocialFailureCategory.Conflict: return SocialSafetyFailureDetails.ConflictFailure;
                case SocialFailureCategory.RateLimit: return SocialSafetyFailureDetails.RateLimitFailure;
                case SocialFailureCategory.Unavailable: return SocialSafetyFailureDetails.ServiceUnavailable;
                default: return SocialSafetyFailureDetails.UnexpectedFailure;
            }
        }

        private static SocialResult<BlockAccountResult> MapBlockResult(SocialResult<SocialSafetyGatewayResult> result)
        {
            if (!result.Success)
            {
                return CreateFailure<BlockAccountResult>(result.Failure.Category, result.Failure.Message, result.Failure.Detail);
            }

            return new SocialResult<BlockAccountResult>
            {
                Success = true,
                Data = new BlockAccountResult { block = MapBlockRecord(result.Data.record) }
            };
        }

        private static BlockRecord MapBlockRecord(SocialSafetyGatewayRecord source)
        {
            return source == null ? null : new BlockRecord
            {
                blockId = source.id,
                blockerAccountId = source.actorAccountId,
                blockedAccountId = source.targetAccountId,
                createdAtUtcMs = source.createdAtUtcMs,
                status = source.status
            };
        }

        private static MuteRecord MapMuteRecord(SocialSafetyGatewayRecord source)
        {
            return source == null ? null : new MuteRecord
            {
                muteId = source.id,
                muterAccountId = source.actorAccountId,
                mutedAccountId = source.targetAccountId,
                createdAtUtcMs = source.createdAtUtcMs,
                muteUntilUtcMs = source.muteUntilUtcMs,
                status = source.status
            };
        }

        private static string GetFailureMessage(SocialFailureCategory category)
        {
            switch (category)
            {
                case SocialFailureCategory.Authentication: return "Authentication is required.";
                case SocialFailureCategory.Authorization: return "This action is not allowed.";
                case SocialFailureCategory.Validation: return "Social safety request is invalid.";
                case SocialFailureCategory.Conflict: return "The social safety state changed. Try again.";
                case SocialFailureCategory.RateLimit: return "Too many social safety requests. Try again later.";
                case SocialFailureCategory.Unavailable: return "Social safety service is unavailable.";
                default: return "Social safety request failed.";
            }
        }

        private async Task<SocialResult<BootstrapIdentityResult>> AwaitBootstrapAsync(Task<IdentityBootstrapSnapshot> bootstrapTask, BootstrapIdentityRequest request, CancellationToken cancellationToken)
        {
            try
            {
                IdentityBootstrapSnapshot snapshot = await AwaitWithCancellationAsync(bootstrapTask, cancellationToken).ConfigureAwait(false);
                SocialResult<BootstrapIdentityResult> success = BuildSuccessResult(snapshot, request);

                lock (_syncRoot)
                {
                    if (_cachedSuccess == null)
                    {
                        _cachedSuccess = Clone(success);
                    }

                    if (ReferenceEquals(_bootstrapTask, bootstrapTask))
                    {
                        _bootstrapTask = null;
                    }
                }

                return Clone(success);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (IdentityBootstrapException exception)
            {
                ClearBootstrapTaskIfCurrent(bootstrapTask);
                return CreateFailure<BootstrapIdentityResult>(exception.Category, exception.Message, exception.Detail);
            }
            catch (Exception exception)
            {
                ClearBootstrapTaskIfCurrent(bootstrapTask);
                return CreateFailure<BootstrapIdentityResult>(SocialFailureCategory.Unknown, "Unexpected identity bootstrap failure.", IdentityBootstrapFailureDetails.UnexpectedFailure);
            }
        }

        private void ClearBootstrapTaskIfCurrent(Task<IdentityBootstrapSnapshot> bootstrapTask)
        {
            lock (_syncRoot)
            {
                if (ReferenceEquals(_bootstrapTask, bootstrapTask))
                {
                    _bootstrapTask = null;
                }
            }
        }

        private static ValidationResult ValidateBootstrapIdentityRequest(BootstrapIdentityRequest request)
        {
            var errors = new List<string>(SocialValidation.ValidateRequest(request, nameof(request)).Errors);

            if (request == null)
            {
                return new ValidationResult(errors);
            }

            errors.AddRange(SocialValidation.ValidateTimestamp(request.requestedAtUtcMs, nameof(request.requestedAtUtcMs)).Errors);

            if (!string.IsNullOrWhiteSpace(request.displayName))
            {
                errors.AddRange(SocialValidation.ValidateDisplayName(request.displayName).Errors);
            }

            return new ValidationResult(errors);
        }

        private static SocialResult<BootstrapIdentityResult> BuildSuccessResult(IdentityBootstrapSnapshot snapshot, BootstrapIdentityRequest request)
        {
            if (snapshot == null)
            {
                throw new IdentityBootstrapException(SocialFailureCategory.Unavailable, "Unity authentication did not return an identity.", "Snapshot was null.");
            }

            if (string.IsNullOrWhiteSpace(snapshot.PlayerId))
            {
                throw new IdentityBootstrapException(SocialFailureCategory.Validation, "Unity authentication returned an invalid player id.", "PlayerId was empty.");
            }

            AccountIdentity account = new AccountIdentity
            {
                accountId = snapshot.PlayerId.Trim(),
                displayName = string.IsNullOrWhiteSpace(request.displayName) ? string.Empty : request.displayName.Trim(),
                createdAtUtcMs = request.requestedAtUtcMs,
                lastSeenAtUtcMs = request.requestedAtUtcMs,
                status = snapshot.RestoredCachedSession ? "Restored" : "Created"
            };

            return new SocialResult<BootstrapIdentityResult>
            {
                Success = true,
                Data = new BootstrapIdentityResult
                {
                    account = account,
                    created = !snapshot.RestoredCachedSession
                }
            };
        }

        private static SocialResult<T> CreateFailure<T>(SocialFailureCategory category, string message, string detail = null)
        {
            return new SocialResult<T>
            {
                Success = false,
                Failure = new SocialFailure
                {
                    Category = category,
                    Message = message,
                    Detail = detail
                }
            };
        }

        private static SocialResult<BootstrapIdentityResult> Clone(SocialResult<BootstrapIdentityResult> source)
        {
            if (source == null)
            {
                return null;
            }

            return new SocialResult<BootstrapIdentityResult>
            {
                Success = source.Success,
                Failure = source.Failure == null
                    ? null
                    : new SocialFailure
                    {
                        Category = source.Failure.Category,
                        Message = source.Failure.Message,
                        Detail = source.Failure.Detail
                    },
                Data = source.Data == null
                    ? null
                    : new BootstrapIdentityResult
                    {
                        created = source.Data.created,
                        account = source.Data.account == null
                            ? null
                            : new AccountIdentity
                            {
                                accountId = source.Data.account.accountId,
                                displayName = source.Data.account.displayName,
                                createdAtUtcMs = source.Data.account.createdAtUtcMs,
                                lastSeenAtUtcMs = source.Data.account.lastSeenAtUtcMs,
                                status = source.Data.account.status
                            }
                    }
            };
        }

        private static async Task<T> AwaitWithCancellationAsync<T>(Task<T> task, CancellationToken cancellationToken)
        {
            if (!cancellationToken.CanBeCanceled)
            {
                return await task.ConfigureAwait(false);
            }

            if (task.IsCompleted)
            {
                return await task.ConfigureAwait(false);
            }

            var cancellationTask = new TaskCompletionSource<bool>();
            using (cancellationToken.Register(state => ((TaskCompletionSource<bool>)state).TrySetResult(true), cancellationTask))
            {
                if (task != await Task.WhenAny(task, cancellationTask.Task).ConfigureAwait(false))
                {
                    throw new OperationCanceledException(cancellationToken);
                }
            }

            return await task.ConfigureAwait(false);
        }
    }

    internal static class IdentityBootstrapFailureDetails
    {
        public const string AuthenticationFailure = "UNITY_AUTH_AUTHENTICATION_FAILURE";
        public const string ServiceUnavailable = "UNITY_AUTH_SERVICE_UNAVAILABLE";
        public const string ValidationFailure = "UNITY_AUTH_VALIDATION_FAILURE";
        public const string UnexpectedFailure = "UNITY_AUTH_UNEXPECTED_FAILURE";
    }

    internal static class SocialSafetyFailureDetails
    {
        public const string AuthenticationFailure = "SOCIAL_SAFETY_AUTHENTICATION_FAILURE";
        public const string AuthorizationFailure = "SOCIAL_SAFETY_AUTHORIZATION_FAILURE";
        public const string ValidationFailure = "SOCIAL_SAFETY_VALIDATION_FAILURE";
        public const string ConflictFailure = "SOCIAL_SAFETY_CONFLICT_FAILURE";
        public const string RateLimitFailure = "SOCIAL_SAFETY_RATE_LIMIT_FAILURE";
        public const string ServiceUnavailable = "SOCIAL_SAFETY_SERVICE_UNAVAILABLE";
        public const string UnexpectedFailure = "SOCIAL_SAFETY_UNEXPECTED_FAILURE";
    }

    [Serializable]
    internal sealed class SocialSafetyGatewayResponse
    {
        public bool success;
        public SocialSafetyGatewayRecord relationship;
        public bool changed;
        public string errorCode;
    }

    internal sealed class UnityCloudCodeSocialSafetyGateway : ISocialSafetyGateway
    {
        private const string ModuleName = "SocialSafety";

        public Task<SocialSafetyGatewayResult> BlockAccountAsync(string targetAccountId, CancellationToken cancellationToken)
        {
            return CallAsync("BlockAccount", targetAccountId, cancellationToken);
        }

        public Task<SocialSafetyGatewayResult> UnblockAccountAsync(string targetAccountId, CancellationToken cancellationToken)
        {
            return CallAsync("UnblockAccount", targetAccountId, cancellationToken);
        }

        public Task<SocialSafetyGatewayResult> MuteAccountAsync(string targetAccountId, CancellationToken cancellationToken)
        {
            return CallAsync("MuteAccount", targetAccountId, cancellationToken);
        }

        public Task<SocialSafetyGatewayResult> UnmuteAccountAsync(string targetAccountId, CancellationToken cancellationToken)
        {
            return CallAsync("UnmuteAccount", targetAccountId, cancellationToken);
        }

        private static async Task<SocialSafetyGatewayResult> CallAsync(string functionName, string targetAccountId, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                SocialSafetyGatewayResponse response = await CloudCodeService.Instance.CallModuleEndpointAsync<SocialSafetyGatewayResponse>(
                    ModuleName,
                    functionName,
                    new Dictionary<string, object> { { "request", new Dictionary<string, object> { { "targetAccountId", targetAccountId } } } }).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();

                if (response == null || !response.success)
                {
                    throw new SocialSafetyGatewayException(ClassifyError(response?.errorCode), DetailFor(response?.errorCode));
                }

                return new SocialSafetyGatewayResult { record = response.relationship, changed = response.changed };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (SocialSafetyGatewayException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new SocialSafetyGatewayException(ClassifySdkException(exception), DetailFor(ClassifySdkException(exception).ToString()), exception);
            }
        }

        private static SocialFailureCategory ClassifyError(string errorCode)
        {
            switch (errorCode)
            {
                case "AUTHENTICATION_REQUIRED": return SocialFailureCategory.Authentication;
                case "AUTHORIZATION_REQUIRED": return SocialFailureCategory.Authorization;
                case "INVALID_REQUEST":
                case "SELF_TARGET_NOT_ALLOWED": return SocialFailureCategory.Validation;
                case "CONFLICT": return SocialFailureCategory.Conflict;
                case "RATE_LIMITED": return SocialFailureCategory.RateLimit;
                case "STORAGE_UNAVAILABLE": return SocialFailureCategory.Unavailable;
                default: return SocialFailureCategory.Unknown;
            }
        }

        private static SocialFailureCategory ClassifySdkException(Exception exception)
        {
            string text = exception == null ? string.Empty : exception.GetType().Name + " " + exception.Message;
            if (text.IndexOf("401", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Authentication", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SocialFailureCategory.Authentication;
            }

            if (text.IndexOf("403", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Forbidden", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SocialFailureCategory.Authorization;
            }

            if (text.IndexOf("400", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Validation", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SocialFailureCategory.Validation;
            }

            if (text.IndexOf("409", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Conflict", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SocialFailureCategory.Conflict;
            }

            if (text.IndexOf("429", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Rate", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SocialFailureCategory.RateLimit;
            }

            if (text.IndexOf("Timeout", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Network", StringComparison.OrdinalIgnoreCase) >= 0 || text.IndexOf("Unavailable", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return SocialFailureCategory.Unavailable;
            }

            return SocialFailureCategory.Unknown;
        }

        private static string DetailFor(string value)
        {
            switch (value)
            {
                case "AUTHENTICATION_REQUIRED": return SocialSafetyFailureDetails.AuthenticationFailure;
                case "AUTHORIZATION_REQUIRED": return SocialSafetyFailureDetails.AuthorizationFailure;
                case "INVALID_REQUEST":
                case "SELF_TARGET_NOT_ALLOWED": return SocialSafetyFailureDetails.ValidationFailure;
                case "CONFLICT": return SocialSafetyFailureDetails.ConflictFailure;
                case "RATE_LIMITED": return SocialSafetyFailureDetails.RateLimitFailure;
                case "STORAGE_UNAVAILABLE": return SocialSafetyFailureDetails.ServiceUnavailable;
                default: return SocialSafetyFailureDetails.UnexpectedFailure;
            }
        }
    }

    public static class UnityAuthenticationFailureClassifier
    {
        public static IdentityBootstrapException Classify(Exception exception)
        {
            if (exception == null)
            {
                throw new ArgumentNullException(nameof(exception));
            }

            if (IsUnavailableException(exception))
            {
                return new IdentityBootstrapException(
                    SocialFailureCategory.Unavailable,
                    "Unity authentication is unavailable.",
                    IdentityBootstrapFailureDetails.ServiceUnavailable,
                    exception);
            }

            if (IsAuthenticationException(exception))
            {
                return new IdentityBootstrapException(
                    SocialFailureCategory.Authentication,
                    "Unity authentication failed.",
                    IdentityBootstrapFailureDetails.AuthenticationFailure,
                    exception);
            }

            if (IsValidationException(exception))
            {
                return new IdentityBootstrapException(
                    SocialFailureCategory.Validation,
                    "Unity authentication rejected the identity bootstrap request.",
                    IdentityBootstrapFailureDetails.ValidationFailure,
                    exception);
            }

            return new IdentityBootstrapException(
                SocialFailureCategory.Unknown,
                "Unexpected Unity authentication failure.",
                IdentityBootstrapFailureDetails.UnexpectedFailure,
                exception);
        }

        private static bool IsAuthenticationException(Exception exception)
        {
            string text = GetExceptionText(exception);
            return text.IndexOf("Authentication", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Unauthenticated", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("InvalidUserState", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsUnavailableException(Exception exception)
        {
            string text = GetExceptionText(exception);
            return text.IndexOf("Unavailable", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Timeout", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Network", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("ServiceUnavailable", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("RequestFailed", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Connection", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static bool IsValidationException(Exception exception)
        {
            string text = GetExceptionText(exception);
            return text.IndexOf("Validation", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("InvalidParameter", StringComparison.OrdinalIgnoreCase) >= 0
                || text.IndexOf("Empty", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string GetExceptionText(Exception exception)
        {
            return string.Concat(
                exception.GetType().Name,
                " ",
                exception.Message ?? string.Empty,
                " ",
                exception.InnerException?.GetType().Name ?? string.Empty);
        }
    }

    internal sealed class UnityAuthenticationIdentityBootstrapGateway : IIdentityBootstrapGateway
    {
        private static readonly object ServicesSyncRoot = new object();
        private static Task _servicesInitializationTask;

        public async Task<IdentityBootstrapSnapshot> BootstrapAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await EnsureUnityServicesInitializedAsync().ConfigureAwait(false);

            if (TryGetSignedInPlayerId(out string signedInPlayerId))
            {
                return new IdentityBootstrapSnapshot
                {
                    PlayerId = signedInPlayerId,
                    RestoredCachedSession = true
                };
            }

            await SignInAnonymouslyAsync().ConfigureAwait(false);

            return new IdentityBootstrapSnapshot
            {
                PlayerId = RequireSignedInPlayerId(),
                RestoredCachedSession = false
            };
        }

        private static async Task EnsureUnityServicesInitializedAsync()
        {
            Task initializationTask;
            lock (ServicesSyncRoot)
            {
                if (_servicesInitializationTask == null || _servicesInitializationTask.IsCanceled || _servicesInitializationTask.IsFaulted)
                {
                    _servicesInitializationTask = UnityServices.InitializeAsync();
                }

                initializationTask = _servicesInitializationTask;
            }

            try
            {
                await initializationTask.ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw MapException(exception);
            }
        }

        private static async Task SignInAnonymouslyAsync()
        {
            try
            {
                await AuthenticationService.Instance.SignInAnonymouslyAsync().ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                throw MapException(exception);
            }
        }

        private static bool TryGetSignedInPlayerId(out string playerId)
        {
            playerId = AuthenticationService.Instance.PlayerId;
            return AuthenticationService.Instance.IsSignedIn && !string.IsNullOrWhiteSpace(playerId);
        }

        private static string RequireSignedInPlayerId()
        {
            if (TryGetSignedInPlayerId(out string playerId))
            {
                return playerId.Trim();
            }

            throw new IdentityBootstrapException(SocialFailureCategory.Validation, "Unity authentication did not expose a valid player id.", "PlayerId was missing after sign-in.");
        }

        private static IdentityBootstrapException MapException(Exception exception)
        {
            return UnityAuthenticationFailureClassifier.Classify(exception);
        }
    }
}