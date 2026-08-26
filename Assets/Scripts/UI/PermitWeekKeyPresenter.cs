using System;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Empire;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Permit Week-Key shell wired to <see cref="IPermitWeekKeyGateway"/> (server path).
    /// Does not touch <c>CollectionAscensionPermits</c> / local ManualTrustedWeekKey stopgap.
    /// </summary>
    public class PermitWeekKeyPresenter : MonoBehaviour
    {
        public const string CanvasName = "PermitWeekKeyCanvas";

        /// <summary>Default activity id used by CloudCode PermitWeekKey server tests.</summary>
        public const string DefaultActivityId = "ascensionPermit.weekly";

        private GameObject _canvasObj;
        private Action _onBack;
        private Text _statusText;
        private Text _detailsText;
        private Image _permitStateIcon;
        private IPermitWeekKeyGateway _gateway;
        private CancellationTokenSource _cts;
        private string _activityId = DefaultActivityId;
        private bool _busy;

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;
        public string ActivityIdForTests => _activityId;

        public void Initialize(Action onBack, IPermitWeekKeyGateway gateway = null, string activityId = null)
        {
            _onBack = onBack;
            _gateway = gateway ?? new UnityCloudCodePermitWeekKeyGateway();
            _activityId = string.IsNullOrWhiteSpace(activityId) ? DefaultActivityId : activityId.Trim();
            BuildUI();
        }

        public Task<PermitStatusResult> RefreshStatusForTests() => RefreshStatusAsync();

        public Task<PermitClaimResult> ClaimWeeklyForTests() => ClaimWeeklyAsync();

        private void BuildUI()
        {
            TeardownUI();
            _cts = new CancellationTokenSource();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 13;

            Color shellFallback = new Color(0.08f, 0.10f, 0.12f, 1f);
            GameObject backing = new GameObject("BackgroundBacking", typeof(RectTransform), typeof(Image));
            backing.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(backing.GetComponent<RectTransform>());
            Image backingImg = backing.GetComponent<Image>();
            backingImg.color = new Color(shellFallback.r, shellFallback.g, shellFallback.b, 1f);
            backingImg.raycastTarget = false;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(bg.GetComponent<RectTransform>());
            PermitWeekKeyUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), shellFallback);

            BuildHeader();
            BuildBody();
            _ = RefreshStatusAsync();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("PermitWeekKeyHeader", typeof(RectTransform));
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

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "PERMIT WEEK KEY",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(720f, 48f));
            title.fontSize = 28;
            SetNorm(title.rectTransform, 0.22f, 0.15f, 0.78f, 0.9f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", "Ready.",
                UITextRole.Caption, TextAnchor.MiddleRight, new Color(0.85f, 0.75f, 0.5f), true,
                new Vector2(420f, 40f));
            SetNorm(_statusText.rectTransform, 0.72f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildBody()
        {
            GameObject panel = new GameObject("PermitPanel", typeof(RectTransform));
            panel.transform.SetParent(_canvasObj.transform, false);
            SetNorm(panel.GetComponent<RectTransform>(), 0.12f, 0.18f, 0.88f, 0.86f);

            Text activity = UISharedFoundation.CreateText(panel.transform, "ActivityId",
                $"activityId: {_activityId}",
                UITextRole.Body, TextAnchor.MiddleLeft, new Color(0.85f, 0.82f, 0.7f), true, new Vector2(900f, 40f));
            SetNorm(activity.rectTransform, 0.05f, 0.82f, 0.78f, 0.95f);

            GameObject stateGo = new GameObject("PermitStateIcon", typeof(RectTransform), typeof(Image));
            stateGo.transform.SetParent(panel.transform, false);
            _permitStateIcon = stateGo.GetComponent<Image>();
            _permitStateIcon.sprite = PermitWeekKeyUiLibrary.LoadPermitState(PermitWeekKeyUiLibrary.PermitState.Unavailable);
            _permitStateIcon.preserveAspect = true;
            _permitStateIcon.raycastTarget = false;
            _permitStateIcon.color = Color.white;
            SetNorm(_permitStateIcon.rectTransform, 0.80f, 0.72f, 0.95f, 0.95f);

            _detailsText = UISharedFoundation.CreateText(panel.transform, "Details",
                "Server-authoritative weekly Ascension Permit status. Local CollectionAscensionPermits is not used here.",
                UITextRole.Body, TextAnchor.UpperLeft, new Color(0.9f, 0.88f, 0.75f), true, new Vector2(900f, 220f));
            SetNorm(_detailsText.rectTransform, 0.05f, 0.32f, 0.95f, 0.80f);

            CreateActionButton(panel.transform, "Btn_RefreshStatus", "REFRESH STATUS", 0.05f, 0.06f, 0.48f, 0.24f,
                () => _ = RefreshStatusAsync());
            CreateActionButton(panel.transform, "Btn_ClaimWeekly", "CLAIM WEEKLY", 0.52f, 0.06f, 0.95f, 0.24f,
                () => _ = ClaimWeeklyAsync(), primary: true);
        }

        private void CreateActionButton(Transform parent, string name, string label,
            float left, float bottom, float right, float top, UnityEngine.Events.UnityAction onClick,
            bool primary = false)
        {
            GameObject btn = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btn.transform.SetParent(parent, false);
            Image img = btn.GetComponent<Image>();
            if (primary) HomeV3UiLibrary.ApplyPrimaryActionButton(btn.GetComponent<Button>(), img);
            else HomeV3UiLibrary.ApplyNeutralActionButton(btn.GetComponent<Button>(), img, new Color(0.2f, 0.36f, 0.28f));
            btn.GetComponent<Button>().onClick.AddListener(onClick);
            SetNorm(btn.GetComponent<RectTransform>(), left, bottom, right, top);
            UISharedFoundation.CreateText(btn.transform, "Text", label, UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(280f, 40f));
        }

        private async Task<PermitStatusResult> RefreshStatusAsync()
        {
            if (!BeginBusy("Loading permit status…"))
                return new PermitStatusResult { errorCode = "BUSY" };
            try
            {
                PermitStatusResult result = await _gateway.GetStatusAsync(_activityId, Token).ConfigureAwait(true);
                if (result == null)
                {
                    SetPermitStateIcon(PermitWeekKeyUiLibrary.PermitState.Unavailable);
                    SetStatus("Status: null response.");
                    return new PermitStatusResult { errorCode = "NULL_RESPONSE" };
                }

                if (!string.IsNullOrEmpty(result.errorCode))
                {
                    SetPermitStateIcon(PermitWeekKeyUiLibrary.PermitState.Unavailable);
                    SetStatus($"Status error: {result.errorCode}");
                    SetDetails($"GetPermitStatus errorCode={result.errorCode}");
                }
                else
                {
                    SetPermitStateIcon(result.claimedThisWeek
                        ? PermitWeekKeyUiLibrary.PermitState.Claimed
                        : PermitWeekKeyUiLibrary.PermitState.Available);
                    SetStatus(
                        $"Balance {result.balance}/{result.hoardCap} · week {result.currentWeekKey} · " +
                        (result.claimedThisWeek ? "claimed" : "unclaimed"));
                    SetDetails(
                        $"activityId={_activityId}\n" +
                        $"balance={result.balance} weeklyRate={result.weeklyRate} hoardCap={result.hoardCap}\n" +
                        $"currentWeekKey={result.currentWeekKey} claimedThisWeek={result.claimedThisWeek}");
                }

                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"Status failed: {ex.Message}");
                return new PermitStatusResult { errorCode = "CLIENT_EXCEPTION" };
            }
            finally
            {
                EndBusy();
            }
        }

        private async Task<PermitClaimResult> ClaimWeeklyAsync()
        {
            if (!BeginBusy("Claiming weekly permit…"))
                return new PermitClaimResult { errorCode = "BUSY" };
            try
            {
                PermitClaimResult result = await _gateway.ClaimWeeklyAsync(_activityId, Token).ConfigureAwait(true);
                if (result == null)
                {
                    SetStatus("Claim: null response.");
                    return new PermitClaimResult { errorCode = "NULL_RESPONSE" };
                }

                if (!string.IsNullOrEmpty(result.errorCode) && !result.success)
                {
                    SetStatus($"Claim error: {result.errorCode}");
                }
                else if (result.alreadyClaimed)
                {
                    SetPermitStateIcon(PermitWeekKeyUiLibrary.PermitState.Claimed);
                    SetStatus($"Already claimed {result.weekKey}. Balance {result.balance}");
                }
                else if (result.success)
                {
                    SetPermitStateIcon(PermitWeekKeyUiLibrary.PermitState.Claimed);
                    SetStatus($"Granted {result.granted}. Balance {result.balance} ({result.weekKey})");
                }
                else
                {
                    SetStatus($"Claim failed: {result.errorCode ?? "unknown"}");
                }

                SetDetails(
                    $"ClaimWeekly activityId={_activityId}\n" +
                    $"success={result.success} granted={result.granted} balance={result.balance}\n" +
                    $"weekKey={result.weekKey} alreadyClaimed={result.alreadyClaimed} error={result.errorCode}");
                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"Claim failed: {ex.Message}");
                return new PermitClaimResult { errorCode = "CLIENT_EXCEPTION" };
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

        private void SetPermitStateIcon(PermitWeekKeyUiLibrary.PermitState state)
        {
            if (_permitStateIcon == null) return;
            Sprite sprite = PermitWeekKeyUiLibrary.LoadPermitState(state);
            if (sprite != null) _permitStateIcon.sprite = sprite;
        }

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
