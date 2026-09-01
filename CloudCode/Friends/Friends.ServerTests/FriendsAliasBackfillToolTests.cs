using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Friends.Tests;

/// <summary>Focused tests for the TEMPORARY one-time alias-reverse-index backfill tool
/// (FriendsAliasBackfillTool.cs) - see that file's own doc comment for why it exists and why it
/// must be deleted after use. Reuses the same FakeStore shape as FriendsOperationsTests.cs (a
/// standalone copy here so this test file has no compile dependency on that one, and can be
/// deleted alongside the tool itself without touching the permanent FriendsOperationsTests.cs).</summary>
public sealed class FriendsAliasBackfillToolTests
{
    [Test]
    public async Task FirstRun_ResolvesBothSidesOfEveryCounterpartPair_AndReportsAccurateCounts()
    {
        var store = new FakeStore();
        // a<->b accepted, a<->c pending outgoing (from a's side only, per AddFriendAsync's
        // AddToIndexAsync calls both sides get an index entry even while pending).
        store.Indices["a"] = new FriendsIndexState { CounterpartAccountIds = new List<string> { "b", "c" } };
        store.Indices["b"] = new FriendsIndexState { CounterpartAccountIds = new List<string> { "a" } };
        store.Indices["c"] = new FriendsIndexState { CounterpartAccountIds = new List<string> { "a" } };

        var tool = new FriendsAliasBackfillOperations(store);
        var result = await tool.RunAsync(Context(), null!, new BackfillAliasReverseIndexRequest { AccountIds = new List<string> { "a" } });

        Assert.That(result.AccountsScanned, Is.EqualTo(1));
        Assert.That(result.AccountsFailed, Is.EqualTo(0));
        // Distinct accounts touched: a, b, c = 3 - all newly resolved on a first run.
        Assert.That(result.AliasesUpdated, Is.EqualTo(3));
        Assert.That(result.AliasesSkipped, Is.EqualTo(0));

        foreach (string accountId in new[] { "a", "b", "c" })
        {
            string? alias = await store.LoadCounterpartAliasAsync(null!, null!, accountId);
            Assert.That(alias, Is.Not.Null.And.Not.Empty, $"{accountId} must have a resolved alias after the backfill.");
            string? reverse = await store.ResolveAccountIdFromAliasAsync(null!, null!, alias!);
            Assert.That(reverse, Is.EqualTo(accountId), $"{accountId}'s alias must resolve back to it via the reverse index.");
        }
    }

    [Test]
    public async Task SecondRun_OverTheSameInput_IsACompleteNoOp()
    {
        var store = new FakeStore();
        store.Indices["a"] = new FriendsIndexState { CounterpartAccountIds = new List<string> { "b" } };
        store.Indices["b"] = new FriendsIndexState { CounterpartAccountIds = new List<string> { "a" } };

        var tool = new FriendsAliasBackfillOperations(store);
        var request = new BackfillAliasReverseIndexRequest { AccountIds = new List<string> { "a", "b" } };

        var first = await tool.RunAsync(Context(), null!, request);
        Assert.That(first.AliasesUpdated, Is.EqualTo(2));
        Assert.That(first.AliasesSkipped, Is.EqualTo(0));

        string aliasForAAfterFirstRun = (await store.LoadCounterpartAliasAsync(null!, null!, "a"))!;
        string aliasForBAfterFirstRun = (await store.LoadCounterpartAliasAsync(null!, null!, "b"))!;

        // Fresh tool instance over the SAME durable store - simulates a real second invocation.
        var second = await new FriendsAliasBackfillOperations(store).RunAsync(Context(), null!, request);

        Assert.That(second.AccountsScanned, Is.EqualTo(2));
        Assert.That(second.AliasesUpdated, Is.EqualTo(0), "A second run over the same input must write nothing new.");
        Assert.That(second.AliasesSkipped, Is.EqualTo(2));
        Assert.That(second.AccountsFailed, Is.EqualTo(0));

        // Aliases themselves must be byte-identical across both runs - never re-minted.
        Assert.That(await store.LoadCounterpartAliasAsync(null!, null!, "a"), Is.EqualTo(aliasForAAfterFirstRun));
        Assert.That(await store.LoadCounterpartAliasAsync(null!, null!, "b"), Is.EqualTo(aliasForBAfterFirstRun));
    }

    [Test]
    public async Task AccountAlreadyHavingAnAlias_ButMissingOnlyTheReverseEntry_IsUpdatedNotSkipped()
    {
        // Simulates the exact real gap this tool exists to close: 034b1141 shipped with an alias
        // already minted for some accounts (from before the fix) but no reverse-index entry.
        var store = new FakeStore();
        store.Indices["a"] = new FriendsIndexState { CounterpartAccountIds = new List<string> { "b" } };
        store.Indices["b"] = new FriendsIndexState { CounterpartAccountIds = new List<string> { "a" } };
        await store.SaveCounterpartAliasAsync(null!, null!, "b", "pre-existing-alias-for-b");
        // Deliberately no SaveAliasReverseMappingAsync call for "b" - this is the gap.

        var tool = new FriendsAliasBackfillOperations(store);
        var result = await tool.RunAsync(Context(), null!, new BackfillAliasReverseIndexRequest { AccountIds = new List<string> { "a" } });

        Assert.That(result.AliasesUpdated, Is.EqualTo(2), "Both a (new alias+reverse) and b (reverse entry only) count as updated.");
        Assert.That(await store.LoadCounterpartAliasAsync(null!, null!, "b"), Is.EqualTo("pre-existing-alias-for-b"), "The pre-existing alias for b must be preserved, never re-minted.");
        Assert.That(await store.ResolveAccountIdFromAliasAsync(null!, null!, "pre-existing-alias-for-b"), Is.EqualTo("b"));
    }

