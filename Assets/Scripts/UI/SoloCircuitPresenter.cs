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
    /// POPUP over Empire - NOT a fullscreen swap. This was originally built fullscreen and is
    /// corrected here, before anything wired an entry point to it, on real evidence rather than
    /// preference:
    ///   - ST's locked framing (2026-08-26) puts the Circuit under "the Empire's War Room".
    ///   - The War Room entry already exists at EmpirePresenter.cs:596 and opens
    ///     TacticalPuzzlePresenter "as an overlay, leaving the Empire canvas underneath".
    ///   - CR reverted the identical fullscreen assumption on Guild Hall in 7185a4c, twice-burned.
    /// So the Circuit follows the same convention as EmpireBuildingDetail / GuildHallEntry /
    /// TacticalPuzzle: EmpireCanvas stays alive and findable below this screen.
    ///
    /// The practical consequence: this screen MUST NOT call CleanupStaleMetagameCanvases. Doing so
    /// destroys EmpireCanvas out from under a popup, which is exactly the bug CR spent a cycle
    /// reverting.
    ///
    /// RESOLVED 2026-08-26: "SoloCircuitCanvas" is now in
    /// CampaignMapPresenter.CleanupStaleMetagameCanvases' master list (CC-authorized one-time
    /// exception, same pattern as TacticalPuzzleCanvas) - any other screen can now clean up a
    /// stale Circuit canvas, matching every other popup in this family.
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

            // Deliberately NO CleanupStaleMetagameCanvases: this is a popup and EmpireCanvas must
            // survive underneath it. See the class header - calling it here is the exact regression
            // 7185a4c reverted on Guild Hall.

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 40;   // popup band, same as GuildHallEntryCanvas

            // Guaranteed-opaque base before any art, and MORE important now that this is a popup:
            // EmpireCanvas is genuinely alive underneath, so any gap left by a preserveAspect
            // background would let it both show through AND stay clickable. That was CR's real root
            // cause on Guild Hall (7185a4c). An opaque dimmer makes it impossible at any aspect.
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
                header, "Title", "COMMAND CIRCUIT", UITextRole.Title, TextAnchor.MiddleCenter,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(900f, 60f));

            // ST's locked framing (2026-08-26). Institutionally attributed to the War Room -
            // deliberately avatar-less, so no new speaker or portrait is needed.
            UISharedFoundation.CreateText(
                header, "Framing",
                "The Empire's War Room sets three daily trials to sharpen formation, judgement, "
                + "and command of the available ranks.",
                UITextRole.Caption, TextAnchor.MiddleCenter,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(1500f, 40f));

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

            // Titles and subtitles are ST's locked copy, not invented here.
            BuildTrialRow(body.transform, SoloCircuitTrial.Formation,
                "ORDER THE RANKS", "Victory begins with where each force stands.");
            BuildTrialRow(body.transform, SoloCircuitTrial.Collection,
                "MUSTER THE RANKS", "A capable commander understands every force available.");
            BuildTrialRow(body.transform, SoloCircuitTrial.TacticalBrief,
                "READ THE FIELD", "Study the position before issuing the decisive order.");
            BuildCycleRow(body.transform);
        }

        private void BuildTrialRow(Transform parent, SoloCircuitTrial trial, string label, string flavour)
        {
            RectTransform card = UISharedFoundation.CreateCardPrimitive(
                parent, "Trial_" + trial, new Vector2(1400f, 180f), UIFrozenTokens.ColorPanel);

            UISharedFoundation.CreateText(
                card, "Label", label, UITextRole.Title, TextAnchor.UpperLeft,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(1300f, 44f));

            UISharedFoundation.CreateText(
                card, "Flavour", flavour, UITextRole.Caption, TextAnchor.MiddleLeft,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(1300f, 36f));

            // The RULE stays separate from the flavour line and is generated from the same
            // deterministic seed that scores the trial - copy must never drift from what is
            // actually being judged.
            UISharedFoundation.CreateText(
                card, "Status", DescribeTrial(trial), UITextRole.Body, TextAnchor.LowerLeft,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(1300f, 60f));

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
            // ST's locked clean/partial lines, chosen from real state rather than hardcoded.
            string summary = SoloCollectionCircuit.AllThreeClearedToday(progress)
                ? "Command Circuit complete. Every lesson has been carried into tomorrow's campaign."
                : "Part of today's circuit is secured. The remaining trials still await your command.";

            string text =
                summary + "\n7-DAY CYCLE   " + days + " / " +
                SoloCollectionCircuit.CircuitsRequiredForCycleBonus +
                "   -   a missed day starts the cycle over.";

            UISharedFoundation.CreateText(
                card, "CycleText", text, UITextRole.Body, TextAnchor.MiddleCenter,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(1340f, 100f));
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
