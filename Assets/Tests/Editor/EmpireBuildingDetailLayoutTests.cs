using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Data;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Building-detail popup layout. Added 2026-08-25 for commit 150f32d, which wired the five new
    /// building renders into a previously-empty popup region and whose author verified "no overlap
    /// with the Upgrade/Requirements buttons by coordinate math, not visually".
    ///
    /// This asserts the same claim against the BUILT hierarchy instead. Re-deriving it from the
    /// same anchor constants would only confirm the arithmetic; measuring real world rects catches
    /// the case where a region's actual rect differs from its declared anchors - which is exactly
    /// what a padding, layout group or parent-size assumption can do.
    ///
    /// The clearances here are genuinely tight (~9px between the art region and the variant text at
    /// 1080), so this is worth a standing test rather than a one-off check.
    /// </summary>
    public class EmpireBuildingDetailLayoutTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDBuildingDetail_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            foreach (Canvas c in Object.FindObjectsOfType<Canvas>())
                if (c != null) Object.DestroyImmediate(c.gameObject);
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir)) Directory.Delete(_scratchSaveDir, true);
        }

        private EmpireBuildingDetailPresenter OpenDetail(EmpireBuildingKind kind)
        {
            var go = new GameObject("BuildingDetailHost_" + kind);
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);
            empire.OpenBuildingDetailForTests(kind);

            var detail = go.GetComponent<EmpireBuildingDetailPresenter>();
            Assert.IsNotNull(detail, "Setup: expected a building-detail presenter for " + kind);

            GameObject canvasGo = detail.CanvasObjectForTests;
            Assert.IsNotNull(canvasGo, "Setup: expected the detail popup to build a canvas.");
            foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
            return detail;
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c.Min(v => v.x), xMax = c.Max(v => v.x);
            float yMin = c.Min(v => v.y), yMax = c.Max(v => v.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        private static Transform FindDeep(Transform root, string name) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == name);

        /// <summary>Every building kind that got a render in 150f32d.</summary>
        private static readonly EmpireBuildingKind[] RenderedKinds =
        {
            EmpireBuildingKind.Storage,
            EmpireBuildingKind.TrainingGrounds,
            EmpireBuildingKind.Quarry,
            EmpireBuildingKind.Academy,
            EmpireBuildingKind.TreeOfKnowledge,
        };

        [Test]
        public void TheBuildingRender_NeverOverlapsAnInteractiveControl()
        {
            var collisions = new List<string>();

            foreach (EmpireBuildingKind kind in RenderedKinds)
            {
                EmpireBuildingDetailPresenter detail = OpenDetail(kind);
                Transform canvas = detail.CanvasObjectForTests.transform;

                // The art region is the only Image added by 150f32d; find it by its rect rather
                // than a name assumption, so a rename cannot silently skip this check.
                foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
                {
                    if (!button.gameObject.activeInHierarchy) continue;
                    Rect btn = WorldRect(button.GetComponent<RectTransform>());

                    foreach (Image img in canvas.GetComponentsInChildren<Image>(true))
                    {
                        if (img.sprite == null || !img.gameObject.activeInHierarchy) continue;
                        if (img.GetComponent<Button>() != null) continue;      // the button's own art
                        if (img.transform.IsChildOf(button.transform)) continue;

                        Rect art = WorldRect(img.rectTransform);
                        if (art.width <= 0f || art.height <= 0f) continue;
                        if (art.Overlaps(btn))
                            collisions.Add($"{kind}: '{img.name}' {art} overlaps '{button.name}' {btn}");
                    }
                }

                TearDownCanvases();
            }

            CollectionAssert.IsEmpty(collisions,
                "A building render overlaps an interactive control, so a tap would land on art " +
                "instead of the button: " + string.Join("  |  ", collisions));
        }

        [Test]
        public void EveryRenderedKind_ActuallyBuildsItsPopup()
        {
            // Guards the check above from silently measuring nothing - if OpenDetail ever stops
            // producing a canvas, the overlap test would pass vacuously.
            foreach (EmpireBuildingKind kind in RenderedKinds)
            {
                EmpireBuildingDetailPresenter detail = OpenDetail(kind);
                Assert.IsNotNull(detail.CanvasObjectForTests, kind + " built no popup.");
                Assert.Greater(detail.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true).Length, 5,
                    kind + " built a suspiciously empty popup.");
                TearDownCanvases();
            }
        }

        // ------------------------------------------------------------------ entry points + levels
        //
        // Added 2026-08-25. Five of eleven Empire buildings had detail popups, art and copy and NO
        // WAY TO BE TAPPED - all of that work was unreachable by a player. These pin the entry
        // points and the newly-persisted levels behind them.

        [Test]
        public void EveryRemainingStructure_HasARealEntryPointOnEmpire()
        {
            var go = new GameObject("EmpireEntryHost");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);

            Assert.AreEqual(RenderedKinds.Length, empire.StructureTileCountForTests,
                "Each of the five buildings must have its own tappable tile.");

            string[] names = Object.FindObjectsOfType<Button>(true).Select(b => b.name).ToArray();
            foreach (EmpireBuildingKind kind in RenderedKinds)
                CollectionAssert.Contains(names, "Structure_" + kind,
                    kind + " has no entry point, so its detail popup is unreachable.");
        }

        [Test]
        public void TappingAStructureTile_OpensThatBuildingsDetail()
        {
            // Guards against five tiles that all open the same popup - a mistake that looks
            // completely correct until you tap the second one.
            foreach (EmpireBuildingKind kind in RenderedKinds)
            {
                var go = new GameObject("EmpireTapHost_" + kind);
                _spawned.Add(go);
                var empire = go.AddComponent<EmpirePresenter>();
                empire.Initialize(onBackToHome: null);

                Button tile = Object.FindObjectsOfType<Button>(true)
                    .FirstOrDefault(b => b.name == "Structure_" + kind);
                Assert.IsNotNull(tile, "No tile for " + kind + ".");

                tile.onClick.Invoke();

                var detail = go.GetComponent<EmpireBuildingDetailPresenter>();
                Assert.IsNotNull(detail, "Tapping " + kind + " opened no detail popup.");
                Assert.AreEqual(kind, detail.KindForTests,
                    "Tapping " + kind + " opened the wrong building's detail.");

                TearDownCanvases();
                Object.DestroyImmediate(go);
                _spawned.Remove(go);
            }
        }

        [Test]
        public void EachStructure_ShowsItsRealStoredLevel_NotTheRuntimePlaceholder()
        {
            // Before the save fields existed these five could only render "[runtime]". That marker
            // is reserved for values genuinely not persisted - now that they ARE persisted, showing
            // it would be a lie.
            PlayerProfile profile = SaveManager.SaveData;
            Assert.IsNotNull(profile, "Setup: expected a live profile.");

            foreach (EmpireBuildingKind kind in RenderedKinds)
            {
                Assert.IsTrue(EmpireBuildingLevels.HasStoredLevel(kind),
                    kind + " must have a stored level field.");

                string line = EmpireBuildingDetailCopy.FormatLevelLine(kind, profile);
                StringAssert.DoesNotContain(EmpireBuildingDetailCopy.RuntimePlaceholder, line,
                    kind + " still renders the runtime placeholder despite having a save field.");
                StringAssert.Contains(
                    EmpireBuildingLevels.LevelOf(profile, kind).ToString(), line,
                    kind + " must display its actual stored level.");
            }
        }

        [Test]
        public void AFreshProfile_StartsTheseBuildingsAtLevelOne_NotZero()
        {
            // THE LOCKED DEFAULT, and the reason it is 1: these are minimum-valid structures, not
            // absent inventory. A 0 default would read as "not built" and could block a migrated
            // player - especially an existing Evolution user - behind a field that did not exist
            // when they last played.
            var fresh = new PlayerProfile();

            foreach (EmpireBuildingKind kind in RenderedKinds)
                Assert.AreEqual(1, EmpireBuildingLevels.LevelOf(fresh, kind),
                    kind + " must default to Level 1 for new AND migrated accounts.");
        }

        [Test]
        public void UnbackedBuildings_StillReportNoStoredLevel()
        {
            // Guards the helper from quietly claiming a level for buildings that genuinely have
            // none - Guild Hall is flat by design, Embassy and Prison are pending-server. 0 means
            // "no level field", never "level zero".
            foreach (EmpireBuildingKind kind in new[]
                     {
                         EmpireBuildingKind.Embassy, EmpireBuildingKind.Prison,
                         EmpireBuildingKind.GuildHall,
                     })
            {
                Assert.IsFalse(EmpireBuildingLevels.HasStoredLevel(kind),
                    kind + " has no save field and must not claim one.");
            }
        }

        private void TearDownCanvases()
        {
            foreach (Canvas c in Object.FindObjectsOfType<Canvas>())
                if (c != null) Object.DestroyImmediate(c.gameObject);
        }
    }
}
