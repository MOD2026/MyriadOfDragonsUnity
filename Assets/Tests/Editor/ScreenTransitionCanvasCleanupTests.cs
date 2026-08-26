using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>Real bug (owner screenshot, 2026-08-26): opening Guild Hall from Empire showed
    /// "GUILD HALL" and "EXPEDITION" (Guild Hall's own entry button label) visibly overlapping
    /// Empire's content underneath.
    ///
    /// First fix attempt (dcf9610) misdiagnosed this as Guild Hall needing to be a full-screen
    /// replacement - it hid Empire's canvas and added CleanupStaleMetagameCanvases() to Guild
    /// Hall's own build. That directly conflicted with MetagameNavigationSpineTests.
    /// OpenAndCloseGuildHallEntry (WH's sweep), which established Guild Hall as a POPUP over
    /// Empire - identical convention to EmpireBuildingDetailPresenter/TacticalPuzzlePresenter,
    /// where the screen underneath stays active and findable. Reverted.
    ///
    /// Real root cause: GuildHallUiLibrary.ApplyFullscreenShell sets preserveAspect=true on its
    /// art sprite, which can letterbox - leaving gaps where Empire's canvas visually bled through
    /// and stayed interactive underneath. Real fix: an always-opaque "Dimmer" layer behind the
    /// art, same pattern as EmpireBuildingDetailPresenter's own Dimmer, guaranteeing full coverage
    /// regardless of the art's aspect ratio.</summary>
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
        public void OpeningGuildHall_KeepsEmpireCanvasActiveUnderneath_PopupConvention()
        {
            var go = new GameObject("EmpireGuildHallTransitionReach");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);

            GameObject empireCanvas = empire.CanvasObjectForTests;
            Assert.NotNull(empireCanvas, "Setup: Empire must build its canvas.");

            empire.OpenGuildHallEntryForTests();

            GameObject guildHallCanvas = GameObject.Find(GuildHallEntryPresenter.CanvasName);
            Assert.NotNull(guildHallCanvas, "Guild Hall must build its own canvas.");
            Assert.IsTrue(empireCanvas.activeSelf,
                "Guild Hall is a popup over Empire (same convention as BuildingDetail) - " +
                "Empire's canvas must stay active underneath, not be hidden.");
            Assert.NotNull(GameObject.Find("EmpireCanvas"),
                "Empire's canvas must remain findable while Guild Hall is open on top of it.");
        }

        [Test]
        public void GuildHallDimmer_IsOpaqueAndFullyCoversTheCanvas()
        {
            // The real fix for the visible-overlap bug: a guaranteed-opaque backdrop, independent
            // of the art sprite's aspect ratio, so nothing underneath can bleed through.
            var go = new GameObject("GuildHallDimmerReach");
            _spawned.Add(go);
            var guildHall = go.AddComponent<GuildHallEntryPresenter>();
            guildHall.Initialize(onBack: null);

            Transform dimmer = guildHall.CanvasObjectForTests.transform.Find("Dimmer");
            Assert.NotNull(dimmer, "Guild Hall must have an opaque Dimmer layer, same pattern as EmpireBuildingDetailPresenter.");
            Image dimImg = dimmer.GetComponent<Image>();
            Assert.NotNull(dimImg);
            Assert.AreEqual(1f, dimImg.color.a, "Dimmer must be fully opaque - no letterbox gaps allowed to show through.");
            Assert.IsTrue(dimImg.raycastTarget, "Dimmer must block input to whatever is underneath.");

            RectTransform dimRect = (RectTransform)dimmer;
            Assert.AreEqual(Vector2.zero, dimRect.anchorMin);
            Assert.AreEqual(Vector2.one, dimRect.anchorMax);
        }

        [Test]
        public void ReturningFromGuildHall_DestroysGuildHallCanvas_EmpireUnaffected()
        {
            var go = new GameObject("EmpireGuildHallReturnReach");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);
            GameObject empireCanvas = empire.CanvasObjectForTests;
            empire.OpenGuildHallEntryForTests();

            GameObject guildHallCanvas = GameObject.Find(GuildHallEntryPresenter.CanvasName);
            Assert.NotNull(guildHallCanvas, "Setup: Guild Hall must be open.");
            Button back = guildHallCanvas.transform
                .Find("GuildHallHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back, "Setup: Guild Hall must expose a real Back button.");
            back.onClick.Invoke();

            Assert.IsNull(GameObject.Find(GuildHallEntryPresenter.CanvasName),
                "Guild Hall's canvas must be gone after its own Back button is pressed.");
            Assert.IsTrue(empireCanvas.activeSelf, "Empire's canvas was never hidden, so it stays active throughout.");
        }
    }
}
