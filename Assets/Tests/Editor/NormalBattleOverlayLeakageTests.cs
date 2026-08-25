using System.Collections.Generic;
using System.IO;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// NORMAL-BATTLE NARRATIVE-OVERLAY LEAKAGE, 2026-08-17 - proves a normal match is completely
    /// free-play: the older JSON-driven "how to play" narrative walkthrough
    /// (MaybeShowTutorial/_tutorialOverlay/BuildTutorialOverlay) can never be active during or
    /// survive into a normal battle, and ordinary card-first/lane-first Formation placement runs
    /// unrestricted through the real production handlers.
    ///
    /// MaybeShowTutorial itself never fires in EditMode (it early-returns under
    /// Application.isPlaying, which is always false here), so "the narrative overlay was left
    /// open when a normal match starts" - the actual reported bug (a player who never engaged the
    /// guided tutorial reaches a normal battle from Deck Builder's own "To Battle" action while
    /// SeenIntro is still false) - is reproduced directly via ForceNarrativeOverlayActiveForTests
    /// rather than by trying to route through MaybeShowTutorial's own Play-Mode-only gate.
    /// </summary>
    public class NormalBattleOverlayLeakageTests
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
            return bootstrap;
        }

        [Test]
        public void NormalMatch_AfterAValidSavedDeckExists_HasTheNarrativeOverlayInactive()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Leakage_ValidDeckBootstrap");

            // A valid, confirmed 10-card saved deck, exactly what Deck Builder's own Recommended
            // Deck / manual confirm would leave behind.
            var validDeck = new List<string>();
            foreach (Card card in CardDatabase.Instance.AllCards)
            {
                if (validDeck.Count >= 10) break;
                validDeck.Add(card.Id);
            }
            bootstrap.Profile.activeDeckCardIds = validDeck;

            // Reproduces the actual reported leak: a player who never engaged the guided tutorial
            // (SeenIntro still false) reaches a normal battle - MaybeShowTutorial would have left
            // the narrative open in a real Play Mode session.
            bootstrap.ForceNarrativeOverlayActiveForTests();
            Assert.IsTrue(bootstrap.NarrativeOverlayActiveForTests, "Setup: expected the narrative overlay to be forced open.");

            // "Clicking To Battle" - the real production path for starting/re-entering a normal
            // match on an existing instance.
            bootstrap.ResetLineupForTests();

            Assert.IsFalse(bootstrap.NarrativeOverlayActiveForTests,
                "The older JSON-driven narrative walkthrough must be closed the moment a normal match starts.");
        }

        [Test]
        public void NormalMatch_PlayAgain_AlsoClosesALeakedNarrativeOverlay()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Leakage_PlayAgainBootstrap");
            bootstrap.ForceNarrativeOverlayActiveForTests();

            bootstrap.PlayAgainForTests();

            Assert.IsFalse(bootstrap.NarrativeOverlayActiveForTests,
                "Play Again must also close a leaked narrative overlay, not just the initial Reset Lineup path.");
        }

        [Test]
        public void NormalMatch_HandCardTapAndLaneTap_DeployThroughTheRealProductionRoute_Unrestricted()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Leakage_FreeplayDeployBootstrap");
            bootstrap.ForceNarrativeOverlayActiveForTests();
            bootstrap.ResetLineupForTests();
            BattleController controller = bootstrap.Battle;

            Card anyCard = null;
            foreach (Card card in controller.PlayerState.Hand)
            {
                if (card.ResourceCost <= controller.PlayerState.Resource) { anyCard = card; break; }
            }
            Assert.IsNotNull(anyCard, "Setup: expected at least one affordable hand card.");

            bootstrap.HandCardPressedForTests(anyCard);
            Assert.AreEqual(anyCard.Id, bootstrap.SelectedCardIdForTests,
                "A normal match's hand-card tap must select the card through the real, unrestricted handler.");

            bootstrap.LanePressedForTests(Lane.Front);
            Assert.AreEqual(1, controller.PlayerState.Lanes[Lane.Front].Cards.Count,
                "A normal match's lane tap must place the card through the real, unrestricted handler.");
            Assert.AreEqual(anyCard.Id, controller.PlayerState.Lanes[Lane.Front].Cards[0].Definition.Id);
            Assert.IsNull(bootstrap.SelectedCardIdForTests, "Setup: placement should clear the selection, as it always does.");
        }
    }
}
