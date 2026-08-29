using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// CR-SELECTEDCARD-HIT-TEST-005. CR-BETA-COVERAGE-MATRIX-AUDIT-004 identified SelectedCardBox
    /// (GameBootstrap.cs:3866, the "Selected Card / Place In" collapsible status box in the hand
    /// dock) as having no dedicated test anywhere, despite being a real tappable control with its
    /// own Button/Image wiring. Uses only existing public seams - HandDockRectForTests,
    /// HandCardPressedForTests, TryExpandSelectedCardForTests, CollapseSelectedCardForTests,
    /// SelectedCardExpandedForTests, SelectedCardExpandOverlayActiveForTests - no reflection, no
    /// GameBootstrap.cs change.
    /// </summary>
    public class SelectedCardBoxHitAreaCoverageTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDSelectedCardBox_" + System.Guid.NewGuid().ToString("N"));
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

        /// <summary>Same pattern as GameBootstrapStateCoverageTests.SaveValidDeckForNormalMatch.</summary>
        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("SelectedCardBox_CardDatabase");
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

        private static Button SelectedCardBoxButton(GameBootstrap bootstrap)
        {
            RectTransform handDock = bootstrap.HandDockRectForTests;
            Assert.IsNotNull(handDock, "Setup: the hand dock must exist to locate SelectedCardBox.");
            Transform box = handDock.Find("SelectedCardBox");
            Assert.IsNotNull(box, "STATE UNREACHED: SelectedCardBox was not found under the hand dock.");
            Button button = box.GetComponent<Button>();
            Assert.IsNotNull(button, "STATE UNREACHED: SelectedCardBox has no Button component.");
            return button;
        }

        [Test]
        public void SelectedCardBox_ImageIsRaycastable_AndIsTheButtonsTargetGraphic()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SelectedCardBox_Raycast");

            Button button = SelectedCardBoxButton(bootstrap);
            Image image = button.GetComponent<Image>();

            Assert.IsNotNull(image, "SelectedCardBox must carry the Image its Button targets.");
            Assert.IsTrue(image.raycastTarget, "SelectedCardBox's Image must be raycastable, or the box is visible but untappable.");
            Assert.AreSame(image, button.targetGraphic, "SelectedCardBox's Button must target its own Image, not a missing/wrong graphic.");
        }

        [Test]
        public void SelectedCardBox_HasANonZeroValidHitRectangle()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SelectedCardBox_HitRect");

            Button button = SelectedCardBoxButton(bootstrap);
            var rect = (RectTransform)button.transform;

            Assert.Greater(rect.rect.width, 0f, "SelectedCardBox hit area must have nonzero width.");
            Assert.Greater(rect.rect.height, 0f, "SelectedCardBox hit area must have nonzero height.");
            Assert.IsTrue(button.interactable, "SelectedCardBox must be interactable to receive a real tap.");
        }

        [Test]
        public void SelectedCardBox_RealButtonClick_ProducesTheSameOutcomeAsTheDirectExpansionSeam()
        {
            // TryExpandSelectedCard's own doc comment: real board headroom may legitimately block
            // expansion today, so this does not assert expansion always succeeds - only that the
            // real Button.onClick wiring reaches the exact same toggle logic
            // TryExpandSelectedCardForTests/CollapseSelectedCardForTests already exercise directly,
            // by comparing two identically-set-up bootstraps rather than hardcoding true or false.
            SaveValidDeckForNormalMatch();
            GameBootstrap viaButton = SpawnAndInitializeBootstrap("SelectedCardBox_ViaButton");
            Card cardForButton = viaButton.Battle.PlayerState.Hand[0];
            viaButton.HandCardPressedForTests(cardForButton);
            Assert.IsFalse(viaButton.SelectedCardExpandedForTests, "Setup: must start collapsed.");

            Button button = SelectedCardBoxButton(viaButton);
            button.onClick.Invoke();

            SaveValidDeckForNormalMatch();
            GameBootstrap viaSeam = SpawnAndInitializeBootstrap("SelectedCardBox_ViaSeam");
            Card cardForSeam = viaSeam.Battle.PlayerState.Hand[0];
            viaSeam.HandCardPressedForTests(cardForSeam);
            Assert.IsFalse(viaSeam.SelectedCardExpandedForTests, "Setup: must start collapsed.");
            viaSeam.TryExpandSelectedCardForTests();

            Assert.AreEqual(viaSeam.SelectedCardExpandedForTests, viaButton.SelectedCardExpandedForTests,
                "A real tap on SelectedCardBox must produce the same expanded-state outcome as calling the " +
                "expansion seam directly - proves Button.onClick is genuinely wired to the real callback.");
            Assert.AreEqual(viaSeam.SelectedCardExpandOverlayActiveForTests, viaButton.SelectedCardExpandOverlayActiveForTests);
        }

        [Test]
        public void SelectedCardBox_Tapping_DoesNotMutateCardPlacement()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("SelectedCardBox_NoMutation");
            BattleController controller = bootstrap.Battle;

            Card card = controller.PlayerState.Hand[0];
            bootstrap.HandCardPressedForTests(card);
            int handCountBefore = controller.PlayerState.Hand.Count;
            var laneCountsBefore = System.Enum.GetValues(typeof(Lane)).Cast<Lane>()
                .ToDictionary(lane => lane, lane => controller.PlayerState.Lanes[lane].Cards.Count);

            Button button = SelectedCardBoxButton(bootstrap);
            button.onClick.Invoke(); // toggle (expand or blocked, either way)
            button.onClick.Invoke(); // toggle back

            Assert.AreEqual(handCountBefore, controller.PlayerState.Hand.Count,
                "Tapping SelectedCardBox must never remove a card from the hand - it is not a placement control.");
            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Assert.AreEqual(laneCountsBefore[lane], controller.PlayerState.Lanes[lane].Cards.Count,
                    $"Tapping SelectedCardBox must never place a card into {lane} - it only toggles the status display.");
            }
        }
    }
}
