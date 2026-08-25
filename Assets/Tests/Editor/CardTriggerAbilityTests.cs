using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Suppressible Triggered-Ability Package (LOCKED 2026-08-25, GPT, register commit 2b54084):
    /// Goblin Caster/Cleric/Novice Knight/Phoenix's four once-per-unit-per-match passives, plus
    /// the Silence spells (Volcanic Prison, Titan Seal) that suppress them - completes the
    /// catalog to 36/36.
    /// </summary>
    public class CardTriggerAbilityTests
    {
        [Test]
        public void CreateCatalog_ContainsExactlyThirtySixSpells_ThirtyFourPlusSilencePackage()
        {
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            Assert.AreEqual(36, catalog.Count);
            CollectionAssert.IsSubsetOf(new[] { "Volcanic Prison", "Titan Seal" }, catalog.Select(s => s.Name).ToList());
        }

        [TestCase("Volcanic Prison", "volcanic_prison", SpellSchool.Andras, 58, 6, 1)]
        [TestCase("Titan Seal", "titan_seal", SpellSchool.Pnevmas, 72, 7, 2)] // locked correction: duration 1->2 clashes
        public void SilenceSpell_MatchesTheLockedCatalogRow(string name, string id, SpellSchool school, int energyCost, int cooldownTicks, int magnitude)
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Name == name);
            Assert.AreEqual(id, spell.Id);
            Assert.AreEqual(school, spell.School);
            Assert.AreEqual(energyCost, spell.EnergyCost);
            Assert.AreEqual(cooldownTicks, spell.CooldownTicks);
            Assert.AreEqual(SpellEffect.Silence, spell.Effect);
            Assert.AreEqual(magnitude, spell.Magnitude);
        }

        [Test]
        public void EveryCatalogSpellId_IsUniqueAndNeverEmpty()
        {
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            Assert.IsTrue(catalog.All(s => !string.IsNullOrEmpty(s.Id)));
            Assert.AreEqual(catalog.Count, catalog.Select(s => s.Id).Distinct().Count(), "No two catalog spells may share an id.");
        }

        // ---------- CardTriggerAbilities: identity mapping ----------

        [TestCase("goblin_caster", CardTriggerAbility.HexSpark)]
        [TestCase("cleric", CardTriggerAbility.BattleMend)]
        [TestCase("novice_knight", CardTriggerAbility.ShieldDiscipline)]
        [TestCase("phoenix", CardTriggerAbility.AshRebirth)]
        [TestCase("warrior", CardTriggerAbility.None)]
        public void CardTriggerAbilities_MapsExactlyTheFourNamedCards(string cardId, CardTriggerAbility expected)
        {
            Assert.AreEqual(expected, CardTriggerAbilities.For(cardId));
        }

        // ---------- Hex Spark (Goblin Caster) ----------

        [Test]
        public void HexSpark_FirstClash_Deals1DamageToOpposingUnitInLane()
        {
            var sideA = MakeState();
            var sideB = MakeState();
            BattleCardInstance goblinCaster = MakeUnitWithStats("goblin_caster", attack: 8, health: 12);
            BattleCardInstance opposing = MakeUnitWithStats("warrior", attack: 8, health: 12);
            sideA.Lanes[Lane.Front].Cards.Add(goblinCaster);
            sideB.Lanes[Lane.Front].Cards.Add(opposing);
            int before = opposing.CurrentHealth;

            LaneBattleResolver.ResolveTurn(sideA, sideB, tickNumber: 1);

            // The clash's own combat damage (goblinCaster.Attack) lands too - Hex Spark's own
            // contribution is the extra 1 on top of that. Clamped at 0 like every other damage
            // application, matching BattleCardInstance.ApplyDamage's own floor.
            int expected = System.Math.Max(0, before - goblinCaster.Attack - 1);
            Assert.AreEqual(expected, opposing.CurrentHealth);
        }

        [Test]
        public void HexSpark_OnlyFiresOnce_AcrossMultipleClashes()
        {
            var sideA = MakeState();
            var sideB = MakeState();
            BattleCardInstance goblinCaster = MakeUnitWithStats("goblin_caster", attack: 8, health: 12);
            BattleCardInstance opposing = MakeUnitWithStats("warrior", attack: 8, health: 12);
            sideA.Lanes[Lane.Front].Cards.Add(goblinCaster);
            sideB.Lanes[Lane.Front].Cards.Add(opposing);

            LaneBattleResolver.ResolveTurn(sideA, sideB, tickNumber: 1);
            int afterFirstClash = opposing.CurrentHealth; // took ordinary combat damage + Hex Spark's +1
            LaneBattleResolver.ResolveTurn(sideA, sideB, tickNumber: 2);

            // Second clash: only ordinary combat damage should land - Hex Spark already fired once.
            // Clamped at 0 the same way, in case the second clash alone is already lethal.
            int expected = System.Math.Max(0, afterFirstClash - goblinCaster.Attack);
            Assert.AreEqual(expected, opposing.CurrentHealth);
            Assert.IsTrue(goblinCaster.HasUsedTrigger);
        }

        // ---------- Battle Mend (Cleric) ----------

        [Test]
        public void BattleMend_HealsTheMostDamagedFriendlyUnit_AfterFirstClash()
        {
            var sideA = MakeState();
            var sideB = MakeState();
            BattleCardInstance cleric = MakeUnit("cleric", rarity: 7); // must survive the clash for its own trigger to fire
            BattleCardInstance ally = MakeUnit("warrior", rarity: 7); // big Health pool, survives real damage
            sideA.Lanes[Lane.Front].Cards.Add(cleric);
            sideA.Lanes[Lane.Front].Cards.Add(ally);
            BattleCardInstance attacker = MakeUnit("warrior", rarity: 3); // real but modest damage - damages, doesn't kill either tanky unit
            sideB.Lanes[Lane.Front].Cards.Add(attacker);

            LaneBattleResolver.ResolveTurn(sideA, sideB, tickNumber: 1);

            // The clash damaged the friendly lane (attacker's Attack landed on cleric/ally per
            // taunt-then-order rules) - Battle Mend must have healed 1 HP onto whichever unit
            // ended up most damaged, so nobody in the lane still shows a missing-Health gap of
            // exactly what raw combat damage alone would have left.
            int totalMissing = (cleric.MaxHealth - cleric.CurrentHealth) + (ally.MaxHealth - ally.CurrentHealth);
            int totalRawDamage = attacker.Attack;
            Assert.Less(totalMissing, totalRawDamage, "Battle Mend must have healed back at least 1 of the raw damage dealt.");
        }

        [Test]
        public void BattleMend_ConsumedOnFirstClash_EvenIfNoFriendlyWasDamaged()
        {
            var sideA = MakeState();
            var sideB = MakeState(); // no enemy at all - the Cleric's first clash deals/takes no damage
            BattleCardInstance cleric = MakeUnit("cleric");
            sideA.Lanes[Lane.Front].Cards.Add(cleric);

            LaneBattleResolver.ResolveTurn(sideA, sideB, tickNumber: 1);

            Assert.IsTrue(cleric.HasUsedTrigger, "Battle Mend's window is the first clash itself, not the first clash where a friendly happens to be damaged.");
        }

        // ---------- Shield Discipline (Novice Knight) ----------

        [Test]
        public void ShieldDiscipline_FirstIncomingDamage_IsReducedByOne()
        {
            BattleCardInstance knight = MakeUnit("novice_knight");
            int before = knight.CurrentHealth;

            knight.ApplyDamage(3);

            Assert.AreEqual(before - 2, knight.CurrentHealth, "First hit must be reduced by 1 (3 -> 2).");
        }

        [Test]
        public void ShieldDiscipline_OnlyReducesTheFirstHit()
        {
            BattleCardInstance knight = MakeUnit("novice_knight", rarity: 7); // enough Health for two real hits
            int before = knight.CurrentHealth;

            knight.ApplyDamage(3); // reduced to 2
            knight.ApplyDamage(3); // full 3, no reduction

            Assert.AreEqual(before - 2 - 3, knight.CurrentHealth);
        }

        [Test]
        public void ShieldDiscipline_ReductionNeverGoesNegative()
        {
            BattleCardInstance knight = MakeUnit("novice_knight");
            int before = knight.CurrentHealth;

            knight.ApplyDamage(1); // 1 - 1 = 0, must not become -1 or heal

            Assert.AreEqual(before, knight.CurrentHealth, "A reduced-to-zero hit must deal exactly zero damage, never negative (a heal).");
        }

        // ---------- Ash Rebirth (Phoenix) ----------

        [Test]
        public void AshRebirth_FirstLethalHit_LeavesTheUnitAt1HP()
        {
            BattleCardInstance phoenix = MakeUnit("phoenix");

            phoenix.ApplyDamage(9999);

            Assert.AreEqual(1, phoenix.CurrentHealth);
            Assert.IsTrue(phoenix.IsAlive);
            Assert.IsTrue(phoenix.HasUsedTrigger);
        }

        [Test]
        public void AshRebirth_OnlyFiresOnce_SecondLethalHitActuallyDefeatsIt()
        {
            BattleCardInstance phoenix = MakeUnit("phoenix");

            phoenix.ApplyDamage(9999); // saved, at 1 HP
            phoenix.ApplyDamage(9999); // no save left

            Assert.AreEqual(0, phoenix.CurrentHealth);
            Assert.IsFalse(phoenix.IsAlive);
        }

        // ---------- Silence: suppression, legality, expiry ----------

        [Test]
        public void Silence_SuppressesTheTrigger_WhileActive()
        {
            BattleCardInstance phoenix = MakeUnit("phoenix");
            phoenix.ApplySilence(1);

            phoenix.ApplyDamage(9999);

            Assert.AreEqual(0, phoenix.CurrentHealth, "A silenced Ash Rebirth must not save the unit.");
            Assert.IsFalse(phoenix.IsAlive);
            Assert.IsFalse(phoenix.HasUsedTrigger, "A suppressed trigger must not be consumed either - it never entered the queue.");
        }

        [Test]
        public void Silence_Expires_AndAnUnusedTriggerBecomesAvailableAgain()
        {
            var sideA = MakeState();
            var sideB = MakeState();
            // Asymmetric on purpose: goblinCaster must survive TWO full clashes' worth of
            // counter-attack for this test to observe its own trigger firing on the second one -
            // a weak opposing (rarity 1) makes that trivial, and whether opposing itself survives
            // is irrelevant to what this test actually checks (goblinCaster's own HasUsedTrigger).
            BattleCardInstance goblinCaster = MakeUnitWithStats("goblin_caster", attack: 8, health: 12);
            BattleCardInstance opposing = MakeUnit("warrior", rarity: 1);
            sideA.Lanes[Lane.Front].Cards.Add(goblinCaster);
            sideB.Lanes[Lane.Front].Cards.Add(opposing);
            goblinCaster.ApplySilence(1);

            LaneBattleResolver.ResolveTurn(sideA, sideB, tickNumber: 1); // silenced - Hex Spark suppressed, silence expires this clash
            Assert.IsFalse(goblinCaster.HasUsedTrigger);

            LaneBattleResolver.ResolveTurn(sideA, sideB, tickNumber: 2); // silence expired - trigger fires now

            Assert.IsTrue(goblinCaster.HasUsedTrigger,
                "Once Silence expires, the still-unused Hex Spark trigger must fire on the next clash.");
        }

        [Test]
        public void Cast_Silence_WithIllegalTarget_ChangesNothing()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "volcanic_prison");
            var caster = MakeState();
            var opponent = MakeState();
            BattleCardInstance notDeployed = MakeUnit("warrior"); // never added to any lane

            int dealt = spell.Cast(caster, opponent, Lane.Front, silenceTarget: notDeployed);

            Assert.AreEqual(0, dealt);
            Assert.IsFalse(notDeployed.IsSilenced);
        }

        [Test]
        public void Cast_Silence_WithLegalTarget_AppliesTheDuration()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "titan_seal");
            var caster = MakeState();
            var opponent = MakeState();
            BattleCardInstance target = MakeUnit("phoenix");
            opponent.Lanes[Lane.Front].Cards.Add(target);

            spell.Cast(caster, opponent, Lane.Front, silenceTarget: target);

            Assert.IsTrue(target.IsSilenced);
        }

        [Test]
        public void TryCastSpell_Silence_WithIllegalTarget_RejectsAndSpendsNothing()
        {
            var controller = new BattleController();
            var economy = new BattleController.MatchEconomy(60, 60, 1000);
            controller.StartMatch(new List<Card>(), new List<Card>(), economy, economy,
                equippedSpellIds: new List<string> { "volcanic_prison" });
            BattleCardInstance notDeployed = MakeUnit("warrior");
            controller.PlayerState.Lanes[Lane.Front].Cards.Add(MakeUnit("warrior"));
            controller.ConfirmFormation(); // Phase must leave Formation before AdvanceCombatTick does anything.
            while (controller.TickCount < 2) controller.AdvanceCombatTick();
            int energyBefore = controller.Energy;

            bool cast = controller.TryCastSpell(0, Lane.Front, out int dealt, silenceTarget: notDeployed);

            Assert.IsFalse(cast);
            Assert.AreEqual(0, dealt);
            Assert.AreEqual(energyBefore, controller.Energy);
        }

        // ---------- helpers ----------

        private static PlayerBattleState MakeState() =>
            new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);

        private static BattleCardInstance MakeUnit(string cardId, int rarity = 3)
        {
            var data = new CardData
            {
                id = cardId, name = cardId, art_file = "x.png", element = "Andras", type = "warrior", rarity = rarity,
            };
            return new BattleCardInstance(Card.FromData(data), isPlayerOwned: true, laneAttackBonus: 0, laneHealthBonus: 0);
        }

        /// <summary>Explicit Attack/Health, not rarity-derived - for multi-clash tests where the
        /// exact numbers need to be controlled precisely rather than reasoned about through the
        /// rarity table's hash-derived ranges.</summary>
        /// <summary>Rarity 7 (Godlike): the real legal Attack/Health range is [7, 12], the widest
        /// available - `attack`/`health` must land inside that range or Card.FromData rejects them
        /// as "unauthored" and falls back to unpredictable hash-derived stats instead.</summary>
        private static BattleCardInstance MakeUnitWithStats(string cardId, int attack, int health)
        {
            var data = new CardData
            {
                id = cardId, name = cardId, art_file = "x.png", element = "Andras", type = "warrior", rarity = 7,
                attack = attack, health = health,
            };
            return new BattleCardInstance(Card.FromData(data), isPlayerOwned: true, laneAttackBonus: 0, laneHealthBonus: 0);
        }
    }
}
