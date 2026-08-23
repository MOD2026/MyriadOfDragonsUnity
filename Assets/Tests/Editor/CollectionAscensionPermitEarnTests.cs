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
    /// <summary>Production permit earn hook on first Chapter 1 clear (stage 1-3).</summary>
    public class CollectionAscensionPermitEarnTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsPermitEarn_" + System.Guid.NewGuid().ToString("N"));
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
                Directory.Delete(_scratchSaveDir, recursive: true);
        }

        [Test]
        public void FirstClearOfChapter1Boss_GrantsOnePermit_RespectingCaps()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnBootstrap();
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHome(controller);
            presenter.SetActiveStageForTests(CampaignMapPresenter.GetStageForTests(HomePagePresenter.FirstCampaignChapterClearStageId));

            PlayerProfile profile = SaveManager.SaveData;
            CollectionSchemaMigration.Apply(profile);
            Assert.AreEqual(0, profile.ascensionPermitBalance);
            string weekKeyBefore = profile.ascensionPermitWeekKey;
            int weeklyBefore = profile.ascensionPermitsEarnedThisWeek;

            PlayOneCardAndWin(controller);

            Assert.AreEqual(1, profile.ascensionPermitBalance);
            Assert.AreEqual(weeklyBefore, profile.ascensionPermitsEarnedThisWeek,
                "Ch1 milestone grant must not touch the weekly earned counter.");
            Assert.AreEqual(weekKeyBefore, profile.ascensionPermitWeekKey,
                "Ch1 milestone grant must not set a week key.");
        }

        [Test]
        public void ChapterFinale_AtFullHoard_GrantsZero_SilentLoss_WeekKeyUntouched()
        {
            // CC-locked: full-hoard finale grant is a silent loss (no overflow queue).
            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            profile.ascensionPermitBalance = CollectionSchemaRules.AscensionPermitHoardCap;
            profile.ascensionPermitWeekKey = "full-hoard-week";
            profile.ascensionPermitsEarnedThisWeek = 7;

            Assert.AreEqual(0, HomePagePresenter.TryGrantChapterFinalePermit(profile, "1-3"));
            Assert.AreEqual(0, HomePagePresenter.TryGrantChapterFinalePermit(profile, "10-30"));
            Assert.AreEqual(CollectionSchemaRules.AscensionPermitHoardCap, profile.ascensionPermitBalance);
            Assert.AreEqual("full-hoard-week", profile.ascensionPermitWeekKey);
            Assert.AreEqual(7, profile.ascensionPermitsEarnedThisWeek);
        }

        [Test]
        public void ChapterFinale_CapCollision_FifteenThenSixteen_NextFinaleZero()
        {
            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            profile.ascensionPermitBalance = CollectionSchemaRules.AscensionPermitHoardCap - 1;
            profile.ascensionPermitWeekKey = "cap-collision-week";
            profile.ascensionPermitsEarnedThisWeek = 0;

            Assert.AreEqual(1, HomePagePresenter.TryGrantChapterFinalePermit(profile, "3-30"));
            Assert.AreEqual(CollectionSchemaRules.AscensionPermitHoardCap, profile.ascensionPermitBalance);

            Assert.AreEqual(0, HomePagePresenter.TryGrantChapterFinalePermit(profile, "4-30"),
                "Next finale at full hoard must grant 0 (silent loss).");
            Assert.AreEqual(CollectionSchemaRules.AscensionPermitHoardCap, profile.ascensionPermitBalance);
            Assert.AreEqual("cap-collision-week", profile.ascensionPermitWeekKey);
            Assert.AreEqual(0, profile.ascensionPermitsEarnedThisWeek);
        }

        [Test]
        public void TryGrantMilestone_Direct_DoesNotTouchWeeklyLedger()
        {
            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            profile.ascensionPermitWeekKey = "existing-week";
            profile.ascensionPermitsEarnedThisWeek = 4;

            Assert.AreEqual(1, HomePagePresenter.TryGrantChapterFinalePermit(
                profile, HomePagePresenter.FirstCampaignChapterClearStageId));
            Assert.AreEqual(1, profile.ascensionPermitBalance);
            Assert.AreEqual(4, profile.ascensionPermitsEarnedThisWeek);
            Assert.AreEqual("existing-week", profile.ascensionPermitWeekKey);

            Assert.AreEqual(0, HomePagePresenter.TryGrantChapterFinalePermit(profile, "1-1"));
            Assert.AreEqual(1, profile.ascensionPermitBalance);
        }

        [Test]
        public void ChapterFinaleStages_EachGrantOnce_NonFinaleGrantsZero_WeekKeyUntouched()
        {
            var profile = new PlayerProfile();
            CollectionSchemaMigration.Apply(profile);
            profile.ascensionPermitWeekKey = "locked-week";
            profile.ascensionPermitsEarnedThisWeek = 2;

            Assert.AreEqual(10, HomePagePresenter.ChapterFinalePermitStageIds.Length);
            CollectionAssert.Contains(HomePagePresenter.ChapterFinalePermitStageIds, "1-3");
            CollectionAssert.Contains(HomePagePresenter.ChapterFinalePermitStageIds, "2-21");
            CollectionAssert.Contains(HomePagePresenter.ChapterFinalePermitStageIds, "10-30");

            int expectedBalance = 0;
            foreach (string stageId in HomePagePresenter.ChapterFinalePermitStageIds)
            {
                Assert.IsTrue(HomePagePresenter.IsChapterFinalePermitStage(stageId), stageId);
                Assert.AreEqual(1, HomePagePresenter.TryGrantChapterFinalePermit(profile, stageId),
                    $"Finale {stageId} must grant 1 permit.");
                expectedBalance++;
                Assert.AreEqual(expectedBalance, profile.ascensionPermitBalance);
            }

            Assert.AreEqual(10, profile.ascensionPermitBalance);
            Assert.AreEqual("locked-week", profile.ascensionPermitWeekKey);
            Assert.AreEqual(2, profile.ascensionPermitsEarnedThisWeek);

            Assert.AreEqual(0, HomePagePresenter.TryGrantChapterFinalePermit(profile, "2-1"),
                "Non-finale must grant 0.");
            Assert.AreEqual(0, HomePagePresenter.TryGrantChapterFinalePermit(profile, "1-1"));
            Assert.AreEqual(10, profile.ascensionPermitBalance);
            Assert.AreEqual("locked-week", profile.ascensionPermitWeekKey);
            Assert.AreEqual(2, profile.ascensionPermitsEarnedThisWeek);
        }

        [Test]
        public void ChapterFinale_ReplayViaClaimedStage_DoesNotGrantAgain()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnBootstrap();
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHome(controller);

            CampaignStageData stage = CampaignMapPresenter.GetStageForTests("2-21");
            Assert.NotNull(stage, "Setup: stage 2-21 must exist in the campaign list.");
            presenter.SetActiveStageForTests(stage);

            PlayerProfile profile = SaveManager.SaveData;
            CollectionSchemaMigration.Apply(profile);
            profile.ascensionPermitWeekKey = "wk";
            profile.ascensionPermitsEarnedThisWeek = 1;

            PlayOneCardAndWin(controller);
            Assert.AreEqual(1, profile.ascensionPermitBalance);
            Assert.AreEqual("wk", profile.ascensionPermitWeekKey);
            Assert.AreEqual(1, profile.ascensionPermitsEarnedThisWeek);

            bootstrap.PlayAgainForTests();
            controller = bootstrap.Battle;
            presenter.SetActiveStageForTests(stage);
            bootstrap.Battle.SetMirroredEnemySpellsEnabledForTests(false);
            PlayOneCardAndWin(controller);

            Assert.AreEqual(1, profile.ascensionPermitBalance, "Replay must not grant a second permit.");
            Assert.AreEqual("wk", profile.ascensionPermitWeekKey);
            Assert.AreEqual(1, profile.ascensionPermitsEarnedThisWeek);
        }

        [Test]
        public void FirstClearOfNonChapterBoss_DoesNotGrantPermit()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnBootstrap();
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHome(controller);
            presenter.SetActiveStageForTests(CampaignMapPresenter.GetStageForTests("1-1"));

            PlayerProfile profile = SaveManager.SaveData;
            CollectionSchemaMigration.Apply(profile);

            PlayOneCardAndWin(controller);

            Assert.AreEqual(0, profile.ascensionPermitBalance);
        }

        [Test]
        public void ReplayingChapter1Boss_DoesNotGrantDuplicatePermit()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnBootstrap();
            BattleController controller = bootstrap.Battle;
            HomePagePresenter presenter = SpawnHome(controller);
            presenter.SetActiveStageForTests(CampaignMapPresenter.GetStageForTests(HomePagePresenter.FirstCampaignChapterClearStageId));

            PlayOneCardAndWin(controller);
            PlayerProfile profile = SaveManager.SaveData;
            Assert.AreEqual(1, profile.ascensionPermitBalance);

            bootstrap.PlayAgainForTests();
            controller = bootstrap.Battle;
            PlayOneCardAndWin(controller);

            Assert.AreEqual(1, profile.ascensionPermitBalance,
                "Replaying an already-cleared chapter boss must not mint duplicate permits.");
        }

        private GameBootstrap SpawnBootstrap()
        {
            var go = new GameObject("PermitEarnBootstrap");
            _spawned.Add(go);
            GameBootstrap bootstrap = go.AddComponent<GameBootstrap>();
            bootstrap.Initialize();
            foreach (string spawnedName in new[] { "Canvas", "EventSystem", "CardDatabase", "BattleController" })
            {
                GameObject spawned = GameObject.Find(spawnedName);
                if (spawned != null) _spawned.Add(spawned);
            }

            // Permit-earn fixtures assert reward attribution, not combat balance. Mirrored AI spells
            // (Option B) can flip a one-card win into a tick-cap loss — keep them off here.
            bootstrap.Battle.SetMirroredEnemySpellsEnabledForTests(false);
            return bootstrap;
        }

        private HomePagePresenter SpawnHome(BattleController presenterBattle)
        {
            var go = new GameObject("HomePagePresenterUnderTest_PermitEarn");
            _spawned.Add(go);
            var presenter = go.AddComponent<HomePagePresenter>();
            presenter.BindBattleControllerForTests(presenterBattle);
            return presenter;
        }

        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("PermitEarn_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;
            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();

            PlayerProfile profile = new PlayerProfile();
            profile.cardCollection = new List<string>(deckIds);
            profile.ApplyDataToEmpire();
            profile.activeDeckCardIds = new List<string>(deckIds);
            SaveSystem.Save(profile);
            SaveSystem.ResetCurrentProfileForTests();
            Object.DestroyImmediate(databaseGo);
        }

        private static void PlayOneCardAndWin(BattleController controller)
        {
            Card anyCard = controller.PlayerState.Hand.First(c => c.ResourceCost <= controller.PlayerState.Resource);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, anyCard, Lane.Front));
            Assert.IsTrue(controller.ConfirmFormation());
            while (controller.Phase == BattlePhase.Combat)
                controller.AdvanceCombatTick();
        }
    }
}
