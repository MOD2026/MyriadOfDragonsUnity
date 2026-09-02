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
    /// IMPL-BATTLE-RESULT-BINDING-001. Proves BuildResultOverlay's ResultPanel actually loads the
    /// approved Revamp V2 asset (docs/REVAMP_V2_APPROVAL_REGISTRY.md row `13_battle_result.png`,
    /// APPROVED_PRODUCTION) from UI/RevampV2Approved/BattleResult/battle_result_v2, rather than
    /// just asserting the overlay appears (ResultOverlayOutcomeCoverageTests already covers CTA/
    /// outcome behavior and is untouched here). Same StartApprovedTutorialBattle/AutoFormation-
    /// ForTests setup pattern already established in that file.
    /// </summary>
    public class BattleResultArtBindingTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDBattleResultArt_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, true);
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
            return bootstrap;
        }

        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("BattleResultArt_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance;

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;

            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize)
                .ToList();
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: expected enough real cards to fill a full-size deck.");

            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(deckIds),
                activeDeckCardIds = new List<string>(deckIds),
            };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();

            Object.DestroyImmediate(databaseGo);
        }

        private static void ResolveCombat(Battle.BattleController controller)
        {
            int ticksRun = 0;
            while (controller.Phase == Battle.BattlePhase.Combat)
            {
                controller.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, Battle.BattleController.MaxCombatTicks,
                    "STATE UNREACHED: the forced outcome did not resolve within the existing combat tick cap.");
            }
            Assert.AreEqual(Battle.BattlePhase.Resolved, controller.Phase, "STATE UNREACHED: match did not reach Resolved.");
        }

        private GameBootstrap ResolveNormalMatch(string hostName, bool playerWins)
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap(hostName);
            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();
            Battle.BattleController controller = bootstrap.Battle;
            Assert.AreEqual(Battle.BattlePhase.Combat, controller.Phase, "Setup: expected Combat before forcing an outcome.");

            if (playerWins) controller.EnemyState.AvatarHealth = 1;
            else controller.PlayerState.AvatarHealth = 1;

            ResolveCombat(controller);
            return bootstrap;
        }

        [Test]
        public void ResultOverlay_BindsTheApprovedRevampV2Asset_NotTheProceduralFallback()
        {
            GameBootstrap bootstrap = ResolveNormalMatch("ResultArt_Bound", playerWins: true);
            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests, "Setup: overlay must be showing to check its bound art.");

            Sprite bound = bootstrap.ResultPanelSpriteForTests;
            Sprite approved = Resources.Load<Sprite>("UI/RevampV2Approved/BattleResult/battle_result_v2");

            Assert.IsNotNull(approved,
                "Setup: the approved asset must exist on the Resources path this test (and BuildResultOverlay) load from.");
            Assert.AreSame(approved, bound,
                "ResultPanel must display the approved battle_result_v2 sprite, not the procedural Popup_Frame fallback.");
        }

        [Test]
        public void ResultOverlay_ApprovedAsset_IsUnslicedSimpleImage_NotTiledAsABorder()
        {
            // battle_result_v2 is a full illustrated scene (registry: 1672x941), not a 9-slice
            // border like Popup_Frame - it must render as a single stretched image, not repeat/
            // tile at its border regions the way CreateRoundedPanel's default art does.
            GameBootstrap bootstrap = ResolveNormalMatch("ResultArt_Unsliced", playerWins: false);

            GameObject canvasObj = GameObject.Find("Canvas");
            Assert.IsNotNull(canvasObj, "Setup: the battle canvas must exist.");
            Transform panelTransform = canvasObj.GetComponentsInChildren<Transform>(true)
                .FirstOrDefault(t => t.name == "ResultPanel");
            Assert.IsNotNull(panelTransform, "STATE UNREACHED: could not locate ResultPanel in the real hierarchy.");

            Image panelImage = panelTransform.GetComponent<Image>();
            Assert.IsNotNull(panelImage);
            Assert.AreEqual(Image.Type.Simple, panelImage.type,
                "The approved result art is a full scene, not a tileable border - it must use Image.Type.Simple.");
            Assert.AreEqual(Color.white, panelImage.color,
                "The approved art must render at full white tint, matching every other RevampV2Approved binding.");
        }

        [Test]
        public void ResultOverlay_BoundArt_IsIdenticalForVictoryAndDefeat()
        {
            // The registry approves one battle_result_v2 asset for the whole result surface, not a
            // separate victory/defeat pair - both outcomes must bind the exact same sprite.
            GameBootstrap victory = ResolveNormalMatch("ResultArt_Victory", playerWins: true);
            GameBootstrap defeat = ResolveNormalMatch("ResultArt_Defeat", playerWins: false);

            Assert.IsNotNull(victory.ResultPanelSpriteForTests);
            Assert.AreSame(victory.ResultPanelSpriteForTests, defeat.ResultPanelSpriteForTests);
        }
    }
}
