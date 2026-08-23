using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Social;
using NUnit.Framework;

public sealed class SocialSafetyOperationsTests
{
    [Test]
    public async Task AuthenticatedBlockReachesGatewayWithOnlyTarget()
    {
        var gateway = new FakeSafetyGateway();
        var service = Create(gateway);
        var result = await service.BlockAccountAsync(new BlockAccountRequest { blockedAccountId = "target" }, CancellationToken.None);

        Assert.That(result.Success, Is.True);
        Assert.That(gateway.LastOperation, Is.EqualTo("BlockAccount"));
        Assert.That(gateway.LastTarget, Is.EqualTo("target"));
        Assert.That(gateway.LastCredentialArgument, Is.Null);
        Assert.That(gateway.LastProjectArgument, Is.Null);
    }

    [Test]
    public async Task SelfBlockAndSelfMuteAreRejectedBeforeSafetyGateway()
    {
        var gateway = new FakeSafetyGateway();
        var service = Create(gateway);
        var block = await service.BlockAccountAsync(new BlockAccountRequest { blockedAccountId = "actor" }, CancellationToken.None);
        var mute = await service.MuteAccountAsync(new MuteAccountRequest { mutedAccountId = "actor" }, CancellationToken.None);

        Assert.That(block.Failure.Category, Is.EqualTo(SocialFailureCategory.Validation));
        Assert.That(mute.Failure.Category, Is.EqualTo(SocialFailureCategory.Validation));
        Assert.That(gateway.CallCount, Is.EqualTo(0));
    }

    [Test]
    public async Task MapsBlockUnblockMuteAndUnmuteResults()
    {
        var gateway = new FakeSafetyGateway
        {
            BlockResult = Result(Record("active"), true),
            MuteResult = Result(Record("active"), true),
            UnblockResult = Result(Record("inactive"), true),
            UnmuteResult = Result(Record("inactive"), true)
        };
        var service = Create(gateway);
        var block = await service.BlockAccountAsync(new BlockAccountRequest { blockedAccountId = "target" }, CancellationToken.None);
        var unblock = await service.UnblockAccountAsync(new UnblockAccountRequest { blockedAccountId = "target" }, CancellationToken.None);
        var mute = await service.MuteAccountAsync(new MuteAccountRequest { mutedAccountId = "target" }, CancellationToken.None);
        var unmute = await service.UnmuteAccountAsync(new UnmuteAccountRequest { mutedAccountId = "target" }, CancellationToken.None);

        Assert.That(block.Data.block.blockId, Is.EqualTo("record"));
        Assert.That(unblock.Data.unblocked, Is.True);
        Assert.That(mute.Data.mute.muteId, Is.EqualTo("record"));
        Assert.That(unmute.Data.unmuted, Is.True);
    }

    [Test]
    public async Task RepeatedUnblockAndUnmuteReturnSuccessfulNoOpsWithoutFabricatingRecords()
    {
        var gateway = new FakeSafetyGateway
        {
            UnblockResult = Result(null, false),
            UnmuteResult = Result(null, false)
        };
        var service = Create(gateway);

        var unblock = await service.UnblockAccountAsync(new UnblockAccountRequest { blockedAccountId = "target" }, CancellationToken.None);
        var unmute = await service.UnmuteAccountAsync(new UnmuteAccountRequest { mutedAccountId = "target" }, CancellationToken.None);

        Assert.That(unblock.Success, Is.True);
        Assert.That(unblock.Data.unblocked, Is.True);
        Assert.That(unmute.Success, Is.True);
        Assert.That(unmute.Data.unmuted, Is.True);
    }

    [Test]
    public void CancellationIsPreserved()
    {
        var gateway = new FakeSafetyGateway { Cancel = true };
        var service = Create(gateway);
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        Assert.CatchAsync<OperationCanceledException>(() => service.BlockAccountAsync(new BlockAccountRequest { blockedAccountId = "target" }, cancellation.Token));
    }

    [TestCase(SocialFailureCategory.Authentication)]
    [TestCase(SocialFailureCategory.Authorization)]
    [TestCase(SocialFailureCategory.Validation)]
    [TestCase(SocialFailureCategory.Conflict)]
    [TestCase(SocialFailureCategory.RateLimit)]
    [TestCase(SocialFailureCategory.Unavailable)]
    [TestCase(SocialFailureCategory.Unknown)]
    public async Task MapsStableFailureCategories(SocialFailureCategory category)
    {
        var gateway = new FakeSafetyGateway { Failure = new SocialSafetyGatewayException(category, "server secret") };
        var result = await Create(gateway).MuteAccountAsync(new MuteAccountRequest { mutedAccountId = "target" }, CancellationToken.None);
        Assert.That(result.Success, Is.False);
        Assert.That(result.Failure.Category, Is.EqualTo(category));
        Assert.That(result.Failure.Detail, Is.EqualTo(ExpectedDetail(category)));
        Assert.That(result.Failure.Detail, Is.Not.EqualTo("server secret"));
        Assert.That(result.Failure.Message, Does.Not.Contain("server secret"));
        Assert.That(result.Data, Is.Null);
    }

