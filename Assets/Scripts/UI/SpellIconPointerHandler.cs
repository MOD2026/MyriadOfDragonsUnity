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
        private bool _pressed;

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            _holdFired = false;
            if (_holdTimer != null) StopCoroutine(_holdTimer);
            _holdTimer = StartCoroutine(HoldTimer());
        }

        /// <summary>Only a release that follows a press on THIS tile counts. Before this guard a
        /// stray Up (or the Up that follows an Exit) re-ran the tap.</summary>
        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_pressed) return;
            EndPress(cancelled: false);
        }

        /// <summary>A finger/cursor leaving the tile CANCELS the press: it ends a hold (so the
        /// tooltip closes) but never reports a tap. This used to report OnQuickTap, which meant
        /// (a) a mouse merely hovering over and off a spell armed/cast it, (b) every ordinary
        /// click fired a SECOND tap when the mouse left the tile afterwards (a bogus "still
        /// cooling down" rejection right after a real cast), and (c) dragging off a tile before
        /// releasing still cast it - unlike every other button in the game.</summary>
        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_pressed) return;
            EndPress(cancelled: true);
        }

        private void EndPress(bool cancelled)
        {
            if (_holdTimer != null)
            {
                StopCoroutine(_holdTimer);
                _holdTimer = null;
            }

            bool wasHold = _holdFired;
            _pressed = false;
            _holdFired = false;

            if (wasHold) OnHoldEnd?.Invoke();
            else if (!cancelled) OnQuickTap?.Invoke();
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
            // A hold that was already showing its tooltip must close it: nothing else will ever
            // deliver the matching Up/Exit to a tile that just went inactive, so the tooltip
            // would otherwise stay on screen. A press that had NOT become a hold reports nothing.
            bool hadHold = _holdFired;
            _pressed = false;
            _holdFired = false;
            if (hadHold) OnHoldEnd?.Invoke();
        }
    }
}
