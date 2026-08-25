using System;
using System.IO;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Block I — proves Option C weekly Permit claim is wired through real Home build
    /// (BuildHomePageUIForTests), not only the static helper.
    /// </summary>
    public class HomeWeeklyPermitClaimTests
    {
        private GameObject _spawned;
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsHomeWeeklyPermit_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            if (_spawned != null) UnityEngine.Object.DestroyImmediate(_spawned);
            _spawned = null;
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                try { Directory.Delete(_scratchSaveDir, true); }
                catch (IOException) { }
            }
        }

        [Test]
        public void BuildHome_CreatesWeeklyPermitStripAndClaimButton()
        {
            HomePagePresenter home = SpawnHome();
            home.BuildHomePageUIForTests();

            Transform strip = FindWeeklyStrip(home);
            Assert.NotNull(strip, "BuildHomePageUI must create WeeklyPermitStrip.");
            Assert.NotNull(strip.Find("ClaimWeeklyPermitsButton"),
                "BuildHomePageUI must create ClaimWeeklyPermitsButton.");
            Assert.NotNull(strip.Find("WeeklyPermitStatus"),
                "BuildHomePageUI must create WeeklyPermitStatus.");
            Assert.NotNull(strip.Find("ClaimWeeklyPermitsButton")?.GetComponent<Button>());
        }

        [Test]
        public void BuildHome_AutoClaimOnOpen_AdvancesBalanceAndWeekKey()
        {
            PlayerProfile profile = SaveManager.SaveData;
            CollectionSchemaMigration.Apply(profile);
            Assert.AreEqual(0, profile.ascensionPermitBalance);
            Assert.AreEqual(string.Empty, profile.ascensionPermitWeekKey);

            HomePagePresenter home = SpawnHome();
            home.BuildHomePageUIForTests();

            Assert.AreEqual(CollectionSchemaRules.AscensionPermitsPerTrustedWeek, profile.ascensionPermitBalance,
                "TryAutoClaimWeeklyPermitsOnHomeOpen must run through BuildHomePageUI.");
            Assert.AreEqual(CollectionAscensionPermits.ManualTrustedWeekKey, profile.ascensionPermitWeekKey);
            Assert.AreEqual(CollectionSchemaRules.AscensionPermitsPerTrustedWeek, profile.ascensionPermitsEarnedThisWeek);

            Text status = FindStatusText(home);
            Assert.NotNull(status);
            StringAssert.Contains("Granted", status.text);
        }

        [Test]
        public void SecondBuildHome_DoesNotDoubleGrant_Idempotent()
        {
            PlayerProfile profile = SaveManager.SaveData;
            CollectionSchemaMigration.Apply(profile);

            HomePagePresenter home = SpawnHome();
            home.BuildHomePageUIForTests();
            Assert.AreEqual(4, profile.ascensionPermitBalance);

            home.BuildHomePageUIForTests();

            Assert.AreEqual(4, profile.ascensionPermitBalance,
                "Second BuildHomePageUI must not grant another weekly allotment.");
            Assert.AreEqual(4, profile.ascensionPermitsEarnedThisWeek);
            Assert.AreEqual(CollectionAscensionPermits.ManualTrustedWeekKey, profile.ascensionPermitWeekKey);

            Text status = FindStatusText(home);
            Assert.NotNull(status);
            StringAssert.StartsWith("Already claimed this week.", status.text);
        }

        [Test]
        public void BuildHome_WhenHoardFull_ShowsHoardFullStatus_ClaimStillReachable()
        {
            PlayerProfile profile = SaveManager.SaveData;
            CollectionSchemaMigration.Apply(profile);
            profile.ascensionPermitBalance = CollectionSchemaRules.AscensionPermitHoardCap;

            HomePagePresenter home = SpawnHome();
            home.BuildHomePageUIForTests();

            Text status = FindStatusText(home);
            Assert.NotNull(status);
            StringAssert.Contains("Hoard full", status.text,
                "Home open at hoard cap must explain why weekly claim grants nothing.");

            Button claimButton = FindClaimButton(home);
            Assert.NotNull(claimButton, "Claim control must remain present — not a dead-end screen.");
            claimButton.onClick.Invoke();

            Assert.AreEqual(CollectionSchemaRules.AscensionPermitHoardCap, profile.ascensionPermitBalance,
                "Claim at hoard cap must not overflow the cap.");
            StringAssert.Contains("Hoard full", FindStatusText(home).text);
        }

        [Test]
        public void ClaimButton_AfterAutoClaim_Invoke_DoesNotDoubleGrant()
        {
            PlayerProfile profile = SaveManager.SaveData;
            CollectionSchemaMigration.Apply(profile);

            HomePagePresenter home = SpawnHome();
            home.BuildHomePageUIForTests();
            Assert.AreEqual(4, profile.ascensionPermitBalance);

            Button claimButton = FindClaimButton(home);
            Assert.NotNull(claimButton);
            claimButton.onClick.Invoke();

            Assert.AreEqual(4, profile.ascensionPermitBalance);
            Assert.AreEqual(4, profile.ascensionPermitsEarnedThisWeek);

            Text status = FindStatusText(home);
            Assert.NotNull(status);
            StringAssert.StartsWith("Already claimed this week.", status.text);
        }

        private HomePagePresenter SpawnHome()
        {
            _spawned = new GameObject("HomeWeeklyPermitHarness");
            return _spawned.AddComponent<HomePagePresenter>();
        }

        private static Transform FindWeeklyStrip(HomePagePresenter home)
        {
            GameObject canvas = home.HomeCanvasObjectForTests;
            Assert.NotNull(canvas);
            return canvas.transform.Find("TopHud/WeeklyPermitStrip");
        }

        private static Button FindClaimButton(HomePagePresenter home)
        {
            Transform strip = FindWeeklyStrip(home);
            return strip?.Find("ClaimWeeklyPermitsButton")?.GetComponent<Button>();
        }

        private static Text FindStatusText(HomePagePresenter home)
        {
            Transform strip = FindWeeklyStrip(home);
            return strip?.Find("WeeklyPermitStatus")?.GetComponent<Text>();
        }
    }
}
