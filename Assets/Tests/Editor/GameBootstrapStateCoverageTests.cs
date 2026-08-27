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
    /// COVERAGE, not correctness (CC, 2026-08-27, first cut - proves the mechanism on ONE screen
    /// before any manifest is proposed for the other ~24). `UiValidationRunTests` answers "is
    /// what I measured real?" (RunCalibrationCheck). This answers a DIFFERENT question: "did I
    /// look at anything worth looking at?" A gate that only ever builds a screen in its default
    /// state and reports zero findings is not proof the screen is fine - it may just mean the
    /// gate never reached the state that was broken. Tonight's Shop investigation showed exactly
    /// how easy that is to miss without a real driven test.
    ///
    /// DECLARED STATES for GameBootstrap (CC's Q1 answer: a distinct branch of the builder that
    /// produces different visual output AND is reachable in normal play - not every conditional):
    ///   1. Formation, deck blocked (no saved deck at all)
    ///   2. Formation, valid deck, empty board (nothing placed yet)
    ///   3. Formation, ready (Auto Formation run, all lanes occupied)
    ///   4. Combat
    ///   5. Resolved
    ///
    /// Each state below is DRIVEN, not assumed - a real GameBootstrap instance is pushed through
    /// production code paths (StartApprovedTutorialBattle-style helpers, AutoFormationForTests,
    /// StartBattleForTests, AdvanceCombatTick) exactly as a player's actions would, and a REAL,
    /// player-visible signal is asserted to confirm the state was actually entered - not that a
    /// method was called, but that its effect exists (same discipline CLAUDE.md already requires
    /// for BattleController: "assert relationships... real logic in plain testable methods").
    ///
    /// An unreached declared state FAILS THE TEST, not a warning - unlike UiValidationRunTests'
    /// own contrast tier (still deliberately WARN-only pending real remediation capacity),
    /// coverage has no such gray area: either the harness looked at the state or it did not, and
    /// pretending otherwise is exactly the false-green failure mode this file exists to catch.
    /// </summary>
    public class GameBootstrapStateCoverageTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MyriadOfDragonsCoverage_" + System.Guid.NewGuid().ToString("N"));
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

        /// <summary>Same pattern as every other Battle-adjacent test file in this suite
        /// (TutorialGuidanceTests.SaveValidDeckForNormalMatch, etc.) - a real, production-shaped
        /// saved deck, not a hand-built fixture.</summary>
        private static void SaveValidDeckForNormalMatch()
        {
            var databaseGo = new GameObject("Coverage_CardDatabase");
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

        [Test]
        public void State1_Formation_DeckBlocked_IsReachedAndVisible()
        {
            // No SaveValidDeckForNormalMatch call - a genuinely fresh profile has no saved deck.
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Coverage_DeckBlocked");

            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase, "Setup: expected Formation.");
            Assert.IsNotNull(bootstrap.NormalMatchStatusForTests, "Setup: expected a blocked-start status with no saved deck.");
            // The REAL player-visible effect (BuildDeckBlockedOverlay's own surface), not just
            // that the underlying flag is set - matches this project's own "assert the rendered
            // effect, not that a method fired" rule.
            Assert.IsTrue(bootstrap.DeckBlockedOverlayActiveForTests,
                "STATE UNREACHED: Formation/deck-blocked did not produce a visible DeckBlockedOverlay.");
        }

        [Test]
        public void State2_Formation_ValidDeckEmptyBoard_IsReachedAndVisible()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Coverage_EmptyBoard");

            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase, "Setup: expected Formation.");
            Assert.IsNull(bootstrap.NormalMatchStatusForTests, "Setup: expected a valid, unblocked deck.");
            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Assert.AreEqual(0, bootstrap.Battle.PlayerState.Lanes[lane].Cards.Count,
                    "Setup: expected an empty board before any placement.");
            }
            Assert.IsTrue(bootstrap.RecommendedLineupButtonActiveForTests,
                "STATE UNREACHED: Formation/valid-deck/empty-board did not offer Auto Formation.");
        }

        [Test]
        public void State3_Formation_Ready_IsReachedAndVisible()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Coverage_Ready");

            bootstrap.AutoFormationForTests();

            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                Assert.AreEqual(1, bootstrap.Battle.PlayerState.Lanes[lane].Cards.Count,
                    "STATE UNREACHED: Auto Formation did not occupy every lane.");
            }
            Assert.IsTrue(bootstrap.PrimaryActionButtonActiveForTests,
                "STATE UNREACHED: Formation/ready did not offer Start Battle.");
        }

        [Test]
        public void State4_Combat_IsReachedAndVisible()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Coverage_Combat");

            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();

            Assert.AreEqual(BattlePhase.Combat, bootstrap.Battle.Phase,
                "STATE UNREACHED: Start Battle did not move Formation to Combat.");
            Assert.AreEqual(GameBootstrap.NormalBattleModeLabel, bootstrap.TutorialGuidanceCaptionTextForTests,
                "STATE UNREACHED: Combat's own mode caption did not render.");
        }

        [Test]
        public void State5_Resolved_IsReachedAndVisible()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Coverage_Resolved");

            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();
            Assert.AreEqual(BattlePhase.Combat, bootstrap.Battle.Phase, "Setup: expected Combat before driving to resolution.");

            int ticksRun = 0;
            while (bootstrap.Battle.Phase == BattlePhase.Combat)
            {
                bootstrap.Battle.AdvanceCombatTick();
                ticksRun++;
                Assert.LessOrEqual(ticksRun, BattleController.MaxCombatTicks,
                    "STATE UNREACHED: Combat never resolved within the tick cap.");
            }

            Assert.AreEqual(BattlePhase.Resolved, bootstrap.Battle.Phase, "STATE UNREACHED: match did not reach Resolved.");
            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests,
                "STATE UNREACHED: Resolved did not produce a visible result overlay.");
            Assert.IsFalse(string.IsNullOrEmpty(bootstrap.ResultTextForTests),
                "STATE UNREACHED: Resolved's result overlay carried no text.");
        }

        // ---------- Enemy HUD (owner instruction, 2026-08-27: "animation, not text") ----------
        // Four more declared states, same discipline as the five above - a real GameBootstrap
        // instance, real battle state (AvatarHealth is a public field on the real
        // PlayerBattleState, set directly here rather than mocked), and the REAL RefreshAll path
        // driving the HUD, then asserting what actually rendered (EnemyHealthSegmentsFilledForTests
        // counts segments by their ACTUAL colour, not by recomputing the fill formula).

        [Test]
        public void EnemyHud_FullHealth_AllSegmentsFilledAndCrestPresent()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Coverage_HudFull");

            Assert.AreEqual(bootstrap.Battle.EnemyState.MaxAvatarHealth, bootstrap.Battle.EnemyState.AvatarHealth,
                "Setup: expected the enemy to start at full health.");
            Assert.AreEqual(20, bootstrap.EnemyHealthSegmentCountForTests, "STATE UNREACHED: the 20-segment bar was not built.");
            Assert.AreEqual(20, bootstrap.EnemyHealthSegmentsFilledForTests,
                "STATE UNREACHED: full health did not render as all 20 segments filled.");
            Assert.IsNotNull(bootstrap.EnemyCrestSpriteForTests,
                "STATE UNREACHED: no crest sprite loaded for this encounter's difficulty tier - " +
                "check Resources/UI/EnemyCrestsV1 against AiArchetypeForTests' real AIDifficultyTier.");
        }

        [Test]
        public void EnemyHud_PartialHealth_SegmentsMatchRealFraction()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Coverage_HudPartial");

            int maxHp = bootstrap.Battle.EnemyState.MaxAvatarHealth;
            bootstrap.Battle.EnemyState.AvatarHealth = Mathf.Max(1, maxHp / 3); // ~33%, mid-bar
            bootstrap.RefreshAllForTests();

            int expected = GameBootstrap.ComputeFilledHealthSegments(
                bootstrap.Battle.EnemyState.AvatarHealth, maxHp, bootstrap.EnemyHealthSegmentCountForTests);
            Assert.AreEqual(expected, bootstrap.EnemyHealthSegmentsFilledForTests,
                "STATE UNREACHED: partial health did not render the segment count the real formula predicts.");
            Assert.Greater(bootstrap.EnemyHealthSegmentsFilledForTests, 0,
                "STATE UNREACHED: partial (non-lethal) health rendered as fully empty.");
            Assert.Less(bootstrap.EnemyHealthSegmentsFilledForTests, 20,
                "STATE UNREACHED: partial health rendered as fully filled.");
        }

        [Test]
        public void EnemyHud_LethalThreshold_MarkerReflectsRealBoardDamage()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Coverage_HudLethal");

            bootstrap.AutoFormationForTests(); // real cards, real Attack values, all 3 lanes
            int realIncomingAttack = 0;
            foreach (Lane lane in System.Enum.GetValues(typeof(Lane)))
            {
                foreach (BattleCardInstance c in bootstrap.Battle.PlayerState.Lanes[lane].Cards)
                {
                    if (c.IsAlive) realIncomingAttack += c.Attack;
                }
            }
            Assert.Greater(realIncomingAttack, 0, "Setup: expected Auto Formation to field cards with real Attack.");

            int maxHp = bootstrap.Battle.EnemyState.MaxAvatarHealth;
            // Put the enemy just above the real board's lethal range, so the marker sits inside
            // the bar (not clamped to either end) and is genuinely checking a real threshold.
            bootstrap.Battle.EnemyState.AvatarHealth = Mathf.Clamp(realIncomingAttack + 5, 1, maxHp);
            bootstrap.RefreshAllForTests();

            Assert.IsTrue(bootstrap.EnemyLethalMarkerPresentForTests, "STATE UNREACHED: no lethal marker was built.");
            float expectedFraction = GameBootstrap.ComputeLethalMarkerFraction(
                bootstrap.Battle.EnemyState.AvatarHealth, maxHp, realIncomingAttack);
            Assert.AreEqual(expectedFraction, bootstrap.EnemyLethalMarkerFractionForTests, 0.001f,
                "STATE UNREACHED: the marker's real rendered position did not match the real board's lethal threshold.");
        }

        [Test]
        public void EnemyHud_ZeroHealth_AllSegmentsEmpty()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Coverage_HudZero");

            bootstrap.Battle.EnemyState.AvatarHealth = 0;
            bootstrap.RefreshAllForTests();

            Assert.AreEqual(0, bootstrap.EnemyHealthSegmentsFilledForTests,
                "STATE UNREACHED: zero health did not render as zero filled segments.");
        }
    }
}
