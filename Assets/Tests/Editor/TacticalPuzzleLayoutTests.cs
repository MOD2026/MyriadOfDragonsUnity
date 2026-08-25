using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Tactical Puzzle geometry. Added 2026-08-25 after 383d02d wired real art (entry shell, board
    /// frame, result modal, 3 tile states) into TacticalPuzzlePresenter with no standing regression
    /// coverage - the same gap EmpireBuildingDetailLayoutTests closed for the building popup right
    /// after ITS art landed, and the same class of bug that Home screen shipped with tonight (a
    /// tutorial banner overlapping a chip row) until a real test existed to catch it.
    ///
    /// Same method as EmpireBuildingDetailLayoutTests: measure the BUILT hierarchy's real world
    /// rects rather than re-deriving from the same anchor constants the presenter itself uses -
    /// that would only confirm the arithmetic, not catch a padding/parent-size surprise.
    /// </summary>
    public class TacticalPuzzleLayoutTests
    {
        private GameObject _host;
        private GameObject _databaseHost;
        private Card _light;
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), "MoDPuzzleLayout_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_scratchSaveDir);
            MyriadOfDragons.Save.SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            MyriadOfDragons.Save.SaveSystem.ResetCurrentProfileForTests();

            CardDatabase.ResetForTests();
            _databaseHost = new GameObject("PuzzleLayoutCardDatabase");
            CardDatabase db = _databaseHost.AddComponent<CardDatabase>();
            db.Initialize();

            _light = CardDatabase.Instance.AllCards
                .Where(c => c.SlotWeight == 1)
                .OrderBy(c => c.ResourceCost)
                .ThenBy(c => c.Id, System.StringComparer.Ordinal)
                .FirstOrDefault();
            Assert.IsNotNull(_light, "Setup: need one single-slot card.");
        }

        [TearDown]
        public void TearDown()
        {
            if (_host != null) Object.DestroyImmediate(_host);
            if (_databaseHost != null) Object.DestroyImmediate(_databaseHost);
            foreach (Canvas c in Object.FindObjectsOfType<Canvas>())
                if (c != null) Object.DestroyImmediate(c.gameObject);
            TacticalPuzzleLibrary.ClearPuzzlesForTests();
            TacticalPuzzleLibrary.ResetCacheForTests();
            CardDatabase.ResetForTests();

            MyriadOfDragons.Save.SaveSystem.ClearRootDirectoryOverride();
            MyriadOfDragons.Save.SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && System.IO.Directory.Exists(_scratchSaveDir))
                System.IO.Directory.Delete(_scratchSaveDir, true);
        }

        /// <summary>Same shape as TacticalPuzzlePresenterTests' own fixture - an empty Back lane so
        /// the outcome turns on whether the deploy landed, not on damage tuning.</summary>
        private TacticalPuzzleDefinition Puzzle(string id)
        {
            string cardId = _light.Id;
            return new TacticalPuzzleDefinition
            {
                PuzzleId = id,
                DisplayName = id,
                StartingResource = 99,
                ResourceCap = 99,
                AvatarHealth = 20,
                ActionBudget = 0,
                Hand = new List<string> { cardId, cardId },
                PlayerBoard = new List<TacticalPuzzleUnitSpec>
                {
                    new TacticalPuzzleUnitSpec { CardId = cardId, Lane = Lane.Front },
                    new TacticalPuzzleUnitSpec { CardId = cardId, Lane = Lane.Front },
                    new TacticalPuzzleUnitSpec { CardId = cardId, Lane = Lane.Front },
                },
                EnemyBoard = new List<TacticalPuzzleUnitSpec>
                {
                    new TacticalPuzzleUnitSpec { CardId = cardId, Lane = Lane.Front },
                },
                Objective = new TacticalPuzzleObjectiveSpec
                {
                    Kind = TacticalPuzzleObjectiveKind.ProtectLane,
                    ProtectedLane = Lane.Back,
                },
            };
        }

        private TacticalPuzzlePresenter Open(params TacticalPuzzleDefinition[] puzzles)
        {
            _host = new GameObject("PuzzleLayoutHost");
            var presenter = _host.AddComponent<TacticalPuzzlePresenter>();
            presenter.Initialize(puzzles);
            RebuildAll(presenter.CanvasObjectForTests);
            return presenter;
        }

        private static void RebuildAll(GameObject canvasGo)
        {
            if (canvasGo == null) return;
            foreach (RectTransform rt in canvasGo.GetComponentsInChildren<RectTransform>(true)
                         .OrderByDescending(r => r.GetComponentsInParent<Transform>(true).Length))
                LayoutRebuilder.ForceRebuildLayoutImmediate(rt);
        }

        private static Rect WorldRect(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);
            float xMin = c.Min(v => v.x), xMax = c.Max(v => v.x);
            float yMin = c.Min(v => v.y), yMax = c.Max(v => v.y);
            return new Rect(xMin, yMin, xMax - xMin, yMax - yMin);
        }

        /// <summary>Every Button on the canvas must never have non-button art drawn ON TOP of it -
        /// the same failure mode the settings-gear/banner bugs both were, just checked geometrically
        /// instead of by screenshot. A full-screen backdrop that geometrically overlaps everything
        /// (by design - it IS the background) is not a bug; what matters is Unity's real render/
        /// raycast order, so this only flags art that comes AFTER the button in a depth-first
        /// traversal of the hierarchy - meaning it draws on top and could actually intercept a tap.
        /// A naive rect-overlap check (no order awareness) flags every screen's own backdrop
        /// against every button on it, which is exactly what the first version of this test did.</summary>
        private static List<string> FindArtOverlappingButtons(GameObject canvasGo, string viewName)
        {
            var collisions = new List<string>();
            Transform canvas = canvasGo.transform;

            // GetComponentsInChildren returns depth-first hierarchy order, which is also the order
            // Unity draws in (and the order GraphicRaycaster resolves, topmost/last-drawn wins) -
            // so an element's position in this list stands in for its real paint order.
            Transform[] drawOrder = canvas.GetComponentsInChildren<Transform>(true);
            var indexOf = new Dictionary<Transform, int>();
            for (int i = 0; i < drawOrder.Length; i++) indexOf[drawOrder[i]] = i;

            foreach (Button button in canvas.GetComponentsInChildren<Button>(true))
            {
                if (!button.gameObject.activeInHierarchy) continue;
                Rect btn = WorldRect(button.GetComponent<RectTransform>());
                int buttonIndex = indexOf[button.transform];

                foreach (Image img in canvas.GetComponentsInChildren<Image>(true))
                {
                    if (img.sprite == null || !img.gameObject.activeInHierarchy) continue;
                    if (img.GetComponent<Button>() != null) continue;
                    if (img.transform.IsChildOf(button.transform)) continue;
                    if (indexOf[img.transform] <= buttonIndex) continue; // drawn before the button = safely behind it

                    Rect art = WorldRect(img.rectTransform);
                    if (art.width <= 0f || art.height <= 0f) continue;
                    if (art.Overlaps(btn))
                        collisions.Add($"{viewName}: '{img.name}' {art} overlaps '{button.name}' {btn} and draws AFTER it");
                }
            }

            return collisions;
        }

        [Test]
        public void EntryView_ArtNeverOverlapsAButton()
        {
            TacticalPuzzlePresenter presenter = Open(Puzzle("entry-layout"));
            Assert.AreEqual(TacticalPuzzleView.Entry, presenter.CurrentView, "Setup: expected Entry view.");

            List<string> collisions = FindArtOverlappingButtons(presenter.CanvasObjectForTests, "Entry");
            CollectionAssert.IsEmpty(collisions,
                "Entry-view art overlaps a button, so a tap would land on chrome instead: " +
                string.Join("  |  ", collisions));
        }

        [Test]
        public void BoardView_ArtNeverOverlapsAButton()
        {
            TacticalPuzzlePresenter presenter = Open(Puzzle("board-layout"));
            presenter.OpenSlot(0);
            RebuildAll(presenter.CanvasObjectForTests);
            Assert.AreEqual(TacticalPuzzleView.Board, presenter.CurrentView, "Setup: expected Board view.");

            List<string> collisions = FindArtOverlappingButtons(presenter.CanvasObjectForTests, "Board");
            CollectionAssert.IsEmpty(collisions,
                "Board-view art overlaps a button, so a tap would land on chrome instead: " +
                string.Join("  |  ", collisions));
        }

        [Test]
        public void ResultView_ArtNeverOverlapsAButton()
        {
            TacticalPuzzlePresenter presenter = Open(Puzzle("result-layout"));
            presenter.OpenSlot(0);
            presenter.ShowResult();
            RebuildAll(presenter.CanvasObjectForTests);
            Assert.AreEqual(TacticalPuzzleView.Result, presenter.CurrentView, "Setup: expected Result view.");

            List<string> collisions = FindArtOverlappingButtons(presenter.CanvasObjectForTests, "Result");
            CollectionAssert.IsEmpty(collisions,
                "Result-view art overlaps a button, so a tap would land on chrome instead: " +
                string.Join("  |  ", collisions));
        }

        [Test]
        public void AllThreeViews_ActuallyBuildRealContent()
        {
            // Guards the three checks above from silently measuring nothing - if a view ever stops
            // building real content, the overlap tests would pass vacuously.
            TacticalPuzzlePresenter presenter = Open(Puzzle("vacuous-guard"));

            Assert.Greater(presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true).Length, 5,
                "Entry view built a suspiciously empty screen.");

            presenter.OpenSlot(0);
            RebuildAll(presenter.CanvasObjectForTests);
            Assert.Greater(presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true).Length, 5,
                "Board view built a suspiciously empty screen.");

            presenter.ShowResult();
            RebuildAll(presenter.CanvasObjectForTests);
            Assert.Greater(presenter.CanvasObjectForTests.GetComponentsInChildren<RectTransform>(true).Length, 5,
                "Result view built a suspiciously empty screen.");
        }
    }
}
