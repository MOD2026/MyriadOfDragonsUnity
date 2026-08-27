using System.IO;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// COVERAGE, not correctness — same discipline as GameBootstrapStateCoverageTests.
    ///
    /// AvatarPresenter has exactly ONE declared state reachable in normal play: HomePagePresenter's
    /// sole call site (OpenAvatar, HomePagePresenter.cs:1082-1114) always passes non-null
    /// onOpenEmpire and onOpenSpellLoadout callbacks, so the "button absent" branches for
    /// Btn_OpenEmpire / Btn_SpellLoadout are dead code from any real player's path — per this
    /// project's own criteria ("reachable in normal play — not every conditional") they are not
    /// declared states, and are deliberately not tested here. The "profile == null" fallback in
    /// BuildBody is likewise unreachable post-onboarding (SaveManager.SaveData is always populated
    /// by then) and is excluded for the same reason.
    ///
    /// What IS worth covering: whether the body actually renders the REAL live economy numbers a
    /// real profile computes (Cap / Turn-1 Resource / Start HP / Deck Slots, sourced from
    /// PlayerProfile.ApplyDataToEmpire — the same Empire reader Battle itself uses), rather than
    /// stale or miscomputed values. Nothing exercised that before this test.
    /// </summary>
    public class AvatarPresenterStateCoverageTests
    {
        private System.Collections.Generic.List<GameObject> _spawned = new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsAvatarCoverage_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        [Test]
        public void AvatarBody_RendersLiveEconomyFromRealProfile()
        {
            // Distinctive, non-default values so a stale/miscomputed render can't accidentally
            // match by coincidence with defaults.
            var profile = new PlayerProfile
            {
                playerName = "Coverage Sovereign",
                avatarLevel = 7,
                castleLevel = 4,
                barracksLevel = 3,
                gateLevel = 2,
            };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the profile.");
            SaveSystem.ResetCurrentProfileForTests();

            // Compute expected values the SAME way BuildBody does — via the real Empire reader,
            // not a hand-duplicated formula that could drift from production and paper over a bug.
            PlayerProfile expected = SaveManager.SaveData;
            expected.ApplyDataToEmpire();
            int expectedCap = expected.Empire.ResourceCap;
            int expectedTurn1 = expected.Empire.Turn1Resource;
            int expectedStartHp = expected.Empire.StartingAvatarHealth;
            int expectedDeckSlots = expected.Empire.DeckSlotCount;

            var go = new GameObject("Coverage_AvatarBody");
            _spawned.Add(go);
            AvatarPresenter presenter = go.AddComponent<AvatarPresenter>();
            presenter.Initialize(onBackToHome: null, onOpenEmpire: () => { }, onOpenSpellLoadout: () => { });

            Assert.IsNotNull(presenter.CanvasObjectForTests, "STATE UNREACHED: AvatarCanvas was not built.");

            Text nameText = presenter.CanvasObjectForTests.transform.Find("AvatarBody/AvatarName")?.GetComponent<Text>();
            Assert.IsNotNull(nameText, "STATE UNREACHED: AvatarName was not built.");
            StringAssert.Contains("COVERAGE SOVEREIGN", nameText.text,
                "STATE UNREACHED: AvatarName did not render the real saved player name.");

            Text levelText = presenter.CanvasObjectForTests.transform.Find("AvatarBody/AvatarLevel")?.GetComponent<Text>();
            Assert.IsNotNull(levelText, "STATE UNREACHED: AvatarLevel was not built.");
            StringAssert.Contains("7", levelText.text,
                "STATE UNREACHED: AvatarLevel did not render the real saved avatar level.");

            Text combatText = presenter.CanvasObjectForTests.transform.Find("AvatarBody/CombatStats")?.GetComponent<Text>();
            Assert.IsNotNull(combatText, "STATE UNREACHED: CombatStats was not built.");
            StringAssert.Contains($"Cap  {expectedCap}", combatText.text,
                "STATE UNREACHED: CombatStats did not render the live Empire resource cap.");
            StringAssert.Contains($"Turn-1 Resource  {expectedTurn1}", combatText.text,
                "STATE UNREACHED: CombatStats did not render the live Turn-1 resource.");
            StringAssert.Contains($"Start HP  {expectedStartHp}", combatText.text,
                "STATE UNREACHED: CombatStats did not render the live starting HP.");
            StringAssert.Contains($"Deck Slots  {expectedDeckSlots}", combatText.text,
                "STATE UNREACHED: CombatStats did not render the live deck slot count.");

            Text buildingText = presenter.CanvasObjectForTests.transform.Find("AvatarBody/BuildingContext")?.GetComponent<Text>();
            Assert.IsNotNull(buildingText, "STATE UNREACHED: BuildingContext was not built.");
            StringAssert.Contains("Castle L4", buildingText.text,
                "STATE UNREACHED: BuildingContext did not render the real saved Castle level.");
            StringAssert.Contains("Barracks L3", buildingText.text,
                "STATE UNREACHED: BuildingContext did not render the real saved Barracks level.");
            StringAssert.Contains("Gate L2", buildingText.text,
                "STATE UNREACHED: BuildingContext did not render the real saved Gate level.");
        }
    }
}
