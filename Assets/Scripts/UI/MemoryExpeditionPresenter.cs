using System;
using System.Collections.Generic;
using MyriadOfDragons.Data;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Memory Expedition play screen — 3-round match grid wired to
    /// <see cref="MemoryExpeditionService"/> / <see cref="MemoryExpedition"/>.
    /// </summary>
    public class MemoryExpeditionPresenter : MonoBehaviour
    {
        public const string CanvasName = "MemoryExpeditionCanvas";

        private GameObject _canvasObj;
        private Action _onBack;
        private Text _statusText;
        private Text _roundText;
        private MemoryExpeditionState _state;
        private readonly List<Text> _tileLabels = new List<Text>();
        private DateTime _utcNowForTests = DateTime.MinValue;

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;
        public MemoryExpeditionState StateForTests => _state;
        public int TileCountForTests => _tileLabels.Count;

        public void Initialize(Action onBack)
        {
            _onBack = onBack;
            BuildUI();
        }

        /// <summary>Inject UTC for EditMode (mirrors Daily Login test seams).</summary>
        public void SetUtcNowForTests(DateTime utcNow) => _utcNowForTests = utcNow;

        public MemoryExpeditionTapResult TapTileForTests(int tileIndex) => TapTile(tileIndex);

        public MemoryExpeditionClaimResult ClaimForTests() => ClaimRewards();

        private DateTime NowUtc() =>
            _utcNowForTests == DateTime.MinValue ? DateTime.UtcNow : _utcNowForTests;

        private void BuildUI()
        {
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            PlayerProfile profile = SaveManager.SaveData;
            _state = MemoryExpeditionService.EnsureRun(profile, NowUtc());
            if (profile != null)
                SaveManager.Save();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 12;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(bg.GetComponent<RectTransform>());
            MemoryExpeditionUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), new Color(0.08f, 0.09f, 0.12f));

            BuildHeader();
            BuildHud();
            BuildGrid();
            BuildClaimBar();
            RefreshHud();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("MemoryExpeditionHeader", typeof(RectTransform), typeof(Image));
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
            backBtn.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier3Utility;

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "MEMORY EXPEDITION",
                UITextRole.Display, TextAnchor.MiddleCenter, Color.white, true,
                new Vector2(640f, 48f));
            title.fontSize = 30;
            title.fontStyle = FontStyle.Bold;
            SetNorm(title.rectTransform, 0.28f, 0.15f, 0.72f, 0.9f);

            Text wallet = UISharedFoundation.CreateText(topBar.transform, "WalletLine",
                MetagameShellProfileBinding.WalletLine(), UITextRole.Caption, TextAnchor.MiddleLeft,
                Color.white, true, new Vector2(420f, 28f));
            UISharedFoundation.ApplyTextShadow(wallet);
            SetNorm(wallet.rectTransform, 0.16f, 0.12f, 0.48f, 0.88f);
            UISharedFoundation.AddLocalGradientScrim(
                topBar.transform, new Vector2(1920f * 0.32f, 40f), new Vector2(520f, 64f),
                UISharedFoundation.GradientDirection.TopToBottom, 0.9f);
            UISharedFoundation.AddLocalGradientScrim(
                topBar.transform, new Vector2(1920f * 0.32f, 40f), new Vector2(520f, 64f),
                UISharedFoundation.GradientDirection.BottomToTop, 0.9f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine", string.Empty,
                UITextRole.Caption, TextAnchor.MiddleRight, Color.white, true,
                new Vector2(520f, 40f));
            _statusText.fontSize = 22;
            _statusText.fontStyle = FontStyle.Bold;
            SetNorm(_statusText.rectTransform, 0.72f, 0.1f, 0.98f, 0.9f);
        }

        private void BuildHud()
        {
            GameObject hud = new GameObject("RunHud", typeof(RectTransform));
            hud.transform.SetParent(_canvasObj.transform, false);
            SetNorm(hud.GetComponent<RectTransform>(), 0.2f, 0.84f, 0.8f, 0.90f);

            _roundText = UISharedFoundation.CreateText(hud.transform, "RoundLine", string.Empty,
                UITextRole.Body, TextAnchor.MiddleCenter, Color.white, true,
                new Vector2(700f, 36f));
            _roundText.fontSize = 22;
            _roundText.fontStyle = FontStyle.Bold;
            SetNorm(_roundText.rectTransform, 0f, 0f, 1f, 1f);
        }

        private void BuildGrid()
        {
            _tileLabels.Clear();
            Transform old = _canvasObj.transform.Find("TileGrid");
            if (old != null)
            {
                if (Application.isPlaying) Destroy(old.gameObject);
                else DestroyImmediate(old.gameObject);
            }

            if (_state == null) return;
            MemoryExpeditionRoundRules rules = MemoryExpedition.RulesForRound(_state.CurrentRound);
            if (rules == null) return;

            GameObject grid = new GameObject("TileGrid", typeof(RectTransform));
            grid.transform.SetParent(_canvasObj.transform, false);
            SetNorm(grid.GetComponent<RectTransform>(), 0.18f, 0.18f, 0.82f, 0.82f);

            int rows = rules.Rows;
            int cols = rules.Columns;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    int tileIndex = r * cols + c;
                    float left = (float)c / cols;
                    float right = (float)(c + 1) / cols;
                    float top = 1f - (float)r / rows;
                    float bottom = 1f - (float)(r + 1) / rows;

                    GameObject tile = new GameObject($"Tile_{tileIndex}", typeof(RectTransform), typeof(Image), typeof(Button));
                    tile.transform.SetParent(grid.transform, false);
                    Image img = tile.GetComponent<Image>();
                    // Not ApplyFramedPanel: this is a near-square memory-match tile, not a wide
                    // row - the real ListRow/ContentPanel art's border insets are proportioned
                    // for a wide/thin or large shape and would deform below the manifest's own
                    // minimum-rect floors at this tile's real size. Token color only.
                    img.color = UIFrozenTokens.ColorPanel;
                    Button btn = tile.GetComponent<Button>();
                    btn.targetGraphic = img;
                    // None, not Unity's default ColorTint - no ApplyXActionButton helper is called
                    // for this tile (token color only, see comment above). Safe to cache a static
                    // base color here: RefreshTiles only ever mutates the child label's text, never
                    // this tile's own Image.color, so nothing external fights the controller for it.
                    btn.transition = Selectable.Transition.None;
                    tile.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier3Utility;
                    int captured = tileIndex;
                    btn.onClick.AddListener(() => TapTile(captured));
                    RectTransform tileRect = tile.GetComponent<RectTransform>();
                    SetNorm(tileRect, left + 0.01f, bottom + 0.01f, right - 0.01f, top - 0.01f);

                    // Real resolved size, not a formula assuming a fixed 1920x1080 canvas (CC
                    // 2026-08-27, "800x800 @ (200,200)" was found identical at three call sites -
                    // a scrim's parent must be measured, not assumed. An earlier attempt at this
                    // fix computed from a hardcoded 1920x1080 literal and was WRONG by exactly the
                    // canvas's own real-vs-assumed scale ratio - .rect is the actual resolved size
                    // right now, correct regardless of what the canvas turns out to be.
                    Vector2 tileSize = tileRect.rect.size;
                    UISharedFoundation.AddLocalGradientScrim(
                        tile.transform, tileSize * 0.5f, tileSize,
                        UISharedFoundation.GradientDirection.TopToBottom, 0.85f);
                    UISharedFoundation.AddLocalGradientScrim(
                        tile.transform, tileSize * 0.5f, tileSize,
                        UISharedFoundation.GradientDirection.BottomToTop, 0.85f);
                    Text label = UISharedFoundation.CreateText(tile.transform, "Face", "?",
                        UITextRole.Title, TextAnchor.MiddleCenter, Color.white, true,
                        new Vector2(80f, 80f));
                    label.fontSize = 28;
                    UISharedFoundation.ApplyTextShadow(label);
                    SetNorm(label.rectTransform, 0.1f, 0.1f, 0.9f, 0.9f);
                    _tileLabels.Add(label);
                }
            }

            RefreshTiles();
        }

        private void BuildClaimBar()
        {
            GameObject bar = new GameObject("ClaimBar", typeof(RectTransform));
            bar.transform.SetParent(_canvasObj.transform, false);
            SetNorm(bar.GetComponent<RectTransform>(), 0.3f, 0.04f, 0.7f, 0.14f);

            GameObject claim = new GameObject("Btn_Claim", typeof(RectTransform), typeof(Image), typeof(Button));
            claim.transform.SetParent(bar.transform, false);
            Image img = claim.GetComponent<Image>();
            HomeV3UiLibrary.ApplyNeutralActionButton(claim.GetComponent<Button>(), img, new Color(0.22f, 0.38f, 0.3f));
            claim.GetComponent<Button>().onClick.AddListener(() => ClaimRewards());
            SetNorm(claim.GetComponent<RectTransform>(), 0.1f, 0.15f, 0.9f, 0.85f);
            UISharedFoundation.CreateText(claim.transform, "Text", "CLAIM REWARDS", UITextRole.Body,
                TextAnchor.MiddleCenter, Color.white, true, new Vector2(320f, 40f));
            claim.AddComponent<InteractionStateController>().Tier = UIDesignTokens.FrameTier.Tier1Hero;
        }

        private MemoryExpeditionTapResult TapTile(int tileIndex)
        {
            PlayerProfile profile = SaveManager.SaveData;
            MemoryExpeditionTapResult result = MemoryExpeditionService.TapTile(profile, tileIndex, NowUtc());
            _state = profile?.ToMemoryExpeditionState();
            if (profile != null)
                SaveManager.Save();

            // Round advance rebuilds the grid (size may change).
            if (result.Status == MemoryExpeditionTapStatus.RoundCleared && !result.RunOver)
                BuildGrid();
            else
                RefreshTiles();

            RefreshHud();
            if (!string.IsNullOrEmpty(result.Message))
                SetStatus(result.Message);
            else
                SetStatus(StatusFor(result.Status));
            return result;
        }

        private MemoryExpeditionClaimResult ClaimRewards()
        {
            PlayerProfile profile = SaveManager.SaveData;
            MemoryExpeditionClaimResult result = MemoryExpeditionService.ClaimRewards(profile, NowUtc());
            _state = profile?.ToMemoryExpeditionState();
            if (profile != null)
                SaveManager.Save();
            RefreshHud();
            SetStatus(result.Message ?? result.Status.ToString());
            return result;
        }

        private void RefreshHud()
        {
            if (_roundText == null || _state == null) return;
            string fail = _state.RunFailed ? " · FAILED" : string.Empty;
            string claimed = _state.RewardClaimed ? " · CLAIMED" : string.Empty;
            _roundText.text =
                $"Round {_state.CurrentRound}/{MemoryExpedition.Rounds.Length} · Mistakes {_state.MistakesRemaining} · Cleared {_state.HighestRoundCleared}{fail}{claimed}";
        }

        private void RefreshTiles()
        {
            if (_state == null || _tileLabels.Count == 0) return;
            MemoryExpeditionRoundRules rules = MemoryExpedition.RulesForRound(_state.CurrentRound);
            if (rules == null) return;
            int[] layout = MemoryExpedition.LayoutFor(_state.Seed, _state.CurrentRound);

            for (int i = 0; i < _tileLabels.Count && i < rules.TileCount; i++)
            {
                Text label = _tileLabels[i];
                if (label == null) continue;
                bool revealed = MemoryExpedition.IsTileResolved(_state, i);
                bool selected = _state.FirstSelectedTile == i;
                if (revealed || selected)
                    label.text = layout[i].ToString();
                else
                    label.text = "?";
            }
        }

        private static string StatusFor(MemoryExpeditionTapStatus status) => status switch
        {
            MemoryExpeditionTapStatus.FirstTileSelected => "Pick a second tile.",
            MemoryExpeditionTapStatus.Matched => "Match!",
            MemoryExpeditionTapStatus.Mismatched => "Mismatch.",
            MemoryExpeditionTapStatus.RoundCleared => "Round cleared.",
            MemoryExpeditionTapStatus.RunFailed => "Run failed.",
            _ => status.ToString(),
        };

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
            _tileLabels.Clear();
            if (_canvasObj == null) return;
            if (Application.isPlaying) Destroy(_canvasObj);
            else DestroyImmediate(_canvasObj);
            _canvasObj = null;
        }

        private void OnDestroy() => TeardownUI();
    }
}
