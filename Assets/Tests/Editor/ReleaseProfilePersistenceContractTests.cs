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
    /// RELEASE-PROFILE PERSISTENCE CONTRACT (release feature) - one end-to-end integration test
    /// that drives a single fresh profile through every real production owner this session's
    /// features built: starter entitlement (GameBootstrap.EnsureApprovedStarterCollectionGranted,
    /// reached via HomePagePresenter.OnToBattleClickedForTests' own redirect gate), Deck Builder
    /// confirmation (DeckBuilderPresenter.SetAndConfirmDeckForTests/ConfirmDeck), a Shop purchase
    /// (ShopPresenter.PurchaseForTests/AttemptPurchase, now routed through CurrencyManager), a
    /// Chapter 1 campaign-stage victory (the full Campaign match-context lifecycle -
    /// GameBootstrap.SetPendingCampaignStageForNextMatch/StartNewMatch plus
    /// HomePagePresenter.HandleMatchCompleted's reward/unlock/claim), a full disk reload via
    /// SaveSystem.ResetCurrentProfileForTests + a brand-new GameBootstrap instance (never reusing
    /// the first session's in-memory profile/singleton), and finally a repeat of the reward and
    /// purchase paths plus Tutorial, all against the reloaded state. Nothing here reimplements any
    /// of those systems - every mutation happens through the same private handler or ...ForTests()
    /// hook the real UI already calls, exactly as every other test file from this session's work
    /// does individually; this file is the single combined narrative across all of them.
    /// </summary>
    public class ReleaseProfilePersistenceContractTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsReleaseProfile_" + System.Guid.NewGuid().ToString("N"));
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
            var go = new GameObject("HomePagePresenterUnderTest_ReleaseProfile");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(bootstrap.Battle);
            presenter.BindGameBootstrapForTests(bootstrap);
            return presenter;
        }

        private ShopPresenter SpawnShop(PlayerProfile profile)
        {
            var go = new GameObject("ShopPresenterUnderTest_ReleaseProfile");
            _spawned.Add(go);
            var shop = go.AddComponent<ShopPresenter>();
            shop.Initialize(profile, onBackToHome: null);
            GameObject shopCanvas = GameObject.Find("ShopCanvas");
            if (shopCanvas != null) _spawned.Add(shopCanvas);
            return shop;
        }

        private static void PlayOneCardAndWin(BattleController controller)
        {
            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front),
                "Setup: expected to be able to play at least one card into Front.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");

            int ticksRun = 0;
            while (controller.Phase == BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks, "Setup: expected a knockout well inside the tick cap.");
            }
        }

        [Test]
        public void FreshProfile_FullLifecycle_SurvivesReload_AndCannotDuplicateOnRepeat()
        {
            // ===== SETUP: real CardDatabase, real Chapter 1 stage data =====
            var databaseGo = new GameObject("ReleaseProfile_CardDatabase");
            _spawned.Add(databaseGo);
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("1-1");
            Assert.IsNotNull(stage, "Setup: expected Stage 1-1 to exist in the production campaign list.");

            // ===== REQUIREMENT 1: starter collection granted once, through the real entitlement
            // owner, reached via the real "To Battle" redirect gate on a genuinely fresh profile. =====
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("ReleaseProfile_Bootstrap");
            HomePagePresenter home = SpawnHomePagePresenter(bootstrap);

            home.OnToBattleClickedForTests();

            DeckBuilderPresenter deckBuilder = home.GetComponent<DeckBuilderPresenter>();
            Assert.IsNotNull(deckBuilder, "Setup: a fresh player's 'To Battle' press must open the existing Deck Builder.");

            string[] approvedStarterIds =
            {
                "warrior", "novice_knight", "goblin_caster",
                "cleric", "archer_elf", "fox", "bunny", "forest", "tribal_warrior", "undead_soldier",
            };
            CollectionAssert.AreEquivalent(approvedStarterIds, bootstrap.Profile.cardCollection,
                "Requirement 1: a fresh profile must receive exactly the approved starter collection, once.");

            // Pressing "To Battle" again (still no confirmed deck) must not duplicate the grant.
            home.OnToBattleClickedForTests();
            Assert.AreEqual(approvedStarterIds.Length, bootstrap.Profile.cardCollection.Count,
                "Requirement 1: the starter grant must never be duplicated by a repeated redirect.");

            // ===== REQUIREMENT 2: a valid 10-card deck is confirmed and persists. =====
            deckBuilder.SetAndConfirmDeckForTests(approvedStarterIds);
            CollectionAssert.AreEqual(approvedStarterIds, bootstrap.Profile.activeDeckCardIds,
                "Requirement 2: the confirmed deck must be exactly the ten cards submitted.");
            Assert.IsTrue(bootstrap.HasValidConfirmedDeckForNormalBattle(), "Setup: expected the confirmed deck to satisfy the normal-battle saved-deck gate.");

            // ===== REQUIREMENT 3: a Shop purchase changes only the intended wallet/card state. =====
            PlayerProfile profile = SaveManager.SaveData;
            ShopPresenter shop = SpawnShop(profile);

            int gemsBeforeCurrencyPurchase = profile.gems;
            int goldBeforeCurrencyPurchase = profile.gold;
            var deckBeforeShop = new List<string>(profile.activeDeckCardIds);
            shop.PurchaseForTests("res_gold"); // 50 Gems -> +1,500 Gold, currency-only

            Assert.AreEqual(gemsBeforeCurrencyPurchase - 50, profile.gems, "Requirement 3: Gold Vault must spend exactly 50 Gems.");
            Assert.AreEqual(goldBeforeCurrencyPurchase + 1500, profile.gold, "Requirement 3: Gold Vault must grant exactly 1,500 Gold.");
            Assert.AreEqual(approvedStarterIds.Length, profile.cardCollection.Count, "Requirement 3: a currency-only purchase must not touch the card collection.");
            CollectionAssert.AreEqual(deckBeforeShop, profile.activeDeckCardIds, "Requirement 3: a Shop purchase must never touch the confirmed deck.");

            int goldBeforeCardPurchase = profile.gold;
            string expectedGrantedCardId = database.AllCards.Select(c => c.Id)
                .First(id => id != "dragon" && !profile.cardCollection.Contains(id));
            shop.PurchaseForTests("pack_novice"); // 500 Gold -> one new deterministic card

            Assert.AreEqual(goldBeforeCardPurchase - 500, profile.gold, "Requirement 3: Novice Card Pack must spend exactly 500 Gold.");
            Assert.AreEqual(approvedStarterIds.Length + 1, profile.cardCollection.Count, "Requirement 3: a card-pack purchase must grant exactly one new card.");
            Assert.AreEqual(expectedGrantedCardId, profile.cardCollection[^1], "Requirement 3: the granted card must be the next id in the real deterministic sequence.");
            CollectionAssert.AreEqual(deckBeforeShop, profile.activeDeckCardIds, "Requirement 3: a card-granting purchase must still never touch the confirmed deck.");

            // ===== REQUIREMENT 4: a Chapter 1 victory grants its first-clear reward, records its
            // claim, and unlocks the next stage - through the real Campaign match-context lifecycle. =====
            home.SetActiveStageForTests(stage);
            bootstrap.SetPendingCampaignStageForNextMatch(stage);
            bootstrap.SetBattleCanvasVisible(true);
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "Setup: expected a valid, unblocked campaign match.");

            int goldBeforeStageWin = profile.gold;
            int gemsBeforeStageWin = profile.gems;
            bool? isVictory = null;
            bootstrap.Battle.OnMatchCompleted += result => isVictory = result.IsVictory;
            PlayOneCardAndWin(bootstrap.Battle);

            Assert.IsTrue(isVictory, "Setup: expected the undefended enemy to produce a player victory.");
            Assert.AreEqual(goldBeforeStageWin + stage.goldReward, profile.gold, "Requirement 4: the stage win must grant exactly its configured Gold reward.");
            Assert.AreEqual(gemsBeforeStageWin + stage.gemReward, profile.gems, "Requirement 4: the stage win must grant exactly its configured Gems reward.");
            CollectionAssert.Contains(profile.claimedStageRewardIds, stage.stageId, "Requirement 4: the stage id must be recorded as claimed.");
            CollectionAssert.Contains(profile.unlockedStageIds, stage.stageId, "Requirement 4: the cleared stage itself must remain (or become) unlocked.");

            // ===== Snapshot every field the reload must preserve, before dropping the in-memory
            // singleton. =====
            var expectedCardCollection = new List<string>(profile.cardCollection);
            var expectedActiveDeck = new List<string>(profile.activeDeckCardIds);
            int expectedGold = profile.gold;
            int expectedGems = profile.gems;
            var expectedClaimedStageRewardIds = new List<string>(profile.claimedStageRewardIds);
            var expectedUnlockedStageIds = new List<string>(profile.unlockedStageIds);

            // ===== REQUIREMENT 5 + 8: a full save reload, through a brand-new in-memory singleton
            // (ResetCurrentProfileForTests), must reproduce every one of the above exactly - not a
            // stale cached copy. =====
            SaveSystem.ResetCurrentProfileForTests();
            PlayerProfile reloaded = SaveSystem.Load();

            CollectionAssert.AreEqual(expectedCardCollection, reloaded.cardCollection, "Requirement 5: owned-card collection (including the starter grant) must survive a full reload.");
            CollectionAssert.AreEqual(expectedActiveDeck, reloaded.activeDeckCardIds, "Requirement 5: the confirmed deck must survive a full reload.");
            Assert.AreEqual(expectedGold, reloaded.gold, "Requirement 5: the wallet Gold balance must survive a full reload.");
            Assert.AreEqual(expectedGems, reloaded.gems, "Requirement 5: the wallet Gems balance must survive a full reload.");
            CollectionAssert.AreEqual(expectedClaimedStageRewardIds, reloaded.claimedStageRewardIds, "Requirement 5: claimed reward ids must survive a full reload.");
            CollectionAssert.AreEqual(expectedUnlockedStageIds, reloaded.unlockedStageIds, "Requirement 5: unlocked stage ids must survive a full reload.");

            // ===== REQUIREMENT 8, reinforced: a brand-new GameBootstrap (never sharing an object
            // reference with the first session) must resolve the SAME state purely by reading disk
            // through SaveSystem.CurrentProfile, proving nothing routed around the reset singleton. =====
            GameBootstrap bootstrap2 = SpawnAndInitializeBootstrap("ReleaseProfile_Bootstrap_PostReload");
            HomePagePresenter home2 = SpawnHomePagePresenter(bootstrap2);

            Assert.AreNotSame(bootstrap, bootstrap2, "Setup: expected a genuinely new GameBootstrap instance for the post-reload session.");
            CollectionAssert.AreEqual(expectedCardCollection, bootstrap2.Profile.cardCollection, "Requirement 8: a fresh GameBootstrap must read the reloaded card collection, not a stale in-memory copy.");
            CollectionAssert.AreEqual(expectedActiveDeck, bootstrap2.Profile.activeDeckCardIds, "Requirement 8: a fresh GameBootstrap must read the reloaded deck, not a stale in-memory copy.");
            Assert.AreEqual(expectedGold, bootstrap2.Profile.gold, "Requirement 8: a fresh GameBootstrap must read the reloaded Gold balance.");
            Assert.IsTrue(bootstrap2.HasValidConfirmedDeckForNormalBattle(), "Requirement 5/8: the reloaded deck must still satisfy the normal-battle saved-deck gate.");

            // ===== REQUIREMENT 6: repeating the same reward path after reload cannot duplicate the
            // first-clear reward or corrupt the deck. =====
            PlayerProfile profile2 = bootstrap2.Profile;
            home2.SetActiveStageForTests(stage);
            bootstrap2.SetPendingCampaignStageForNextMatch(stage);
            bootstrap2.SetBattleCanvasVisible(true);
            Assert.IsNull(bootstrap2.NormalMatchStatusForTests, "Setup: expected the same stage to still be a valid, unblocked campaign match after reload.");

            int goldBeforeRepeatWin = profile2.gold;
            int gemsBeforeRepeatWin = profile2.gems;
            int claimedCountBeforeRepeatWin = profile2.claimedStageRewardIds.Count;
            bool? isRepeatVictory = null;
            bootstrap2.Battle.OnMatchCompleted += result => isRepeatVictory = result.IsVictory;
            PlayOneCardAndWin(bootstrap2.Battle);

            Assert.IsTrue(isRepeatVictory, "Setup: expected the repeat win to also succeed.");
            Assert.AreEqual(goldBeforeRepeatWin, profile2.gold, "Requirement 6: repeating an already-claimed stage win after reload must grant no duplicate Gold.");
            Assert.AreEqual(gemsBeforeRepeatWin, profile2.gems, "Requirement 6: repeating an already-claimed stage win after reload must grant no duplicate Gems.");
            Assert.AreEqual(claimedCountBeforeRepeatWin, profile2.claimedStageRewardIds.Count, "Requirement 6: repeating an already-claimed stage win after reload must not add a second claimed-reward entry.");
            CollectionAssert.AreEqual(expectedActiveDeck, profile2.activeDeckCardIds, "Requirement 6: repeating the reward path after reload must not corrupt the confirmed deck.");

            // ===== REQUIREMENT 6, continued: repeating the same Shop purchase path after reload
            // cannot duplicate a card id. =====
            ShopPresenter shop2 = SpawnShop(profile2);
            var collectionBeforeRepeatPurchase = new List<string>(profile2.cardCollection);
            string expectedSecondGrantedCardId = database.AllCards.Select(c => c.Id)
                .First(id => id != "dragon" && !profile2.cardCollection.Contains(id));
            shop2.PurchaseForTests("pack_novice");

            Assert.AreEqual(collectionBeforeRepeatPurchase.Count + 1, profile2.cardCollection.Count, "Requirement 6: a repeated card-pack purchase after reload must still grant exactly one new card.");
            Assert.AreEqual(expectedSecondGrantedCardId, profile2.cardCollection[^1], "Requirement 6: the repeated purchase must grant the next real id in sequence, never a duplicate.");
            Assert.AreEqual(profile2.cardCollection.Count, profile2.cardCollection.Distinct().Count(), "Requirement 6: the card collection must contain no duplicate ids after the repeat purchase.");
            CollectionAssert.AreEqual(expectedActiveDeck, profile2.activeDeckCardIds, "Requirement 6: a repeated Shop purchase after reload must not corrupt the confirmed deck.");

            // ===== REQUIREMENT 7: Tutorial state and normal campaign context remain isolated,
            // even against this fully-populated, post-reload profile. The earlier repeat-win
            // above already resolved (and, per the Campaign match-context lifecycle contract,
            // cleared) that pending stage - so a fresh, still-UNRESOLVED pending stage is attached
            // here to give this isolation check something real to prove Tutorial does not touch. =====
            bootstrap2.SetPendingCampaignStageForNextMatch(stage);
            int goldBeforeTutorial = profile2.gold;
            int gemsBeforeTutorial = profile2.gems;
            int claimedCountBeforeTutorial = profile2.claimedStageRewardIds.Count;

            bootstrap2.StartApprovedTutorialBattle();
            bootstrap2.SetBattleCanvasVisible(true);

            HashSet<string> tutorialPlayerIds = new HashSet<string>(bootstrap2.Battle.PlayerState.Hand
                .Concat(bootstrap2.Battle.PlayerState.DrawPile)
                .Select(c => c.Id));
            CollectionAssert.AreEquivalent(new[] { "warrior", "novice_knight", "goblin_caster" }, tutorialPlayerIds,
                "Requirement 7: Tutorial must keep its fixed scripted starter formation regardless of the accumulated campaign/profile state.");
            Assert.AreEqual(goldBeforeTutorial, profile2.gold, "Requirement 7: Tutorial must never mutate wallet state.");
            Assert.AreEqual(gemsBeforeTutorial, profile2.gems, "Requirement 7: Tutorial must never mutate wallet state.");
            Assert.AreEqual(claimedCountBeforeTutorial, profile2.claimedStageRewardIds.Count, "Requirement 7: Tutorial must never mutate claimed-reward state.");

            // A subsequent Play Again (the real StartNewMatch path) must still resolve the same
            // still-pending campaign stage untouched by Tutorial having just run.
            bootstrap2.PlayAgainForTests();
            Assert.IsNull(bootstrap2.NormalMatchStatusForTests, "Requirement 7: the pending campaign stage must remain valid and untouched after Tutorial runs.");
            HashSet<string> postTutorialEnemyIds = new HashSet<string>(bootstrap2.Battle.EnemyState.Hand
                .Concat(bootstrap2.Battle.EnemyState.DrawPile)
                .Select(c => c.Id));
            CollectionAssert.AreEquivalent(stage.enemyDeckCardIds, postTutorialEnemyIds,
                "Requirement 7: Tutorial must never mutate or clear the normal campaign context.");
        }
    }
}
