using System.Collections;
using UnityEngine;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Reusable reveal for a completed UI surface or confirmation panel. Normal Play Mode uses a
    /// short alpha/scale entrance; EditMode and reduced-motion mode snap to the final state.
    /// There is intentionally no rotation, pulse, or repeated drift in this primitive.
    /// </summary>
    public sealed class PresentationRevealRunner : MonoBehaviour
    {
        public const float DefaultDurationSeconds = 0.18f;

        private CanvasGroup _canvasGroup;
        private RectTransform _contentRect;
        private Vector3 _restScale = Vector3.one;
        private Coroutine _revealCoroutine;

        /// <summary>Starts the reveal after the caller has finished building the real content.</summary>
        public void Initialize(CanvasGroup canvasGroup, RectTransform contentRect,
            float durationSeconds = DefaultDurationSeconds)
        {
            _canvasGroup = canvasGroup;
            _contentRect = contentRect != null ? contentRect : transform as RectTransform;
            _restScale = _contentRect != null ? _contentRect.localScale : Vector3.one;

            if (_revealCoroutine != null)
            {
                StopCoroutine(_revealCoroutine);
                _revealCoroutine = null;
            }

            if (_canvasGroup == null)
                return;

            if (!MotionPolicy.ShouldPlayDecorativeMotion(Application.isPlaying) || durationSeconds <= 0f)
            {
                SnapToVisible();
                return;
            }

            _canvasGroup.alpha = 0f;
            if (_contentRect != null)
                _contentRect.localScale = _restScale * 0.985f;
            _revealCoroutine = StartCoroutine(Reveal(durationSeconds));
        }

        private void SnapToVisible()
        {
            _canvasGroup.alpha = 1f;
            if (_contentRect != null)
                _contentRect.localScale = _restScale;
        }

        private IEnumerator Reveal(float durationSeconds)
        {
            float elapsed = 0f;
            while (elapsed < durationSeconds && _canvasGroup != null)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / durationSeconds);
                float eased = t * t * (3f - 2f * t);
                _canvasGroup.alpha = eased;
                if (_contentRect != null)
                    _contentRect.localScale = Vector3.LerpUnclamped(_restScale * 0.985f, _restScale, eased);
                yield return null;
            }

            if (_canvasGroup != null)
                _canvasGroup.alpha = 1f;
            if (_contentRect != null)
                _contentRect.localScale = _restScale;
            _revealCoroutine = null;
        }
    }
}
