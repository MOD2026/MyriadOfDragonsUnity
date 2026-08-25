using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Metagame;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// BAZAAR V1 art shell wired to <see cref="IBazaarGateway"/>. Wallet reads, Browse
    /// (QueryListings) and List/Buy/Cancel are all real, live plumbing (Collection ItemInstance
    /// creation is still deferred — ListItem expects INSTANCE_NOT_FOUND until that lands).
    /// "My Listings" (tab 2) still has no seller-filtered query endpoint - QueryListings is a
    /// global browse, not scoped to the caller's own listings.
    /// </summary>
    public class BazaarPresenter : MonoBehaviour
    {
        public const string CanvasName = "BazaarCanvas";

        /// <summary>Placeholder instance id until Collection can mint real ItemInstances.</summary>
        public const string DeferredInstanceIdPlaceholder = "collection-pending-instance";

        private GameObject _canvasObj;
        private Action _onBack;
        private Text _statusText;
        private Text _detailsText;
        private IBazaarGateway _gateway;
        private CancellationTokenSource _cts;
        private int _activeTab;
        private int _selectedWell = -1;
        private string _selectedListingId = string.Empty;
        private bool _busy;
        private Text[] _wellTexts;
        private List<BazaarListingSummaryDto> _browseListings = new();

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;
        public int ActiveTabForTests => _activeTab;
        public string SelectedListingIdForTests => _selectedListingId ?? string.Empty;
        public int BrowseListingCountForTests => _browseListings.Count;

        public void Initialize(Action onBack, IBazaarGateway gateway = null)
        {
            _onBack = onBack;
            _gateway = gateway ?? new UnityCloudCodeBazaarGateway();
            BuildUI();
        }

        public Task<BazaarWalletResult> RefreshWalletForTests() => RefreshWalletAsync();

        public Task<BazaarListingResult> ListDeferredInstanceForTests(int askCredits = 100) =>
            ListItemAsync(DeferredInstanceIdPlaceholder, askCredits);

        public Task<BazaarBuyResult> BuySelectedForTests(string listingId = null) =>
            BuyItemAsync(string.IsNullOrWhiteSpace(listingId) ? _selectedListingId : listingId);

        public Task<BazaarCancelResult> CancelSelectedForTests(string listingId = null) =>
            CancelListingAsync(string.IsNullOrWhiteSpace(listingId) ? _selectedListingId : listingId);

        public Task RunPrimaryActionForTests() => RunPrimaryActionAsync();

        public Task<BazaarListingsQueryResult> RefreshBrowseForTests() => RefreshBrowseAsync();

        private void BuildUI()
        {
            TeardownUI();
            _cts = new CancellationTokenSource();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 12;

            // Opaque theme backing under preserveAspect shell art — kills camera clear-color
            // (sky-blue) letterbox bleed on non-16:9 viewports (same class of fix as e57aa02).
            Color shellFallback = new Color(0.08f, 0.09f, 0.12f);
            GameObject backing = new GameObject("BackgroundBacking", typeof(RectTransform), typeof(Image));
            backing.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(backing.GetComponent<RectTransform>());
            Image backingImg = backing.GetComponent<Image>();
            backingImg.color = new Color(shellFallback.r, shellFallback.g, shellFallback.b, 1f);
            backingImg.raycastTarget = false;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(bg.GetComponent<RectTransform>());
            BazaarUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), shellFallback);

            BuildHeader();
            BuildTabs();
            BuildListingGrid();
            BuildSelectedPanel();
            SelectTab(0);
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("BazaarHeader", typeof(RectTransform));
            topBar.transform.SetParent(_canvasObj.transform, false);
            SetNorm(topBar.GetComponent<RectTransform>(), 0f, 0.90f, 1f, 1f);

            GameObject backBtn = new GameObject("Btn_Back", typeof(RectTransform), typeof(Image), typeof(Button));
            backBtn.transform.SetParent(topBar.transform, false);
            Image backImg = backBtn.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNavTileButton(backBtn.GetComponent<Button>(), backImg);
            backImg.color = new Color(0.3f, 0.2f, 0.2f);
            backBtn.GetComponent<Button>().onClick.AddListener(() =>
            {
                TeardownUI();
                _onBack?.Invoke();
            });
            RectTransform backRect = backBtn.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 0.5f);
            backRect.anchorMax = new Vector2(0f, 0.5f);
            backRect.pivot = new Vector2(0f, 0.5f);
            backRect.anchoredPosition = new Vector2(30f, 0f);
            backRect.sizeDelta = new Vector2(160f, 56f);
            UISharedFoundation.CreateText(backBtn.transform, "Text", "< BACK", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(140f, 44f));

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "BAZAAR",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(640f, 48f));
            title.fontSize = 30;
            SetNorm(title.rectTransform, 0.28f, 0.15f, 0.72f, 0.9f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", "Ready.",
                UITextRole.Caption, TextAnchor.MiddleRight, new Color(0.85f, 0.75f, 0.5f), true,
                new Vector2(520f, 40f));
            SetNorm(_statusText.rectTransform, 0.72f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildTabs()
        {
            GameObject tabs = new GameObject("TabStrip", typeof(RectTransform));
            tabs.transform.SetParent(_canvasObj.transform, false);
            SetNorm(tabs.GetComponent<RectTransform>(), 0.03f, 0.04f, 0.56f, 0.14f);
            string[] labels = { "Browse", "Sell", "My Listings", "Wallet" };
            float w = 1f / labels.Length;
            for (int i = 0; i < labels.Length; i++)
            {
                int idx = i;
                GameObject tab = new GameObject($"Tab_{labels[i].Replace(" ", "")}", typeof(RectTransform), typeof(Image), typeof(Button));
                tab.transform.SetParent(tabs.transform, false);
                Image img = tab.GetComponent<Image>();
                HomeV3UiLibrary.ApplyNeutralActionButton(tab.GetComponent<Button>(), img, new Color(0.18f, 0.22f, 0.28f, 0.55f));
                tab.GetComponent<Button>().onClick.AddListener(() => SelectTab(idx));
                SetNorm(tab.GetComponent<RectTransform>(), i * w + 0.01f, 0.1f, (i + 1) * w - 0.01f, 0.9f);
                UISharedFoundation.CreateText(tab.transform, "Text", labels[i].ToUpperInvariant(), UITextRole.Caption,
                    TextAnchor.MiddleCenter, Color.white, true, new Vector2(180f, 36f));
            }
        }

        private void BuildListingGrid()
        {
            GameObject grid = new GameObject("ListingGrid", typeof(RectTransform));
            grid.transform.SetParent(_canvasObj.transform, false);
            SetNorm(grid.GetComponent<RectTransform>(), 0.04f, 0.22f, 0.56f, 0.72f);
            _wellTexts = new Text[BazaarOpenValues.ShellListingWellCount];
            for (int i = 0; i < BazaarOpenValues.ShellListingWellCount; i++)
            {
                int slot = i;
                int col = i % 3;
                int row = i / 3;
                float cw = 1f / 3f;
                float rh = 1f / 2f;
                GameObject well = new GameObject($"ListingWell_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                well.transform.SetParent(grid.transform, false);
                Image img = well.GetComponent<Image>();
                img.color = new Color(0.1f, 0.12f, 0.16f, 0.35f);
                Button btn = well.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SelectListingWell(slot));
                SetNorm(well.GetComponent<RectTransform>(), col * cw + 0.02f, 1f - (row + 1) * rh + 0.02f, (col + 1) * cw - 0.02f, 1f - row * rh - 0.02f);
                Text t = UISharedFoundation.CreateText(well.transform, "Placeholder",
                    "Empty",
                    UITextRole.Caption, TextAnchor.MiddleCenter, new Color(0.9f, 0.88f, 0.75f), true, new Vector2(160f, 40f));
                SetNorm(t.rectTransform, 0.05f, 0.35f, 0.95f, 0.65f);
                _wellTexts[i] = t;
            }
        }

        private void BuildSelectedPanel()
        {
            GameObject panel = new GameObject("SelectedPanel", typeof(RectTransform));
            panel.transform.SetParent(_canvasObj.transform, false);
            SetNorm(panel.GetComponent<RectTransform>(), 0.60f, 0.18f, 0.97f, 0.86f);
            _detailsText = UISharedFoundation.CreateText(panel.transform, "Details",
                "Loading…",
                UITextRole.Body, TextAnchor.UpperLeft, new Color(0.9f, 0.88f, 0.75f), true, new Vector2(480f, 220f));
            SetNorm(_detailsText.rectTransform, 0.05f, 0.35f, 0.95f, 0.95f);
            GameObject action = new GameObject("Btn_PrimaryAction", typeof(RectTransform), typeof(Image), typeof(Button));
            action.transform.SetParent(panel.transform, false);
            Image aImg = action.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(action.GetComponent<Button>(), aImg, new Color(0.2f, 0.4f, 0.3f));
            action.GetComponent<Button>().onClick.AddListener(() => _ = RunPrimaryActionAsync());
            SetNorm(action.GetComponent<RectTransform>(), 0.05f, 0.05f, 0.95f, 0.22f);
            UISharedFoundation.CreateText(action.transform, "Text", "CONFIRM", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(280f, 40f));
        }

        private void SelectTab(int tabIndex)
        {
            _activeTab = Mathf.Clamp(tabIndex, 0, 3);
            switch (_activeTab)
            {
                case 0:
                    _selectedListingId = string.Empty;
                    _ = RefreshBrowseAsync();
                    break;
                case 1:
                    SetDetails(
                        $"Sell — CONFIRM lists placeholder instance '{DeferredInstanceIdPlaceholder}' " +
                        "(expects INSTANCE_NOT_FOUND until Collection mints ItemInstances).");
                    SetStatus("Sell tab.");
                    break;
                case 2:
                    SetDetails(
                        "My Listings — no listing-query endpoint. Paste/select a real listingId via BuySelectedForTests / CancelSelectedForTests only.");
                    SetStatus("My Listings tab — no local catalog.");
                    _selectedListingId = string.Empty;
                    break;
                case 3:
                    SetDetails("Wallet — CONFIRM calls GetBazaarWallet (live).");
                    SetStatus("Wallet tab.");
                    _ = RefreshWalletAsync();
                    break;
            }
        }

        private void SelectListingWell(int wellIndex)
        {
            _selectedWell = wellIndex;
            if (_activeTab == 0 && wellIndex >= 0 && wellIndex < _browseListings.Count)
            {
                BazaarListingSummaryDto listing = _browseListings[wellIndex];
                _selectedListingId = listing.listingId;
                SetDetails($"Selected listing {listing.listingId}\nseller={listing.sellerId} ask={listing.askCredits}");
                SetStatus($"Selected {listing.listingId} — ask {listing.askCredits}");
                return;
            }

            // Honest empty catalog: do not invent listing-well-N ids for Buy/Cancel.
            _selectedListingId = string.Empty;
            SetDetails($"Selected empty well {wellIndex}. No listingId — Buy/Cancel refuse until a real listingId is supplied.");
            SetStatus($"Empty well {wellIndex} — no listingId.");
        }

        private async Task<BazaarListingsQueryResult> RefreshBrowseAsync()
        {
            if (!BeginBusy("Loading listings…"))
                return new BazaarListingsQueryResult { errorCode = "BUSY" };
            try
            {
                BazaarListingsQueryResult result = await _gateway.QueryListingsAsync(BazaarOpenValues.ShellListingWellCount, null, Token).ConfigureAwait(true);
                _browseListings = result != null && result.success && result.listings != null
                    ? result.listings
                    : new List<BazaarListingSummaryDto>();

                for (int i = 0; i < _wellTexts.Length; i++)
                {
                    _wellTexts[i].text = i < _browseListings.Count
                        ? $"{_browseListings[i].askCredits}c"
                        : "Empty";
                }

                if (result == null)
                {
                    SetStatus("Browse: null response.");
                    SetDetails("QueryBazaarListings returned no response.");
                }
                else if (!result.success)
                {
                    SetStatus($"Browse failed: {result.errorCode ?? "unknown"}");
                    SetDetails($"QueryBazaarListings errorCode={result.errorCode}");
                }
                else
                {
                    SetStatus($"Browse: {_browseListings.Count} active listing(s).");
                    SetDetails(_browseListings.Count == 0
                        ? "No active listings right now."
                        : "Select a well to view a listing and Buy.");
                }

                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"Browse failed: {ex.Message}");
                return new BazaarListingsQueryResult { errorCode = "CLIENT_EXCEPTION" };
            }
            finally
            {
                EndBusy();
            }
        }

        private async Task RunPrimaryActionAsync()
        {
            if (_busy) return;
            switch (_activeTab)
            {
                case 0:
                    await BuyItemAsync(_selectedListingId).ConfigureAwait(true);
                    break;
                case 1:
                    await ListItemAsync(DeferredInstanceIdPlaceholder, askCredits: 100).ConfigureAwait(true);
                    break;
                case 2:
                    await CancelListingAsync(_selectedListingId).ConfigureAwait(true);
                    break;
                case 3:
                    await RefreshWalletAsync().ConfigureAwait(true);
                    break;
            }
        }

        private async Task<BazaarWalletResult> RefreshWalletAsync()
        {
            if (!BeginBusy("Loading wallet…"))
                return new BazaarWalletResult { errorCode = "BUSY" };
            try
            {
                BazaarWalletResult result = await _gateway.GetWalletAsync(Token).ConfigureAwait(true);
                if (result == null)
                {
                    SetStatus("Wallet: null response.");
                    return new BazaarWalletResult { errorCode = "NULL_RESPONSE" };
                }

                if (!string.IsNullOrEmpty(result.errorCode))
                {
                    SetStatus($"Wallet error: {result.errorCode}");
                    SetDetails($"Wallet errorCode={result.errorCode}");
                }
                else
                {
                    SetStatus($"Wallet: {result.balanceCredits} Market Credits");
                    SetDetails($"balanceCredits={result.balanceCredits}");
                }

                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"Wallet failed: {ex.Message}");
                return new BazaarWalletResult { errorCode = "CLIENT_EXCEPTION" };
            }
            finally
            {
                EndBusy();
            }
        }

        private async Task<BazaarListingResult> ListItemAsync(string instanceId, int askCredits)
        {
            if (!BeginBusy("Listing item…"))
                return new BazaarListingResult { errorCode = "BUSY" };
            try
            {
                BazaarListingResult result = await _gateway.ListItemAsync(instanceId, askCredits, Token).ConfigureAwait(true);
                if (result == null)
                {
                    SetStatus("List: null response.");
                    return new BazaarListingResult { errorCode = "NULL_RESPONSE" };
                }

                if (result.success)
                    SetStatus($"Listed {result.listingId} (fee {result.goldFeeDue}g)");
                else
                    SetStatus($"List failed: {result.errorCode ?? "unknown"}");
                SetDetails(
                    $"ListItem instanceId={instanceId} askCredits={askCredits}\n" +
                    $"success={result.success} listingId={result.listingId} errorCode={result.errorCode}");
                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"List failed: {ex.Message}");
                return new BazaarListingResult { errorCode = "CLIENT_EXCEPTION" };
            }
            finally
            {
                EndBusy();
            }
        }

        private async Task<BazaarBuyResult> BuyItemAsync(string listingId)
        {
            if (string.IsNullOrWhiteSpace(listingId))
            {
                SetStatus("Buy: no listingId (browse catalog endpoint not live).");
                return new BazaarBuyResult { errorCode = "INVALID_REQUEST" };
            }

            if (!BeginBusy("Buying…"))
                return new BazaarBuyResult { errorCode = "BUSY" };
            try
            {
                string idempotencyKey = Guid.NewGuid().ToString("N");
                BazaarBuyResult result = await _gateway.BuyItemAsync(listingId, idempotencyKey, Token).ConfigureAwait(true);
                if (result == null)
                {
                    SetStatus("Buy: null response.");
                    return new BazaarBuyResult { errorCode = "NULL_RESPONSE" };
                }

                if (result.success)
                    SetStatus($"Bought {result.listingId} for {result.pricePaidCredits} credits");
                else
                    SetStatus($"Buy failed: {result.errorCode ?? "unknown"}");
                SetDetails(
                    $"BuyItem listingId={listingId}\n" +
                    $"success={result.success} errorCode={result.errorCode} paid={result.pricePaidCredits}");
                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"Buy failed: {ex.Message}");
                return new BazaarBuyResult { errorCode = "CLIENT_EXCEPTION" };
            }
            finally
            {
                EndBusy();
            }
        }

        private async Task<BazaarCancelResult> CancelListingAsync(string listingId)
        {
            if (string.IsNullOrWhiteSpace(listingId))
            {
                SetStatus("Cancel: no listingId (no my-listings query endpoint).");
                return new BazaarCancelResult { errorCode = "INVALID_REQUEST" };
            }

            if (!BeginBusy("Cancelling…"))
                return new BazaarCancelResult { errorCode = "BUSY" };
            try
            {
                BazaarCancelResult result = await _gateway.CancelListingAsync(listingId, Token).ConfigureAwait(true);
                if (result == null)
                {
                    SetStatus("Cancel: null response.");
                    return new BazaarCancelResult { errorCode = "NULL_RESPONSE" };
                }

                if (result.success)
                    SetStatus($"Cancelled {listingId}");
                else
                    SetStatus($"Cancel failed: {result.errorCode ?? "unknown"}");
                SetDetails($"CancelListing listingId={listingId}\nsuccess={result.success} errorCode={result.errorCode}");
                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"Cancel failed: {ex.Message}");
                return new BazaarCancelResult { errorCode = "CLIENT_EXCEPTION" };
            }
            finally
            {
                EndBusy();
            }
        }

        private bool BeginBusy(string message)
        {
            if (_busy) return false;
            _busy = true;
            SetStatus(message);
            return true;
        }

        private void EndBusy() => _busy = false;

        private CancellationToken Token =>
            _cts != null ? _cts.Token : CancellationToken.None;

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message ?? string.Empty;
        }

        private void SetDetails(string message)
        {
            if (_detailsText != null)
                _detailsText.text = message ?? string.Empty;
        }

        private static void SetNorm(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }

        public void TeardownUI()
        {
            if (_cts != null)
            {
                _cts.Cancel();
                _cts.Dispose();
                _cts = null;
            }

            if (_canvasObj == null) return;
            if (Application.isPlaying) Destroy(_canvasObj);
            else DestroyImmediate(_canvasObj);
            _canvasObj = null;
        }

        private void OnDestroy() => TeardownUI();
    }
}
