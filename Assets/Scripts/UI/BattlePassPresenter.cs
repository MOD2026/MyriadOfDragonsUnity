using System;
using MyriadOfDragons.Data;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.Season;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Battle Pass dual-track shell (V1 art). Claims and premium unlock refuse while
    /// <see cref="BattlePassOpenValues"/> reward numbers stay OPEN.
    /// </summary>
    public class BattlePassPresenter : MonoBehaviour
    {
        public const string CanvasName = "BattlePassCanvas";

        private GameObject _canvasObj;
        private Action _onBackToHome;
        private RetentionTelemetryOutbox _telemetryOutbox;
        private Text _statusText;
        private Text _xpValuesText;

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;
        public RetentionTelemetryOutbox TelemetryOutboxForTests => _telemetryOutbox;

        public void Initialize(Action onBackToHome, RetentionTelemetryOutbox telemetryOutbox = null)
        {
            _onBackToHome = onBackToHome;
            _telemetryOutbox = telemetryOutbox ?? new RetentionTelemetryOutbox(new UnityCloudCodeRetentionTelemetryGateway());
            BuildUI();
            RefreshBound();
        }

        public BattlePassClaimResult ClaimTierForTests(int tierIndex, bool premiumTrack) => AttemptClaim(tierIndex, premiumTrack);

        /// <summary>Real retention-telemetry trigger point (register: "Retention telemetry
        /// architecture - LOCKED" / dispatch "wire the actual emit calls into real gameplay call
        /// sites") - the single real claim path both the production tier-well button and the
        /// EditMode test entry go through.</summary>
        private BattlePassClaimResult AttemptClaim(int tierIndex, bool premiumTrack)
        {
            PlayerProfile profile = SaveSystem.CurrentProfile ?? SaveManager.SaveData;
            BattlePassClaimResult result = BattlePassOpenValues.TryClaimTier(profile, tierIndex, premiumTrack);
            if (result.Status == BattlePassClaimStatus.Applied)
                SaveSystem.Save(profile);
            SetStatus(result.Message);

            if (_telemetryOutbox != null && result.Status == BattlePassClaimStatus.Applied)
            {
                string playerId = RetentionTelemetryPlayerId.CurrentOrEmpty();
                _telemetryOutbox.Enqueue(RetentionTelemetryEvents.ModeRewardClaimed(
                    playerId, "battle_pass", $"tier_{tierIndex}_{(premiumTrack ? "premium" : "free")}", "claimed"));
                _ = _telemetryOutbox.FlushAsync(System.Threading.CancellationToken.None);
            }
            return result;
        }

        public BattlePassClaimResult UnlockPremiumForTests()
        {
            BattlePassClaimResult result = BattlePassOpenValues.TryUnlockPremium();
            SetStatus(result.Message);
            return result;
        }

        private void BuildUI()
        {
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 12;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(bg.GetComponent<RectTransform>());
            Image bgImg = bg.GetComponent<Image>();
            bgImg.raycastTarget = false;
            BattlePassUiLibrary.ApplyFullscreenShell(bgImg, new Color(0.08f, 0.09f, 0.11f));

            BuildHeader();
            BuildSeasonXp();
            BuildTracks();
            BuildPremiumBar();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("BattlePassHeader", typeof(RectTransform), typeof(Image));
            topBar.transform.SetParent(_canvasObj.transform, false);
            Image topBg = topBar.GetComponent<Image>();
            topBg.color = UIFrozenTokens.ColorHeader;
            topBg.raycastTarget = false;
            SetNorm(topBar.GetComponent<RectTransform>(), 0f, 0.88f, 1f, 1f);
            UISharedFoundation.AddLocalGradientScrim(topBar.transform, Vector2.zero, new Vector2(1920f, 130f), UISharedFoundation.GradientDirection.TopToBottom, 0.95f);

            CreateHeaderButton(topBar.transform, "Btn_Back", "< BACK", new Vector2(30f, 0f), () =>
            {
                TeardownUI();
                _onBackToHome?.Invoke();
            });

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "BATTLE PASS",
                UITextRole.Display, TextAnchor.MiddleCenter, Color.white, true,
                new Vector2(640f, 48f));
            title.fontSize = 32;
            title.fontStyle = FontStyle.Bold;
            SetNorm(title.rectTransform, 0.28f, 0.48f, 0.72f, 0.95f);

            Text season = UISharedFoundation.CreateText(topBar.transform, "SeasonLength",
                BattlePassOpenValues.SeasonLengthCopy, UITextRole.Title, TextAnchor.MiddleCenter,
                Color.white, true, new Vector2(420f, 32f));
            season.fontSize = 24;
            season.fontStyle = FontStyle.Bold;
            SetNorm(season.rectTransform, 0.32f, 0.08f, 0.68f, 0.48f);

            Text timer = UISharedFoundation.CreateText(topBar.transform, "SeasonTimer",
                MetagameShellProfileBinding.UtcDayKeyLine(), UITextRole.Body, TextAnchor.MiddleLeft,
                Color.white, true, new Vector2(280f, 28f));
            timer.fontSize = 28;
            timer.fontStyle = FontStyle.Bold;
            // Widened 0.18 -> 0.12 (scoped exception, SeasonTimer only): "UTC reset ..." at 28px
            // bold did not fit the old 0.14-wide column, wrapped to two lines and measured 63px
            // against a 51.8px band. Grow-the-box per the locked rule; the 28px size is untouched.
            // Widened rather than heightened because Title owns 0.28-0.72 x at 0.48-0.95 y, so
            // growing upward would have overlapped it. Left edge stops clear of Btn_Back, which
            // ends near 0.099 (anchored x=30, width 160 in a 1920 header).
            SetNorm(timer.rectTransform, 0.12f, 0.08f, 0.32f, 0.48f);
        }

        private void BuildSeasonXp()
        {
            GameObject xp = new GameObject("SeasonXpRow", typeof(RectTransform), typeof(Image));
            xp.transform.SetParent(_canvasObj.transform, false);
            Image xpImg = xp.GetComponent<Image>();
            xpImg.color = UIFrozenTokens.ColorHeader;
            xpImg.raycastTarget = false;
            SetNorm(xp.GetComponent<RectTransform>(), 0.18f, 0.78f, 0.82f, 0.86f);
            UISharedFoundation.AddLocalGradientScrim(xp.transform, Vector2.zero, new Vector2(1200f, 80f), UISharedFoundation.GradientDirection.TopToBottom, 0.95f);

            // SeasonXpRow label column.
            Text xpLabel = UISharedFoundation.CreateText(xp.transform, "Label", "SEASON XP", UITextRole.Caption,
                TextAnchor.MiddleLeft, Color.white, true, new Vector2(160f, 24f));
            xpLabel.fontSize = 24;
            xpLabel.fontStyle = FontStyle.Bold;
            UISharedFoundation.ApplyTextShadow(xpLabel);
            SetNorm(xpLabel.rectTransform, 0.00f, 0.15f, 0.16f, 0.85f);

            GameObject bar = new GameObject("XpBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(xp.transform, false);
            bar.GetComponent<Image>().color = new Color(0.18f, 0.55f, 0.32f, 0.85f);
            bar.GetComponent<Image>().raycastTarget = false;
            SetNorm(bar.GetComponent<RectTransform>(), 0.18f, 0.28f, 0.72f, 0.72f);

            Text values = UISharedFoundation.CreateText(xp.transform, "XpValues",
                MetagameShellProfileBinding.PassTierProgressLine(),
                UITextRole.Body, TextAnchor.MiddleRight, Color.white, true,
                new Vector2(280f, 28f));
            values.fontSize = 28;
            values.fontStyle = FontStyle.Bold;
            _xpValuesText = values;
            SetNorm(values.rectTransform, 0.74f, 0.15f, 1f, 0.85f);
        }

        private void RefreshBound()
        {
            if (_xpValuesText != null)
                _xpValuesText.text = MetagameShellProfileBinding.PassTierProgressLine();
        }

        private void BuildTracks()
        {
            GameObject table = new GameObject("TrackTable", typeof(RectTransform));
            table.transform.SetParent(_canvasObj.transform, false);
            SetNorm(table.GetComponent<RectTransform>(), 0.04f, 0.22f, 0.96f, 0.76f);

            // Track titles live in the dual-track shell art — do not draw procedural TrackLabel
            // text over FREE TRACK / PREMIUM TRACK (verified double-label collision at 1920x1080).
            BuildTrackRow(table.transform, "FreeTrackRow", premium: false, 0.52f, 0.98f);
            BuildTrackRow(table.transform, "PremiumTrackRow", premium: true, 0.02f, 0.48f);
        }

        private void BuildTrackRow(Transform parent, string rowName, bool premium, float yMin, float yMax)
        {
            GameObject row = new GameObject(rowName, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            SetNorm(row.GetComponent<RectTransform>(), 0f, yMin, 1f, yMax);

            float wellWidth = 0.86f / BattlePassOpenValues.ShellTierWellCount;
            for (int i = 0; i < BattlePassOpenValues.ShellTierWellCount; i++)
            {
                int tier = i;
                bool capturedPremium = premium;
                float left = 0.14f + i * wellWidth;
                GameObject well = new GameObject($"TierWell_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                well.transform.SetParent(row.transform, false);
                Image img = well.GetComponent<Image>();
                Button btn = well.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => AttemptClaim(tier, capturedPremium));
                SetNorm(well.GetComponent<RectTransform>(), left, 0.08f, left + wellWidth * 0.92f, 0.92f);
                // Applied AFTER final positioning - see EmpirePresenter's same fix for why.
                UISharedFoundation.ApplyFramedPanel(img, null,
                    UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorHeader);

                Text headerN = UISharedFoundation.CreateText(well.transform, "TierIndex", $"T{i + 1}",
                    UITextRole.Caption, TextAnchor.UpperCenter, Color.white, true, new Vector2(80f, 24f));
                headerN.fontSize = 24;
                headerN.fontStyle = FontStyle.Bold;
                UISharedFoundation.ApplyTextShadow(headerN);
                SetNorm(headerN.rectTransform, 0.05f, 0.72f, 0.95f, 0.98f);

                Text amount = UISharedFoundation.CreateText(well.transform, "RewardAmount",
                    MetagameShellProfileBinding.PassSeasonXpLine(), UITextRole.Body, TextAnchor.MiddleCenter,
                    Color.white, true, new Vector2(100f, 28f));
                amount.fontSize = 28;
                amount.fontStyle = FontStyle.Bold;
                UISharedFoundation.ApplyTextShadow(amount);
                SetNorm(amount.rectTransform, 0.05f, 0.08f, 0.95f, 0.45f);
            }
        }

        private void BuildPremiumBar()
        {
            GameObject bar = new GameObject("PremiumBar", typeof(RectTransform));
            bar.transform.SetParent(_canvasObj.transform, false);
            SetNorm(bar.GetComponent<RectTransform>(), 0.04f, 0.04f, 0.96f, 0.18f);

            GameObject unlock = new GameObject("Btn_UnlockPremium", typeof(RectTransform), typeof(Image), typeof(Button));
            unlock.transform.SetParent(bar.transform, false);
            Image img = unlock.GetComponent<Image>();
            HomeV3UiLibrary.ApplyPrimaryActionButton(unlock.GetComponent<Button>(), img);
            unlock.GetComponent<Button>().onClick.AddListener(() =>
            {
                BattlePassClaimResult result = BattlePassOpenValues.TryUnlockPremium();
                SetStatus(result.Message);
            });
            SetNorm(unlock.GetComponent<RectTransform>(), 0.00f, 0.25f, 0.32f, 0.90f);
            Text unlockText = UISharedFoundation.CreateText(unlock.transform, "Text", "UNLOCK PREMIUM", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(280f, 40f));
            unlockText.fontSize = 28;
            unlockText.fontStyle = FontStyle.Bold;
            UISharedFoundation.StretchFull(unlockText.rectTransform);

            Text access = UISharedFoundation.CreateText(bar.transform, "PremiumAccessCopy",
                $"Premium track · unlock price not set · {MetagameShellProfileBinding.WalletLine()}",
                UITextRole.Body, TextAnchor.MiddleLeft, new Color(0.9f, 0.88f, 0.75f), true, new Vector2(800f, 36f));
            access.fontSize = 28;
            access.fontStyle = FontStyle.Bold;
            SetNorm(access.rectTransform, 0.36f, 0.45f, 0.98f, 0.90f);

            _statusText = UISharedFoundation.CreateText(bar.transform, "StatusLine", BattlePassOpenValues.PlayerStatus,
                UITextRole.Caption, TextAnchor.MiddleLeft, new Color(0.85f, 0.75f, 0.5f), true, new Vector2(1600f, 28f));
            _statusText.fontSize = 24;
            _statusText.fontStyle = FontStyle.Bold;
            SetNorm(_statusText.rectTransform, 0.00f, 0.00f, 1f, 0.32f);
        }

        private static void CreateHeaderButton(Transform parent, string name, string label, Vector2 anchoredPos,
            Action onClick)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            Image img = btnObj.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNavTileButton(btnObj.GetComponent<Button>(), img);
            img.color = new Color(0.12f, 0.14f, 0.18f, 0.95f);
            btnObj.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(160f, 56f);
            UISharedFoundation.AddLocalGradientScrim(btnObj.transform, Vector2.zero, new Vector2(160f, 56f), UISharedFoundation.GradientDirection.TopToBottom, 0.95f);
            Text backText = UISharedFoundation.CreateText(btnObj.transform, "Text", label, UITextRole.Body, TextAnchor.MiddleCenter,
                Color.white, true, new Vector2(140f, 44f));
            backText.fontSize = 28;
            backText.fontStyle = FontStyle.Bold;
            UISharedFoundation.ApplyTextShadow(backText);
            UISharedFoundation.StretchFull(backText.rectTransform);
        }

        /// <summary>Pushes a claim result message into the StatusLine. Maps the OpenValues
        /// diagnostic to its short player-facing form: BattlePassClaimResult.Message carries the
        /// full ~350-char StatusNote when values aren't locked, which overflows this band. Every
        /// other status message is already short and passes through unchanged.</summary>
        private void SetStatus(string message)
        {
            if (_statusText == null) return;
            _statusText.text = message == BattlePassOpenValues.StatusNote
                ? BattlePassOpenValues.PlayerStatus
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
            if (_canvasObj == null) return;
            if (Application.isPlaying) Destroy(_canvasObj);
            else DestroyImmediate(_canvasObj);
            _canvasObj = null;
        }

        private void OnDestroy() => TeardownUI();
    }
}
