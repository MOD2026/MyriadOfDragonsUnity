using System;
using System.Collections;
using UnityEngine;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Staggered flip/fade reveal for pack draw tiles. First tile shows immediately; later tiles
    /// animate in during Play Mode. EditMode tests call <see cref="RevealNextForTests"/> (snap).
    /// Soft #3 (agent clarity): progress callbacks + tap-to-advance so multi-card packs are not a dead wait.
    /// </summary>
    public sealed class PackOpenRevealRunner : MonoBehaviour
    {
        public const float StaggerSeconds = 0.22f;
        public const float RevealDurationSeconds = 0.2f;
        public const float FirstTilePunchSeconds = 0.18f;

        public struct RevealTile
        {
            public CanvasGroup Group;
            public RectTransform Rect;
        }

        private RevealTile[] _tiles;
        private int _revealedCount;
        private Coroutine _routine;
        private Action _onAllRevealed;
        private Action<int, int> _onRevealProgress;
        private bool _skipWaitRequested;
        private bool _skipAnimationRequested;

        public int RevealedCountForTests => _revealedCount;
        public int TotalTilesForTests => _tiles?.Length ?? 0;

        public void Initialize(RevealTile[] tiles, Action onAllRevealed, Action<int, int> onRevealProgress = null)
        {
            _tiles = tiles ?? Array.Empty<RevealTile>();
            _onAllRevealed = onAllRevealed;
            _onRevealProgress = onRevealProgress;
            _revealedCount = _tiles.Length > 0 ? 1 : 0;
            _skipWaitRequested = false;
            _skipAnimationRequested = false;

            for (int i = 0; i < _tiles.Length; i++)
                SetTileSnap(i, i < _revealedCount);

            NotifyProgress();

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            if (_revealedCount >= _tiles.Length)
            {
                if (Application.isPlaying && _tiles.Length == 1)
                    _routine = StartCoroutine(FirstTilePunchThenComplete());
                else
                    _onAllRevealed?.Invoke();
                return;
            }

            if (Application.isPlaying)
                _routine = StartCoroutine(RevealRoutine());
        }

        /// <summary>Play Mode: skip current stagger wait / finish the in-flight flip so multi-packs are not a dead wait.</summary>
        public void RequestRevealNext()
        {
            if (_tiles == null || _revealedCount >= _tiles.Length) return;
            _skipWaitRequested = true;
            _skipAnimationRequested = true;
        }

        public bool RevealNextForTests()
        {
            if (_tiles == null || _revealedCount >= _tiles.Length)
                return false;

            _revealedCount++;
            SetTileSnap(_revealedCount - 1, true);
            NotifyProgress();

            if (_revealedCount >= _tiles.Length)
                _onAllRevealed?.Invoke();

            return true;
        }

        public int CountVisibleTilesForTests(float alphaThreshold = 0.5f)
        {
            if (_tiles == null) return 0;

            int count = 0;
            foreach (RevealTile tile in _tiles)
            {
                if (tile.Group != null && tile.Group.alpha >= alphaThreshold)
                    count++;
            }

            return count;
        }

        private void NotifyProgress()
        {
            _onRevealProgress?.Invoke(_revealedCount, _tiles?.Length ?? 0);
        }

        private IEnumerator FirstTilePunchThenComplete()
        {
            yield return PunchTile(0);
            _onAllRevealed?.Invoke();
            _routine = null;
        }

        private IEnumerator RevealRoutine()
        {
            if (_revealedCount > 0)
                yield return PunchTile(0);

            while (_revealedCount < _tiles.Length)
            {
                float waited = 0f;
                _skipWaitRequested = false;
                while (waited < StaggerSeconds && !_skipWaitRequested)
                {
                    waited += Time.deltaTime;
                    yield return null;
                }

                _skipWaitRequested = false;
                int index = _revealedCount;
                _revealedCount++;
                NotifyProgress();
                yield return AnimateTileIn(index);
            }

            _onAllRevealed?.Invoke();
            _routine = null;
        }

        private IEnumerator AnimateTileIn(int index)
        {
            RevealTile tile = _tiles[index];
            if (tile.Group == null || tile.Rect == null)
            {
                SetTileSnap(index, true);
                yield break;
            }

            _skipAnimationRequested = false;
            float elapsed = 0f;
            while (elapsed < RevealDurationSeconds && !_skipAnimationRequested)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / RevealDurationSeconds);
                float eased = 1f - (1f - t) * (1f - t);
                SetTileAnimated(tile, eased);
                yield return null;
            }

            SetTileSnap(index, true);
            _skipAnimationRequested = false;
        }

        private IEnumerator PunchTile(int index)
        {
            if (_tiles == null || index < 0 || index >= _tiles.Length) yield break;
            RevealTile tile = _tiles[index];
            if (tile.Rect == null) yield break;

            float elapsed = 0f;
            while (elapsed < FirstTilePunchSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / FirstTilePunchSeconds);
                // Up then settle — alpha untouched so EditMode visibility contract stays stable.
                float scale = t < 0.5f
                    ? Mathf.Lerp(1f, 1.08f, t * 2f)
                    : Mathf.Lerp(1.08f, 1f, (t - 0.5f) * 2f);
                tile.Rect.localScale = new Vector3(scale, scale, 1f);
                yield return null;
            }

            tile.Rect.localScale = Vector3.one;
        }

        private static void SetTileAnimated(RevealTile tile, float eased)
        {
            tile.Group.alpha = eased;
            tile.Group.interactable = eased > 0.5f;
            tile.Group.blocksRaycasts = eased > 0.5f;
            float scaleX = Mathf.Lerp(0.06f, 1f, eased);
            float scaleY = Mathf.Lerp(0.9f, 1f, eased);
            tile.Rect.localScale = new Vector3(scaleX, scaleY, 1f);
        }

        private void SetTileSnap(int index, bool visible)
        {
            if (_tiles == null || index < 0 || index >= _tiles.Length) return;

            RevealTile tile = _tiles[index];
            if (tile.Group == null || tile.Rect == null) return;

            if (visible)
            {
                tile.Group.alpha = 1f;
                tile.Group.interactable = true;
                tile.Group.blocksRaycasts = true;
                tile.Rect.localScale = Vector3.one;
            }
            else
            {
                tile.Group.alpha = 0f;
                tile.Group.interactable = false;
                tile.Group.blocksRaycasts = false;
                tile.Rect.localScale = new Vector3(0.06f, 0.9f, 1f);
            }
        }
    }
}
