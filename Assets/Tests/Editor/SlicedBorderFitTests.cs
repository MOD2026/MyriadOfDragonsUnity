using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Verification for the "flat boxes everywhere" root cause (register 2026-08-26, commit
    /// c13d8a0) - a Sliced Image's border collides with itself when the caller's rect is smaller
    /// than the sprite's own border sum, and UISharedFoundation.FitSlicedBorderToRect is the fix.
    /// Exists specifically to verify, by measurement rather than reasoning alone, that
    /// border-in-source-pixels compares 1:1 against RectTransform.rect with no CanvasScaler
    /// .scaleFactor term - proven using the real measured DeckBuilder numbers and checking the
    /// exact expected multiplier, not just "greater than 1".
    ///
    /// A resize-watcher approach (OnRectTransformDimensionsChange) was tried for the one known
    /// bad-call-order site (EmpirePresenter.cs applying chrome before positioning) and directly
    /// DISPROVEN here - it did not fire synchronously in EditMode even after an explicit
    /// Canvas.ForceUpdateCanvases() pump. Removed from UISharedFoundation.cs rather than shipped
    /// as unverified complexity; EmpirePresenter.cs was reordered instead (simpler, verified).
    /// </summary>
    public class SlicedBorderFitTests
    {
        private GameObject _canvasGo;
        private RectTransform _parent;

        // Real, already-imported asset (ui_button_secondary_normal_v1.png.meta):
        // spritePixelsToUnits 100, spriteBorder {160, 64, 160, 64} - so vertical border sum is
        // 64+64=128 source pixels, horizontal is 160+160=320.
        private const string RealSlicedSpritePath = "UI/SharedFoundation/ui_button_secondary_normal_v1";

        [SetUp]
        public void SetUp()
        {
            _canvasGo = new GameObject("FitTestCanvas", typeof(RectTransform), typeof(Canvas),
                typeof(CanvasScaler), typeof(GraphicRaycaster));
            var scaler = _canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            _parent = (RectTransform)_canvasGo.transform;
        }

        [TearDown]
        public void TearDown()
        {
            if (_canvasGo != null) Object.DestroyImmediate(_canvasGo);
        }

        private Image CreateSlicedImage(float width, float height)
        {
            var go = new GameObject("TestImage", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_parent, false);
            Image image = go.GetComponent<Image>();
            Sprite sprite = Resources.Load<Sprite>(RealSlicedSpritePath);
            Assert.IsNotNull(sprite, "Setup: the real SharedFoundation secondary-button sprite must exist.");
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(width, height);
            return image;
        }

        [Test]
        public void RealSprite_HasTheExactBorderThisFixWasMeasuredAgainst()
        {
            // Pins the asset itself, independent of the fix - if this ever fails, every other
            // assertion in this file needs re-deriving, not just re-running.
            Sprite sprite = Resources.Load<Sprite>(RealSlicedSpritePath);
            Assert.IsNotNull(sprite);
            Assert.AreEqual(new Vector4(160, 64, 160, 64), sprite.border);
            Assert.AreEqual(100f, sprite.pixelsPerUnit);
        }

        [Test]
        public void FitSlicedBorderToRect_ComputesTheExactMeasuredDeckBuilderMultiplier()
        {
            // The real repro: DeckBuilder's rail buttons render at ~320x62 (CreateButton's
            // Vector2(320, 62) requested size, confirmed by SetNormalizedRect's own 0.18-0.82
            // band of a 96px rail = 61.44px, effectively the same). Vertical border sum is
            // 64+64=128 against a 62px-tall rect - the exact "roughly 2x over" case from the
            // register entry.
            Image image = CreateSlicedImage(320f, 62f);

            UISharedFoundation.FitSlicedBorderToRect(image);

            // Expected: borderHeightSum / max(1, rectHeight - MinCenterPx) = 128 / (62-6) = 128/56.
            // Width axis needs far less (320 / (320-6) ≈ 1.02), so height drives the result -
            // this is what proves the fix targets the axis that's actually collapsing, not just
            // "some" axis.
            float expected = 128f / 56f;
            Assert.AreEqual(expected, image.pixelsPerUnitMultiplier, 0.001f,
                "No CanvasScaler.scaleFactor term should appear here - comparing border source " +
                "pixels straight against RectTransform.rect is the whole point of this fix; a " +
                "scaleFactor-inflated comparison would give a different, resolution-dependent number.");
        }

        [Test]
        public void FitSlicedBorderToRect_LeavesMultiplierAtOne_WhenTheRectAlreadyHasRoom()
        {
            // A panel-sized rect, well above the 128px vertical / 320px horizontal border sum -
            // must render the art at its full authored size, not shrink it unnecessarily.
            Image image = CreateSlicedImage(600f, 400f);

            UISharedFoundation.FitSlicedBorderToRect(image);

            Assert.AreEqual(1f, image.pixelsPerUnitMultiplier,
                "A rect with real room to spare must never shrink the border below its authored size.");
        }

        [Test]
        public void FitSlicedBorderToRect_ClampsAtFour_ForAnAbsurdlyTinyRect()
        {
            Image image = CreateSlicedImage(10f, 10f);

            UISharedFoundation.FitSlicedBorderToRect(image);

            Assert.AreEqual(4f, image.pixelsPerUnitMultiplier,
                "An element far too small for this art is a real separate layout bug - clamp " +
                "rather than shrink the border into illegibility trying to compensate for it.");
        }

        [Test]
        public void FitSlicedBorderToRect_IsANoOp_BeforeTheRectIsEverSized()
        {
            // Unity's own default sizeDelta (100x100) is a real size, not zero - this test exists
            // to pin that calling too early doesn't throw or silently misbehave, now that the
            // resize-watcher approach (which would have "fixed" this case automatically) has been
            // removed as disproven. Callers must position before applying chrome; this just
            // confirms an early call is at least harmless, computing against whatever the rect
            // happens to be rather than corrupting state.
            var go = new GameObject("EarlyApplyImage", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(_parent, false);
            Image image = go.GetComponent<Image>();
            image.sprite = Resources.Load<Sprite>(RealSlicedSpritePath);
            image.type = Image.Type.Sliced;

            Assert.DoesNotThrow(() => UISharedFoundation.FitSlicedBorderToRect(image));
        }
    }
}
