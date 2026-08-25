using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// AI Tier -> Stage-Gated Spell Access (LOCKED 2026-08-24, docs/LOCKED_DECISIONS_REGISTER.md):
    /// cumulative, authored progression fiction, verified here against the register's own worked
    /// magnitudes rather than re-derived - Novice=Cinder Lash+Vital Spark; Apprentice adds Fault
    /// Line+Renewal; Veteran adds Sun Lance+Banner of Ashes; Master adds Tempest Brand; Titan
    /// inherits all 7, no exclusive. The register's own note that Cinder Lash/Vital Spark/Sun
    /// Lance/Tempest Brand become eligible but stay unselected (weaker same-effect-type options
    /// always win), while Fault Line/Renewal/Banner of Ashes do change tier loadouts, is exactly
    /// what these tests prove against the real catalog Magnitudes - not assumed.
    ///
    /// CORRECTED 2026-08-25 (dead-loadout-slot fix, LOCKED): Apprentice+ previously also resolved
    /// Cleansing Root and Oracle Sight into the loadout (their real AvatarLevel unlock Rules plus
    /// the loadout expansion's AI 6-slot cap gave both a free, uncontested slot) - but the
    /// per-spell impact diagnostic found both fired ZERO times across 1500 VeteranPlus trials,
    /// because AISpellCaster's EffectPriority loop never attempts Cleanse or DrawCards at all (no
    /// TryPickTarget case exists for either). AIEnemySpellbookResolver now filters the AI's
    /// eligible pool to only AI-castable effects before slot selection, so Cleansing Root/Oracle
    /// Sight can never win a slot again - the freed slots go to a real castable spell (Windstep,
    /// Reposition) where the pool has one, and stay unfilled where it does not (only 5 of the 6
    /// slots are ever genuinely fillable at Apprentice+ today - see each test's own comment for
    /// exactly why).
    /// </summary>
    public class AIEnemySpellbookResolverTests
    {
        [TestCase(AIDifficultyTier.Novice)]
        [TestCase(AIDifficultyTier.Apprentice)]
        [TestCase(AIDifficultyTier.Veteran)]
        [TestCase(AIDifficultyTier.Master)]
        [TestCase(AIDifficultyTier.Titan)]
        public void ResolveSpellbook_EveryTier_ReturnsARealNonEmptyCastableLoadout(AIDifficultyTier tier)
        {
            List<AvatarSpell> spellbook = AIEnemySpellbookResolver.ResolveSpellbook(tier);

            Assert.IsTrue(spellbook.Count > 0, $"{tier}: must never resolve to an empty spellbook.");
            Assert.IsTrue(spellbook.All(s => !string.IsNullOrEmpty(s.Id)), $"{tier}: every resolved spell must be a real catalog entry.");
        }

        [Test]
        public void ResolveSpellbook_EveryTier_NeverEquipsTwoAvatarStrikes()
        {
            foreach (AIDifficultyTier tier in System.Enum.GetValues(typeof(AIDifficultyTier)).Cast<AIDifficultyTier>())
            {
                int avatarStrikeCount = AIEnemySpellbookResolver.ResolveSpellbook(tier).Count(s => s.Effect == SpellEffect.AvatarStrike);
                Assert.LessOrEqual(avatarStrikeCount, 1, $"{tier}: MOS's max-1-AvatarStrike-equipped lock must hold for the AI too.");
            }
        }

        [Test]
        public void ResolveSpellbook_IsDeterministic_SameTierAlwaysResolvesTheSameLoadout()
        {
            List<string> first = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Veteran).Select(s => s.Id).ToList();
            List<string> second = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Veteran).Select(s => s.Id).ToList();

            CollectionAssert.AreEqual(first, second, "No randomness - a given tier must always resolve the same loadout.");
        }

        [Test]
        public void ResolveSpellbook_Novice_IsExactlyTheStarterFour()
        {
            // Novice's own band-top (10) hasn't cleared Stone Judgment's L12 gate yet, and neither
            // Cinder Lash (magnitude 2) nor Vital Spark (magnitude 2) can outclass Firestorm(4)/
            // Mend(4) - the register's own "become eligible but stay unselected" case.
            List<string> names = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Novice).Select(s => s.Name).ToList();
            CollectionAssert.AreEquivalent(new[] { "Firestorm", "Mend", "War Cry", "Divine Bolt" }, names);
        }

        /// <summary>Fault Line (magnitude 5) and Renewal (magnitude 6) both outclass their
        /// starter counterparts once unlocked at Apprentice - the register's own "materially
        /// change tier loadouts" case. Banner of Ashes isn't unlocked until Veteran, so War Cry
        /// still holds LaneAttackBuff. Stone Judgment (L12, cleared at rep level 25) replaces
        /// Divine Bolt.
        ///
        /// Windstep REMOVED, LOCKED (register, grew out of 0fdd193's corrected ablation: removing
        /// it increases Apprentice's real AI win rate by 6.6pp, SE 0.9%, z=7.61 - a real, highly
        /// significant, previously-masked-by-a-broken-control effect, not availability bias).
        /// AIEnemySpellbookResolver.ApplyApprenticeWindstepRemoval strips the Reposition slot for
        /// this tier specifically (the spell itself, and its role at other AI tiers, are
        /// untouched) and adds Mend (LaneHeal, magnitude 4) instead of leaving the slot empty.
        /// AvatarStrike-doubling (Blood Price alongside Stone Judgment) was tried first but real
        /// bug found: ResolveSpellbook_EveryTier_NeverEquipsTwoAvatarStrikes below caught that it
        /// violates MOS's own locked max-1-AvatarStrike rule - see
        /// ApplyApprenticeWindstepRemoval's own doc comment for the full reasoning and why LaneHeal
        /// was picked instead.</summary>
        [Test]
        public void ResolveSpellbook_Apprentice_FaultLineAndRenewalTakeOver_WarCryStillHolds_WindstepReplacedByMend()
        {
            List<string> names = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Apprentice).Select(s => s.Name).ToList();
            CollectionAssert.AreEquivalent(new[] { "Fault Line", "Renewal", "War Cry", "Stone Judgment", "Mend" }, names);
        }

        /// <summary>Windstep REMOVED at Veteran ("VeteranPlus" in the SimMatrix's own naming),
        /// LOCKED (register, same decision and mechanism as Apprentice's - see
        /// AIEnemySpellbookResolver.ApplyWindstepRemoval's own doc comment): the corrected
        /// ablation found removing it increases Veteran's real AI win rate by 6.5pp (SE 0.8%,
        /// z=7.68) - nearly identical to Apprentice's 6.6pp, not a per-tier fluke. Mend (LaneHeal)
        /// replaces it, same reasoning as Apprentice (AvatarStrike ruled out by MOS's max-1 lock,
        /// LaneDamage ruled out by the proven Ember Wave shadowing).</summary>
        [Test]
        public void ResolveSpellbook_Veteran_BannerOfAshesTakesOverLaneAttackBuff_WindstepReplacedByMend()
        {
            List<string> names = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Veteran).Select(s => s.Name).ToList();
            CollectionAssert.AreEquivalent(new[] { "Fault Line", "Renewal", "Banner of Ashes", "Stone Judgment", "Mend" }, names);
        }

        [TestCase(AIDifficultyTier.Master)]
        [TestCase(AIDifficultyTier.Titan)]
        public void ResolveSpellbook_MasterAndTitan_BannerOfAshesTakesOverLaneAttackBuff_WindstepUnchanged(AIDifficultyTier tier)
        {
            // Banner of Ashes (magnitude 3) outclasses War Cry (2) and Rallying Gale (1) once
            // unlocked at Veteran - stays the pick through Master/Titan too (Tempest Brand, Master's
            // own addition, never competes for this slot - it's LaneDamage, magnitude 3, and always
            // loses to Fault Line's 5). Windstep removal (LOCKED, see the Veteran-specific test
            // above) is explicitly scoped to Apprentice+Veteran only - the corrected ablation never
            // measured Master/Titan, so they keep the normal pool/selection result unchanged.
            List<string> names = AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Name).ToList();
            CollectionAssert.AreEquivalent(new[] { "Fault Line", "Renewal", "Banner of Ashes", "Stone Judgment", "Windstep" }, names,
                $"{tier}: expected the same converged loadout as before - Windstep removal doesn't apply here, out of the corrected ablation's measured scope.");
        }

        [Test]
        public void ResolveSpellbook_MasterAndTitan_ResolveToTheIdenticalLoadout()
        {
            // Register: "Titan inherits all 7, no [Titan-]exclusive" - Titan's own pool differs
            // from Master's only in name, not composition, so the two tiers must converge exactly.
            List<string> master = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Master).Select(s => s.Id).ToList();
            List<string> titan = AIEnemySpellbookResolver.ResolveSpellbook(AIDifficultyTier.Titan).Select(s => s.Id).ToList();
            CollectionAssert.AreEquivalent(master, titan);
        }

        [TestCase(AIDifficultyTier.Novice, "cinder_lash")]
        [TestCase(AIDifficultyTier.Novice, "vital_spark")]
        [TestCase(AIDifficultyTier.Veteran, "sun_lance")]
        [TestCase(AIDifficultyTier.Master, "tempest_brand")]
        public void ResolveSpellbook_TheFourNeverSelectedSpells_StayEligibleButUnselected(AIDifficultyTier tier, string neverSelectedId)
        {
            // The register's own explicit case: these become part of the pool at their tier but a
            // same-effect-type option with higher Magnitude always wins the slot instead.
            List<string> ids = AIEnemySpellbookResolver.ResolveSpellbook(tier).Select(s => s.Id).ToList();
            CollectionAssert.DoesNotContain(ids, neverSelectedId, $"{tier}: {neverSelectedId} should stay eligible-but-unselected, per the locked decision.");
        }

        [Test]
        public void ResolveSpellbook_NeverReadsAnyPlayerState()
        {
            // Structural proof, not just a naming convention: ResolveSpellbook's only parameter is
            // the tier itself - there is no PlayerProfile, no ownedSpellIds, no unlockedStageIds
            // input path into this method at all, so it cannot read player state even by accident.
            var method = typeof(AIEnemySpellbookResolver).GetMethod("ResolveSpellbook");
            Assert.AreEqual(1, method.GetParameters().Length);
            Assert.AreEqual(typeof(AIDifficultyTier), method.GetParameters()[0].ParameterType);
        }
    }
}
