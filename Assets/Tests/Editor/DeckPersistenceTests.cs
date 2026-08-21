using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// DECK PERSISTENCE, 2026-08-15 (revised in the release repair pass below) - proves
    /// DeckBuilderPresenter's save path and GameBootstrap's saved-deck consumption path through
    /// their REAL private handlers: SetAndConfirmDeckForTests reuses the real (still-private)
    /// AddCardToDeck/ConfirmDeck; PlayAgainForTests/ResetLineupForTests reuse the real
    /// (still-private) OnPlayAgainPressed/OnLineupButtonPressed -&gt; StartNewMatch -&gt;
    /// TryBuildSavedPlayerDeck, exactly as production does.
    ///
    /// A confirmed valid activeDeckCardIds deck is the exclusive source of the normal player
    /// battle deck - an invalid or incomplete saved deck blocks normal battle instead of padding,
    /// substituting, or skip-and-continuing with a partial/regenerated deck (see
    /// TryBuildSavedPlayerDeck's own comment). The padding behavior this file used to assert was
    /// itself the defect; see UndersizedSavedDeck_BlocksNormalBattle_WithNoPadding and
    /// DuplicateAndInvalidSavedIds_BlockNormalBattle_WithNoSkipOrPad below.
    ///
    /// Two separate profiles, same as the other Home/Battle test files in this suite -
    /// GameBootstrap's own `bootstrap.Profile` (Application.isPlaying ? SaveSystem.CurrentProfile
    /// : new PlayerProfile(), always the latter in EditMode) versus `SaveManager.SaveData`
    /// (DeckBuilderPresenter's own profile, a facade over SaveSystem.CurrentProfile). Tests that
    /// drive GameBootstrap read/write `bootstrap.Profile`; the one test that drives
    /// DeckBuilderPresenter directly reads/writes `SaveManager.SaveData`.
    /// </summary>
    public class DeckPersistenceTests
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

        /// <summary>Every card id currently in the player's dealt deck (drawn or not) - Hand and
        /// DrawPile together are the whole deck StartMatch handed this side, before any further
        /// draws.</summary>
        private static HashSet<string> DealtDeckIds(PlayerBattleState side)
        {
            return new HashSet<string>(side.Hand.Concat(side.DrawPile).Select(c => c.Id));
        }

        [Test]
        public void ConfirmingADeck_PersistsExactlyThoseIds_AcrossAFreshLoad()
        {
            var databaseGo = new GameObject("DeckPersistence_CardDatabase");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            List<string> realCardIds = database.AllCards.Select(c => c.Id).Take(25).ToList();
            Assert.GreaterOrEqual(realCardIds.Count, 20, "Setup: expected at least 20 real cards in the database.");

            PlayerProfile profile = SaveManager.SaveData;
            profile.cardCollection = new List<string>(realCardIds);
            profile.ApplyDataToEmpire();
            int deckSizeLimit = profile.Empire.DeckSlotCount;
            List<string> chosenDeck = realCardIds.Take(deckSizeLimit).ToList();

            var presenterGo = new GameObject("DeckBuilderPresenterUnderTest");
            _spawned.Add(presenterGo);
            var presenter = presenterGo.AddComponent<DeckBuilderPresenter>();
            presenter.Initialize(onBackToHome: null);
            GameObject deckBuilderCanvas = GameObject.Find("DeckBuilderCanvas");
            if (deckBuilderCanvas != null) _spawned.Add(deckBuilderCanvas);

            presenter.SetAndConfirmDeckForTests(chosenDeck);

            PlayerProfile reloaded = SaveSystem.Load();
            CollectionAssert.AreEquivalent(chosenDeck, reloaded.activeDeckCardIds,
                "Confirming a deck must persist exactly those ids, recoverable from a fresh load.");
        }

        [Test]
        public void NormalMatch_UsesTheSavedDeck_WhenItResolvesToAFullDeck()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("DeckPersistence_NormalBootstrap");
            // Read the required deck size directly from Empire rather than from the boot-time
            // match's own dealt hand: with no saved deck yet (a fresh profile), the boot match
            // now correctly deals nothing at all (blocked, see NormalMatchStatusForTests below).
            int deckSize = bootstrap.Profile.Empire.DeckSlotCount;
            Assert.Greater(deckSize, 0, "Setup: expected a positive current deck size.");

            List<string> realCardIds = CardDatabase.Instance.AllCards.Select(c => c.Id).Take(deckSize).ToList();
            Assert.AreEqual(deckSize, realCardIds.Count, "Setup: expected enough real cards to fill a full-size saved deck.");
            bootstrap.Profile.activeDeckCardIds = new List<string>(realCardIds);

            bootstrap.PlayAgainForTests();

            HashSet<string> dealtIds = DealtDeckIds(bootstrap.Battle.PlayerState);
            CollectionAssert.AreEquivalent(realCardIds, dealtIds,
                "A full-size, fully valid saved deck must be used exactly as saved on ordinary replay.");
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "A complete, valid saved deck must not produce a blocked-start status.");
        }

        /// <summary>Command Centre decision (release repair, deck-to-battle contract): an
        /// undersized saved deck must BLOCK normal battle, not be padded with random cards up to
        /// the current DeckSlotCount. Padding was the exact defect this test used to lock in -
        /// see NormalBattleSavedDeckIntegrationTests for the confirmed-valid-deck happy path this
        /// invariant protects.</summary>
        [Test]
        public void UndersizedSavedDeck_BlocksNormalBattle_WithNoPadding()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("DeckPersistence_UndersizedBootstrap");
            int deckSize = bootstrap.Profile.Empire.DeckSlotCount;
            Assert.Greater(deckSize, 2, "Setup: expected a deck size large enough to test an undersized subset.");

            List<string> undersizedDeck = CardDatabase.Instance.AllCards.Select(c => c.Id).Take(deckSize - 2).ToList();
            bootstrap.Profile.activeDeckCardIds = new List<string>(undersizedDeck);

            bootstrap.PlayAgainForTests();

            List<string> dealtIdsList = bootstrap.Battle.PlayerState.Hand
                .Concat(bootstrap.Battle.PlayerState.DrawPile)
                .Select(c => c.Id)
                .ToList();

            Assert.AreEqual(0, dealtIdsList.Count,
                "An undersized saved deck must block normal battle entirely, not be padded or fielded understrength.");
            Assert.IsNotNull(bootstrap.NormalMatchStatusForTests,
                "An undersized saved deck must produce a blocked-start status the player can see.");
        }

        /// <summary>Same invariant as UndersizedSavedDeck_BlocksNormalBattle_WithNoPadding, for
        /// duplicate/invalid ids: the whole deck is rejected, not silently repaired by skipping
        /// the bad ids and padding the rest.</summary>
        [Test]
        public void DuplicateAndInvalidSavedIds_BlockNormalBattle_WithNoSkipOrPad()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("DeckPersistence_DuplicateInvalidBootstrap");

            List<string> validUniqueIds = CardDatabase.Instance.AllCards.Select(c => c.Id).Take(3).ToList();
            var poisonedIds = new List<string>
            {
                validUniqueIds[0], validUniqueIds[0], validUniqueIds[0], // duplicate
                "definitely_not_a_real_card_id",                        // invalid
                "",                                                     // empty
                validUniqueIds[1],
                validUniqueIds[2],
            };
            bootstrap.Profile.activeDeckCardIds = poisonedIds;

            Assert.DoesNotThrow(() => bootstrap.PlayAgainForTests());

            List<string> dealtIdsList = bootstrap.Battle.PlayerState.Hand
                .Concat(bootstrap.Battle.PlayerState.DrawPile)
                .Select(c => c.Id)
                .ToList();

            Assert.AreEqual(0, dealtIdsList.Count,
                "A saved deck containing any duplicate or invalid id must block normal battle entirely, not skip the bad ids and pad the rest.");
            Assert.IsNotNull(bootstrap.NormalMatchStatusForTests,
                "A poisoned saved deck must produce a blocked-start status the player can see.");
        }

        [Test]
        public void ResetLineup_BypassesTheSavedDeck()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("DeckPersistence_ResetBootstrap");
            int deckSize = DealtDeckIds(bootstrap.Battle.PlayerState).Count;

            List<string> savedDeck = CardDatabase.Instance.AllCards.Select(c => c.Id).Take(deckSize).ToList();
            bootstrap.Profile.activeDeckCardIds = new List<string>(savedDeck);

            bootstrap.ResetLineupForTests();

            HashSet<string> dealtIds = DealtDeckIds(bootstrap.Battle.PlayerState);
            // If Reset reused the saved deck the same way Play Again does, this set would be
            // an EXACT match to savedDeck (BuildSavedPlayerDeck alone, no padding needed for a
            // full-size deck) - the only way it can differ is if the saved deck was bypassed and
            // a fresh random deck was built instead. With 85 real cards in the database and a
            // deck size far smaller than that, the odds of BuildBalancedDecks' random selection
            // coincidentally reproducing this exact set are astronomically small.
            CollectionAssert.AreNotEquivalent(savedDeck, dealtIds,
                "Reset Lineup must bypass the saved deck and build a fresh random one, not reuse it.");
        }

        [Test]
        public void Tutorial_IsUnaffectedByAnySavedDeck()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("DeckPersistence_TutorialBootstrap");

            // A saved deck that, if it leaked into the tutorial, would be trivially detectable -
            // none of these ids overlap the tutorial's own hardcoded warrior/novice_knight/
            // goblin_caster deck.
            List<string> unrelatedSavedDeck = CardDatabase.Instance.AllCards
                .Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(10)
                .ToList();
            bootstrap.Profile.activeDeckCardIds = unrelatedSavedDeck;

            bootstrap.StartApprovedTutorialBattle();

            HashSet<string> tutorialDeckIds = DealtDeckIds(bootstrap.Battle.PlayerState);
            CollectionAssert.AreEquivalent(new[] { "warrior", "novice_knight", "goblin_caster" }, tutorialDeckIds,
                "The tutorial's deck must always be exactly its own approved hardcoded cards, regardless of any saved deck - " +
                "the unrelated saved deck set up above must not leak any of its own cards in.");
        }
    }
}
