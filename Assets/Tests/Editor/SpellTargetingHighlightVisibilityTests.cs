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
    /// CR-BATTLE-PRESENTATION-VISUAL-PASS-002, 2026-09-17 - the marketing reference video
    /// (outreach-assets/current-gameplay/myriad-of-dragons-battle-scene-2026-09-12.mp4) shows a
    /// vivid glowing border around a targeted card/lane; the live Battle UI's own targeting
    /// highlight was only a faint 0.28-0.32 alpha background wash with no border at all
    /// (RefreshLaneButtons/ArmSpellTargeting). This proves the fix - a real Outline component,
    /// toggled alongside the same background tint, plus the raised alpha - actually shows and
    /// clears at the right moments, through the real input path (SpellTappedForTests), not a
    /// direct field write. Presentation only: does not touch which lane is a legal target,
    /// Energy, cooldowns, or SpellTargetsFriendlyLane's own targeting-legality rule.
    /// </summary>
    public class SpellTargetingHighlightVisibilityTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDTargetHighlight_" + System.Guid.NewGuid().ToString("N"));
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

        private static void OverrideWithSurvivableMatchAndConfirm(BattleController controller)
        {
            var data = new CardData
            {
                id = "target_highlight_card", name = "Target Highlight Card", art_file = "x.png",
                element = "Andras", type = "warrior", rarity = 1,
            };
            Card card = Card.FromData(data);
            var economy = new BattleController.MatchEconomy(resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 100000);
            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy);
            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);

            Card handCard = controller.PlayerState.Hand.First();
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, handCard, Lane.Front), "Setup: expected a legal placement.");
            Assert.IsTrue(controller.ConfirmFormation(), "Setup: expected the Formation to lock legally.");
        }

        [Test]
        public void EveryLaneButton_HasAnOutlineComponent_DisabledByDefault()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("TargetHighlight_DefaultOffBootstrap");
            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Assert.IsFalse(bootstrap.PlayerLaneHighlightOutlineEnabledForTests(lane),
                    $"Player {lane}'s highlight outline must be off before anything is armed/selected.");
                Assert.IsFalse(bootstrap.EnemyLaneHighlightOutlineEnabledForTests(lane),
                    $"Enemy {lane}'s highlight outline must be off before anything is armed/selected.");
            }
        }

        [Test]
        public void ArmingAnEnemyTargetedSpell_ShowsTheOutlineOnEveryEnemyLane_AndNotPlayerLanes()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("TargetHighlight_EnemyArmedBootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);
            controller.SetEnergyForTutorial(60);

            bootstrap.SpellTappedForTests(0); // Firestorm (LaneDamage) - enemy-targeted.

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Assert.IsTrue(bootstrap.EnemyLaneHighlightOutlineEnabledForTests(lane),
                    $"Enemy {lane}'s highlight outline must show while an enemy-targeted spell is armed.");
                Assert.IsFalse(bootstrap.PlayerLaneHighlightOutlineEnabledForTests(lane),
                    $"Player {lane} must not glow for an enemy-targeted spell.");
            }
        }

        [Test]
        public void ArmingAFriendlyTargetedSpell_ShowsTheOutlineOnEveryPlayerLane_AndNotEnemyLanes()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("TargetHighlight_FriendlyArmedBootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);
            controller.SetEnergyForTutorial(60);

            bootstrap.SpellTappedForTests(1); // Mend (LaneHeal) - friendly-targeted.

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Assert.IsTrue(bootstrap.PlayerLaneHighlightOutlineEnabledForTests(lane),
                    $"Player {lane}'s highlight outline must show while a friendly-targeted spell is armed.");
                Assert.IsFalse(bootstrap.EnemyLaneHighlightOutlineEnabledForTests(lane),
                    $"Enemy {lane} must not glow for a friendly-targeted spell.");
            }
        }

        [Test]
        public void CastingTheArmedSpell_ClearsTheOutlineAfterwards()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("TargetHighlight_CastClearsBootstrap");
            BattleController controller = bootstrap.Battle;
            OverrideWithSurvivableMatchAndConfirm(controller);
            controller.SetEnergyForTutorial(60);

            bootstrap.SpellTappedForTests(0); // Firestorm - arms enemy targeting.
            Assert.IsTrue(bootstrap.EnemyLaneHighlightOutlineEnabledForTests(Lane.Front), "Setup: expected the outline to be showing before the cast.");

            bootstrap.SpellTargetLanePressedForTests(Lane.Front);

            Assert.AreEqual(1, controller.SpellCastLog.Count, "Setup: expected the cast to actually succeed.");
            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Assert.IsFalse(bootstrap.EnemyLaneHighlightOutlineEnabledForTests(lane),
                    $"Enemy {lane}'s outline must clear once the cast that armed it has resolved.");
            }
        }
    }
}
