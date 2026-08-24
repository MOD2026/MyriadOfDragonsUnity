using System.Collections.Generic;
using System.IO;
using MyriadOfDragons.Empire;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
        /// Session-shipped metagame screens — chained navigation (not isolated reachability).
        /// Home → Collection → Deck → Collection → Home → Shop → Home → Pass → Home → Login → Home
        /// → Settings → Home → Empire → building-detail variants → Expedition → Empire → Home
        /// → Avatar → Home.
    /// </summary>
    public class MetagameNavigationSpineTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        private static readonly string[] MetagameCanvasNames =
        {
            "CampaignMapCanvas",
            "ShopCanvas",
            "DeckBuilderCanvas",
            "CollectionCanvas",
            "EmpireCanvas",
            "EmpireExpeditionCanvas",
            "SettingsCanvas",
            "AvatarCanvas",
            "BattlePassCanvas",
            "DailyLoginQuestsCanvas",
            "EmpireBuildingDetailCanvas",
            "BazaarCanvas",
            "GuildHallEntryCanvas",
            "ChatSocialCanvas",
            "MemoryExpeditionCanvas",
            "MailInboxCanvas",
            "FriendsCanvas",
        };

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(),
                "MyriadOfDragonsNavSpine_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
            PurgeOrphanMetagameCanvases();

            var profile = new PlayerProfile { gold = 5000, gems = 500, stamina = 50, maxStamina = 100 };
            CollectionSchemaMigration.Apply(profile);
            foreach (string cardId in CardTileCompositionV1.PocCardIds)
            {
                profile.cardProgression.Add(new CardProgressionRecord
                {
                    cardId = cardId,
                    copyCount = 1,
                    cardLevel = 1,
                    evolutionStep = 0,
                    trainingXp = 0,
                });
            }

            Assert.IsTrue(SaveSystem.Save(profile));
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            PurgeOrphanMetagameCanvases();
            CampaignMapPresenter.CleanupStaleMetagameCanvases();
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
        public void FullMetagameSpine_NavigationRoundTrips_NoStaleCanvases_PocDeckTilesVisible()
        {
            HomePagePresenter home = SpawnHome();
            GameObject homeCanvas = home.HomeCanvasObjectForTests;
            Assert.IsTrue(homeCanvas.activeSelf);

            // --- Home → Collection ---
            Click(homeCanvas, "NavigationStage/Btn_Cards");
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find("CollectionCanvas"));
            Assert.NotNull(home.GetComponent<CollectionPresenter>());

            var collection = home.GetComponent<CollectionPresenter>();
            Assert.NotNull(collection);
            Assert.That(collection.VisibleCardIdsForTests, Does.Contain("warrior"));

            if (CardTileCompositionV1.HasPack)
            {
                Transform warriorTile = GameObject.Find("CollectionCanvas")
                    ?.transform.Find("GridPanel/CollectionScroll/Viewport/Content/OwnedCard_warrior");
                Assert.NotNull(warriorTile, "POC warrior tile must render in Collection grid.");
                Assert.NotNull(warriorTile.Find("CardFrame"), "Collection POC tile must use V1 frame layer.");
            }

            // --- Collection → Deck Builder ---
            Click(GameObject.Find("CollectionCanvas"), "OpenDeckBuilderButton");
            Assert.IsNull(GameObject.Find("CollectionCanvas"));
            Assert.NotNull(GameObject.Find("DeckBuilderCanvas"));
            Assert.IsNull(home.GetComponent<CollectionPresenter>());

            Transform deckWarrior = GameObject.Find("DeckBuilderCanvas")
                ?.transform.Find("CollectionPanel/CollectionScroll/Viewport/Content/Card_warrior");
            Assert.NotNull(deckWarrior, "Owned warrior must appear in Deck Builder grid.");
            if (CardTileCompositionV1.HasPack)
                Assert.NotNull(deckWarrior.Find("CardPortrait"), "Deck Builder POC card must use V1 portrait layer.");

            // --- Deck → Collection (return path) ---
            Click(GameObject.Find("DeckBuilderCanvas"), "ActionRail/Btn_Back_Rail");
            Assert.NotNull(GameObject.Find("CollectionCanvas"));
            Assert.IsNull(GameObject.Find("DeckBuilderCanvas"));
            Assert.IsNull(home.GetComponent<DeckBuilderPresenter>());

            // --- Collection → Home ---
            Click(GameObject.Find("CollectionCanvas"), "BackButton");
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(home.GetComponent<CollectionPresenter>());
            AssertNoMetagameCanvases();

            int goldBeforeShop = SaveSystem.CurrentProfile.gold;

            // --- Home → Shop V1 ---
            Click(homeCanvas, "NavigationStage/Btn_Shop");
            Assert.IsFalse(homeCanvas.activeSelf);
            GameObject shopCanvas = GameObject.Find("ShopCanvas");
            Assert.NotNull(shopCanvas);
            Assert.IsTrue(ShopV1UiLibrary.HasShopV1Pack);
            Image shopBg = shopCanvas.transform.Find("Background")?.GetComponent<Image>();
            Assert.NotNull(shopBg?.sprite);
            Assert.AreEqual(ShopV1UiLibrary.CatalogShellName, shopBg.sprite.name);

            // --- Shop → Home ---
            Click(shopCanvas, "HeaderBar/Btn_Back");
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(home.GetComponent<ShopPresenter>());
            AssertNoMetagameCanvases();
            Assert.AreEqual(goldBeforeShop, SaveSystem.CurrentProfile.gold, "Shop back must not mutate wallet.");

            // --- Home → Battle Pass ---
            Click(homeCanvas, "Btn_BattlePass");
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(BattlePassPresenter.CanvasName));
            Assert.NotNull(home.GetComponent<BattlePassPresenter>());
            Assert.AreEqual("28-DAY SEASON",
                GameObject.Find(BattlePassPresenter.CanvasName).transform
                    .Find("BattlePassHeader/SeasonLength")?.GetComponent<Text>()?.text);
            Click(GameObject.Find(BattlePassPresenter.CanvasName), "BattlePassHeader/Btn_Back");
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(home.GetComponent<BattlePassPresenter>());
            AssertNoMetagameCanvases();

            // --- Home → Daily Login / Quests ---
            Click(homeCanvas, "Btn_DailyLogin");
            Assert.IsFalse(homeCanvas.activeSelf);
            Assert.NotNull(GameObject.Find(DailyLoginQuestsPresenter.CanvasName));
            Assert.NotNull(home.GetComponent<DailyLoginQuestsPresenter>());
            StringAssert.Contains("PAUSED", home.GetComponent<DailyLoginQuestsPresenter>().StatusTextForTests);
            Click(GameObject.Find(DailyLoginQuestsPresenter.CanvasName), "DailyLoginHeader/Btn_Back");
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(home.GetComponent<DailyLoginQuestsPresenter>());
            AssertNoMetagameCanvases();

            // --- Home → Settings ---
            Click(homeCanvas, "Btn_Settings");
            Assert.IsFalse(homeCanvas.activeSelf);
            GameObject settingsCanvas = GameObject.Find("SettingsCanvas");
            Assert.NotNull(settingsCanvas);
            Click(settingsCanvas, "SettingsHeader/Btn_Back");
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(home.GetComponent<SettingsPresenter>());
            AssertNoMetagameCanvases();

            // --- Home → Empire → Expedition → Empire → Home ---
            Click(homeCanvas, "NavigationStage/Btn_Empire");
            Assert.IsFalse(homeCanvas.activeSelf);
            GameObject empireCanvas = GameObject.Find("EmpireCanvas");
            Assert.NotNull(empireCanvas);

            OpenAndCloseBuildingDetail(home, empireCanvas, "EmpireConstructionRoot/CastleRow",
                EmpireBuildingKind.Castle);
            OpenAndCloseBuildingDetail(home, GameObject.Find("EmpireCanvas"), "EmpireConstructionRoot/BarracksRow",
                EmpireBuildingKind.Barracks);
            OpenAndCloseBuildingDetail(home, GameObject.Find("EmpireCanvas"), "EmpireConstructionRoot/GateRow",
                EmpireBuildingKind.Gate);
            OpenAndCloseGuildHallEntry(home, GameObject.Find("EmpireCanvas"));
            OpenAndCloseBuildingDetail(home, GameObject.Find("EmpireCanvas"),
                "EmpireConstructionRoot/VariantStrip/Chip_Prison", EmpireBuildingKind.Prison);
            OpenAndCloseBuildingDetail(home, GameObject.Find("EmpireCanvas"),
                "EmpireConstructionRoot/VariantStrip/Chip_Embassy", EmpireBuildingKind.Embassy);

            Assert.NotNull(GameObject.Find("EmpireCanvas"), "Empire must remain after closing every building popup.");
            Assert.IsNull(GameObject.Find(EmpireBuildingDetailPresenter.CanvasName));
            Assert.IsNull(home.GetComponent<EmpireBuildingDetailPresenter>());
            Assert.IsNull(GameObject.Find(GuildHallEntryPresenter.CanvasName));
            Assert.IsNull(home.GetComponent<GuildHallEntryPresenter>());

            Click(GameObject.Find("EmpireCanvas"), "EmpireHeader/OpenExpeditionButton");
            Assert.IsNull(GameObject.Find("EmpireCanvas"));
            GameObject expeditionCanvas = GameObject.Find(EmpireExpeditionPresenter.CanvasName);
            Assert.NotNull(expeditionCanvas);
            Assert.NotNull(home.GetComponent<EmpireExpeditionPresenter>());

            Click(expeditionCanvas, "ExpeditionHeader/Btn_Back");
            Assert.NotNull(GameObject.Find("EmpireCanvas"));
            Assert.IsNull(GameObject.Find(EmpireExpeditionPresenter.CanvasName));
            Assert.IsNull(home.GetComponent<EmpireExpeditionPresenter>());

            Click(GameObject.Find("EmpireCanvas"), "EmpireHeader/Btn_Back");
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(home.GetComponent<EmpirePresenter>());
            AssertNoMetagameCanvases();

            // --- Home → Avatar → Home (direct tile) ---
            Click(homeCanvas, "NavigationStage/Btn_Avatar");
            Assert.IsFalse(homeCanvas.activeSelf);
            GameObject avatarCanvas = GameObject.Find("AvatarCanvas");
            Assert.NotNull(avatarCanvas);
            Click(avatarCanvas, "AvatarHeader/Btn_Back");
            Assert.IsTrue(homeCanvas.activeSelf);
            Assert.IsNull(home.GetComponent<AvatarPresenter>());
            AssertNoMetagameCanvases();
        }

        private HomePagePresenter SpawnHome()
        {
            var go = new GameObject("HomePagePresenter_NavSpine");
            _spawned.Add(go);
            var home = go.AddComponent<HomePagePresenter>();
            home.BuildHomePageUIForTests();
            return home;
        }

        private static void OpenAndCloseGuildHallEntry(HomePagePresenter home, GameObject empireCanvas)
        {
            Click(empireCanvas, "EmpireConstructionRoot/VariantStrip/Chip_GuildHall");
            GameObject entry = GameObject.Find(GuildHallEntryPresenter.CanvasName);
            Assert.NotNull(entry, "Guild Hall chip must open the flat-entry art popup.");
            Assert.NotNull(GameObject.Find("EmpireCanvas"), "Empire canvas must stay under the Guild Hall popup.");
            Assert.NotNull(home.GetComponent<GuildHallEntryPresenter>());
            Click(entry, "GuildHallHeader/Btn_Back");
            Assert.IsNull(GameObject.Find(GuildHallEntryPresenter.CanvasName));
            Assert.IsNull(home.GetComponent<GuildHallEntryPresenter>());
            Assert.NotNull(GameObject.Find("EmpireCanvas"));
        }

        private static void OpenAndCloseBuildingDetail(HomePagePresenter home, GameObject empireCanvas, string rowPath,
            EmpireBuildingKind expectedKind)
        {
            Click(empireCanvas, rowPath);
            GameObject detail = GameObject.Find(EmpireBuildingDetailPresenter.CanvasName);
            Assert.NotNull(detail, $"Expected building detail after tapping '{rowPath}'.");
            Assert.NotNull(GameObject.Find("EmpireCanvas"), "Empire canvas must stay under the popup.");
            var presenter = home.GetComponent<EmpireBuildingDetailPresenter>();
            Assert.NotNull(presenter);
            Assert.AreEqual(expectedKind, presenter.KindForTests);
            Click(detail, "DetailPanel/DetailHeader/Btn_Return");
            Assert.IsNull(GameObject.Find(EmpireBuildingDetailPresenter.CanvasName),
                $"Detail canvas must die after Return from {expectedKind}.");
            Assert.IsNull(home.GetComponent<EmpireBuildingDetailPresenter>());
            Assert.NotNull(GameObject.Find("EmpireCanvas"));
        }

        private static void Click(GameObject canvas, string path)
        {
            Button button = canvas.transform.Find(path)?.GetComponent<Button>();
            Assert.NotNull(button, $"Missing button '{path}' on '{canvas.name}'.");
            Assert.IsTrue(button.interactable, $"Button '{path}' must be interactable.");
            button.onClick.Invoke();
        }

        private static void AssertNoMetagameCanvases()
        {
            foreach (string canvasName in MetagameCanvasNames)
            {
                Assert.IsNull(GameObject.Find(canvasName),
                    $"Expected no leftover '{canvasName}' after navigation returned Home.");
            }
        }

        private static void PurgeOrphanMetagameCanvases()
        {
            foreach (string canvasName in MetagameCanvasNames)
            {
                GameObject stale;
                int guard = 0;
                while ((stale = GameObject.Find(canvasName)) != null)
                {
                    Object.DestroyImmediate(stale);
                    if (++guard > 32) break;
                }
            }
        }
    }
}
