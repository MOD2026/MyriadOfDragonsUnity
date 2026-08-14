using System;
using System.IO;
using MyriadOfDragons.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// HOME UI RENDER ISOLATION follow-up, 2026-08-15 - guards the two concrete rendering-safety
    /// gaps found by comparing UISharedFoundation's text factory against GameBootstrap's own
    /// proven CreateText: a legacy Text with a null font populates zero mesh vertices (registered,
    /// valid rect/color, but no visible glyphs), and Unity's default verticalOverflow (Truncate)
    /// silently clips text taller than its RectTransform instead of rendering it. Neither of these
    /// is directly visible in an EditMode test (no rendering happens outside Play Mode), but both
    /// are directly inspectable Text/Font properties, so this pins the fix without needing Play
    /// Mode to catch a regression.
    /// </summary>
    public class HomePageTextRenderingTests
    {
        private GameObject _spawned;
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            // Same isolation as HomePageTutorialRewardGuardTests - BuildHomePageUI() reads
            // SaveManager.SaveData (a facade over SaveSystem.CurrentProfile), which otherwise
            // touches the real machine's save file.
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (_spawned != null) UnityEngine.Object.DestroyImmediate(_spawned);

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                Directory.Delete(_scratchSaveDir, recursive: true);
            }
        }

        [Test]
        public void BuildHomePageUI_EveryTextHasANonNullFontAndDoesNotTruncateOverflow()
        {
            _spawned = new GameObject("HomePagePresenterUnderTest");
            var presenter = _spawned.AddComponent<HomePagePresenter>();

            presenter.BuildHomePageUIForTests();

            GameObject canvasObject = presenter.HomeCanvasObjectForTests;
            Assert.IsNotNull(canvasObject, "Setup: BuildHomePageUIForTests must create the Home canvas GameObject.");

            Text[] allText = canvasObject.GetComponentsInChildren<Text>(true);
            Assert.Greater(allText.Length, 0, "Setup: Home's UI must contain at least one Text element to check.");

            foreach (Text text in allText)
            {
                Assert.IsNotNull(text.font,
                    $"'{text.name}' has a null font - a legacy Text with no font renders zero " +
                    "visible glyphs even though its rect/color read as perfectly valid.");
                Assert.AreNotEqual(VerticalWrapMode.Truncate, text.verticalOverflow,
                    $"'{text.name}' uses Truncate vertical overflow - text taller than its " +
                    "RectTransform's height silently renders nothing instead of spilling over.");
            }
        }
    }
}
