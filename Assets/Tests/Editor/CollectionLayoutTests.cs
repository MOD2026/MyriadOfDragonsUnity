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
    /// Collection layout/geometry after design-token rollout batch 1 (5f204a5). Same method as
    /// <see cref="EmpireBuildingDetailLayoutTests"/> / <see cref="TacticalPuzzleLayoutTests"/>:
    /// measure BUILT world rects via GetWorldCorners; only flag sprite art that draws AFTER a
    /// button it geometrically overlaps (depth-first paint order). Full-screen framed backdrops
    /// that sit behind controls are ignored by design.
    /// </summary>
    public class CollectionLayoutTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private GameObject _databaseGo;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MoDCollectionLayout_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();

            CardDatabase.ResetForTests();
            _databaseGo = new GameObject("CollectionLayout_CardDatabase");
            _spawned.Add(_databaseGo);
            _databaseGo.AddComponent<CardDatabase>().Initialize();

            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
        }

        [TearDown]
        public void TearDown()
        {
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
            foreach (Canvas c in Object.FindObjectsOfType<Canvas>())
            {
                if (c != null) Object.DestroyImmediate(c.gameObject);
            }
            CardDatabase.ResetForTests();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        private CollectionPresenter Open()
        {
            var go = new GameObject("CollectionLayoutHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<CollectionPresenter>();
            presenter.Initialize(onBackToHome: null, onOpenDeckBuilder: null);
            foreach (RectTransform rt in presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            Canvas.ForceUpdateCanvases();
            return presenter;
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c.Min(v => v.x), xMax = c.Max(v => v.x);
            float yMin = c.Min(v => v.y), yMax = c.Max(v => v.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        [Test]
        public void Collection_ActuallyBuildsItsCanvas()
        {
            CollectionPresenter presenter = Open();
            Assert.IsNotNull(presenter.CanvasObjectForTests, "Collection built no canvas.");
            Assert.Greater(presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true).Length, 3,
                "Collection built a suspiciously empty canvas.");
        }

        [Test]
        public void Collection_NeverDrawsArtOnTopOfAnInteractiveControl()
        {
            // DRAW ORDER MATTERS, NOT BARE OVERLAP. Token rollout gave framed panels real sprites;
            // the Empire construction-panel / Btn_Back bug was invisible until that happened.
            CollectionPresenter presenter = Open();
            Transform canvas = presenter.CanvasObjectForTests.transform;

            Transform[] drawOrder = canvas.GetComponentsInChildren<Transform>(true);
            var indexOf = new Dictionary<Transform, int>();
            for (int i = 0; i < drawOrder.Length; i++) indexOf[drawOrder[i]] = i;

            var collisions = new List<string>();
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
                        collisions.Add($"'{img.name}' {art} overlaps '{button.name}' {btn} and draws AFTER it");
                }
            }

            CollectionAssert.IsEmpty(collisions,
                "Art draws on top of an interactive control, so a tap would land on art instead of the button: " +
                string.Join("  |  ", collisions));
        }
    }
}
