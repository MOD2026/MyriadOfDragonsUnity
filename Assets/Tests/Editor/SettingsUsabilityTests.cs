using System.IO;
using System.Threading.Tasks;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>WH-UI-SETTINGS-USABILITY-001 — Back hit target + selectable language list.</summary>
    public class SettingsUsabilityTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MoDSettingsUsability_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
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
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
        }

        private SettingsPresenter Open()
        {
            var go = new GameObject("SettingsUsabilityHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<SettingsPresenter>();
            presenter.Initialize(onBackToHome: null);
            return presenter;
        }

        [Test]
        public void BackButton_HasLandscapeHitArea_AndIsTopmostInHeader()
        {
            SettingsPresenter presenter = Open();
            Transform back = presenter.CanvasObjectForTests.transform.Find("SettingsHeader/Btn_Back");
            Assert.NotNull(back);
            RectTransform rect = back.GetComponent<RectTransform>();
            Assert.GreaterOrEqual(rect.sizeDelta.x, 200f);
            Assert.GreaterOrEqual(rect.sizeDelta.y, 72f);
            Assert.AreEqual(presenter.CanvasObjectForTests.transform.Find("SettingsHeader").childCount - 1,
                back.GetSiblingIndex(),
                "BACK must be last sibling so title text cannot steal clicks.");
            Assert.IsFalse(presenter.CanvasObjectForTests.transform.Find("SettingsHeader/Title")
                .GetComponent<Text>().raycastTarget);
        }

        [Test]
        public async Task BackButton_ReturnsViaCallback()
        {
            bool backFired = false;
            var go = new GameObject("SettingsUsabilityBackHost");
            _spawned.Add(go);
            var presenter = go.AddComponent<SettingsPresenter>();
            presenter.Initialize(onBackToHome: () => backFired = true);
            await presenter.WaitForOpenTransitionForTests();
            await presenter.PressBackForTests();
            Assert.IsTrue(backFired);
            Assert.IsNull(presenter.CanvasObjectForTests);
        }

        [Test]
        public void LanguageOptions_MatchSupportedCatalog_AndShowCurrentSelection()
        {
            PlayerProfile profile = SaveSystem.LoadOrCreate();
            profile.preferredLanguageCode = "ja";
            SaveSystem.Save(profile);

            SettingsPresenter presenter = Open();
            Transform options = presenter.CanvasObjectForTests.transform
                .Find("SettingsBody/LanguageRow/LanguageOptions");
            Assert.NotNull(options);
            Assert.AreEqual(PlayerSettingsCatalog.SupportedLanguages.Length, options.childCount);

            foreach (PlayerSettingsCatalog.LanguageOption option in PlayerSettingsCatalog.SupportedLanguages)
            {
                string id = option.Code.Replace('-', '_');
                Assert.NotNull(options.Find($"Btn_Language_{id}"),
                    $"Missing selectable language control for {option.Code}.");
            }

            Text value = presenter.CanvasObjectForTests.transform
                .Find("SettingsBody/LanguageRow/Value").GetComponent<Text>();
            StringAssert.Contains("日本語", value.text);
            StringAssert.Contains("ja", value.text);
            Assert.IsNull(presenter.CanvasObjectForTests.transform
                .Find("SettingsBody/LanguageRow/Btn_CycleLanguage"),
                "Opaque CHANGE cycle control must be removed.");
        }
    }
}
