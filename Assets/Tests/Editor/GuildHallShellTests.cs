using System.IO;
using MyriadOfDragons.Metagame;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    public class GuildHallShellTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsGuildHallShell_" + System.Guid.NewGuid().ToString("N"));
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
        public void OpenValues_RefuseWhileUnset()
        {
            StringAssert.Contains("OPEN", GuildHallEntryOpenValues.StatusNote.ToUpperInvariant());
            Assert.AreEqual("[runtime]", GuildHallEntryOpenValues.RuntimePlaceholder);
        }

        [Test]
        public void Presenter_BuildsArtShell_AndActionsRefuse()
        {
            var go = new GameObject("GuildHallHarness");
            _spawned.Add(go);
            var presenter = go.AddComponent<GuildHallEntryPresenter>();
            presenter.Initialize(onBack: null);

            GameObject canvas = presenter.CanvasObjectForTests;
            Assert.NotNull(canvas);

            Assert.IsTrue(GuildHallUiLibrary.HasGuildHallV1Pack);
            Assert.AreEqual(GuildHallUiLibrary.FlatEntryPopupName,
                canvas.transform.Find("Background")?.GetComponent<Image>()?.sprite?.name);
            Assert.AreEqual(GuildHallEntryOpenValues.FlatNonUpgradeCopy,
                canvas.transform.Find("EntryPanel/FlatStatus")?.GetComponent<Text>()?.text);
            Assert.AreEqual("EXPEDITION",
                canvas.transform.Find("EntryPanel/Btn_EntryAction/Text")?.GetComponent<Text>()?.text);
        }

        [Test]
        public void Empire_GuildHallChip_OpensFlatEntryPopup()
        {
            var go = new GameObject("EmpireGuildHallReach");
            _spawned.Add(go);
            var empire = go.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);

            Button chip = empire.CanvasObjectForTests.transform
                .Find("EmpireConstructionRoot/VariantStrip/Chip_GuildHall")?.GetComponent<Button>();
            Assert.NotNull(chip);
            chip.onClick.Invoke();

            Assert.NotNull(go.GetComponent<GuildHallEntryPresenter>());
            Assert.NotNull(GameObject.Find(GuildHallEntryPresenter.CanvasName));
            Assert.IsTrue(GuildHallUiLibrary.HasGuildHallV1Pack);
        }

    }
}
