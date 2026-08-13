using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Tutorial;
using NUnit.Framework;

public sealed class TutorialServiceTests
{
    [Test]
    public void ExactServiceSurfaceExists()
    {
        Assert.That(typeof(ITutorialService).GetMethod("FetchAuthoritativeStateAsync"), Is.Not.Null);
        Assert.That(typeof(ITutorialService).GetMethod("AcknowledgeCheckpointAsync"), Is.Not.Null);
        Assert.That(typeof(ITutorialService).GetMethod("RequestStarterGrantAsync"), Is.Not.Null);
        Assert.That(typeof(ITutorialService).GetMethod("SaveFormationAsync"), Is.Not.Null);
        Assert.That(typeof(ITutorialService).GetMethod("RequestBattleStartAsync"), Is.Not.Null);
        Assert.That(typeof(ITutorialService).GetMethod("ReconcileBattleResultAsync"), Is.Not.Null);
        Assert.That(typeof(ITutorialService).GetMethod("FetchResumeInstructionAsync"), Is.Not.Null);
    }

    [Test]
    public void RequestDtosContainNoActorOrProviderFields()
    {
        var requestTypes = new[]
        {
            typeof(FetchTutorialStateRequest),
            typeof(AcknowledgeTutorialCheckpointRequest),
            typeof(StarterGrantRequest),
            typeof(SaveFormationRequest),
            typeof(TutorialBattleStartContext),
            typeof(ReconcileTutorialBattleResultRequest),
            typeof(FetchTutorialResumeInstructionRequest),
        };
        var forbidden = new[] { "actor", "account", "auth", "token", "credential", "project", "environment" };

        foreach (Type requestType in requestTypes)
        {
            foreach (FieldInfo field in requestType.GetFields(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.That(forbidden.Any(term => field.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0), Is.False, requestType.Name + "." + field.Name);
            }
        }
    }

    [Test]
    public async Task GatewayReceivesOnlyPermittedRequestData()
    {
        var gateway = new FakeGateway();
        var service = new TutorialClientService(gateway);
        var formation = new SavedFormation
        {
            Front = new List<string> { "warrior" },
            Middle = new List<string> { "novice_knight" },
            Back = new List<string> { "goblin_caster" },
        };

        var result = await service.SaveFormationAsync(new SaveFormationRequest { RequestId = "formation-1", Formation = formation }, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(gateway.LastFormationRequest.RequestId, Is.EqualTo("formation-1"));
        Assert.That(gateway.LastFormationRequest.Formation.Front, Is.EqualTo(new[] { "warrior" }));
        Assert.That(gateway.LastStarterRequest, Is.Null);
    }

    [Test]
    public async Task StarterGrantContentsCannotBeClientSelected()
    {
        var gateway = new FakeGateway();
        var service = new TutorialClientService(gateway);
        var result = await service.RequestStarterGrantAsync(new StarterGrantRequest { RequestId = "grant-1" }, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(result.Data.CardIds, Is.EqualTo(new[] { "warrior", "novice_knight", "goblin_caster" }));
        Assert.That(typeof(StarterGrantRequest).GetFields().Any(field => field.Name.IndexOf("card", StringComparison.OrdinalIgnoreCase) >= 0), Is.False);
    }

    [Test]
    public async Task CancellationPropagates()
    {
        var gateway = new FakeGateway { ThrowCancellation = true };
        var service = new TutorialClientService(gateway);
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            Assert.CatchAsync<OperationCanceledException>(() => service.FetchAuthoritativeStateAsync(new FetchTutorialStateRequest(), cancellation.Token));
        }
    }

    [TestCase(TutorialFailureCategory.Authentication, "AUTHENTICATION_REQUIRED")]
    [TestCase(TutorialFailureCategory.Unavailable, "SERVICE_UNAVAILABLE")]
    [TestCase(TutorialFailureCategory.Conflict, "CONFLICT")]
    [TestCase(TutorialFailureCategory.Validation, "INVALID_REQUEST")]
    public async Task GatewayFailuresMapToStableCategories(TutorialFailureCategory category, string code)
    {
        var gateway = new FakeGateway { Failure = new TutorialGatewayException(category, code, new InvalidOperationException("raw backend secret")) };
        var service = new TutorialClientService(gateway);
        var result = await service.FetchAuthoritativeStateAsync(new FetchTutorialStateRequest(), CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Failure.Category, Is.EqualTo(category));
        Assert.That(result.Failure.Code, Is.EqualTo(code));
        Assert.That(result.Failure.Code, Does.Not.Contain("raw backend secret"));
    }

    [Test]
    public async Task RawExceptionTextIsNotExposed()
    {
        var gateway = new FakeGateway { RawFailure = new InvalidOperationException("provider token secret") };
        var result = await new TutorialClientService(gateway).FetchResumeInstructionAsync(new FetchTutorialResumeInstructionRequest(), CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Failure.Category, Is.EqualTo(TutorialFailureCategory.Unavailable));
        Assert.That(result.Failure.Code, Does.Not.Contain("provider token secret"));
    }

    [TestCase(TutorialBattleResumeInstruction.None)]
    [TestCase(TutorialBattleResumeInstruction.ResumeFromFormation)]
    [TestCase(TutorialBattleResumeInstruction.ShowConfirmedResult)]
    public async Task ResumeInstructionMapsEveryValidValueIncludingTheDefault(TutorialBattleResumeInstruction instruction)
    {
        // ExecuteAsync's generic helper has no `where TData : class` constraint (TData is a
        // value type here), so a real enum value - including None, the type's own default -
        // must never be mistaken for a missing/empty success response.
        var gateway = new FakeGateway { ResumeInstruction = instruction };
        var service = new TutorialClientService(gateway);
        var result = await service.FetchResumeInstructionAsync(new FetchTutorialResumeInstructionRequest(), CancellationToken.None);

        Assert.That(result.Success, Is.True,
            $"A gateway-reported success carrying {instruction} must not be rejected as missing data.");
        Assert.That(result.Data, Is.EqualTo(instruction));
        Assert.That(result.Failure, Is.Null);
    }

    [Test]
    public async Task ResumeInstructionGatewayReportedFailureIsRejectedNotTreatedAsSuccess()
    {
        var gateway = new FakeGateway { ReturnResumeFailure = true };
        var service = new TutorialClientService(gateway);
        var result = await service.FetchResumeInstructionAsync(new FetchTutorialResumeInstructionRequest(), CancellationToken.None);

        Assert.That(result.Success, Is.False,
            "A gateway-reported failure must not be fabricated into success just because the enum has no null state to lean on.");
        Assert.That(result.Failure, Is.Not.Null);
    }

    [Test]
    public void ResumeInstructionCancellationPropagates()
    {
        var gateway = new FakeGateway { ThrowCancellation = true };
        var service = new TutorialClientService(gateway);
        using (var cancellation = new CancellationTokenSource())
        {
            cancellation.Cancel();
            Assert.CatchAsync<OperationCanceledException>(() => service.FetchResumeInstructionAsync(new FetchTutorialResumeInstructionRequest(), cancellation.Token));
        }
    }

    [Test]
    public async Task FailedGatewayCallCannotProduceSuccess()
    {
        var gateway = new FakeGateway { ReturnFailure = true };
        var result = await new TutorialClientService(gateway).RequestBattleStartAsync(
            new TutorialBattleStartContext { RequestId = "battle-1", Formation = Formation() }, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Data, Is.Null);
    }

    [Test]
    public async Task DuplicateAndReplaySemanticsComeFromAuthoritativeResponses()
    {
        var gateway = new FakeGateway { StarterAlreadyGranted = true, RewardAlreadyReconciled = true };
        var service = new TutorialClientService(gateway);
        var grant = await service.RequestStarterGrantAsync(new StarterGrantRequest { RequestId = "grant-replay" }, CancellationToken.None);
        var reward = await service.ReconcileBattleResultAsync(new ReconcileTutorialBattleResultRequest
        {
            RequestId = "victory-replay",
            BattleTransactionId = "battle-1",
            Outcome = TutorialBattleOutcome.Victory,
        }, CancellationToken.None);

        Assert.That(grant.Success, Is.True);
        Assert.That(grant.Data.Status, Is.EqualTo(StarterGrantStatus.AlreadyGranted));
        Assert.That(reward.Success, Is.True);
        Assert.That(reward.Data.Status, Is.EqualTo(TutorialReconciliationStatus.AlreadyReconciled));
    }

    [Test]
    public async Task DefeatCannotRequestRewardAdvancementAndUnconfirmedResultIsRejected()
    {
        var gateway = new FakeGateway();
        var service = new TutorialClientService(gateway);
        var defeat = await service.ReconcileBattleResultAsync(new ReconcileTutorialBattleResultRequest
        {
            RequestId = "defeat-1",
            BattleTransactionId = "battle-1",
            Outcome = TutorialBattleOutcome.Defeat,
        }, CancellationToken.None);
        var unconfirmed = await service.ReconcileBattleResultAsync(new ReconcileTutorialBattleResultRequest
        {
            RequestId = "unknown-1",
            BattleTransactionId = "battle-2",
            Outcome = TutorialBattleOutcome.Unconfirmed,
        }, CancellationToken.None);

        Assert.That(defeat.Success, Is.True);
        Assert.That(gateway.LastBattleResult.Outcome, Is.EqualTo(TutorialBattleOutcome.Defeat));
        Assert.That(unconfirmed.Success, Is.False);
        Assert.That(unconfirmed.Failure.Category, Is.EqualTo(TutorialFailureCategory.Validation));
    }

    [Test]
    public void ProductionServiceIsExplicitlyUnavailable()
    {
        var service = new UnavailableTutorialService();
        Assert.Throws<NotImplementedException>(() => service.FetchAuthoritativeStateAsync(new FetchTutorialStateRequest(), CancellationToken.None));
        Assert.That(typeof(UnavailableTutorialService).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(method => method.DeclaringType == typeof(UnavailableTutorialService))
            .All(method => method.ReturnType == typeof(Task<>).MakeGenericType(typeof(TutorialServiceResult<TutorialState>)) || method.Name != "FetchAuthoritativeStateAsync"), Is.True);
    }

    private static SavedFormation Formation()
    {
        return new SavedFormation
        {
            Front = new List<string> { "warrior" },
            Middle = new List<string> { "novice_knight" },
            Back = new List<string> { "goblin_caster" },
        };
    }

    private sealed class FakeGateway : ITutorialGateway
    {
        public TutorialGatewayException Failure;
        public Exception RawFailure;
        public bool ReturnFailure;
        public bool ThrowCancellation;
        public bool StarterAlreadyGranted;
        public bool RewardAlreadyReconciled;
        public TutorialBattleResumeInstruction ResumeInstruction = TutorialBattleResumeInstruction.ResumeFromFormation;
        public bool ReturnResumeFailure;
        public SaveFormationRequest LastFormationRequest;
        public StarterGrantRequest LastStarterRequest;
        public ReconcileTutorialBattleResultRequest LastBattleResult;

        public Task<TutorialStateResult> FetchAuthoritativeStateAsync(FetchTutorialStateRequest request, CancellationToken cancellationToken)
        {
            return Respond(new TutorialStateResult { Success = true, State = new TutorialState() }, cancellationToken);
        }

        public Task<AcknowledgeTutorialCheckpointResult> AcknowledgeCheckpointAsync(AcknowledgeTutorialCheckpointRequest request, CancellationToken cancellationToken)
        {
            return Respond(new AcknowledgeTutorialCheckpointResult { Success = true, State = new TutorialState() }, cancellationToken);
        }

        public Task<StarterGrantResult> RequestStarterGrantAsync(StarterGrantRequest request, CancellationToken cancellationToken)
        {
            LastStarterRequest = request;
            return Respond(new StarterGrantResult
            {
                Success = true,
                Status = StarterAlreadyGranted ? StarterGrantStatus.AlreadyGranted : StarterGrantStatus.Granted,
                GrantTransactionId = "grant-1",
                CardIds = new List<string> { "warrior", "novice_knight", "goblin_caster" },
            }, cancellationToken);
        }

        public Task<SaveFormationResult> SaveFormationAsync(SaveFormationRequest request, CancellationToken cancellationToken)
        {
            LastFormationRequest = request;
            return Respond(new SaveFormationResult { Success = true, Formation = request.Formation }, cancellationToken);
        }

        public Task<TutorialBattleStartResult> RequestBattleStartAsync(TutorialBattleStartContext request, CancellationToken cancellationToken)
        {
            return Respond(new TutorialBattleStartResult { Success = !ReturnFailure, BattleTransactionId = "battle-1", Formation = request.Formation }, cancellationToken);
        }

        public Task<ReconcileTutorialBattleResultResult> ReconcileBattleResultAsync(ReconcileTutorialBattleResultRequest request, CancellationToken cancellationToken)
        {
            LastBattleResult = request;
            return Respond(new ReconcileTutorialBattleResultResult
            {
                Success = true,
                Status = RewardAlreadyReconciled ? TutorialReconciliationStatus.AlreadyReconciled : TutorialReconciliationStatus.Applied,
                VictoryCheckpointAdvanced = request.Outcome == TutorialBattleOutcome.Victory && !RewardAlreadyReconciled,
                RewardTransactionApplied = request.Outcome == TutorialBattleOutcome.Victory && !RewardAlreadyReconciled,
                State = new TutorialState { FirstVictoryConfirmed = request.Outcome == TutorialBattleOutcome.Victory },
            }, cancellationToken);
        }

        public Task<FetchTutorialResumeInstructionResult> FetchResumeInstructionAsync(FetchTutorialResumeInstructionRequest request, CancellationToken cancellationToken)
        {
            // Constructed directly rather than through Respond's ReturnFailure branch (which is
            // specific to TutorialBattleStartResult) - keeps this isolated from the unrelated
            // RequestBattleStartAsync failure test.
            return Respond(new FetchTutorialResumeInstructionResult
            {
                Success = !ReturnResumeFailure,
                Instruction = ResumeInstruction,
                Failure = ReturnResumeFailure
                    ? new TutorialFailure { Category = TutorialFailureCategory.Unavailable, Code = "RESUME_UNAVAILABLE" }
                    : null,
            }, cancellationToken);
        }

        private Task<T> Respond<T>(T response, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (ThrowCancellation) throw new OperationCanceledException(cancellationToken);
            if (Failure != null) throw Failure;
            if (RawFailure != null) throw RawFailure;
            if (ReturnFailure)
            {
                if (response is TutorialBattleStartResult battleStart)
                {
                    battleStart.Success = false;
                    battleStart.Failure = new TutorialFailure { Category = TutorialFailureCategory.Unavailable, Code = "SERVICE_UNAVAILABLE" };
                }
            }

            return Task.FromResult(response);
        }
    }
}