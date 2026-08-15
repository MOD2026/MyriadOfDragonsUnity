using System.Collections.Generic;
using System.IO;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// TUTORIAL STARTER CARD PERSISTENCE, 2026-08-15 - proves
    /// GameBootstrap.GrantApprovedStarterCardsIfMissing (called from the real, still-private
    /// StartApprovedTutorialBattle) now persists the approved starter grant, not just mutates
    /// cardCollection in memory. Uses bootstrap.Profile, not SaveManager.SaveData - in EditMode
    /// GameBootstrap's own _profile is a standalone `new PlayerProfile()`
    /// (Application.isPlaying ? SaveSystem.CurrentProfile : new PlayerProfile()), the exact
    /// object the grant writes to and _profile.Save() persists - see the fix's own comment for
    /// why SaveManager.Save() would be the wrong API here.
    /// </summary>
    public class TutorialStarterEntitlementPersistenceTests
    {
        private static readonly string[] ApprovedStarterCardIds = { "warrior", "novice_knight", "goblin_caster" };

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            // Isolation for SaveSystem, which otherwise reads/writes the real machine's save
            // file - see SaveSystem.OverrideRootDirectoryForTests' own comment. A fresh temp
            // directory per test, never the real persistentDataPath.
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
        public void FreshProfile_GainsExactlyTheThreeApprovedStarterCards()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrant_FreshBootstrap");

            bootstrap.StartApprovedTutorialBattle();

            CollectionAssert.AreEquivalent(ApprovedStarterCardIds, bootstrap.Profile.cardCollection,
                "A fresh profile must gain exactly the three approved starter cards, nothing more or less.");
        }

        [Test]
        public void Grant_PersistsAcrossAFreshLoad()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrant_PersistBootstrap");

            bootstrap.StartApprovedTutorialBattle();

            PlayerProfile reloaded = SaveSystem.Load();
            CollectionAssert.AreEquivalent(ApprovedStarterCardIds, reloaded.cardCollection,
                "The starter-card grant must survive a fresh load from disk (i.e. an app restart), not just live in memory.");
        }

        [Test]
        public void Retry_DoesNotDuplicateCardsOrTriggerAnotherPersistenceWrite()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrant_RetryBootstrap");

            bootstrap.StartApprovedTutorialBattle();
            Assert.IsTrue(SaveSystem.Exists, "Setup: expected the first grant to have persisted a save file.");

            // Deleting the file makes a second, wrongful write unambiguous to detect - if
            // GrantApprovedStarterCardsIfMissing is correctly idempotent (nothing new to grant),
            // nothing recreates it; if it isn't, the file reappears.
            SaveSystem.Delete();
            Assert.IsFalse(SaveSystem.Exists, "Setup: expected the save file to actually be gone before the retry.");

            bootstrap.StartApprovedTutorialBattle(); // retry / second tutorial start

            Assert.AreEqual(ApprovedStarterCardIds.Length, bootstrap.Profile.cardCollection.Count,
                "A retry must not duplicate any starter card.");
            CollectionAssert.AreEquivalent(ApprovedStarterCardIds, bootstrap.Profile.cardCollection);
            Assert.IsFalse(SaveSystem.Exists,
                "A retry with nothing new to grant must not trigger another persistence write.");
        }

        [Test]
        public void Grant_ChangesNoOtherProfileField()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrant_NoSideEffectBootstrap");

            // A fresh PlayerProfile's own declared defaults - captured independently of
            // bootstrap.Profile so this test does not depend on StartApprovedTutorialBattle
            // having already run.
            var baseline = new PlayerProfile();

            bootstrap.StartApprovedTutorialBattle();
            PlayerProfile profile = bootstrap.Profile;

            Assert.AreEqual(baseline.gold, profile.gold, "The starter-card grant must never change gold.");
            Assert.AreEqual(baseline.gems, profile.gems, "The starter-card grant must never change gems.");
            Assert.AreEqual(baseline.avatarLevel, profile.avatarLevel, "The starter-card grant must never change avatarLevel.");
            Assert.AreEqual(baseline.totalMatches, profile.totalMatches, "The starter-card grant must never change totalMatches.");
            Assert.AreEqual(baseline.totalWins, profile.totalWins, "The starter-card grant must never change totalWins.");
            Assert.AreEqual(baseline.winStreak, profile.winStreak, "The starter-card grant must never change winStreak.");
            CollectionAssert.AreEquivalent(baseline.unlockedStageIds, profile.unlockedStageIds,
                "The starter-card grant must never unlock a stage.");
            CollectionAssert.AreEquivalent(baseline.activeDeckCardIds, profile.activeDeckCardIds,
                "The starter-card grant must never change the saved deck.");
            Assert.AreEqual(baseline.hasSeenIntro, profile.hasSeenIntro,
                "The starter-card grant must never change onboarding/tutorial-completion-style state - none exists yet, and this grant must not invent any.");
        }
    }
}
