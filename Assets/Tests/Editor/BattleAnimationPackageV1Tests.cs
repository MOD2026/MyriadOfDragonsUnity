using System.Collections.Generic;
using System.IO;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Combat;
using MyriadOfDragons.Save;
using MyriadOfDragons.UI;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// BATTLE_ANIMATION_PACKAGE_V1, proceeding without the four unconfirmed FX atlases (no new art
    /// created or packaged - AD's own atlas-existence check was never confirmed, and none exist
    /// under Assets/Resources by the spec's names, verified directly). Audit against
    /// docs/BATTLE_ANIMATION_PACKAGE_V1.md's nine-beat timing table found eight of nine beats
    /// already implemented and tested (placement/reinforcement via SlideNewestCardIntoLane+
    /// PlayEffect, attack/contact and damage/defeat via CombatResolutionStage/ShowTurnDamage,
    /// spell cast via PlayCastImpact, tick/result via CombatResolutionEventMapper, victory/defeat
    /// via the existing result overlay) - all already covered by CombatFeedbackWiringTests,
    /// CombatResolutionStageTests, and ResultOverlayOutcomeCoverageTests, none of which this task
    /// touches. The one real, verified gap was "Card draw / hand arrival" (RefreshHand had no
    /// entrance presentation at all) - implemented here, procedurally, with a "replaceable FX
    /// hook" shape (no atlas dependency; the timing/stagger logic is independent of what visual,
    /// if any, is layered on top later).
    /// </summary>
    public class BattleAnimationPackageV1Tests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private bool _reduceMotionBefore;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDAnimPackageV1_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_scratchSaveDir);
            SaveSystem.OverrideRootDirectoryForTests(_scratchSaveDir);
            SaveSystem.ResetCurrentProfileForTests();
            _reduceMotionBefore = MotionPolicy.ReduceMotion;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned) if (go != null) Object.DestroyImmediate(go);
            _spawned.Clear();
            SaveSystem.ClearRootDirectoryOverride();
            SaveSystem.ResetCurrentProfileForTests();
            MotionPolicy.ReduceMotion = _reduceMotionBefore;
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
            var databaseGo = new GameObject("AnimPackageV1_CardDatabase");
            CardDatabase database = databaseGo.AddComponent<CardDatabase>();
            database.Initialize();
            database = CardDatabase.Instance;
            var sizingProfile = new PlayerProfile();
            sizingProfile.ApplyDataToEmpire();
            int deckSize = sizingProfile.Empire.DeckSlotCount;
            List<string> deckIds = database.AllCards.Select(c => c.Id)
                .Where(id => id != "warrior" && id != "novice_knight" && id != "goblin_caster")
                .Take(deckSize).ToList();
            var profile = new PlayerProfile { cardCollection = new List<string>(deckIds), activeDeckCardIds = new List<string>(deckIds) };
            Assert.IsTrue(SaveSystem.Save(profile), "Setup: the production save path must persist the confirmed deck.");
            SaveSystem.ResetCurrentProfileForTests();
            Object.DestroyImmediate(databaseGo);
        }

        // ---------- No unapproved FX atlases ----------

        [Test]
        public void NoneOfTheFourListedFxAtlases_ExistInTheProject()
        {
            // docs/BATTLE_ANIMATION_PACKAGE_V1.md "Missing assets" section names four specific
            // atlases. AD's existence check was never confirmed - verified directly here so this
            // task's "proceed without them" premise is checked, not assumed.
            string[] forbiddenNameFragments = { "sparkle", "hitflash", "hit_flash", "spellimpactoverlay", "dockpulse", "dock_pulse" };
            string[] allResourceFiles = Directory.GetFiles(Application.dataPath + "/Resources", "*.png", SearchOption.AllDirectories);
            foreach (string file in allResourceFiles)
            {
                string lowerName = Path.GetFileNameWithoutExtension(file).ToLowerInvariant().Replace("-", "").Replace("_", "");
                foreach (string fragment in forbiddenNameFragments)
                {
                    string normalizedFragment = fragment.Replace("_", "");
                    Assert.IsFalse(lowerName.Contains(normalizedFragment),
                        $"Found an asset matching an unapproved FX atlas name ({fragment}): {file}. " +
                        "This task must proceed without them - if one now exists, the premise here is stale.");
                }
            }
        }

        // ---------- Card draw / hand arrival (the one real gap) ----------

        [Test]
        public void HandArrivalAnimation_TriggersExactlyOnce_ForTheInitialDeal()
        {
            // Initialize() itself deals the formation hand and runs the startup RefreshAll
            // synchronously - by the time control returns here, the pending flag has already been
            // armed AND consumed within that one call chain, so the externally-observable signal
            // is the trigger COUNT, not the transient pending flag (see the flag's own comment).
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AnimPackageV1_ArrivalTriggerCount");

            Assert.AreEqual(1, bootstrap.HandArrivalAnimationTriggerCountForTests,
                "STATE UNREACHED: the initial real deal must trigger the arrival animation exactly once.");
            Assert.IsFalse(bootstrap.HandArrivalAnimationPendingForTests, "Setup: expected the initial deal already consumed.");
        }

        [Test]
        public void HandArrivalAnimation_NeverRetriggers_OnUnrelatedRefreshes()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AnimPackageV1_ArrivalNoReplay");
            Assert.AreEqual(1, bootstrap.HandArrivalAnimationTriggerCountForTests, "Setup: expected exactly one initial trigger.");

            // Deterministic mapping: further unrelated refreshes (selecting a card, any other UI
            // change) must never re-arm or re-trigger it - only a real DealFormationHand call does.
            for (int i = 0; i < 5; i++)
            {
                bootstrap.RefreshAllForTests();
                Assert.AreEqual(1, bootstrap.HandArrivalAnimationTriggerCountForTests,
                    $"Refresh #{i}: an unrelated RefreshAll must never retrigger the arrival animation.");
            }
        }

        [Test]
        public void HandArrivalAnimation_TriggersAgain_OnATrueSecondDeal_ResetLineup()
        {
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AnimPackageV1_ArrivalRedeal");
            Assert.AreEqual(1, bootstrap.HandArrivalAnimationTriggerCountForTests, "Setup: expected exactly one initial trigger.");

            bootstrap.ResetLineupForTests(); // a real second DealFormationHand(PlayerState) call.

            Assert.AreEqual(2, bootstrap.HandArrivalAnimationTriggerCountForTests,
                "A genuine second deal (Reset Lineup) must trigger the arrival animation again.");
            Assert.IsFalse(bootstrap.HandArrivalAnimationPendingForTests, "The second deal's own RefreshAll must also consume its flag.");
        }

        [Test]
        public void HandArrivalTiming_MatchesTheLockedSpec_220msSlideFade_60msStagger_3CardCap()
        {
            // Named-value regression: docs/BATTLE_ANIMATION_PACKAGE_V1.md's "Card draw / hand
            // arrival" row ("180-260 ms slide/fade; stagger at most 3 cards, 60 ms apart").
            Assert.AreEqual(220, CombatPresentationPolicy.HandArrivalMs);
            Assert.GreaterOrEqual(CombatPresentationPolicy.HandArrivalMs, 180);
            Assert.LessOrEqual(CombatPresentationPolicy.HandArrivalMs, 260);
            Assert.AreEqual(60, CombatPresentationPolicy.HandArrivalStaggerMs);
            Assert.AreEqual(3, CombatPresentationPolicy.HandArrivalMaxStaggeredCards);
        }

        [Test]
        public void HandArrivalDuration_ResolvesToZero_UnderReducedMotion()
        {
            // The same shared ResolveDurationMs policy every other beat already uses for Reduced
            // Motion (an immediate/static result, no animation to cancel mid-flight) - this beat
            // must not invent a separate Reduced Motion rule.
            Assert.Greater(CombatPresentationPolicy.ResolveDurationMs(CombatPresentationPolicy.HandArrivalMs, reducedMotion: false), 0);
            Assert.AreEqual(0, CombatPresentationPolicy.ResolveDurationMs(CombatPresentationPolicy.HandArrivalMs, reducedMotion: true));
        }

        // ---------- Existing beats untouched: presence + gameplay-neutrality spot checks ----------

        [Test]
        public void ExistingReinforcementAndPlacementPresentation_StillFireOnFormationLock()
        {
            // Confirms this task did not disturb the already-implemented Placement beat while
            // adding the new Hand Arrival beat next to it in the same file area.
            SaveValidDeckForNormalMatch();
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("AnimPackageV1_PlacementUntouched");
            bootstrap.AutoFormationForTests();
            Assert.IsTrue(bootstrap.Battle.PlayerState.Lanes.Values.Sum(l => l.Cards.Count) > 0,
                "Setup: Auto Formation must have placed at least one card.");
            // No exception, no altered card/Resource/lane state beyond what AutoFormationForTests
            // itself performs - the real behavioral proof (SlideNewestCardIntoLane firing) is a
            // Play-Mode-only visual and is out of this focused EditMode file's scope; this asserts
            // the gameplay side of that same code path is unaffected by BuildCinematicOverlay's
            // and RefreshHand's edits in this and the prior task.
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase);
        }
    }
}
