using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Approved Battle Result atlas (UI Beta pack, Battle_Result_Atlas_Runtime): the imported assets
    /// match the supplied slice contract (names, cell rectangles, pivots, PPU, alpha bounds, the
    /// identical reduced-motion atlas), and the icons are bound only to the existing result
    /// controls - decorative, raycast-free, inside their control, swapping immediately under Reduced
    /// Motion. Handlers, rewards, Stamina, Save and frozen contracts are not touched or asserted.
    /// </summary>
    public class BattleResultAtlasTests
    {
        private static readonly string[] Names = BattleResultIconSet.Cells.Select(c => c.Name).ToArray();

        private static string AssetsFolder => Path.Combine(Application.dataPath, "Resources", "UI", "Battle", "Interaction");

        // ---------- imported assets match the pack's slice contract ----------

        [Test]
        public void EveryIconSlice_ImportsAsA256Sprite_WithTheSuppliedRectPivotAndPpu_InBothMotionModes()
        {
            Assert.AreEqual(5, BattleResultIconSet.Cells.Length);
            foreach (BattleResultIconSet.Cell cell in BattleResultIconSet.Cells)
            {
                foreach (bool reduce in new[] { false, true })
                {
                    string path = BattleResultIconSet.SlicePath(cell.Name, reduce);
                    Sprite sprite = Resources.Load<Sprite>(path);
                    Assert.IsNotNull(sprite, $"{cell.Name} (reduceMotion={reduce}) must import at {path}.");
                    Assert.AreEqual(new Rect(0, 0, cell.Width, cell.Height), sprite.rect, $"{path} rect (a full 256x256 cell).");
                    Assert.AreEqual(new Vector2(128f, 128f), sprite.pivot, $"{path} pivot (0.5, 0.5).");
                    Assert.AreEqual(BattleResultIconSet.PixelsPerUnit, sprite.pixelsPerUnit, 0.0001f, $"{path} PPU.");
                    Assert.AreEqual(256, sprite.texture.width, $"{path} width.");
                    Assert.AreEqual(256, sprite.texture.height, $"{path} height.");
                }
            }
        }

        [Test]
        public void ReducedMotionSlices_HaveIdenticalGeometryToTheNormalSlices()
        {
            foreach (string name in Names)
            {
                Sprite a = Resources.Load<Sprite>(BattleResultIconSet.SlicePath(name, false));
                Sprite b = Resources.Load<Sprite>(BattleResultIconSet.SlicePath(name, true));
                Assert.IsNotNull(a, name);
                Assert.IsNotNull(b, name);
                Assert.AreEqual(a.rect, b.rect, $"{name} rect");
                Assert.AreEqual(a.pivot, b.pivot, $"{name} pivot");
                Assert.AreEqual(a.pixelsPerUnit, b.pixelsPerUnit, 0f, $"{name} PPU");
            }
        }

        /// <summary>Inclusive alpha bounds (top-left origin) of a cell inside a decoded PNG.</summary>
        private static (int minX, int minY, int maxX, int maxY) AlphaBounds(Texture2D texture, int cellX)
        {
            int minX = int.MaxValue, minY = int.MaxValue, maxX = -1, maxY = -1;
            for (int y = 0; y < BattleResultIconSet.CellSize; y++)
            {
                for (int x = 0; x < BattleResultIconSet.CellSize; x++)
                {
                    // Texture2D rows run bottom-up; the pack measures from the top.
                    Color32 pixel = texture.GetPixel(cellX + x, texture.height - 1 - y);
                    if (pixel.a == 0) continue;
                    if (x < minX) minX = x;
                    if (x > maxX) maxX = x;
                    if (y < minY) minY = y;
                    if (y > maxY) maxY = y;
                }
            }
            return (minX, minY, maxX, maxY);
        }

        private static Texture2D DecodePng(string fileName)
        {
            string path = Path.Combine(AssetsFolder, fileName);
            Assert.IsTrue(File.Exists(path), $"Expected the imported PNG {path}.");
            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            Assert.IsTrue(ImageConversion.LoadImage(texture, File.ReadAllBytes(path)), $"Decode {fileName}.");
            return texture;
        }

        [Test]
        public void SingleSlicePngs_ExistForEveryState_InBothMotionModes_WithTheSameAlphaBounds()
        {
            string[] states = { "Victory", "Defeat", "Retry", "Replay", "ResolvedConfirmed" };
            for (int i = 0; i < states.Length; i++)
            {
                BattleResultIconSet.Cell cell = BattleResultIconSet.Cells[i];
                foreach (string file in new[]
                {
                    $"BattleResult_Normal_{states[i]}_256x256.png",
                    $"ReducedMotion/BattleResult_ReducedMotion_Static_{states[i]}_256x256.png",
                })
                {
                    Texture2D texture = DecodePng(file);
                    Assert.AreEqual(256, texture.width, file);
                    Assert.AreEqual(256, texture.height, file);
                    Assert.AreEqual((cell.AlphaMinX, cell.AlphaMinY, cell.AlphaMaxX, cell.AlphaMaxY), AlphaBounds(texture, 0), $"{file} alpha bounds.");
                    Object.DestroyImmediate(texture);
                }
            }
        }

        // ---------- selection rules ----------

        [Test]
        public void StateMapping_VictoryReplay_DefeatRetry_ReturnIsResolvedConfirmed()
        {
            Assert.AreEqual(BattleResultIconSet.Victory, BattleResultIconSet.OutcomeBadgeName(true));
            Assert.AreEqual(BattleResultIconSet.Defeat, BattleResultIconSet.OutcomeBadgeName(false));
            Assert.AreEqual(BattleResultIconSet.Replay, BattleResultIconSet.ActionIconName(true));
            Assert.AreEqual(BattleResultIconSet.Retry, BattleResultIconSet.ActionIconName(false));
            Assert.AreEqual(BattleResultIconSet.ResolvedConfirmed, BattleResultIconSet.ReturnIconName);
        }

        [Test]
        public void SlicePaths_FollowTheSuppliedFileNaming()
        {
            Assert.AreEqual("UI/Battle/Interaction/BattleResult_Normal_Victory_256x256", BattleResultIconSet.SlicePath(BattleResultIconSet.Victory, false));
            Assert.AreEqual("UI/Battle/Interaction/ReducedMotion/BattleResult_ReducedMotion_Static_ResolvedConfirmed_256x256",
                BattleResultIconSet.SlicePath(BattleResultIconSet.ResolvedConfirmed, true));
        }

        [Test]
        public void Resolve_UsesTheMotionModesSlice_AndDegradesToTheOtherVariant_ThenToNull()
        {
            foreach (string name in Names)
            {
                Assert.AreEqual(Path.GetFileName(BattleResultIconSet.SlicePath(name, false)),
                    BattleResultIconSet.Resolve(name, false, p => Resources.Load<Sprite>(p)).name, $"{name} normal.");
                Assert.AreEqual(Path.GetFileName(BattleResultIconSet.SlicePath(name, true)),
                    BattleResultIconSet.Resolve(name, true, p => Resources.Load<Sprite>(p)).name, $"{name} reduced.");
            }

            // Reduced slice missing: the normal slice supplies the same icon.
            Sprite fallback = BattleResultIconSet.Resolve(BattleResultIconSet.Retry, true,
                p => p == BattleResultIconSet.SlicePath(BattleResultIconSet.Retry, true) ? null : Resources.Load<Sprite>(p));
            Assert.IsNotNull(fallback);
            Assert.AreEqual("BattleResult_Normal_Retry_256x256", fallback.name);

            Assert.IsNull(BattleResultIconSet.Resolve(BattleResultIconSet.Victory, false, _ => null), "Nothing available -> no icon (the labelled control still works).");
        }

        // ---------- runtime binding to the existing result controls ----------

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private bool _reduceMotionBefore;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDResultAtlas_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            _reduceMotionBefore = MotionPolicy.ReduceMotion;
        }

        [TearDown]
        public void TearDown()
        {
            MotionPolicy.ReduceMotion = _reduceMotionBefore;
            foreach (GameObject go in _spawned)
                if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            if (_scratchSaveDir != null && Directory.Exists(_scratchSaveDir))
                Directory.Delete(_scratchSaveDir, recursive: true);
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
            var databaseGo = new GameObject("ResultAtlas_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance;

            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;
            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize).ToList();
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: enough real cards for a full deck.");

            var profile = new PlayerProfile
            {
                cardCollection = new List<string>(deckIds),
                activeDeckCardIds = new List<string>(deckIds),
            };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();
            Object.DestroyImmediate(databaseGo);
        }

        private GameBootstrap ResolveNormalMatch(string host, bool playerWins)
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap(host);
            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();
            BattleController controller = bootstrap.Battle;
            Assert.AreEqual(BattlePhase.Combat, controller.Phase, "Setup: Combat before forcing an outcome.");
            if (playerWins) controller.EnemyState.AvatarHealth = 1;
            else controller.PlayerState.AvatarHealth = 1;
            int guard = 0;
            while (controller.Phase == BattlePhase.Combat && guard++ < BattleController.MaxCombatTicks)
                controller.AdvanceCombatTick();
            Assert.AreEqual(BattlePhase.Resolved, controller.Phase, "Setup: the forced outcome resolved.");
            return bootstrap;
        }

        [Test]
        public void Victory_BindsVictoryBadge_ReplayAndResolvedConfirmedIcons()
        {
            MotionPolicy.ReduceMotion = false;
            GameBootstrap bootstrap = ResolveNormalMatch("ResultAtlas_Victory", playerWins: true);

            Assert.AreEqual(BattleResultIconSet.Victory, bootstrap.ResultBadgeSpriteNameForTests);
            Assert.AreEqual(BattleResultIconSet.Replay, bootstrap.PlayAgainIconSpriteNameForTests);
            Assert.AreEqual(BattleResultIconSet.ResolvedConfirmed, bootstrap.ReturnIconSpriteNameForTests);
            Assert.AreEqual("BattleResult_Normal_Victory_256x256", bootstrap.ResultBadgeSourceAssetNameForTests);
        }

        [Test]
        public void Defeat_BindsDefeatBadge_RetryAndResolvedConfirmedIcons()
        {
            MotionPolicy.ReduceMotion = false;
            GameBootstrap bootstrap = ResolveNormalMatch("ResultAtlas_Defeat", playerWins: false);

            Assert.AreEqual(BattleResultIconSet.Defeat, bootstrap.ResultBadgeSpriteNameForTests);
            Assert.AreEqual(BattleResultIconSet.Retry, bootstrap.PlayAgainIconSpriteNameForTests);
            Assert.AreEqual(BattleResultIconSet.ResolvedConfirmed, bootstrap.ReturnIconSpriteNameForTests);
        }

        [Test]
        public void ReducedMotion_SwapsInTheReducedMotionSlice_Immediately_WithTheSameIcons()
        {
            MotionPolicy.ReduceMotion = true;
            GameBootstrap bootstrap = ResolveNormalMatch("ResultAtlas_Reduced", playerWins: true);

            Assert.AreEqual(BattleResultIconSet.Victory, bootstrap.ResultBadgeSpriteNameForTests, "Same semantic state.");
            Assert.AreEqual("BattleResult_ReducedMotion_Static_Victory_256x256", bootstrap.ResultBadgeSourceAssetNameForTests);
            Assert.AreEqual(1f, bootstrap.ResultOverlayAlphaForTests, 0.0001f, "No delayed reveal under Reduced Motion.");
        }

        [Test]
        public void ResultIcons_AreDecorative_RaycastFree_AndSitInsideTheirOwnControl_WithoutResizingIt()
        {
            MotionPolicy.ReduceMotion = false;
            GameBootstrap bootstrap = ResolveNormalMatch("ResultAtlas_Geometry", playerWins: true);
            Assert.IsTrue(bootstrap.ResultIconsAreRaycastFreeForTests, "The icons must never take input.");

            foreach ((RectTransform icon, RectTransform button, string what) in new[]
            {
                (bootstrap.PlayAgainIconRectForTests, bootstrap.PlayAgainButtonRectForTests, "play-again"),
                (bootstrap.ReturnIconRectForTests, bootstrap.ReturnButtonRectForTests, "return"),
            })
            {
                Assert.AreEqual(GameBootstrap.ResultActionIconSize, icon.rect.width, 0.01f, $"{what} icon width");
                Assert.GreaterOrEqual(icon.rect.width, 48f);
                Assert.LessOrEqual(icon.rect.width, 72f, "The pack asks for a 48-72 px icon.");
                Assert.LessOrEqual(icon.rect.height, button.rect.height + 0.01f, $"{what} icon must not be taller than its control.");

                var iconCorners = new Vector3[4];
                var buttonCorners = new Vector3[4];
                icon.GetWorldCorners(iconCorners);
                button.GetWorldCorners(buttonCorners);
                Assert.GreaterOrEqual(iconCorners[0].x, buttonCorners[0].x - 0.5f, $"{what} icon left inside its control");
                Assert.GreaterOrEqual(iconCorners[0].y, buttonCorners[0].y - 0.5f, $"{what} icon bottom inside its control");
                Assert.LessOrEqual(iconCorners[2].x, buttonCorners[2].x + 0.5f, $"{what} icon right inside its control");
                Assert.LessOrEqual(iconCorners[2].y, buttonCorners[2].y + 0.5f, $"{what} icon top inside its control");
            }
        }

        [Test]
        public void Retry_ClearsNothingItShouldnt_AndTheNextResultRebindsTheIconsForItsOwnOutcome()
        {
            MotionPolicy.ReduceMotion = false;
            GameBootstrap bootstrap = ResolveNormalMatch("ResultAtlas_Rebind", playerWins: false);
            Assert.AreEqual(BattleResultIconSet.Retry, bootstrap.PlayAgainIconSpriteNameForTests, "Setup: defeat bound Retry.");

            LogAssert.ignoreFailingMessages = true; // edit-mode Destroy() log from unrelated shared cleanup
            try
            {
                bootstrap.RetryForTests();
                Assert.IsFalse(bootstrap.ResultOverlayActiveForTests, "Retry hides the result overlay as before.");

                BattleController controller = bootstrap.Battle;
                bootstrap.AutoFormationForTests();
                bootstrap.StartBattleForTests();
                controller.EnemyState.AvatarHealth = 1;
                int guard = 0;
                while (controller.Phase == BattlePhase.Combat && guard++ < BattleController.MaxCombatTicks)
                    controller.AdvanceCombatTick();

                Assert.AreEqual(BattleResultIconSet.Victory, bootstrap.ResultBadgeSpriteNameForTests, "The second result rebinds for its own outcome.");
                Assert.AreEqual(BattleResultIconSet.Replay, bootstrap.PlayAgainIconSpriteNameForTests);
            }
            finally
            {
                LogAssert.ignoreFailingMessages = false;
            }
        }
    }
}
