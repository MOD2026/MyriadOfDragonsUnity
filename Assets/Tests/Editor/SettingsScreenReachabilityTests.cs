using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>Settings/Options utility screen — audio, notifications, language, logout.</summary>
    public class SettingsScreenReachabilityTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;
        private string _languageChangedCode;

        [SetUp]
        public void SetUp()
        {
            _languageChangedCode = null;
            RealtimeTranslationPreferences.LanguageChanged += OnLanguageChanged;

            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsSettingsReach_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            AudioListener.volume = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            RealtimeTranslationPreferences.LanguageChanged -= OnLanguageChanged;
            AudioListener.volume = 1f;

            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        private void OnLanguageChanged(string code) => _languageChangedCode = code;

        [Test]
        public void FromHome_SettingsGear_OpensSettings_AndBackReturnsHome()
        {
            HomePagePresenter home = SpawnAndBuildHome();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;

            Button settingsBtn = FindButton(homeCanvas, "TopHud/Btn_Settings");
            Assert.IsTrue(settingsBtn.interactable, "Home must expose a Settings entry control.");
            settingsBtn.onClick.Invoke();

            Assert.IsFalse(homeCanvas.activeSelf, "Opening Settings must hide Home.");
            SettingsPresenter settings = home.GetComponent<SettingsPresenter>();
            Assert.IsNotNull(settings, "Settings gear must attach SettingsPresenter.");
            Assert.IsNotNull(settings.CanvasObjectForTests, "Settings must have a real canvas.");
            Assert.IsTrue(settings.CanvasObjectForTests.activeInHierarchy);

            Button back = FindButton(settings.CanvasObjectForTests, "SettingsHeader/Btn_Back");
            back.onClick.Invoke();

            Assert.IsTrue(homeCanvas.activeSelf, "Backing out of Settings must restore Home.");
            Assert.IsNull(home.GetComponent<SettingsPresenter>(),
                "Leaving Settings must tear down SettingsPresenter.");
        }

        [Test]
        public void Settings_AudioToggle_Persists_AndAppliesAudioListenerVolume()
        {
            PlayerProfile profile = NewProfile();
            profile.settingsAudioEnabled = true;
            SaveSystem.Save(profile);

            SettingsPresenter settings = SpawnSettings(profile);
            Assert.AreEqual(1f, AudioListener.volume, "Audio ON must use full listener volume.");

            FindButton(settings.CanvasObjectForTests, "SettingsBody/AudioRow/Btn_Toggle").onClick.Invoke();
            Assert.IsFalse(profile.settingsAudioEnabled, "Audio toggle must flip profile field.");
            Assert.AreEqual(0f, AudioListener.volume, "Audio OFF must mute listener volume.");

            SaveSystem.ResetCurrentProfileForTests();
            PlayerProfile reloaded = SaveSystem.LoadOrCreate();
            Assert.IsFalse(reloaded.settingsAudioEnabled, "Audio preference must persist to save.");
        }

        [Test]
        public void Settings_LanguagePreference_Cycles_AndNotifiesTranslationHook()
        {
            PlayerProfile profile = NewProfile();
            profile.preferredLanguageCode = "en";
            SaveSystem.Save(profile);

            SettingsPresenter settings = SpawnSettings(profile);
            Assert.AreEqual("en", RealtimeTranslationPreferences.CurrentLanguageCode);

            FindButton(settings.CanvasObjectForTests, "SettingsBody/LanguageRow/Btn_CycleLanguage").onClick.Invoke();

            Assert.AreEqual("es", profile.preferredLanguageCode,
                "Language CHANGE must advance to the next supported code.");
            Assert.AreEqual("es", RealtimeTranslationPreferences.CurrentLanguageCode);
            Assert.AreEqual("es", _languageChangedCode,
                "Translation hook must fire when language preference changes.");

            SaveSystem.ResetCurrentProfileForTests();
            PlayerProfile reloaded = SaveSystem.LoadOrCreate();
            Assert.AreEqual("es", reloaded.preferredLanguageCode, "Language must persist to save.");
        }

        [Test]
        public void Settings_LogoutAction_SurfacesStatus_WithoutDeadEnding()
        {
            PlayerProfile profile = NewProfile();
            SettingsPresenter settings = SpawnSettings(profile);

            FindButton(settings.CanvasObjectForTests, "SettingsBody/LogoutRow/Btn_Logout").onClick.Invoke();

            Assert.IsFalse(string.IsNullOrEmpty(settings.StatusTextForTests),
                "Logout must surface a status line instead of dead-ending silently.");
        }

        private HomePagePresenter SpawnAndBuildHome()
        {
            var go = new GameObject("HomePagePresenter_SettingsReach");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            return home;
        }

        private SettingsPresenter SpawnSettings(PlayerProfile profile)
        {
            SaveSystem.CurrentProfile = profile;
            SaveSystem.Save(profile);
            var go = new GameObject("SettingsHarness");
            _spawned.Add(go);
            var settings = go.AddComponent<SettingsPresenter>();
            settings.Initialize(onBackToHome: null);
            return settings;
        }

        private static PlayerProfile NewProfile() => new PlayerProfile();

        private static Button FindButton(GameObject canvas, string path)
        {
            Transform target = canvas.transform.Find(path);
            Assert.NotNull(target, $"Missing expected control at '{path}'.");
            Button button = target.GetComponent<Button>();
            Assert.NotNull(button, $"Expected a Button component on '{path}'.");
            return button;
        }
    }
}
