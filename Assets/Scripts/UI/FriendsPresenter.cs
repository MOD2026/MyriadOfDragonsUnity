using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Metagame;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>FRIENDS V1 art shell wired to <see cref="IFriendsGateway"/> (live Friends
    /// CloudCode module, nonprod-validation). The roster (Friends tab), daily gift, and
    /// add-friend-by-typed-account-id (profile drawer) are all real; Requests/Find nav tabs still
    /// route through FriendsOpenValues (no distinct UI for browsing/searching other accounts).</summary>
    public class FriendsPresenter : MonoBehaviour
    {
        public const string CanvasName = "FriendsCanvas";
        public const int VisibleRowCount = 6;

        private GameObject _canvasObj;
        private Action _onBack;
        private Text _statusText;
        private Text[] _rowTexts;
        private RectTransform _friendsListRoot;
        private RectTransform _emptyStateRegion;
        // Add Friend has no player-facing UI: the account-id input and Btn_AddFriend were
        // retired as a deferred feature (social routes are not configured - see
        // FriendsOpenValues.AreSocialRoutesConfigured). The gateway path is preserved and
        // driven by AddFriendForTests, so this holds the target that the input used to.
        private string _pendingAddFriendTarget = string.Empty;
        private IFriendsGateway _gateway;
        private CancellationTokenSource _cts;
        private List<FriendSummaryDto> _friends = new();
        private string _selectedCounterpartId = string.Empty;

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;
        public int FriendCountForTests => _friends.Count;
        public string SelectedCounterpartIdForTests => _selectedCounterpartId ?? string.Empty;

        public void Initialize(Action onBack, IFriendsGateway gateway = null)
        {
            _onBack = onBack;
            _gateway = gateway ?? new UnityCloudCodeFriendsGateway();
            BuildUI();
        }

        public FriendsActionResult MessageForTests()
        {
            var r = FriendsOpenValues.TryMessage();
            SetStatus(r.Message);
            return r;
        }

        public Task<ListFriendsGatewayResult> RefreshFriendsForTests() => RefreshFriendsAsync();

        public Task<GiftGatewayResult> GiftSelectedForTests() => SendGiftAsync();

        public Task<FriendGatewayResult> AddFriendForTests(string targetAccountId)
        {
            _pendingAddFriendTarget = targetAccountId ?? string.Empty;
            return SendAddFriendAsync();
        }

        private void BuildUI()
        {
            TeardownUI();
            _cts = new CancellationTokenSource();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 12;

            // Opaque theme backing under preserveAspect shell — same letterbox fix as Bazaar/Chat.
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
            FriendsUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), shellFallback);

            BuildHeader();
            BuildNav(); BuildList(); BuildProfile();
            _ = RefreshFriendsAsync();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("FriendsHeader", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(_canvasObj.transform, false);
            Image topBg = topBar.GetComponent<Image>();
            topBg.color = UIFrozenTokens.ColorHeader;
            topBg.raycastTarget = false;
            SetNorm(topBar.GetComponent<RectTransform>(), 0f, 0.90f, 1f, 1f);
            UISharedFoundation.AddLocalGradientScrim(topBar.transform, Vector2.zero, new Vector2(1920f, 108f), UISharedFoundation.GradientDirection.TopToBottom, 0.95f);

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
            UISharedFoundation.AddLocalGradientScrim(backBtn.transform, Vector2.zero, new Vector2(160f, 56f), UISharedFoundation.GradientDirection.TopToBottom, 0.95f);
            Text backTxt = UISharedFoundation.CreateText(backBtn.transform, "Text", "< BACK", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(140f, 44f));
            backTxt.fontSize = 22;
            backTxt.fontStyle = FontStyle.Bold;

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "FRIENDS",
                UITextRole.Display, TextAnchor.MiddleCenter, Color.white, true,
                new Vector2(640f, 48f));
            title.fontSize = 30;
            title.fontStyle = FontStyle.Bold;
            SetNorm(title.rectTransform, 0.29f, 0.15f, 0.71f, 0.90f);

            Text identity = UISharedFoundation.CreateText(topBar.transform, "SelfIdentity",
                MetagameShellProfileBinding.SelfIdentityLine(), UITextRole.Caption, TextAnchor.MiddleLeft,
                Color.white, true, new Vector2(280f, 28f));
            identity.fontSize = 22;
            identity.fontStyle = FontStyle.Bold;
            SetNorm(identity.rectTransform, 0.11f, 0.15f, 0.28f, 0.85f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", FriendsOpenValues.PlayerStatus,
                UITextRole.Caption, TextAnchor.MiddleRight, Color.white, true,
                new Vector2(520f, 40f));
            _statusText.fontSize = 22;
            _statusText.fontStyle = FontStyle.Bold;
            SetNorm(_statusText.rectTransform, 0.72f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildNav()
        {
            GameObject rail = new GameObject("NavRail", typeof(RectTransform));
            rail.transform.SetParent(_canvasObj.transform, false);
            SetNorm(rail.GetComponent<RectTransform>(), 0.05f, 0.08f, 0.14f, 0.88f);
            // Requests/Find retired: FriendsOpenValues.TrySelectTab is an unconditional
            // Refuse, so both were dead controls that could never select anything.
            string[] tabs = { "Friends" };
            // Fixed tab height, NOT 1f/tabs.Length. With the two dead tabs retired the
            // proportional split gave the sole survivor the whole 864px rail, which read as a
            // broken column rather than a nav item. 0.12 of the rail is ~86px tall against a
            // ~155px usable width - a normal tile, and it stays correct if a real second tab
            // is ever added.
            const float h = 0.12f;
            for (int i = 0; i < tabs.Length; i++)
            {
                int idx = i;
                GameObject tab = new GameObject($"Nav_{tabs[i]}", typeof(RectTransform), typeof(Image), typeof(Button));
                tab.transform.SetParent(rail.transform, false);
                Image img = tab.GetComponent<Image>();
                HomeV3UiLibrary.ApplyNeutralActionButton(tab.GetComponent<Button>(), img, new Color(0.16f, 0.2f, 0.26f, 0.7f));
                tab.GetComponent<Button>().onClick.AddListener(() =>
                {
                    if (idx == 0) _ = RefreshFriendsAsync();
                    else SetStatus(FriendsOpenValues.TrySelectTab(idx).Message);
                });
                SetNorm(tab.GetComponent<RectTransform>(), 0.05f, 1f - (i + 1) * h + 0.01f, 0.95f, 1f - i * h - 0.01f);
                UISharedFoundation.CreateText(tab.transform, "Text", tabs[i].ToUpperInvariant(), UITextRole.Caption,
                    TextAnchor.MiddleCenter, Color.white, true, new Vector2(150f, 28f));
            }
        }

        private void BuildList()
        {
            GameObject list = new GameObject("FriendsList", typeof(RectTransform));
            list.transform.SetParent(_canvasObj.transform, false);
            SetNorm(list.GetComponent<RectTransform>(), 0.15f, 0.08f, 0.68f, 0.88f);
            _friendsListRoot = list.GetComponent<RectTransform>();
            _rowTexts = new Text[VisibleRowCount];
            for (int i = 0; i < VisibleRowCount; i++)
            {
                int row = i;
                float h = 1f / VisibleRowCount;
                GameObject well = new GameObject($"FriendRow_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                well.transform.SetParent(list.transform, false);
                Image img = well.GetComponent<Image>();
                Button btn = well.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => SelectFriendRow(row));
                SetNorm(well.GetComponent<RectTransform>(), 0.02f, 1f - (i + 1) * h + 0.02f, 0.98f, 1f - i * h - 0.02f);
                // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
                UISharedFoundation.ApplyFramedPanel(img, null,
                    UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorHeader,
                    kind: UISharedFoundation.FramedPanelKind.ListRow);
                // Default empty-row glyph; RefreshFriendsAsync swaps to relationship state cells.
                FriendsUiLibrary.ApplyAtlasIcon(well.transform, "RelIcon",
                    FriendsUiLibrary.LoadRelationshipCell(FriendsUiLibrary.RelOffline),
                    0.02f, 0.12f, 0.14f, 0.88f);
                Text t = UISharedFoundation.CreateText(well.transform, "DisplayName", "Empty",
                    UITextRole.Caption, TextAnchor.MiddleLeft, new Color(0.9f, 0.88f, 0.75f), true, new Vector2(400f, 28f));
                SetNorm(t.rectTransform, 0.16f, 0.1f, 0.96f, 0.9f);
                _rowTexts[i] = t;
            }

            GameObject emptyObj = new GameObject("FriendsEmptyState", typeof(RectTransform));
            emptyObj.transform.SetParent(list.transform, false);
            _emptyStateRegion = emptyObj.GetComponent<RectTransform>();
            SetNorm(_emptyStateRegion, 0f, 0f, 1f, 1f);
            emptyObj.SetActive(false);
        }

        /// <summary>
        /// Shows the shared empty state in place of the six friend rows, or restores the rows.
        ///
        /// The rows are DEACTIVATED while the empty state shows, and that is load-bearing twice
        /// over. FriendsLayoutTests.Friends_NeverDrawsArtOnTopOfAnInteractiveControl fails if the
        /// illustration is drawn over live buttons - a tap would land on art instead of the row.
        /// And six rows reading "Empty" that answer a tap with "Empty row 3." are exactly the dead
        /// end wearing a control's clothes that UIEmptyState refuses. All nine controls are still
        /// BUILT and are restored the moment a friend exists; none was removed.
        ///
        /// Waiting, with no action: Add Friend is retired under Policy B, so there is no truthful
        /// action to offer here (board ruling, 2026-08-28). Title supplied by CC; nothing invented.
        /// </summary>
        private void ApplyEmptyState(bool isEmpty)
        {
            if (_friendsListRoot == null || _emptyStateRegion == null) return;

            for (int i = 0; i < VisibleRowCount; i++)
            {
                Transform row = _friendsListRoot.Find($"FriendRow_{i}");
                if (row != null) row.gameObject.SetActive(!isEmpty);
            }

            _emptyStateRegion.gameObject.SetActive(isEmpty);
            if (!isEmpty) return;

            for (int i = _emptyStateRegion.childCount - 1; i >= 0; i--)
            {
                GameObject child = _emptyStateRegion.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }

            UIEmptyState.Build(
                _emptyStateRegion,
                EmptyStateKind.Waiting,
                "No friends yet",
                explanation: null,
                statusLine: null,
                illustrationPath: UIEmptyState.IllustrationNoFriends);
        }

        private void SelectFriendRow(int row)
        {
            if (row < 0 || row >= _friends.Count)
            {
                _selectedCounterpartId = string.Empty;
                SetStatus($"Empty row {row}.");
                return;
            }

            FriendSummaryDto friend = _friends[row];
            _selectedCounterpartId = friend.counterpartAccountId;
            SetStatus(friend.status == "Accepted"
                ? $"Selected {friend.counterpartAccountId} — {(friend.canGiftToday ? "gift available" : "gift already sent today")}"
                : $"Selected {friend.counterpartAccountId} — {(friend.isOutgoingRequest ? "request sent" : "incoming request")}");
        }

        private async Task<ListFriendsGatewayResult> RefreshFriendsAsync()
        {
            SetStatus("Loading friends…");
            try
            {
                ListFriendsGatewayResult result = await _gateway.ListFriendsAsync(Token).ConfigureAwait(true);
                _friends = result != null && result.success && result.friends != null
                    ? result.friends
                    : new List<FriendSummaryDto>();

                ApplyEmptyState(_friends.Count == 0);

                for (int i = 0; i < _rowTexts.Length; i++)
                {
                    Transform row = _canvasObj != null
                        ? _canvasObj.transform.Find($"FriendsList/FriendRow_{i}")
                        : null;
                    Image relIcon = row != null ? row.Find("RelIcon")?.GetComponent<Image>() : null;

                    if (i >= _friends.Count)
                    {
                        _rowTexts[i].text = "Empty";
                        if (relIcon != null)
                            relIcon.sprite = FriendsUiLibrary.LoadRelationshipCell(FriendsUiLibrary.RelOffline);
                        continue;
                    }

                    FriendSummaryDto friend = _friends[i];
                    _rowTexts[i].text = friend.status == "Accepted"
                        ? friend.counterpartAccountId
                        : $"{friend.counterpartAccountId} ({(friend.isOutgoingRequest ? "pending sent" : "pending received")})";
                    if (relIcon != null)
                    {
                        int cell = friend.status == "Accepted"
                            ? FriendsUiLibrary.RelFriend
                            : (friend.isOutgoingRequest ? FriendsUiLibrary.RelOutgoing : FriendsUiLibrary.RelIncoming);
                        relIcon.sprite = FriendsUiLibrary.LoadRelationshipCell(cell);
                    }
                }

                if (result == null)
                {
                    SetStatus("Friends: null response.");
                }
                else if (!result.success)
                {
                    SetStatus($"Friends load failed: {result.errorCode ?? "unknown"}");
                }
                else
                {
                    SetStatus($"Friends: {_friends.Count} relationship(s).");
                }

                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"Friends load failed: {ex.Message}");
                return new ListFriendsGatewayResult { errorCode = "CLIENT_EXCEPTION" };
            }
        }

        private async Task<GiftGatewayResult> SendGiftAsync()
        {
            if (string.IsNullOrWhiteSpace(_selectedCounterpartId))
            {
                SetStatus("Gift: no friend selected.");
                return new GiftGatewayResult { errorCode = "INVALID_REQUEST" };
            }

            try
            {
                GiftGatewayResult result = await _gateway.SendDailyGiftAsync(_selectedCounterpartId, Token).ConfigureAwait(true);
                SetStatus(result != null && result.success
                    ? $"Gift sent to {_selectedCounterpartId}."
                    : $"Gift failed: {result?.errorCode ?? "unknown"}");
                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"Gift failed: {ex.Message}");
                return new GiftGatewayResult { errorCode = "CLIENT_EXCEPTION" };
            }
        }

        private CancellationToken Token => _cts != null ? _cts.Token : CancellationToken.None;

        private void BuildProfile()
        {
            GameObject drawer = new GameObject("ProfileDrawer", typeof(RectTransform));
            drawer.transform.SetParent(_canvasObj.transform, false);
            SetNorm(drawer.GetComponent<RectTransform>(), 0.70f, 0.08f, 0.95f, 0.88f);
            Text summary = UISharedFoundation.CreateText(drawer.transform, "PublicSummary",
                $"{MetagameShellProfileBinding.SelfIdentityLine()}\n{MetagameShellProfileBinding.WalletLine()}\n\n" +
                "Select a friend, then GIFT to send today's daily gift.",
                UITextRole.Body, TextAnchor.UpperCenter,
                new Color(0.9f, 0.88f, 0.75f), true, new Vector2(360f, 100f));
            SetNorm(summary.rectTransform, 0.08f, 0.62f, 0.92f, 0.85f);

            // Reframes the band vacated by the retired account-id input and ADD FRIEND button.
            // The drawer's shell art carries a slot here, so leaving it empty read as a missing
            // element in the loaded-profile capture. A non-interactive deferred-feature note
            // fills it honestly and keeps the control count at 9.
            Text deferred = UISharedFoundation.CreateText(drawer.transform, "DeferredNote",
                FriendsOpenValues.PlayerStatus,
                UITextRole.Caption, TextAnchor.MiddleCenter,
                new Color(0.72f, 0.70f, 0.62f), true, new Vector2(360f, 60f));
            SetNorm(deferred.rectTransform, 0.08f, 0.34f, 0.92f, 0.56f);

            GameObject msg = new GameObject("Btn_Gift", typeof(RectTransform), typeof(Image), typeof(Button));
            msg.transform.SetParent(drawer.transform, false);
            Image mImg = msg.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(msg.GetComponent<Button>(), mImg, new Color(0.2f, 0.32f, 0.4f));
            msg.GetComponent<Button>().onClick.AddListener(() => _ = SendGiftAsync());
            SetNorm(msg.GetComponent<RectTransform>(), 0.1f, 0.16f, 0.9f, 0.28f);
            FriendsUiLibrary.ApplyAtlasIcon(msg.transform, "ActionIcon",
                FriendsUiLibrary.LoadProfileActionCell(FriendsUiLibrary.ActionChat),
                0.04f, 0.12f, 0.22f, 0.88f);
            UISharedFoundation.CreateText(msg.transform, "Text", "GIFT", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(200f, 36f));
        }

        private async Task<FriendGatewayResult> SendAddFriendAsync()
        {
            string targetAccountId = (_pendingAddFriendTarget ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(targetAccountId))
            {
                SetStatus("Add friend: type an account id first.");
                return new FriendGatewayResult { errorCode = "INVALID_REQUEST" };
            }

            try
            {
                FriendGatewayResult result = await _gateway.AddFriendAsync(targetAccountId, Token).ConfigureAwait(true);
                if (result != null && result.success)
                {
                    SetStatus($"Request sent to {targetAccountId}.");
                    _pendingAddFriendTarget = string.Empty;
                    await RefreshFriendsAsync().ConfigureAwait(true);
                }
                else
                {
                    SetStatus($"Add friend failed: {result?.errorCode ?? "unknown"}");
                }

                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"Add friend failed: {ex.Message}");
                return new FriendGatewayResult { errorCode = "CLIENT_EXCEPTION" };
            }
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                // The OpenValues diagnostic is mapped to its short player-facing form: the
                // ActionResult Message carries the full StatusNote when values are not locked,
                // which overflows this band. Repointing only the initial CreateText would leave
                // the long string one click away from returning.
                _statusText.text = message == FriendsOpenValues.StatusNote
                    ? FriendsOpenValues.PlayerStatus
                    : (message ?? string.Empty);
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
