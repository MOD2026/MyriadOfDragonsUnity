using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using Fault = MyriadOfDragons.CloudCode.Bazaar.Tests.BazaarSettlementContractTests.FaultStore;
using Snapshot = MyriadOfDragons.CloudCode.Bazaar.Tests.BazaarSettlementContractTests.StoreSnapshot;

namespace MyriadOfDragons.CloudCode.Bazaar.Tests;

/// <summary>
/// Tests for <see cref="BazaarReconciliationSweep"/>: recovery of SettlementJournal records stuck
/// InProgress, stale active-index detection/repair, idempotent replay, and "no double charge".
/// In-memory fault-injectable store only - no Cloud Save, no deployment, no real account or
/// credential. Stuck states are produced through the REAL saga path (fault injection during
/// BuyItemAsync) rather than hand-built, except for the deliberately corrupt/legacy cases.
/// The existing reconciliation definition (BazaarSettlementContractTests.Reconcile) is reused to
/// prove recovered sales are consistent.
/// </summary>
public sealed class BazaarReconciliationSweepTests
{
    private const long Now = BazaarSettlementContractTests.Now;

    // ---------------- helpers ----------------

    private static Fault NewStore(string listingId = "listing-1", string seller = "seller", string buyer = "buyer", int ask = 100, int buyerBalance = 500)
    {
        var store = new Fault();
        store.SeedActiveListing(listingId, seller, ask);
        store.Wallets[buyer] = new WalletState { AccountId = buyer, BalanceCredits = buyerBalance };
        store.Wallets[seller] = new WalletState { AccountId = seller, BalanceCredits = 0 };
        return store;
    }

    private static BazaarReconciliationSweep NewSweep(Fault store) =>
        new(store, BazaarSettlementContractTests.Create(store), new BazaarSettlementContractTests.FixedClock(Now));

    private static Task<ReconciliationSweepResult> Sweep(Fault store, long minAgeMs = 0) =>
        NewSweep(store).SweepAsync(BazaarSettlementContractTests.Context("sweeper"), null!, minAgeMs);

    private static Task<BuyResult> Buy(Fault store, string buyer, string listingId, string key) =>
        BazaarSettlementContractTests.Create(store).BuyItemAsync(BazaarSettlementContractTests.Context(buyer), null!, BazaarSettlementContractTests.Buy(listingId, key));

    /// <summary>Drives the real saga until write number <paramref name="failAtSave"/> fails, leaving a
    /// genuinely stuck InProgress settlement, then disarms the fault.</summary>
    private static async Task StickAsync(Fault store, string buyer, string listingId, string key, int failAtSave, bool afterWrite = false)
    {
        store.SaveCount = 0;
        store.FailOnSaveNumber = failAtSave;
        store.ThrowAfterWrite = afterWrite;
        var failed = await Buy(store, buyer, listingId, key);
        Assert.That(failed.Success, Is.False, "setup: the fault must actually interrupt the settlement");
        store.FailOnSaveNumber = 0;
        store.ThrowAfterWrite = false;
    }

    // ---------------- recovery ----------------

    [TestCase(2, false)]
    [TestCase(3, false)]
    [TestCase(4, false)]
    [TestCase(2, true)]
    [TestCase(3, true)]
    [TestCase(4, true)]
    public async Task Sweep_RecoversAStuckSettlement_ChargesExactlyOnce_AndReconcilesClean(int failAtSave, bool afterWrite)
    {
        var store = NewStore();
        var before = store.Snapshot();
        await StickAsync(store, "buyer", "listing-1", "key-1", failAtSave, afterWrite);

        var result = await Sweep(store);

        Assert.That(result.Findings.Select(f => f.Kind), Is.EqualTo(new[] { ReconciliationFindingKind.RecoveredSettlement }));
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(400), "Charged exactly once.");
        Assert.That(store.Wallets["seller"].BalanceCredits, Is.EqualTo(88), "Credited exactly once.");
        Assert.That(store.Index.ActiveListingIds, Is.Empty);

