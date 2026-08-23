using System;
using System.Collections;
using UnityEngine;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// Staggered flip/fade reveal for pack draw tiles. First tile shows immediately; later tiles
    /// animate in during Play Mode. EditMode tests call <see cref="RevealNextForTests"/> (snap).
    /// </summary>
    public sealed class PackOpenRevealRunner : MonoBehaviour
    {
        public const float StaggerSeconds = 0.22f;
        public const float RevealDurationSeconds = 0.2f;

        public struct RevealTile
        {
            public CanvasGroup Group;
            public RectTransform Rect;
        }

        private RevealTile[] _tiles;
        private int _revealedCount;
        private Coroutine _routine;
        private Action _onAllRevealed;

        public int RevealedCountForTests => _revealedCount;
        public int TotalTilesForTests => _tiles?.Length ?? 0;

        public void Initialize(RevealTile[] tiles, Action onAllRevealed)
        {
            _tiles = tiles ?? Array.Empty<RevealTile>();
            _onAllRevealed = onAllRevealed;
            _revealedCount = _tiles.Length > 0 ? 1 : 0;

            for (int i = 0; i < _tiles.Length; i++)
                SetTileSnap(i, i < _revealedCount);

            if (_routine != null)
            {
                StopCoroutine(_routine);
                _routine = null;
            }

            if (_revealedCount >= _tiles.Length)
            {
                _onAllRevealed?.Invoke();
                return;
            }

            if (Application.isPlaying)
                _routine = StartCoroutine(RevealRoutine());
        }

        public bool RevealNextForTests()
        {
            if (_tiles == null || _revealedCount >= _tiles.Length)
                return false;

            _revealedCount++;
            SetTileSnap(_revealedCount - 1, true);

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

        private IEnumerator RevealRoutine()
        {
            while (_revealedCount < _tiles.Length)
            {
                yield return new WaitForSeconds(StaggerSeconds);
                int index = _revealedCount;
                _revealedCount++;
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

            float elapsed = 0f;
            while (elapsed < RevealDurationSeconds)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / RevealDurationSeconds);
                float eased = 1f - (1f - t) * (1f - t);
                SetTileAnimated(tile, eased);
                yield return null;
            }

            SetTileSnap(index, true);
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
