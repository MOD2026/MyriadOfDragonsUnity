using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.Battle;
using MyriadOfDragons.Cards;
using MyriadOfDragons.Empire;
using NUnit.Framework;
using UnityEngine;

namespace MyriadOfDragons.Tests
{
    /// <summary>
    /// BattleController's player deployment log - the last piece the Formation Trial needed.
    ///
    /// Asserts the properties the Trial actually depends on, not the mechanics of deploying:
    /// player-only, append-only, and surviving the death of the unit it recorded.
    /// </summary>
    public class SoloCircuitDeploymentTrackingTests
    {
        private GameObject _databaseHost;
        private BattleController _controller;
        private GameObject _controllerHost;

        [SetUp]
        public void SetUp()
        {
            CardDatabase.ResetForTests();
            _databaseHost = new GameObject("DeploymentTrackingCardDatabase");
            _databaseHost.AddComponent<CardDatabase>().Initialize();

            _controllerHost = new GameObject("DeploymentTrackingController");
            _controller = _controllerHost.AddComponent<BattleController>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_controllerHost != null) Object.DestroyImmediate(_controllerHost);
            if (_databaseHost != null) Object.DestroyImmediate(_databaseHost);
            CardDatabase.ResetForTests();
        }

        private List<Card> Deck() =>
            CardDatabase.Instance.AllCards
                .Where(c => c.SlotWeight == 1)
                .OrderBy(c => c.ResourceCost)
                .Take(8)
                .ToList();

        private void StartFreshMatch() =>
            _controller.StartMatch(Deck(), Deck(),
                new BattleController.MatchEconomy(20, 20, 100), new BattleController.MatchEconomy(20, 20, 100));

        [Test]
        public void AFreshMatch_StartsWithAnEmptyLog()
        {
            StartFreshMatch();
            Assert.AreEqual(0, _controller.PlayerDeployments.Count);
        }

        [Test]
        public void PlayerDeployments_AreRecordedWithTheirLane()
        {
            StartFreshMatch();
            Card card = _controller.PlayerState.Hand.First();

            Assert.IsTrue(_controller.TryPlayCard(_controller.PlayerState, card, Lane.Front));

            Assert.AreEqual(1, _controller.PlayerDeployments.Count);
            Assert.AreEqual(Lane.Front, _controller.PlayerDeployments[0].Lane);
        }

        [Test]
        public void ENEMY_Deployments_AreNeverRecorded()
        {
            // The restriction describes what the PLAYER did. Counting the AI's plays would break
            // every rule the moment the opponent deployed anywhere.
            StartFreshMatch();
            Card enemyCard = _controller.EnemyState.Hand.First();

            _controller.TryPlayCard(_controller.EnemyState, enemyCard, Lane.Middle);

            Assert.AreEqual(0, _controller.PlayerDeployments.Count,
                "Only the player's own deployments may be judged against their restriction.");
        }

        [Test]
        public void ANewMatch_ClearsThePreviousMatchesLog()
        {
            // Without this, a player's second battle inherits the first one's deployments and fails
            // a restriction for units they never placed in it.
            StartFreshMatch();
            _controller.TryPlayCard(_controller.PlayerState, _controller.PlayerState.Hand.First(), Lane.Front);
            Assert.AreEqual(1, _controller.PlayerDeployments.Count);

            StartFreshMatch();
            Assert.AreEqual(0, _controller.PlayerDeployments.Count);
        }

        [Test]
        public void TheLogFeedsTheFormationRule_EndToEnd()
        {
            // The whole point: a real deployment, through the real controller, judged by the real
            // rule - rather than each half being tested against a fixture the other never sees.
            StartFreshMatch();
            _controller.TryPlayCard(_controller.PlayerState, _controller.PlayerState.Hand.First(), Lane.Front);

            var deployments = _controller.PlayerDeployments
                .Select(d => new SoloCircuitDeployment(d.Lane, d.Tick))
                .ToList();

            Assert.IsTrue(SoloCircuitFormationRule.IsCleared(
                "Front lane only - no units may be deployed to the Middle or Back lane.",
                deployments, isVictory: true));

            Assert.IsFalse(SoloCircuitFormationRule.IsCleared(
                "Back lane only - no units may be deployed to the Front or Middle lane.",
                deployments, isVictory: true));
        }
    }
}
