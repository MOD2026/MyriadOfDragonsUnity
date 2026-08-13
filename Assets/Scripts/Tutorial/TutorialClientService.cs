using System;
using System.Threading;
using System.Threading.Tasks;

namespace MyriadOfDragons.Tutorial
{
    [Serializable]
    public sealed class FetchTutorialStateRequest
    {
    }

    [Serializable]
    public sealed class FetchTutorialResumeInstructionRequest
    {
    }

    [Serializable]
    public sealed class FetchTutorialResumeInstructionResult
    {
        public bool Success;
        public TutorialBattleResumeInstruction Instruction;
        public TutorialFailure Failure;
    }

    public interface ITutorialGateway
    {
        Task<TutorialStateResult> FetchAuthoritativeStateAsync(FetchTutorialStateRequest request, CancellationToken cancellationToken);
        Task<AcknowledgeTutorialCheckpointResult> AcknowledgeCheckpointAsync(AcknowledgeTutorialCheckpointRequest request, CancellationToken cancellationToken);
        Task<StarterGrantResult> RequestStarterGrantAsync(StarterGrantRequest request, CancellationToken cancellationToken);
        Task<SaveFormationResult> SaveFormationAsync(SaveFormationRequest request, CancellationToken cancellationToken);
        Task<TutorialBattleStartResult> RequestBattleStartAsync(TutorialBattleStartContext request, CancellationToken cancellationToken);
        Task<ReconcileTutorialBattleResultResult> ReconcileBattleResultAsync(ReconcileTutorialBattleResultRequest request, CancellationToken cancellationToken);
        Task<FetchTutorialResumeInstructionResult> FetchResumeInstructionAsync(FetchTutorialResumeInstructionRequest request, CancellationToken cancellationToken);
    }

    public interface ITutorialService
    {
        Task<TutorialServiceResult<TutorialState>> FetchAuthoritativeStateAsync(FetchTutorialStateRequest request, CancellationToken cancellationToken);
        Task<TutorialServiceResult<TutorialState>> AcknowledgeCheckpointAsync(AcknowledgeTutorialCheckpointRequest request, CancellationToken cancellationToken);
        Task<TutorialServiceResult<StarterGrantResult>> RequestStarterGrantAsync(StarterGrantRequest request, CancellationToken cancellationToken);
        Task<TutorialServiceResult<SavedFormation>> SaveFormationAsync(SaveFormationRequest request, CancellationToken cancellationToken);
        Task<TutorialServiceResult<TutorialBattleStartResult>> RequestBattleStartAsync(TutorialBattleStartContext request, CancellationToken cancellationToken);
        Task<TutorialServiceResult<ReconcileTutorialBattleResultResult>> ReconcileBattleResultAsync(ReconcileTutorialBattleResultRequest request, CancellationToken cancellationToken);
        Task<TutorialServiceResult<TutorialBattleResumeInstruction>> FetchResumeInstructionAsync(FetchTutorialResumeInstructionRequest request, CancellationToken cancellationToken);
    }

    [Serializable]
    public sealed class TutorialServiceResult<T>
    {
        public bool Success;
        public T Data;
        public TutorialFailure Failure;

        public static TutorialServiceResult<T> FromSuccess(T data)
        {
            return new TutorialServiceResult<T> { Success = true, Data = data };
        }

        public static TutorialServiceResult<T> FromFailure(TutorialFailure failure)
        {
            return new TutorialServiceResult<T> { Success = false, Failure = failure };
        }
    }

    [Serializable]
    public sealed class TutorialGatewayException : Exception
    {
        public TutorialGatewayException(TutorialFailureCategory category, string code, Exception innerException = null)
            : base("Tutorial gateway request failed.", innerException)
        {
            Category = category;
            Code = code;
        }

        public TutorialFailureCategory Category { get; }
        public string Code { get; }
    }

    public sealed class TutorialClientService : ITutorialService
    {
        private readonly ITutorialGateway _gateway;

        public TutorialClientService(ITutorialGateway gateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
        }

        public Task<TutorialServiceResult<TutorialState>> FetchAuthoritativeStateAsync(FetchTutorialStateRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return Task.FromResult(Failure<TutorialState>("STATE_REQUEST_REQUIRED"));
            }

            return ExecuteAsync(
                () => _gateway.FetchAuthoritativeStateAsync(request, cancellationToken),
                response => response.Success,
                response => response.Failure,
                response => response.State,
                cancellationToken);
        }

