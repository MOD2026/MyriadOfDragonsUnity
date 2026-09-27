using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.UI
{
    /// <summary>
    /// PRODUCTIVE CODING TASK - Loading Sigil v3 implementation. The smallest reusable loading
    /// overlay this project has - none existed before this (checked UIEmptyState.cs,
    /// StartupSoftLandingPresenter.cs, and every other UI file for a "Loading"/"Spinner" class;
    /// none found). Built for the real, existing Startup-to-Home transition
    /// (StartupSoftLandingPresenter.OnContinuePressed) rather than a hypothetical future need.
    ///
    /// Frames: the 8 approved Revamp V2 Loading Sigil v3 frames
    /// (Assets/Resources/UI/RevampV2Approved/LoadingSigil/loading_sigil_v3_frame_01..08.png,
    /// packaged/Unity-imported at commit 05cc2fc1). Cycled in strict 01->08->01 order, real-time,
    /// only in Play mode (Application.isPlaying guard) - matches this project's established
    /// convention of keeping MonoBehaviour timing out of EditMode-reachable paths
    /// (StoryOverlayPresenter's fade coroutine uses the same gate). SetFrameForTests exposes the
    /// same frame-selection logic synchronously so ordering/availability are testable without
    /// relying on a ticking Update loop.
    /// </summary>
    public class LoadingSigilOverlay : MonoBehaviour
    {
        public const string FrameResourcePathFormat = "UI/RevampV2Approved/LoadingSigil/loading_sigil_v3_frame_{0:00}";
        public const int FrameCount = 8;
        private const float SecondsPerFrame = 0.09f;

        private GameObject _canvasObj;
        private Image _sigilImg;
        private Coroutine _cycleCoroutine;
        private int _currentFrameIndex;

        /// <summary>Exposed for tests: the canvas Show() built.</summary>
        public GameObject CanvasObjectForTests => _canvasObj;

        /// <summary>Exposed for tests: which frame (0-based) is currently displayed.</summary>
        public int CurrentFrameIndexForTests => _currentFrameIndex;

        /// <summary>Exposed for tests: the Image's current sprite, to verify against a real
        /// independent Resources.Load of the same frame.</summary>
        public Sprite CurrentSpriteForTests => _sigilImg != null ? _sigilImg.sprite : null;

        /// <summary>Builds and shows the overlay as a child of <paramref name="parent"/>. Starts on
        /// frame 0 and, only in Play mode, begins real-time cycling. Returns the component so the
        /// caller can Hide() it later.</summary>
        public static LoadingSigilOverlay Show(Transform parent)
        {
            GameObject host = new GameObject("LoadingSigilOverlayHost");
            host.transform.SetParent(parent, false);
            var overlay = host.AddComponent<LoadingSigilOverlay>();
            overlay.BuildUI();
            overlay.SetFrameForTests(0);
            if (Application.isPlaying)
                overlay._cycleCoroutine = overlay.StartCoroutine(overlay.CycleFrames());
            return overlay;
        }

        private void BuildUI()
        {
            _canvasObj = new GameObject("LoadingSigilCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvasObj.transform.SetParent(transform, false);

            Canvas canvas = _canvasObj.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 200; // above StoryCanvas (100) - loading must cover everything.

            CanvasScaler scaler = _canvasObj.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = UISharedFoundation.MatchWidthOrHeight;

            // Decorative overlay art never intercepts input - same rule applied to every other
            // full-screen backdrop this project ships (Home, Campaign, Story, Collection).
            _canvasObj.GetComponent<GraphicRaycaster>().enabled = false;

            GameObject sigilObj = new GameObject("Sigil", typeof(RectTransform), typeof(Image));
            sigilObj.transform.SetParent(_canvasObj.transform, false);
            _sigilImg = sigilObj.GetComponent<Image>();
            _sigilImg.preserveAspect = true;
            _sigilImg.raycastTarget = false;

            RectTransform sigilRect = sigilObj.GetComponent<RectTransform>();
            sigilRect.anchorMin = new Vector2(0.5f, 0.5f);
            sigilRect.anchorMax = new Vector2(0.5f, 0.5f);
            sigilRect.pivot = new Vector2(0.5f, 0.5f);
            sigilRect.sizeDelta = new Vector2(256f, 256f); // native frame size, no upscaling.
        }

        /// <summary>Sets the displayed frame by 0-based index, wrapping. Synchronous, EditMode-safe
        /// - the real logic Update-loop cycling calls into, exposed directly for tests.</summary>
        public void SetFrameForTests(int index)
        {
            _currentFrameIndex = ((index % FrameCount) + FrameCount) % FrameCount;
            string path = string.Format(FrameResourcePathFormat, _currentFrameIndex + 1);
            Sprite frame = Resources.Load<Sprite>(path);
            if (_sigilImg != null) _sigilImg.sprite = frame;
        }

        private System.Collections.IEnumerator CycleFrames()
        {
            while (true)
            {
                yield return new WaitForSeconds(SecondsPerFrame);
                SetFrameForTests(_currentFrameIndex + 1);
            }
        }

        private bool _tornDown;

        /// <summary>Stops cycling and tears down the overlay cleanly - including its own host
        /// GameObject (Show() creates one dedicated to this component, so nothing else owns it).
        /// Safe to call multiple times.</summary>
        public void Hide()
        {
            if (_tornDown) return;
            _tornDown = true;

            if (_cycleCoroutine != null)
            {
                StopCoroutine(_cycleCoroutine);
                _cycleCoroutine = null;
            }
            _canvasObj = null;
            _sigilImg = null;

            if (gameObject != null)
            {
                if (Application.isPlaying) Destroy(gameObject);
                else DestroyImmediate(gameObject);
            }
        }

        private void OnDestroy() => Hide();
    }
}
