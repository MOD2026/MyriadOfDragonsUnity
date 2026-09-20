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
    public class NormalBattleSavedDeckIntegrationTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsNormalDeck_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject spawned in _spawned)
            {
                if (spawned != null) Object.DestroyImmediate(spawned);
            }
            _spawned.Clear();

            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
            {
                Directory.Delete(_scratchSaveDir, true);
            }
        }

        [Test]
        public void SavedDeckIsReloadedForNormalBattle_AndTutorialRemainsFixed()
        {
            var databaseObject = new GameObject("NormalBattleSavedDeckDatabase");
            _spawned.Add(databaseObject);
            CardDatabase database = databaseObject.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            List<string> distinctiveDeck = database.AllCards
                .Select(card => card.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(MyriadOfDragons.Empire.PlayerEmpireData.DeckSlotsForBarracksLevel(1))
                .ToList();
            Assert.AreEqual(MyriadOfDragons.Empire.PlayerEmpireData.DeckSlotsForBarracksLevel(1), distinctiveDeck.Count, "Setup: expected a full deck of distinctive real cards.");

            PlayerProfile profile = new PlayerProfile();
            profile.cardCollection = new List<string>(distinctiveDeck);
            profile.ApplyDataToEmpire();
            Assert.AreEqual(MyriadOfDragons.Empire.PlayerEmpireData.DeckSlotsForBarracksLevel(1), profile.Empire.DeckSlotCount, "Setup: expected the level-1 deck size.");
            profile.activeDeckCardIds = new List<string>(distinctiveDeck);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();

            var bootstrapObject = new GameObject("NormalBattleSavedDeckBootstrap");
            _spawned.Add(bootstrapObject);
            GameBootstrap bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            bootstrap.Initialize();

            // GameBootstrap.Initialize() creates Canvas/EventSystem/CardDatabase/BattleController
            // as separate root GameObjects, not children of this fixture's own object - so adding
            // only the bootstrap object to _spawned left them alive after TearDown, for the rest of
            // the Unity process. Every later fixture that does GameObject.Find("Canvas") then found
            // THIS stale one instead of its own: RarityFrameRenderingTests looked up a HandRow with
            // no cards in it (Card_warrior null) and TutorialTeachingOverlayTests sized the stale
            // canvas instead of its own (a 234px geometry shift). Both were false failures that only
            // appeared in a continuous full-suite run. Same collect-then-destroy pattern every other
            // GameBootstrap fixture in this suite already uses.
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (candidate.name == spawnedName && !_spawned.Contains(candidate))
                        _spawned.Add(candidate);
                }
            }

            HashSet<string> normalDealtIds = new HashSet<string>(bootstrap.Battle.PlayerState.Hand
                .Concat(bootstrap.Battle.PlayerState.DrawPile)
                .Select(card => card.Id));
            CollectionAssert.AreEquivalent(distinctiveDeck, normalDealtIds,
                "Normal battle must deal only the ten ids loaded from the confirmed active deck.");
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "A complete saved deck must not produce a blocked-start status.");

            bootstrap.StartApprovedTutorialBattle(false);
            HashSet<string> tutorialDealtIds = new HashSet<string>(bootstrap.Battle.PlayerState.Hand
                .Concat(bootstrap.Battle.PlayerState.DrawPile)
                .Select(card => card.Id));
            CollectionAssert.AreEquivalent(new[] { "warrior", "novice_knight", "goblin_caster" }, tutorialDealtIds,
                "Tutorial must keep its fixed three-card setup and ignore the saved normal deck.");
        }

        /// <summary>
        /// Release repair regression test: GameBootstrap.Initialize() runs once, at app boot,
        /// before the player has ever visited Deck Builder in this session (Home starts with the
        /// battle canvas hidden and only reveals it later via SetBattleCanvasVisible(true), the
        /// real "To Battle" call site). Without a refresh on that reveal, confirming a new deck
        /// in Deck Builder after boot and then going straight to Battle dealt whatever deck
        /// existed AT BOOT TIME, not the just-confirmed one - the exact "Deck Builder saves a
        /// confirmed 10-card deck, but normal Battle deals unrelated cards" defect. This proves
        /// the hidden-to-visible transition alone (no explicit StartNewMatch/PlayAgain call, the
        /// same call HomePagePresenter's OnToBattleClicked makes) picks up a deck confirmed after
        /// boot.
        /// </summary>
        [Test]
        public void SavedDeckConfirmedAfterBoot_IsPickedUpWhenBattleCanvasIsRevealed()
        {
            var databaseObject = new GameObject("NormalBattleSavedDeckDatabase_PostBoot");
            _spawned.Add(databaseObject);
            CardDatabase database = databaseObject.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            var bootstrapObject = new GameObject("NormalBattleSavedDeckBootstrap_PostBoot");
            _spawned.Add(bootstrapObject);
            GameBootstrap bootstrap = bootstrapObject.AddComponent<GameBootstrap>();
            bootstrap.Initialize(); // boots with no saved deck yet - the pre-Deck-Builder state

            // GameBootstrap.Initialize() creates Canvas/EventSystem/CardDatabase/BattleController
            // as separate root GameObjects, not children of this fixture's own object - so adding
            // only the bootstrap object to _spawned left them alive after TearDown, for the rest of
            // the Unity process. Every later fixture that does GameObject.Find("Canvas") then found
            // THIS stale one instead of its own: RarityFrameRenderingTests looked up a HandRow with
            // no cards in it (Card_warrior null) and TutorialTeachingOverlayTests sized the stale
            // canvas instead of its own (a 234px geometry shift). Both were false failures that only
            // appeared in a continuous full-suite run. Same collect-then-destroy pattern every other
            // GameBootstrap fixture in this suite already uses.
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (candidate.name == spawnedName && !_spawned.Contains(candidate))
                        _spawned.Add(candidate);
                }
            }

            List<string> confirmedAfterBoot = database.AllCards
                .Select(card => card.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(bootstrap.Profile.Empire.DeckSlotCount)
                .ToList();
            Assert.AreEqual(bootstrap.Profile.Empire.DeckSlotCount, confirmedAfterBoot.Count,
                "Setup: expected enough real cards to fill a full-size deck.");
            bootstrap.Profile.activeDeckCardIds = new List<string>(confirmedAfterBoot);

            // The real Home->Battle handoff: hide (as Home does at its own startup), then reveal
            // (the "To Battle" action) - no explicit StartNewMatch/PlayAgain call from the test.
            bootstrap.SetBattleCanvasVisible(false);
            bootstrap.SetBattleCanvasVisible(true);

            HashSet<string> dealtIds = new HashSet<string>(bootstrap.Battle.PlayerState.Hand
                .Concat(bootstrap.Battle.PlayerState.DrawPile)
                .Select(card => card.Id));
            CollectionAssert.AreEquivalent(confirmedAfterBoot, dealtIds,
                "Revealing the battle canvas must deal the deck confirmed after boot, not whatever existed at boot time.");
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "A complete saved deck must not produce a blocked-start status.");
        }
    }
}
