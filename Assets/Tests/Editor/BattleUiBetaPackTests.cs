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
using UnityEngine.UI;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// UI Beta Implementation Pack 2026-09-20 (Battle/Battle_Spell_Family_Mapping.md and
    /// Battle_State_Acceptance_GUI_Handoff.md): the 36-ID family mapping, the clipped VFX stage,
    /// the explicit target marker, the Victory/Defeat action labels, and clean Retry / Replay /
    /// Return to Empire transitions. Presentation only - no combat rule, damage, Save, Economy,
    /// reward, MatchResult, OnMatchCompleted, Instance or SetBattleCanvasVisible change is
    /// asserted or made here.
    /// </summary>
    public class BattleUiBetaPackTests
    {
        // (spell id, pack family, normal-motion asset root) - copied verbatim from the pack's mapping.
        private static readonly (string id, string family, string root)[] PackMapping =
        {
            ("firestorm", "FirestormImpact", "Firestorm_Impact"),
            ("mend", "Heal", "Heal_Ring"),
            ("war_cry", "AttackBuff", "Magic_Circle"),
            ("divine_bolt", "AvatarStrike", "Lightning_Strike"),
            ("cinder_lash", "LaneDamage", "Fire_Explosion"),
            ("ember_wave", "LaneDamage", "Fire_Explosion"),
            ("fault_line", "LaneDamage", "Fire_Explosion"),
            ("vital_spark", "Heal", "Heal_Ring"),
            ("renewal", "Heal", "Heal_Ring"),
            ("rallying_gale", "AttackBuff", "Magic_Circle"),
            ("banner_of_ashes", "AttackBuff", "Magic_Circle"),
            ("sun_lance", "AvatarStrike", "Lightning_Strike"),
            ("stone_judgment", "AvatarStrike", "Lightning_Strike"),
            ("tempest_brand", "LaneDamage", "Fire_Explosion"),
            ("magma_rend", "LaneDamage", "Fire_Explosion"),
            ("blood_price", "AvatarStrike", "Lightning_Strike"),
            ("grave_mend", "Heal", "Heal_Ring"),
            ("celestial_verdict", "AvatarStrike", "Lightning_Strike"),
            ("aegis_return", "Heal", "Heal_Ring"),
            ("ember_guard", "Shield", "Shield_Bubble"),
            ("earthward", "Shield", "Shield_Bubble"),
            ("stonewall", "Shield", "Shield_Bubble"),
            ("veil_of_zeus", "Shield", "Shield_Bubble"),
            ("cleansing_root", "CleanseDispel", "Cleanse_Dispel"),
            ("gale_break", "CleanseDispel", "Cleanse_Dispel"),
            ("infernal_mark", "MarkSilence", "Mark_Silence"),
            ("thunder_decree", "AttackBuff", "Magic_Circle"),
            ("ashfall", "AreaDamage", "Area_Damage"),
            ("stormchain", "AreaDamage", "Area_Damage"),
            ("scorched_sky", "AreaDamage", "Area_Damage"),
            ("leyline_draw", "DrawMovement", "Draw_Movement"),
            ("oracle_sight", "DrawMovement", "Draw_Movement"),
            ("windstep", "DrawMovement", "Draw_Movement"),
            ("seismic_swap", "DrawMovement", "Draw_Movement"),
            ("volcanic_prison", "MarkSilence", "Mark_Silence"),
            ("titan_seal", "MarkSilence", "Mark_Silence")
        };

        private readonly List<GameObject> _spawned = new List<GameObject>();
        private string _scratchSaveDir;
        private bool _reduceMotionBefore;

        [SetUp]
        public void SetUp()
        {
            _scratchSaveDir = Path.Combine(Path.GetTempPath(), "MoDUiBetaPack_" + System.Guid.NewGuid().ToString("N"));
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
            var databaseGo = new GameObject("UiBetaPack_CardDatabase");
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

        // ---------- 36-ID family mapping ----------

        [Test]
        public void PackMapping_CoversExactlyTheCatalog_36Ids_In10Families()
        {
            Assert.AreEqual(36, PackMapping.Length);
            CollectionAssert.AreEquivalent(AvatarSpell.CreateCatalog().Select(s => s.Id).ToList(), PackMapping.Select(m => m.id).ToList(),
                "The pack's 36 IDs must be exactly the runtime catalog.");
            Assert.AreEqual(10, PackMapping.Select(m => m.family).Distinct().Count(), "Ten shared families, no bespoke effects.");
        }

        [Test]
        public void EverySpell_ResolvesToItsPackAsset_NormalAndReducedMotion()
        {
            Dictionary<string, AvatarSpell> catalog = AvatarSpell.CreateCatalog().ToDictionary(s => s.Id);
            foreach ((string id, string family, string root) in PackMapping)
            {
                foreach (bool reduce in new[] { false, true })
                {
                    string firstRequested = null;
                    Sprite Load(string path)
                    {
                        if (firstRequested == null) firstRequested = path;
                        return Resources.Load<Sprite>(path);
                    }

                    Sprite sprite = GameBootstrap.ResolveSpellEffectSprite(catalog[id], reduce, Load);

                    string expected = "UI/VFX/" + root + (reduce ? "_Static" : "");
                    Assert.AreEqual(expected, firstRequested, $"{id} ({family}, reduceMotion={reduce}) must use the pack's asset first.");
                    Assert.IsNotNull(sprite, $"{id} ({family}, reduceMotion={reduce}) must resolve to real art.");
                }
            }
        }

        [Test]
        public void Fallback_MissingAnimatedFrame_HoldsTheFamilysOwnStaticMarker_NotAnotherFamily()
        {
            Dictionary<string, AvatarSpell> catalog = AvatarSpell.CreateCatalog().ToDictionary(s => s.Id);
            foreach ((string id, string family, string root) in PackMapping)
            {
                var requested = new List<string>();
                Sprite marker = Sprite.Create(new Texture2D(2, 2), new Rect(0, 0, 2, 2), Vector2.zero);
                Sprite Load(string path)
                {
                    requested.Add(path);
                    return path == "UI/VFX/" + root + "_Static" ? marker : null;
                }

                Sprite result = GameBootstrap.ResolveSpellEffectSprite(catalog[id], false, Load);

                Assert.AreSame(marker, result, $"{id}: a missing animated asset must hold the family's static marker ({family}).");
                Assert.AreEqual("UI/VFX/" + root, requested[0]);
                Assert.AreEqual("UI/VFX/" + root + "_Static", requested[1], $"{id}: the very next attempt is the family's own static marker.");
            }
        }

        // ---------- clipped VFX stage + target marker ----------

        [Test]
        public void SpellImpact_IsClippedToTheVfxStage_AndNeverParentedToTheSpellRows()
        {
            foreach (bool reduce in new[] { false, true })
            {
                MotionPolicy.ReduceMotion = reduce;
                GameBootstrap bootstrap = SpawnAndInitializeBootstrap("UiBeta_Stage_" + reduce);
                Transform stage = bootstrap.VfxStageForTests;
                Assert.IsNotNull(stage, "Setup: the Battle VFX stage exists.");
                Assert.IsNotNull(stage.GetComponent<RectMask2D>(), "The VFX stage must clip its contents.");

                bootstrap.PlayCastImpactForTests(AvatarSpell.CreateCatalog()[0], Lane.Front);

                IReadOnlyList<GameObject> effects = bootstrap.PresentationEffectObjectsForTests;
                Assert.GreaterOrEqual(effects.Count, 1, $"An impact effect is drawn (reduceMotion={reduce}).");
                Transform spellRail = bootstrap.BattlePresentationRootForTests.Find("SpellRail");
                Assert.IsNotNull(spellRail, "Setup: the spell rail exists.");
                foreach (GameObject effect in effects)
                {
                    Assert.IsTrue(effect.transform.IsChildOf(stage), "The impact effect lives inside the clipped VFX stage.");
                    Assert.IsFalse(effect.GetComponent<Image>().raycastTarget, "VFX must never block input.");
                    Assert.IsFalse(effect.transform.IsChildOf(spellRail), "VFX must not cover the spell rows.");
                }
            }
        }

        [Test]
        public void SpellImpact_ShowsSpellNameAndAnExplicitTargetMarker()
        {
            GameBootstrap bootstrap = SpawnAndInitializeBootstrap("UiBeta_TargetMarker");
            AvatarSpell damage = AvatarSpell.CreateCatalog().First(s => s.Effect == SpellEffect.LaneDamage);
            AvatarSpell heal = AvatarSpell.CreateCatalog().First(s => s.Effect == SpellEffect.LaneHeal);
            Transform stage = bootstrap.VfxStageForTests;

            bootstrap.PlayCastImpactForTests(damage, Lane.Middle);
            bootstrap.PlayCastImpactForTests(heal, Lane.Back);

            List<string> texts = stage.GetComponentsInChildren<Text>().Select(t => t.text).ToList();
            CollectionAssert.Contains(texts, damage.Name.ToUpperInvariant(), "The spell name stays visible.");
            CollectionAssert.Contains(texts, GameBootstrap.SpellTargetMarkerText(false, Lane.Middle), "An enemy-lane spell marks the enemy lane.");
            CollectionAssert.Contains(texts, GameBootstrap.SpellTargetMarkerText(true, Lane.Back), "A friendly-lane spell marks the player's lane.");
        }

        [Test]
        public void TargetMarkerText_NamesSideAndLane()
        {
            Assert.AreEqual("ENEMY FRONT", GameBootstrap.SpellTargetMarkerText(false, Lane.Front));
            Assert.AreEqual("YOUR BACK", GameBootstrap.SpellTargetMarkerText(true, Lane.Back));
        }

        // ---------- result actions ----------

        [Test]
        public void Victory_OffersReplayAndReturnToEmpire()
        {
            GameBootstrap bootstrap = ResolveNormalMatch("UiBeta_Victory", playerWins: true);

            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests);
            Assert.AreEqual(GameBootstrap.ReplayLabel, bootstrap.PlayAgainLabelForTests);
            Assert.AreEqual(GameBootstrap.ReturnToEmpireLabel, bootstrap.ReturnToCityLabelForTests);
            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests);
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests);
        }

        [Test]
        public void Defeat_OffersRetryAndReturnToEmpire()
        {
            GameBootstrap bootstrap = ResolveNormalMatch("UiBeta_Defeat", playerWins: false);

            Assert.IsTrue(bootstrap.ResultOverlayActiveForTests);
            Assert.AreEqual(GameBootstrap.RetryLabel, bootstrap.PlayAgainLabelForTests);
            Assert.AreEqual(GameBootstrap.ReturnToEmpireLabel, bootstrap.ReturnToCityLabelForTests);
            Assert.IsTrue(bootstrap.PlayAgainButtonActiveForTests);
            Assert.IsTrue(bootstrap.ReturnToCityButtonActiveForTests);
        }

        // ---------- retry / replay / return ----------

        [Test]
        public void Retry_ReturnsToAValidFormation_WithNoOverlayBannerOrStaleEffects()
        {
            GameBootstrap bootstrap = ResolveNormalMatch("UiBeta_Retry", playerWins: false);
            LogAssert.ignoreFailingMessages = true; // edit-mode Destroy() log from the shared cleanup path

            bootstrap.RetryForTests();

            Assert.IsFalse(bootstrap.ResultOverlayActiveForTests, "The result overlay is gone after Retry.");
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase, "Retry returns to a valid Formation.");
            Assert.AreEqual(0, bootstrap.StartBattleBannerCountForTests, "No banner survives Retry.");
            Assert.AreEqual(0, bootstrap.PresentationEffectCountForTests, "No stale VFX survives Retry.");
        }

        [Test]
        public void Replay_ReentersFormation_WithoutStackingOverlays()
        {
            GameBootstrap bootstrap = ResolveNormalMatch("UiBeta_Replay", playerWins: true);
            LogAssert.ignoreFailingMessages = true;

            bootstrap.PlayAgainForTests();

            Assert.IsFalse(bootstrap.ResultOverlayActiveForTests, "Replay hides the result overlay.");
            Assert.AreEqual(BattlePhase.Formation, bootstrap.Battle.Phase, "Replay re-enters the existing Formation flow.");
        }

        [Test]
        public void ReturnToEmpire_FiresOneReturnAndLeavesNoResidualBattleCanvas_EvenIfPressedTwice()
        {
            GameBootstrap bootstrap = ResolveNormalMatch("UiBeta_Return", playerWins: true);
            int returns = 0;
            System.Action handler = () => returns++;
            bootstrap.OnReturnToCityRequested += handler;
            try
            {
                bootstrap.ReturnToCityForTests();
                bootstrap.ReturnToCityForTests();
            }
            finally
            {
                bootstrap.OnReturnToCityRequested -= handler;
            }

            Assert.AreEqual(1, returns, "One destination action, however many times it is pressed.");
            Assert.IsFalse(bootstrap.BattleCanvasVisibleForTests, "No residual Battle canvas layer after Return to Empire.");
            Assert.IsFalse(bootstrap.ResultOverlayActiveForTests, "No result overlay left behind.");
        }
    }
}
