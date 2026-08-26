using System;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Solo Collection Circuit - the player-reachable screen for the three daily trials.
    ///
    /// FULLSCREEN, not a popup. That distinction has already caused one real regression on this
    /// project (the Guild Hall overlap), so it is stated rather than left to be inferred: this is a
    /// destination the player navigates TO, so it calls CleanupStaleMetagameCanvases and expects
    /// nothing to remain underneath. A popup (EmpireBuildingDetail, GuildHallEntry) deliberately
    /// does the opposite and leaves EmpireCanvas alive below it.
    ///
    /// KNOWN GAP, FLAGGED NOT HIDDEN: "SoloCircuitCanvas" is NOT yet in
    /// CampaignMapPresenter.CleanupStaleMetagameCanvases' master list, because that file belongs to
    /// the metagame seat. Until it is added, OTHER screens cannot clean up a stale Circuit canvas -
    /// exactly the bug class that left TacticalPuzzleCanvas orphaned and is the leading suspect for
    /// the Mail-screen freeze. This screen cleans up after itself on close, so the gap is narrow,
    /// but it is real and one line fixes it.
    ///
    /// All real logic lives in SoloCollectionCircuit / SoloCircuitDailySeed /
    /// SoloCircuitCollectionRule as plain testable classes. This MonoBehaviour supplies timing and
    /// pixels only, per CLAUDE.md non-negotiable #6.
    /// </summary>
    public class SoloCircuitPresenter : MonoBehaviour
    {
        public const string CanvasName = "SoloCircuitCanvas";

        private GameObject _canvasObj;
        private Action _onBack;
        private PlayerProfile _profile;
        private DateTime _nowUtc;

        public GameObject CanvasObjectForTests => _canvasObj;

        /// <summary>
        /// nowUtc is injected rather than read from DateTime.UtcNow so an EditMode test can drive a
        /// day boundary, a completed cycle, or a clock rollback without touching the machine clock -
        /// the same reason every entry point in SoloCollectionCircuit takes it.
        /// </summary>
        public void Initialize(PlayerProfile profile, DateTime nowUtc, Action onBack)
        {
            _profile = profile;
            _nowUtc = nowUtc;
            _onBack = onBack;
            BuildUI();
        }

        public void PressBackForTests() => Close();

        /// <summary>EditMode cannot click a real Button - drives the same handler a tap would.</summary>
        public void PressTrialForTests(SoloCircuitTrial trial) => AttemptTrial(trial);

        public string TrialStatusForTests(SoloCircuitTrial trial) => DescribeTrial(trial);

        private SoloCircuitProgress Progress =>
            _profile != null ? _profile.soloCircuitProgress : null;

        private void BuildUI()
        {
            TeardownUI();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 30;

            // Guaranteed-opaque base before any art. The Guild Hall bug was a preserveAspect
            // background letterboxing and letting the screen underneath both show through AND stay
            // clickable; an opaque dimmer first makes that impossible regardless of art aspect.
            GameObject dim = new GameObject("Dimmer", typeof(RectTransform), typeof(Image));
            dim.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(dim.GetComponent<RectTransform>());
            Image dimImg = dim.GetComponent<Image>();
            dimImg.color = UIFrozenTokens.ColorBackground;
            dimImg.raycastTarget = true;

            BuildHeader();
            BuildTrialRows();
        }

        private void BuildHeader()
        {
            RectTransform header = UISharedFoundation.CreateHeaderShell(
                _canvasObj.transform, "SoloCircuitHeader", 140f, null, UIFrozenTokens.ColorHeader);

            UISharedFoundation.CreateText(
                header, "Title", "DAILY CIRCUIT", UITextRole.Title, TextAnchor.MiddleCenter,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(900f, 60f));

            UISharedFoundation.CreateButton(
                header, "Btn_Back", "BACK", new Vector2(180f, 70f),
                UIFrozenTokens.ColorPanel, Close, null, true);
        }

        private void BuildTrialRows()
        {
            GameObject body = new GameObject("TrialList", typeof(RectTransform));
            body.transform.SetParent(_canvasObj.transform, false);
            RectTransform bodyRect = body.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0.08f, 0.12f);
            bodyRect.anchorMax = new Vector2(0.92f, 0.86f);
            bodyRect.offsetMin = Vector2.zero;
            bodyRect.offsetMax = Vector2.zero;

            VerticalLayoutGroup layout = body.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 24f;
            layout.childControlHeight = true;
            layout.childForceExpandHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandWidth = true;

            BuildTrialRow(body.transform, SoloCircuitTrial.Formation, "FORMATION TRIAL");
            BuildTrialRow(body.transform, SoloCircuitTrial.Collection, "COLLECTION TRIAL");
            BuildTrialRow(body.transform, SoloCircuitTrial.TacticalBrief, "TACTICAL BRIEF");
            BuildCycleRow(body.transform);
        }

        private void BuildTrialRow(Transform parent, SoloCircuitTrial trial, string label)
        {
            RectTransform card = UISharedFoundation.CreateCardPrimitive(
                parent, "Trial_" + trial, new Vector2(1400f, 180f), UIFrozenTokens.ColorPanel);

            UISharedFoundation.CreateText(
                card, "Label", label, UITextRole.Title, TextAnchor.UpperLeft,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(1300f, 44f));

            UISharedFoundation.CreateText(
                card, "Status", DescribeTrial(trial), UITextRole.Body, TextAnchor.LowerLeft,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(1300f, 90f));

            UISharedFoundation.CreateButton(
                card, "Btn_Play", IsCleared(trial) ? "CLEARED" : "PLAY", new Vector2(220f, 72f),
                IsCleared(trial) ? UIFrozenTokens.ColorHeader : UIFrozenTokens.ColorAccentEmerald,
                () => AttemptTrial(trial), null, true);
        }

        private void BuildCycleRow(Transform parent)
        {
            SoloCircuitProgress progress = Progress;
            int days = progress != null ? progress.circuitDaysInCycle : 0;

            RectTransform card = UISharedFoundation.CreateCardPrimitive(
                parent, "CycleRow", new Vector2(1400f, 120f), UIFrozenTokens.ColorHeader);

            // Says explicitly that a missed day ends the streak. The rule is unforgiving and a
            // player who is not told will read a reset as a bug - the same reason the mid-week gap
            // was worth surfacing in the first place.
            string text =
                "7-DAY CYCLE   " + days + " / " + SoloCollectionCircuit.CircuitsRequiredForCycleBonus +
                "   -   clear all three trials every day; a missed day starts the cycle over.";

            UISharedFoundation.CreateText(
                card, "CycleText", text, UITextRole.Body, TextAnchor.MiddleCenter,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(1340f, 60f));
        }

        private bool IsCleared(SoloCircuitTrial trial) =>
            SoloCollectionCircuit.IsTrialClearedToday(Progress, trial);

        private string DescribeTrial(SoloCircuitTrial trial)
        {
            if (Progress == null) return "Unavailable - no profile loaded.";

            string dayKey = SoloCollectionCircuit.UtcDayKey(_nowUtc);
            if (IsCleared(trial)) return "Cleared today. Come back tomorrow.";

            switch (trial)
            {
                case SoloCircuitTrial.Formation:
                    return SoloCircuitDailySeed.FormationRestrictionFor(dayKey);
                case SoloCircuitTrial.Collection:
                    return SoloCircuitCollectionRule.BandFor(dayKey).Describe();
                case SoloCircuitTrial.TacticalBrief:
                    return "Complete today's tactical puzzle.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Records a clear and rebuilds.
        ///
        /// The presenter deliberately does NOT decide whether the player actually met the trial's
        /// condition - that is the battle/puzzle result, and inventing it here would make the
        /// rewards claimable by opening a screen. Wiring the real completion signal is the next
        /// task; until then this is the single funnel every completion will route through, so the
        /// grant path and its caps are already exercised.
        /// </summary>
        private void AttemptTrial(SoloCircuitTrial trial)
        {
            if (Progress == null) return;

            SoloCollectionCircuit.RecordClear(Progress, trial, _nowUtc);
            SaveSystem.Save(_profile);
            BuildUI();
        }

        private void Close()
        {
            TeardownUI();
            _onBack?.Invoke();
        }

        private void TeardownUI()
        {
            if (_canvasObj == null) return;
            DestroyImmediate(_canvasObj);
            _canvasObj = null;
        }

        private void OnDestroy() => TeardownUI();
    }
}