        // The buyer's own late retry replays the recovered outcome; it must not charge again.
        var replay = await Buy(store, "buyer", "listing-1", "key-1");
        Assert.That(replay.Success, Is.True, replay.ErrorCode);
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(400));
        var report = BazaarSettlementContractTests.Reconcile(before, store.Snapshot(), replay, "listing-1", "buyer", "seller");
        Assert.That(report, Is.Empty, string.Join("; ", report));
    }

    [Test]
    public async Task Sweep_IsIdempotent_ASecondSweepOverAHealedBoardIsClean_AndChangesNothing()
    {
        var store = NewStore();
        await StickAsync(store, "buyer", "listing-1", "key-1", failAtSave: 3);
        await Sweep(store);
        var healed = store.Snapshot();
        int savesAfterFirst = store.SaveCount;

        var second = await Sweep(store);
        var third = await Sweep(store);

        Assert.That(second.IsClean, Is.True);
        Assert.That(third.IsClean, Is.True);
        Assert.That(store.Snapshot(), Is.EqualTo(healed));
        Assert.That(store.SaveCount, Is.EqualTo(savesAfterFirst), "A clean replay must perform no writes.");
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(400));
    }

    [Test]
    public async Task Sweep_AfterTheBuyerAlreadyRetried_HasNothingToDo_NoDoubleCharge()
    {
        var store = NewStore();
        await StickAsync(store, "buyer", "listing-1", "key-1", failAtSave: 3);
        var retry = await Buy(store, "buyer", "listing-1", "key-1");
        Assert.That(retry.Success, Is.True, retry.ErrorCode);

        var result = await Sweep(store);

        Assert.That(result.IsClean, Is.True);
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(400));
        Assert.That(store.Wallets["seller"].BalanceCredits, Is.EqualTo(88));
    }

    [Test]
    public async Task Sweep_StorageFailureDuringRecovery_IsDeferred_ThenTheNextSweepCompletes_WithoutDoubleCharge()
    {
        var store = NewStore();
        await StickAsync(store, "buyer", "listing-1", "key-1", failAtSave: 2);

        store.SaveCount = 0;
        store.FailOnSaveNumber = 1; // the first write of the resume (buyer debit) fails
        var deferred = await Sweep(store);

        Assert.That(deferred.Findings.Select(f => f.Kind), Is.EqualTo(new[] { ReconciliationFindingKind.DeferredFailure }));
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(500), "A failed recovery must not have charged.");

        store.FailOnSaveNumber = 0;
        var recovered = await Sweep(store);

        Assert.That(recovered.Findings.Select(f => f.Kind), Is.EqualTo(new[] { ReconciliationFindingKind.RecoveredSettlement }));
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(400), "Deferred-then-recovered must still be exactly one charge.");
        Assert.That(store.Wallets["seller"].BalanceCredits, Is.EqualTo(88));
    }

    [Test]
    public async Task Sweep_BuyerNoLongerHasTheCredits_AbortsDefinitively_ReleasesTheListing_ChargesNothing()
    {
        var store = NewStore();
        await StickAsync(store, "buyer", "listing-1", "key-1", failAtSave: 2);
        store.Wallets["buyer"].BalanceCredits = 0;

        var result = await Sweep(store);

        Assert.That(result.Findings.Select(f => f.Kind), Is.EqualTo(new[] { ReconciliationFindingKind.AbortedSettlement }));
        Assert.That(store.Listings["listing-1"].State, Is.EqualTo(BazaarListingState.Active));
        Assert.That(store.Listings["listing-1"].SettlementId, Is.Null);
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(0));
        Assert.That(store.Wallets["seller"].BalanceCredits, Is.EqualTo(0));
        Assert.That((await Sweep(store)).IsClean, Is.True);
    }

    [Test]
    public async Task Sweep_AnAbortedJournalThatStillHoldsItsClaim_ReleasesTheListing()
    {
        var store = NewStore();
        await StickAsync(store, "buyer", "listing-1", "key-1", failAtSave: 2);
        var journal = (await store.TryGetSettlementJournalAsync(null!, null!, "buyer", "key-1"))!;
        journal.Phase = SettlementJournal.PhaseAborted;
        journal.FailureCode = "INSUFFICIENT_CREDITS";
        await store.SaveSettlementJournalAsync(null!, null!, "buyer", "key-1", journal);

        var result = await Sweep(store);

        Assert.That(result.Findings.Select(f => f.Kind), Is.EqualTo(new[] { ReconciliationFindingKind.ReleasedAbortedClaim }));
        Assert.That(store.Listings["listing-1"].State, Is.EqualTo(BazaarListingState.Active));
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(500));
        Assert.That((await Sweep(store)).IsClean, Is.True);

        store.Wallets["other"] = new WalletState { AccountId = "other", BalanceCredits = 500 };
        var resold = await Buy(store, "other", "listing-1", "key-other");
        Assert.That(resold.Success, Is.True, resold.ErrorCode);
    }

    [Test]
    public async Task Sweep_HonorsACallerSuppliedMinimumJournalAge_AndDoesNotInventOne()
    {
        var store = NewStore();
        await StickAsync(store, "buyer", "listing-1", "key-1", failAtSave: 2);

        var tooRecent = await Sweep(store, minAgeMs: 60_000);
        Assert.That(tooRecent.Findings.Select(f => f.Kind), Is.EqualTo(new[] { ReconciliationFindingKind.SkippedTooRecent }));
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(500), "A skipped journal must not be touched.");

        var recovered = await Sweep(store); // default 0 = no age gate
        Assert.That(recovered.Findings.Select(f => f.Kind), Is.EqualTo(new[] { ReconciliationFindingKind.RecoveredSettlement }));
    }

    [Test]
    public async Task Sweep_RecoversSeveralStuckSettlements_InIndexOrder_Deterministically()
    {
        var store = NewStore("listing-1", "seller", "buyerA");
        store.SeedActiveListing("listing-2", "seller", 200);
        store.SeedActiveListing("listing-3", "seller", 300);
        store.Wallets["buyerB"] = new WalletState { AccountId = "buyerB", BalanceCredits = 1_000 };
        await StickAsync(store, "buyerA", "listing-1", "key-a", failAtSave: 2);
        await StickAsync(store, "buyerB", "listing-3", "key-b", failAtSave: 3);

        var result = await Sweep(store);

        Assert.That(result.Scanned, Is.EqualTo(3));
        Assert.That(result.Findings.Select(f => f.ListingId), Is.EqualTo(new[] { "listing-1", "listing-3" }), "Findings follow index order; the healthy listing-2 is untouched.");
        Assert.That(result.Findings.All(f => f.Kind == ReconciliationFindingKind.RecoveredSettlement), Is.True);
        Assert.That(store.Wallets["buyerA"].BalanceCredits, Is.EqualTo(400));
        Assert.That(store.Wallets["buyerB"].BalanceCredits, Is.EqualTo(700));
        Assert.That(store.Listings["listing-2"].State, Is.EqualTo(BazaarListingState.Active));
    }

    [Test]
    public async Task Sweep_AStorageFailureOnOneListing_IsDeferred_AndTheRestOfTheBoardIsStillReconciled()
    {
        // Regression for a defect found by the live nonprod-validation run: reading ANOTHER buyer's
        // record failed and that one exception used to abort the entire sweep.
        var store = NewStore("listing-1", "seller", "buyerA");
        store.SeedActiveListing("listing-2", "seller", 200);
        store.SeedActiveListing("listing-3", "seller", 50);
        store.Listings["listing-3"].State = BazaarListingState.Cancelled;
        store.Wallets["buyerB"] = new WalletState { AccountId = "buyerB", BalanceCredits = 1_000 };
        await StickAsync(store, "buyerA", "listing-1", "key-a", failAtSave: 2);
        await StickAsync(store, "buyerB", "listing-2", "key-b", failAtSave: 2);
        store.FailIdempotencyRead = (buyer, _) => buyer == "buyerA";

        var partial = await Sweep(store);

        Assert.That(partial.Findings.Select(f => (f.ListingId, f.Kind)), Is.EqualTo(new[]
        {
            ("listing-1", ReconciliationFindingKind.DeferredFailure),
            ("listing-2", ReconciliationFindingKind.RecoveredSettlement),
            ("listing-3", ReconciliationFindingKind.RemovedStaleIndexEntry),
        }));
        Assert.That(store.Wallets["buyerA"].BalanceCredits, Is.EqualTo(500), "The deferred settlement must not have been touched.");
        Assert.That(store.Wallets["buyerB"].BalanceCredits, Is.EqualTo(800));

        store.FailIdempotencyRead = null;
        var healed = await Sweep(store);
        Assert.That(healed.Findings.Select(f => f.Kind), Is.EqualTo(new[] { ReconciliationFindingKind.RecoveredSettlement }));
        Assert.That(store.Wallets["buyerA"].BalanceCredits, Is.EqualTo(400), "Recovered exactly once after the deferral.");
        Assert.That((await Sweep(store)).IsClean, Is.True);
    }

    // ---------------- stale index detection ----------------

    [TestCase(false)]
    [TestCase(true)]
    public async Task Sweep_DetectsAndRemovesAStaleIndexEntry_LeftByBestEffortRemoval_WithoutTouchingMoney(bool removalActuallyLanded)
    {
        var store = NewStore();
        var before = store.Snapshot();
        store.SaveCount = 0;
        store.FailOnSaveNumber = 5; // the index removal, the saga's last and best-effort write
        store.ThrowAfterWrite = removalActuallyLanded;
        var sale = await Buy(store, "buyer", "listing-1", "key-1");
        Assert.That(sale.Success, Is.True, "Index removal is best-effort: the sale itself succeeds.");
        store.FailOnSaveNumber = 0;
        store.ThrowAfterWrite = false;
        var afterSale = store.Snapshot();

        var result = await Sweep(store);

        if (removalActuallyLanded)
        {
            Assert.That(result.IsClean, Is.True, "Nothing was stale if the removal write did land.");
        }
        else
        {
            Assert.That(result.Findings.Select(f => f.Kind), Is.EqualTo(new[] { ReconciliationFindingKind.RemovedStaleIndexEntry }));
            Assert.That(store.Index.ActiveListingIds, Is.Empty);
        }

        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(400));
        Assert.That(store.Wallets["seller"].BalanceCredits, Is.EqualTo(88));
        var report = BazaarSettlementContractTests.Reconcile(before, store.Snapshot(), sale, "listing-1", "buyer", "seller");
        Assert.That(report, Is.Empty, string.Join("; ", report));
        Assert.That((await Sweep(store)).IsClean, Is.True);
    }

    [Test]
    public async Task Sweep_RemovesIndexEntriesForCancelledMissingAndLegacySoldListings_AndLeavesHealthyOnesAlone()
    {
        var store = NewStore("healthy", "seller", "buyer");
        store.SeedActiveListing("cancelled", "seller", 50);
        store.Listings["cancelled"].State = BazaarListingState.Cancelled;
        store.SeedActiveListing("legacy-sold", "seller", 50);
        store.Listings["legacy-sold"].State = BazaarListingState.Sold; // sold before the saga: no SettlementId
        store.Index.ActiveListingIds.Add("ghost"); // index entry whose listing record is gone
        var walletsBefore = store.Snapshot().Balances;

        var result = await Sweep(store);

        Assert.That(result.Scanned, Is.EqualTo(4));
        Assert.That(result.Findings.Select(f => f.ListingId), Is.EqualTo(new[] { "cancelled", "legacy-sold", "ghost" }));
        Assert.That(result.Findings.All(f => f.Kind == ReconciliationFindingKind.RemovedStaleIndexEntry), Is.True);
        Assert.That(store.Index.ActiveListingIds, Is.EqualTo(new[] { "healthy" }));
        Assert.That(store.Snapshot().Balances, Is.EqualTo(walletsBefore), "Index repair must never move credits.");
        Assert.That((await Sweep(store)).IsClean, Is.True);
    }

    [Test]
    public async Task Sweep_OnAnEmptyOrAllHealthyBoard_IsCleanAndWritesNothing()
    {
        var empty = new Fault();
        Assert.That((await Sweep(empty)).IsClean, Is.True);

        var healthy = NewStore();
        var result = await Sweep(healthy);
        Assert.That(result.Scanned, Is.EqualTo(1));
        Assert.That(result.IsClean, Is.True);
        Assert.That(healthy.SaveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Sweep_ACompletedSaleWhoseIndexEntryIsStale_IsDetectedFromTheRecordedResult()
    {
        var store = NewStore();
        store.Listings["listing-1"].State = BazaarListingState.Sold;
        store.Listings["listing-1"].SettlementId = "buyer|key-1";
        store.IdempotencyRecords[("buyer", "key-1")] = new BuyResult { Success = true, ListingId = "listing-1", PricePaidCredits = 100 };

        var result = await Sweep(store);

        Assert.That(result.Findings.Select(f => f.Kind), Is.EqualTo(new[] { ReconciliationFindingKind.RemovedStaleIndexEntry }));
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(500), "Detection from the recorded result must not re-apply money.");
        Assert.That(store.Wallets["seller"].BalanceCredits, Is.EqualTo(0));
    }

    // ---------------- what the sweep must NOT guess ----------------

    [Test]
    public async Task Sweep_AClaimedListingWithNoJournalOrResult_IsFlaggedNotGuessed_AndStaysFlagged()
    {
        var store = NewStore();
        store.Listings["listing-1"].State = BazaarListingState.Sold;
        store.Listings["listing-1"].SettlementId = "ghost-buyer|ghost-key";
        var before = store.Snapshot();

        var first = await Sweep(store);
        var second = await Sweep(store);

        Assert.That(first.Findings.Select(f => f.Kind), Is.EqualTo(new[] { ReconciliationFindingKind.UnrecoverableMissingJournal }));
        Assert.That(second.Findings.Select(f => f.Kind), Is.EqualTo(new[] { ReconciliationFindingKind.UnrecoverableMissingJournal }), "Deterministic: it keeps reporting until a human resolves it.");
        Assert.That(store.Snapshot(), Is.EqualTo(before), "Nothing is mutated for an unrecoverable record.");
        Assert.That(store.SaveCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Sweep_AMalformedSettlementId_IsTreatedAsLegacy_NotAsARecoverableJournal()
    {
        var store = NewStore();
        store.Listings["listing-1"].State = BazaarListingState.Sold;
        store.Listings["listing-1"].SettlementId = "no-separator-here";

        var result = await Sweep(store);

        Assert.That(result.Findings.Select(f => f.Kind), Is.EqualTo(new[] { ReconciliationFindingKind.RemovedStaleIndexEntry }));
        Assert.That(store.Wallets["buyer"].BalanceCredits, Is.EqualTo(500));
    }
}