    [Test]
    public async Task FailedAccount_IsReportedAndDoesNotStopTheRestOfTheBatch()
    {
        var store = new FakeStore(failOnAccountId: "bad-account");
        store.Indices["good"] = new FriendsIndexState();
        store.Indices["bad-account"] = new FriendsIndexState();

        var tool = new FriendsAliasBackfillOperations(store);
        var result = await tool.RunAsync(Context(), null!, new BackfillAliasReverseIndexRequest { AccountIds = new List<string> { "good", "bad-account" } });

        Assert.That(result.AccountsScanned, Is.EqualTo(2));
        Assert.That(result.AccountsFailed, Is.EqualTo(1));
        Assert.That(result.FailedAccountIds, Is.EquivalentTo(new[] { "bad-account" }));
        Assert.That(result.Errors, Has.Count.EqualTo(1));
        // "bad-account" resolves its OWN alias successfully (that happens before LoadIndexAsync,
        // which is where the simulated storage failure is raised) - so both "good" and
        // "bad-account" contribute one resolved alias each before the batch moves on. This is
        // correct, not a bug: partial per-account progress already written is real and should not
        // be discarded just because a later step for that same account failed.
        Assert.That(result.AliasesUpdated, Is.EqualTo(2), "The failure on one account must not prevent the other account in the same batch from being resolved, and partial progress on the failing account itself is preserved.");
    }

    [Test]
    public async Task BlankAndDuplicateAccountIdsInInput_AreHandledSafely()
    {
        var store = new FakeStore();
        store.Indices["a"] = new FriendsIndexState();

        var tool = new FriendsAliasBackfillOperations(store);
        var result = await tool.RunAsync(Context(), null!, new BackfillAliasReverseIndexRequest { AccountIds = new List<string> { "a", "", "  ", "a" } });

        Assert.That(result.AccountsScanned, Is.EqualTo(2), "Blank entries are skipped before counting as scanned; the duplicate 'a' is still scanned twice.");
        Assert.That(result.AliasesUpdated, Is.EqualTo(1), "The duplicate scan of 'a' must not double-write or double-count its alias resolution.");
        Assert.That(result.AliasesSkipped, Is.EqualTo(0));
    }

    private static FakeExecutionContext Context() => new("actor");

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

    private sealed class FakeStore : IFriendsStore
    {
        private readonly string? _failOnAccountId;
        public Dictionary<string, FriendsIndexState> Indices { get; } = new();
        public Dictionary<string, string> Aliases { get; } = new();
        public Dictionary<string, string> AliasReverse { get; } = new();

        public FakeStore(string? failOnAccountId = null) => _failOnAccountId = failOnAccountId;

        public Task<FriendshipRecord?> LoadFriendshipAsync(IExecutionContext context, IGameApiClient apiClient, string accountA, string accountB) => Task.FromResult<FriendshipRecord?>(null);
        public Task SaveFriendshipAsync(IExecutionContext context, IGameApiClient apiClient, string accountA, string accountB, FriendshipRecord record) => Task.CompletedTask;
        public Task DeleteFriendshipAsync(IExecutionContext context, IGameApiClient apiClient, string accountA, string accountB) => Task.CompletedTask;

        public Task<FriendsIndexState> LoadIndexAsync(IExecutionContext context, IGameApiClient apiClient, string accountId)
        {
            if (accountId == _failOnAccountId)
            {
                throw new FriendsStorageException("STORAGE_UNAVAILABLE");
            }

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

        public Task<string?> LoadCounterpartAliasAsync(IExecutionContext context, IGameApiClient apiClient, string counterpartAccountId)
            => Task.FromResult(Aliases.TryGetValue(counterpartAccountId, out var alias) ? alias : null);

        public Task SaveCounterpartAliasAsync(IExecutionContext context, IGameApiClient apiClient, string counterpartAccountId, string aliasId)
        {
            Aliases[counterpartAccountId] = aliasId;
            return Task.CompletedTask;
        }

        public Task<string?> ResolveAccountIdFromAliasAsync(IExecutionContext context, IGameApiClient apiClient, string aliasId)
            => Task.FromResult(AliasReverse.TryGetValue(aliasId, out var accountId) ? accountId : null);

        public Task SaveAliasReverseMappingAsync(IExecutionContext context, IGameApiClient apiClient, string aliasId, string accountId)
        {
            AliasReverse[aliasId] = accountId;
            return Task.CompletedTask;
        }
    }
}
