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
    /// In-game Battle motion (presentation only): formation placement, Battle start, automatic
    /// clash, unit attack / hit feedback, victory/defeat reveal, and retry/replay cleanup. The motion
    /// rules are pure (BattleMotionPlan) so EditMode can test them; the runtime application is
    /// proven through the same envelope sampler the coroutine uses. Nothing here reads or changes
    /// combat state, Save, Economy, rewards or a frozen contract.
    /// </summary>
    public class BattleMotionTests
    {
        private static readonly BattleMotionKind[] AllKinds = (BattleMotionKind[])System.Enum.GetValues(typeof(BattleMotionKind));

        // ---------- pure motion plan ----------

        [Test]
        public void EveryBeat_HasAFullMotionStep_WithADuration_AndSettlesToExactlyRest()
        {
            foreach (BattleMotionKind kind in AllKinds)
            {
                BattleMotionStep step = BattleMotionPlan.For(kind, reduceMotion: false);
                Assert.IsFalse(step.IsStatic, $"{kind} must animate under full motion.");
                Assert.Greater(step.Duration, 0f);
                Assert.Less(step.Duration, 0.5f, $"{kind} must stay short - motion never delays input or the next tick.");
                Assert.AreEqual(1f, BattleMotionPlan.EvaluateScale(step, 1f), 0f, $"{kind} ends exactly at rest size.");
                Assert.AreEqual(step.StartScale, BattleMotionPlan.EvaluateScale(step, 0f), 0.0001f, $"{kind} starts at its start scale.");
                Assert.AreEqual(step.PeakScale, BattleMotionPlan.EvaluateScale(step, BattleMotionPlan.PeakAt), 0.0001f, $"{kind} peaks where declared.");
            }
        }

        [Test]
        public void EveryBeat_ScaleStaysWithinItsEnvelope_ForAnySample()
        {
            foreach (BattleMotionKind kind in AllKinds)
            {
                BattleMotionStep step = BattleMotionPlan.For(kind, false);
                float lo = Mathf.Min(1f, Mathf.Min(step.StartScale, step.PeakScale));
                float hi = Mathf.Max(1f, Mathf.Max(step.StartScale, step.PeakScale));
                for (float t = -0.5f; t <= 1.5f; t += 0.05f)
                {
                    float scale = BattleMotionPlan.EvaluateScale(step, t);
                    Assert.GreaterOrEqual(scale, lo - 0.0001f, $"{kind} t={t}");
                    Assert.LessOrEqual(scale, hi + 0.0001f, $"{kind} t={t}");
                }
            }
        }

        [Test]
        public void ReducedMotion_RemovesTheMotion_OfEveryBeat()
        {
            foreach (BattleMotionKind kind in AllKinds)
            {
                BattleMotionStep step = BattleMotionPlan.For(kind, reduceMotion: true);
                Assert.IsTrue(step.IsStatic, $"{kind} must have no motion under Reduced Motion.");
                for (float t = 0f; t <= 1f; t += 0.1f)
                    Assert.AreEqual(1f, BattleMotionPlan.EvaluateScale(step, t), 0f, $"{kind} is a flat, still frame under Reduced Motion.");
            }
        }

        [Test]
        public void HitTint_FadesUnderFullMotion_AndHoldsStillUnderReducedMotion()
        {
            float strength = BattleMotionPlan.HeavyHitTint;

            Assert.AreEqual(strength, BattleMotionPlan.HitTintAlpha(strength, 0f, false), 0.0001f);
            Assert.Less(BattleMotionPlan.HitTintAlpha(strength, 0.5f, false), strength, "Full motion fades the tint.");
            Assert.AreEqual(0f, BattleMotionPlan.HitTintAlpha(strength, 1f, false), 0f);

            foreach (float t in new[] { 0f, 0.3f, 0.7f, 0.99f })
                Assert.AreEqual(strength, BattleMotionPlan.HitTintAlpha(strength, t, true), 0f, "Reduced Motion holds a still marker - no fade.");
            Assert.AreEqual(0f, BattleMotionPlan.HitTintAlpha(strength, 1f, true), 0f, "It still ends.");
        }

        // ---------- attack / hit reaction (reads a resolved clash only) ----------

        private static LaneClashResult Clash(int defeatedA = 0, int defeatedB = 0, int overflowA = 0, int overflowB = 0, bool clearedA = false, bool clearedB = false) =>
            new LaneClashResult
            {
                Lane = Lane.Front,
                SideACleared = clearedA,
                SideBCleared = clearedB,
                OverflowToA = overflowA,
                OverflowToB = overflowB,
                DefeatedCardNamesA = Enumerable.Repeat("x", defeatedA).ToList(),
                DefeatedCardNamesB = Enumerable.Repeat("y", defeatedB).ToList(),
            };

        [Test]
        public void Reaction_TradedBlowsWithNoLoss_IsAnAttackLunge_ForBothSides()
        {
            LaneClashResult trade = Clash();
            Assert.AreEqual(BattleMotionKind.AttackLunge, BattleMotionPlan.ReactionFor(trade, sideA: true));
            Assert.AreEqual(BattleMotionKind.AttackLunge, BattleMotionPlan.ReactionFor(trade, sideA: false));
            Assert.AreEqual(BattleMotionPlan.LightHitTint, BattleMotionPlan.HitTintStrengthFor(trade, true));
        }

        [Test]
        public void Reaction_LostUnitsOrAvatarOverflowOrClearedLane_IsAHeavyHit_OnThatSideOnly()
        {
            LaneClashResult lostA = Clash(defeatedA: 1);
            Assert.AreEqual(BattleMotionKind.HitFeedback, BattleMotionPlan.ReactionFor(lostA, true));
            Assert.AreEqual(BattleMotionKind.AttackLunge, BattleMotionPlan.ReactionFor(lostA, false), "The other side did not lose anything.");

            Assert.AreEqual(BattleMotionKind.HitFeedback, BattleMotionPlan.ReactionFor(Clash(overflowB: 5), false), "Avatar overflow is a heavy hit.");
            Assert.AreEqual(BattleMotionKind.HitFeedback, BattleMotionPlan.ReactionFor(Clash(clearedA: true), true), "A cleared lane is a heavy hit.");
            Assert.AreEqual(BattleMotionPlan.HeavyHitTint, BattleMotionPlan.HitTintStrengthFor(lostA, true));
            Assert.Greater(BattleMotionPlan.HeavyHitTint, BattleMotionPlan.LightHitTint);
            Assert.Greater(BattleMotionPlan.LightHitTint, 0f);
        }

        [Test]
        public void Reaction_ToleratesAResultWithNoDefeatedLists()
        {
            var bare = new LaneClashResult { Lane = Lane.Middle };
            Assert.AreEqual(BattleMotionKind.AttackLunge, BattleMotionPlan.ReactionFor(bare, true));
            Assert.AreEqual(BattleMotionKind.AttackLunge, BattleMotionPlan.ReactionFor(bare, false));
        }

        // ---------- runtime application + cleanup ----------

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private bool _reduceMotionBefore;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDBattleMotion_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            _reduceMotionBefore = MotionPolicy.ReduceMotion;
        }

        [TearDown]
        public void TearDown()
        {
            MotionPolicy.ReduceMotion = _reduceMotionBefore;
            LogAssert.ignoreFailingMessages = false;
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
            var databaseGo = new GameObject("BattleMotion_CardDatabase");
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

        [Test]
        public void MotionSample_ScalesALaneSlot_AndRegistersItForCleanup()
        {
            MotionPolicy.ReduceMotion = false;
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Motion_Sample");
            Transform slot = bootstrap.PlayerLaneSlotForTests(Lane.Front);

            bootstrap.ApplyMotionSampleForTests(slot, BattleMotionKind.PlacementLand, BattleMotionPlan.PeakAt);

            Assert.Greater(slot.localScale.x, 1f, "A placement lands with a scale beat.");
            Assert.AreEqual(1, bootstrap.MotionTargetCountForTests);
        }

        [Test]
        public void Interruption_HidingBattle_ReturnsEveryAnimatedTargetToRest()
        {
            MotionPolicy.ReduceMotion = false;
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Motion_Interrupt");
            Transform player = bootstrap.PlayerLaneSlotForTests(Lane.Front);
            Transform enemy = bootstrap.EnemyLaneSlotForTests(Lane.Middle);
            bootstrap.ApplyMotionSampleForTests(player, BattleMotionKind.AttackLunge, BattleMotionPlan.PeakAt);
            bootstrap.ApplyMotionSampleForTests(enemy, BattleMotionKind.HitFeedback, BattleMotionPlan.PeakAt);
            Assert.AreNotEqual(1f, player.localScale.x, "Setup: mid-beat.");

            bootstrap.SetBattleCanvasVisible(false);

            Assert.AreEqual(1f, player.localScale.x, 0f, "An interrupted beat must not leave a lane scaled.");
            Assert.AreEqual(1f, enemy.localScale.x, 0f);
            Assert.AreEqual(0, bootstrap.MotionTargetCountForTests);
        }

        [Test]
        public void RetryAndReplay_ReturnEveryAnimatedTargetToRest_AndClearStaleMotion()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Motion_Retry");
            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();
            BattleController controller = bootstrap.Battle;
            controller.PlayerState.AvatarHealth = 1;
            int guard = 0;
            while (controller.Phase == BattlePhase.Combat && guard++ < BattleController.MaxCombatTicks)
                controller.AdvanceCombatTick();
            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests, "Setup: a defeat result is showing.");

            Transform slot = bootstrap.PlayerLaneSlotForTests(Lane.Back);
            bootstrap.ApplyMotionSampleForTests(slot, BattleMotionKind.ResultReveal, 0f);
            Assert.AreNotEqual(1f, slot.localScale.x, "Setup: mid-beat at retry time.");
            LogAssert.ignoreFailingMessages = true; // edit-mode Destroy() log from unrelated shared cleanup

            bootstrap.RetryForTests();

            Assert.AreEqual(1f, slot.localScale.x, 0f, "Retry resets any animated target to rest.");
            Assert.AreEqual(0, bootstrap.MotionTargetCountForTests, "No stale motion survives Retry.");
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase);
        }

        [Test]
        public void MotionBeats_DoNotChangeCombatState()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("Motion_NoCombatEffect");
            bootstrap.AutoFormationForTests();
            bootstrap.StartBattleForTests();
            BattleController controller = bootstrap.Battle;
            int playerHealth = controller.PlayerState.AvatarHealth;
            int enemyHealth = controller.EnemyState.AvatarHealth;
            int tick = controller.TickCount;
            int energy = controller.Energy;

            foreach (BattleMotionKind kind in AllKinds)
                bootstrap.ApplyMotionSampleForTests(bootstrap.PlayerLaneSlotForTests(Lane.Front), kind, 0.5f);

            Assert.AreEqual(playerHealth, controller.PlayerState.AvatarHealth);
            Assert.AreEqual(enemyHealth, controller.EnemyState.AvatarHealth);
            Assert.AreEqual(tick, controller.TickCount);
            Assert.AreEqual(energy, controller.Energy);
            Assert.AreEqual(BattlePhase.Combat, controller.Phase);
        }
    }
}
