using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Makes the 10 Phase-1 spells beyond the starter four actually reachable in real matches,
    /// instead of only existing for SpellCatalogPhase1Tests to construct in isolation. Covers the
    /// unlock resolver (SPELL_CATALOG_v1.md's own "Unlock" column, resolved against real
    /// PlayerProfile/PlayerEmpireData data - Avatar level, unlocked campaign stage ids - with no
    /// new Save schema), the Phase-1 auto-equip stopgap (one spell per effect type, which also
    /// structurally enforces MOS_v1.1.md's "max 1 AvatarStrike equipped" lock), and
    /// BattleController.StartMatch's real wiring of both.
    /// </summary>
    public class SpellLoadoutTests
    {
        // ---------- SpellUnlockResolver ----------

        [TestCase("Firestorm")]
        [TestCase("Mend")]
        [TestCase("War Cry")]
        [TestCase("Divine Bolt")]
        public void IsUnlocked_StarterSpells_AreAlwaysUnlocked(string name)
        {
            AvatarSpell spell = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == name);
            Assert.IsTrue(SpellUnlockResolver.IsUnlocked(spell, avatarLevel: 1, unlockedStageIds: null));
        }

        [Test]
        public void IsUnlocked_StageGatedSpell_LockedUntilItsStageIsUnlocked()
        {
            AvatarSpell cinderLash = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == "Cinder Lash");
            Assert.IsFalse(SpellUnlockResolver.IsUnlocked(cinderLash, avatarLevel: 99, unlockedStageIds: new List<string> { "1-1" }),
                "A stage-gated spell must not unlock from Avatar level alone.");
            Assert.IsTrue(SpellUnlockResolver.IsUnlocked(cinderLash, avatarLevel: 1, unlockedStageIds: new List<string> { "1-1", "1-2" }),
                "Cinder Lash's catalog row is Ch1-2 - it must unlock once stage '1-2' is reached.");
        }

        [Test]
        public void IsUnlocked_AvatarLevelGatedSpell_LockedUntilTheRequiredLevel()
        {
            AvatarSpell stoneJudgment = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == "Stone Judgment");
            Assert.IsFalse(SpellUnlockResolver.IsUnlocked(stoneJudgment, avatarLevel: 11, unlockedStageIds: null));
            Assert.IsTrue(SpellUnlockResolver.IsUnlocked(stoneJudgment, avatarLevel: 12, unlockedStageIds: null));
        }

        [TestCase("Sun Lance")]
        [TestCase("Tempest Brand")]
        public void IsUnlocked_SpellBookGatedSpells_NeverUnlockViaAvatarLevelOrStageAlone(string name)
        {
            // Catalog rows "Ch2 spell book" / "Ch3 spell book" name a real acquisition channel
            // now (SpellBookGrant) - but it's a fundamentally different gate (own the finale spell
            // book) this resolver's own inputs (Avatar level, unlocked stage ids) cannot evaluate.
            // Proven here with a deliberately absurd amount of level/stage progress, so this can't
            // pass by accident - see SpellBookGrantTests for the real acquisition path.
            AvatarSpell spell = AvatarSpell.CreatePhase1Catalog().Single(s => s.Name == name);
            var everyStage = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" };
            Assert.IsFalse(SpellUnlockResolver.IsUnlocked(spell, avatarLevel: 999, unlockedStageIds: everyStage));
        }

        [Test]
        public void HasUnresolvableSpellBookGates_IsNowFalse_TheGapIsResolved()
        {
            // RESOLVED 2026-08-24: SpellBookGrant is the real acquisition channel Sun Lance/
            // Tempest Brand were missing. This flag existing at all, still true, would mean that
            // gap had silently regressed back to "nothing tracks this."
            Assert.IsFalse(SpellUnlockResolver.HasUnresolvableSpellBookGates);
        }

        [Test]
        public void IsUnlocked_ANonCatalogSpell_IsNeverUnlocked()
        {
            var madeUp = new AvatarSpell("Not In The Catalog", "d", energyCost: 1, cooldownTicks: 1, SpellEffect.LaneDamage, magnitude: 1);
            Assert.IsFalse(SpellUnlockResolver.IsUnlocked(madeUp, avatarLevel: 999, unlockedStageIds: new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" }));
        }

        [Test]
        public void ResolveUnlockedSpells_FreshPlayer_IsExactlyTheStarterFour()
        {
            List<AvatarSpell> unlocked = SpellUnlockResolver.ResolveUnlockedSpells(avatarLevel: 1, unlockedStageIds: null);
            CollectionAssert.AreEquivalent(new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt" }, unlocked.Select(s => s.Name).ToList());
        }

        [Test]
        public void ResolveUnlockedSpells_FullProgress_IncludesEveryReachableSpellButNotTheSpellBookGatedOnes()
        {
            var everyStage = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" };
            List<AvatarSpell> unlocked = SpellUnlockResolver.ResolveUnlockedSpells(avatarLevel: 12, unlockedStageIds: everyStage);

            CollectionAssert.AreEquivalent(
                new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt", "Cinder Lash", "Ember Wave", "Fault Line",
                    "Vital Spark", "Renewal", "Rallying Gale", "Banner of Ashes", "Stone Judgment" },
                unlocked.Select(s => s.Name).ToList());
            CollectionAssert.DoesNotContain(unlocked.Select(s => s.Name).ToList(), "Sun Lance");
            CollectionAssert.DoesNotContain(unlocked.Select(s => s.Name).ToList(), "Tempest Brand");
        }

        // ---------- SpellLoadoutAutoEquip ----------

        [Test]
        public void AutoEquip_FreshPlayer_ResolvesToExactlyTheStarterFourInOrder()
        {
            List<AvatarSpell> loadout = SpellLoadoutAutoEquip.AutoEquip(avatarLevel: 1, unlockedStageIds: null);
            CollectionAssert.AreEqual(new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt" }, loadout.Select(s => s.Name).ToList(),
                "A fresh player's auto-equipped loadout must match the old hardcoded starter four exactly, same order.");
        }

        [Test]
        public void AutoEquip_AlwaysHasAtMostOneAvatarStrikeSpell()
        {
            var everyStage = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" };
            for (int level = 1; level <= 15; level++)
            {
                List<AvatarSpell> loadout = SpellLoadoutAutoEquip.AutoEquip(level, everyStage);
                Assert.LessOrEqual(loadout.Count(s => s.Effect == SpellEffect.AvatarStrike), 1,
                    $"MOS_v1.1.md's max-1-AvatarStrike lock was violated at Avatar level {level}.");
            }
        }

        /// <summary>Loadout expansion (LOCKED 2026-08-25) changed this from a hard "always 4" -
        /// slot count is now tier-unlocked by Avatar level (SpellLoadoutAutoEquip.
        /// RequiredSlotCount), capped at 6, not a fixed 4. At avatarLevel 999 with a well-
        /// progressed stage list, enough distinct effect types are genuinely reachable (the
        /// original 4 plus Cleanse/DrawCards/Reposition/Silence, each via a real AvatarLevel
        /// Rule) to fill every one of the 6 slots the level unlocks - never more than 6, and
        /// never more than one spell per effect type, still.</summary>
        [Test]
        public void AutoEquip_NeverExceedsTheAvatarLevelsRealSlotCap()
        {
            var everyStage = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" };

            List<AvatarSpell> lowLevelLoadout = SpellLoadoutAutoEquip.AutoEquip(avatarLevel: 1, unlockedStageIds: everyStage);
            Assert.AreEqual(4, lowLevelLoadout.Count, "A level-1 profile still only has 4 slots and only the original 4 effect types are reachable.");

            List<AvatarSpell> maxLevelLoadout = SpellLoadoutAutoEquip.AutoEquip(avatarLevel: 999, unlockedStageIds: everyStage);
            Assert.AreEqual(SpellLoadoutAutoEquip.MaxSlotCount, maxLevelLoadout.Count,
                "A fully-progressed profile has 6 slots, and enough distinct effect types are genuinely reachable to fill all 6.");
            Assert.AreEqual(maxLevelLoadout.Count, maxLevelLoadout.Select(s => s.Effect).Distinct().Count(),
                "Still at most one spell per effect type, even with 6 slots.");
        }

        [Test]
        public void AutoEquip_FullProgress_PicksTheStrongestUnlockedSpellInEachType()
        {
            var everyStage = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" };
            List<AvatarSpell> loadout = SpellLoadoutAutoEquip.AutoEquip(avatarLevel: 12, unlockedStageIds: everyStage);

            // Fault Line (5) > Firestorm (4) > Cinder Lash (2) > Ember Wave (3) - Fault Line wins.
            Assert.IsTrue(loadout.Any(s => s.Name == "Fault Line" && s.Effect == SpellEffect.LaneDamage));
            // Renewal (6) beats Mend (4) and Vital Spark (2).
            Assert.IsTrue(loadout.Any(s => s.Name == "Renewal" && s.Effect == SpellEffect.LaneHeal));
            // Banner of Ashes (+3) beats War Cry (+2) and Rallying Gale (+1).
            Assert.IsTrue(loadout.Any(s => s.Name == "Banner of Ashes" && s.Effect == SpellEffect.LaneAttackBuff));
            // Stone Judgment (120) beats Divine Bolt (100); Sun Lance (75) is never even eligible.
            Assert.IsTrue(loadout.Any(s => s.Name == "Stone Judgment" && s.Effect == SpellEffect.AvatarStrike));
            Assert.IsFalse(loadout.Any(s => s.Name == "Sun Lance"));
        }

        [Test]
        public void AutoEquip_PartialProgress_UpgradesOnlyTheTypesThatHaveANewerUnlockedOption()
        {
            // Only the LaneDamage upgrade (Fault Line, Ch2-4) is reachable - Heal/Buff/Strike
            // upgrades all require progress this scenario deliberately doesn't have.
            var partialStages = new List<string> { "1-1", "2-4" };
            List<AvatarSpell> loadout = SpellLoadoutAutoEquip.AutoEquip(avatarLevel: 1, unlockedStageIds: partialStages);

            CollectionAssert.AreEqual(new[] { "Fault Line", "Mend", "War Cry", "Divine Bolt" }, loadout.Select(s => s.Name).ToList());
        }

        // ---------- BattleController.StartMatch wiring ----------

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private static Card MakeWeakCard(string id) => Card.FromData(new CardData
        {
            id = id, name = "Spell Loadout Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = 1,
        });

        private BattleController CreateController()
        {
            var go = new GameObject("SpellLoadoutTestBattleController");
            _spawned.Add(go);
            return go.AddComponent<BattleController>();
        }

        [Test]
        public void StartMatch_OmittedProgressArgs_StillResolvesToTheStarterFour()
        {
            // Backward compatibility: every pre-existing StartMatch call site (every other test
            // file, the scripted tutorial encounter) must see exactly the same spellbook as
            // before this task, without having to be updated to pass real progress.
            BattleController controller = CreateController();
            Card card = MakeWeakCard("loadout_default_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(20, 20, 1000);
            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy);

            CollectionAssert.AreEqual(new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt" }, controller.Spellbook.Select(s => s.Name).ToList());
            CollectionAssert.AreEqual(new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt" }, controller.EnemySpellbook.Select(s => s.Name).ToList());
        }

        [Test]
        public void StartMatch_WithRealProgress_TheSpellbookReflectsIt()
        {
            BattleController controller = CreateController();
            Card card = MakeWeakCard("loadout_progressed_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(20, 20, 1000);
            var everyStage = new List<string> { "1-1", "1-2", "1-6", "2-4", "2-8", "3-3" };

            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy,
                avatarLevel: 12, unlockedStageIds: everyStage);

            Assert.IsTrue(controller.Spellbook.Any(s => s.Name == "Stone Judgment"), "The player's spellbook should reflect real unlocked progress.");
            Assert.AreEqual(1, controller.Spellbook.Count(s => s.Effect == SpellEffect.AvatarStrike), "Still exactly one AvatarStrike, never two.");
            Assert.IsTrue(controller.EnemySpellbook.Any(s => s.Name == "Stone Judgment"), "The mirrored enemy spellbook uses the same progress inputs.");
        }

        // ---------- StartMatch's equippedSpellIds resolution ----------

        [Test]
        public void StartMatch_WithEquippedSpellIds_ResolvesTheRealPlayerChosenLoadout_NotAutoEquip()
        {
            BattleController controller = CreateController();
            Card card = MakeWeakCard("loadout_equipped_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(20, 20, 1000);
            var equipped = new List<string> { "cinder_lash", "vital_spark", "rallying_gale", "sun_lance" };

            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy,
                avatarLevel: 1, unlockedStageIds: null, equippedSpellIds: equipped);

            CollectionAssert.AreEqual(new[] { "Cinder Lash", "Vital Spark", "Rallying Gale", "Sun Lance" },
                controller.Spellbook.Select(s => s.Name).ToList(),
                "Real player-choice loadout must resolve exactly the equipped ids, in order - not the auto-equip heuristic.");
        }

        [Test]
        public void StartMatch_WithNullOrEmptyEquippedSpellIds_FallsBackToAutoEquip()
        {
            BattleController controller = CreateController();
            Card card = MakeWeakCard("loadout_fallback_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(20, 20, 1000);

            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy,
                avatarLevel: 1, unlockedStageIds: null, equippedSpellIds: new List<string>());

            CollectionAssert.AreEqual(new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt" }, controller.Spellbook.Select(s => s.Name).ToList());
        }

        [Test]
        public void StartMatch_WithAnUnknownEquippedSpellId_SkipsItGracefully()
        {
            BattleController controller = CreateController();
            Card card = MakeWeakCard("loadout_unknown_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(20, 20, 1000);
            var equipped = new List<string> { "firestorm", "not_a_real_spell_id", "mend" };

            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy,
                avatarLevel: 1, unlockedStageIds: null, equippedSpellIds: equipped);

            CollectionAssert.AreEqual(new[] { "Firestorm", "Mend" }, controller.Spellbook.Select(s => s.Name).ToList(),
                "An unknown equipped id must be skipped, not crash or blank the whole spellbook.");
        }

        [Test]
        public void StartMatch_WithOnlyUnknownEquippedSpellIds_FallsBackToAutoEquipRatherThanAnEmptySpellbook()
        {
            BattleController controller = CreateController();
            Card card = MakeWeakCard("loadout_allunknown_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(20, 20, 1000);
            var equipped = new List<string> { "not_a_real_spell_id", "also_not_real" };

            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy,
                avatarLevel: 1, unlockedStageIds: null, equippedSpellIds: equipped);

            Assert.IsTrue(controller.Spellbook.Count > 0, "A caller must never end up with an empty castable spellbook.");
        }

        // ---------- StartMatch's enemyTier wiring (AI's own loadout, not mirrored) ----------

        [Test]
        public void StartMatch_WithEnemyTier_TheEnemySpellbookIsTierAuthored_NotMirroredFromThePlayer()
        {
            BattleController controller = CreateController();
            Card card = MakeWeakCard("loadout_tiered_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(20, 20, 1000);
            var playerEquipped = new List<string> { "cinder_lash", "vital_spark", "rallying_gale", "sun_lance" };

            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy,
                avatarLevel: 1, unlockedStageIds: null, equippedSpellIds: playerEquipped, enemyTier: AIDifficultyTier.Titan);

            CollectionAssert.AreEqual(new[] { "Cinder Lash", "Vital Spark", "Rallying Gale", "Sun Lance" },
                controller.Spellbook.Select(s => s.Name).ToList(), "The player's own spellbook must be unaffected by enemyTier.");
            CollectionAssert.AreEqual(AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Titan).Select(s => s.Name).ToList(),
                controller.EnemySpellbook.Select(s => s.Name).ToList(),
                "With enemyTier supplied, the enemy spellbook must come from AIEnemySpellbookResolver, not the player's equippedSpellIds.");
            CollectionAssert.AreNotEqual(controller.Spellbook.Select(s => s.Name).ToList(), controller.EnemySpellbook.Select(s => s.Name).ToList(),
                "Setup: expected the player's stage/spell-book-only loadout to genuinely differ from the Titan-tier AI loadout.");
        }

        [Test]
        public void StartMatch_WithoutEnemyTier_KeepsTheOldMirroredBehaviour()
        {
            BattleController controller = CreateController();
            Card card = MakeWeakCard("loadout_notier_" + System.Guid.NewGuid().ToString("N"));
            var economy = new BattleController.MatchEconomy(20, 20, 1000);
            var equipped = new List<string> { "firestorm", "mend", "war_cry", "divine_bolt" };

            controller.StartMatch(new List<Card> { card }, new List<Card> { card }, economy, economy,
                avatarLevel: 1, unlockedStageIds: null, equippedSpellIds: equipped); // enemyTier omitted.

            CollectionAssert.AreEqual(controller.Spellbook.Select(s => s.Name).ToList(), controller.EnemySpellbook.Select(s => s.Name).ToList(),
                "Backward compatibility: no enemyTier means the enemy still mirrors the player's own resolved spellbook, exactly as before this change.");
        }
    }
}
