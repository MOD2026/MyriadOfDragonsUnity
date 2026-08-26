using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// Ledger -> presentation beat mapping.
    ///
    /// The risk this guards is not "wrong pixels" - it is the stage telling the player something
    /// the tick did not actually say. Wrong side, invented numbers, or a defeat that never gets a
    /// beat are all bugs that look cosmetic and are not.
    /// </summary>
    public class CombatResolutionEventMapperTests
    {
        private static LaneClashResult Lane(
            Lane lane, string[] playerDead = null, string[] enemyDead = null,
            bool playerCleared = false, bool enemyCleared = false,
            int overflowToPlayer = 0, int overflowToEnemy = 0) =>
            new LaneClashResult
            {
                Lane = lane,
                SideACleared = playerCleared,
                SideBCleared = enemyCleared,
                OverflowToA = overflowToPlayer,
                OverflowToB = overflowToEnemy,
                DefeatedCardNamesA = playerDead ?? new string[0],
                DefeatedCardNamesB = enemyDead ?? new string[0],
            };

        private static CombatTickRecord Tick(
            int number, int dmgToPlayer = 0, int dmgToEnemy = 0, params LaneClashResult[] lanes) =>
            new CombatTickRecord(number, dmgToPlayer, dmgToEnemy, 100, 100, lanes.ToList(), 0, 0);

        [Test]
        public void ALaneClash_ProducesOneAggregateBeat_NotOnePerCard()
        {
            // "Do not play nine individual full animations." Three cards dying on each side is one
            // clash beat plus the individual defeat beats - not seven clash animations.
            CombatTickRecord tick = Tick(1, lanes: Lane(
                MyriadOfDragons.Battle.Lane.Front,
                playerDead: new[] { "a", "b" },
                enemyDead: new[] { "c" }));

            List<CombatResolutionEvent> beats = CombatResolutionEventMapper.MapTick(tick);

            Assert.AreEqual(1, beats.Count(b => b.Type == CombatResolutionEventType.ClashResolved),
                "Exactly one clash beat per lane.");
        }

        [Test]
        public void EveryDefeatedCard_GetsItsOwnBeat_OnTheCorrectSide()
        {
            // Side correctness matters more than it looks: BattleController resolves
            // ResolveTurn(PlayerState, EnemyState), so SideA is the PLAYER. Getting this backwards
            // would keep every number right while pointing the animation at the wrong player.
            CombatTickRecord tick = Tick(1, lanes: Lane(
                MyriadOfDragons.Battle.Lane.Middle,
                playerDead: new[] { "mine1", "mine2" },
                enemyDead: new[] { "theirs" }));

            List<CombatResolutionEvent> defeats = CombatResolutionEventMapper.MapTick(tick)
                .Where(b => b.Type == CombatResolutionEventType.CardDefeated).ToList();

            Assert.AreEqual(3, defeats.Count);
            Assert.AreEqual(2, defeats.Count(d => d.Source == CombatResolutionSide.Player));
            Assert.AreEqual(1, defeats.Count(d => d.Source == CombatResolutionSide.Enemy));
            CollectionAssert.Contains(defeats.Select(d => d.SourceId).ToList(), "theirs");
        }

        [Test]
        public void ATickWithNoAvatarDamage_EmitsNoAvatarBeat()
        {
            // "Nothing happened to you" is not worth 0.85s of stage time, and the doc reserves the
            // avatar beat for a real signed HP delta.
            CombatTickRecord tick = Tick(1, dmgToPlayer: 0, dmgToEnemy: 0,
                lanes: Lane(MyriadOfDragons.Battle.Lane.Front));

            Assert.IsFalse(CombatResolutionEventMapper.MapTick(tick)
                .Any(b => b.Type == CombatResolutionEventType.AvatarHealthChanged));
        }

        [Test]
        public void AvatarDamage_IsReportedAsANEGATIVESignedValue()
        {
            // The player reads a signed number. Damage that arrives positive would display as a
            // heal with a damage icon - colour and sign disagreeing is exactly what the doc's
            // "motion, icon and number must agree" rule forbids.
            CombatTickRecord tick = Tick(4, dmgToPlayer: 7, lanes: Lane(MyriadOfDragons.Battle.Lane.Front));

            CombatResolutionEvent avatar = CombatResolutionEventMapper.MapTick(tick)
                .First(b => b.Type == CombatResolutionEventType.AvatarHealthChanged);

            Assert.AreEqual(-7, avatar.SignedValue);
            Assert.AreEqual(CombatResolutionSide.Player, avatar.Source);
        }

        [Test]
        public void ALaneStateBeat_IsEmittedOnlyWhenTheLaneACTUALLYCleared()
        {
            // A lane can lose cards without emptying. Inferring "cleared" from a defeat count would
            // dim the lane crest on a lane that is still holding.
            CombatTickRecord notCleared = Tick(1, lanes: Lane(
                MyriadOfDragons.Battle.Lane.Back, playerDead: new[] { "one" }, playerCleared: false));
            Assert.IsFalse(CombatResolutionEventMapper.MapTick(notCleared)
                .Any(b => b.Type == CombatResolutionEventType.LaneStateChanged));

            CombatTickRecord cleared = Tick(1, lanes: Lane(
                MyriadOfDragons.Battle.Lane.Back, playerDead: new[] { "one" }, playerCleared: true));
            Assert.IsTrue(CombatResolutionEventMapper.MapTick(cleared)
                .Any(b => b.Type == CombatResolutionEventType.LaneStateChanged &&
                          b.RemainingSlots == 0));
        }

        [Test]
        public void BeatOrder_FollowsCausality_ClashThenDefeatsThenAvatar()
        {
            // Cards trade, cards die, the Avatar takes what got through. Presenting the avatar hit
            // before the clash that caused it would tell the story backwards.
            CombatTickRecord tick = Tick(2, dmgToPlayer: 3, lanes: Lane(
                MyriadOfDragons.Battle.Lane.Front, playerDead: new[] { "x" }));

            List<CombatResolutionEvent> beats = CombatResolutionEventMapper.MapTick(tick);
            int clash = beats.FindIndex(b => b.Type == CombatResolutionEventType.ClashResolved);
            int defeat = beats.FindIndex(b => b.Type == CombatResolutionEventType.CardDefeated);
            int avatar = beats.FindIndex(b => b.Type == CombatResolutionEventType.AvatarHealthChanged);

            Assert.Less(clash, defeat);
            Assert.Less(defeat, avatar);
        }

        [Test]
        public void ASpellBeat_CarriesItsCasterSideAndTargetLane()
        {
            var cast = new SpellCastRecord(3, "Firestorm", MyriadOfDragons.Battle.Lane.Middle, 5, true);

            CombatResolutionEvent beat = CombatResolutionEventMapper.MapSpell(cast);

            Assert.AreEqual(CombatResolutionEventType.SpellResolved, beat.Type);
            Assert.AreEqual(CombatResolutionSide.Player, beat.Source);
            Assert.AreEqual(MyriadOfDragons.Battle.Lane.Middle, beat.Lane);
            Assert.AreEqual(-5, beat.SignedValue);
            Assert.AreEqual("Firestorm", beat.SourceId);
        }

        [Test]
        public void SpellTier_IsNEVERDerivedFromDamage()
        {
            // The doc forbids computing visual tier from raw damage - doing so invents a combat
            // classification the game does not have. A 50-damage spell and a 1-damage spell must
            // present at the same tier until real effect data says otherwise.
            var small = new SpellCastRecord(1, "Spark", MyriadOfDragons.Battle.Lane.Front, 1, true);
            var huge = new SpellCastRecord(1, "Cataclysm", MyriadOfDragons.Battle.Lane.Front, 50, true);

            Assert.AreEqual(CombatResolutionEventMapper.MapSpell(small).Tier,
                CombatResolutionEventMapper.MapSpell(huge).Tier,
                "Tier is a classification, not a magnitude.");
        }

        [Test]
        public void ANullLaneList_MapsToNoBeats_RatherThanThrowing()
        {
            // A presentation-layer throw inside the match-end path would break combat over a
            // cosmetic concern. It degrades instead.
            var tick = new CombatTickRecord(1, 0, 0, 100, 100, null, 0, 0);
            Assert.DoesNotThrow(() => CombatResolutionEventMapper.MapTick(tick));
            Assert.IsEmpty(CombatResolutionEventMapper.MapTick(tick));
        }
    }
}
