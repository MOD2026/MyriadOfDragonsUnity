using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Metagame;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Guild Expedition shell wired to <see cref="IGuildExpeditionGateway"/>.
    /// Objective ids / milestone thresholds mirror the deployed CloudCode scaffold catalog
    /// (StaticExpeditionManifest + §5.1 bands) — not a tuned production week manifest.
    /// </summary>
    public class GuildExpeditionPresenter : MonoBehaviour
    {
        public const string CanvasName = "GuildExpeditionCanvas";

        /// <summary>Server scaffold objective ids (CloudCode StaticExpeditionManifest).</summary>
        public static readonly string[] ScaffoldObjectiveIds =
        {
            "scout.revealEnemyDeck",
            "scout.winWithInfoHandicap",
            "scout.defeatMarkedTarget",
            "supply.cooperativeDelivery",
            "supply.protectFormation",
            "assault.clearFormation",
            "assault.defeatObstacle",
            "builder.focusComplete",
            "raid.phaseOne",
            "raid.phaseTwo",
            "raid.phaseThree",
        };

        /// <summary>Player-facing objective labels (ST-GUILD-EXPEDITION-COPY-001 / WH-UI-GUILD-EXPEDITION-COPY-001).
        /// Index-aligned with <see cref="ScaffoldObjectiveIds"/> — ids unchanged.</summary>
        public static readonly string[] PlayerFacingObjectiveLabels =
        {
            "Enemy Deck Revealed",
            "Victory Without Full Intelligence [PROVISIONAL - OPEN]",
            "Marked Target Defeated",
            "Joint Supply Delivery [PROVISIONAL - OPEN]",
            "Formation Protected",
            "Enemy Formation Cleared",
            "Obstacle Defeated [PROVISIONAL - OPEN]",
            "Focus Completed [PROVISIONAL - OPEN]",
            "Raid: First Phase",
            "Raid: Second Phase",
            "Raid: Final Phase",
        };

        /// <summary>Server §5.1 personal milestone bands (Guild Contribution only).</summary>
        public static readonly int[] MilestoneThresholds = { 100, 250, 400, 700, 1000 };

        /// <summary>Player-facing milestone labels — index-aligned with <see cref="MilestoneThresholds"/>.</summary>
        public static readonly string[] PlayerFacingMilestoneLabels =
        {
            "First Milestone",
            "Second Milestone",
            "Third Milestone",
            "Fourth Milestone",
            "Final Milestone",
        };

        private GameObject _canvasObj;
        private Action _onBack;
        private Text _statusText;
        private Text _detailsText;
        private IGuildExpeditionGateway _gateway;
        private CancellationTokenSource _cts;
        private string _selectedObjectiveId;
        private int _selectedObjectiveIndex;
        private int _selectedMilestone = 100;
        private bool _busy;
        private readonly HashSet<string> _completedObjectives = new HashSet<string>();
        private Transform _objectiveGrid;
        private Transform _milestoneStrip;
        private Text _objectiveSelectorLabel;
        private bool _actionsGateOpen;

        public GameObject CanvasObjectForTests => _canvasObj;
        public string StatusTextForTests => _statusText != null ? _statusText.text : null;
        public string SelectedObjectiveIdForTests => _selectedObjectiveId;
        public int SelectedMilestoneForTests => _selectedMilestone;

        public void Initialize(Action onBack, IGuildExpeditionGateway gateway = null)
        {
            _onBack = onBack;
            _gateway = gateway ?? new UnityCloudCodeGuildExpeditionGateway();
            // Deferred-feature gate (real bug fix, CC 2026-08-27): unlike Guild Hall/Mail, this
            // presenter's default gateway is a REAL, deployed CloudCode module
            // (nonprod-validation - see GuildExpeditionGateway.cs), so a real player reaching
            // this screen previously got raw backend failure text ("Consume failed: unknown")
            // instead of an honest "not live yet" message. Same refuse-before-any-real-call
            // pattern as GuildHallEntryOpenValues. An explicitly injected gateway (tests, the
            // UiScreenRegistry geometry harness never presses these buttons) is a deliberate
            // wiring check, not a live player path, and bypasses the gate - the only production
            // call site (GuildHallEntryPresenter.OpenGuildExpedition) never injects one.
            _actionsGateOpen = gateway != null || GuildExpeditionOpenValues.AreActionsConfigured;
            _selectedObjectiveIndex = 0;
            _selectedObjectiveId = ScaffoldObjectiveIds[0];
            _selectedMilestone = MilestoneThresholds[0];
            BuildUI();
        }

        public Task<GuildExpeditionAttemptResult> ConsumeAttemptForTests() => ConsumeAttemptAsync();

        public Task<GuildExpeditionObjectiveResult> SubmitSelectedObjectiveForTests() =>
            SubmitObjectiveAsync(_selectedObjectiveId);

        public Task<GuildExpeditionMilestoneResult> ClaimSelectedMilestoneForTests() =>
            ClaimMilestoneAsync(_selectedMilestone);

        private void BuildUI()
        {
            TeardownUI();
            _cts = new CancellationTokenSource();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 41;

            Color shellFallback = new Color(0.07f, 0.09f, 0.11f, 1f);
            GameObject backing = new GameObject("BackgroundBacking", typeof(RectTransform), typeof(Image));
            backing.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(backing.GetComponent<RectTransform>());
            Image backingImg = backing.GetComponent<Image>();
            backingImg.color = new Color(shellFallback.r, shellFallback.g, shellFallback.b, 1f);
            backingImg.raycastTarget = false;

            GameObject bg = new GameObject("Background", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(bg.GetComponent<RectTransform>());
            GuildExpeditionUiLibrary.ApplyFullscreenShell(bg.GetComponent<Image>(), shellFallback);

            BuildHeader();
            BuildBody();
            RefreshDetails();
        }

        private void BuildHeader()
        {
            GameObject topBar = new GameObject("GuildExpeditionHeader", typeof(RectTransform), typeof(Image));
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

            Text title = UISharedFoundation.CreateText(topBar.transform, "Title", "GUILD EXPEDITION",
                UITextRole.Display, TextAnchor.MiddleCenter, Color.white, true,
                new Vector2(720f, 48f));
            title.fontSize = 28;
            title.fontStyle = FontStyle.Bold;
            // Header bands separated (WH-UI-GUILD-EXPEDITION-HEADER-001): prior Title
            // (0.22-0.78) overlapped StatusLine (0.72-0.98) by ~115px — Friends/Chat pattern.
            SetNorm(title.rectTransform, 0.22f, 0.15f, 0.70f, 0.90f);

            _statusText = UISharedFoundation.CreateText(topBar.transform, "StatusLine",
                _actionsGateOpen ? "Ready." : GuildExpeditionOpenValues.PlayerStatus,
                UITextRole.Caption, TextAnchor.MiddleRight, Color.white, true,
                new Vector2(420f, 40f));
            _statusText.fontSize = 22;
            _statusText.fontStyle = FontStyle.Bold;
            SetNorm(_statusText.rectTransform, 0.72f, 0.10f, 0.98f, 0.90f);
        }

        private void BuildBody()
        {
            GameObject panel = new GameObject("ExpeditionPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(_canvasObj.transform, false);
            SetNorm(panel.GetComponent<RectTransform>(), 0.05f, 0.08f, 0.95f, 0.88f);
            Image panelImg = panel.GetComponent<Image>();
            UISharedFoundation.ApplyFramedPanel(panelImg, null,
                UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorBackground);

            _detailsText = UISharedFoundation.CreateText(panel.transform, "Details", string.Empty,
                UITextRole.Body, TextAnchor.UpperLeft, Color.white, true, new Vector2(900f, 120f));
            _detailsText.fontSize = 22;
            _detailsText.fontStyle = FontStyle.Bold;
            SetNorm(_detailsText.rectTransform, 0.02f, 0.78f, 0.98f, 0.98f);

            // Conservative IA (WH-UI-GUILD-EXPEDITION-CONSERVATIVE-IA-001): one Objective Selector
            // replaces 11 Objective_* buttons. Objective_0..10 remain as passive selectable entries
            // (stage/state displays) driven by the selector — no Btn_Prev/Next/Abandon/Finalize.
            CreateActionButton(panel.transform, "Btn_ObjectiveSelector", "OBJECTIVE", 0.02f, 0.68f, 0.62f, 0.76f,
                CycleObjectiveSelection);
            Transform selectorTf = panel.transform.Find("Btn_ObjectiveSelector");
            _objectiveSelectorLabel = selectorTf != null
                ? selectorTf.Find("Text")?.GetComponent<Text>()
                : null;

            GameObject objGrid = new GameObject("ObjectiveGrid", typeof(RectTransform));
            objGrid.transform.SetParent(panel.transform, false);
            _objectiveGrid = objGrid.transform;
            SetNorm(objGrid.GetComponent<RectTransform>(), 0.02f, 0.28f, 0.62f, 0.66f);
            int cols = 3;
            int rows = 4;
            for (int i = 0; i < ScaffoldObjectiveIds.Length; i++)
            {
                int col = i % cols;
                int row = i / cols;
                float cw = 1f / cols;
                float rh = 1f / rows;
                // Passive entry (Image only) — selection is via Btn_ObjectiveSelector.
                GameObject well = new GameObject($"Objective_{i}", typeof(RectTransform), typeof(Image));
                well.transform.SetParent(objGrid.transform, false);
                Image img = well.GetComponent<Image>();
                img.sprite = null;
                img.color = new Color(0.16f, 0.22f, 0.28f, 0.55f);
                img.raycastTarget = false;
                SetNorm(well.GetComponent<RectTransform>(),
                    col * cw + 0.01f, 1f - (row + 1) * rh + 0.02f,
                    (col + 1) * cw - 0.01f, 1f - row * rh - 0.02f);
                GuildExpeditionUiLibrary.ApplyStageIcon(well.transform, "StageIcon",
                    GuildExpeditionUiLibrary.StageState.Available, 0.08f, 0.38f, 0.92f, 0.94f);
                Text label = UISharedFoundation.CreateText(well.transform, "Label", PlayerFacingObjectiveLabels[i],
                    UITextRole.Caption, TextAnchor.MiddleCenter, Color.white, true, new Vector2(200f, 40f));
                label.fontSize = 22;
                label.fontStyle = FontStyle.Bold;
                label.raycastTarget = false;
                SetNorm(label.rectTransform, 0.02f, 0.02f, 0.98f, 0.36f);
            }
            RefreshObjectiveStageIcons();
            RefreshObjectiveSelectorLabel();

            GameObject mileStrip = new GameObject("MilestoneStrip", typeof(RectTransform));
            mileStrip.transform.SetParent(panel.transform, false);
            _milestoneStrip = mileStrip.transform;
            SetNorm(mileStrip.GetComponent<RectTransform>(), 0.64f, 0.28f, 0.98f, 0.76f);
            for (int i = 0; i < MilestoneThresholds.Length; i++)
            {
                int idx = i;
                float h = 1f / MilestoneThresholds.Length;
                int threshold = MilestoneThresholds[i];
                // Passive state display (WH-UI-GUILD-EXPEDITION-CONSERVATIVE-IA-001): not a
                // player-facing action. Button retained only so shell tests can Invoke selection;
                // interactable/raycast off. Claim action is exclusively Btn_ClaimMilestone.
                GameObject chip = new GameObject($"Milestone_{threshold}", typeof(RectTransform), typeof(Image), typeof(Button));
                chip.transform.SetParent(mileStrip.transform, false);
                Image img = chip.GetComponent<Image>();
                img.sprite = null;
                img.raycastTarget = false;
                Button chipBtn = chip.GetComponent<Button>();
                chipBtn.transition = Selectable.Transition.None;
                chipBtn.targetGraphic = img;
                chipBtn.interactable = false;
                chipBtn.onClick.AddListener(() =>
                {
                    _selectedMilestone = MilestoneThresholds[idx];
                    RefreshDetails();
                    RefreshMilestoneDisplays();
                    SetStatus($"Milestone: {PlayerFacingMilestoneLabels[idx]}");
                });
                SetNorm(chip.GetComponent<RectTransform>(), 0.05f, 1f - (i + 1) * h + 0.02f, 0.95f, 1f - i * h - 0.02f);
                Text chipLabel = UISharedFoundation.CreateText(chip.transform, "Label", PlayerFacingMilestoneLabels[i],
                    UITextRole.Caption, TextAnchor.MiddleCenter, Color.white, true, new Vector2(180f, 36f));
                chipLabel.fontSize = 22;
                chipLabel.fontStyle = FontStyle.Bold;
                chipLabel.raycastTarget = false;
                UISharedFoundation.StretchFull(chipLabel.rectTransform);
            }
            RefreshMilestoneDisplays();

            CreateActionButton(panel.transform, "Btn_ConsumeAttempt", "CONSUME ATTEMPT", 0.02f, 0.04f, 0.32f, 0.22f,
                () => _ = ConsumeAttemptAsync());
            CreateActionButton(panel.transform, "Btn_SubmitObjective", "SUBMIT OBJECTIVE", 0.35f, 0.04f, 0.65f, 0.22f,
                () => _ = SubmitObjectiveAsync(_selectedObjectiveId));
            CreateActionButton(panel.transform, "Btn_ClaimMilestone", "CLAIM MILESTONE", 0.68f, 0.04f, 0.98f, 0.22f,
                () => _ = ClaimMilestoneAsync(_selectedMilestone), primary: true);
        }

        private void CycleObjectiveSelection()
        {
            _selectedObjectiveIndex = (_selectedObjectiveIndex + 1) % ScaffoldObjectiveIds.Length;
            _selectedObjectiveId = ScaffoldObjectiveIds[_selectedObjectiveIndex];
            RefreshDetails();
            RefreshObjectiveStageIcons();
            RefreshObjectiveSelectorLabel();
            SetStatus($"Objective: {PlayerFacingObjectiveLabels[_selectedObjectiveIndex]}");
        }

        private void RefreshObjectiveSelectorLabel()
        {
            if (_objectiveSelectorLabel == null) return;
            _objectiveSelectorLabel.text = PlayerFacingObjectiveLabels[_selectedObjectiveIndex];
        }

        private void RefreshMilestoneDisplays()
        {
            if (_milestoneStrip == null) return;
            for (int i = 0; i < MilestoneThresholds.Length; i++)
            {
                int threshold = MilestoneThresholds[i];
                Transform chip = _milestoneStrip.Find($"Milestone_{threshold}");
                Image img = chip != null ? chip.GetComponent<Image>() : null;
                if (img == null) continue;
                bool selected = threshold == _selectedMilestone;
                img.color = selected
                    ? new Color(0.32f, 0.42f, 0.24f, 0.95f)
                    : new Color(0.18f, 0.22f, 0.16f, 0.75f);
            }
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

        private async Task<GuildExpeditionAttemptResult> ConsumeAttemptAsync()
        {
            if (!_actionsGateOpen)
            {
                SetStatus(GuildExpeditionOpenValues.PlayerStatus);
                return new GuildExpeditionAttemptResult { errorCode = "NOT_LIVE" };
            }
            if (!BeginBusy("Consuming attempt…"))
                return new GuildExpeditionAttemptResult { errorCode = "BUSY" };
            try
            {
                GuildExpeditionAttemptResult result = await _gateway.ConsumeAttemptAsync(Token).ConfigureAwait(true);
                if (result == null)
                {
                    SetStatus("Consume: null response.");
                    return new GuildExpeditionAttemptResult { errorCode = "NULL_RESPONSE" };
                }

                if (result.success)
                    SetStatus($"Attempt consumed. Remaining={result.remaining}");
                else
                    SetStatus($"Consume failed: {result.errorCode ?? "unknown"}");
                SetDetails($"ConsumeAttempt success={result.success} remaining={result.remaining} error={result.errorCode}");
                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"Consume failed: {ex.Message}");
                return new GuildExpeditionAttemptResult { errorCode = "CLIENT_EXCEPTION" };
            }
            finally
            {
                EndBusy();
            }
        }

        private async Task<GuildExpeditionObjectiveResult> SubmitObjectiveAsync(string objectiveId)
        {
            if (!_actionsGateOpen)
            {
                SetStatus(GuildExpeditionOpenValues.PlayerStatus);
                return new GuildExpeditionObjectiveResult { errorCode = "NOT_LIVE" };
            }
            if (!BeginBusy("Submitting objective…"))
                return new GuildExpeditionObjectiveResult { errorCode = "BUSY" };
            try
            {
                GuildExpeditionObjectiveResult result =
                    await _gateway.SubmitObjectiveResultAsync(objectiveId, Token).ConfigureAwait(true);
                if (result == null)
                {
                    SetStatus("Submit: null response.");
                    return new GuildExpeditionObjectiveResult { errorCode = "NULL_RESPONSE" };
                }

                if (result.success)
                {
                    _completedObjectives.Add(objectiveId);
                    RefreshObjectiveStageIcons();
                    SetStatus($"Scored +{result.pointsAwarded} (total {result.totalPoints})");
                }
                else
                    SetStatus($"Submit failed: {result.errorCode ?? "unknown"}");
                SetDetails(
                    $"SubmitObjective id={objectiveId}\n" +
                    $"success={result.success} points={result.pointsAwarded} total={result.totalPoints} " +
                    $"alreadyScored={result.alreadyScored} week={result.weekKey} error={result.errorCode}");
                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"Submit failed: {ex.Message}");
                return new GuildExpeditionObjectiveResult { errorCode = "CLIENT_EXCEPTION" };
            }
            finally
            {
                EndBusy();
            }
        }

        private async Task<GuildExpeditionMilestoneResult> ClaimMilestoneAsync(int threshold)
        {
            if (!_actionsGateOpen)
            {
                SetStatus(GuildExpeditionOpenValues.PlayerStatus);
                return new GuildExpeditionMilestoneResult { errorCode = "NOT_LIVE" };
            }
            if (!BeginBusy($"Claiming milestone {threshold}…"))
                return new GuildExpeditionMilestoneResult { errorCode = "BUSY" };
            try
            {
                GuildExpeditionMilestoneResult result =
                    await _gateway.ClaimMilestoneAsync(threshold, Token).ConfigureAwait(true);
                if (result == null)
                {
                    SetStatus("Claim: null response.");
                    return new GuildExpeditionMilestoneResult { errorCode = "NULL_RESPONSE" };
                }

                if (result.success)
                {
                    SetStatus($"Claimed {threshold}: +{result.guildContributionGranted} GC");
                    // Advance selected band after a successful claim so players can walk the
                    // passive milestone strip without a separate selector (no Btn_Prev/Next).
                    int idx = Array.IndexOf(MilestoneThresholds, threshold);
                    if (idx >= 0 && idx + 1 < MilestoneThresholds.Length)
                        _selectedMilestone = MilestoneThresholds[idx + 1];
                    RefreshMilestoneDisplays();
                    RefreshDetails();
                }
                else
                    SetStatus($"Claim failed: {result.errorCode ?? "unknown"}");
                SetDetails(
                    $"ClaimMilestone threshold={threshold}\n" +
                    $"success={result.success} granted={result.guildContributionGranted} " +
                    $"alreadyClaimed={result.alreadyClaimed} error={result.errorCode}");
                return result;
            }
            catch (Exception ex)
            {
                SetStatus($"Claim failed: {ex.Message}");
                return new GuildExpeditionMilestoneResult { errorCode = "CLIENT_EXCEPTION" };
            }
            finally
            {
                EndBusy();
            }
        }

        private void RefreshObjectiveStageIcons()
        {
            if (_objectiveGrid == null) return;
            for (int i = 0; i < ScaffoldObjectiveIds.Length; i++)
            {
                Transform well = _objectiveGrid.Find($"Objective_{i}");
                if (well == null) continue;
                Image wellImg = well.GetComponent<Image>();
                if (wellImg != null)
                {
                    bool selected = i == _selectedObjectiveIndex;
                    wellImg.color = selected
                        ? new Color(0.22f, 0.36f, 0.48f, 0.85f)
                        : new Color(0.16f, 0.22f, 0.28f, 0.55f);
                }
                Image icon = well.Find("StageIcon")?.GetComponent<Image>();
                if (icon == null) continue;
                string id = ScaffoldObjectiveIds[i];
                GuildExpeditionUiLibrary.StageState state = _completedObjectives.Contains(id)
                    ? GuildExpeditionUiLibrary.StageState.Completed
                    : GuildExpeditionUiLibrary.StageState.Available;
                icon.sprite = GuildExpeditionUiLibrary.LoadStageState(state);
            }
        }

        private void RefreshDetails()
        {
            SetDetails(
                $"Selected objective: {PlayerFacingObjectiveLabels[_selectedObjectiveIndex]}\n" +
                $"Selected milestone: {MilestoneLabelForThreshold(_selectedMilestone)}\n" +
                "Catalog = CloudCode StaticExpeditionManifest scaffold (not a tuned week).");
        }

        private static string MilestoneLabelForThreshold(int threshold)
        {
            int idx = Array.IndexOf(MilestoneThresholds, threshold);
            if (idx >= 0 && idx < PlayerFacingMilestoneLabels.Length)
                return PlayerFacingMilestoneLabels[idx];
            return threshold.ToString();
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
