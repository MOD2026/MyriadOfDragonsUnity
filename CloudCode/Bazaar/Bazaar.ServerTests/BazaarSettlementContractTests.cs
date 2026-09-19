using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NUnit.Framework;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Bazaar.Tests;

/// <summary>
/// Non-production settlement CONTRACT tests for the Bazaar module: idempotency, authorization,
/// reconciliation and failure handling around BuyItemAsync / CancelListingAsync. Runs entirely
/// against an in-memory, fault-injectable store - no Cloud Save, no Cloud Code deployment, no real
/// account, no credential, no production call, and no change to any module code.
///
/// WHY THIS EXISTS: BazaarOperations.BuyItemAsync's own doc comment states it "does NOT provide
/// true all-or-nothing atomicity" across the buyer wallet, seller wallet, instance and listing
/// saves, and names atomic transactions/escrow as a still-open backend dependency. That is the
/// highest-priority open blocker on the only deployed module that moves currency. The existing
/// BazaarOperationsTests cover the happy path and single-call idempotency; nothing yet pins down
/// what a mid-settlement storage failure leaves behind, or gives a reconciliation job a tested
/// definition of "consistent". This file supplies both.
///
/// TWO KINDS OF TEST, deliberately labelled:
///  - Contract tests (plain names): behavior that is guaranteed today and must stay guaranteed.
///  - KnownGap_ tests: characterize the CURRENT, documented non-atomic behavior. They pass today
///    and are expected to FAIL (and be rewritten to assert the safe behavior) the moment atomic
///    settlement/escrow lands - that failure is the signal, not a regression.
/// </summary>
public sealed class BazaarSettlementContractTests
{
    private const long Now = 10_000_000_000L;

    // ---------------- Reconciliation ----------------

    [TestCase(1)]
    [TestCase(7)]
    [TestCase(99)]
    [TestCase(100)]
    [TestCase(101)]
    [TestCase(999)]
    [TestCase(12_345)]
    public async Task Reconciliation_SuccessfulSale_ConservesEveryCredit_ForAnyAskPrice(int ask)
    {
        var store = new FaultStore();
        store.SeedActiveListing("listing-1", "seller", ask);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 1_000_000 };
        var before = store.Snapshot();

        var result = await Create(store).BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-1"));