        public Task<TutorialServiceResult<TutorialState>> AcknowledgeCheckpointAsync(AcknowledgeTutorialCheckpointRequest request, CancellationToken cancellationToken)
        {
            TutorialFailure validation = ValidateRequestId(request?.RequestId, "CHECKPOINT_REQUEST_ID_REQUIRED");
            if (validation != null)
            {
                return Task.FromResult(TutorialServiceResult<TutorialState>.FromFailure(validation));
            }

            return ExecuteAsync(
                () => _gateway.AcknowledgeCheckpointAsync(request, cancellationToken),
                response => response.Success,
                response => response.Failure,
                response => response.State,
                cancellationToken);
        }

        public Task<TutorialServiceResult<StarterGrantResult>> RequestStarterGrantAsync(StarterGrantRequest request, CancellationToken cancellationToken)
        {
            TutorialFailure validation = ValidateRequestId(request?.RequestId, "STARTER_GRANT_REQUEST_ID_REQUIRED");
            if (validation != null)
            {
                return Task.FromResult(TutorialServiceResult<StarterGrantResult>.FromFailure(validation));
            }

            return ExecuteAsync(
                () => _gateway.RequestStarterGrantAsync(request, cancellationToken),
                response => response.Success,
                response => response.Failure,
                response => response,
                cancellationToken);
        }

        public Task<TutorialServiceResult<SavedFormation>> SaveFormationAsync(SaveFormationRequest request, CancellationToken cancellationToken)
        {
            TutorialFailure validation = ValidateRequestId(request?.RequestId, "FORMATION_REQUEST_ID_REQUIRED");
            if (validation == null && request.Formation == null)
            {
                validation = Failure(TutorialFailureCategory.Validation, "FORMATION_REQUIRED");
            }

            if (validation != null)
            {
                return Task.FromResult(TutorialServiceResult<SavedFormation>.FromFailure(validation));
            }

            return ExecuteAsync(
                () => _gateway.SaveFormationAsync(request, cancellationToken),
                response => response.Success,
                response => response.Failure,
                response => response.Formation,
                cancellationToken);
        }

        public Task<TutorialServiceResult<TutorialBattleStartResult>> RequestBattleStartAsync(TutorialBattleStartContext request, CancellationToken cancellationToken)
        {
            TutorialFailure validation = ValidateRequestId(request?.RequestId, "BATTLE_START_REQUEST_ID_REQUIRED");
            if (validation == null && request.Formation == null)
            {
                validation = Failure(TutorialFailureCategory.Validation, "FORMATION_REQUIRED");
            }

            if (validation != null)
            {
                return Task.FromResult(TutorialServiceResult<TutorialBattleStartResult>.FromFailure(validation));
            }

            return ExecuteAsync(
                () => _gateway.RequestBattleStartAsync(request, cancellationToken),
                response => response.Success,
                response => response.Failure,
                response => response,
                cancellationToken);
        }

        public Task<TutorialServiceResult<ReconcileTutorialBattleResultResult>> ReconcileBattleResultAsync(ReconcileTutorialBattleResultRequest request, CancellationToken cancellationToken)
        {
            TutorialFailure validation = ValidateRequestId(request?.RequestId, "BATTLE_RESULT_REQUEST_ID_REQUIRED");
            if (validation == null && string.IsNullOrWhiteSpace(request.BattleTransactionId))
            {
                validation = Failure(TutorialFailureCategory.Validation, "BATTLE_TRANSACTION_ID_REQUIRED");
            }
            else if (validation == null && request.Outcome == TutorialBattleOutcome.Unconfirmed)
            {
                validation = Failure(TutorialFailureCategory.Validation, "BATTLE_RESULT_MUST_BE_CONFIRMED");
            }

            if (validation != null)
            {
                return Task.FromResult(TutorialServiceResult<ReconcileTutorialBattleResultResult>.FromFailure(validation));
            }

            return ExecuteAsync(
                () => _gateway.ReconcileBattleResultAsync(request, cancellationToken),
                response => response.Success,
                response => response.Failure,
                response => response,
                cancellationToken);
        }

        public Task<TutorialServiceResult<TutorialBattleResumeInstruction>> FetchResumeInstructionAsync(FetchTutorialResumeInstructionRequest request, CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return Task.FromResult(Failure<TutorialBattleResumeInstruction>("RESUME_REQUEST_REQUIRED"));
            }

