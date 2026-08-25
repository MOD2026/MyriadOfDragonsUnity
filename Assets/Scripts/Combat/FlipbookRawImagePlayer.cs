using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Combat
{
    /// <summary>
    /// Thin MonoBehaviour timing shim (CLAUDE.md non-negotiable #6) - owns the RawImage and the
    /// flipbook's own columns/rows/duration, and every Update() does is measure elapsed wall-clock
    /// time and hand it to <see cref="FlipbookFrames.UvRectForElapsed"/> for the real frame math.
    /// No logic lives here.
    /// </summary>
    public sealed class FlipbookRawImagePlayer : MonoBehaviour
    {
        [SerializeField] private RawImage image;
        [SerializeField] private int columns = 4;
        [SerializeField] private int rows = 4;
        [SerializeField] private float durationMs = 150f;

        /// <summary>Read-only accessors so EditMode wiring tests can verify a prefab's serialized
        /// configuration without exercising Update() (which EditMode cannot run).</summary>
        public RawImage Image => image;
        public int Columns => columns;
        public int Rows => rows;
        public float DurationMs => durationMs;

        private float _elapsedMs;

        private void Update()
        {
            _elapsedMs += Time.deltaTime * 1000f;
            if (image != null)
                image.uvRect = FlipbookFrames.UvRectForElapsed(_elapsedMs, durationMs, columns, rows);
        }
    }
}
