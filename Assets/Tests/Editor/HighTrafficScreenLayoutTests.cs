using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Layout/geometry coverage for the five highest-traffic metagame screens (Home dock, Shop,
    /// Collection, DeckBuilder, CampaignMap). Same method as
    /// <see cref="EmpireBuildingDetailLayoutTests"/> and <see cref="TacticalPuzzleLayoutTests"/>:
    /// measure the BUILT hierarchy's world rects and Unity's depth-first paint order so a
    /// full-screen backdrop isn't flagged, but art that draws AFTER a button and overlaps it is.
    /// </summary>
    public class HighTrafficScreenLayoutTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private GameObject _databaseGo;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MoDHighTrafficLayout_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            CardDatabase.ResetForTests();
            _databaseGo = new GameObject("HighTrafficLayout_CardDatabase");
            _spawned.Add(_databaseGo);
            _databaseGo.AddComponent<CardDatabase>().Initialize();

            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }

            _spawned.Clear();
            TearDownCanvases();
            CardDatabase.ResetForTests();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        private static void TearDownCanvases()
        {
            foreach (Canvas c in Object.FindObjectsOfType<Canvas>())
            {
                if (c != null) Object.DestroyImmediate(c.gameObject);
            }
        }

        private static void RebuildAll(GameObject canvasGo)
        {
            if (canvasGo == null) return;
            foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Canvas.ForceUpdateCanvases();
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c.Min(v => v.x), xMax = c.Max(v => v.x);
            float yMin = c.Min(v => v.y), yMax = c.Max(v => v.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        /// <summary>
        /// Art with a sprite that draws AFTER a button and geometrically overlaps it can steal the
        /// tap. Backdrops that only sit behind buttons (earlier in depth-first order) are ignored.
        /// </summary>
        private static List<string> FindArtOverlappingButtons(GameObject canvasGo, string viewName)
        {
            var collisions = new List<string>();
            Transform canvas = canvasGo.transform;

            Transform[] drawOrder = canvas.GetComponentsInChildren<Transform>(true);
            var indexOf = new Dictionary<Transform, int>();
            for (int i = 0; i < drawOrder.Length; i++) indexOf[drawOrder[i]] = i;

            foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy) continue;
                RectTransform buttonRt = button.GetComponent<RectTransform>();
                if (buttonRt == null) continue;
                Rect btn = WorldRect(buttonRt);
                if (btn.width <= 0f || btn.height <= 0f) continue;
                if (!indexOf.TryGetValue(button.transform, out int buttonIndex)) continue;

                foreach (Image img in canvas.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite == null || !img.gameObject.activeInHierarchy) continue;
                    if (img.GetComponent<Button>() != null) continue;
                    if (img.transform.IsChildOf(button.transform)) continue;
                    if (!indexOf.TryGetValue(img.transform, out int artIndex)) continue;
                    if (artIndex <= buttonIndex) continue;

                    Rect art = WorldRect(img.rectTransform);
                    if (art.width <= 0f || art.height <= 0f) continue;
                    if (art.Overlaps(btn))
                        collisions.Add($"{viewName}: '{img.name}' {art} overlaps '{button.name}' {btn} and draws AFTER it");
                }
            }

            return collisions;
        }

        private static void AssertBuiltAndClearOfOverlaps(GameObject canvas, string screenName)
        {
            Assert.IsNotNull(canvas, screenName + " built no canvas.");
            Assert.Greater(canvas.GetComponentsInChildren<RectTransform>(true).Length, 5,
                screenName + " built a suspiciously empty screen.");
            Assert.Greater(canvas.GetComponentsInChildren<Button>(true).Length, 0,
                screenName + " has no buttons to protect — overlap check would be vacuous.");

            List<string> collisions = FindArtOverlappingButtons(canvas, screenName);
            CollectionAssert.IsEmpty(collisions,
                screenName + " art overlaps a button (draws AFTER it), so a tap would land on chrome: " +
                string.Join("  |  ", collisions));
        }

        // ------------------------------------------------------------------ Home dock

        [Test]
        public void HomeDock_ArtNeverOverlapsAButton()
        {
            var go = new GameObject("HomeLayoutHost");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            GameObject canvas = home.HomeCanvasObjectForTests;
            Assert.IsNotNull(canvas);
            canvas.GetComponent<RectTransform>().sizeDelta = new Vector2(1920f, 1080f);
            RebuildAll(canvas);

            AssertBuiltAndClearOfOverlaps(canvas, "Home");
        }

        // ------------------------------------------------------------------ Shop

        [Test]
        public void Shop_ArtNeverOverlapsAButton()
        {
            var profile = new PlayerProfile { gems = 500, stamina = 20, maxStamina = 100 };
            CollectionSchemaMigration.Apply(profile);

            var go = new GameObject("ShopLayoutHost");
            _spawned.Add(go);
            go.AddComponent<ShopPresenter>().Initialize(profile, onBackToHome: null);
            GameObject canvas = GameObject.Find("ShopCanvas");
            RebuildAll(canvas);

            AssertBuiltAndClearOfOverlaps(canvas, "Shop");
        }

        // ------------------------------------------------------------------ Collection

        [Test]
        public void Collection_ArtNeverOverlapsAButton()
        {
            var go = new GameObject("CollectionLayoutHost");
            _spawned.Add(go);
            go.AddComponent<CollectionPresenter>().Initialize(null, null);
            GameObject canvas = GameObject.Find("CollectionCanvas");
            RebuildAll(canvas);

            AssertBuiltAndClearOfOverlaps(canvas, "Collection");
        }

        // ------------------------------------------------------------------ DeckBuilder

        [Test]
        public void DeckBuilder_ArtNeverOverlapsAButton()
        {
            var go = new GameObject("DeckBuilderLayoutHost");
            _spawned.Add(go);
            go.AddComponent<DeckBuilderPresenter>().Initialize(onBackToHome: null);
            GameObject canvas = GameObject.Find("DeckBuilderCanvas");
            RebuildAll(canvas);

            AssertBuiltAndClearOfOverlaps(canvas, "DeckBuilder");
        }

        // ------------------------------------------------------------------ CampaignMap

        [Test]
        public void CampaignMap_ArtNeverOverlapsAButton()
        {
            var go = new GameObject("CampaignMapLayoutHost");
            _spawned.Add(go);
            var map = go.AddComponent<CampaignMapPresenter>();
            map.Initialize(onBackToHome: null, onLaunchBattle: _ => CampaignLaunchOutcome.BlockedLocked);
            GameObject canvas = GameObject.Find("CampaignMapCanvas");
            RebuildAll(canvas);

            AssertBuiltAndClearOfOverlaps(canvas, "CampaignMap");
        }

        [Test]
        public void AllFiveScreens_ActuallyBuildRealContent()
        {
            // Vacuous-guard: if a screen stops building, the overlap tests must not pass silently.
            HomeDock_ArtNeverOverlapsAButton();
            TearDownCanvases();

            Shop_ArtNeverOverlapsAButton();
            TearDownCanvases();

            Collection_ArtNeverOverlapsAButton();
            TearDownCanvases();

            DeckBuilder_ArtNeverOverlapsAButton();
            TearDownCanvases();

            CampaignMap_ArtNeverOverlapsAButton();
        }
    }
}
