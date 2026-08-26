using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Trigger resolution must not depend on which side is passed first.
    ///
    /// THE BUG THIS PINS: `BattleController` always passes the player as sideA, and the resolver
    /// used to run `ResolveTriggers(laneA, ...)` then `ResolveTriggers(laneB, ...)`, applying each
    /// side's effects immediately. So a player trigger that killed an enemy unit removed that unit
    /// before its own trigger was ever considered - a hidden first-mover advantage in every lane of
    /// every tick of every match, and a violation of MOS_v1.1 section 20's "no initiative in
    /// simultaneous combat".
    ///
    /// WHY NO EXISTING TEST CAUGHT IT, which is the part worth internalising: the behaviour was
    /// fully deterministic and internally consistent. Win rates, damage averages and balance sims
    /// all sampled it happily, because the bias was baked uniformly into every sample. **An
    /// aggregate assertion cannot see a uniform bias.** It is only visible if you ask whether
    /// swapping the two sides changes the outcome - which is precisely what this file asks.
    /// </summary>
    public class TriggerOrderFairnessTests
    {
        /// <summary>
        /// Both units carry Hex Spark, deal 1 combat damage, and have 2 HP - so ordinary combat
        /// leaves each at exactly 1 HP and each side's trigger is then lethal to the other. Under
        /// sequential resolution whoever fires first survives outright; under simultaneous
        /// resolution both die.
        ///
        /// THE STATS ARE NOT ARBITRARY AND MUST STAY INSIDE RARITY 1's AUTHORED RANGE (attack 1-2,
        /// health 1-2). My first version passed `attack: 0` intending "no combat damage". Card
        /// .ComputeStats treats 0 as the unauthored sentinel and substitutes rarity-generated
        /// stats, so both units silently got real attack values and killed each other with combat
        /// damage - the test passed against the BUGGY resolver, proving nothing. It only surfaced
        /// because the before/after baseline was actually run instead of assumed.
        /// </summary>
        private static (PlayerBattleState side, BattleCardInstance unit) MakeMutualKillSide()
        {
            PlayerBattleState state = MakeState();
            BattleCardInstance caster = MakeUnit("goblin_caster", attack: 1, health: 2);
            state.Lanes[Lane.Front].Cards.Add(caster);
            return (state, caster);
        }

        private static PlayerBattleState MakeState() =>
            new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20, startingAvatarHealth: 1000);

        private static BattleCardInstance MakeUnit(string cardId, int attack, int health)
        {
            var data = new CardData
            {
                // Rarity 1 so attack/health of 1-2 are inside the authored range and are taken
                // verbatim. Same element on both sides, so no elemental advantage skews damage.
                // "strategist" rather than "warrior": the Warrior class widens the accepted attack
                // range and adds a lane bonus this test does not want.
                id = cardId, name = cardId, art_file = "x.png", element = "Andras", type = "strategist",
                rarity = 1, attack = attack, health = health,
            };
            return new BattleCardInstance(Card.FromData(data), isPlayerOwned: true, laneAttackBonus: 0, laneHealthBonus: 0);
        }

        [Test]
        public void TwoMutuallyLethalTriggers_KillBothUnits_RegardlessOfWhichSideResolvesFirst()
        {
            // Player passed as sideA - the real production arrangement.
            (PlayerBattleState playerFirstA, BattleCardInstance playerUnit1) = MakeMutualKillSide();
            (PlayerBattleState playerFirstB, BattleCardInstance enemyUnit1) = MakeMutualKillSide();

            // Setup guard. If the stats were silently replaced by rarity generation again, this
            // fails LOUDLY here rather than letting the test pass for the wrong reason - which is
            // exactly what happened the first time.
            Assert.AreEqual(1, playerUnit1.Attack, "Authored attack was not taken verbatim.");
            Assert.AreEqual(2, playerUnit1.MaxHealth, "Authored health was not taken verbatim.");
            Assert.AreEqual(CardTriggerAbility.HexSpark, playerUnit1.Ability,
                "This scenario depends on both units carrying Hex Spark.");
            LaneBattleResolver.ResolveTurn(playerFirstA, playerFirstB, tickNumber: 1);

            // Same board, sides swapped. If resolution is genuinely simultaneous the outcome is
            // identical; if it is sequential, the advantage simply changes hands.
            (PlayerBattleState enemyFirstA, BattleCardInstance enemyUnit2) = MakeMutualKillSide();
            (PlayerBattleState enemyFirstB, BattleCardInstance playerUnit2) = MakeMutualKillSide();
            LaneBattleResolver.ResolveTurn(enemyFirstA, enemyFirstB, tickNumber: 1);

            Assert.IsFalse(playerUnit1.IsAlive,
                "Player unit survived when the player resolved first - its trigger killed the enemy " +
                "before the enemy's equally lethal trigger could fire. That is the first-mover bias.");
            Assert.IsFalse(enemyUnit1.IsAlive, "Enemy unit should also have died - both triggers are lethal.");

            Assert.IsFalse(enemyUnit2.IsAlive, "Enemy unit survived when it resolved first - the bias merely changed hands.");
            Assert.IsFalse(playerUnit2.IsAlive, "Player unit should also have died - both triggers are lethal.");
        }

        [Test]
        public void SwappingTheSides_ProducesTheMirroredSurvivorSet_NotADifferentOne()
        {
            // Asymmetric board: the side with two casters out-triggers the side with one. That is a
            // legitimate advantage from BOARD STATE, and it must be the only thing deciding the
            // outcome - the survivor set has to mirror exactly when the sides are swapped.
            List<string> ForwardRun()
            {
                PlayerBattleState a = MakeState();
                PlayerBattleState b = MakeState();
                a.Lanes[Lane.Front].Cards.Add(MakeUnit("goblin_caster", 1, 2));
                a.Lanes[Lane.Front].Cards.Add(MakeUnit("goblin_caster", 1, 2));
                b.Lanes[Lane.Front].Cards.Add(MakeUnit("goblin_caster", 1, 2));
                LaneBattleResolver.ResolveTurn(a, b, tickNumber: 1);
                return Survivors(a, b);
            }

            List<string> SwappedRun()
            {
                PlayerBattleState a = MakeState();
                PlayerBattleState b = MakeState();
                a.Lanes[Lane.Front].Cards.Add(MakeUnit("goblin_caster", 1, 2));
                b.Lanes[Lane.Front].Cards.Add(MakeUnit("goblin_caster", 1, 2));
                b.Lanes[Lane.Front].Cards.Add(MakeUnit("goblin_caster", 1, 2));
                LaneBattleResolver.ResolveTurn(a, b, tickNumber: 1);
                // Reported b-then-a so the two runs describe the same board in the same order.
                return Survivors(b, a);
            }

            CollectionAssert.AreEqual(ForwardRun(), SwappedRun(),
                "The survivor set changed when the sides were swapped, so which side is passed " +
                "first still decides the outcome.");
        }

        private static List<string> Survivors(PlayerBattleState first, PlayerBattleState second)
        {
            var result = new List<string>();
            foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
            {
                result.Add("first:" + lane + ":" + first.Lanes[lane].Cards.Count(c => c.IsAlive));
                result.Add("second:" + lane + ":" + second.Lanes[lane].Cards.Count(c => c.IsAlive));
            }

            return result;
        }
    }
}
