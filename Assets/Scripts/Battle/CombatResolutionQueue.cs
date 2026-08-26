using System.Collections.Generic;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// Playback queue for the Combat Resolution Stage.
    ///
    /// This is the piece with real rules, so it lives here as a plain testable class rather than
    /// inside a MonoBehaviour (CLAUDE.md non-negotiable #6). The stage component owns pixels and
    /// timing; every decision about WHAT plays and WHAT gets merged is made here and is testable
    /// without a scene.
    ///
    /// THE RULE THAT SHAPES EVERYTHING: presentation must never slow, pause or reorder authoritative
    /// combat. Combat runs at its own pace and this queue catches up by MERGING, never by making
    /// gameplay wait. That is why Enqueue can silently coalesce - and why it is forbidden from
    /// coalescing the beats that carry irreversible information.
    /// </summary>
    public class CombatResolutionQueue
    {
        /// <summary>How many future beats are visibly indicated. The doc's queue indicator shows the
        /// active beat plus up to two queued, so anything beyond this is real but not advertised.</summary>
        public const int VisibleQueuedBeats = 2;

        private readonly List<CombatResolutionEvent> _pending = new List<CombatResolutionEvent>();
        private bool _hasActive;
        private CombatResolutionEvent _active;

        public int PendingCount => _pending.Count;
        public bool HasActive => _hasActive;
        public CombatResolutionEvent Active => _active;

        /// <summary>Diamonds to light: the active beat plus up to two queued. Never more, however
        /// deep the real backlog is.</summary>
        public int VisibleQueueIndicators
        {
            get
            {
                int queued = _pending.Count;
                if (queued > VisibleQueuedBeats) queued = VisibleQueuedBeats;
                return (_hasActive ? 1 : 0) + queued;
            }
        }

        /// <summary>
        /// Adds a beat, merging it into the pending tail when the two are redundant.
        ///
        /// Coalescing is deliberately NARROW: same tick, same type, same side, same lane, and
        /// neither beat critical. Two ordinary clash beats from one tick become one aggregate -
        /// which is what the doc asks for ("do not play nine individual full animations"). Anything
        /// that fails those checks is appended, because merging across ticks or sides would tell the
        /// player something that did not happen.
        /// </summary>
        public void Enqueue(CombatResolutionEvent beat)
        {
            if (beat.IsCriticalToPreserve)
            {
                _pending.Add(beat);
                return;
            }

            if (_pending.Count > 0)
            {
                CombatResolutionEvent tail = _pending[_pending.Count - 1];
                if (CanCoalesce(tail, beat))
                {
                    _pending[_pending.Count - 1] = Merge(tail, beat);
                    return;
                }
            }

            _pending.Add(beat);
        }

        private static bool CanCoalesce(CombatResolutionEvent a, CombatResolutionEvent b) =>
            !a.IsCriticalToPreserve &&
            !b.IsCriticalToPreserve &&
            a.TickIndex == b.TickIndex &&
            a.Type == b.Type &&
            a.Source == b.Source &&
            a.HasLane == b.HasLane &&
            (!a.HasLane || a.Lane == b.Lane);

        /// <summary>
        /// Combines two redundant beats into one.
        ///
        /// Numeric results ADD rather than being replaced: the aggregate must equal what actually
        /// happened, so showing only the later number would under-report the tick. Slot state takes
        /// the LATER known value, since it is a state rather than a delta.
        /// </summary>
        private static CombatResolutionEvent Merge(CombatResolutionEvent a, CombatResolutionEvent b) =>
            new CombatResolutionEvent(
                a.Type,
                a.Source,
                a.TickIndex,
                a.SignedValue + b.SignedValue,
                a.Lane,
                a.HasLane,
                a.Overflow + b.Overflow,
                b.RemainingSlots >= 0 ? b.RemainingSlots : a.RemainingSlots,
                string.IsNullOrEmpty(a.SourceId) ? b.SourceId : a.SourceId,
                a.HasSchool ? a.School : b.School,
                a.HasSchool || b.HasSchool,
                a.Tier > b.Tier ? a.Tier : b.Tier);

        /// <summary>Promotes the next beat to active. Returns false when nothing is waiting.</summary>
        public bool TryAdvance()
        {
            if (_pending.Count == 0)
            {
                _hasActive = false;
                return false;
            }

            _active = _pending[0];
            _pending.RemoveAt(0);
            _hasActive = true;
            return true;
        }

        /// <summary>Marks the active beat finished without pulling the next one, so the caller
        /// controls pacing.</summary>
        public void CompleteActive() => _hasActive = false;

        /// <summary>
        /// Drops everything, for scene exit / result transition / replay skip.
        ///
        /// Unconditional on purpose: at that point nothing is going to be presented, so preserving
        /// critical beats would only leak them into the next match's stage.
        /// </summary>
        public void Clear()
        {
            _pending.Clear();
            _hasActive = false;
        }
    }
}
