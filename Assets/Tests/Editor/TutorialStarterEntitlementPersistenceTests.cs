using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// TUTORIAL STARTER COLLECTION ENTITLEMENT, 2026-08-17 (v2, player-agency fix) - proves
    /// GameBootstrap.GrantApprovedStarterCardsIfMissing (called from the real, still-private
    /// StartApprovedTutorialBattle) grants the curated ten-card starter collection (not just the
    /// original three), persists it, safely tops up an existing three-card profile with only the
    /// missing seven, grants no currency/XP/level/stage-unlock alongside it, and - the fix in
    /// this revision - never writes to activeDeckCardIds at all. An earlier revision auto-seeded
    /// the saved deck whenever it was empty/unusable, which meant Deck Builder opened already at
    /// 10/10 before the player pressed anything, silently doing the Recommended Deck button's own
    /// job for it. A fresh player must now own ten valid cards but have no confirmed deck -
    /// pressing Recommended Deck in Deck Builder remains the one explicit way that deck gets
    /// built. Uses bootstrap.Profile, not SaveManager.SaveData - in EditMode GameBootstrap's own
    /// _profile is a standalone `new PlayerProfile()` (Application.isPlaying ?
    /// SaveSystem.CurrentProfile : new PlayerProfile()), the exact object the grant writes to and
    /// _profile.Save() persists - see the fix's own comment for why SaveManager.Save() would be
    /// the wrong API here.
    /// </summary>
    public class TutorialStarterEntitlementPersistenceTests
    {
        private static readonly string[] ApprovedStarterCollectionCardIds =
        {
            "warrior", "novice_knight", "goblin_caster",
            "cleric", "archer_elf", "fox", "bunny", "forest", "tribal_warrior", "undead_soldier",
        };

        private static readonly string[] OriginalThreeCardIds = { "warrior", "novice_knight", "goblin_caster" };

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        /// <summary>Mirrors GameBootstrap's own private SeenIntroPrefKey constant exactly
        /// (see SeedNewProfile's own one-time PlayerPrefs carry-over comment). PlayerPrefs is
        /// real machine/registry state, not sandboxed by SaveSystem.OverrideRootDirectoryForTests
        /// the way the save file is - a value left behind by an earlier interactive Editor/Play
        /// session (or an earlier batch run) leaks into SeedNewProfile's NewGame branch here,
        /// making Grant_ChangesNoCurrencyProgressionOrStageUnlock's hasSeenIntro assertion depend
        /// on unrelated history instead of this test's own setup.</summary>
        private const string SeenIntroPrefKey = "MOD_SeenIntro";
        private bool _hadSeenIntroPref;
        private int _priorSeenIntroPrefValue;

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

            // Reset to a known, deleted state before every test in this file - independent of
            // whatever an earlier Unity/Editor session left in the real PlayerPrefs store.
            // Whatever was actually there is captured first and restored in TearDown, so this
            // test file leaves the developer's own machine state exactly as it found it.
            _hadSeenIntroPref = PlayerPrefs.HasKey(SeenIntroPrefKey);
            _priorSeenIntroPrefValue = PlayerPrefs.GetInt(SeenIntroPrefKey, 0);
            PlayerPrefs.DeleteKey(SeenIntroPrefKey);
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

            if (_hadSeenIntroPref) PlayerPrefs.SetInt(SeenIntroPrefKey, _priorSeenIntroPrefValue);
            else PlayerPrefs.DeleteKey(SeenIntroPrefKey);
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
        public void FreshProfile_GainsAtLeastTenUniqueValidOwnedCards_ButNoAutoSavedDeck()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrant_FreshBootstrap");
            List<string> deckBeforeGrant = new List<string>(bootstrap.Profile.activeDeckCardIds);

            bootstrap.StartApprovedTutorialBattle();

            CollectionAssert.AreEquivalent(ApprovedStarterCollectionCardIds, bootstrap.Profile.cardCollection,
                "A fresh profile must gain exactly the curated ten-card starter collection.");
            Assert.GreaterOrEqual(bootstrap.Profile.cardCollection.Distinct().Count(), 10,
                "A fresh tutorial completion must yield at least ten unique owned card ids.");
            foreach (string id in bootstrap.Profile.cardCollection)
            {
                Assert.IsNotNull(CardDatabase.Instance.GetCard(id), $"Every granted id must resolve to a real, valid card - '{id}' did not.");
            }

            CollectionAssert.AreEqual(deckBeforeGrant, bootstrap.Profile.activeDeckCardIds,
                "The starter-collection grant must never write to activeDeckCardIds - Deck Builder must not open " +
                "already at 10/10 before the player has pressed Recommended Deck themselves.");
            CollectionAssert.AreNotEquivalent(ApprovedStarterCollectionCardIds, bootstrap.Profile.activeDeckCardIds,
                "Setup/regression guard: the saved deck must not happen to already equal the starter collection.");
        }

        [Test]
        public void ExistingThreeCardProfile_ReceivesOnlyTheMissingSevenIds_NoDuplicatesNoDeckOverwrite()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrant_ExistingProfileBootstrap");
            bootstrap.Profile.cardCollection.Clear();
            bootstrap.Profile.cardCollection.AddRange(OriginalThreeCardIds);
            List<string> deckBeforeGrant = new List<string>(bootstrap.Profile.activeDeckCardIds);

            bootstrap.StartApprovedTutorialBattle();

            CollectionAssert.AreEquivalent(ApprovedStarterCollectionCardIds, bootstrap.Profile.cardCollection,
                "An existing three-card profile must end up owning exactly the full ten-card collection.");
            Assert.AreEqual(10, bootstrap.Profile.cardCollection.Count,
                "No card may be duplicated when topping up an existing three-card profile.");
            CollectionAssert.AreEqual(deckBeforeGrant, bootstrap.Profile.activeDeckCardIds,
                "Topping up an existing profile's card collection must never touch its saved deck.");
        }

        [Test]
        public void Grant_PersistsAcrossAFreshLoad()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrant_PersistBootstrap");
            List<string> deckBeforeGrant = new List<string>(bootstrap.Profile.activeDeckCardIds);

            bootstrap.StartApprovedTutorialBattle();

            PlayerProfile reloaded = SaveSystem.Load();
            CollectionAssert.AreEquivalent(ApprovedStarterCollectionCardIds, reloaded.cardCollection,
                "The starter-collection grant must survive a fresh load from disk (i.e. an app restart), not just live in memory.");
            CollectionAssert.AreEqual(deckBeforeGrant, reloaded.activeDeckCardIds,
                "The saved deck must be exactly as untouched after a fresh load as it was before the grant.");
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

            Assert.AreEqual(ApprovedStarterCollectionCardIds.Length, bootstrap.Profile.cardCollection.Count,
                "A retry must not duplicate any starter card.");
            CollectionAssert.AreEquivalent(ApprovedStarterCollectionCardIds, bootstrap.Profile.cardCollection);
            Assert.IsFalse(SaveSystem.Exists,
                "A retry with nothing new to grant must not trigger another persistence write.");
        }

        [Test]
        public void DefaultPlaceholderSavedDeck_IsLeftCompletelyUntouched()
        {
            // PlayerProfile's own untouched default ({"c1","c3","c4","c6"}) is not empty by
            // Count, and every one of those ids is fake (none resolve to a real card) - but the
            // grant must still never touch it. A fresh player is meant to reach Deck Builder
            // owning ten valid cards with no confirmed deck yet, and press Recommended Deck
            // themselves - not find it silently pre-filled.
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrant_DefaultDeckUntouchedBootstrap");
            CollectionAssert.AreEquivalent(new[] { "c1", "c3", "c4", "c6" }, bootstrap.Profile.activeDeckCardIds,
                "Setup: expected PlayerProfile's own untouched placeholder default.");

            bootstrap.StartApprovedTutorialBattle();

            CollectionAssert.AreEquivalent(new[] { "c1", "c3", "c4", "c6" }, bootstrap.Profile.activeDeckCardIds,
                "The starter-collection grant must leave even an unusable placeholder saved deck completely untouched.");
        }

        [Test]
        public void RealConfirmedDeck_IsNeverOverwritten()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrant_RealDeckUntouchedBootstrap");
            bootstrap.Profile.cardCollection.Clear();
            bootstrap.Profile.cardCollection.Add("cyclops");
            // A deliberately small, otherwise-unrelated "confirmed deck" - one real owned card
            // plus some not-yet-owned ones a player might have picked before finishing it.
            var confirmedDeck = new List<string> { "cyclops", "medusa", "minotaur" };
            bootstrap.Profile.activeDeckCardIds = new List<string>(confirmedDeck);

            bootstrap.StartApprovedTutorialBattle();

            CollectionAssert.AreEqual(confirmedDeck, bootstrap.Profile.activeDeckCardIds,
                "A player's confirmed deck must never be overwritten by the starter-collection grant.");
        }

        [Test]
        public void Grant_ChangesNoCurrencyProgressionOrStageUnlock()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrant_NoSideEffectBootstrap");

            // A fresh PlayerProfile's own declared defaults - captured independently of
            // bootstrap.Profile so this test does not depend on StartApprovedTutorialBattle
            // having already run.
            var baseline = new PlayerProfile();

            bootstrap.StartApprovedTutorialBattle();
            PlayerProfile profile = bootstrap.Profile;

            Assert.AreEqual(baseline.gold, profile.gold, "The starter-collection grant must never change gold.");
            Assert.AreEqual(baseline.gems, profile.gems, "The starter-collection grant must never change gems.");
            Assert.AreEqual(baseline.avatarLevel, profile.avatarLevel, "The starter-collection grant must never change avatarLevel.");
            Assert.AreEqual(baseline.totalMatches, profile.totalMatches, "The starter-collection grant must never change totalMatches.");
            Assert.AreEqual(baseline.totalWins, profile.totalWins, "The starter-collection grant must never change totalWins.");
            Assert.AreEqual(baseline.winStreak, profile.winStreak, "The starter-collection grant must never change winStreak.");
            CollectionAssert.AreEquivalent(baseline.unlockedStageIds, profile.unlockedStageIds,
                "The starter-collection grant must never unlock a stage.");
            Assert.AreEqual(baseline.hasSeenIntro, profile.hasSeenIntro,
                "The starter-collection grant must never change onboarding/tutorial-completion-style state - none exists yet, and this grant must not invent any.");
        }

        [Test]
        public void TutorialFormation_AndDealtDeck_RemainTheOriginalThreeCards_RegardlessOfTheLargerCollection()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("StarterGrant_TutorialFormationUnchangedBootstrap");

            bootstrap.StartApprovedTutorialBattle();

            HashSet<string> dealtIds = new HashSet<string>(bootstrap.Battle.PlayerState.Hand.Select(c => c.Id)
                .Concat(bootstrap.Battle.PlayerState.DrawPile.Select(c => c.Id)));
            CollectionAssert.AreEquivalent(OriginalThreeCardIds, dealtIds,
                "The tutorial's own scripted formation must remain exactly warrior/novice_knight/goblin_caster, " +
                "unaffected by owning a larger ten-card collection.");
        }
    }
}
