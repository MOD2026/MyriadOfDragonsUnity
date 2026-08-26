using System;
using MyriadOfDragons.Metagame;
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
            BattlePassClaimResult result = BattlePassOpenValues.TryClaimTier(tierIndex, premiumTrack);
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
            GameObject topBar = new GameObject("BattlePassHeader", typeof(RectTransform));
            topBar.transform.SetParent(_canvasObj.transform, false);
            SetNorm(topBar.GetComponent<RectTransform>(), 0f, 0.88f, 1f, 1f);

            CreateHeaderButton(topBar.transform, "Btn_Back", "< BACK", new Vector2(30f, 0f), () =>
            {
                TeardownUI();
                _onBackToHome?.Invoke();
            });

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "BATTLE PASS",
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.82f), true,
                new Vector2(640f, 48f));
            title.fontSize = 32;
            SetNorm(title.rectTransform, 0.28f, 0.48f, 0.72f, 0.95f);

            Text season = UISharedFoundation.CreateText(topBar.transform, "SeasonLength",
                BattlePassOpenValues.SeasonLengthCopy, UITextRole.Title, TextAnchor.MiddleCenter,
                new Color(0.75f, 0.88f, 0.7f), true, new Vector2(420f, 32f));
            season.fontSize = 18;
            SetNorm(season.rectTransform, 0.32f, 0.08f, 0.68f, 0.48f);

            Text timer = UISharedFoundation.CreateText(topBar.transform, "SeasonTimer",
                MetagameShellProfileBinding.UtcDayKeyLine(), UITextRole.Body, TextAnchor.MiddleLeft,
                new Color(0.85f, 0.82f, 0.7f), true, new Vector2(280f, 28f));
            SetNorm(timer.rectTransform, 0.18f, 0.08f, 0.32f, 0.48f);
        }

        private void BuildSeasonXp()
        {
            GameObject xp = new GameObject("SeasonXpRow", typeof(RectTransform));
            xp.transform.SetParent(_canvasObj.transform, false);
            SetNorm(xp.GetComponent<RectTransform>(), 0.18f, 0.78f, 0.82f, 0.86f);

            Text xpLabel = UISharedFoundation.CreateText(xp.transform, "Label", "SEASON XP", UITextRole.Caption,
                TextAnchor.MiddleLeft, new Color(0.7f, 0.9f, 0.72f), true, new Vector2(160f, 24f));
            SetNorm(xpLabel.rectTransform, 0.00f, 0.15f, 0.16f, 0.85f);

            GameObject bar = new GameObject("XpBar", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(xp.transform, false);
            bar.GetComponent<Image>().color = new Color(0.18f, 0.55f, 0.32f, 0.85f);
            bar.GetComponent<Image>().raycastTarget = false;
            SetNorm(bar.GetComponent<RectTransform>(), 0.18f, 0.28f, 0.72f, 0.72f);

            Text values = UISharedFoundation.CreateText(xp.transform, "XpValues",
                MetagameShellProfileBinding.PassTierProgressLine(),
                UITextRole.Body, TextAnchor.MiddleRight, new Color(0.9f, 0.95f, 0.85f), true,
                new Vector2(280f, 28f));
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

            BuildTrackRow(table.transform, "FreeTrackRow", "FREE TRACK", premium: false, 0.52f, 0.98f);
            BuildTrackRow(table.transform, "PremiumTrackRow", "PREMIUM TRACK", premium: true, 0.02f, 0.48f);
        }

        private void BuildTrackRow(Transform parent, string rowName, string label, bool premium, float yMin, float yMax)
        {
            GameObject row = new GameObject(rowName, typeof(RectTransform));
            row.transform.SetParent(parent, false);
            SetNorm(row.GetComponent<RectTransform>(), 0f, yMin, 1f, yMax);

            Text labelText = UISharedFoundation.CreateText(row.transform, "TrackLabel", label, UITextRole.Title,
                TextAnchor.MiddleCenter, new Color(0.95f, 0.9f, 0.79f), true, new Vector2(180f, 40f));
            SetNorm(labelText.rectTransform, 0.00f, 0.15f, 0.14f, 0.85f);

            float wellWidth = 0.86f / BattlePassOpenValues.ShellTierWellCount;
            for (int i = 0; i < BattlePassOpenValues.ShellTierWellCount; i++)
            {
                int tier = i;
                bool capturedPremium = premium;
                float left = 0.14f + i * wellWidth;
                GameObject well = new GameObject($"TierWell_{i}", typeof(RectTransform), typeof(Image), typeof(Button));
                well.transform.SetParent(row.transform, false);
                Image img = well.GetComponent<Image>();
                UISharedFoundation.ApplyFramedPanel(img, null,
                    UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorHeader);
                Button btn = well.GetComponent<Button>();
                btn.targetGraphic = img;
                btn.onClick.AddListener(() => AttemptClaim(tier, capturedPremium));
                SetNorm(well.GetComponent<RectTransform>(), left, 0.08f, left + wellWidth * 0.92f, 0.92f);

                Text headerN = UISharedFoundation.CreateText(well.transform, "TierIndex", $"T{i + 1}", UITextRole.Caption,
                    TextAnchor.UpperCenter, new Color(0.8f, 0.85f, 0.7f), true, new Vector2(80f, 24f));
                SetNorm(headerN.rectTransform, 0.05f, 0.72f, 0.95f, 0.98f);

                Text amount = UISharedFoundation.CreateText(well.transform, "RewardAmount",
                    MetagameShellProfileBinding.PassSeasonXpLine(), UITextRole.Body, TextAnchor.MiddleCenter,
                    new Color(0.95f, 0.9f, 0.79f), true, new Vector2(100f, 28f));
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
            HomeV3UiLibrary.ApplyNeutralActionButton(unlock.GetComponent<Button>(), img,
                new Color(0.16f, 0.38f, 0.28f));
            unlock.GetComponent<Button>().onClick.AddListener(() =>
            {
                BattlePassClaimResult result = BattlePassOpenValues.TryUnlockPremium();
                SetStatus(result.Message);
            });
            SetNorm(unlock.GetComponent<RectTransform>(), 0.00f, 0.25f, 0.32f, 0.90f);
            UISharedFoundation.CreateText(unlock.transform, "Text", "UNLOCK PREMIUM", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(280f, 40f));

            Text access = UISharedFoundation.CreateText(bar.transform, "PremiumAccessCopy",
                $"Premium track · unlock price not set · {MetagameShellProfileBinding.WalletLine()}",
                UITextRole.Body, TextAnchor.MiddleLeft, new Color(0.9f, 0.88f, 0.75f), true, new Vector2(800f, 36f));
            SetNorm(access.rectTransform, 0.36f, 0.45f, 0.98f, 0.90f);

            _statusText = UISharedFoundation.CreateText(bar.transform, "StatusLine", BattlePassOpenValues.StatusNote,
                UITextRole.Caption, TextAnchor.MiddleLeft, new Color(0.85f, 0.75f, 0.5f), true, new Vector2(1600f, 28f));
            SetNorm(_statusText.rectTransform, 0.00f, 0.00f, 1f, 0.32f);
        }

        private static void CreateHeaderButton(Transform parent, string name, string label, Vector2 anchoredPos,
            Action onClick)
        {
            GameObject btnObj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            btnObj.transform.SetParent(parent, false);
            Image img = btnObj.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNavTileButton(btnObj.GetComponent<Button>(), img);
            img.color = new Color(0.3f, 0.2f, 0.2f);
            btnObj.GetComponent<Button>().onClick.AddListener(() => onClick?.Invoke());
            RectTransform rect = btnObj.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = anchoredPos;
            rect.sizeDelta = new Vector2(160f, 56f);
            UISharedFoundation.CreateText(btnObj.transform, "Text", label, UITextRole.Body, TextAnchor.MiddleCenter,
                Color.white, true, new Vector2(140f, 44f));
        }

        private void SetStatus(string message)
        {
            if (_statusText != null)
                _statusText.text = message ?? string.Empty;
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
