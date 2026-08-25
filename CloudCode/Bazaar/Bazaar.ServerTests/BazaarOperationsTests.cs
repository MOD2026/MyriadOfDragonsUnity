using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using NUnit.Framework;
using Newtonsoft.Json;
using Unity.Services.CloudCode.Apis;
using Unity.Services.CloudCode.Core;

namespace MyriadOfDragons.CloudCode.Bazaar.Tests;

public sealed class BazaarOperationsTests
{
    private const long Now = 10_000_000_000L; // arbitrary fixed instant, well past any hold window from zero

    // ---------------- Listing ----------------

    [Test]
    public async Task ListItem_MissingActorOrBlankInstanceOrNonPositiveAskIsRejected()
    {
        var operations = Create();
        var noActor = await operations.ListItemAsync(new FakeExecutionContext(null), null!, ListRequest("a", 100));
        var blank = await operations.ListItemAsync(Context(), null!, ListRequest("", 100));
        var zero = await operations.ListItemAsync(Context(), null!, ListRequest("a", 0));
        Assert.That(noActor.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
        Assert.That(blank.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
        Assert.That(zero.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
    }

    [Test]
    public async Task ListItem_UnknownInstanceIsRejected()
    {
        var result = await Create().ListItemAsync(Context(), null!, ListRequest("does-not-exist", 100));
        Assert.That(result.ErrorCode, Is.EqualTo("INSTANCE_NOT_FOUND"));
    }

    [Test]
    public async Task ListItem_NotOwnedByCallerIsRejected()
    {
        var store = new FakeStore();
        store.Seed(EligibleInstance("inst-1", ownerId: "someone-else"));
        var result = await Create(store).ListItemAsync(Context(), null!, ListRequest("inst-1", 100));
        Assert.That(result.ErrorCode, Is.EqualTo("NOT_OWNER"));
    }

    [TestCase(false, 2)]
    [TestCase(true, 5)]
    [TestCase(true, 7)]
    public async Task ListItem_NotTradeableOrOutOfRarityBandIsRejected(bool tradeable, int rarity)
    {
        var store = new FakeStore();
        var instance = EligibleInstance("inst-1", ownerId: "actor");
        instance.Tradeable = tradeable;
        instance.Rarity = rarity;
        store.Seed(instance);

        var result = await Create(store).ListItemAsync(Context(), null!, ListRequest("inst-1", 100));
        Assert.That(result.ErrorCode, Is.EqualTo("NOT_TRADEABLE"));
    }

    [Test]
    public async Task ListItem_AlreadyListedIsRejected()
    {
        var store = new FakeStore();
        var instance = EligibleInstance("inst-1", ownerId: "actor");
        instance.State = ItemInstanceState.Listed;
        store.Seed(instance);

        var result = await Create(store).ListItemAsync(Context(), null!, ListRequest("inst-1", 100));
        Assert.That(result.ErrorCode, Is.EqualTo("ALREADY_LISTED"));
    }

    [Test]
    public async Task ListItem_BeforeAcquisitionHoldElapsed_IsRejected()
    {
        var store = new FakeStore();
        var instance = EligibleInstance("inst-1", ownerId: "actor");
        instance.LastAcquiredUtcMs = Now; // minted just now
        store.Seed(instance);

        var result = await Create(store).ListItemAsync(Context(), null!, ListRequest("inst-1", 100));
        Assert.That(result.ErrorCode, Is.EqualTo("HOLD_NOT_ELAPSED"));
    }

    [Test]
    public async Task ListItem_AfterAcquisitionHoldElapsed_Succeeds_AndReturnsTheGoldFee()
    {
        var store = new FakeStore();
        store.Seed(EligibleInstance("inst-1", ownerId: "actor")); // seeded well before Now
        var result = await Create(store).ListItemAsync(Context(), null!, ListRequest("inst-1", 1000));

        Assert.That(result.Success, Is.True);
        Assert.That(result.ListingId, Is.Not.Null.And.Not.Empty);
        Assert.That(result.GoldFeeDue, Is.EqualTo(50), "2% of 1000 = 20, which is below the 50 minimum, so the 50 floor applies.");
    }

    [Test]
    public async Task ListItem_GoldFee_UsesTheHigherOfPercentOrMinimum()
    {
        var store = new FakeStore();
        store.Seed(EligibleInstance("small-ask", ownerId: "actor"));
        var small = await Create(store).ListItemAsync(Context(), null!, ListRequest("small-ask", 100)); // 2% = 2, floor 50
        Assert.That(small.GoldFeeDue, Is.EqualTo(50));

        store.Seed(EligibleInstance("big-ask", ownerId: "actor"));
        var big = await Create(store).ListItemAsync(Context(), null!, ListRequest("big-ask", 10_000)); // 2% = 200 > 50
        Assert.That(big.GoldFeeDue, Is.EqualTo(200));
    }

    [Test]
    public async Task ListItem_RelistHold_UsesSeventyTwoHours_NotSevenDays_AfterAPurchase()
    {
        var store = new FakeStore();
        var instance = EligibleInstance("inst-1", ownerId: "actor");
        instance.LastAcquisitionWasPurchase = true;
        instance.LastAcquiredUtcMs = Now - (long)TimeSpan.FromHours(73).TotalMilliseconds; // past 72h, well within 7 days
        store.Seed(instance);

        var result = await Create(store).ListItemAsync(Context(), null!, ListRequest("inst-1", 100));
        Assert.That(result.Success, Is.True, "72h relist hold should have elapsed even though the 7-day mint hold has not.");
    }

    [Test]
    public async Task ListItem_MovesInstanceToListedState()
    {
        var store = new FakeStore();
        store.Seed(EligibleInstance("inst-1", ownerId: "actor"));
        await Create(store).ListItemAsync(Context(), null!, ListRequest("inst-1", 100));

        var instance = await store.LoadInstanceAsync(null!, null!, "inst-1");
        Assert.That(instance!.State, Is.EqualTo(ItemInstanceState.Listed));
    }

    // ---------------- Buying ----------------

    [Test]
    public async Task BuyItem_MissingActorOrBlankFieldsIsRejected()
    {
        var operations = Create();
        var noActor = await operations.BuyItemAsync(new FakeExecutionContext(null), null!, BuyRequest("listing-1", "key-1"));
        var blankListing = await operations.BuyItemAsync(Context("buyer"), null!, BuyRequest("", "key-1"));
        var blankKey = await operations.BuyItemAsync(Context("buyer"), null!, BuyRequest("listing-1", ""));
        Assert.That(noActor.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
        Assert.That(blankListing.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
        Assert.That(blankKey.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
    }

    [Test]
    public async Task BuyItem_UnknownOrInactiveListingIsRejected()
    {
        var store = new FakeStore();
        var operations = Create(store);
        var missing = await operations.BuyItemAsync(Context("buyer"), null!, BuyRequest("does-not-exist", "key-1"));
        Assert.That(missing.ErrorCode, Is.EqualTo("LISTING_NOT_AVAILABLE"));

        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 100);
        store.Listings["listing-1"].State = BazaarListingState.Cancelled;
        var cancelled = await operations.BuyItemAsync(Context("buyer"), null!, BuyRequest("listing-1", "key-1"));
        Assert.That(cancelled.ErrorCode, Is.EqualTo("LISTING_NOT_AVAILABLE"));
    }

    [Test]
    public async Task BuyItem_SelfTradeIsRejected()
    {
        var store = new FakeStore();
        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 100);
        var result = await Create(store).BuyItemAsync(Context("seller"), null!, BuyRequest("listing-1", "key-1"));
        Assert.That(result.ErrorCode, Is.EqualTo("SELF_TRADE_NOT_ALLOWED"));
    }

    [Test]
    public async Task BuyItem_InsufficientCreditsIsRejected()
    {
        var store = new FakeStore();
        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 500);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 100 };

        var result = await Create(store).BuyItemAsync(Context("buyer"), null!, BuyRequest("listing-1", "key-1"));
        Assert.That(result.ErrorCode, Is.EqualTo("INSUFFICIENT_CREDITS"));
    }

    [Test]
    public async Task BuyItem_TaxMath_SplitsTwelvePercentIntoSixAndSix_SellerReceivesTheRest()
    {
        var store = new FakeStore();
        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 1000);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 1000 };

        var result = await Create(store).BuyItemAsync(Context("buyer"), null!, BuyRequest("listing-1", "key-1"));

        Assert.That(result.Success, Is.True);
        Assert.That(result.PricePaidCredits, Is.EqualTo(1000));
        Assert.That(result.TaxBurnedCredits, Is.EqualTo(60));
        Assert.That(result.TaxTreasuryCredits, Is.EqualTo(60));
        Assert.That(result.SellerReceivedCredits, Is.EqualTo(880));
        Assert.That(result.TaxBurnedCredits + result.TaxTreasuryCredits + result.SellerReceivedCredits, Is.EqualTo(result.PricePaidCredits),
            "Money conservation: burned + treasury + seller must equal exactly what the buyer paid.");
    }

