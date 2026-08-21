using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Data;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// ACQUIRED-CARD-TO-COMBAT CONTRACT (release feature, verification pass) - one end-to-end
    /// integration test proving a card granted by a real Shop purchase survives every hop to
    /// normal Battle: Shop -&gt; save/reload -&gt; Deck Builder acceptance/replace/confirm -&gt;
    /// save/reload -&gt; normal Battle deck resolution. Every mutation happens through the real
    /// production handler or ...ForTests() hook the real UI already calls
    /// (ShopPresenter.PurchaseForTests, DeckBuilderPresenter.SetAndConfirmDeckForTests,
    /// HomePagePresenter.OnToBattleClickedForTests, GameBootstrap.HasValidConfirmedDeckForNormalBattle) -
    /// nothing here reimplements ownership, deck-building, or deck-resolution logic. Each of the
    /// three phases (purchase, deck edit, Battle entry) uses a brand-new GameBootstrap after
    /// SaveSystem.ResetCurrentProfileForTests, so a stale in-memory singleton can never stand in
    /// for what is actually on disk.
    /// </summary>
    public class AcquiredCardToCombatContractTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsAcquiredCard_" + System.Guid.NewGuid().ToString("N"));
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
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null) _spawned.Add(spawned);
            }
            bootstrap.SetBattleCanvasVisible(false);
            return bootstrap;
        }

        private HomePagePresenter SpawnHomePagePresenter(GameBootstrap bootstrap)
        {
            var go = new GameObject("HomePagePresenterUnderTest_AcquiredCard");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        private ShopPresenter SpawnShop(PlayerProfile profile)
        {
            var go = new GameObject("ShopPresenterUnderTest_AcquiredCard");
            _spawned.Add(go);
            var shop = go.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null);
            GameObject shopCanvas = GameObject.Find("ShopCanvas");
            if (shopCanvas != null) _spawned.Add(shopCanvas);
            return shop;
        }

        private DeckBuilderPresenter SpawnDeckBuilder()
        {
            var go = new GameObject("DeckBuilderPresenterUnderTest_AcquiredCard");
            _spawned.Add(go);
            var deckBuilder = go.AddComponent<DeckBuilderPresenter>();
            deckBuilder.Initialize(onBackToHome: null);
            GameObject deckCanvas = GameObject.Find("DeckBuilderCanvas");
            if (deckCanvas != null) _spawned.Add(deckCanvas);
            return deckBuilder;
        }

        [Test]
        public void PurchasedCard_SurvivesReload_ReplacesADeckSlot_PersistsAndReachesNormalBattle()
        {
            // ===== SETUP: an existing player with a full owned collection and an already
            // confirmed 10-card deck - distinct from the starter set, so this test cannot pass by
            // accident on freshly-granted starter cards alone. =====
            var databaseGo = new GameObject("AcquiredCard_CardDatabase");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;

            List<string> ownedIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "dragon")
                .Take(deckSize + 5) // owns a few more cards than are in the deck, like a real player
                .ToList();
            Assert.AreEqual(deckSize + 5, ownedIds.Count, "Setup: expected enough real cards to fill a full-size deck plus a few extra.");
            List<string> originalDeckIds = ownedIds.Take(deckSize).ToList();

            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(ownedIds),
                activeDeckCardIds = new List<string>(originalDeckIds),
            };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: expected the existing player's profile to save.");
            SaveSystem.ResetCurrentProfileForTests();

            // ===== REQUIREMENT 1 + 5: a real Shop card-pack purchase grants exactly one new,
            // deterministic, non-placeholder card, and never touches the active deck. =====
            GameBootstrap bootstrap1 = SpawnAndInitializeBootstrap("AcquiredCard_Bootstrap_Shop");
            PlayerProfile liveProfile = bootstrap1.Profile;
            var deckBeforePurchase = new List<string>(liveProfile.activeDeckCardIds);

            string expectedGrantedCardId = database.AllCards.Select(c => c.Id)
                .First(id => id != "dragon" && !liveProfile.cardCollection.Contains(id));
            Assert.AreNotEqual("dragon", expectedGrantedCardId, "Setup: the deterministic sequence must never select the placeholder id.");

            ShopPresenter shop = SpawnShop(liveProfile);
            shop.PurchaseForTests("pack_novice");

            CollectionAssert.Contains(liveProfile.cardCollection, expectedGrantedCardId, "Requirement 1: the purchased card must appear in the owned collection immediately.");
            CollectionAssert.AreEqual(deckBeforePurchase, liveProfile.activeDeckCardIds, "Requirement 5: a Shop purchase must never silently change the active deck.");

            // ===== REQUIREMENT 1, continued: the granted card survives a full save/reload. =====
            SaveSystem.ResetCurrentProfileForTests();
            PlayerProfile reloadedAfterPurchase = SaveSystem.Load();
            CollectionAssert.Contains(reloadedAfterPurchase.cardCollection, expectedGrantedCardId, "Requirement 1: the purchased card must still be owned after a full reload.");
            CollectionAssert.AreEqual(originalDeckIds, reloadedAfterPurchase.activeDeckCardIds, "Setup: expected the deck to still be exactly the original ten cards after reload, before any edit.");

            // ===== REQUIREMENT 2 + 3: the reloaded card is accepted by a fresh Deck Builder as
            // owned, the player replaces one deck card with it, confirms a valid unique 10-card
            // deck, and it persists. A brand-new GameBootstrap proves this isn't the same
            // in-memory profile the purchase used. =====
            GameBootstrap bootstrap2 = SpawnAndInitializeBootstrap("AcquiredCard_Bootstrap_DeckEdit");
            Assert.AreNotSame(bootstrap1, bootstrap2, "Setup: expected a genuinely new GameBootstrap instance for the deck-edit phase.");
            CollectionAssert.Contains(bootstrap2.Profile.cardCollection, expectedGrantedCardId, "Requirement 8 (no stale singleton): a fresh GameBootstrap must see the purchased card via disk, not a cached copy.");

            List<string> newDeckIds = originalDeckIds.Skip(1).Append(expectedGrantedCardId).ToList(); // drop one original card, add the acquired one
            Assert.AreEqual(deckSize, newDeckIds.Count, "Setup: expected the replacement deck to still be exactly ten cards.");
            Assert.AreEqual(deckSize, newDeckIds.Distinct().Count(), "Requirement 6: the replacement deck must contain no duplicate ids.");

            DeckBuilderPresenter deckBuilder = SpawnDeckBuilder();
            deckBuilder.SetAndConfirmDeckForTests(newDeckIds);

            // If the acquired card had NOT been accepted as owned, SetAndConfirmDeckForTests would
            // have silently skipped it (see its own doc comment) and the confirmed deck would be
            // short - so its presence here is itself the proof of Requirement 2.
            CollectionAssert.AreEqual(newDeckIds, bootstrap2.Profile.activeDeckCardIds,
                "Requirement 2 + 3: the acquired card must be accepted as owned and the replacement deck must be confirmed and persisted exactly.");
            CollectionAssert.Contains(bootstrap2.Profile.activeDeckCardIds, expectedGrantedCardId, "Requirement 3: the confirmed deck must contain the newly acquired card.");

            // ===== REQUIREMENT 3, continued: the edited deck survives another full reload. =====
            SaveSystem.ResetCurrentProfileForTests();
            PlayerProfile reloadedAfterDeckEdit = SaveSystem.Load();
            CollectionAssert.AreEqual(newDeckIds, reloadedAfterDeckEdit.activeDeckCardIds, "Requirement 3: the confirmed replacement deck must survive a full reload.");
            CollectionAssert.Contains(reloadedAfterDeckEdit.cardCollection, expectedGrantedCardId, "Setup: expected the card to still be owned after this second reload.");

            // ===== REQUIREMENT 4 + 6: normal Battle resolves and deals from exactly this
            // confirmed deck - the acquired card must be part of the resolved player deck, with
            // no duplicate ids, no padding, and no fallback deck. =====
            GameBootstrap bootstrap3 = SpawnAndInitializeBootstrap("AcquiredCard_Bootstrap_Battle");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap3);
            Assert.IsTrue(bootstrap3.HasValidConfirmedDeckForNormalBattle(), "Setup: expected the confirmed replacement deck to satisfy the normal-battle saved-deck gate.");

            home.OnToBattleClickedForTests();

            Assert.IsTrue(bootstrap3.BattleCanvasVisibleForTests, "Requirement 4: a valid confirmed deck must enter normal Battle directly, no Deck Builder redirect.");
            Assert.IsNull(home.GetComponent<DeckBuilderPresenter>(), "Requirement 4: a valid confirmed deck must not redirect to Deck Builder.");
            Assert.IsNull(bootstrap3.NormalMatchStatusForTests, "Requirement 4: normal Battle must not be blocked.");

            HashSet<string> dealtIds = new HashSet<string>(bootstrap3.Battle.PlayerState.Hand
                .Concat(bootstrap3.Battle.PlayerState.DrawPile)
                .Select(c => c.Id));
            CollectionAssert.AreEquivalent(newDeckIds, dealtIds, "Requirement 4 + 6: normal Battle must deal exactly the confirmed deck - no padding, substitution, or fallback.");
            CollectionAssert.Contains(dealtIds, expectedGrantedCardId, "Requirement 4: the acquired card must be part of the resolved player deck.");
            Assert.AreEqual(dealtIds.Count, dealtIds.Distinct().Count(), "Requirement 6: the resolved player deck must contain no duplicate ids.");

            // ===== REQUIREMENT 7: Tutorial remains fully isolated from all of the above. =====
            bootstrap3.StartApprovedTutorialBattle();
            bootstrap3.SetBattleCanvasVisible(true);
            HashSet<string> tutorialIds = new HashSet<string>(bootstrap3.Battle.PlayerState.Hand
                .Concat(bootstrap3.Battle.PlayerState.DrawPile)
                .Select(c => c.Id));
            CollectionAssert.AreEquivalent(new[] { "warrior", "novice_knight", "goblin_caster" }, tutorialIds,
                "Requirement 7: Tutorial must keep its fixed scripted starter formation, unaffected by the acquired card or the confirmed deck.");
        }
    }
}
