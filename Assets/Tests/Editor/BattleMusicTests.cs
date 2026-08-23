using System.Collections.Generic;
using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// BATTLE MUSIC, 2026-08-15 - proves the vertical slice's rules through the real production
    /// paths (Initialize -> StartNewMatch, StartApprovedTutorialBattle, OnReturnToCityPressed via
    /// ReturnToCityForTests): music starts when a battle becomes active, stops on return to City,
    /// and Retry/Reset never create a second, layered AudioSource.
    /// </summary>
    public class BattleMusicTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsTests_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) UnityEngine.Object.DestroyImmediate(go);
            }
            _spawned.Clear();

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                Directory.Delete(_scratchSaveDir, recursive: true);
            }
        }

        private GameBootstrap SpawnAndInitializeBootstrap(string name)
        {
            var go = new GameObject(name);
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null) _spawned.Add(spawned);
            }
            return bootstrap;
        }

        [Test]
        public void NormalBattleBecomingActive_StartsMusicWithExactlyOneSource()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Music_NormalStartBootstrap");

            Assert.IsTrue(bootstrap.BattleMusicIsPlayingForTests,
                "Battle music must be playing once a normal battle (Initialize's own StartNewMatch) is active.");
            Assert.AreEqual(1, bootstrap.BattleMusicSourceCountForTests,
                "Exactly one AudioSource must exist for battle music.");
        }

        [Test]
        public void RetryAndResetLineup_DoNotCreateOrLayerASecondMusicSource()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Music_RetryNoDuplicateBootstrap");
            Assert.IsTrue(bootstrap.BattleMusicIsPlayingForTests, "Setup: expected music already playing after boot.");

            bootstrap.ResetLineupForTests();
            Assert.IsTrue(bootstrap.BattleMusicIsPlayingForTests, "Reset Lineup must not stop the music.");
            Assert.AreEqual(1, bootstrap.BattleMusicSourceCountForTests,
                "Reset Lineup must not create a second, layered AudioSource.");

            bootstrap.UseRecommendedLineupForTests();
            Assert.IsTrue(bootstrap.BattleMusicIsPlayingForTests, "Recommended Lineup must not stop the music.");
            Assert.AreEqual(1, bootstrap.BattleMusicSourceCountForTests,
                "Recommended Lineup must not create a second, layered AudioSource.");
        }

        [Test]
        public void ReturnToCity_StopsMusic_AndStartingAgainResumesWithoutLayering()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Music_ReturnToCityBootstrap");
            Assert.IsTrue(bootstrap.BattleMusicIsPlayingForTests, "Setup: expected music already playing after boot.");

            bootstrap.ReturnToCityForTests();
            Assert.IsFalse(bootstrap.BattleMusicIsPlayingForTests, "Returning to City must stop the battle music.");

            // Real production flow: Home only ever calls StartApprovedTutorialBattle/StartNewMatch
            // again after re-showing the battle canvas via SetBattleCanvasVisible(true) - the
            // canvas (and this AudioSource, its child) was deactivated by ReturnToCityForTests
            // above, so this mirrors that handoff rather than an unreachable direct call.
            bootstrap.SetBattleCanvasVisible(true);
            bootstrap.StartApprovedTutorialBattle();
            Assert.IsTrue(bootstrap.BattleMusicIsPlayingForTests, "A tutorial battle becoming active must (re)start the music.");
            Assert.AreEqual(1, bootstrap.BattleMusicSourceCountForTests,
                "Starting again must reuse the single existing AudioSource, never add a second one.");
        }

        /// <summary>
        /// GROUP B/A ISOLATION LOCK, 2026-08-21 (Command Centre vertical-slice task) - unlike the
        /// cinematic system, battle music was always deliberately live across every match type
        /// (PlayBattleMusicIfNeeded is called unconditionally from both StartNewMatch and
        /// StartApprovedTutorialBattle, with no IsTutorialMatch/campaign gate at all) and is
        /// already the accepted, tested behaviour for normal Battle
        /// (NormalBattleBecomingActive_StartsMusicWithExactlyOneSource above) - so this is not a
        /// Group B leak to close, just a coverage gap: the Campaign entry path specifically was
        /// never directly proven before this task. Locking it here so a future change cannot
        /// silently break music for Campaign (the live Chapter 1 vertical slice) without a normal
        /// or tutorial test happening to catch it too.
        /// </summary>
        [Test]
        public void CampaignMatchBecomingActive_StartsMusicWithExactlyOneSource()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Music_CampaignStartBootstrap");

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            bootstrap.SetPendingCampaignStageForNextMatch(stage);
            // SetBattleCanvasVisible(true) only re-runs StartNewMatch on a hidden->visible
            // transition - the canvas starts already visible after Initialize(), so it must be
            // hidden first for the pending Campaign stage to actually take effect.
            bootstrap.SetBattleCanvasVisible(false);
            bootstrap.SetBattleCanvasVisible(true);

            Assert.IsFalse(bootstrap.IsTutorialMatch, "Setup: expected a Campaign match, not Tutorial.");
            Assert.IsTrue(bootstrap.BattleMusicIsPlayingForTests,
                "Battle music must be playing once a Campaign match is active - same accepted behaviour as normal Battle and Tutorial.");
            Assert.AreEqual(1, bootstrap.BattleMusicSourceCountForTests,
                "Exactly one AudioSource must exist, even after the hide/show cycle that re-ran StartNewMatch for the Campaign context.");
        }
    }
}