    [Test]
    public async Task BuyItem_DebitsBuyerAndCreditsSeller_AndTransfersOwnership()
    {
        var store = new FakeStore();
        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 200);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 300 };
        store.Wallets["seller"] = new WalletState { AccountId = "seller", BalanceCredits = 0 };

        await Create(store).BuyItemAsync(Context("buyer"), null!, BuyRequest("listing-1", "key-1"));

        Assert.That((await store.LoadWalletAsync(null!, null!, "buyer")).BalanceCredits, Is.EqualTo(100));
        Assert.That((await store.LoadWalletAsync(null!, null!, "seller")).BalanceCredits, Is.EqualTo(176)); // 200 - 12% = 176

        var instance = await store.LoadInstanceAsync(null!, null!, store.Listings["listing-1"].InstanceId);
        Assert.That(instance!.OwnerId, Is.EqualTo("buyer"));
        Assert.That(instance.State, Is.EqualTo(ItemInstanceState.Owned));
        Assert.That(instance.LastAcquisitionWasPurchase, Is.True, "So the buyer's own next listing attempt uses the 72h relist hold, not the 7-day mint hold.");
    }

    [Test]
    public async Task BuyItem_ClosesTheListing()
    {
        var store = new FakeStore();
        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 100);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 100 };

        await Create(store).BuyItemAsync(Context("buyer"), null!, BuyRequest("listing-1", "key-1"));

        var listing = await store.LoadListingAsync(null!, null!, "listing-1");
        Assert.That(listing!.State, Is.EqualTo(BazaarListingState.Sold));
    }

    [Test]
    public async Task BuyItem_SameIdempotencyKeyTwice_ReturnsTheOriginalResult_DoesNotDebitAgain()
    {
        var store = new FakeStore();
        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 100);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 500 };
        var operations = Create(store);

        var first = await operations.BuyItemAsync(Context("buyer"), null!, BuyRequest("listing-1", "retry-key"));
        var second = await operations.BuyItemAsync(Context("buyer"), null!, BuyRequest("listing-1", "retry-key"));

        Assert.That(second.PricePaidCredits, Is.EqualTo(first.PricePaidCredits));
        Assert.That(second.ListingId, Is.EqualTo(first.ListingId));
        Assert.That((await store.LoadWalletAsync(null!, null!, "buyer")).BalanceCredits, Is.EqualTo(400), "Only the first call may debit - the retry must not charge again.");
    }

    [Test]
    public async Task BuyItem_DifferentIdempotencyKey_IsATrulyNewAttempt_AndFailsBecauseListingAlreadySold()
    {
        var store = new FakeStore();
        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 100);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 500 };
        var operations = Create(store);

        await operations.BuyItemAsync(Context("buyer"), null!, BuyRequest("listing-1", "key-a"));
        var differentKey = await operations.BuyItemAsync(Context("buyer"), null!, BuyRequest("listing-1", "key-b"));

        Assert.That(differentKey.ErrorCode, Is.EqualTo("LISTING_NOT_AVAILABLE"));
    }

    [Test]
    public async Task BuyItem_SellerAtRollingSalesLimit_IsRejected()
    {
        var store = new FakeStore();
        store.Wallets["seller"] = new WalletState
        {
            AccountId = "seller",
            RecentSaleUtcMs = Enumerable.Repeat(Now - 1000, 5).ToList(), // 5 sales, all inside the window
        };
        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 100);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 500 };

        var result = await Create(store).BuyItemAsync(Context("buyer"), null!, BuyRequest("listing-1", "key-1"));
        Assert.That(result.ErrorCode, Is.EqualTo("SELLER_SALE_LIMIT_REACHED"));
    }

    [Test]
    public async Task BuyItem_SellerSaleOutsideTheRollingWindow_DoesNotCountTowardTheLimit()
    {
        var store = new FakeStore();
        long eightDaysAgo = Now - (long)TimeSpan.FromDays(8).TotalMilliseconds;
        store.Wallets["seller"] = new WalletState
        {
            AccountId = "seller",
            RecentSaleUtcMs = Enumerable.Repeat(eightDaysAgo, 5).ToList(), // 5 sales, but all outside the 7-day window
        };
        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 100);
        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 500 };

        var result = await Create(store).BuyItemAsync(Context("buyer"), null!, BuyRequest("listing-1", "key-1"));
        Assert.That(result.Success, Is.True);
    }

    // ---------------- Cancelling ----------------

    [Test]
    public async Task CancelListing_NotSellerIsRejected()
    {
        var store = new FakeStore();
        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 100);
        var result = await Create(store).CancelListingAsync(Context("someone-else"), null!, new CancelListingRequest { ListingId = "listing-1" });
        Assert.That(result.ErrorCode, Is.EqualTo("NOT_SELLER"));
    }

    [Test]
    public async Task CancelListing_ReturnsTheInstanceToOwnedState()
    {
        var store = new FakeStore();
        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 100);

        var result = await Create(store).CancelListingAsync(Context("seller"), null!, new CancelListingRequest { ListingId = "listing-1" });

        Assert.That(result.Success, Is.True);
        var listing = await store.LoadListingAsync(null!, null!, "listing-1");
        Assert.That(listing!.State, Is.EqualTo(BazaarListingState.Cancelled));
        var instance = await store.LoadInstanceAsync(null!, null!, listing.InstanceId);
        Assert.That(instance!.State, Is.EqualTo(ItemInstanceState.Owned));
    }

    [Test]
    public async Task CancelListing_AlreadySoldOrCancelledIsRejected()
    {
        var store = new FakeStore();
        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 100);
        store.Listings["listing-1"].State = BazaarListingState.Sold;

        var result = await Create(store).CancelListingAsync(Context("seller"), null!, new CancelListingRequest { ListingId = "listing-1" });
        Assert.That(result.ErrorCode, Is.EqualTo("LISTING_NOT_AVAILABLE"));
    }

    // ---------------- QueryListings ----------------

    [Test]
    public async Task QueryListings_MissingActorIsRejected()
    {
        var result = await Create().QueryListingsAsync(new FakeExecutionContext(null), null!, new QueryListingsRequest());
        Assert.That(result.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));
    }

    [TestCase(0)]
    [TestCase(101)]
    public async Task QueryListings_OutOfRangePageSizeIsRejected(int pageSize)
    {
        var result = await Create().QueryListingsAsync(Context(), null!, new QueryListingsRequest { PageSize = pageSize });
        Assert.That(result.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
    }

    [Test]
    public async Task QueryListings_MalformedPageTokenIsRejected()
    {
        var result = await Create().QueryListingsAsync(Context(), null!, new QueryListingsRequest { PageToken = "not-a-number" });
        Assert.That(result.ErrorCode, Is.EqualTo("INVALID_REQUEST"));
    }

    [Test]
    public async Task QueryListings_EmptyBoard_ReturnsSuccessWithNoListingsAndNoNextPage()
    {
        var result = await Create().QueryListingsAsync(Context(), null!, new QueryListingsRequest());
        Assert.That(result.Success, Is.True);
        Assert.That(result.Listings, Is.Empty);
        Assert.That(result.NextPageToken, Is.Null);
    }

    [Test]
    public async Task QueryListings_ReturnsOnlyActiveListings_NewestFirst()
    {
        var store = new FakeStore();
        SeedActiveListing(store, listingId: "older", sellerId: "seller", ask: 100);
        store.Listings["older"].CreatedUtcMs = Now - 1000;
        SeedActiveListing(store, listingId: "newer", sellerId: "seller", ask: 200);
        store.Listings["newer"].CreatedUtcMs = Now;
        SeedActiveListing(store, listingId: "sold-one", sellerId: "seller", ask: 300);
        store.Listings["sold-one"].State = BazaarListingState.Sold;

        var result = await Create(store).QueryListingsAsync(Context(), null!, new QueryListingsRequest { PageSize = 20 });

        Assert.That(result.Success, Is.True);
        Assert.That(result.Listings.Select(l => l.ListingId).ToList(), Is.EqualTo(new[] { "newer", "older" }),
            "Sold listings must be filtered out and the rest ordered newest-first.");
    }

    [Test]
    public async Task QueryListings_PagesUsingTheReturnedNextPageToken()
    {
        var store = new FakeStore();
        for (int i = 0; i < 5; i++)
        {
            SeedActiveListing(store, listingId: $"listing-{i}", sellerId: "seller", ask: 100);
            store.Listings[$"listing-{i}"].CreatedUtcMs = Now + i;
        }

        var operations = Create(store);
        var firstPage = await operations.QueryListingsAsync(Context(), null!, new QueryListingsRequest { PageSize = 2 });
        Assert.That(firstPage.Listings.Count, Is.EqualTo(2));
        Assert.That(firstPage.NextPageToken, Is.Not.Null);

        var secondPage = await operations.QueryListingsAsync(Context(), null!, new QueryListingsRequest { PageSize = 2, PageToken = firstPage.NextPageToken });
        Assert.That(secondPage.Listings.Count, Is.EqualTo(2));
        Assert.That(secondPage.NextPageToken, Is.Not.Null);

        var thirdPage = await operations.QueryListingsAsync(Context(), null!, new QueryListingsRequest { PageSize = 2, PageToken = secondPage.NextPageToken });
        Assert.That(thirdPage.Listings.Count, Is.EqualTo(1));
        Assert.That(thirdPage.NextPageToken, Is.Null, "Exhausted the index - no further page.");

        var seenIds = firstPage.Listings.Concat(secondPage.Listings).Concat(thirdPage.Listings).Select(l => l.ListingId).ToList();
        Assert.That(seenIds.Distinct().Count(), Is.EqualTo(5), "Paging through every page must cover each listing exactly once.");
    }

    [Test]
    public async Task ListItem_AddsToTheIndex_BuyItem_RemovesFromIt()
    {
        var store = new FakeStore();
        store.Seed(EligibleInstance("inst-1", ownerId: "actor"));
        var operations = Create(store);

        var listResult = await operations.ListItemAsync(Context("actor"), null!, ListRequest("inst-1", 100));
        Assert.That(store.Index.ActiveListingIds, Does.Contain(listResult.ListingId));

        store.Wallets["buyer"] = new WalletState { AccountId = "buyer", BalanceCredits = 1000 };
        await operations.BuyItemAsync(Context("buyer"), null!, BuyRequest(listResult.ListingId!, "key-1"));
        Assert.That(store.Index.ActiveListingIds, Does.Not.Contain(listResult.ListingId), "A sold listing must not still surface in QueryListings.");
    }

    [Test]
    public async Task CancelListing_RemovesFromTheIndex()
    {
        var store = new FakeStore();
        SeedActiveListing(store, listingId: "listing-1", sellerId: "seller", ask: 100);
        Assert.That(store.Index.ActiveListingIds, Does.Contain("listing-1"));

        await Create(store).CancelListingAsync(Context("seller"), null!, new CancelListingRequest { ListingId = "listing-1" });
        Assert.That(store.Index.ActiveListingIds, Does.Not.Contain("listing-1"));
    }

    // ---------------- Wallet / contract / config ----------------

    [Test]
    public async Task GetWallet_MissingActorIsRejected_OtherwiseReturnsBalance()
    {
        var operations = Create();
        var noActor = await operations.GetWalletAsync(new FakeExecutionContext(null), null!);
        Assert.That(noActor.ErrorCode, Is.EqualTo("AUTHENTICATION_REQUIRED"));

        var store = new FakeStore();
        store.Wallets["actor"] = new WalletState { AccountId = "actor", BalanceCredits = 42 };
        var result = await Create(store).GetWalletAsync(Context(), null!);
        Assert.That(result.BalanceCredits, Is.EqualTo(42));
    }

    [Test]
    public void NoRequestTypeCanSupplyOwnershipEditionClockOrPriceAuthority()
    {
        // §4 non-goal: "client-set ownership/edition/clock/price authority" - none of these
        // request types can carry an owner, a timestamp, or (for Buy/Cancel) a price at all; the
        // only place a price is ever specified is the seller's own ask at listing time.
        bool HasForbiddenField(Type type) => type.GetProperties()
            .Any(p => p.Name.IndexOf("Owner", StringComparison.OrdinalIgnoreCase) >= 0
                || p.Name.IndexOf("Time", StringComparison.OrdinalIgnoreCase) >= 0
                || p.Name.IndexOf("Edition", StringComparison.OrdinalIgnoreCase) >= 0);

        Assert.That(HasForbiddenField(typeof(ListItemRequest)), Is.False);
        Assert.That(HasForbiddenField(typeof(BuyItemRequest)), Is.False);
        Assert.That(HasForbiddenField(typeof(CancelListingRequest)), Is.False);
        Assert.That(typeof(BuyItemRequest).GetProperty("AskCredits"), Is.Null, "Buy must never take a client-supplied price - only the listing's own ask is charged.");
    }

    [Test]
    public void ResponseSerializationUsesTheDocumentedCamelCaseContract()
    {
        var listing = new ListingResult { Success = true, ListingId = "l1", GoldFeeDue = 50 };
        Assert.That(JsonConvert.SerializeObject(listing), Does.Contain("\"goldFeeDue\":50"));

        var buy = new BuyResult { Success = true, PricePaidCredits = 100, SellerReceivedCredits = 88, TaxBurnedCredits = 6, TaxTreasuryCredits = 6 };
        string buyJson = JsonConvert.SerializeObject(buy);
        Assert.That(buyJson, Does.Contain("\"pricePaidCredits\":100"));
        Assert.That(buyJson, Does.Contain("\"sellerReceivedCredits\":88"));
    }

    [TestCase(2, 2, 100)]
    [TestCase(0, 2, 100)]
    [TestCase(101, 2, 100)]
    [TestCase("malformed", 2, 100)]
    public void ListingFeePercentConfigurationRespectsCeiling(object configured, int fallback, int maximum)
    {
        int result = ReadConfiguredInt(
            new Dictionary<string, object> { { RemoteConfigBazaarRulesConfiguration.ListingFeePercentKey, configured } },
            RemoteConfigBazaarRulesConfiguration.ListingFeePercentKey,
            fallback,
            maximum);
        Assert.That(result, Is.EqualTo(configured is int intValue && intValue == maximum ? maximum : fallback));
    }

    private static int ReadConfiguredInt(Dictionary<string, object> settings, string key, int fallback, int maximum)
    {
        var method = typeof(RemoteConfigBazaarRulesConfiguration).GetMethod("ReadPositiveIntBounded", BindingFlags.Static | BindingFlags.NonPublic);
        return (int)method!.Invoke(null, new object[] { settings, key, fallback, maximum })!;
    }

    private static BazaarOperations Create(FakeStore? store = null)
    {
        return new BazaarOperations(store ?? new FakeStore(), new FakeClock(Now), new FixedRules());
    }

    private static FakeExecutionContext Context(string playerId = "actor") => new(playerId);

    private static ItemInstance EligibleInstance(string instanceId, string ownerId) => new()
    {
        InstanceId = instanceId,
        DefinitionId = "card.example",
        OwnerId = ownerId,
        Rarity = 2,
        Tradeable = true,
        State = ItemInstanceState.Owned,
        LastAcquiredUtcMs = Now - (long)TimeSpan.FromDays(30).TotalMilliseconds,
        LastAcquisitionWasPurchase = false,
    };

    private static void SeedActiveListing(FakeStore store, string listingId, string sellerId, int ask)
    {
        string instanceId = listingId + "-instance";
        var instance = EligibleInstance(instanceId, sellerId);
        instance.State = ItemInstanceState.Listed;
        store.Seed(instance);
        store.Listings[listingId] = new BazaarListing
        {
            ListingId = listingId,
            InstanceId = instanceId,
            SellerId = sellerId,
            AskCredits = ask,
            State = BazaarListingState.Active,
            CreatedUtcMs = Now,
        };
        store.Index.ActiveListingIds.Add(listingId);
    }

    private static ListItemRequest ListRequest(string instanceId, int ask) => new() { InstanceId = instanceId, AskCredits = ask };
    private static BuyItemRequest BuyRequest(string listingId, string idempotencyKey) => new() { ListingId = listingId, IdempotencyKey = idempotencyKey };

    /// <summary>In-memory reference implementation of <see cref="IBazaarStore"/> - see that
    /// interface's own doc comment for why this scaffold has no concrete Cloud Save-backed
    /// implementation.</summary>
    private sealed class FakeStore : IBazaarStore
    {
        public Dictionary<string, ItemInstance> Instances { get; } = new();
        public Dictionary<string, BazaarListing> Listings { get; } = new();
        public Dictionary<string, WalletState> Wallets { get; } = new();
        public BazaarListingIndex Index { get; } = new();
        private readonly Dictionary<(string, string), BuyResult> _idempotency = new();

        public void Seed(ItemInstance instance) => Instances[instance.InstanceId] = instance;

        public Task<ItemInstance?> LoadInstanceAsync(IExecutionContext context, IGameApiClient apiClient, string instanceId)
            => Task.FromResult(Instances.TryGetValue(instanceId, out var instance) ? Clone(instance) : null);

        public Task SaveInstanceAsync(IExecutionContext context, IGameApiClient apiClient, ItemInstance instance)
        {
            Instances[instance.InstanceId] = Clone(instance);
            return Task.CompletedTask;
        }

        public Task<BazaarListing?> LoadListingAsync(IExecutionContext context, IGameApiClient apiClient, string listingId)
            => Task.FromResult(Listings.TryGetValue(listingId, out var listing) ? Clone(listing) : null);

        public Task SaveListingAsync(IExecutionContext context, IGameApiClient apiClient, BazaarListing listing)
        {
            Listings[listing.ListingId] = Clone(listing);
            return Task.CompletedTask;
        }

        public Task<WalletState> LoadWalletAsync(IExecutionContext context, IGameApiClient apiClient, string accountId)
        {
            if (!Wallets.TryGetValue(accountId, out var wallet))
            {
                wallet = new WalletState { AccountId = accountId };
            }

            return Task.FromResult(Clone(wallet));
        }

        public Task SaveWalletAsync(IExecutionContext context, IGameApiClient apiClient, WalletState wallet)
        {
            Wallets[wallet.AccountId] = Clone(wallet);
            return Task.CompletedTask;
        }

        public Task<BuyResult?> TryGetIdempotentBuyResultAsync(IExecutionContext context, IGameApiClient apiClient, string buyerId, string idempotencyKey)
            => Task.FromResult(_idempotency.TryGetValue((buyerId, idempotencyKey), out var result) ? result : null);

        public Task SaveIdempotentBuyResultAsync(IExecutionContext context, IGameApiClient apiClient, string buyerId, string idempotencyKey, BuyResult result)
        {
            _idempotency[(buyerId, idempotencyKey)] = result;
            return Task.CompletedTask;
        }

        public Task<BazaarListingIndex> LoadIndexAsync(IExecutionContext context, IGameApiClient apiClient)
            => Task.FromResult(new BazaarListingIndex { ActiveListingIds = new List<string>(Index.ActiveListingIds) });

        public Task SaveIndexAsync(IExecutionContext context, IGameApiClient apiClient, BazaarListingIndex index)
        {
            Index.ActiveListingIds.Clear();
            Index.ActiveListingIds.AddRange(index.ActiveListingIds);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BazaarListing>> LoadListingsBatchAsync(IExecutionContext context, IGameApiClient apiClient, IReadOnlyList<string> listingIds)
        {
            var results = new List<BazaarListing>();
            foreach (string listingId in listingIds)
            {
                if (Listings.TryGetValue(listingId, out var listing))
                {
                    results.Add(Clone(listing));
                }
            }

            return Task.FromResult<IReadOnlyList<BazaarListing>>(results);
        }

        private static ItemInstance Clone(ItemInstance instance) =>
            JsonConvert.DeserializeObject<ItemInstance>(JsonConvert.SerializeObject(instance))!;
        private static BazaarListing Clone(BazaarListing listing) =>
            JsonConvert.DeserializeObject<BazaarListing>(JsonConvert.SerializeObject(listing))!;
        private static WalletState Clone(WalletState wallet) =>
            JsonConvert.DeserializeObject<WalletState>(JsonConvert.SerializeObject(wallet))!;
    }

    private sealed class FakeClock : IBazaarClock
    {
        public FakeClock(long now) => UtcNowMs = now;
        public long UtcNowMs { get; }
    }

    private sealed class FixedRules : IBazaarRulesConfiguration
    {
        public Task<BazaarRulesConfiguration> LoadAsync(IExecutionContext context, IGameApiClient apiClient)
            => Task.FromResult(BazaarRulesConfiguration.Fallback());
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
