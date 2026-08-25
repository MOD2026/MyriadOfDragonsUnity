using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Friends.Tests;

public sealed class FriendsOperationsTests
{
    private const long Now = 10_000_000_000L; // arbitrary fixed instant

    // ---------------- AddFriend ----------------

    [Test]
    public async Task AddFriend_MissingActorOrBlankOrSelfTargetIsRejected()
    {
        var operations = Create();
        var noActor = await operations.AddFriendAsync(new FakeExecutionContext(null), null!, Request("target"));
        var blank = await operations.AddFriendAsync(Context("actor"), null!, Request(""));
        var self = await operations.AddFriendAsync(Context("actor"), null!, Request("actor"));
        Assert.That(noActor.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
        Assert.That(blank.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
        Assert.That(self.ErrorCode, Is.EqualTo("SELF_TARGET_NOT_ALLOWED"));
    }

    [Test]
    public async Task AddFriend_FirstRequest_CreatesAPendingRecordVisibleToBothSides()
    {
        var store = new FakeStore();
        var result = await Create(store).AddFriendAsync(Context("a"), null!, Request("b"));

        Assert.That(result.Success, Is.True);
        Assert.That(result.Status, Is.EqualTo(FriendshipStatus.Pending));

        var record = await store.LoadFriendshipAsync(null!, null!, "a", "b");
        Assert.That(record!.RequesterAccountId, Is.EqualTo("a"));
        Assert.That(record.RecipientAccountId, Is.EqualTo("b"));

        Assert.That((await store.LoadIndexAsync(null!, null!, "a")).CounterpartAccountIds, Does.Contain("b"));
        Assert.That((await store.LoadIndexAsync(null!, null!, "b")).CounterpartAccountIds, Does.Contain("a"));
    }

    [Test]
    public async Task AddFriend_DuplicateOutgoingRequestIsRejected()
    {
        var store = new FakeStore();
        await Create(store).AddFriendAsync(Context("a"), null!, Request("b"));
        var again = await Create(store).AddFriendAsync(Context("a"), null!, Request("b"));
        Assert.That(again.ErrorCode, Is.EqualTo("ALREADY_REQUESTED"));
    }

    [Test]
    public async Task AddFriend_WhileAnIncomingRequestIsPending_IsRejected_NotAutoAccepted()
    {
        var store = new FakeStore();
        await Create(store).AddFriendAsync(Context("a"), null!, Request("b"));
        var reverse = await Create(store).AddFriendAsync(Context("b"), null!, Request("a"));
        Assert.That(reverse.ErrorCode, Is.EqualTo("INCOMING_REQUEST_PENDING"));

        var record = await store.LoadFriendshipAsync(null!, null!, "a", "b");
        Assert.That(record!.Status, Is.EqualTo(FriendshipStatus.Pending), "A reverse AddFriend call must never silently accept the original request.");
    }

    [Test]
    public async Task AddFriend_AlreadyFriendsIsRejected()
    {
        var store = new FakeStore();
        await Create(store).AddFriendAsync(Context("a"), null!, Request("b"));
        await Create(store).AcceptFriendAsync(Context("b"), null!, Request("a"));

        var result = await Create(store).AddFriendAsync(Context("a"), null!, Request("b"));
        Assert.That(result.ErrorCode, Is.EqualTo("ALREADY_FRIENDS"));
    }

    // ---------------- Accept / Decline ----------------

    [Test]
    public async Task AcceptFriend_OnlyTheRecipientMayAccept()
    {
        var store = new FakeStore();
        await Create(store).AddFriendAsync(Context("a"), null!, Request("b"));

        var requesterTriesToAccept = await Create(store).AcceptFriendAsync(Context("a"), null!, Request("b"));
        Assert.That(requesterTriesToAccept.ErrorCode, Is.EqualTo("NOT_RECIPIENT"));

        var recipientAccepts = await Create(store).AcceptFriendAsync(Context("b"), null!, Request("a"));
        Assert.That(recipientAccepts.Success, Is.True);
        Assert.That(recipientAccepts.Status, Is.EqualTo(FriendshipStatus.Accepted));
    }

    [Test]
    public async Task AcceptFriend_NoRequestExists_IsRejected()
    {
        var result = await Create().AcceptFriendAsync(Context("b"), null!, Request("a"));
        Assert.That(result.ErrorCode, Is.EqualTo("REQUEST_NOT_FOUND"));
    }

    [Test]
    public async Task DeclineFriend_OnlyTheRecipientMayDecline_AndItDeletesTheRecordAndBothIndices()
    {
        var store = new FakeStore();
        await Create(store).AddFriendAsync(Context("a"), null!, Request("b"));

        var requesterTriesToDecline = await Create(store).DeclineFriendAsync(Context("a"), null!, Request("b"));
        Assert.That(requesterTriesToDecline.ErrorCode, Is.EqualTo("NOT_RECIPIENT"));

        var declined = await Create(store).DeclineFriendAsync(Context("b"), null!, Request("a"));
        Assert.That(declined.Success, Is.True);
        Assert.That(await store.LoadFriendshipAsync(null!, null!, "a", "b"), Is.Null);
        Assert.That((await store.LoadIndexAsync(null!, null!, "a")).CounterpartAccountIds, Does.Not.Contain("b"));
        Assert.That((await store.LoadIndexAsync(null!, null!, "b")).CounterpartAccountIds, Does.Not.Contain("a"));
    }

    // ---------------- Remove ----------------

    [Test]
    public async Task RemoveFriend_EitherSideOfAnAcceptedFriendshipMayRemove()
    {
        var store = new FakeStore();
        await Create(store).AddFriendAsync(Context("a"), null!, Request("b"));
        await Create(store).AcceptFriendAsync(Context("b"), null!, Request("a"));

        var result = await Create(store).RemoveFriendAsync(Context("b"), null!, Request("a"));
        Assert.That(result.Success, Is.True);
        Assert.That(await store.LoadFriendshipAsync(null!, null!, "a", "b"), Is.Null);
    }

    [Test]
    public async Task RemoveFriend_RequesterMayCancelTheirOwnPendingRequest()
    {
        var store = new FakeStore();
        await Create(store).AddFriendAsync(Context("a"), null!, Request("b"));

        var result = await Create(store).RemoveFriendAsync(Context("a"), null!, Request("b"));
        Assert.That(result.Success, Is.True);
    }

    [Test]
    public async Task RemoveFriend_RecipientCannotSilentlyDeleteAPendingRequest_MustDecline()
    {
        var store = new FakeStore();
        await Create(store).AddFriendAsync(Context("a"), null!, Request("b"));

        var result = await Create(store).RemoveFriendAsync(Context("b"), null!, Request("a"));
        Assert.That(result.ErrorCode, Is.EqualTo("NOT_AUTHORIZED"));
    }

    [Test]
    public async Task RemoveFriend_NoRelationshipExists_IsRejected()
    {
        var result = await Create().RemoveFriendAsync(Context("a"), null!, Request("b"));
        Assert.That(result.ErrorCode, Is.EqualTo("REQUEST_NOT_FOUND"));
    }

    // ---------------- ListFriends ----------------

    [Test]
    public async Task ListFriends_MissingActorIsRejected()
    {
        var result = await Create().ListFriendsAsync(new FakeExecutionContext(null), null!, new ListFriendsRequest());
        Assert.That(result.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
    }

    [Test]
    public async Task ListFriends_ShowsPendingOutgoing_PendingIncoming_AndAccepted_Distinctly()
    {
        var store = new FakeStore();
        var operations = Create(store);
        await operations.AddFriendAsync(Context("a"), null!, Request("outgoing-target"));
        await operations.AddFriendAsync(Context("incoming-source"), null!, Request("a"));
        await operations.AddFriendAsync(Context("a"), null!, Request("friend"));
        await operations.AcceptFriendAsync(Context("friend"), null!, Request("a"));

        var result = await operations.ListFriendsAsync(Context("a"), null!, new ListFriendsRequest());
        Assert.That(result.Success, Is.True);
        Assert.That(result.Friends.Count, Is.EqualTo(3));

        var outgoing = result.Friends.Single(f => f.CounterpartAccountId == "outgoing-target");
        Assert.That(outgoing.Status, Is.EqualTo(FriendshipStatus.Pending));
        Assert.That(outgoing.IsOutgoingRequest, Is.True);

        var incoming = result.Friends.Single(f => f.CounterpartAccountId == "incoming-source");
        Assert.That(incoming.Status, Is.EqualTo(FriendshipStatus.Pending));
        Assert.That(incoming.IsOutgoingRequest, Is.False);

        var friend = result.Friends.Single(f => f.CounterpartAccountId == "friend");
        Assert.That(friend.Status, Is.EqualTo(FriendshipStatus.Accepted));
        Assert.That(friend.CanGiftToday, Is.True);
    }

    [Test]
    public async Task ListFriends_ToleratesAnIndexEntryWhoseRecordIsGone()
    {
        var store = new FakeStore();
        store.Indices["a"] = new FriendsIndexState { CounterpartAccountIds = new List<string> { "ghost" } };

        var result = await Create(store).ListFriendsAsync(Context("a"), null!, new ListFriendsRequest());
        Assert.That(result.Success, Is.True);
        Assert.That(result.Friends, Is.Empty);
    }

    // ---------------- SendDailyGift ----------------

    [Test]
    public async Task SendDailyGift_NotFriendsIsRejected()
    {
        var result = await Create().SendDailyGiftAsync(Context("a"), null!, Request("b"));
        Assert.That(result.ErrorCode, Is.EqualTo("NOT_FRIENDS"));
    }

    [Test]
    public async Task SendDailyGift_FirstGiftOfTheDaySucceeds_SecondFromTheSameSenderSameDayIsRejected()
    {
        var store = new FakeStore();
        await Create(store).AddFriendAsync(Context("a"), null!, Request("b"));
        await Create(store).AcceptFriendAsync(Context("b"), null!, Request("a"));

        var first = await Create(store).SendDailyGiftAsync(Context("a"), null!, Request("b"));
        Assert.That(first.Success, Is.True);

        var second = await Create(store).SendDailyGiftAsync(Context("a"), null!, Request("b"));
        Assert.That(second.ErrorCode, Is.EqualTo("GIFT_ALREADY_SENT_TODAY"));
    }

    [Test]
    public async Task SendDailyGift_BothDirectionsCanGiftIndependentlyTheSameDay()
    {
        var store = new FakeStore();
        await Create(store).AddFriendAsync(Context("a"), null!, Request("b"));
        await Create(store).AcceptFriendAsync(Context("b"), null!, Request("a"));

        var aToB = await Create(store).SendDailyGiftAsync(Context("a"), null!, Request("b"));
        var bToA = await Create(store).SendDailyGiftAsync(Context("b"), null!, Request("a"));
        Assert.That(aToB.Success, Is.True);
        Assert.That(bToA.Success, Is.True, "A's own gift today must not block B's independent gift to A the same day.");
    }

    [Test]
    public async Task SendDailyGift_NextUtcDayAllowsAnotherGift()
    {
        var store = new FakeStore();
        var clock = new FakeClock(Now);
        var operations = new FriendsOperations(store, clock);
        await operations.AddFriendAsync(Context("a"), null!, Request("b"));
        await operations.AcceptFriendAsync(Context("b"), null!, Request("a"));

        Assert.That((await operations.SendDailyGiftAsync(Context("a"), null!, Request("b"))).Success, Is.True);

        clock.UtcNowMs = Now + (long)TimeSpan.FromDays(1).TotalMilliseconds;
        var nextDay = await operations.SendDailyGiftAsync(Context("a"), null!, Request("b"));
        Assert.That(nextDay.Success, Is.True);
    }

    [Test]
    public void IsSameUtcDay_ZeroTimestampIsNeverTreatedAsToday()
    {
        Assert.That(FriendsOperations.IsSameUtcDay(0, Now), Is.False, "A never-gifted (default 0) timestamp must not be mistaken for 'already gifted today'.");
    }

    // ---------------- Contract ----------------

    [Test]
    public void ResponseSerializationUsesTheDocumentedCamelCaseContract()
    {
        var result = new FriendResult { Success = true, Status = FriendshipStatus.Accepted };
        string json = JsonConvert.SerializeObject(result);
        Assert.That(json, Does.Contain("\"success\":true"));
        Assert.That(json, Does.Contain("\"status\":\"Accepted\""),
            "Status must serialize as a string enum name, not a raw int - the client gateway/presenter compare against \"Accepted\".");

        var listResult = new ListFriendsResult
        {
            Success = true,
            Friends = new List<FriendSummary> { new() { CounterpartAccountId = "x", CanGiftToday = true } },
        };
        Assert.That(JsonConvert.SerializeObject(listResult), Does.Contain("\"canGiftToday\":true"));
    }

    private static FriendsOperations Create(FakeStore? store = null) => new(store ?? new FakeStore(), new FakeClock(Now));

    private static FakeExecutionContext Context(string playerId) => new(playerId);

    private static FriendRequest Request(string targetAccountId) => new() { TargetAccountId = targetAccountId };

    /// <summary>In-memory reference implementation of <see cref="IFriendsStore"/> - same pattern as
    /// Bazaar's own FakeStore.</summary>
    private sealed class FakeStore : IFriendsStore
    {
        public Dictionary<string, FriendshipRecord> Records { get; } = new();
        public Dictionary<string, FriendsIndexState> Indices { get; } = new();

        private static string PairKey(string a, string b) =>
            string.CompareOrdinal(a, b) <= 0 ? a + "|" + b : b + "|" + a;

        public Task<FriendshipRecord?> LoadFriendshipAsync(IExecutionContext context, IGameApiClient apiClient, string accountA, string accountB)
            => Task.FromResult(Records.TryGetValue(PairKey(accountA, accountB), out var record) ? Clone(record) : null);

        public Task SaveFriendshipAsync(IExecutionContext context, IGameApiClient apiClient, string accountA, string accountB, FriendshipRecord record)
        {
            Records[PairKey(accountA, accountB)] = Clone(record);
            return Task.CompletedTask;
        }

        public Task DeleteFriendshipAsync(IExecutionContext context, IGameApiClient apiClient, string accountA, string accountB)
        {
            Records.Remove(PairKey(accountA, accountB));
            return Task.CompletedTask;
        }

        public Task<FriendsIndexState> LoadIndexAsync(IExecutionContext context, IGameApiClient apiClient, string accountId)
        {
            if (!Indices.TryGetValue(accountId, out var index))
            {
                index = new FriendsIndexState();
            }

            return Task.FromResult(new FriendsIndexState { CounterpartAccountIds = new List<string>(index.CounterpartAccountIds) });
        }

        public Task SaveIndexAsync(IExecutionContext context, IGameApiClient apiClient, string accountId, FriendsIndexState index)
        {
            Indices[accountId] = new FriendsIndexState { CounterpartAccountIds = new List<string>(index.CounterpartAccountIds) };
            return Task.CompletedTask;
        }

        private static FriendshipRecord Clone(FriendshipRecord record) =>
            JsonConvert.DeserializeObject<FriendshipRecord>(JsonConvert.SerializeObject(record))!;
    }

    private sealed class FakeClock : IFriendsClock
    {
        public FakeClock(long now) => UtcNowMs = now;
        public long UtcNowMs { get; set; }
    }

    private sealed class FakeExecutionContext : IExecutionContext
    {
        public FakeExecutionContext(string? playerId) => PlayerId = playerId!;
        public string ProjectId => "project";
        public string PlayerId { get; }
        public string EnvironmentId => "environment";
        public string EnvironmentName => "production";
        public string AccessToken => "token";
        public string UserId => null!;
        public string Issuer => null!;
        public string ServiceToken => "service-token";
        public string AnalyticsUserId => null!;
        public string UnityInstallationId => null!;
        public string CorrelationId => null!;
        public string ScopeId => null!;
        public int CallDepth => 0;
        public ISession Session => null!;
    }
}
