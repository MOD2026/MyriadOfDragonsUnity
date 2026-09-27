using System.Collections.Generic;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>PRODUCTIVE CODING TASK - Loading Sigil v3. Covers the new reusable
    /// LoadingSigilOverlay: that all 8 approved frames actually resolve from Resources, that
    /// SetFrameForTests walks them in strict 01-&gt;08 order (wrapping), and that Hide() leaves no
    /// GameObject or sprite reference behind - EditMode-safe, no reliance on a ticking Update
    /// loop (Application.isPlaying is false here, same gate LoadingSigilOverlay.Show uses to skip
    /// starting its real-time cycling coroutine).</summary>
    public class LoadingSigilOverlayTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
        }

        private GameObject NewHost(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            return go;
        }

        [Test]
        public void AllEightApprovedFrames_ExistInResources()
        {
            for (int i = 1; i <= LoadingSigilOverlay.FrameCount; i++)
            {
                string path = string.Format(LoadingSigilOverlay.FrameResourcePathFormat, i);
                Sprite frame = Resources.Load<Sprite>(path);
                Assert.IsNotNull(frame, $"Loading Sigil v3 frame {i:00} missing at '{path}' - packaging/import broken.");
            }
        }

        [Test]
        public void Show_BindsFrameOne_AsTheInitialFrame()
        {
            GameObject parent = NewHost("LoadingSigilTestParent_Initial");
            LoadingSigilOverlay overlay = LoadingSigilOverlay.Show(parent.transform);

            Assert.AreEqual(0, overlay.CurrentFrameIndexForTests);
            Sprite expectedFrame1 = Resources.Load<Sprite>(string.Format(LoadingSigilOverlay.FrameResourcePathFormat, 1));
            Assert.AreSame(expectedFrame1, overlay.CurrentSpriteForTests,
                "Show() must start on the exact approved frame 01 sprite, not a different/placeholder one.");

            overlay.Hide();
        }

        [Test]
        public void SetFrameForTests_WalksAllEightFramesInStrictOrder()
        {
            GameObject parent = NewHost("LoadingSigilTestParent_Order");
            LoadingSigilOverlay overlay = LoadingSigilOverlay.Show(parent.transform);

            for (int i = 0; i < LoadingSigilOverlay.FrameCount; i++)
            {
                overlay.SetFrameForTests(i);
                Assert.AreEqual(i, overlay.CurrentFrameIndexForTests, $"Frame index mismatch at step {i}.");

                Sprite expected = Resources.Load<Sprite>(string.Format(LoadingSigilOverlay.FrameResourcePathFormat, i + 1));
                Assert.IsNotNull(expected, $"Setup: frame {i + 1:00} must exist for this ordering check to be meaningful.");
                Assert.AreSame(expected, overlay.CurrentSpriteForTests,
                    $"SetFrameForTests({i}) must bind the exact approved frame {(i + 1):00} sprite, in order.");
            }

            overlay.Hide();
        }

        [Test]
        public void SetFrameForTests_WrapsPastTheLastFrame_BackToFrameOne()
        {
            GameObject parent = NewHost("LoadingSigilTestParent_Wrap");
            LoadingSigilOverlay overlay = LoadingSigilOverlay.Show(parent.transform);

            overlay.SetFrameForTests(LoadingSigilOverlay.FrameCount);
            Assert.AreEqual(0, overlay.CurrentFrameIndexForTests,
                "Cycling past frame 08 must wrap back to frame 01 (0-based index 0), not stop or go out of range.");

            overlay.Hide();
        }

        /// <summary>Looping behavior: the sigil must keep repeating 01-&gt;08-&gt;01-&gt;08... forever,
        /// not stop after one pass. Drives three full loops (24 steps) and confirms every step
        /// lands on the exact expected frame, by identity against an independent Resources.Load.</summary>
        [Test]
        public void SetFrameForTests_LoopsContinuouslyAcrossMultiplePasses()
        {
            GameObject parent = NewHost("LoadingSigilTestParent_Loop");
            LoadingSigilOverlay overlay = LoadingSigilOverlay.Show(parent.transform);

            const int loops = 3;
            for (int step = 0; step < LoadingSigilOverlay.FrameCount * loops; step++)
            {
                overlay.SetFrameForTests(step);
                int expectedIndex = step % LoadingSigilOverlay.FrameCount;
                Assert.AreEqual(expectedIndex, overlay.CurrentFrameIndexForTests,
                    $"Loop step {step} (pass {step / LoadingSigilOverlay.FrameCount + 1}) landed on the wrong frame index.");

                Sprite expected = Resources.Load<Sprite>(string.Format(LoadingSigilOverlay.FrameResourcePathFormat, expectedIndex + 1));
                Assert.AreSame(expected, overlay.CurrentSpriteForTests,
                    $"Loop step {step} did not bind the exact expected frame sprite - looping must never desync from the real frame set.");
            }

            overlay.Hide();
        }

        [Test]
        public void Show_BuildsANonRaycastingFullscreenCanvas_ThatDoesNotInterceptInput()
        {
            GameObject parent = NewHost("LoadingSigilTestParent_Raycast");
            LoadingSigilOverlay overlay = LoadingSigilOverlay.Show(parent.transform);

            GameObject canvasObj = overlay.CanvasObjectForTests;
            Assert.IsNotNull(canvasObj, "Show() must build a real canvas GameObject.");

            GraphicRaycaster raycaster = canvasObj.GetComponent<GraphicRaycaster>();
            Assert.IsNotNull(raycaster, "Setup: expected a GraphicRaycaster on the sigil canvas.");
            Assert.IsFalse(raycaster.enabled,
                "Decorative loading art must never intercept input, same rule every other full-screen backdrop follows.");

            Image sigilImage = canvasObj.GetComponentInChildren<Image>();
            Assert.IsNotNull(sigilImage, "Setup: expected the Sigil Image to exist.");
            Assert.IsFalse(sigilImage.raycastTarget, "The sigil Image itself must not be a raycast target.");

            overlay.Hide();
        }

        [Test]
        public void Hide_DestroysTheOverlayCanvas_AndClearsTheSpriteReference()
        {
            GameObject parent = NewHost("LoadingSigilTestParent_Teardown");
            LoadingSigilOverlay overlay = LoadingSigilOverlay.Show(parent.transform);
            GameObject canvasObj = overlay.CanvasObjectForTests;
            Assert.IsNotNull(canvasObj);

            overlay.Hide();

            Assert.IsNull(overlay.CanvasObjectForTests, "Hide() must clear the canvas reference.");
            Assert.IsNull(overlay.CurrentSpriteForTests, "Hide() must clear the bound sprite reference.");
            Assert.IsTrue(canvasObj == null, "Hide() must actually destroy the canvas GameObject (Unity null check), not just drop the reference.");
        }

        [Test]
        public void Hide_IsSafeToCallMultipleTimes()
        {
            GameObject parent = NewHost("LoadingSigilTestParent_DoubleHide");
            LoadingSigilOverlay overlay = LoadingSigilOverlay.Show(parent.transform);

            overlay.Hide();
            Assert.DoesNotThrow(() => overlay.Hide(), "A second Hide() call must be a safe no-op, not throw.");
        }

        [Test]
        public void DestroyingTheHost_TearsDownTheOverlayCanvasToo()
        {
            GameObject parent = NewHost("LoadingSigilTestParent_HostDestroy");
            LoadingSigilOverlay overlay = LoadingSigilOverlay.Show(parent.transform);
            GameObject canvasObj = overlay.CanvasObjectForTests;
            Assert.IsNotNull(canvasObj);

            Object.DestroyImmediate(overlay.gameObject);

            Assert.IsTrue(canvasObj == null,
                "OnDestroy must tear the sigil canvas down when the overlay's own host is destroyed - no leaked GameObject.");
        }
    }
}
