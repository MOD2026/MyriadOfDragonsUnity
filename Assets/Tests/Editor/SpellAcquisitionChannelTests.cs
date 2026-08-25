using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Acquisition channels for all remaining spells (LOCKED 2026-08-25, GPT): the Stage/
    /// AvatarLevel half of the spec (SpellBookGrantTests.cs covers the SpellBookGrant half) -
    /// Ember Guard/Earthward/Gale Break's real first-clear Stage rules and Titan Seal's Avatar L30
    /// rule, resolved through SpellUnlockResolver the same way every other Stage/AvatarLevel spell
    /// already is.
    /// </summary>
    public class SpellAcquisitionChannelTests
    {
        [TestCase("Ember Guard", "4-15")]
        [TestCase("Earthward", "5-15")]
        [TestCase("Gale Break", "7-15")]
        public void StageSpell_UnlocksOnlyAfterItsRealFirstClearStage(string spellName, string requiredStageId)
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Name == spellName);

            Assert.IsFalse(SpellUnlockResolver.IsUnlocked(spell, avatarLevel: 99, unlockedStageIds: new List<string>()),
                $"{spellName} must not unlock without its real stage clear, no matter how high Avatar level is.");
            Assert.IsTrue(SpellUnlockResolver.IsUnlocked(spell, avatarLevel: 1, unlockedStageIds: new List<string> { requiredStageId }),
                $"{spellName} must unlock once its real first-clear stage ({requiredStageId}) is cleared.");
        }

        [Test]
        public void TitanSeal_UnlocksAtAvatarLevelThirty_NotBefore()
        {
            AvatarSpell titanSeal = AvatarSpell.CreateCatalog().Single(s => s.Id == "titan_seal");

            Assert.IsFalse(SpellUnlockResolver.IsUnlocked(titanSeal, avatarLevel: 29, unlockedStageIds: null));
            Assert.IsTrue(SpellUnlockResolver.IsUnlocked(titanSeal, avatarLevel: 30, unlockedStageIds: null));
        }

        [TestCase("Scorched Sky")]
        [TestCase("Volcanic Prison")]
        [TestCase("Leyline Draw")]
        [TestCase("Thunder Decree")]
        public void SpellBookGrantKindSpells_AreNeverUnlockedByThisResolver_OnlyByRealOwnership(string spellName)
        {
            // Matches the established pattern for every other SpellBookGrant-kind spell (Sun
            // Lance, Magma Rend, etc.) - this resolver's own inputs (Avatar level, stage ids)
            // cannot see spell-book ownership, so it must never claim one of these is unlocked,
            // no matter how high the level or how many stages are cleared.
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Name == spellName);

            Assert.IsFalse(SpellUnlockResolver.IsUnlocked(spell, avatarLevel: 99,
                unlockedStageIds: new List<string> { "4-15", "5-15", "7-15", "7-30", "8-30", "9-30", "10-30" }));
        }

        [Test]
        public void ResolveUnlockedSpells_IncludesEmberGuardEarthwardGaleBreakTitanSeal_OnceTheirGatesAreMet()
        {
            var unlockedStageIds = new List<string> { "4-15", "5-15", "7-15" };

            List<string> unlockedNames = SpellUnlockResolver.ResolveUnlockedSpells(avatarLevel: 30, unlockedStageIds)
                .Select(s => s.Name).ToList();

            CollectionAssert.Contains(unlockedNames, "Ember Guard");
            CollectionAssert.Contains(unlockedNames, "Earthward");
            CollectionAssert.Contains(unlockedNames, "Gale Break");
            CollectionAssert.Contains(unlockedNames, "Titan Seal");
        }

        // ---------- Final 4 spell acquisition channels (LOCKED 2026-08-25, GPT) ----------

        [TestCase("Ashfall", "4-30")]
        [TestCase("Stormchain", "6-30")]
        public void FinalStageSpell_UnlocksOnlyAfterItsRealFirstClearStage(string spellName, string requiredStageId)
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Name == spellName);

            Assert.IsFalse(SpellUnlockResolver.IsUnlocked(spell, avatarLevel: 99, unlockedStageIds: new List<string>()),
                $"{spellName} must not unlock without its real stage clear, no matter how high Avatar level is.");
            Assert.IsTrue(SpellUnlockResolver.IsUnlocked(spell, avatarLevel: 1, unlockedStageIds: new List<string> { requiredStageId }),
                $"{spellName} must unlock once its real first-clear stage ({requiredStageId}) is cleared.");
        }

        [TestCase("Windstep", 18)]
        [TestCase("Seismic Swap", 24)]
        public void FinalAvatarLevelSpell_UnlocksAtItsRealLevel_NotBefore(string spellName, int requiredLevel)
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Name == spellName);

            Assert.IsFalse(SpellUnlockResolver.IsUnlocked(spell, avatarLevel: requiredLevel - 1, unlockedStageIds: null));
            Assert.IsTrue(SpellUnlockResolver.IsUnlocked(spell, avatarLevel: requiredLevel, unlockedStageIds: null));
        }

        [Test]
        public void EveryCatalogSpellExceptAegisReturn_IsReachable_ByARuleOrARealSpellBookGrant()
        {
            // Final 4 spell acquisition channels (LOCKED 2026-08-25): "catalog genuinely 36/36
            // reachable now" - checked directly rather than assumed, same discipline as the
            // earlier overclaim this session caught (register corrected from "36/36" to "32/36"
            // acquisition channels before this task existed). Aegis Return ("Event book later")
            // remains the one genuine, permanent exception - no real acquisition channel exists
            // for it anywhere in the game yet, not a gap introduced or left by this commit.
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            Assert.AreEqual(36, catalog.Count, "Setup: expected the full catalog.");

            HashSet<string> spellBookGrantedIds = new HashSet<string>
            {
                "sun_lance", "tempest_brand", "magma_rend", "stonewall", "infernal_mark", "veil_of_zeus",
                "grave_mend", "celestial_verdict", "scorched_sky", "volcanic_prison", "leyline_draw", "thunder_decree",
            };

            List<string> unreachable = catalog
                .Where(s => s.Id != "aegis_return")
                .Where(s => !spellBookGrantedIds.Contains(s.Id))
                .Where(s => !HasAnyStageOrLevelRuleReachableEventually(s))
                .Select(s => s.Name)
                .ToList();

            CollectionAssert.IsEmpty(unreachable,
                "Every catalog spell except Aegis Return must have either a real SpellUnlockResolver Rule or a real SpellBookGrant entry.");
        }

        /// <summary>True if SpellUnlockResolver would EVER return true for this spell at some
        /// high enough level with every stage cleared - i.e. it has a real Stage/AvatarLevel/
        /// Starter Rule, not just "no Rule = never unlocks."</summary>
        private static bool HasAnyStageOrLevelRuleReachableEventually(AvatarSpell spell)
        {
            var everyKnownStageId = new List<string>
            {
                "1-1", "1-2", "1-6", "2-4", "2-8", "3-3", "4-15", "4-30", "5-15", "6-30", "7-15",
            };
            return SpellUnlockResolver.IsUnlocked(spell, avatarLevel: 999, unlockedStageIds: everyKnownStageId);
        }
    }
}
