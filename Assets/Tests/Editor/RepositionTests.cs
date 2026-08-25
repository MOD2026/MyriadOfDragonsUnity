using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Windstep/Seismic Swap (GPT spec, LOCKED 2026-08-25) - the targeting-model gap flagged when
    /// Wave 4 first landed without Reposition. Real coverage for RepositionRules' legality (the
    /// evaluator both the player UI and the AI must share, per the spec's own "no divergence"
    /// requirement) and AIRepositionSelector's priority-order selection, not just catalog data.
    /// </summary>
    public class RepositionTests
    {
        [Test]
        public void CreateCatalog_ContainsWindstepAndSeismicSwap()
        {
            // Not an exact-count assertion any more - the Silence package (CardTriggerAbilityTests)
            // completed the catalog to 36/36 on top of these, and that test file now owns the
            // real total.
            List<AvatarSpell> catalog = AvatarSpell.CreateCatalog();
            CollectionAssert.IsSubsetOf(new[] { "Windstep", "Seismic Swap" }, catalog.Select(s => s.Name).ToList());
        }

        [TestCase("Windstep", "windstep", SpellSchool.Pnevmas, 26, 3)]
        [TestCase("Seismic Swap", "seismic_swap", SpellSchool.Ktini, 48, 5)]
        public void RepositionSpell_MatchesTheLockedCatalogRow(string name, string id, SpellSchool school, int energyCost, int cooldownTicks)
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Name == name);
            Assert.AreEqual(id, spell.Id);
            Assert.AreEqual(school, spell.School);
            Assert.AreEqual(energyCost, spell.EnergyCost);
            Assert.AreEqual(cooldownTicks, spell.CooldownTicks);
            Assert.AreEqual(SpellEffect.Reposition, spell.Effect);
        }

        // ---------- RepositionRules: adjacency ----------

        [Test]
        public void AdjacentLanes_FrontAndBack_HaveExactlyOneNeighbor_NoWrapAround()
        {
            CollectionAssert.AreEquivalent(new[] { Lane.Middle }, RepositionRules.AdjacentLanes(Lane.Front).ToList());
            CollectionAssert.AreEquivalent(new[] { Lane.Middle }, RepositionRules.AdjacentLanes(Lane.Back).ToList());
            CollectionAssert.AreEquivalent(new[] { Lane.Front, Lane.Back }, RepositionRules.AdjacentLanes(Lane.Middle).ToList());
        }

        // ---------- RepositionRules: Windstep legality ----------

        [Test]
        public void LegalWindstepDestinations_ExcludesNonAdjacentLane_AndFullLane()
        {
            var side = MakeState();
            BattleCardInstance mover = MakeUnit(rarity: 1); // SlotWeight 1
            side.Lanes[Lane.Front].Cards.Add(mover);
            FillLane(side.Lanes[Lane.Back], slots: 3); // Back is full - would be illegal anyway, but also not adjacent to Front
            FillLane(side.Lanes[Lane.Middle], slots: 2); // 1 free slot left

            List<Lane> destinations = RepositionRules.LegalWindstepDestinations(side, mover);

            CollectionAssert.AreEquivalent(new[] { Lane.Middle }, destinations);
        }

        [Test]
        public void LegalWindstepDestinations_ExcludesDestinationWithoutRoomForSlotWeight()
        {
            var side = MakeState();
            BattleCardInstance mover = MakeUnit(rarity: 7); // SlotWeight 2
            side.Lanes[Lane.Front].Cards.Add(mover);
            FillLane(side.Lanes[Lane.Middle], slots: 2); // only 1 free slot - not enough for a 2-slot unit

            List<Lane> destinations = RepositionRules.LegalWindstepDestinations(side, mover);

            CollectionAssert.IsEmpty(destinations);
        }

        [Test]
        public void LegalWindstepUnits_ExcludesDefeatedAndUndeployedUnits()
        {
            var side = MakeState();
            BattleCardInstance alive = MakeUnit(rarity: 1);
            BattleCardInstance dead = MakeUnit(rarity: 1);
            dead.ApplyDamage(999);
            BattleCardInstance neverDeployed = MakeUnit(rarity: 1);
            side.Lanes[Lane.Front].Cards.Add(alive);
            side.Lanes[Lane.Front].Cards.Add(dead);

            List<BattleCardInstance> legal = RepositionRules.LegalWindstepUnits(side);

            Assert.Contains(alive, legal);
            Assert.IsFalse(legal.Contains(dead));
            Assert.IsFalse(legal.Contains(neverDeployed));
        }

        [Test]
        public void ExecuteWindstep_MovesUnitBetweenLanes_AndReappliesLaneBonus()
        {
            var side = MakeState();
            BattleCardInstance unit = MakeUnit(rarity: 1);
            side.Lanes[Lane.Front].Cards.Add(unit); // Front grants +1 Attack on deploy (see MakeUnit)
            int attackBeforeMove = unit.Attack;

            RepositionRules.ExecuteWindstep(side, unit, Lane.Middle);

            Assert.IsFalse(side.Lanes[Lane.Front].Cards.Contains(unit));
            Assert.IsTrue(side.Lanes[Lane.Middle].Cards.Contains(unit));
            Assert.AreEqual(attackBeforeMove - BattleController.FrontLaneAttackBonus, unit.Attack,
                "Moving out of Front must remove the Front Attack bonus it was carrying.");
            Assert.AreEqual(unit.Definition.Health + BattleController.MiddleLaneHealthBonus, unit.MaxHealth,
                "Moving into Middle must grant the Middle Health bonus.");
        }

        // ---------- RepositionRules: Seismic Swap legality ----------

        [Test]
        public void IsLegalSeismicSwap_RejectsSameLanePair()
        {
            var side = MakeState();
            BattleCardInstance a = MakeUnit(rarity: 1);
            BattleCardInstance b = MakeUnit(rarity: 1);
            side.Lanes[Lane.Front].Cards.Add(a);
            side.Lanes[Lane.Front].Cards.Add(b);

            Assert.IsFalse(RepositionRules.IsLegalSeismicSwap(side, a, b));
        }

        [Test]
        public void IsLegalSeismicSwap_RejectsWhenSwapWouldOverflowCapacity()
        {
            // Front: a 2-slot unit (A) alone (1 free slot). Back: a 2-slot unit (B) plus a 1-slot
            // unit (2/3 used, 1 free). Swapping A<->B: Front loses A(-2) gains B(+2) = fine (2/3).
            // Back loses B(-2) gains A(+2) = also fine... so pick weights that actually overflow:
            // Front has A(1-slot) + a 2-slot other (3/3 full, 0 free). Back has B(1-slot) alone (1/3).
            // Swap A(1-slot) for B(1-slot): both fine. Instead: make A a 2-slot unit so Front is
            // full (3/3) with a 1-slot partner, and B a 1-slot unit in an otherwise-full Back (3/3,
            // a 2-slot other + B). Front loses A(-2) gains B(+1) = 1+1=2/3, fine. Back loses B(-1)
            // gains A(+2) = 2+2=4/3 - overflow.
            var side = MakeState();
            BattleCardInstance a = MakeUnit(rarity: 7); // 2-slot
            BattleCardInstance frontOther = MakeUnit(rarity: 1); // 1-slot
            side.Lanes[Lane.Front].Cards.Add(a);
            side.Lanes[Lane.Front].Cards.Add(frontOther); // Front: 3/3 full

            BattleCardInstance b = MakeUnit(rarity: 1); // 1-slot
            BattleCardInstance backOther = MakeUnit(rarity: 7); // 2-slot
            side.Lanes[Back(side)].Cards.Add(b);
            side.Lanes[Back(side)].Cards.Add(backOther); // Back: 3/3 full

            Assert.IsFalse(RepositionRules.IsLegalSeismicSwap(side, a, b),
                "A(2-slot) taking B's spot in an already-full Back must be rejected as over-capacity.");
        }

        [Test]
        public void ExecuteSeismicSwap_ExchangesLanes_AndReappliesBothUnitsLaneBonuses()
        {
            var side = MakeState();
            BattleCardInstance front = DeployUnit(side, Lane.Front, rarity: 1);
            BattleCardInstance middle = DeployUnit(side, Lane.Middle, rarity: 1);
            int frontAttackBefore = front.Attack;
            int middleHealthBefore = middle.MaxHealth;

            RepositionRules.ExecuteSeismicSwap(side, front, middle);

            Assert.IsTrue(side.Lanes[Lane.Middle].Cards.Contains(front));
            Assert.IsTrue(side.Lanes[Lane.Front].Cards.Contains(middle));
            Assert.AreEqual(frontAttackBefore - BattleController.FrontLaneAttackBonus, front.Attack,
                "The unit that left Front must lose its Front bonus.");
            Assert.AreEqual(middleHealthBefore - BattleController.MiddleLaneHealthBonus, middle.MaxHealth,
                "The unit that left Middle must lose its Middle bonus.");
            Assert.AreEqual(middle.Definition.Attack + BattleController.FrontLaneAttackBonus, middle.Attack,
                "The unit that entered Front must gain the Front bonus.");
        }

        // ---------- Cast() wiring: illegal target is a silent no-op, same contract as every other effect ----------

        [Test]
        public void Windstep_Cast_WithIllegalTarget_ChangesNothing()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "windstep");
            var caster = MakeState();
            var opponent = MakeState();
            BattleCardInstance unit = MakeUnit(rarity: 1);
            caster.Lanes[Lane.Front].Cards.Add(unit);

            // Back is not adjacent to Front - illegal.
            int dealt = spell.Cast(caster, opponent, Lane.Front, RepositionTarget.ForWindstep(unit, Lane.Back));

            Assert.AreEqual(0, dealt);
            Assert.IsTrue(caster.Lanes[Lane.Front].Cards.Contains(unit));
        }

        [Test]
        public void Windstep_Cast_WithLegalTarget_MovesTheUnit()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "windstep");
            var caster = MakeState();
            var opponent = MakeState();
            BattleCardInstance unit = MakeUnit(rarity: 1);
            caster.Lanes[Lane.Front].Cards.Add(unit);

            spell.Cast(caster, opponent, Lane.Front, RepositionTarget.ForWindstep(unit, Lane.Middle));

            Assert.IsTrue(caster.Lanes[Lane.Middle].Cards.Contains(unit));
        }

        [Test]
        public void SeismicSwap_Cast_WithLegalTarget_ExchangesTheUnits()
        {
            AvatarSpell spell = AvatarSpell.CreateCatalog().Single(s => s.Id == "seismic_swap");
            var caster = MakeState();
            var opponent = MakeState();
            BattleCardInstance a = MakeUnit(rarity: 1);
            BattleCardInstance b = MakeUnit(rarity: 1);
            caster.Lanes[Lane.Front].Cards.Add(a);
            caster.Lanes[Lane.Back].Cards.Add(b);

            spell.Cast(caster, opponent, Lane.Front, RepositionTarget.ForSeismicSwap(a, b));

            Assert.IsTrue(caster.Lanes[Lane.Back].Cards.Contains(a));
            Assert.IsTrue(caster.Lanes[Lane.Front].Cards.Contains(b));
        }

        // ---------- BattleController: illegal Reposition target never spends Energy/cooldown ----------

        [Test]
        public void TryCastSpell_Windstep_WithIllegalTarget_RejectsAndSpendsNothing()
        {
            BattleController controller = MakeStartedController(out BattleCardInstance unit);
            int spellIndex = controller.Spellbook.ToList().FindIndex(s => s.Id == "windstep");
            int energyBefore = controller.Energy;

            bool cast = controller.TryCastSpell(spellIndex, Lane.Front, out int dealt,
                RepositionTarget.ForWindstep(unit, Lane.Back)); // not adjacent to Front - illegal

            Assert.IsFalse(cast);
            Assert.AreEqual(0, dealt);
            Assert.AreEqual(energyBefore, controller.Energy);
        }

        // ---------- AIRepositionSelector: priority order ----------

        [Test]
        public void TryPickWindstep_PrefersMovingAUnitThatIsAboutToDie_ToASaferLane()
        {
            var aiSide = MakeState();
            var opposingSide = MakeState();

            // Front: AI's fragile unit (1 HP), facing lethal incoming Attack from opposing Front.
            BattleCardInstance fragile = MakeUnit(rarity: 1);
            fragile.ApplyDamage(fragile.CurrentHealth - 1); // leave exactly 1 HP
            aiSide.Lanes[Lane.Front].Cards.Add(fragile);
            AddAttacker(opposingSide, Lane.Front, attack: 5);

            // Middle: safe (no incoming Attack) - the correct destination.
            // Back: also has a unit, but is not adjacent to Front, so irrelevant to this pick.
            BattleCardInstance other = MakeUnit(rarity: 1);
            aiSide.Lanes[Lane.Back].Cards.Add(other);

            bool found = AIRepositionSelector.TryPickWindstep(aiSide, opposingSide, out RepositionTarget target);

            Assert.IsTrue(found);
            Assert.AreSame(fragile, target.UnitA);
            Assert.AreEqual(Lane.Middle, target.DestinationLaneForA);
        }

        [Test]
        public void TryPickWindstep_NoLegalMoves_ReturnsFalse()
        {
            var aiSide = MakeState(); // every lane empty - no units to move at all
            var opposingSide = MakeState();

            bool found = AIRepositionSelector.TryPickWindstep(aiSide, opposingSide, out RepositionTarget target);

            Assert.IsFalse(found);
            Assert.IsNull(target);
        }

        [Test]
        public void TryPickSeismicSwap_PrefersThePairThatSavesADoomedUnit()
        {
            var aiSide = MakeState();
            var opposingSide = MakeState();

            // Front: fragile AI unit (1 HP) facing lethal incoming Attack.
            BattleCardInstance fragile = MakeUnit(rarity: 1);
            fragile.ApplyDamage(fragile.CurrentHealth - 1);
            aiSide.Lanes[Lane.Front].Cards.Add(fragile);
            AddAttacker(opposingSide, Lane.Front, attack: 5);

            // Back: healthy AI unit, opposing Back has no living attacker - a safe lane to swap into.
            BattleCardInstance healthy = MakeUnit(rarity: 1);
            aiSide.Lanes[Lane.Back].Cards.Add(healthy);

            bool found = AIRepositionSelector.TryPickSeismicSwap(aiSide, opposingSide, out RepositionTarget target);

            Assert.IsTrue(found);
            var pair = new[] { target.UnitA, target.UnitB };
            CollectionAssert.Contains(pair, fragile);
            CollectionAssert.Contains(pair, healthy);
        }

        [Test]
        public void TryPickSeismicSwap_NoLegalPairs_ReturnsFalse()
        {
            var aiSide = MakeState();
            BattleCardInstance onlyUnit = MakeUnit(rarity: 1);
            aiSide.Lanes[Lane.Front].Cards.Add(onlyUnit); // a single unit has no partner to swap with
            var opposingSide = MakeState();

            bool found = AIRepositionSelector.TryPickSeismicSwap(aiSide, opposingSide, out RepositionTarget target);

            Assert.IsFalse(found);
            Assert.IsNull(target);
        }

        // ---------- RepositionSelectionState: the player-facing tap-to-target flow ----------

        [Test]
        public void Windstep_HappyPath_UnitThenDestination_BuildsARealTarget()
        {
            var side = MakeState();
            BattleCardInstance unit = DeployUnit(side, Lane.Front, rarity: 1);
            var state = new RepositionSelectionState();

            state.BeginWindstep(spellIndex: 3);
            Assert.AreEqual(RepositionSelectionState.Step.WindstepSelectingUnit, state.Current);
            CollectionAssert.Contains(state.LegalUnitCandidates(side), unit);

            Assert.IsTrue(state.TrySelectUnit(side, unit));
            Assert.AreEqual(RepositionSelectionState.Step.WindstepSelectingDestination, state.Current);
            CollectionAssert.AreEquivalent(new[] { Lane.Middle }, state.LegalDestinationLanes(side));

            Assert.IsTrue(state.TrySelectDestination(side, Lane.Middle));
            RepositionTarget target = state.TryBuildWindstepTarget(side, Lane.Middle);

            Assert.IsNotNull(target);
            Assert.AreSame(unit, target.UnitA);
            Assert.AreEqual(Lane.Middle, target.DestinationLaneForA);
            Assert.IsNull(target.UnitB);
        }

        [Test]
        public void Windstep_TapOnIllegalUnit_IsRejected_StateUnchanged()
        {
            var side = MakeState();
            BattleCardInstance unit = DeployUnit(side, Lane.Front, rarity: 1);
            BattleCardInstance notOnThisSide = MakeUnit(rarity: 1); // never deployed anywhere
            var state = new RepositionSelectionState();
            state.BeginWindstep(spellIndex: 3);

            bool accepted = state.TrySelectUnit(side, notOnThisSide);

            Assert.IsFalse(accepted);
            Assert.AreEqual(RepositionSelectionState.Step.WindstepSelectingUnit, state.Current,
                "A rejected tap must not advance the flow.");
            Assert.IsNull(state.FirstUnit);
        }

        [Test]
        public void Windstep_TapOnIllegalDestination_IsRejected_DoesNotBuildATarget()
        {
            var side = MakeState();
            BattleCardInstance unit = DeployUnit(side, Lane.Front, rarity: 1);
            var state = new RepositionSelectionState();
            state.BeginWindstep(spellIndex: 3);
            state.TrySelectUnit(side, unit);

            // Back is not adjacent to Front - illegal.
            bool accepted = state.TrySelectDestination(side, Lane.Back);
            RepositionTarget target = state.TryBuildWindstepTarget(side, Lane.Back);

            Assert.IsFalse(accepted);
            Assert.IsNull(target, "TryBuildWindstepTarget must not hand back an illegal target just because both taps happened.");
        }

        [Test]
        public void SeismicSwap_HappyPath_TwoUnits_BuildsARealTarget()
        {
            var side = MakeState();
            BattleCardInstance front = DeployUnit(side, Lane.Front, rarity: 1);
            BattleCardInstance back = DeployUnit(side, Lane.Back, rarity: 1);
            var state = new RepositionSelectionState();

            state.BeginSeismicSwap(spellIndex: 4);
            CollectionAssert.AreEquivalent(new[] { front, back }, state.LegalUnitCandidates(side));

            Assert.IsTrue(state.TrySelectUnit(side, front));
            Assert.AreEqual(RepositionSelectionState.Step.SeismicSwapSelectingSecondUnit, state.Current);
            CollectionAssert.AreEquivalent(new[] { back }, state.LegalUnitCandidates(side));

            Assert.IsTrue(state.TrySelectUnit(side, back));
            RepositionTarget target = state.TryBuildSeismicSwapTarget(side, back);

            Assert.IsNotNull(target);
            Assert.AreSame(front, target.UnitA);
            Assert.AreSame(back, target.UnitB);
        }

        [Test]
        public void SeismicSwap_SecondTapOnSameUnitAgain_IsRejected()
        {
            var side = MakeState();
            BattleCardInstance front = DeployUnit(side, Lane.Front, rarity: 1);
            DeployUnit(side, Lane.Back, rarity: 1); // a real partner must exist or nothing here is legal at all
            var state = new RepositionSelectionState();
            state.BeginSeismicSwap(spellIndex: 4);
            state.TrySelectUnit(side, front);

            bool accepted = state.TrySelectUnit(side, front); // tapping the same unit again

            Assert.IsFalse(accepted);
            Assert.AreEqual(RepositionSelectionState.Step.SeismicSwapSelectingSecondUnit, state.Current);
        }

        [Test]
        public void Cancel_ClearsState_EvenMidFlow()
        {
            var side = MakeState();
            BattleCardInstance front = DeployUnit(side, Lane.Front, rarity: 1);
            var state = new RepositionSelectionState();
            state.BeginSeismicSwap(spellIndex: 4);
            state.TrySelectUnit(side, front);

            state.Cancel();

            Assert.AreEqual(RepositionSelectionState.Step.Idle, state.Current);
            Assert.IsFalse(state.IsActive);
            Assert.IsNull(state.FirstUnit);
            Assert.IsNull(state.TryBuildSeismicSwapTarget(side, front), "A cancelled selection must never still produce a castable target.");
        }

        // ---------- helpers ----------

        private static PlayerBattleState MakeState() =>
            new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);

        private static Lane Back(PlayerBattleState side) => Lane.Back; // readability alias for the capacity test above

        /// <summary>Deploys a fresh unit into `lane` with the real bonus that lane actually
        /// grants (mirrors BattleController.TryPlayCard) - unlike the bare MakeUnit helper below,
        /// which always bakes in Front's bonus regardless of where the caller then places it.</summary>
        private static BattleCardInstance DeployUnit(PlayerBattleState side, Lane lane, int rarity)
        {
            var data = new CardData
            {
                id = "reposition_test_card_" + System.Guid.NewGuid().ToString("N"),
                name = "Reposition Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = rarity,
            };
            var unit = new BattleCardInstance(Card.FromData(data), isPlayerOwned: true,
                laneAttackBonus: RepositionRules.LaneAttackBonusFor(lane),
                laneHealthBonus: RepositionRules.LaneHealthBonusFor(lane));
            side.Lanes[lane].Cards.Add(unit);
            return unit;
        }

        private static void FillLane(LaneState lane, int slots)
        {
            int used = 0;
            while (used < slots)
            {
                lane.Cards.Add(MakeUnit(rarity: 1));
                used += 1;
            }
        }

        private static void AddAttacker(PlayerBattleState side, Lane lane, int attack)
        {
            var data = new CardData
            {
                id = "reposition_attacker_" + System.Guid.NewGuid().ToString("N"),
                name = "Reposition Test Attacker", art_file = "x.png", element = "Andras", type = "warrior",
                rarity = 1, attack = attack, health = 10,
            };
            side.Lanes[lane].Cards.Add(new BattleCardInstance(Card.FromData(data), isPlayerOwned: false, laneAttackBonus: 0, laneHealthBonus: 0));
        }

        private static BattleCardInstance MakeUnit(int rarity)
        {
            var data = new CardData
            {
                id = "reposition_test_card_" + System.Guid.NewGuid().ToString("N"),
                name = "Reposition Test Card", art_file = "x.png", element = "Andras", type = "warrior", rarity = rarity,
            };
            // Deployed with Front's real Attack bonus so bonus-recompute tests have something to
            // remove/re-add - matches how BattleController.TryPlayCard actually deploys a card.
            return new BattleCardInstance(Card.FromData(data), isPlayerOwned: true,
                laneAttackBonus: BattleController.FrontLaneAttackBonus, laneHealthBonus: 0);
        }

        private static BattleController MakeStartedController(out BattleCardInstance windstepUnit)
        {
            var controller = new BattleController();
            var economy = new BattleController.MatchEconomy(60, 60, 1000);
            controller.StartMatch(new List<Card>(), new List<Card>(), economy, economy,
                equippedSpellIds: new List<string> { "windstep" });
            windstepUnit = MakeUnit(rarity: 1);
            controller.PlayerState.Lanes[Lane.Front].Cards.Add(windstepUnit);
            controller.ConfirmFormation(); // Phase must leave Formation before AdvanceCombatTick does anything.
            while (controller.TickCount < AISpellCaster.MinTickForAnySpell)
                controller.AdvanceCombatTick();
            return controller;
        }
    }
}
