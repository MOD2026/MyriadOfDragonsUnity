using System.IO;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class BazaarShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsBazaarShell_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        [Test]
        public void OpenValues_ShellPlaceholdersStillPresent()
        {
            Assert.AreEqual("[runtime]", BazaarOpenValues.RuntimePlaceholder);
            Assert.AreEqual(6, BazaarOpenValues.ShellListingWellCount);
        }

        [Test]
        public async Task Presenter_WalletTab_UsesInjectedGateway()
        {
            var go = new GameObject("BazaarHarness");
            _spawned.Add(go);
            var fake = new FakeBazaarGateway { WalletBalance = 42 };
            var presenter = go.AddComponent<BazaarPresenter>();
            presenter.Initialize(onBack: null, gateway: fake);

            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.NotNull(canvas);
            Assert.IsTrue(BazaarUiLibrary.HasBazaarV1Pack);
            Assert.AreEqual(BazaarUiLibrary.CatalogShellName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.NotNull(canvas.transform.Find("ListingGrid/ListingWell_5"));
            Assert.NotNull(canvas.transform.Find("TabStrip/Tab_Wallet"));
            StringAssert.Contains("Empty",
                canvas.transform.Find("ListingGrid/ListingWell_0/Placeholder")?.GetComponent<Text>()?.text);

            canvas.transform.Find("ListingGrid/ListingWell_0").GetComponent<Button>().onClick.Invoke();
            Assert.AreEqual(string.Empty, presenter.SelectedListingIdForTests);
            BazaarBuyResult buyEmpty = await presenter.BuySelectedForTests();
            Assert.AreEqual("INVALID_REQUEST", buyEmpty.errorCode);

            canvas.transform.Find("TabStrip/Tab_Wallet").GetComponent<Button>().onClick.Invoke();
            BazaarWalletResult wallet = await presenter.RefreshWalletForTests();
            Assert.AreEqual(42, wallet.balanceCredits);
            Assert.IsTrue(string.IsNullOrEmpty(wallet.errorCode));
            StringAssert.Contains("42", presenter.StatusTextForTests);
            // Tab select auto-refreshes once; RefreshWalletForTests is the explicit second call.
            Assert.GreaterOrEqual(fake.WalletCalls, 1);
        }

        [Test]
        public async Task Presenter_SellConfirm_CallsListItem_WithDeferredInstance()
        {
            var go = new GameObject("BazaarSellHarness");
            _spawned.Add(go);
            var fake = new FakeBazaarGateway
            {
                ListResult = new BazaarListingResult { success = false, errorCode = "INSTANCE_NOT_FOUND" }
            };
            var presenter = go.AddComponent<BazaarPresenter>();
            presenter.Initialize(onBack: null, gateway: fake);

            presenter.CanvasObjectForTests.transform.Find("TabStrip/Tab_Sell").GetComponent<Button>().onClick.Invoke();
            BazaarListingResult listed = await presenter.ListDeferredInstanceForTests(100);
            Assert.AreEqual("INSTANCE_NOT_FOUND", listed.errorCode);
            Assert.AreEqual(1, fake.ListCalls);
            Assert.AreEqual(BazaarPresenter.DeferredInstanceIdPlaceholder, fake.LastInstanceId);
        }

        [Test]
        public void Home_BazaarButton_OpensShell_AndBackReturnsHome()
        {
            // Real reachability path since the Home IA rebuild (register: "LOCKED: Home IA
            // rebuild") - Bazaar is no longer a direct Home button, it's a tab inside the
            // Collection destination hub (register: "Cards/Shop/Bazaar become ONE tabbed hub").
            var go = new GameObject("HomeBazaarReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            home.OpenCollectionHubForTests();
            GameObject hub = home.TabHubObjectForTests;
            Assert.NotNull(hub, "Setup: expected the Collection hub to open.");
            Button openBtn = hub.transform.Find("Dest_BAZAAR")?.GetComponent<Button>();
            Assert.NotNull(openBtn, "Setup: expected a BAZAAR tab inside the Collection hub.");
            openBtn.onClick.Invoke();
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(BazaarPresenter.CanvasName));

            Button back = GameObject.Find(BazaarPresenter.CanvasName).transform
                .Find("BazaarHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back);
            back.onClick.Invoke();
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(go.GetComponent<BazaarPresenter>());
        }

        private sealed class FakeBazaarGateway : IBazaarGateway
        {
            public int WalletBalance = 0;
            public int WalletCalls;
            public int ListCalls;
            public string LastInstanceId;
            public BazaarListingResult ListResult = new BazaarListingResult { success = true, listingId = "L1" };

            public Task<BazaarWalletResult> GetWalletAsync(CancellationToken cancellationToken)
            {
                WalletCalls++;
                return Task.FromResult(new BazaarWalletResult { balanceCredits = WalletBalance });
            }

            public Task<BazaarListingResult> ListItemAsync(string instanceId, int askCredits, CancellationToken cancellationToken)
            {
                ListCalls++;
                LastInstanceId = instanceId;
                return Task.FromResult(ListResult);
            }

            public Task<BazaarBuyResult> BuyItemAsync(string listingId, string idempotencyKey, CancellationToken cancellationToken) =>
                Task.FromResult(new BazaarBuyResult { success = false, errorCode = "LISTING_NOT_AVAILABLE" });

            public Task<BazaarCancelResult> CancelListingAsync(string listingId, CancellationToken cancellationToken) =>
                Task.FromResult(new BazaarCancelResult { success = false, errorCode = "LISTING_NOT_AVAILABLE" });

            public Task<BazaarListingsQueryResult> QueryListingsAsync(int pageSize, string pageToken, CancellationToken cancellationToken) =>
                Task.FromResult(new BazaarListingsQueryResult { success = false, errorCode = "NOT_IMPLEMENTED" });
        }
    }
}