    [Test]
    public async Task MapsUnknownRawExceptionWithoutExposingItsText()
    {
        var gateway = new FakeSafetyGateway { RawFailure = new InvalidOperationException("raw server secret") };
        var result = await Create(gateway).MuteAccountAsync(new MuteAccountRequest { mutedAccountId = "target" }, CancellationToken.None);

        Assert.That(result.Success, Is.False);
        Assert.That(result.Failure.Category, Is.EqualTo(SocialFailureCategory.Unknown));
        Assert.That(result.Failure.Detail, Is.EqualTo("SOCIAL_SAFETY_UNEXPECTED_FAILURE"));
        Assert.That(result.Failure.Message, Does.Not.Contain("raw server secret"));
        Assert.That(result.Failure.Detail, Does.Not.Contain("raw server secret"));
        Assert.That(result.Data, Is.Null);
    }

    [Test]
    public async Task FailedGatewayCallCannotProduceLocalSuccess()
    {
        var gateway = new FakeSafetyGateway { Failure = new SocialSafetyGatewayException(SocialFailureCategory.Unknown, "secret") };
        var result = await Create(gateway).BlockAccountAsync(new BlockAccountRequest { blockedAccountId = "target" }, CancellationToken.None);
        Assert.That(result.Success, Is.False);
        Assert.That(result.Data, Is.Null);
    }

    [Test]
    public void SubmitReportGuildAndDirectMessageRemainUnimplemented()
    {
        var service = Create(new FakeSafetyGateway());
        Assert.Throws<NotImplementedException>(() => service.SubmitReportAsync(null, CancellationToken.None));
        Assert.Throws<NotImplementedException>(() => service.CreateGuildAsync(null, CancellationToken.None));
        Assert.Throws<NotImplementedException>(() => service.CreateDirectMessageRequestAsync(null, CancellationToken.None));
    }

    private static UnityAuthenticationSocialService Create(FakeSafetyGateway gateway)
    {
        return new UnityAuthenticationSocialService(new FakeIdentityGateway(), gateway);
    }

    private static SocialSafetyGatewayRecord Record(string status)
    {
        return new SocialSafetyGatewayRecord
        {
            id = "record",
            actorAccountId = "actor",
            targetAccountId = "target",
            createdAtUtcMs = 1000,
            muteUntilUtcMs = 2000,
            status = status
        };
    }

    private static SocialSafetyGatewayResult Result(SocialSafetyGatewayRecord record, bool changed)
    {
        return new SocialSafetyGatewayResult { record = record, changed = changed };
    }

    private static string ExpectedDetail(SocialFailureCategory category)
    {
        switch (category)
        {
            case SocialFailureCategory.Authentication: return "SOCIAL_SAFETY_AUTHENTICATION_FAILURE";
            case SocialFailureCategory.Authorization: return "SOCIAL_SAFETY_AUTHORIZATION_FAILURE";
            case SocialFailureCategory.Validation: return "SOCIAL_SAFETY_VALIDATION_FAILURE";
            case SocialFailureCategory.Conflict: return "SOCIAL_SAFETY_CONFLICT_FAILURE";
            case SocialFailureCategory.RateLimit: return "SOCIAL_SAFETY_RATE_LIMIT_FAILURE";
            case SocialFailureCategory.Unavailable: return "SOCIAL_SAFETY_SERVICE_UNAVAILABLE";
            default: return "SOCIAL_SAFETY_UNEXPECTED_FAILURE";
        }
    }

    private sealed class FakeIdentityGateway : IIdentityBootstrapGateway
    {
        public Task<IdentityBootstrapSnapshot> BootstrapAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new IdentityBootstrapSnapshot { PlayerId = "actor" });
        }
    }

    private sealed class FakeSafetyGateway : ISocialSafetyGateway
    {
        public string LastOperation;
        public string LastTarget;
        public string LastCredentialArgument;
        public string LastProjectArgument;
        public int CallCount;
        public bool Cancel;
        public SocialSafetyGatewayResult BlockResult = ResultForDefault("inactive");
        public SocialSafetyGatewayResult UnblockResult = ResultForDefault("inactive");
        public SocialSafetyGatewayResult MuteResult = ResultForDefault("inactive");
        public SocialSafetyGatewayResult UnmuteResult = ResultForDefault("inactive");
        public SocialSafetyGatewayException Failure;
        public Exception RawFailure;

        public Task<SocialSafetyGatewayResult> BlockAccountAsync(string targetAccountId, CancellationToken cancellationToken) => Call("BlockAccount", targetAccountId, cancellationToken, BlockResult);
        public Task<SocialSafetyGatewayResult> UnblockAccountAsync(string targetAccountId, CancellationToken cancellationToken) => Call("UnblockAccount", targetAccountId, cancellationToken, UnblockResult);
        public Task<SocialSafetyGatewayResult> MuteAccountAsync(string targetAccountId, CancellationToken cancellationToken) => Call("MuteAccount", targetAccountId, cancellationToken, MuteResult);
        public Task<SocialSafetyGatewayResult> UnmuteAccountAsync(string targetAccountId, CancellationToken cancellationToken) => Call("UnmuteAccount", targetAccountId, cancellationToken, UnmuteResult);

        private static SocialSafetyGatewayResult ResultForDefault(string status)
        {
            return new SocialSafetyGatewayResult { record = new SocialSafetyGatewayRecord { id = "record", actorAccountId = "actor", targetAccountId = "target", status = status }, changed = false };
        }

        private Task<SocialSafetyGatewayResult> Call(string operation, string targetAccountId, CancellationToken cancellationToken, SocialSafetyGatewayResult result)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastOperation = operation;
            LastTarget = targetAccountId;
            CallCount++;
            if (Cancel) throw new OperationCanceledException(cancellationToken);
            if (Failure != null) throw Failure;
            if (RawFailure != null) throw RawFailure;
            return Task.FromResult(result);
        }
    }
}
