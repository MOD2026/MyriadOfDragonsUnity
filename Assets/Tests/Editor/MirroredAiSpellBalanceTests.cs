using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Empire;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// P0 balance coverage for mirrored PvE AI spells (Option B) WITH spells enabled.
    /// BalanceSimulationTests keeps AI spells OFF for siege / maths baselines — see that
    /// fixture's class comment. This suite does not weaken AISpellCaster.
    /// </summary>
    public class MirroredAiSpellBalanceTests
    {
        private const int SampleMatches = 80;

        private readonly List<GameObject> _spawned = new List<GameObject>();

        [TearDown]
        public void TearDown()
        {
            LaneBattleResolver.ResetRulesToDefault();
            foreach (GameObject go in _spawned)
            {
                if (go != null) Object.DestroyImmediate(go);
            }
            _spawned.Clear();
        }

        [Test]
        public void MirroredAiSpellsOn_EnemyCastsWhenLegal_AndMatchesResolve()
        {
            CardDatabase db = LoadDatabase();
            List<Card> pool = db.AllCards.ToList();

            var empire = new PlayerEmpireData();
            empire.SetLevelsForTesting(avatarLevel: 1, castleLevel: 1, barracksLevel: 1);
            empire.InitializeTCGModifiers();
            var economy = new BattleController.MatchEconomy(
                empire.ResourceCap, empire.Turn1Resource, empire.StartingAvatarHealth);

            int resolved = 0;
            int matchesWithEnemyCast = 0;
            int totalEnemyCasts = 0;

            Random.InitState(20260823);

            for (int i = 0; i < SampleMatches; i++)
            {
                BattleController controller = CreateController();
                List<Card> playerDeck = pool.OrderBy(_ => Random.value).Take(empire.DeckSlotCount).ToList();
                List<Card> enemyDeck = pool.OrderBy(_ => Random.value).Take(empire.DeckSlotCount).ToList();

                controller.StartMatch(playerDeck, enemyDeck, economy, economy);
                controller.EnableMirroredEnemySpellsForPvE();
                Assert.IsTrue(controller.MirroredEnemySpellsEnabled);

                controller.DealFormationHand(controller.PlayerState);
                controller.DealFormationHand(controller.EnemyState);

                DeployWholeSquad(controller, controller.PlayerState, AIArchetype.Balanced);
                SimpleAIOpponent.TakeTurn(controller, AIArchetype.Balanced);

                if (!controller.ConfirmFormation())
                {
                    Object.DestroyImmediate(controller.gameObject);
                    continue;
                }

                while (controller.Phase == BattlePhase.Combat)
                    controller.AdvanceCombatTick();

                Assert.AreEqual(BattlePhase.Resolved, controller.Phase,
                    "Matches with mirrored AI spells must still terminate.");
                resolved++;

                int enemyCasts = controller.SpellCastLog.Count(c => !c.CastByPlayer);
                totalEnemyCasts += enemyCasts;
                if (enemyCasts > 0) matchesWithEnemyCast++;

                Object.DestroyImmediate(controller.gameObject);
            }

            Assert.Greater(resolved, SampleMatches / 2,
                "Setup: expected most samples to form and resolve.");
            Assert.Greater(matchesWithEnemyCast, 0,
                "With EnableMirroredEnemySpellsForPvE, at least one match must log an enemy cast when energy/targets allow.");
            Assert.Greater(totalEnemyCasts, 0);

            Debug.Log($"[MirroredAI] resolved={resolved}/{SampleMatches}  " +
                      $"matchesWithEnemyCast={matchesWithEnemyCast}  totalEnemyCasts={totalEnemyCasts}");
        }

        [Test]
        public void MirroredAiSpellsOff_ByDefault_NoSurpriseEnemyCasts()
        {
            CardDatabase db = LoadDatabase();
            List<Card> pool = db.AllCards.Take(12).ToList();
            var economy = new BattleController.MatchEconomy(60, 60, 2000);

            BattleController controller = CreateController();
            controller.StartMatch(pool.ToList(), pool.ToList(), economy, economy);
            Assert.IsFalse(controller.MirroredEnemySpellsEnabled,
                "StartMatch must leave mirrored AI spells off (isolation / siege baseline).");

            controller.DealFormationHand(controller.PlayerState);
            controller.DealFormationHand(controller.EnemyState);
            Assert.IsTrue(controller.TryPlayCard(controller.PlayerState, controller.PlayerState.Hand.First(), Lane.Front));
            Assert.IsTrue(controller.TryPlayCard(controller.EnemyState, controller.EnemyState.Hand.First(), Lane.Middle));
            Assert.IsTrue(controller.ConfirmFormation());

            for (int i = 0; i < 4 && controller.Phase == BattlePhase.Combat; i++)
                controller.AdvanceCombatTick();

            Assert.IsFalse(controller.SpellCastLog.Any(c => !c.CastByPlayer),
                "Default StartMatch must not produce surprise enemy casts.");
        }

        private CardDatabase LoadDatabase()
        {
            var go = new GameObject("MirroredAiSpell_CardDatabase");
            _spawned.Add(go);
            CardDatabase db = go.AddComponent<CardDatabase>();
            db.Initialize();
            db = CardDatabase.Instance; // real fix: Initialize() may have destroyed this local instance if a duplicate was already live (see CardDatabase.Initialize's own comment) - always resolve to the survivor.
            return db;
        }

        private BattleController CreateController()
        {
            var go = new GameObject("MirroredAiSpell_BattleController");
            _spawned.Add(go);
            return go.AddComponent<BattleController>();
        }

        private static void DeployWholeSquad(BattleController controller, PlayerBattleState side, AIArchetype archetype)
        {
            bool placed = true;
            while (placed)
            {
                placed = false;
                foreach (Card card in side.Hand.ToList())
                {
                    foreach (Lane lane in new[] { Lane.Front, Lane.Middle, Lane.Back })
                    {
                        if (controller.TryPlayCard(side, card, lane))
                        {
                            placed = true;
                            break;
                        }
                    }
                }
            }
        }
    }
}
