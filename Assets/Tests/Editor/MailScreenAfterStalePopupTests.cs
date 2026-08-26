using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>Real-cause investigation (2026-08-26, CR, read-only diagnosis while polling on the
    /// batch lock): the Mail-screen "stuck/unresponsive" symptom was only ever inferred as the same
    /// orphan-canvas class as TacticalPuzzleCanvas/SoloCircuitCanvas (register line ~4569-4572,
    /// ~5291) - never reproduced or confirmed root-caused.
    ///
    /// That theory does NOT hold for Mail specifically: unlike TacticalPuzzleCanvas/SoloCircuitCanvas
    /// (which really were missing from CampaignMapPresenter.CleanupStaleMetagameCanvases's master
    /// list until this session added them), "MailInboxCanvas" was ALREADY in that list from the
    /// start. This test proves the orphan-popup path is a non-issue for Mail: opening a popup-band
    /// screen (GuildHallEntry, sortingOrder 40) over Empire and then jumping straight to Mail
    /// WITHOUT going through GuildHallEntry's own Back button (the exact shape of the theorized bug)
    /// still leaves Mail fully clean and interactive, because MailInboxPresenter.BuildUI() calls
    /// CleanupStaleMetagameCanvases() before building, which destroys the stale GuildHallEntryCanvas
    /// by name.
    ///
    /// If this test passes, the real root cause of any genuine Mail-freeze symptom is NOT the
    /// orphan-canvas bug class and lies elsewhere (not yet found by this read-only pass) - do not
    /// re-attribute a future Mail report to this theory without a new, actual finding.</summary>
    public class MailScreenAfterStalePopupTests
    {
        private readonly System.Collections.Generic.List<GameObject> _spawned =
            new System.Collections.Generic.List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsMailAfterStalePopup_" + System.Guid.NewGuid().ToString("N"));
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
        public void OpeningMail_AfterAnOrphanedGuildHallPopup_CleansTheOrphan_AndMailStaysInteractive()
        {
            var empireGo = new GameObject("EmpireForMailOrphanReach");
            _spawned.Add(empireGo);
            var empire = empireGo.AddComponent<EmpirePresenter>();
            empire.Initialize(onBackToHome: null);
            empire.OpenGuildHallEntryForTests();

            GameObject guildHallCanvas = GameObject.Find(GuildHallEntryPresenter.CanvasName);
            Assert.NotNull(guildHallCanvas,
                "Setup: Guild Hall must be open (simulating a popup left behind without its own Back button being pressed).");

            // Jump straight to Mail, exactly as a player would from Home - never routing back
            // through GuildHallEntryPresenter's own Back button/teardown.
            var mailGo = new GameObject("MailAfterOrphanReach");
            _spawned.Add(mailGo);
            var mail = mailGo.AddComponent<MailInboxPresenter>();
            mail.Initialize(onBack: null);

            Assert.IsNull(GameObject.Find(GuildHallEntryPresenter.CanvasName),
                "The stale Guild Hall popup canvas must be gone once Mail builds - Mail's own " +
                "CleanupStaleMetagameCanvases() call destroys it by name before building.");

            GameObject mailCanvas = mail.CanvasObjectForTests;
            Assert.NotNull(mailCanvas, "Mail must build its own canvas.");
            Assert.IsTrue(mailCanvas.activeInHierarchy, "Mail's canvas must be active.");

            Button back = mailCanvas.transform.Find("MailInboxHeader/Btn_Back")?.GetComponent<Button>();
            Assert.NotNull(back, "Setup: Mail must expose a real Back button.");

            bool backInvoked = false;
            mail.Initialize(onBack: () => backInvoked = true);
            mailCanvas = mail.CanvasObjectForTests;
            back = mailCanvas.transform.Find("MailInboxHeader/Btn_Back")?.GetComponent<Button>();
            back.onClick.Invoke();
            Assert.IsTrue(backInvoked,
                "Mail's Back button must actually fire its callback - proves the screen is genuinely " +
                "interactive after opening over a stale popup, not just visually present.");
        }
    }
}
