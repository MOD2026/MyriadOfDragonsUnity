using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// PackOpenOverlay layout/geometry coverage - one of 16 screens with zero prior layout
    /// regression coverage. Unlike the other 15 (a MonoBehaviour presenter with its own Initialize),
    /// PackOpenOverlayPresenter is a static overlay built on top of an existing canvas - built here
    /// via the same real production path PackOpenOverlayTests already uses (a real
    /// ShopPresenter.PurchaseForTests gem-pack purchase), not a direct Show() call with hand-built
    /// inputs. Same method as EmpireBuildingDetailLayoutTests/TacticalPuzzleLayoutTests: measure the
    /// BUILT hierarchy's real world rects, only flag art that draws AFTER a button it geometrically
    /// overlaps (real depth-first paint/raycast order).
    /// </summary>
    public class PackOpenOverlayLayoutTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            CollectionPackReceiptService.ClearCommittedReceiptsForTests();
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDPackOpenLayout_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            CardDatabase.ResetForTests();
            var dbGo = new GameObject("PackOpenLayoutCardDatabase");
            _spawned.Add(dbGo);
            dbGo.AddComponent<CardDatabase>().Initialize();
        }

        [TearDown]
        public void TearDown()
        {
            CollectionPackReceiptService.ClearCommittedReceiptsForTests();
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            var shopCanvas = GameObject.Find("ShopCanvas");
            if (shopCanvas != null) Object.DestroyImmediate(shopCanvas);
            CardDatabase.ResetForTests();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
        }

        private GameObject OpenOverlayViaRealPurchase()
        {
            // Real bug found on first run: a raw `new PlayerProfile()` skips schema migration,
            // and the purchase silently produced no overlay (PackDrawContentForTests came back
            // null) - matching PackOpenOverlayTests' own NewMigratedProfile helper fixes it.
            var profile = new PlayerProfile { gems = 500 };
            CollectionSchemaMigration.Apply(profile);
            var go = new GameObject("PackOpenLayoutShopHost");
            _spawned.Add(go);
            var shop = go.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null);

            Assert.IsTrue(shop.PurchaseForTests(CollectionPackCatalog.SingleSigilSkuId),
                "Setup: gem-pack purchase must succeed to open the real overlay.");
            Assert.IsNotNull(shop.PackDrawContentForTests, "Setup: gem-pack purchase must open the reveal overlay.");

            // ShopPresenter has no CanvasObjectForTests accessor - its real canvas is a
            // GameObject named "ShopCanvas" (see ShopPresenter's own construction), the same
            // lookup PackOpenOverlayTests already uses in its own TearDown.
            GameObject shopCanvas = GameObject.Find("ShopCanvas");
            Assert.IsNotNull(shopCanvas, "Setup: expected a real ShopCanvas after Initialize.");

            foreach (RectTransform rt in shopCanvas.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            return shopCanvas;
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c.Min(v => v.x), xMax = c.Max(v => v.x);
            float yMin = c.Min(v => v.y), yMax = c.Max(v => v.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static Transform FindOverlayRoot(GameObject shopCanvas) =>
            shopCanvas.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == PackOpenOverlayPresenter.OverlayRootName);

        [Test]
        public void PackOpenOverlay_ActuallyBuildsOnPurchase()
        {
            GameObject shopCanvas = OpenOverlayViaRealPurchase();
            Transform overlay = FindOverlayRoot(shopCanvas);
            Assert.IsNotNull(overlay, "PackOpenOverlay built no overlay root on a real purchase.");
            Assert.Greater(overlay.GetComponentsInChildren<RectTransform>(true).Length, 3,
                "PackOpenOverlay built a suspiciously empty overlay.");
        }

        [Test]
        public void PackOpenOverlay_NeverDrawsArtOnTopOfAnInteractiveControl()
        {
            GameObject shopCanvas = OpenOverlayViaRealPurchase();
            Transform overlay = FindOverlayRoot(shopCanvas);
            Assert.IsNotNull(overlay, "Setup: expected a real overlay root.");

            // Same draw-order-aware check as the other 15 screens, but scoped to the overlay
            // subtree specifically - the overlay sits on top of the whole Shop canvas by design
            // (that IS the overlay), so checking against Shop's own buttons underneath it would
            // misfire on exactly the backdrop-is-supposed-to-cover-everything false positive this
            // whole check exists to avoid. Buttons/art INSIDE the overlay are the real surface.
            Transform[] drawOrder = overlay.GetComponentsInChildren<Transform>(true);
            var indexOf = new Dictionary<Transform, int>();
            for (int i = 0; i < drawOrder.Length; i++) indexOf[drawOrder[i]] = i;

            var collisions = new List<string>();
            foreach (Button button in overlay.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy) continue;
                Rect btn = WorldRect(button.GetComponent<RectTransform>());
                int buttonIndex = indexOf[button.transform];

                foreach (Image img in overlay.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite == null || !img.gameObject.activeInHierarchy) continue;
                    if (img.GetComponent<Button>() != null) continue;
                    if (img.transform.IsChildOf(button.transform)) continue;
                    if (indexOf[img.transform] <= buttonIndex) continue;

                    Rect art = WorldRect(img.rectTransform);
                    if (art.width <= 0f || art.height <= 0f) continue;
                    if (art.Overlaps(btn))
                        collisions.Add($"'{img.name}' {art} overlaps '{button.name}' {btn} and draws AFTER it");
                }
            }

            CollectionAssert.IsEmpty(collisions,
                "Art draws on top of an interactive control, so a tap would land on art instead of the button: " +
                string.Join("  |  ", collisions));
        }
    }
}
