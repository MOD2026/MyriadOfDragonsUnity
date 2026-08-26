using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>Real bug (owner screenshot, 2026-08-26): GuildHallEntryPresenter opened from
    /// EmpirePresenter without hiding Empire's own canvas first, and without calling
    /// CleanupStaleMetagameCanvases() on its own entry - EmpireCanvas stayed active underneath
    /// GuildHallEntryCanvas, both fully interactive and rendering at once. Fixed in
    /// EmpirePresenter.OpenGuildHallEntry() (hide/restore, matching HomePagePresenter's own
    /// working pattern) + GuildHallEntryPresenter.BuildUI() (added the same cleanup call every
    /// sibling presenter already has). These tests check the TRANSITION, not just each screen in
    /// isolation - existing shell tests build one screen and never catch a leftover canvas left
    /// behind by a DIFFERENT screen.</summary>
    public class ScreenTransitionCanvasCleanupTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsScreenTransition_" + System.Guid.NewGuid().ToString("N"));
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

        [Test]
        public void OpeningGuildHall_HidesEmpireCanvas_NotBothVisibleAtOnce()
        {
            var go = new GameObject("EmpireGuildHallTransitionReach");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);

            GameObject empireCanvas = empire.CanvasObjectForTests;
            Assert.NotNull(empireCanvas, "Setup: Empire must build its canvas.");
            Assert.IsTrue(empireCanvas.activeSelf, "Setup: Empire canvas starts active.");

            empire.OpenGuildHallEntryForTests();

            GameObject guildHallCanvas = GameObject.Find(GuildHallEntryPresenter.CanvasName);
            Assert.NotNull(guildHallCanvas, "Guild Hall must build its own canvas.");
            Assert.IsFalse(empireCanvas.activeSelf,
                "Real bug: Empire's canvas must be hidden while Guild Hall is open - " +
                "both were rendering and both were interactive at once (owner screenshot).");
            Assert.IsNull(GameObject.Find("EmpireCanvas"),
                "Empire's canvas must not be independently findable/active while Guild Hall owns the screen.");
        }

        [Test]
        public void ReturningFromGuildHall_RestoresEmpireCanvas_AndDestroysGuildHallCanvas()
        {
            var go = new GameObject("EmpireGuildHallReturnReach");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);
            GameObject empireCanvas = empire.CanvasObjectForTests;
            empire.OpenGuildHallEntryForTests();

            GameObject guildHallCanvas = GameObject.Find(GuildHallEntryPresenter.CanvasName);
            Assert.NotNull(guildHallCanvas, "Setup: Guild Hall must be open.");
            UnityEngine.UI.Button back = guildHallCanvas.transform
                .Find("GuildHallHeader/Btn_Back")?.GetComponent<UnityEngine.UI.Button>();
            Assert.NotNull(back, "Setup: Guild Hall must expose a real Back button.");
            back.onClick.Invoke();

            Assert.IsNull(GameObject.Find(GuildHallEntryPresenter.CanvasName),
                "Guild Hall's canvas must be gone after its own Back button is pressed.");
            Assert.IsTrue(empireCanvas.activeSelf,
                "Empire's canvas must be reactivated on return, not left permanently hidden.");
        }

        [Test]
        public void GuildHallEntry_OwnBuild_CleansUpAnyOtherStaleActiveCanvas()
        {
            // Simulates a leftover active canvas from some unrelated prior screen (the general
            // class of bug the owner asked to audit for, not just the Empire-specific case).
            GameObject stray = new GameObject("MailInboxCanvas");
            _spawned.Add(stray);

            var go = new GameObject("GuildHallDirectReach");
            _spawned.Add(go);
            var guildHall = go.AddComponent<GuildHallEntryPresenter>();
            guildHall.Initialize(onBack: null);

            Assert.IsNull(GameObject.Find("MailInboxCanvas"),
                "GuildHallEntryPresenter.BuildUI must clean up stale canvases on entry, same as every sibling presenter.");
        }
    }
}
