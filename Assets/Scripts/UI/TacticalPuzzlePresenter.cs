using System;
using System.Collections.Generic;
using MyriadOfDragons.Battle;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    public enum TacticalPuzzleView
    {
        Entry,
        Board,
        Result,
    }

    /// <summary>
    /// War-Room Reconstructions screen: the entry list, the playable board, and the result.
    ///
    /// This closes the "nothing was playable" gap - the verifier, the authoring layer and the
    /// session model all existed, and no player could reach any of it.
    ///
    /// IT HOLDS NO RULES. Every legality question goes to <see cref="TacticalPuzzleSession"/>,
    /// which goes to the real verifier. The presenter builds pixels, routes taps and re-renders.
    /// That split is the project's standing rule (MonoBehaviours supply timing only) and it is
    /// also the only way this screen can be tested at all: EditMode runs no Update() and, measured
    /// earlier this session, a GraphicRaycaster resolves nothing without a rendered frame. So every
    /// interaction is a plain method a test can call, and the Buttons merely forward to it.
    ///
    /// NARRATIVE COPY IS NOT INVENTED HERE. It comes from <see cref="TacticalPuzzleCopy"/>, which
    /// carries the locked framing and the deliberately-provisional slot labels.
    ///
    /// ART IS NON-BLOCKING. Sprites are looked up by resource path and every one of them is
    /// optional - a missing sprite falls back to flat colour, exactly like
    /// EmpireBuildingDetailPresenter. Real art drops in later with no second UI pass.
    /// </summary>
    public class TacticalPuzzlePresenter : MonoBehaviour
    {
        public const string CanvasName = "TacticalPuzzleCanvas";

        /// <summary>Optional art, by role. Nothing here is required to exist yet.</summary>
        private static readonly Dictionary<string, string> ArtResourcePaths = new Dictionary<string, string>
        {
            { "entry", "UI/TacticalPuzzleV1/tactical_puzzle_entry_shell_v1_rgba" },
            { "board", "UI/TacticalPuzzleV1/tactical_puzzle_board_frame_v1_rgba" },
            { "result", "UI/TacticalPuzzleV1/tactical_puzzle_result_modal_v1_rgba" },
            { "tile_locked", "UI/TacticalPuzzleV1/tactical_puzzle_tile_locked_v1_rgba" },
            { "tile_available", "UI/TacticalPuzzleV1/tactical_puzzle_tile_available_v1_rgba" },
            { "tile_completed", "UI/TacticalPuzzleV1/tactical_puzzle_tile_completed_v1_rgba" },
        };

        private GameObject _canvasObj;
        private Transform _viewRoot;
        private Action _onExit;

        private TacticalPuzzleSlate _slate;
        private TacticalPuzzleSession _session;
        private int _activeSlotIndex = -1;
        private int _selectedHandIndex;
        private string _status;

        public TacticalPuzzleView CurrentView { get; private set; } = TacticalPuzzleView.Entry;

        // ---- test seams. EditMode cannot tap a Button, so the state a tap would produce is
        // ---- readable and the action a tap would take is callable.
        public GameObject CanvasObjectForTests => _canvasObj;
        public TacticalPuzzleSlate SlateForTests => _slate;
        public TacticalPuzzleSession SessionForTests => _session;
        public int ActiveSlotIndexForTests => _activeSlotIndex;
        public int SelectedHandIndexForTests => _selectedHandIndex;
        public string StatusForTests => _status;

        public void Initialize(IEnumerable<TacticalPuzzleDefinition> puzzles, Action onExit = null)
        {
            _onExit = onExit;
            _slate = new TacticalPuzzleSlate(puzzles);
            CurrentView = TacticalPuzzleView.Entry;
            _status = null;
            Build();
        }

        // ------------------------------------------------------------------ interactions

        /// <summary>Opens a slot. A locked slot is refused with a reason rather than ignored - a
        /// tap that does nothing at all reads as a broken button.</summary>
        public bool OpenSlot(int index)
        {
            TacticalPuzzleSlot slot = _slate?.SlotAt(index);
            if (slot == null) return false;

            if (slot.State == TacticalPuzzleSlotState.Locked)
            {
                _status = TacticalPuzzleCopy.LockedLine;
                Build();
                return false;
            }

            _activeSlotIndex = index;
            _session = new TacticalPuzzleSession(slot.Definition);
            _selectedHandIndex = 0;
            _status = null;
            CurrentView = TacticalPuzzleView.Board;
            Build();
            return true;
        }

        public void SelectHandCard(int handIndex)
        {
            _selectedHandIndex = handIndex;
            Build();
        }

        /// <summary>Deploys the selected card into a lane. The session decides whether that is
        /// allowed; a refusal leaves the position untouched and explains itself.</summary>
        public TacticalPuzzleIssueOutcome DeploySelectedInto(Lane lane)
        {
            if (_session == null) return TacticalPuzzleIssueOutcome.RejectedIllegal;

            TacticalPuzzleIssueReport report = _session.TryIssue(new TacticalPuzzleActionSpec
            {
                Kind = TacticalPuzzleActionKind.Deploy,
                HandIndex = _selectedHandIndex,
                Lane = lane,
            });

            _status = report.Message;

            if (report.Accepted)
            {
                // Keep the selection pointing at something real: a deploy removes the card from
                // the hand, so the old index would otherwise name a different card or nothing.
                int handSize = _session.Board != null ? _session.Board.PlayerSide.Hand.Count : 0;
                if (_selectedHandIndex >= handSize) _selectedHandIndex = Mathf.Max(0, handSize - 1);

                if (_session.IsFinished) ShowResult();
                else Build();
            }
            else
            {
                Build();
            }

            return report.Outcome;
        }

        public void UndoLastOrder()
        {
            if (_session == null || !_session.Undo()) return;
            _status = null;
            Build();
        }

        public void ResetPosition()
        {
            if (_session == null) return;
            _session.Reset();
            _selectedHandIndex = 0;
            _status = null;
            CurrentView = TacticalPuzzleView.Board;
            Build();
        }

        /// <summary>Ends the attempt and shows the verdict. Recording is the slate's call - only a
        /// solved attempt completes a slot.</summary>
        public void ShowResult()
        {
            if (_session == null) return;
            _slate?.RecordAttempt(_activeSlotIndex, _session.Current);
            CurrentView = TacticalPuzzleView.Result;
            Build();
        }

        public void BackToEntry()
        {
            CurrentView = TacticalPuzzleView.Entry;
            _session = null;
            _activeSlotIndex = -1;
            _status = null;
            Build();
        }

        public void Exit()
        {
            TeardownUI();
            _onExit?.Invoke();
        }

        // ------------------------------------------------------------------ building

        private void Build()
        {
            TeardownUI();

            Canvas canvas = UISharedFoundation.CreateScreenCanvas(CanvasName, new Vector2(1920, 1080));
            _canvasObj = canvas.gameObject;
            canvas.sortingOrder = 45;

            GameObject bg = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(_canvasObj.transform, false);
            UISharedFoundation.StretchFull(bg.GetComponent<RectTransform>());
            Image bgImage = bg.GetComponent<Image>();
            // Opaque by design: a transparent root lets the camera clear colour bleed through on
            // non-16:9 viewports, which is the class of bug already logged against other screens.
            bgImage.color = new Color(0.07f, 0.07f, 0.09f, 1f);
            // One role per view. This used to be entry-or-board, which silently left the delivered
            // result-modal art unused - the result view rendered the board frame instead.
            ApplyOptionalArt(bgImage, CurrentView switch
            {
                TacticalPuzzleView.Entry => "entry",
                TacticalPuzzleView.Result => "result",
                _ => "board",
            });

            _viewRoot = bg.transform;

            switch (CurrentView)
            {
                case TacticalPuzzleView.Entry: BuildEntryView(); break;
                case TacticalPuzzleView.Board: BuildBoardView(); break;
                case TacticalPuzzleView.Result: BuildResultView(); break;
            }

            BuildStatusLine();
        }

        /// <summary>Applies art if it exists. A missing sprite is the EXPECTED case right now and
        /// must never blank the element - the flat colour already set stays.</summary>
        private static void ApplyOptionalArt(Image target, string role)
        {
            if (target == null || !ArtResourcePaths.TryGetValue(role, out string path)) return;
            Sprite art = Resources.Load<Sprite>(path);
            if (art == null) return;
            target.sprite = art;
            target.color = Color.white;
        }

        public static bool HasArtPathForTests(string role) => ArtResourcePaths.ContainsKey(role);

        /// <summary>Test seam: resolves a role to its sprite exactly as the screen does. Lets a
        /// test prove the art actually LOADS, which a reserved-path check cannot.</summary>
        public static Sprite LoadArtForTests(string role) =>
            ArtResourcePaths.TryGetValue(role, out string path) ? Resources.Load<Sprite>(path) : null;

        public static IEnumerable<string> ArtRolesForTests => ArtResourcePaths.Keys;

        private void BuildEntryView()
        {
            Text title = UISharedFoundation.CreateText(_viewRoot, "Title", TacticalPuzzleCopy.ScreenTitle,
                UITextRole.Display, TextAnchor.MiddleCenter, new Color(0.95f, 0.88f, 0.62f), true,
                new Vector2(1200f, 60f));
            title.fontSize = 40;
            SetNorm(title.rectTransform, 0.06f, 0.86f, 0.94f, 0.95f);

            Text intro = UISharedFoundation.CreateText(_viewRoot, "Intro", TacticalPuzzleCopy.Intro,
                UITextRole.Body, TextAnchor.UpperCenter, new Color(0.84f, 0.81f, 0.72f), true,
                new Vector2(1200f, 90f));
            intro.horizontalOverflow = HorizontalWrapMode.Wrap;
            intro.verticalOverflow = VerticalWrapMode.Truncate;
            SetNorm(intro.rectTransform, 0.10f, 0.76f, 0.90f, 0.85f);

            int count = _slate?.Slots.Count ?? 0;
            if (count == 0)
            {
                // Say so plainly. An empty row of nothing reads as a broken screen, and puzzle
                // content is a separate design pass that has not landed yet.
                Text empty = UISharedFoundation.CreateText(_viewRoot, "EmptyState",
                    "NO RECORDS HAVE BEEN RECOVERED YET.", UITextRole.Title, TextAnchor.MiddleCenter,
                    new Color(0.75f, 0.73f, 0.66f), true, new Vector2(900f, 40f));
                SetNorm(empty.rectTransform, 0.10f, 0.46f, 0.90f, 0.56f);
            }

            for (int i = 0; i < count; i++)
            {
                BuildSlotTile(i, count);
            }

            UISharedFoundation.CreateButton(_viewRoot, "Btn_ExitPuzzles", "BACK",
                new Vector2(220f, 56f), new Color(0.28f, 0.2f, 0.16f), Exit);
        }

        private void BuildSlotTile(int index, int count)
        {
            TacticalPuzzleSlot slot = _slate.SlotAt(index);

            GameObject tile = new GameObject("Slot_" + index, typeof(RectTransform), typeof(Image), typeof(Button));
            tile.transform.SetParent(_viewRoot, false);

            Image img = tile.GetComponent<Image>();
            img.color = slot.State switch
            {
                TacticalPuzzleSlotState.Completed => new Color(0.16f, 0.36f, 0.26f),
                TacticalPuzzleSlotState.Available => new Color(0.22f, 0.24f, 0.30f),
                _ => new Color(0.14f, 0.13f, 0.14f),
            };
            ApplyOptionalArt(img, slot.State switch
            {
                TacticalPuzzleSlotState.Completed => "tile_completed",
                TacticalPuzzleSlotState.Available => "tile_available",
                _ => "tile_locked",
            });

            int captured = index;
            tile.GetComponent<Button>().onClick.AddListener(() => OpenSlot(captured));

            // Even spread across the row; no magic pixel positions, so the row stays correct
            // whatever number of puzzles the design eventually ships.
            float span = 0.88f / Mathf.Max(1, count);
            float left = 0.06f + span * index;
            SetNorm(tile.GetComponent<RectTransform>(), left + 0.01f, 0.36f, left + span - 0.01f, 0.68f);

            Text label = UISharedFoundation.CreateText(tile.transform, "Label", slot.Label,
                UITextRole.Title, TextAnchor.UpperCenter, new Color(0.95f, 0.9f, 0.79f), true,
                new Vector2(260f, 60f));
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetNorm(label.rectTransform, 0.06f, 0.62f, 0.94f, 0.94f);

            string stateLine = slot.State switch
            {
                TacticalPuzzleSlotState.Completed => TacticalPuzzleCopy.SolvedLine,
                TacticalPuzzleSlotState.Available => "OPEN",
                _ => TacticalPuzzleCopy.LockedLine,
            };
            Text state = UISharedFoundation.CreateText(tile.transform, "State", stateLine,
                UITextRole.Caption, TextAnchor.UpperCenter, new Color(0.82f, 0.8f, 0.7f), true,
                new Vector2(260f, 70f));
            state.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetNorm(state.rectTransform, 0.06f, 0.28f, 0.94f, 0.60f);

            if (slot.BestResult != null)
            {
                Text best = UISharedFoundation.CreateText(tile.transform, "Best",
                    "Best: " + slot.BestResult.ActionsUsed + " orders", UITextRole.Caption,
                    TextAnchor.MiddleCenter, new Color(0.7f, 0.85f, 0.68f), true, new Vector2(260f, 30f));
                SetNorm(best.rectTransform, 0.06f, 0.06f, 0.94f, 0.24f);
            }
        }

        private void BuildBoardView()
        {
            TacticalPuzzleSlot slot = _slate?.SlotAt(_activeSlotIndex);

            Text title = UISharedFoundation.CreateText(_viewRoot, "BoardTitle",
                slot != null ? slot.Label.ToUpperInvariant() : TacticalPuzzleCopy.ScreenTitle,
                UITextRole.Title, TextAnchor.MiddleLeft, new Color(0.95f, 0.88f, 0.62f), true,
                new Vector2(700f, 44f));
            SetNorm(title.rectTransform, 0.04f, 0.90f, 0.60f, 0.97f);

            Text objective = UISharedFoundation.CreateText(_viewRoot, "Objective",
                DescribeObjective(), UITextRole.Body, TextAnchor.MiddleLeft,
                new Color(0.84f, 0.81f, 0.72f), true, new Vector2(900f, 34f));
            SetNorm(objective.rectTransform, 0.04f, 0.84f, 0.72f, 0.90f);

            Text budget = UISharedFoundation.CreateText(_viewRoot, "Budget", DescribeBudget(),
                UITextRole.Caption, TextAnchor.MiddleRight, new Color(0.95f, 0.86f, 0.55f), true,
                new Vector2(400f, 34f));
            SetNorm(budget.rectTransform, 0.72f, 0.84f, 0.96f, 0.90f);

            // Enemy lanes on top, player lanes below - the same vertical reading order as a match,
            // so the position transfers to real play instead of teaching a private layout.
            BuildLaneRow("Enemy", TacticalPuzzleSide.Enemy, 0.60f, 0.82f, interactive: false);
            BuildLaneRow("Player", TacticalPuzzleSide.Player, 0.32f, 0.56f, interactive: true);

            BuildHandRow();

            UISharedFoundation.CreateButton(_viewRoot, "Btn_Undo", "TAKE BACK",
                new Vector2(200f, 52f), new Color(0.26f, 0.24f, 0.20f), UndoLastOrder);
            UISharedFoundation.CreateButton(_viewRoot, "Btn_Reset", "RESET POSITION",
                new Vector2(220f, 52f), new Color(0.30f, 0.20f, 0.18f), ResetPosition);
            UISharedFoundation.CreateButton(_viewRoot, "Btn_Commit", "COMMIT",
                new Vector2(200f, 52f), new Color(0.16f, 0.42f, 0.28f), ShowResult);
            UISharedFoundation.CreateButton(_viewRoot, "Btn_BackToEntry", "BACK",
                new Vector2(180f, 52f), new Color(0.28f, 0.2f, 0.16f), BackToEntry);
        }

        private void BuildLaneRow(string name, TacticalPuzzleSide side, float bottom, float top, bool interactive)
        {
            PlayerBattleState state = side == TacticalPuzzleSide.Player
                ? _session?.Board?.PlayerSide
                : _session?.Board?.EnemySide;

            var lanes = new[] { Lane.Back, Lane.Middle, Lane.Front };
            for (int i = 0; i < lanes.Length; i++)
            {
                Lane lane = lanes[i];
                GameObject laneObj = new GameObject(name + "_" + lane,
                    typeof(RectTransform), typeof(Image), typeof(Button));
                laneObj.transform.SetParent(_viewRoot, false);

                Image img = laneObj.GetComponent<Image>();
                img.color = interactive
                    ? new Color(0.18f, 0.20f, 0.26f, 0.92f)
                    : new Color(0.24f, 0.16f, 0.16f, 0.92f);

                Button button = laneObj.GetComponent<Button>();
                if (interactive)
                {
                    Lane captured = lane;
                    button.onClick.AddListener(() => DeploySelectedInto(captured));
                }
                else
                {
                    button.interactable = false;
                }

                float span = 0.90f / lanes.Length;
                float left = 0.05f + span * i;
                SetNorm(laneObj.GetComponent<RectTransform>(), left + 0.01f, bottom, left + span - 0.01f, top);

                LaneState laneState = state != null ? state.Lanes[lane] : null;
                Text contents = UISharedFoundation.CreateText(laneObj.transform, "Contents",
                    DescribeLane(lane, laneState), UITextRole.Caption, TextAnchor.UpperCenter,
                    new Color(0.92f, 0.9f, 0.82f), true, new Vector2(380f, 150f));
                contents.horizontalOverflow = HorizontalWrapMode.Wrap;
                contents.verticalOverflow = VerticalWrapMode.Truncate;
                SetNorm(contents.rectTransform, 0.04f, 0.04f, 0.96f, 0.96f);
            }
        }

        private static string DescribeLane(Lane lane, LaneState state)
        {
            if (state == null) return lane.ToString().ToUpperInvariant();

            var sb = new System.Text.StringBuilder();
            sb.Append(lane.ToString().ToUpperInvariant());
            sb.Append("  (").Append(state.FreeSlots).Append(" free)");
            foreach (BattleCardInstance unit in state.Cards)
            {
                sb.Append('\n');
                sb.Append(unit.Definition.DisplayName);
                sb.Append("  ").Append(unit.Attack).Append('/').Append(unit.CurrentHealth);
                if (!unit.IsAlive) sb.Append("  (fallen)");
            }

            return sb.ToString();
        }

        private void BuildHandRow()
        {
            List<Cards.Card> hand = _session?.Board?.PlayerSide?.Hand;
            int count = hand?.Count ?? 0;

            Text header = UISharedFoundation.CreateText(_viewRoot, "HandHeader",
                count > 0 ? "ORDERS AVAILABLE" : "NO FORCES IN RESERVE", UITextRole.Caption,
                TextAnchor.MiddleLeft, new Color(0.8f, 0.78f, 0.68f), true, new Vector2(500f, 28f));
            SetNorm(header.rectTransform, 0.05f, 0.24f, 0.60f, 0.30f);

            for (int i = 0; i < count; i++)
            {
                Cards.Card card = hand[i];
                GameObject cardObj = new GameObject("Hand_" + i,
                    typeof(RectTransform), typeof(Image), typeof(Button));
                cardObj.transform.SetParent(_viewRoot, false);

                bool selected = i == _selectedHandIndex;
                cardObj.GetComponent<Image>().color = selected
                    ? new Color(0.34f, 0.40f, 0.30f)
                    : new Color(0.20f, 0.20f, 0.24f);

                int captured = i;
                cardObj.GetComponent<Button>().onClick.AddListener(() => SelectHandCard(captured));

                float span = Mathf.Min(0.18f, 0.90f / Mathf.Max(1, count));
                float left = 0.05f + span * i;
                SetNorm(cardObj.GetComponent<RectTransform>(), left + 0.005f, 0.06f, left + span - 0.005f, 0.22f);

                Text text = UISharedFoundation.CreateText(cardObj.transform, "Text",
                    card.DisplayName + "\n" + card.Attack + "/" + card.Health + "\nCost " + card.ResourceCost,
                    UITextRole.Caption, TextAnchor.MiddleCenter, new Color(0.95f, 0.92f, 0.84f), true,
                    new Vector2(200f, 110f));
                text.horizontalOverflow = HorizontalWrapMode.Wrap;
                SetNorm(text.rectTransform, 0.04f, 0.04f, 0.96f, 0.96f);
            }

            Text resource = UISharedFoundation.CreateText(_viewRoot, "ResourceLine",
                "RESOURCE  " + (_session?.Board?.PlayerSide?.Resource ?? 0), UITextRole.Body,
                TextAnchor.MiddleRight, new Color(0.95f, 0.86f, 0.55f), true, new Vector2(300f, 34f));
            SetNorm(resource.rectTransform, 0.72f, 0.24f, 0.96f, 0.30f);
        }

        private void BuildResultView()
        {
            bool solved = _session != null && _session.IsSolved;

            Text verdict = UISharedFoundation.CreateText(_viewRoot, "Verdict",
                solved ? TacticalPuzzleCopy.SolvedLine : TacticalPuzzleCopy.UnsolvedLine,
                UITextRole.Display, TextAnchor.MiddleCenter,
                solved ? new Color(0.7f, 0.9f, 0.68f) : new Color(0.92f, 0.7f, 0.6f), true,
                new Vector2(900f, 60f));
            verdict.fontSize = 38;
            SetNorm(verdict.rectTransform, 0.10f, 0.72f, 0.90f, 0.84f);

            var sb = new System.Text.StringBuilder();
            if (_session != null)
            {
                foreach (KeyValuePair<string, int> pair in _session.ScoreInputs())
                    sb.Append(pair.Key).Append(":  ").Append(pair.Value).Append('\n');
            }

            Text score = UISharedFoundation.CreateText(_viewRoot, "ScoreInputs", sb.ToString(),
                UITextRole.Body, TextAnchor.UpperCenter, new Color(0.9f, 0.88f, 0.8f), true,
                new Vector2(700f, 200f));
            score.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetNorm(score.rectTransform, 0.28f, 0.40f, 0.72f, 0.70f);

            UISharedFoundation.CreateButton(_viewRoot, "Btn_Retry", "STUDY IT AGAIN",
                new Vector2(260f, 56f), new Color(0.26f, 0.24f, 0.20f), ResetPosition);
            UISharedFoundation.CreateButton(_viewRoot, "Btn_ResultBack", "RETURN TO RECORDS",
                new Vector2(280f, 56f), new Color(0.28f, 0.2f, 0.16f), BackToEntry);
        }

        private void BuildStatusLine()
        {
            if (string.IsNullOrEmpty(_status)) return;

            Text status = UISharedFoundation.CreateText(_viewRoot, "StatusLine", _status,
                UITextRole.Caption, TextAnchor.MiddleCenter, new Color(0.95f, 0.82f, 0.6f), true,
                new Vector2(1400f, 30f));
            status.horizontalOverflow = HorizontalWrapMode.Wrap;
            SetNorm(status.rectTransform, 0.04f, 0.005f, 0.96f, 0.045f);
        }

        private string DescribeObjective()
        {
            TacticalPuzzleObjectiveSpec obj = _session?.Definition?.Objective;
            if (obj == null) return string.Empty;

            return obj.Kind switch
            {
                TacticalPuzzleObjectiveKind.SurviveClashes =>
                    "Hold the line for " + obj.ClashCount + " clash(es).",
                TacticalPuzzleObjectiveKind.DefeatMarkedTarget =>
                    "Bring down the marked target.",
                TacticalPuzzleObjectiveKind.ProtectLane =>
                    "Keep the " + obj.ProtectedLane + " lane held.",
                TacticalPuzzleObjectiveKind.MinimalResourceSolve =>
                    "Hold the field spending no more than " + obj.ResourceBudget + " Resource.",
                _ => string.Empty,
            };
        }

        private string DescribeBudget()
        {
            if (_session == null) return string.Empty;
            int left = _session.ActionsRemaining;
            return left < 0 ? "ORDERS: UNLIMITED" : "ORDERS LEFT: " + left;
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
            _viewRoot = null;
        }

        private void OnDestroy() => TeardownUI();
    }
}