        Assert.That(result.Success, Is.True, result.ErrorCode);
        var report = Reconcile(before, store.Snapshot(), result, "listing-1", "buyer", "seller");
        Assert.That(report, Is.Empty, string.Join("; ", report));
    }

    [Test]
    public async Task Reconciliation_Checker_DetectsAPartialSettlement()
    {
        // Proves the checker is not vacuous: a failure after the buyer was debited but before the
        // seller was credited must be reported as inconsistent, so a real reconciliation job built
        // on the same definition would actually catch it.
        var store = new FaultStore { FailOnSaveNumber = 2, FailureCode = "STORAGE_UNAVAILABLE" };
        store.SeedActiveListing("listing-1", "seller", 100);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 500 };
        var before = store.Snapshot();

        var result = await Create(store).BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-1"));

        Assert.That(result.Success, Is.False);
        var fakeSuccess = new BuyResult { Success = true, PricePaidCredits = 100, SellerReceivedCredits = 88, TaxBurnedCredits = 6, TaxTreasuryCredits = 6 };
        var report = Reconcile(before, store.Snapshot(), fakeSuccess, "listing-1", "buyer", "seller");
        Assert.That(report, Is.Not.Empty, "A buyer-debited/seller-uncredited state must fail reconciliation.");
    }

    // ---------------- Failure handling (guaranteed today) ----------------

    [Test]
    public async Task Failure_BeforeAnyWrite_LeavesNoStateChange_AndNoIdempotencyRecord_AndRetrySucceeds()
    {
        var store = new FaultStore { FailOnSaveNumber = 1, FailureCode = "STORAGE_UNAVAILABLE" };
        store.SeedActiveListing("listing-1", "seller", 100);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 500 };
        var before = store.Snapshot();
        var operations = Create(store);

        var failed = await operations.BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-1"));

        Assert.That(failed.Success, Is.False);
        Assert.That(failed.ErrorCode, Is.EqualTo("STORAGE_UNAVAILABLE"), "The real storage error code must surface to the caller, not be swallowed.");
        Assert.That(store.Snapshot(), Is.EqualTo(before), "Nothing may change when the very first write fails.");
        Assert.That(store.IdempotencyRecords, Is.Empty, "A failed purchase must not be cached as the idempotent outcome.");

        store.FailOnSaveNumber = 0;
        var retry = await operations.BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-1"));
        Assert.That(retry.Success, Is.True, retry.ErrorCode);
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(400));
    }

    [TestCase("CONFLICT")]
    [TestCase("STORAGE_UNAVAILABLE")]
    public async Task Failure_ErrorCodeFromStorage_IsSurfacedVerbatim_OnBuyAndCancel(string code)
    {
        var buyStore = new FaultStore { FailOnSaveNumber = 1, FailureCode = code };
        buyStore.SeedActiveListing("listing-1", "seller", 100);
        buyStore.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 500 };
        var buy = await Create(buyStore).BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-1"));
        Assert.That(buy.ErrorCode, Is.EqualTo(code));

        var cancelStore = new FaultStore { FailOnSaveNumber = 1, FailureCode = code };
        cancelStore.SeedActiveListing("listing-1", "seller", 100);
        var cancel = await Create(cancelStore).CancelListingAsync(Context("seller"), null!, new CancelListingRequest { ListingId = "listing-1" });
        Assert.That(cancel.Success, Is.False);
        Assert.That(cancel.ErrorCode, Is.EqualTo(code));
    }

    [Test]
    public async Task Failure_ValidationRejections_AreNotCachedAsTheIdempotentOutcome()
    {
        // INSUFFICIENT_CREDITS is a rejection, not a settled purchase: once the buyer is funded,
        // retrying with the SAME key must be allowed to succeed rather than replaying the rejection.
        var store = new FaultStore();
        store.SeedActiveListing("listing-1", "seller", 100);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 10 };
        var operations = Create(store);

        var rejected = await operations.BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-1"));
        Assert.That(rejected.ErrorCode, Is.EqualTo("INSUFFICIENT_CREDITS"));
        Assert.That(store.IdempotencyRecords, Is.Empty);

        store.Wallets["buyer"].BalanceCredits = 500;
        var retry = await operations.BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-1"));
        Assert.That(retry.Success, Is.True, retry.ErrorCode);
    }

    // ---------------- Failure handling: KNOWN GAPS (documented non-atomicity) ----------------

    [Test]
    public async Task KnownGap_FailureAfterBuyerDebit_LeavesBuyerDebited_SellerUncredited_ListingStillActive()
    {
        // Documents the open "atomic transactions and escrow" dependency named in
        // BazaarOperations.BuyItemAsync's doc comment. Expected to flip when that lands.
        var store = new FaultStore { FailOnSaveNumber = 2, FailureCode = "STORAGE_UNAVAILABLE" };
        store.SeedActiveListing("listing-1", "seller", 100);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 500 };

        var result = await Create(store).BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-1"));

        Assert.That(result.ErrorCode, Is.EqualTo("STORAGE_UNAVAILABLE"));
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(400), "KNOWN GAP: buyer debit persisted before the failure.");
        Assert.That(store.Wallets.ContainsKey("seller") ? store.Wallets["seller"].BalanceCredits : 0, Is.EqualTo(0), "KNOWN GAP: seller never credited.");
        Assert.That(store.Listings["listing-1"].State, Is.EqualTo(BazaarListingState.Active), "KNOWN GAP: listing still purchasable.");
    }

    [Test]
    public async Task KnownGap_RetryAfterPartialFailure_WithSameKey_ChargesTheBuyerAgain()
    {
        // The idempotency record is written only after full success, so a retry after a partial
        // failure is a brand-new attempt against already-debited state. This is the concrete
        // double-charge hazard atomic settlement/escrow must close.
        var store = new FaultStore { FailOnSaveNumber = 2, FailureCode = "STORAGE_UNAVAILABLE" };
        store.SeedActiveListing("listing-1", "seller", 100);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 500 };
        var operations = Create(store);

        await operations.BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-1"));
        store.FailOnSaveNumber = 0;
        var retry = await operations.BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-1"));

        Assert.That(retry.Success, Is.True, retry.ErrorCode);
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(300), "KNOWN GAP: buyer charged twice (500 -> 400 -> 300) for one item.");
    }

    // ---------------- Authorization ----------------

    [Test]
    public async Task Authorization_UnauthenticatedCaller_IsRejectedOnEveryEndpoint_WithoutTouchingStorage()
    {
        var store = new FaultStore();
        store.SeedActiveListing("listing-1", "seller", 100);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 500 };
        var before = store.Snapshot();
        var operations = Create(store);
        var anonymous = Context(null);

        var buy = await operations.BuyItemAsync(anonymous, null!, Buy("listing-1", "key-1"));
        var cancel = await operations.CancelListingAsync(anonymous, null!, new CancelListingRequest { ListingId = "listing-1" });
        var list = await operations.ListItemAsync(anonymous, null!, new ListItemRequest { InstanceId = "listing-1-instance", AskCredits = 100 });
        var wallet = await operations.GetWalletAsync(anonymous, null!);
        var query = await operations.QueryListingsAsync(anonymous, null!, new QueryListingsRequest { PageSize = 10 });

        Assert.That(buy.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
        Assert.That(cancel.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
        Assert.That(list.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
        Assert.That(wallet.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
        Assert.That(query.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
        Assert.That(store.Snapshot(), Is.EqualTo(before));
        Assert.That(store.SaveCount, Is.EqualTo(0), "A rejected anonymous call must not perform a single write.");
    }

    [Test]
    public async Task Authorization_NonSellerCancel_IsRejected_AndMutatesNothing()
    {
        var store = new FaultStore();
        store.SeedActiveListing("listing-1", "seller", 100);
        var before = store.Snapshot();

        var result = await Create(store).CancelListingAsync(Context("intruder"), null!, new CancelListingRequest { ListingId = "listing-1" });

        Assert.That(result.ErrorCode, Is.EqualTo("NOT_SELLER"));
        Assert.That(store.Snapshot(), Is.EqualTo(before));
        Assert.That(store.SaveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Authorization_SelfTrade_IsRejected_AndMutatesNothing()
    {
        var store = new FaultStore();
        store.SeedActiveListing("listing-1", "seller", 100);
        store.Wallets["seller"] = new WalletState { AccountId = "seller", BalanceCredits = 500 };
        var before = store.Snapshot();

        var result = await Create(store).BuyItemAsync(Context("seller"), null!, Buy("listing-1", "key-1"));

        Assert.That(result.ErrorCode, Is.EqualTo("SELF_TRADE_NOT_ALLOWED"));
        Assert.That(store.Snapshot(), Is.EqualTo(before));
        Assert.That(store.SaveCount, Is.EqualTo(0));
    }

    // ---------------- Idempotency ----------------

    [Test]
    public async Task Idempotency_KeyIsScopedPerBuyer_AnotherBuyerCannotReplayOrHijackIt()
    {
        var store = new FaultStore();
        store.SeedActiveListing("listing-1", "seller", 100);
        store.Wallets["buyerA"] = new WalletState { AccountId = "buyerA", BalanceCredits = 500 };
        store.Wallets["buyerB"] = new WalletState { AccountId = "buyerB", BalanceCredits = 500 };
        var operations = Create(store);

        var first = await operations.BuyItemAsync(Context("buyerA"), null!, Buy("listing-1", "shared-key"));
        var other = await operations.BuyItemAsync(Context("buyerB"), null!, Buy("listing-1", "shared-key"));

        Assert.That(first.Success, Is.True, first.ErrorCode);
        Assert.That(other.Success, Is.False, "buyerB must not receive buyerA's cached success.");
        Assert.That(other.ErrorCode, Is.EqualTo("LISTING_NOT_AVAILABLE"));
        Assert.That(store.Wallets["buyerB"].BalanceCredits, Is.EqualTo(500), "buyerB must never be charged for buyerA's purchase.");
    }

    [Test]
    public async Task Idempotency_ReplayAfterSuccess_PerformsNoAdditionalWrites()
    {
        var store = new FaultStore();
        store.SeedActiveListing("listing-1", "seller", 100);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 500 };
        var operations = Create(store);

        await operations.BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-1"));
        int savesAfterFirst = store.SaveCount;
        var replay = await operations.BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-1"));

        Assert.That(replay.Success, Is.True);
        Assert.That(store.SaveCount, Is.EqualTo(savesAfterFirst), "An idempotent replay must be a pure read.");
    }

    [Test]
    public async Task StateMachine_BuyThenCancel_AndCancelThenBuy_BothRejectTheLoser_WithoutMutation()
    {
        var soldFirst = new FaultStore();
        soldFirst.SeedActiveListing("listing-1", "seller", 100);
        soldFirst.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 500 };
        var ops1 = Create(soldFirst);
        await ops1.BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-1"));
        var afterSale = soldFirst.Snapshot();
        var cancelAfterSale = await ops1.CancelListingAsync(Context("seller"), null!, new CancelListingRequest { ListingId = "listing-1" });
        Assert.That(cancelAfterSale.ErrorCode, Is.EqualTo("LISTING_NOT_AVAILABLE"));
        Assert.That(soldFirst.Snapshot(), Is.EqualTo(afterSale));

        var cancelledFirst = new FaultStore();
        cancelledFirst.SeedActiveListing("listing-1", "seller", 100);
        cancelledFirst.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 500 };
        var ops2 = Create(cancelledFirst);
        await ops2.CancelListingAsync(Context("seller"), null!, new CancelListingRequest { ListingId = "listing-1" });
        var afterCancel = cancelledFirst.Snapshot();
        var buyAfterCancel = await ops2.BuyItemAsync(Context("buyer"), null!, Buy("listing-1", "key-2"));
        Assert.That(buyAfterCancel.ErrorCode, Is.EqualTo("LISTING_NOT_AVAILABLE"));
        Assert.That(cancelledFirst.Snapshot(), Is.EqualTo(afterCancel));
    }

    // ---------------- Reconciliation definition ----------------

    /// <summary>Returns a list of violations (empty == consistent). This is the tested definition
    /// of a settled sale that a real reconciliation job would apply: credits are conserved across
    /// buyer + seller + burn + treasury, and instance/listing/index agree with the wallets.</summary>
    private static List<string> Reconcile(StoreSnapshot before, StoreSnapshot after, BuyResult result, string listingId, string buyerId, string sellerId)
    {
        var violations = new List<string>();
        int buyerDelta = after.Balance(buyerId) - before.Balance(buyerId);
        int sellerDelta = after.Balance(sellerId) - before.Balance(sellerId);

        if (buyerDelta != -result.PricePaidCredits) violations.Add($"buyer delta {buyerDelta} != -{result.PricePaidCredits}");
        if (sellerDelta != result.SellerReceivedCredits) violations.Add($"seller delta {sellerDelta} != {result.SellerReceivedCredits}");
        if (buyerDelta + sellerDelta + result.TaxBurnedCredits + result.TaxTreasuryCredits != 0)
            violations.Add($"credits not conserved: {buyerDelta} + {sellerDelta} + burn {result.TaxBurnedCredits} + treasury {result.TaxTreasuryCredits} != 0");
        if (result.SellerReceivedCredits + result.TaxBurnedCredits + result.TaxTreasuryCredits != result.PricePaidCredits)
            violations.Add("price != seller + burn + treasury");

        string instanceId = listingId + "-instance";
        if (!after.InstanceOwner.TryGetValue(instanceId, out var owner) || owner != buyerId) violations.Add($"instance owner is '{owner}', expected '{buyerId}'");
        if (after.ListingState.GetValueOrDefault(listingId) != BazaarListingState.Sold) violations.Add("listing not Sold");
        if (after.IndexedListingIds.Contains(listingId)) violations.Add("listing still in the active index");
        return violations;
    }

    // ---------------- Helpers ----------------

    private static BazaarOperations Create(FaultStore store) => new(store, new FixedClock(Now), new FixedRules());
    private static Ctx Context(string? playerId) => new(playerId);
    private static BuyItemRequest Buy(string listingId, string key) => new() { ListingId = listingId, IdempotencyKey = key };

    private sealed class StoreSnapshot : IEquatable<StoreSnapshot>
    {
        public Dictionary<string, int> Balances { get; init; } = new();
        public Dictionary<string, string> InstanceOwner { get; init; } = new();
        public Dictionary<string, ItemInstanceState> InstanceState { get; init; } = new();
        public Dictionary<string, BazaarListingState> ListingState { get; init; } = new();
        public List<string> IndexedListingIds { get; init; } = new();

        public int Balance(string accountId) => Balances.GetValueOrDefault(accountId);

        private string Key() => JsonConvert.SerializeObject(new
        {
            b = Balances.OrderBy(p => p.Key), o = InstanceOwner.OrderBy(p => p.Key), s = InstanceState.OrderBy(p => p.Key),
            l = ListingState.OrderBy(p => p.Key), i = IndexedListingIds.OrderBy(x => x),
        });

        public bool Equals(StoreSnapshot? other) => other != null && Key() == other.Key();
        public override bool Equals(object? obj) => Equals(obj as StoreSnapshot);
        public override int GetHashCode() => Key().GetHashCode();
        public override string ToString() => Key();
    }

    /// <summary>In-memory IBazaarStore that can throw BazaarStorageException on the Nth
    /// wallet/instance/listing/index save, to exercise mid-settlement failures.</summary>
    private sealed class FaultStore : IBazaarStore
    {
        public Dictionary<string, ItemInstance> Instances { get; } = new();
        public Dictionary<string, BazaarListing> Listings { get; } = new();
        public Dictionary<string, WalletState> Wallets { get; } = new();
        public BazaarListingIndex Index { get; } = new();
        public Dictionary<(string, string), BuyResult> IdempotencyRecords { get; } = new();

        public int FailOnSaveNumber { get; set; }
        public string FailureCode { get; set; } = "STORAGE_UNAVAILABLE";
        public int SaveCount { get; private set; }

        public void SeedActiveListing(string listingId, string sellerId, int ask)
        {
            string instanceId = listingId + "-instance";
            Instances[instanceId] = new ItemInstance
            {
                InstanceId = instanceId, DefinitionId = "card.example", OwnerId = sellerId, Rarity = 2, Tradeable = true,
                State = ItemInstanceState.Listed, LastAcquiredUtcMs = Now - (long)TimeSpan.FromDays(30).TotalMilliseconds,
            };
            Listings[listingId] = new BazaarListing
            {
                ListingId = listingId, InstanceId = instanceId, SellerId = sellerId, AskCredits = ask,
                State = BazaarListingState.Active, CreatedUtcMs = Now,
            };
            Index.ActiveListingIds.Add(listingId);
        }

        public StoreSnapshot Snapshot() => new()
        {
            Balances = Wallets.ToDictionary(p => p.Key, p => p.Value.BalanceCredits),
            InstanceOwner = Instances.ToDictionary(p => p.Key, p => p.Value.OwnerId),
            InstanceState = Instances.ToDictionary(p => p.Key, p => p.Value.State),
            ListingState = Listings.ToDictionary(p => p.Key, p => p.Value.State),
            IndexedListingIds = new List<string>(Index.ActiveListingIds),
        };

        private void CountSave()
        {
            SaveCount++;
            if (FailOnSaveNumber > 0 && SaveCount == FailOnSaveNumber)
            {
                throw new BazaarStorageException(FailureCode);
            }
        }

        public Task<ItemInstance?> LoadInstanceAsync(IExecutionContext context, IGameApiClient apiClient, string instanceId)
            => Task.FromResult(Instances.TryGetValue(instanceId, out var v) ? Clone(v) : null);

        public Task SaveInstanceAsync(IExecutionContext context, IGameApiClient apiClient, ItemInstance instance)
        {
            CountSave();
            Instances[instance.InstanceId] = Clone(instance);
            return Task.CompletedTask;
        }

        public Task<BazaarListing?> LoadListingAsync(IExecutionContext context, IGameApiClient apiClient, string listingId)
            => Task.FromResult(Listings.TryGetValue(listingId, out var v) ? Clone(v) : null);

        public Task SaveListingAsync(IExecutionContext context, IGameApiClient apiClient, BazaarListing listing)
        {
            CountSave();
            Listings[listing.ListingId] = Clone(listing);
            return Task.CompletedTask;
        }

        public Task<WalletState> LoadWalletAsync(IExecutionContext context, IGameApiClient apiClient, string accountId)
            => Task.FromResult(Wallets.TryGetValue(accountId, out var v) ? Clone(v) : new WalletState { AccountId = accountId });

        public Task SaveWalletAsync(IExecutionContext context, IGameApiClient apiClient, WalletState wallet)
        {
            CountSave();
            Wallets[wallet.AccountId] = Clone(wallet);
            return Task.CompletedTask;
        }

        public Task<BuyResult?> TryGetIdempotentBuyResultAsync(IExecutionContext context, IGameApiClient apiClient, string buyerId, string idempotencyKey)
            => Task.FromResult(IdempotencyRecords.TryGetValue((buyerId, idempotencyKey), out var v) ? v : null);

        public Task SaveIdempotentBuyResultAsync(IExecutionContext context, IGameApiClient apiClient, string buyerId, string idempotencyKey, BuyResult result)
        {
            IdempotencyRecords[(buyerId, idempotencyKey)] = result;
            return Task.CompletedTask;
        }

        public Task<BazaarListingIndex> LoadIndexAsync(IExecutionContext context, IGameApiClient apiClient)
            => Task.FromResult(new BazaarListingIndex { ActiveListingIds = new List<string>(Index.ActiveListingIds) });

        public Task SaveIndexAsync(IExecutionContext context, IGameApiClient apiClient, BazaarListingIndex index)
        {
            CountSave();
            Index.ActiveListingIds.Clear();
            Index.ActiveListingIds.AddRange(index.ActiveListingIds);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BazaarListing>> LoadListingsBatchAsync(IExecutionContext context, IGameApiClient apiClient, IReadOnlyList<string> listingIds)
            => Task.FromResult<IReadOnlyList<BazaarListing>>(listingIds.Where(Listings.ContainsKey).Select(id => Clone(Listings[id])).ToList());

        private static ItemInstance Clone(ItemInstance v) => JsonConvert.DeserializeObject<ItemInstance>(JsonConvert.SerializeObject(v))!;
        private static BazaarListing Clone(BazaarListing v) => JsonConvert.DeserializeObject<BazaarListing>(JsonConvert.SerializeObject(v))!;
        private static WalletState Clone(WalletState v) => JsonConvert.DeserializeObject<WalletState>(JsonConvert.SerializeObject(v))!;
    }

    private sealed class FixedClock : IBazaarClock
    {
        public FixedClock(long now) => UtcNowMs = now;
        public long UtcNowMs { get; }
    }

    private sealed class FixedRules : IBazaarRulesConfiguration
    {
        public Task<BazaarRulesConfiguration> LoadAsync(IExecutionContext context, IGameApiClient apiClient)
            => Task.FromResult(BazaarRulesConfiguration.Fallback());
    }

    private sealed class Ctx : IExecutionContext
    {
        public Ctx(string? playerId) => PlayerId = playerId!;
        public string ProjectId => "project";
        public string PlayerId { get; }
        public string EnvironmentId => "environment";
        public string EnvironmentName => "nonprod-validation";
        public string AccessToken => "test-token";
        public string UserId => null!;
        public string Issuer => null!;
        public string ServiceToken => "test-service-token";
        public string AnalyticsUserId => null!;
        public string UnityInstallationId => null!;
        public string CorrelationId => null!;
        public string ScopeId => null!;
        public int CallDepth => 0;
        public ISession Session => null!;
    }
}
