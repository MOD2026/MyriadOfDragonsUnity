using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Battle Screen asset audit (docs/Battle_Screen_Landscape_Asset_Audit_2026-08-18.md) §3's
    /// still-open item: the card-illustration inset was a guessed flat percentage, not "verified
    /// visually ... per frame type" as the audit itself required. No Play Mode/GUI access exists
    /// to eyeball it, so this instead uses each rarity frame's own Texture Importer spriteBorder -
    /// a real, already-authored per-frame measurement, not a new guess. All four rarities land on
    /// an identical 20% fraction once read that way (verified directly against the four .meta
    /// files: Common/Rare/Epic 68/340 and 92/460, Legendary 80/400 and 92/460 - all exactly 0.20),
    /// so the fix is one shared inset, not four different ones - and it replaces two DIFFERENT
    /// unverified guesses that were in production (hand cards 10%/10-90/95%, board tiles
    /// 12%/12-88/88%), neither of which matched the frame's real art window.
    /// </summary>
    public class RarityFrameRenderingTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "MyriadOfDragonsRarityFrame_" + System.Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(_scratchSaveDir);
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
            if (_scratchSaveDir != null && System.IO.Directory.Exists(_scratchSaveDir))
            {
                System.IO.Directory.Delete(_scratchSaveDir, recursive: true);
            }
        }

        // ---------- Plain data: frame aspect (unchanged, already-correct behaviour) ----------

        [TestCase(1)]
        [TestCase(2)]
        public void GetRarityFrameAspect_CommonTier_MatchesTheRealFrameFileAspect(int rarity)
        {
            Assert.AreEqual(340f / 460f, GameBootstrap.GetRarityFrameAspectForTests(rarity), 0.0001f);
        }

        [TestCase(5)]
        [TestCase(6)]
        public void GetRarityFrameAspect_EpicTier_SharesTheSameFrameFileAsCommonRare(int rarity)
        {
            // Common/Rare/Epic are all the same 340x460 source file per the asset audit.
            Assert.AreEqual(340f / 460f, GameBootstrap.GetRarityFrameAspectForTests(rarity), 0.0001f);
        }

        [Test]
        public void GetRarityFrameAspect_Legendary_MatchesItsOwnWiderFrameFile()
        {
            Assert.AreEqual(400f / 460f, GameBootstrap.GetRarityFrameAspectForTests(7), 0.0001f);
        }

        // ---------- Plain data: the real, spriteBorder-derived art inset ----------

        [Test]
        public void RarityFrameArtInset_MatchesTheRealSpriteBorderProportionForEveryFrame()
        {
            (Vector2 min, Vector2 max) inset = GameBootstrap.RarityFrameArtInsetForTests;

            // Common/Rare/Epic_Card_Frame.png.meta: spriteBorder {x:68,y:92,z:68,w:92} on 340x460.
            Assert.AreEqual(68f / 340f, inset.min.x, 0.001f, "Left inset must match the real Common/Rare/Epic spriteBorder fraction.");
            Assert.AreEqual(92f / 460f, inset.min.y, 0.001f, "Bottom inset must match the real Common/Rare/Epic spriteBorder fraction.");
            Assert.AreEqual(1f - 68f / 340f, inset.max.x, 0.001f, "Right inset must match the real Common/Rare/Epic spriteBorder fraction.");
            Assert.AreEqual(1f - 92f / 460f, inset.max.y, 0.001f, "Top inset must match the real Common/Rare/Epic spriteBorder fraction.");

            // Legendary_Card_Frame.png.meta: spriteBorder {x:80,y:92,z:80,w:92} on 400x460 - same
            // proportion as Common/Rare/Epic despite the different absolute pixels/canvas size.
            Assert.AreEqual(80f / 400f, inset.min.x, 0.001f, "Legendary's border must land on the same fraction despite its wider canvas.");
            Assert.AreEqual(inset.min.x, inset.min.y, 0.02f, "The real per-frame data turns out symmetric (20% every side) - not four different insets.");
        }

        [Test]
        public void RarityFrameArtInset_IsNotTheOldGuessedValues()
        {
            (Vector2 min, Vector2 max) inset = GameBootstrap.RarityFrameArtInsetForTests;

            // The two previously-shipped, unverified guesses this replaces.
            Assert.AreNotEqual(new Vector2(0.10f, 0.10f), inset.min, "Must not still be the old unverified hand-card guess.");
            Assert.AreNotEqual(new Vector2(0.12f, 0.12f), inset.min, "Must not still be the old unverified board-tile guess.");
        }

        // ---------- Real rendering: both card-tile call sites actually use the shared inset ----------

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

        private static RectTransform FindArtFit(Transform cardRoot)
        {
            Transform artFit = cardRoot.Find("ArtFit");
            Assert.NotNull(artFit, $"Expected an ArtFit child under '{cardRoot.name}'.");
            return (RectTransform)artFit;
        }

        [Test]
        public void RealHandCard_ArtFitAnchors_MatchTheSharedRarityFrameInset()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("RarityFrame_HandCard_Bootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();

            Card handCard = bootstrap.Battle.PlayerState.Hand.First();
            GameObject handRow = GameObject.Find("HandRow");
            Assert.NotNull(handRow, "Setup: expected a real HandRow in the scene during Formation.");
            Transform cardRoot = handRow.transform.Find($"Card_{handCard.Id}");
            Assert.NotNull(cardRoot, $"Setup: expected a real hand card GameObject for '{handCard.Id}'.");

            RectTransform artFit = FindArtFit(cardRoot);
            (Vector2 min, Vector2 max) expected = GameBootstrap.RarityFrameArtInsetForTests;
            Assert.AreEqual(expected.min, artFit.anchorMin, "Real hand card must use the shared, evidence-based inset, not the old 10% guess.");
            Assert.AreEqual(expected.max, artFit.anchorMax);
        }

        [Test]
        public void RealBoardCard_ArtFitAnchors_MatchTheSameSharedInsetAsTheHandCard()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("RarityFrame_BoardCard_Bootstrap");
            bootstrap.StartApprovedTutorialBattle();
            bootstrap.SkipCinematicForTests();

            Card warrior = bootstrap.Battle.PlayerState.Hand.First(c => c.Id == "warrior");
            bootstrap.HandCardPressedForTests(warrior);
            bootstrap.LanePressedForTests(Lane.Front);

            GameObject frontLaneCard = GameObject.Find($"Mini_{warrior.Id}");
            Assert.NotNull(frontLaneCard, "Setup: expected a real deployed board-card GameObject after placement.");

            // CreateMiniCardDisplay's hierarchy is Mini_{id} (cell) -> Tile (CreateBoardCardTile) -> ArtFit.
            Transform tile = frontLaneCard.transform.Find("Tile");
            Assert.NotNull(tile, "Setup: expected the board card's own Tile child (CreateBoardCardTile).");
            RectTransform artFit = FindArtFit(tile);
            (Vector2 min, Vector2 max) expected = GameBootstrap.RarityFrameArtInsetForTests;
            Assert.AreEqual(expected.min, artFit.anchorMin, "Real board card must use the same shared inset as the hand card, not the old 12% guess.");
            Assert.AreEqual(expected.max, artFit.anchorMax);
        }
    }
}
