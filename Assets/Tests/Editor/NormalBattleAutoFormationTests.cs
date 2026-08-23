using System.Collections.Generic;
using System.IO;
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
    /// RELEASE FEATURE - turns the existing normal-battle Recommended control into AUTO
    /// FORMATION (same Button/GameObject, no new control - see BuildPrimaryActionAndSpells and
    /// OnAutoFormationPressed in GameBootstrap.cs). AutoFormationForTests() calls the real
    /// private OnAutoFormationPressed() handler directly, the same handler the relabeled button's
    /// onClick calls - not a reimplementation.
    /// </summary>
    public class NormalBattleAutoFormationTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsAutoFormation_" + System.Guid.NewGuid().ToString("N"));
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

        private CardDatabase SpawnDatabase()
        {
            var go = new GameObject("NormalBattleAutoFormation_CardDatabase");
            _spawned.Add(go);
            CardDatabase database = go.AddComponent<CardDatabase>();
            database.Initialize();
            return database;
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

        /// <summary>Confirms a valid full-size deck on a fresh profile before any bootstrap
        /// ever reads it. Uses the approved starter ten (same ids Chapter 1 playability uses)
        /// so Auto Formation's 1-slot-then-strongest policy always has three legal single-slot
        /// cards and the hand/deck subset assertions stay aligned with production onboarding.</summary>
        private List<string> SaveValidDeck(CardDatabase database, int deckSize)
        {
            string[] approvedStarter =
            {
                "warrior", "novice_knight", "goblin_caster",
                "cleric", "archer_elf", "fox", "bunny", "forest", "tribal_warrior", "undead_soldier",
            };
            List<string> deckIds = approvedStarter.Take(deckSize).ToList();
            if (deckIds.Count < deckSize)
            {
                deckIds.AddRange(database.AllCards.Select(c => c.Id)
                    .Where(id => !deckIds.Contains(id))
                    .Take(deckSize - deckIds.Count));
            }
            Assert.AreEqual(deckSize, deckIds.Count, "Setup: expected enough real cards to fill a full-size deck.");

            PlayerProfile profile = new PlayerProfile();
            profile.cardCollection = new List<string>(deckIds);
            profile.ApplyDataToEmpire();
            profile.activeDeckCardIds = new List<string>(deckIds);
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();

            return deckIds;
        }

        private static int DeckSlotCountForFreshProfile()
        {
            var probe = new PlayerProfile();
            probe.ApplyDataToEmpire();
            return probe.Empire.DeckSlotCount;
        }

        [Test]
        public void ValidDeck_AutoFormation_OccupiesExactlyThreeLanes_OneEach()
        {
            CardDatabase database = SpawnDatabase();
            int deckSize = DeckSlotCountForFreshProfile();
            SaveValidDeck(database, deckSize);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AutoFormation_ValidDeckBootstrap");

            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "A complete saved deck must not produce a blocked-start status.");
            Assert.IsTrue(bootstrap.RecommendedLineupButtonActiveForTests,
                "Auto Formation (the relabeled Recommended control) must be visible for a valid deck with an empty board.");
            Assert.IsTrue(bootstrap.TutorialGuidanceCaptionActiveForTests);
            Assert.AreEqual(
                GameBootstrap.NormalBattleModeLabel + "\nYour saved deck fills the hand. Tap Auto Formation to deploy a starting squad.",
                bootstrap.TutorialGuidanceCaptionTextForTests);

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Assert.AreEqual(0, bootstrap.Battle.PlayerState.Lanes[lane].Cards.Count,
                    $"Setup: expected an empty {lane} lane before Auto Formation runs.");
            }

            bootstrap.AutoFormationForTests();

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Assert.AreEqual(1, bootstrap.Battle.PlayerState.Lanes[lane].Cards.Count,
                    $"Auto Formation must place exactly one card in {lane}.");
            }

            int totalPlaced = System.Enum.GetValues(typeof(Lane)).Cast<Lane>()
                .Sum(lane => bootstrap.Battle.PlayerState.Lanes[lane].Cards.Count);
            Assert.AreEqual(3, totalPlaced, "Auto Formation must place exactly three cards total, not a full squad.");
        }

        [Test]
        public void AutoFormation_DeployedCardsOriginateInTheCurrentHandAndSavedDeck()
        {
            CardDatabase database = SpawnDatabase();
            int deckSize = DeckSlotCountForFreshProfile();
            List<string> confirmedDeck = SaveValidDeck(database, deckSize);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AutoFormation_DeckSourceBootstrap");

            HashSet<string> handBeforeIds = new HashSet<string>(bootstrap.Battle.PlayerState.Hand.Select(c => c.Id));
            Assert.IsTrue(handBeforeIds.Count > 0, "Setup: expected a non-empty starting hand from the confirmed deck.");
            CollectionAssert.IsSubsetOf(handBeforeIds, confirmedDeck, "Setup: the dealt hand must come from the confirmed saved deck.");

            bootstrap.AutoFormationForTests();

            List<string> placedIds = System.Enum.GetValues(typeof(Lane))
                .Cast<Lane>()
                .SelectMany(lane => bootstrap.Battle.PlayerState.Lanes[lane].Cards)
                .Select(instance => instance.Definition.Id)
                .ToList();

            Assert.AreEqual(3, placedIds.Count, "Setup: expected exactly three placed cards.");
            CollectionAssert.IsSubsetOf(placedIds, confirmedDeck,
                "Every card Auto Formation deploys must have come from the confirmed saved deck.");
            // Do NOT require placed ⊆ hand-at-start: TryPlayCard's on-play draw hooks
            // (Strategist / Perfect) can pull the next saved-deck card into Hand between the
            // first and third Auto Formation placement. Those draws are still the saved deck,
            // not an external source - asserting against the pre-AF hand snapshot is stale.

            // TryPlayCard removes a played card from Hand - consumed normally, not duplicated.
            HashSet<string> handAfterIds = new HashSet<string>(bootstrap.Battle.PlayerState.Hand.Select(c => c.Id));
            foreach (string placedId in placedIds)
            {
                Assert.IsFalse(handAfterIds.Contains(placedId),
                    $"Card {placedId} must have been consumed from the hand, not left behind after being placed.");
            }
        }

        [Test]
        public void AutoFormation_AppliesNormalLaneBonuses_AndEnablesStartBattle()
        {
            CardDatabase database = SpawnDatabase();
            int deckSize = DeckSlotCountForFreshProfile();
            SaveValidDeck(database, deckSize);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AutoFormation_LaneBonusBootstrap");
            bootstrap.AutoFormationForTests();

            BattleCardInstance frontCard = bootstrap.Battle.PlayerState.Lanes[Lane.Front].Cards.Single();
            BattleCardInstance middleCard = bootstrap.Battle.PlayerState.Lanes[Lane.Middle].Cards.Single();

            // Same bonuses BattleController.TryPlayCard already applies to every manual
            // placement (Front: +1 Attack; Middle: +1 max Health) - Auto Formation introduces no
            // separate placement/bonus logic of its own.
            Assert.AreEqual(frontCard.Definition.Attack + 1, frontCard.Attack,
                "A card Auto Formation places in Front must receive the normal +1 Attack lane bonus.");
            Assert.AreEqual(middleCard.Definition.Health + 1, middleCard.MaxHealth,
                "A card Auto Formation places in Middle must receive the normal +1 Health lane bonus.");

            Assert.AreEqual("START BATTLE", bootstrap.PrimaryActionLabelForTests);
            Assert.IsTrue(bootstrap.PrimaryActionButtonActiveForTests);
            Assert.IsTrue(bootstrap.PrimaryActionButtonInteractableForTests, "Start Battle must be enabled after Auto Formation.");
            Assert.IsTrue(bootstrap.TutorialGuidanceCaptionActiveForTests);
            Assert.AreEqual(
                GameBootstrap.NormalBattleModeLabel + "\nFormation ready. Tap Start Battle.",
                bootstrap.TutorialGuidanceCaptionTextForTests);

            bootstrap.StartBattleForTests();
            Assert.AreEqual(BattlePhase.Combat, bootstrap.Battle.Phase,
                "Start Battle must actually work once Auto Formation has filled the board.");
        }

        [Test]
        public void ManualPlacement_RemainsAvailable_AfterAutoFormation()
        {
            CardDatabase database = SpawnDatabase();
            int deckSize = DeckSlotCountForFreshProfile();
            SaveValidDeck(database, deckSize);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AutoFormation_ManualAfterBootstrap");
            bootstrap.AutoFormationForTests();

            // Must be affordable with whatever Resource Auto Formation left behind, or the real
            // SelectOrDeselectFormationHandCard handler correctly refuses the selection (an
            // unrelated, pre-existing affordability gate, not something this test should trip).
            Card remaining = bootstrap.Battle.PlayerState.Hand
                .FirstOrDefault(c => c.ResourceCost <= bootstrap.Battle.PlayerState.Resource);
            if (remaining == null)
            {
                Assert.Pass("Setup: no affordable card left in hand after Auto Formation - nothing further to manually select.");
                return;
            }

            // Every lane already holds one card; manual reinforcement/placement still routes
            // through the real handlers without being blocked by Auto Formation having run.
            bootstrap.HandCardPressedForTests(remaining);
            Assert.AreEqual(remaining.Id, bootstrap.SelectedCardIdForTests,
                "Manual card selection must still work normally after Auto Formation.");
        }

        [Test]
        public void TutorialSetup_IsUnchangedByAutoFormation()
        {
            CardDatabase database = SpawnDatabase();
            int deckSize = DeckSlotCountForFreshProfile();

            // A saved deck is present (and would offer Auto Formation for a normal match) - the
            // tutorial must ignore it entirely, exactly as it already ignores the saved deck for
            // its own hand (see NormalBattleSavedDeckIntegrationTests).
            SaveValidDeck(database, deckSize);

            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AutoFormation_TutorialBootstrap");
            bootstrap.StartApprovedTutorialBattle();

            Assert.IsFalse(bootstrap.RecommendedLineupButtonActiveForTests,
                "Auto Formation must never be offered during the tutorial.");

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Assert.AreEqual(0, bootstrap.Battle.PlayerState.Lanes[lane].Cards.Count,
                    $"The tutorial's fixed setup must leave {lane} empty until its own guided steps place a card.");
            }

            HashSet<string> tutorialHandIds = new HashSet<string>(bootstrap.Battle.PlayerState.Hand
                .Concat(bootstrap.Battle.PlayerState.DrawPile)
                .Select(card => card.Id));
            CollectionAssert.AreEquivalent(new[] { "warrior", "novice_knight", "goblin_caster" }, tutorialHandIds,
                "The tutorial's hand must remain its own fixed three-card setup.");

            // A direct call to the real handler must also no-op for a tutorial match - the
            // control being hidden is not the only gate; OnAutoFormationPressed re-checks itself.
            bootstrap.AutoFormationForTests();
            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Assert.AreEqual(0, bootstrap.Battle.PlayerState.Lanes[lane].Cards.Count,
                    $"A direct Auto Formation call must not place any card during the tutorial ({lane} must stay empty).");
            }
        }

        [Test]
        public void InvalidSavedDeck_BlocksAutoFormation_WithNoStaleHand()
        {
            // No SaveValidDeck call at all - a genuinely fresh profile has no activeDeckCardIds.
            SpawnDatabase();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AutoFormation_InvalidDeckBootstrap");

            Assert.IsNotNull(bootstrap.NormalMatchStatusForTests, "A missing saved deck must produce a blocked-start status.");
            Assert.AreEqual(0, bootstrap.Battle.PlayerState.Hand.Count + bootstrap.Battle.PlayerState.DrawPile.Count,
                "Battle must not deal any hand at all without a confirmed valid deck.");
            Assert.IsFalse(bootstrap.RecommendedLineupButtonActiveForTests,
                "Auto Formation must not be offered without a valid confirmed deck.");
            Assert.IsFalse(bootstrap.PrimaryActionButtonActiveForTests,
                "With no valid deck there is nothing to Start Battle with either.");

            // A direct call to the real handler must also refuse to place anything.
            bootstrap.AutoFormationForTests();
            Assert.AreEqual(0, bootstrap.Battle.PlayerState.Lanes.Values.Sum(l => l.Cards.Count),
                "A direct Auto Formation call must not place any card without a valid confirmed deck.");
        }
    }
}
