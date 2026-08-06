using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Distinguishes a quick tap from a press-and-hold on a single UI element, and reports
    /// exactly one of the two per press - never both. Built for the spell action bar
    /// (2026-08-06): shrinking spell buttons to 64px icon tiles removed their name/cost/effect
    /// text entirely (no room left on the tile), so a second, alternate gesture (hold) was added
    /// to recover that information as a tooltip. Without owning the tap/hold distinction here, a
    /// hold that gets released would also fire the tap's own action (arm targeting / cast) right
    /// as the tooltip closes, which is not what "hold to inspect" is supposed to mean.
    ///
    /// Deliberately not wired through Button.onClick - Unity's Button always fires onClick on
    /// release regardless of how long the press was held, which is exactly the ambiguity this
    /// class exists to remove.
    /// </summary>
    public class SpellIconPointerHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        /// <summary>How long a press must be held before it counts as a hold rather than a tap.
        /// Short enough to feel responsive; long enough that an ordinary tap never crosses it.</summary>
        public const float HoldThresholdSeconds = 0.35f;

        public Action OnQuickTap;
        public Action OnHoldStart;
        public Action OnHoldEnd;

        private Coroutine _holdTimer;
        private bool _holdFired;

        public void OnPointerDown(PointerEventData eventData)
        {
            _holdFired = false;
            if (_holdTimer != null) StopCoroutine(_holdTimer);
            _holdTimer = StartCoroutine(HoldTimer());
        }

        public void OnPointerUp(PointerEventData eventData) => EndPress();

        /// <summary>A finger/cursor dragged off the tile mid-press ends it the same as releasing -
        /// otherwise a hold that drifts off-target keeps "holding" a tile the pointer isn't over
        /// any more.</summary>
        public void OnPointerExit(PointerEventData eventData) => EndPress();

        private void EndPress()
        {
            if (_holdTimer != null)
            {
                StopCoroutine(_holdTimer);
                _holdTimer = null;
            }

            if (_holdFired) OnHoldEnd?.Invoke();
            else OnQuickTap?.Invoke();

            _holdFired = false;
        }

        private IEnumerator HoldTimer()
        {
            yield return new WaitForSeconds(HoldThresholdSeconds);
            _holdFired = true;
            OnHoldStart?.Invoke();
        }

        /// <summary>A tile can be deactivated mid-press - RefreshPhaseControls toggles the spell
        /// bar's visibility on every combat tick (~2.2s), and a phase change could land mid-hold.
        /// Stops the timer without invoking either callback, since the tile is no longer part of
        /// the active UI to react against.</summary>
        private void OnDisable()
        {
            if (_holdTimer != null)
            {
                StopCoroutine(_holdTimer);
                _holdTimer = null;
            }
            _holdFired = false;
        }
    }
}
