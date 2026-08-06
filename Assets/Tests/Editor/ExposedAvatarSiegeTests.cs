using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// The exposed-Avatar siege rule (LaneBattleResolver.ExposedAvatarSiegeEnabled), ADOPTED
    /// 2026-08-07 at 6% and on by default - see docs/Mechanics_Gap_Analysis.md §1.1.
    ///
    /// The first test here is still the important one, and still deliberately forces the rule OFF
    /// rather than relying on any ambient state: it pins down the stalemate the rule exists to fix,
    /// as a fact about the underlying combat maths, independent of whether siege happens to be
    /// enabled when the test runs. If someone later fixes that stalemate a different way, that test
    /// fails and forces a conversation, which is the correct outcome - two independent fixes for
    /// the same structural problem would stack into a game where matches end far too early.
    /// </summary>
    public class ExposedAvatarSiegeTests
    {
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            // ExposedAvatarSiegeEnabled is a mutable static. A test that flips it and throws
            // before restoring it would leave every later test in the run measuring a different
            // game - including the balance simulation, which would then report numbers for rules
            // nobody asked for. TearDown always runs; manual restoration does not.
            LaneBattleResolver.ResetRulesToDefault();

            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        private CardDatabase LoadDatabase()
        {
            var go = new GameObject("SiegeTestCardDatabase");
            _spawned.Add(go);
            CardDatabase db = go.AddComponent<CardDatabase>();
            db.Initialize(); // Awake() never fires outside Play Mode
            return db;
        }

        /// <summary>A side with an empty deck and an empty board - the state both sides reach once
        /// their formations have wiped each other out.</summary>
        private static PlayerBattleState EmptyBoard(int avatarHealth) =>
            new PlayerBattleState(new List<Card>(), resourceCap: 20, turn1Resource: 20,
                startingAvatarHealth: avatarHealth);

        private static void PlaceLivingCard(PlayerBattleState side, Card card, Lane lane) =>
            side.Lanes[lane].Cards.Add(
                new BattleCardInstance(card, isPlayerOwned: true, laneAttackBonus: 0, laneHealthBonus: 0));

        // ---------- The problem ----------

        [Test]
        public void WithoutSiege_TwoWipedBoardsCanNeverDamageEachOtherAgain()
        {
            // This is the structural problem siege exists to fix, isolated from whether siege is
            // currently on by default - explicit, not ambient, so this test keeps meaning the same
            // thing regardless of what LaneBattleResolver's shipped default is.
            LaneBattleResolver.ExposedAvatarSiegeEnabled = false;

            // Both sides have zero Attack, so no lane can be cleared, so no overflow can ever reach
            // an Avatar. The match is decided by the tick cap because nothing else CAN decide it -
            // not because the Health pool is mistuned.
            PlayerBattleState a = EmptyBoard(260);
            PlayerBattleState b = EmptyBoard(260);

            for (int tick = 1; tick <= BattleController.MaxCombatTicks; tick++)
            {
                LaneBattleResolver.ResolveTurn(a, b, tick);
            }

            Assert.AreEqual(260, a.AvatarHealth);
            Assert.AreEqual(260, b.AvatarHealth);
            Assert.IsFalse(a.IsDefeated);
            Assert.IsFalse(b.IsDefeated);
        }

        // ---------- The rule ----------

        [Test]
        public void WithSiege_AnUndefendedAvatarBleedsOnceOvertimeBegins()
        {
            LaneBattleResolver.ExposedAvatarSiegeEnabled = true;

            PlayerBattleState a = EmptyBoard(260);
            PlayerBattleState b = EmptyBoard(260);

            int tick = LaneBattleResolver.SiegeStartTick;
            int expected = LaneBattleResolver.SiegeDamageFor(a, tick);

            LaneBattleResolver.ResolveTurn(a, b, tick);

            // Asserts the relationship against the rule's own calculation rather than a hardcoded
            // number, so retuning the siege fraction is a balance change and not a test break.
            Assert.Greater(expected, 0, "Sanity check: siege must actually do something at this tick.");
            Assert.AreEqual(260 - expected, a.AvatarHealth);
            Assert.AreEqual(260 - expected, b.AvatarHealth);
        }

        [Test]
        public void WithSiege_TheBleedScalesWithTheAvatarsOwnHealthPool()
        {
            LaneBattleResolver.ExposedAvatarSiegeEnabled = true;

            PlayerBattleState small = EmptyBoard(100);
            PlayerBattleState large = EmptyBoard(400);

            int tick = LaneBattleResolver.SiegeStartTick;
            int smallBleed = LaneBattleResolver.SiegeDamageFor(small, tick);
            int largeBleed = LaneBattleResolver.SiegeDamageFor(large, tick);

            // This is the whole reason siege is a fraction rather than a flat number. Measured
            // with a flat value, the same setting that produced a 100% knockout rate at the mid
            // profile produced 90.5% at max and could not be made correct for both - and would
            // have needed retuning on every future Avatar Health change, of which there have
            // already been three.
            Assert.Greater(largeBleed, smallBleed);

            float smallFraction = smallBleed / 100f;
            float largeFraction = largeBleed / 400f;
            Assert.AreEqual(smallFraction, largeFraction, 0.02f,
                "Siege must cost the same PROPORTION of any Avatar's pool, or it is a different " +
                "rule at every progression level.");
        }

        [Test]
        public void WithSiege_NothingHappensBeforeOvertime()
        {
            LaneBattleResolver.ExposedAvatarSiegeEnabled = true;

            PlayerBattleState a = EmptyBoard(260);
            PlayerBattleState b = EmptyBoard(260);

            for (int tick = 1; tick < LaneBattleResolver.SiegeStartTick; tick++)
            {
                LaneBattleResolver.ResolveTurn(a, b, tick);
            }

            // The delay is the whole reason this rule is usable. Measured without it, siege took
            // the knockout rate to 100% at every profile and cut the early-game match to 1.7
            // ticks - shorter than it takes to bank Energy for the cheapest spell. An empty board
            // early in a match is a fight in progress with two reinforcement windows still to
            // come, not a stalemate.
            Assert.AreEqual(260, a.AvatarHealth);
            Assert.AreEqual(260, b.AvatarHealth);
        }

        [Test]
        public void WithSiege_AnAvatarWithAnythingStillAliveIsNotUnderSiege()
        {
            LaneBattleResolver.ExposedAvatarSiegeEnabled = true;

            Card anyCard = LoadDatabase().AllCards.First();

            PlayerBattleState defended = EmptyBoard(260);
            PlayerBattleState wiped = EmptyBoard(260);
            PlaceLivingCard(defended, anyCard, Lane.Back);

            LaneBattleResolver.ResolveTurn(defended, wiped, LaneBattleResolver.SiegeStartTick);

            // Siege is about having no army at all, not about which lane is empty - one survivor
            // in the Back lane still counts as an army. (The defended side's own card does deal
            // its Attack into the wiped side's undefended lane, so `wiped` takes more than siege
            // alone; what matters here is that `defended` takes none of it.)
            Assert.AreEqual(260, defended.AvatarHealth, "A side with a living card must not be under siege.");
            Assert.Less(wiped.AvatarHealth, 260);
        }

        [Test]
        public void WithSiege_TheBleedEscalatesInOvertimeLikeEveryOtherAvatarDamageSource()
        {
            LaneBattleResolver.ExposedAvatarSiegeEnabled = true;

            PlayerBattleState earlyA = EmptyBoard(1000);
            PlayerBattleState earlyB = EmptyBoard(1000);
            LaneBattleResolver.ResolveTurn(earlyA, earlyB, LaneBattleResolver.SiegeStartTick);
            int earlyBleed = 1000 - earlyA.AvatarHealth;

            PlayerBattleState lateA = EmptyBoard(1000);
            PlayerBattleState lateB = EmptyBoard(1000);
            LaneBattleResolver.ResolveTurn(lateA, lateB, LaneBattleResolver.LateOvertimeStartTick);
            int lateBleed = 1000 - lateA.AvatarHealth;

            // Siege is added to raw overflow *before* the multiplier for exactly this reason: a
            // stalemate-breaker that did not escalate would be the one damage source in the game
            // that gets relatively weaker the longer a match drags on.
            Assert.Greater(lateBleed, earlyBleed);
        }

        [Test]
        public void WithSiege_AMutualWipeResolvesIntoAKnockoutInsteadOfRunningOutTheClock()
        {
            LaneBattleResolver.ExposedAvatarSiegeEnabled = true;

            // The real stalemate position: identical Health pools, both boards wiped, and both
            // Avatars already worn down by the fighting that wiped them. The side that preserved
            // more Health should win by killing the other, not by being handed the result on a
            // percentage.
            //
            // Both sides start from the SAME maximum on purpose. Siege costs a fixed fraction of
            // an Avatar's own pool, so it takes the same number of ticks to burn through any pool
            // - it finishes a damaged Avatar, it does not execute a healthy one. An earlier
            // version of this test gave the loser a smaller maximum and failed, correctly: a
            // 40/40 Avatar is at full Health and siege is not supposed to be able to kill it
            // inside the cap.
            PlayerBattleState healthier = EmptyBoard(260);
            PlayerBattleState weaker = EmptyBoard(260);
            healthier.AvatarHealth = 240;
            weaker.AvatarHealth = 100;

            for (int tick = 1; tick <= BattleController.MaxCombatTicks; tick++)
            {
                LaneBattleResolver.ResolveTurn(healthier, weaker, tick);
                if (weaker.IsDefeated) break;
            }

            Assert.IsTrue(weaker.IsDefeated, "Siege must be able to close out a mutually wiped board " +
                                             "within the tick cap, or it does not solve the problem it exists for.");
            Assert.IsFalse(healthier.IsDefeated, "The side that preserved more Health should still be standing.");
        }

        [Test]
        public void SiegeIsOnByDefault()
        {
            // Adopted 2026-08-07 at 6% - see docs/Mechanics_Gap_Analysis.md §1.1. This test is the
            // inverse of the pre-adoption guard: it now makes an accidental revert to OFF visible
            // in a diff, the same way the old test made an accidental flip to ON visible.
            LaneBattleResolver.ResetRulesToDefault();
            Assert.IsTrue(LaneBattleResolver.ExposedAvatarSiegeEnabled);
        }
    }
}
