using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using MyriadOfDragons.Tutorial;
using NUnit.Framework;

namespace MyriadOfDragons.Tests
{
    public sealed class TutorialFoundationTests
    {
        [Test]
        public void CheckpointOrderMatchesApprovedTenStepFlow()
        {
            var checkpoints = Enum.GetValues(typeof(TutorialCheckpointId)).Cast<TutorialCheckpointId>()
                .Where(checkpoint => checkpoint != TutorialCheckpointId.None).ToArray();

            Assert.That(checkpoints.Length, Is.EqualTo(10));
            Assert.That(checkpoints[0], Is.EqualTo(TutorialCheckpointId.OpeningCinematic));
            Assert.That(checkpoints[1], Is.EqualTo(TutorialCheckpointId.CastleRestoration));
            Assert.That(checkpoints[2], Is.EqualTo(TutorialCheckpointId.BarracksRestoration));
            Assert.That(checkpoints[3], Is.EqualTo(TutorialCheckpointId.StarterCardGrant));
            Assert.That(checkpoints[4], Is.EqualTo(TutorialCheckpointId.FormationTutorial));
            Assert.That(checkpoints[5], Is.EqualTo(TutorialCheckpointId.FirstControlledBattle));
            Assert.That(checkpoints[6], Is.EqualTo(TutorialCheckpointId.ReturnToEmpire));
            Assert.That(checkpoints[7], Is.EqualTo(TutorialCheckpointId.TreasuryIntroduction));
            Assert.That(checkpoints[8], Is.EqualTo(TutorialCheckpointId.DragonRoostForeshadowing));
            Assert.That(checkpoints[9], Is.EqualTo(TutorialCheckpointId.EndMandatoryTutorial));
        }

        [Test]
        public void RequestDtosDoNotContainActorOrCredentialFields()
        {
            var requestTypes = new[]
            {
                typeof(AcknowledgeTutorialCheckpointRequest),
                typeof(StarterGrantRequest),
                typeof(SaveFormationRequest),
                typeof(TutorialBattleStartContext),
                typeof(ReconcileTutorialBattleResultRequest),
            };
            var forbidden = new[] { "actor", "account", "token", "credential", "project", "environment" };

            foreach (Type requestType in requestTypes)
            {
                foreach (FieldInfo field in requestType.GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    Assert.That(forbidden.Any(term => field.Name.IndexOf(term, StringComparison.OrdinalIgnoreCase) >= 0), Is.False, requestType.Name + "." + field.Name);
                }
            }
        }

        [Test]
        public void CheckpointProgressionCannotSkipPredecessors()
        {
            var state = new TutorialState();
            TutorialFailure failure;

            Assert.That(TutorialCheckpointOrder.TryAdvance(state, TutorialCheckpointId.BarracksRestoration, out failure), Is.False);
            Assert.That(failure.Category, Is.EqualTo(TutorialFailureCategory.PreconditionRequired));

            Assert.That(TutorialCheckpointOrder.TryAdvance(state, TutorialCheckpointId.OpeningCinematic, out failure), Is.True);
            TutorialCheckpointOrder.ApplyAcknowledgement(state, TutorialCheckpointId.OpeningCinematic);
            Assert.That(TutorialCheckpointOrder.TryAdvance(state, TutorialCheckpointId.CastleRestoration, out failure), Is.True);
        }

        [Test]
        public void RepeatedCheckpointAcknowledgementIsIdempotent()
        {
            var state = new TutorialState();
            TutorialFailure failure;
            Assert.That(TutorialCheckpointOrder.TryAdvance(state, TutorialCheckpointId.OpeningCinematic, out failure), Is.True);
            TutorialCheckpointOrder.ApplyAcknowledgement(state, TutorialCheckpointId.OpeningCinematic);
            Assert.That(TutorialCheckpointOrder.TryAdvance(state, TutorialCheckpointId.OpeningCinematic, out failure), Is.False);
            Assert.That(state.CompletedCheckpoints.Count, Is.EqualTo(1));
            Assert.That(failure.Category, Is.EqualTo(TutorialFailureCategory.AlreadyCompleted));
        }

        [Test]
        public void StarterGrantCannotRepresentTwoGrants()
        {
            var state = new TutorialState();
            TutorialFailure failure;
            Assert.That(TutorialContractValidator.CanIssueStarterGrant(state, out failure), Is.True);
            state.StarterGrantIssued = true;
            Assert.That(TutorialContractValidator.CanIssueStarterGrant(state, out failure), Is.False);
            Assert.That(failure.Code, Is.EqualTo("STARTER_GRANT_ALREADY_ISSUED"));
        }

        [Test]
        public void FormationValidatesKnownUniqueCardsAcrossExplicitLanes()
        {
            var knownCards = new HashSet<string> { "starter_a", "starter_b", "starter_c" };
            var valid = new SavedFormation
            {
                Front = new List<string> { "starter_a" },
                Middle = new List<string> { "starter_b" },
                Back = new List<string> { "starter_c" },
            };
            var duplicate = new SavedFormation
            {
                Front = new List<string> { "starter_a" },
                Middle = new List<string> { "starter_a" },
                Back = new List<string>(),
            };
            TutorialFailure failure;

            Assert.That(TutorialContractValidator.ValidateFormation(valid, knownCards, out failure), Is.True);
            Assert.That(TutorialContractValidator.ValidateFormation(duplicate, knownCards, out failure), Is.False);
            Assert.That(failure.Code, Is.EqualTo("FORMATION_CARD_DUPLICATE_OR_BLANK"));
        }

        [Test]
        public void DefeatCannotAdvanceVictoryOrRewardCheckpoints()
        {
            var state = new TutorialState();
            TutorialFailure failure;
            Assert.That(TutorialContractValidator.CanApplyVictoryReward(state, out failure), Is.True);
            Assert.That(TutorialContractValidator.ResolveInterruption(false), Is.EqualTo(TutorialBattleResumeInstruction.ResumeFromFormation));
            Assert.That(state.FirstVictoryConfirmed, Is.False);
            Assert.That(state.MandatoryTutorialCompleted, Is.False);
        }

        [Test]
        public void UnconfirmedInterruptionResumesFromFormation()
        {
            Assert.That(TutorialContractValidator.ResolveInterruption(false), Is.EqualTo(TutorialBattleResumeInstruction.ResumeFromFormation));
            Assert.That(TutorialContractValidator.ResolveInterruption(true), Is.EqualTo(TutorialBattleResumeInstruction.ShowConfirmedResult));
        }

        [Test]
        public void ConfirmedVictoryReconciliationCanBeAppliedOnlyOnce()
        {
            var state = new TutorialState();
            TutorialFailure failure;
            Assert.That(TutorialContractValidator.CanApplyVictoryReward(state, out failure), Is.True);
            state.FirstVictoryConfirmed = true;
            Assert.That(TutorialContractValidator.CanApplyVictoryReward(state, out failure), Is.False);
            Assert.That(failure.Code, Is.EqualTo("VICTORY_REWARD_ALREADY_RECONCILED"));
        }

        [Test]
        public void ReplayCannotRequestAnotherGrantOrReward()
        {
            var state = new TutorialState { StarterGrantIssued = true, FirstVictoryConfirmed = true };
            TutorialFailure failure;
            Assert.That(TutorialContractValidator.CanIssueStarterGrant(state, out failure), Is.False);
            Assert.That(TutorialContractValidator.CanApplyVictoryReward(state, out failure), Is.False);
        }
    }
}
