using System;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
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

            // EVERY CHILD BELOW IS EXPLICITLY BANDED, and that is the whole fix for this screen.
            //
            // UISharedFoundation.CreateText sets ONLY sizeDelta - it never touches
            // anchorMin/anchorMax/anchoredPosition. So every child defaults to Unity's centre
            // anchor and lands stacked on the parent's centre point. The TextAnchor argument I was
            // passing (UpperLeft/MiddleLeft/...) aligns text INSIDE its own rect; it does not
            // position the rect. I read it as layout and it is not, which is why the BACK button
            // sat on top of the title and every trial's description printed over its own heading.
            Text title = UISharedFoundation.CreateText(
                header, "Title", "COMMAND CIRCUIT", UITextRole.Title, TextAnchor.MiddleCenter,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(900f, 46f));
            SetNorm(title.rectTransform, 0.22f, 0.52f, 0.78f, 0.94f);

            // ST's locked framing (2026-08-26). Institutionally attributed to the War Room -
            // deliberately avatar-less, so no new speaker or portrait is needed.
            Text framing = UISharedFoundation.CreateText(
                header, "Framing",
                "The Empire's War Room sets three daily trials to sharpen formation, judgement, "
                + "and command of the available ranks.",
                UITextRole.Caption, TextAnchor.MiddleCenter,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(1500f, 34f));
            SetNorm(framing.rectTransform, 0.14f, 0.10f, 0.86f, 0.46f);

            // BACK is pinned hard left so it cannot cover the title, which is exactly what it did.
            Button back = UISharedFoundation.CreateButton(
                header, "Btn_Back", "BACK", new Vector2(180f, 70f),
                UIFrozenTokens.ColorPanel, Close, null, true);
            SetNorm(back.GetComponent<RectTransform>(), 0.01f, 0.24f, 0.13f, 0.80f);
        }

        /// <summary>
        /// Anchors a child to an explicit normalised band of its parent.
        ///
        /// Named `SetNorm` to match the convention every other presenter already uses (23 files
        /// carry an identical private helper). I originally wrote this as `SetNorm()` - a third name
        /// for a thing that already had one, which is how a codebase ends up with three spellings
        /// of the same idea.
        ///
        /// It exists at all because CreateText/CreateButton size their rects but never place them:
        /// without this call every child of a panel sits on the same centre point and silently
        /// overlaps. That is not a quirk of those helpers - it is the established contract, and
        /// every other presenter honours it. I simply omitted the step.
        /// </summary>
        private static void SetNorm(RectTransform rect, float left, float bottom, float right, float top)
        {
            if (rect == null) return;
            rect.anchorMin = new Vector2(left, bottom);
            rect.anchorMax = new Vector2(right, top);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
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

            // Three stacked bands rather than three TextAnchor values. See BuildHeader: the anchor
            // argument aligns text within its rect and does NOT place the rect, so all three of
            // these previously rendered on top of one another at the card's centre.
            Text title = UISharedFoundation.CreateText(
                card, "Label", label, UITextRole.Title, TextAnchor.UpperLeft,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(900f, 40f));
            // 0.88 not 0.94: once ApplyFramedPanel gave these cards a real 9-slice
            // border, the title band ran under the frame's top edge and the text clipped
            // it. The frame did not exist when I first picked these numbers.
            SetNorm(title.rectTransform, 0.05f, 0.60f, 0.62f, 0.88f);

            Text flavourText = UISharedFoundation.CreateText(
                card, "Flavour", flavour, UITextRole.Caption, TextAnchor.UpperLeft,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(900f, 32f));
            SetNorm(flavourText.rectTransform, 0.05f, 0.34f, 0.62f, 0.58f);

            // The RULE stays separate from the flavour line and is generated from the same
            // deterministic seed that scores the trial - copy must never drift from what is
            // actually being judged.
            Text status = UISharedFoundation.CreateText(
                card, "Status", DescribeTrial(trial), UITextRole.Body, TextAnchor.UpperLeft,
                UIFrozenTokens.ColorTextPrimary, false, new Vector2(900f, 36f));
            SetNorm(status.rectTransform, 0.05f, 0.10f, 0.62f, 0.32f);

            Button play = UISharedFoundation.CreateButton(
                card, "Btn_Play", IsCleared(trial) ? "CLEARED" : "PLAY", new Vector2(220f, 72f),
                IsCleared(trial) ? UIFrozenTokens.ColorHeader : UIFrozenTokens.ColorAccentEmerald,
                () => AttemptTrial(trial), null, true);
            SetNorm(play.GetComponent<RectTransform>(), 0.68f, 0.28f, 0.94f, 0.72f);

            // Chrome applied AFTER the card's children are banded and its own rect is final -
            // ApplyFramedPanel fits the 9-slice border against the CURRENT rect, so calling it
            // earlier measures Unity's default 100x100 and shrinks the border (locked rule across
            // ~24 call sites; the helper now warns when it detects exactly that).
            Image cardImage = card.GetComponent<Image>();
            if (cardImage != null)
            {
                UISharedFoundation.ApplyFramedPanel(
                    cardImage, null, UIFrozenTokens.ColorPanel, UIFrozenTokens.ColorPanel);
            }
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

            // Chrome to match the trial cards. Added after the text is placed, for the same
            // border-fit reason - and added at all because the capture showed this row sitting
            // frameless directly beneath three framed ones, which read as an unfinished panel
            // rather than a deliberate summary line. My own change created that inconsistency.
            Image cycleImage = card.GetComponent<Image>();
            if (cycleImage != null)
            {
                UISharedFoundation.ApplyFramedPanel(
                    cycleImage, null, UIFrozenTokens.ColorHeader, UIFrozenTokens.ColorHeader);
            }
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
                    // Same roster the completion check uses - a screen showing a different rule
                    // than the one being scored is worse than showing no rule at all.
                    return SoloCircuitCollectionRule.BandFor(dayKey, OwnedCardIds(), RarityOf).Describe();
                case SoloCircuitTrial.TacticalBrief:
                    return "Complete today's tactical puzzle.";
                default:
                    return string.Empty;
            }
        }

        /// <summary>
        /// Routes a trial tap to its REAL completion check. Never grants on the tap itself.
        ///
        /// This previously called RecordClear directly, which meant the reward was claimable by
        /// opening a screen and pressing a button - the exact thing the header warned against.
        /// Each trial now routes to whatever can actually verify it:
        ///   - Collection: verifiable from the save right now (ownership is a standing fact), so it
        ///     is checked here and clears immediately when the roster qualifies.
        ///   - Tactical Brief: needs an observed solve. Opening today's puzzle is the action; the
        ///     clear arrives via SoloCircuitCompletion.ReportPuzzleSolved when it is actually
        ///     solved.
        ///   - Formation: cleared by actually WINNING a battle under the day's restriction. Wired
        ///     at GameBootstrap.HandleMatchEnded, which is the only place holding both the outcome
        ///     and the live deployment log. Tapping it here still does nothing on purpose - the
        ///     screen is not where that trial is played.
        /// </summary>
        private void AttemptTrial(SoloCircuitTrial trial)
        {
            if (Progress == null) return;

            switch (trial)
            {
                case SoloCircuitTrial.Collection:
                    SoloCircuitCompletion.ReportCollectionChecked(
                        Progress, OwnedCardIds(), RarityOf, _nowUtc);
                    SaveSystem.Save(_profile);
                    break;

                case SoloCircuitTrial.TacticalBrief:
                    OpenTodaysBrief();
                    return;   // the puzzle screen takes over; no rebuild behind it

                case SoloCircuitTrial.Formation:
                    // Deliberately does nothing HERE. This trial is cleared by winning a real
                    // battle under the day's restriction, reported from
                    // GameBootstrap.HandleMatchEnded. A tap is not a completion.
                    break;
            }

            BuildUI();
        }

        /// <summary>Opens today's selected puzzle, and reports the solve back to the Circuit when
        /// the player leaves it. The report is what clears the trial - not the opening.</summary>
        private void OpenTodaysBrief()
        {
            System.Collections.Generic.IReadOnlyList<TacticalPuzzleDefinition> library =
                TacticalPuzzleLibrary.AvailablePuzzles();

            var host = GetComponent<TacticalPuzzlePresenter>();
            if (host == null) host = gameObject.AddComponent<TacticalPuzzlePresenter>();

            host.Initialize(library, onExit: () =>
            {
                ReportBriefOutcome(host);
                BuildUI();
            });
        }

        /// <summary>Reads the solved puzzle id off the presenter and reports it. Kept separate so a
        /// test can drive it without a real puzzle session.</summary>
        private void ReportBriefOutcome(TacticalPuzzlePresenter host)
        {
            // Read the solve off the live session rather than a convenience field the presenter
            // does not expose - IsSolved is the same flag ShowResult() uses to decide whether the
            // attempt was worth persisting, so the Circuit credits exactly what the puzzle screen
            // itself counts as a win.
            TacticalPuzzleSession session = host != null ? host.SessionForTests : null;
            if (session == null || !session.IsSolved) return;

            string solvedId = session.Definition != null ? session.Definition.PuzzleId : null;
            if (string.IsNullOrEmpty(solvedId)) return;

            SoloCircuitCompletion.ReportPuzzleSolved(Progress, PuzzleIds(), solvedId, _nowUtc);
            SaveSystem.Save(_profile);
        }

        private System.Collections.Generic.List<string> PuzzleIds()
        {
            var ids = new System.Collections.Generic.List<string>();
            foreach (TacticalPuzzleDefinition d in TacticalPuzzleLibrary.AvailablePuzzles())
                if (d != null && !string.IsNullOrEmpty(d.PuzzleId)) ids.Add(d.PuzzleId);
            return ids;
        }

        /// <summary>Owned card ids, covering BOTH storage paths - the V1 cardProgression list and
        /// the legacy flat cardCollection. A player mid-migration must not silently fail a trial
        /// because their roster lives in the older field.</summary>
        private System.Collections.Generic.IEnumerable<string> OwnedCardIds()
        {
            var ids = new System.Collections.Generic.List<string>();
            if (_profile == null) return ids;

            if (_profile.cardProgression != null)
                foreach (CardProgressionRecord rec in _profile.cardProgression)
                    if (rec != null && rec.copyCount >= 1 && !string.IsNullOrEmpty(rec.cardId))
                        ids.Add(rec.cardId);

            if (_profile.cardCollection != null)
                foreach (string id in _profile.cardCollection)
                    if (!string.IsNullOrEmpty(id)) ids.Add(id);

            return ids;
        }

        private int RarityOf(string cardId)
        {
            CardDatabase db = CardDatabase.Instance;
            Card card = db != null ? db.GetCard(cardId) : null;
            return card != null ? card.Rarity : 0;
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