            return ExecuteAsync(
                () => _gateway.FetchResumeInstructionAsync(request, cancellationToken),
                response => response.Success,
                response => response.Failure,
                response => response.Instruction,
                cancellationToken);
        }

        private static async Task<TutorialServiceResult<TData>> ExecuteAsync<TGateway, TData>(
            Func<Task<TGateway>> request,
            Func<TGateway, bool> succeeded,
            Func<TGateway, TutorialFailure> failure,
            Func<TGateway, TData> data,
            CancellationToken cancellationToken)
        {
            // No `where TData : class` constraint: TData can legitimately be a value type (e.g.
            // TutorialBattleResumeInstruction). `result == null` below still compiles and behaves
            // correctly for an unconstrained type parameter - C# permits comparing any T to the
            // null literal, boxing a value-type result if needed, and a boxed value is never
            // null. So a value-type result (including one equal to its type's own default, like
            // TutorialBattleResumeInstruction.None) is never mistaken for a missing/empty success
            // response, with no special-case logic required. Reference-type callers are
            // unaffected: this constraint was compile-time-only and never changed their codegen.
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                TGateway response = await request().ConfigureAwait(false);
                if (response == null || !succeeded(response))
                {
                    return TutorialServiceResult<TData>.FromFailure(NormalizeFailure(response == null ? null : failure(response)));
                }

                TData result = data(response);
                return result == null
                    ? TutorialServiceResult<TData>.FromFailure(Failure(TutorialFailureCategory.Unavailable, "TUTORIAL_EMPTY_SUCCESS_RESPONSE"))
                    : TutorialServiceResult<TData>.FromSuccess(result);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (TutorialGatewayException exception)
            {
                return TutorialServiceResult<TData>.FromFailure(Failure(exception.Category, exception.Code));
            }
            catch (Exception)
            {
                return TutorialServiceResult<TData>.FromFailure(Failure(TutorialFailureCategory.Unavailable, "TUTORIAL_SERVICE_UNAVAILABLE"));
            }
        }

        private static TutorialFailure ValidateRequestId(string requestId, string code)
        {
            return TutorialContractValidator.IsRequestIdValid(requestId)
                ? null
                : Failure(TutorialFailureCategory.Validation, code);
        }

        private static TutorialFailure NormalizeFailure(TutorialFailure failure)
        {
            return failure == null
                ? Failure(TutorialFailureCategory.Unavailable, "TUTORIAL_SERVICE_UNAVAILABLE")
                : Failure(failure.Category, string.IsNullOrWhiteSpace(failure.Code) ? "TUTORIAL_SERVICE_FAILURE" : failure.Code);
        }

        private static TutorialFailure Failure(TutorialFailureCategory category, string code)
        {
            return new TutorialFailure { Category = category, Code = code };
        }

        private static TutorialServiceResult<T> Failure<T>(string code)
        {
            return TutorialServiceResult<T>.FromFailure(Failure(TutorialFailureCategory.Validation, code));
        }
    }

    public sealed class UnavailableTutorialService : ITutorialService
    {
        private static NotImplementedException Unavailable()
        {
            return new NotImplementedException("Trusted tutorial backend is not available.");
        }

        public Task<TutorialServiceResult<TutorialState>> FetchAuthoritativeStateAsync(FetchTutorialStateRequest request, CancellationToken cancellationToken) => throw Unavailable();
        public Task<TutorialServiceResult<TutorialState>> AcknowledgeCheckpointAsync(AcknowledgeTutorialCheckpointRequest request, CancellationToken cancellationToken) => throw Unavailable();
        public Task<TutorialServiceResult<StarterGrantResult>> RequestStarterGrantAsync(StarterGrantRequest request, CancellationToken cancellationToken) => throw Unavailable();
        public Task<TutorialServiceResult<SavedFormation>> SaveFormationAsync(SaveFormationRequest request, CancellationToken cancellationToken) => throw Unavailable();
        public Task<TutorialServiceResult<TutorialBattleStartResult>> RequestBattleStartAsync(TutorialBattleStartContext request, CancellationToken cancellationToken) => throw Unavailable();
        public Task<TutorialServiceResult<ReconcileTutorialBattleResultResult>> ReconcileBattleResultAsync(ReconcileTutorialBattleResultRequest request, CancellationToken cancellationToken) => throw Unavailable();
        public Task<TutorialServiceResult<TutorialBattleResumeInstruction>> FetchResumeInstructionAsync(FetchTutorialResumeInstructionRequest request, CancellationToken cancellationToken) => throw Unavailable();
    }
}