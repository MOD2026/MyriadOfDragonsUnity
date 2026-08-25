using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Chat.Tests;

public sealed class ChatOperationsTests
{
    private const long Now = 10_000_000_000L;

    // ---------------- PostMessage ----------------

    [Test]
    public async Task PostMessage_MissingActorOrBlankFieldsIsRejected()
    {
        var operations = Create();
        var noActor = await operations.PostMessageAsync(new FakeExecutionContext(null), null!, PostRequest("general", "hi"));
        var blankChannel = await operations.PostMessageAsync(Context(), null!, PostRequest("", "hi"));
        var blankText = await operations.PostMessageAsync(Context(), null!, PostRequest("general", "  "));
        Assert.That(noActor.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
        Assert.That(blankChannel.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
        Assert.That(blankText.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
    }

    [Test]
    public async Task PostMessage_OverLengthTextIsRejected()
    {
        string tooLong = new string('x', ChatOperations.MaxMessageLength + 1);
        var result = await Create().PostMessageAsync(Context(), null!, PostRequest("general", tooLong));
        Assert.That(result.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
    }

    [Test]
    public async Task PostMessage_Succeeds_AndStoresTheMessageInTheChannel()
    {
        var store = new FakeStore();
        var result = await Create(store).PostMessageAsync(Context("actor"), null!, PostRequest("general", "hello world"));

        Assert.That(result.Success, Is.True);
        Assert.That(result.MessageId, Is.Not.Null.And.Not.Empty);

        var channel = await store.LoadChannelAsync(null!, null!, "general");
        Assert.That(channel.Messages.Single().Text, Is.EqualTo("hello world"));
        Assert.That(channel.Messages.Single().SenderAccountId, Is.EqualTo("actor"));
    }

    [Test]
    public async Task PostMessage_TrimsOldestOnceOverTheStoredCap()
    {
        var store = new FakeStore();
        var operations = Create(store);
        for (int i = 0; i < ChatOperations.MaxStoredMessagesPerChannel + 5; i++)
        {
            await operations.PostMessageAsync(Context("actor"), null!, PostRequest("general", "msg-" + i));
        }

        var channel = await store.LoadChannelAsync(null!, null!, "general");
        Assert.That(channel.Messages.Count, Is.EqualTo(ChatOperations.MaxStoredMessagesPerChannel));
        Assert.That(channel.Messages.First().Text, Is.EqualTo("msg-5"), "The oldest 5 must be trimmed, keeping exactly the cap's worth of most recent messages.");
    }

    // ---------------- FetchChannelHistory ----------------

    [Test]
    public async Task FetchHistory_MissingActorOrBlankChannelOrBadLimitIsRejected()
    {
        var operations = Create();
        var noActor = await operations.FetchChannelHistoryAsync(new FakeExecutionContext(null), null!, new FetchChannelHistoryRequest { ChannelId = "general" });
        var blankChannel = await operations.FetchChannelHistoryAsync(Context(), null!, new FetchChannelHistoryRequest { ChannelId = "" });
        var zeroLimit = await operations.FetchChannelHistoryAsync(Context(), null!, new FetchChannelHistoryRequest { ChannelId = "general", Limit = 0 });
        var hugeLimit = await operations.FetchChannelHistoryAsync(Context(), null!, new FetchChannelHistoryRequest { ChannelId = "general", Limit = 1000 });
        Assert.That(noActor.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
        Assert.That(blankChannel.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
        Assert.That(zeroLimit.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
        Assert.That(hugeLimit.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
    }

    [Test]
    public async Task FetchHistory_ReturnsMostRecentFirst_RespectingTheLimit()
    {
        var store = new FakeStore();
        var clock = new FakeClock(Now);
        var operations = new ChatOperations(store, clock);
        for (int i = 0; i < 5; i++)
        {
            clock.UtcNowMs = Now + i;
            await operations.PostMessageAsync(Context("actor"), null!, PostRequest("general", "msg-" + i));
        }

        var result = await operations.FetchChannelHistoryAsync(Context("reader"), null!, new FetchChannelHistoryRequest { ChannelId = "general", Limit = 3 });

        Assert.That(result.Success, Is.True);
        Assert.That(result.Messages.Count, Is.EqualTo(3));
        Assert.That(result.Messages.Select(m => m.Text).ToList(), Is.EqualTo(new[] { "msg-4", "msg-3", "msg-2" }));
    }

    [Test]
    public async Task FetchHistory_FiltersOutMessagesFromABlockedSender()
    {
        var store = new FakeStore();
        var operations = Create(store);
        await operations.PostMessageAsync(Context("blocked-sender"), null!, PostRequest("general", "should be hidden"));
        await operations.PostMessageAsync(Context("normal-sender"), null!, PostRequest("general", "should be visible"));
        store.Blocked["reader"] = new HashSet<string> { "blocked-sender" };

        var result = await operations.FetchChannelHistoryAsync(Context("reader"), null!, new FetchChannelHistoryRequest { ChannelId = "general", Limit = 50 });

        Assert.That(result.Messages.Select(m => m.SenderAccountId), Does.Not.Contain("blocked-sender"));
        Assert.That(result.Messages.Select(m => m.SenderAccountId), Does.Contain("normal-sender"));
    }

    [Test]
    public async Task FetchHistory_FilteringIsPerReader_NotGlobal()
    {
        var store = new FakeStore();
        var operations = Create(store);
        await operations.PostMessageAsync(Context("sender"), null!, PostRequest("general", "hello"));
        store.Blocked["reader-a"] = new HashSet<string> { "sender" };

        var readerA = await operations.FetchChannelHistoryAsync(Context("reader-a"), null!, new FetchChannelHistoryRequest { ChannelId = "general" });
        var readerB = await operations.FetchChannelHistoryAsync(Context("reader-b"), null!, new FetchChannelHistoryRequest { ChannelId = "general" });

        Assert.That(readerA.Messages, Is.Empty, "reader-a blocked the sender - must see nothing.");
        Assert.That(readerB.Messages.Count, Is.EqualTo(1), "reader-b has no block/mute of the sender - must see the message normally.");
    }

    [Test]
    public async Task FetchHistory_EmptyChannel_ReturnsSuccessWithNoMessages()
    {
        var result = await Create().FetchChannelHistoryAsync(Context(), null!, new FetchChannelHistoryRequest { ChannelId = "unused-channel" });
        Assert.That(result.Success, Is.True);
        Assert.That(result.Messages, Is.Empty);
    }

    // ---------------- Contract ----------------

    [Test]
    public void ResponseSerializationUsesTheDocumentedCamelCaseContract()
    {
        var result = new PostMessageResult { Success = true, MessageId = "m1" };
        Assert.That(JsonConvert.SerializeObject(result), Does.Contain("\"messageId\":\"m1\""));

        var history = new FetchChannelHistoryResult
        {
            Success = true,
            Messages = new List<ChatMessageSummary> { new() { SenderAccountId = "s1", Text = "hi", SentUtcMs = 5 } },
        };
        Assert.That(JsonConvert.SerializeObject(history), Does.Contain("\"senderAccountId\":\"s1\""));
    }

    private static ChatOperations Create(FakeStore? store = null) => new(store ?? new FakeStore(), new FakeClock(Now));

    private static FakeExecutionContext Context(string playerId = "actor") => new(playerId);

    private static PostMessageRequest PostRequest(string channelId, string text) => new() { ChannelId = channelId, Text = text };

    /// <summary>In-memory reference implementation of <see cref="IChatStore"/> - same pattern as
    /// Bazaar/Friends' own FakeStore. <see cref="Blocked"/> is keyed by reader account id and lets
    /// a test declare "this reader has blocked/muted these senders" directly, rather than needing
    /// to fabricate SocialSafety's own Cloud Save records.</summary>
    private sealed class FakeStore : IChatStore
    {
        public Dictionary<string, ChatChannelState> Channels { get; } = new();
        public Dictionary<string, HashSet<string>> Blocked { get; } = new();

        public Task<ChatChannelState> LoadChannelAsync(IExecutionContext context, IGameApiClient apiClient, string channelId)
        {
            if (!Channels.TryGetValue(channelId, out var channel))
            {
                channel = new ChatChannelState();
            }

            return Task.FromResult(Clone(channel));
        }

        public Task SaveChannelAsync(IExecutionContext context, IGameApiClient apiClient, string channelId, ChatChannelState state)
        {
            Channels[channelId] = Clone(state);
            return Task.CompletedTask;
        }

        public Task<HashSet<string>> LoadBlockedOrMutedSendersAsync(IExecutionContext context, IGameApiClient apiClient, string actorAccountId, IReadOnlyList<string> candidateSenderAccountIds, long nowUtcMs)
        {
            var result = new HashSet<string>();
            if (Blocked.TryGetValue(actorAccountId, out var blockedSet))
            {
                foreach (string sender in candidateSenderAccountIds)
                {
                    if (blockedSet.Contains(sender))
                    {
                        result.Add(sender);
                    }
                }
            }

            return Task.FromResult(result);
        }

        private static ChatChannelState Clone(ChatChannelState state) =>
            JsonConvert.DeserializeObject<ChatChannelState>(JsonConvert.SerializeObject(state))!;
    }

    private sealed class FakeClock : IChatClock
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
