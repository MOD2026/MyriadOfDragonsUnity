using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// FIRST-TIME NORMAL-BATTLE ENTRY CONTRACT (release feature) - proves the whole gate through
    /// the real production handler, HomePagePresenter.OnToBattleClickedForTests (the exact
    /// private method the real "To Battle" tile's Button.onClick calls), and the real
    /// GameBootstrap.HasValidConfirmedDeckForNormalBattle/EnsureApprovedStarterCollectionGranted
    /// it delegates to - none of the gating logic is reimplemented here.
    /// </summary>
    public class NormalBattleEntryContractTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        private static readonly string[] ApprovedStarterCollectionCardIds =
        {
            "warrior", "novice_knight", "goblin_caster",
            "cleric", "archer_elf", "fox", "bunny", "forest", "tribal_warrior", "undead_soldier",
        };

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsBattleEntry_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
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
                foreach (GameObject candidate in UnityEngine.SceneManagement.SceneManager.GetActiveScene().GetRootGameObjects())
                {
                    if (candidate.name == spawnedName && !_spawned.Contains(candidate))
                        _spawned.Add(candidate);
                }
            }

            // Mirrors HomePagePresenter.Start()'s own boot sequence exactly (SetBattleCanvasVisible(false)
            // right after Home builds) - Start() never fires in EditMode, so this is the only way a
            // test reproduces the real pre-"To Battle" state instead of GameBootstrap's default
            // freshly-created (active) canvas.
            bootstrap.SetBattleCanvasVisible(false);
            return bootstrap;
        }

        private HomePagePresenter SpawnHomePagePresenter()
        {
            var go = new GameObject("HomePagePresenterUnderTest");
            _spawned.Add(go);
            return go.AddComponent<HomePagePresenter>();
        }

        private static void AssertOwnsApprovedStarterCollection(PlayerProfile profile, string message)
        {
            Assert.NotNull(profile, message);
            foreach (string cardId in ApprovedStarterCollectionCardIds)
            {
                Assert.IsTrue(CollectionProgression.OwnsAnyCopy(profile, cardId),
                    $"{message} Missing ownership of '{cardId}' (Collection V1 uses cardProgression, not legacy cardCollection).");
            }
        }

        private static List<string> BuildValidDeckIds(CardDatabase database, int deckSize)
        {
            List<string> ids = database.AllCards
                .Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();
            Assert.AreEqual(deckSize, ids.Count, "Setup: expected enough real cards to fill a full-size deck.");
            return ids;
        }

        private static Text FindDeckStatusText(GameObject deckBuilderCanvas) =>
            deckBuilderCanvas.transform.Find("DeckPanel/DeckStatus")?.GetComponent<Text>();

        [Test]
        public void FreshPlayer_ToBattle_RedirectsToDeckBuilder_GrantsStarterCollection_AndShowsStatusMessage()
        {
            // A genuinely fresh profile: no owned cards, no saved deck.
            var profile = new PlayerProfile();
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the fresh profile to save.");
            SaveSystem.ResetCurrentProfileForTests();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("FreshPlayer_Bootstrap");
            HomePagePresenter home = SpawnHomePagePresenter();

            home.OnToBattleClickedForTests();

            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "A fresh player must never see normal Battle revealed.");

            var deckBuilder = home.GetComponent<DeckBuilderPresenter>();
            Assert.IsNotNull(deckBuilder, "A fresh player's 'To Battle' press must open the existing Deck Builder.");

            GameObject deckBuilderCanvas = GameObject.Find("DeckBuilderCanvas");
            Assert.IsNotNull(deckBuilderCanvas, "Expected the real Deck Builder canvas to be built.");
            Text status = FindDeckStatusText(deckBuilderCanvas);
            Assert.IsNotNull(status, "Expected the existing Deck Builder status surface to exist.");
            Assert.AreEqual("Build and confirm a 10-card deck before normal Battle.", status.text,
                "The redirect must show the exact required status message on the existing status surface.");

            AssertOwnsApprovedStarterCollection(bootstrap.Profile,
                "A fresh player must receive the full approved starter collection through the existing entitlement owner.");

            // No duplicate grant: pressing "To Battle" again (still no confirmed deck) must not
            // re-add or duplicate any starter id.
            int ownedAfterFirstPress = CountOwnedCards(bootstrap.Profile);
            home.OnToBattleClickedForTests();
            Assert.AreEqual(ownedAfterFirstPress, CountOwnedCards(bootstrap.Profile),
                "A second redirect must not duplicate the starter grant.");
        }

        [Test]
        public void InvalidOrIncompleteSavedDeck_ToBattle_RedirectsToDeckBuilder_NotBattle()
        {
            var databaseGo = new GameObject("BattleEntry_CardDatabase_Invalid");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            var profile = new PlayerProfile();
            List<string> nineCards = BuildValidDeckIds(database, 9); // one short of the required 10
            profile.cardCollection = new List<string>(nineCards);
            profile.activeDeckCardIds = new List<string>(nineCards);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the incomplete-deck profile to save.");
            SaveSystem.ResetCurrentProfileForTests();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("InvalidDeck_Bootstrap");
            HomePagePresenter home = SpawnHomePagePresenter();

            home.OnToBattleClickedForTests();

            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "An incomplete saved deck must never reveal normal Battle.");
            Assert.IsNotNull(home.GetComponent<DeckBuilderPresenter>(), "An incomplete saved deck must redirect to the existing Deck Builder.");
        }

        [Test]
        public void ValidConfirmedDeck_ToBattle_EntersNormalBattleThroughExistingPath()
        {
            var databaseGo = new GameObject("BattleEntry_CardDatabase_Valid");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;
            List<string> deckIds = BuildValidDeckIds(database, deckSize);

            var profile = new PlayerProfile();
            profile.cardCollection = new List<string>(deckIds);
            profile.activeDeckCardIds = new List<string>(deckIds);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the valid-deck profile to save.");
            SaveSystem.ResetCurrentProfileForTests();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ValidDeck_Bootstrap");
            HomePagePresenter home = SpawnHomePagePresenter();

            home.OnToBattleClickedForTests();

            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "A valid confirmed deck must enter normal Battle through the existing route.");
            Assert.IsNull(home.GetComponent<DeckBuilderPresenter>(), "A valid confirmed deck must not redirect to Deck Builder.");
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "A complete saved deck must not produce a blocked-start status.");

            HashSet<string> dealtIds = new HashSet<string>(bootstrap.Battle.PlayerState.Hand
                .Concat(bootstrap.Battle.PlayerState.DrawPile)
                .Select(card => card.Id));
            CollectionAssert.AreEquivalent(deckIds, dealtIds, "Normal Battle must deal only the confirmed saved deck.");
        }

        [Test]
        public void ValidConfirmedDeck_PersistsAcrossReload_AndStillEntersBattle()
        {
            var databaseGo = new GameObject("BattleEntry_CardDatabase_Reload");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;
            List<string> deckIds = BuildValidDeckIds(database, deckSize);

            var profile = new PlayerProfile();
            profile.cardCollection = new List<string>(deckIds);
            profile.activeDeckCardIds = new List<string>(deckIds);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the valid-deck profile to save.");

            // Simulate a fresh process/session: drop the in-memory profile entirely and force a
            // real disk reload, same as NormalBattleSavedDeckIntegrationTests already proves for
            // the plain SetBattleCanvasVisible path.
            SaveSystem.ResetCurrentProfileForTests();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReloadDeck_Bootstrap");
            HomePagePresenter home = SpawnHomePagePresenter();

            home.OnToBattleClickedForTests();

            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "A saved deck reloaded from disk must still enter normal Battle.");
            Assert.IsNull(home.GetComponent<DeckBuilderPresenter>(), "A valid reloaded deck must not redirect to Deck Builder.");

            PlayerProfile reloaded = SaveSystem.Load();
            CollectionAssert.AreEqual(deckIds, reloaded.activeDeckCardIds, "The confirmed deck must still be exactly what was saved.");
        }

        [Test]
        public void Tutorial_DoesNotUseTheBattleEntryRedirect_AndAlwaysDealsItsScriptedFormation()
        {
            // Deliberately no saved deck at all - the exact condition that redirects "To Battle".
            var profile = new PlayerProfile();
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the deckless profile to save.");
            SaveSystem.ResetCurrentProfileForTests();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Tutorial_Bootstrap");

            // The real tutorial entry path (HomePagePresenter.OnStartTutorialClicked), not
            // OnToBattleClicked - proves the two remain fully independent.
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SetBattleCanvasVisible(true);

            Assert.IsTrue(bootstrap.BattleCanvasVisibleForTests, "The tutorial must always reveal Battle regardless of saved-deck state.");
            HashSet<string> tutorialDealtIds = new HashSet<string>(bootstrap.Battle.PlayerState.Hand
                .Concat(bootstrap.Battle.PlayerState.DrawPile)
                .Select(card => card.Id));
            CollectionAssert.AreEquivalent(new[] { "warrior", "novice_knight", "goblin_caster" }, tutorialDealtIds,
                "Tutorial must still deal its fixed scripted starter formation regardless of normal saved-deck state.");
        }

        [Test]
        public void FreshPlayerRedirect_MutatesOnlyTheStarterEntitlement_NothingElse()
        {
            var profile = new PlayerProfile();
            int goldBefore = profile.gold;
            int gemsBefore = profile.gems;
            int staminaBefore = profile.stamina;
            var activeDeckBefore = new List<string>(profile.activeDeckCardIds);
            var unlockedStagesBefore = new List<string>(profile.unlockedStageIds);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the fresh profile to save.");
            SaveSystem.ResetCurrentProfileForTests();

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("MutationScope_Bootstrap");
            HomePagePresenter home = SpawnHomePagePresenter();

            home.OnToBattleClickedForTests();

            Assert.AreEqual(goldBefore, bootstrap.Profile.gold, "The redirect must not mutate currency.");
            Assert.AreEqual(gemsBefore, bootstrap.Profile.gems, "The redirect must not mutate currency.");
            Assert.AreEqual(staminaBefore, bootstrap.Profile.stamina, "The redirect must not mutate stamina/energy.");
            CollectionAssert.AreEqual(activeDeckBefore, bootstrap.Profile.activeDeckCardIds,
                "The redirect must never silently write or overwrite activeDeckCardIds - only the player's own Deck Builder confirmation may.");
            CollectionAssert.AreEqual(unlockedStagesBefore, bootstrap.Profile.unlockedStageIds,
                "The redirect must not mutate campaign/stage progression.");

            AssertOwnsApprovedStarterCollection(bootstrap.Profile,
                "The one permitted mutation is the existing starter-card entitlement.");
        }

        private static int CountOwnedCards(PlayerProfile profile)
        {
            if (profile == null) return 0;
            if (profile.UsesCollectionV1)
            {
                int count = 0;
                if (profile.cardProgression == null) return 0;
                foreach (CardProgressionRecord record in profile.cardProgression)
                {
                    if (record != null && !string.IsNullOrEmpty(record.cardId) && record.copyCount > 0)
                        count++;
                }
                return count;
            }

            return profile.cardCollection == null
                ? 0
                : profile.cardCollection.Where(id => !string.IsNullOrEmpty(id)).Distinct().Count();
        }
    }
}
