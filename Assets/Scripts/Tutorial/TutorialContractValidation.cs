using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Tutorial
{
    public static class TutorialCheckpointOrder
    {
        public const int FirstCheckpointNumber = (int)TutorialCheckpointId.OpeningCinematic;
        public const int LastCheckpointNumber = (int)TutorialCheckpointId.EndMandatoryTutorial;

        public static bool TryAdvance(TutorialState state, TutorialCheckpointId requested, out TutorialFailure failure)
        {
            failure = null;
            if (state == null)
            {
                failure = Failure(TutorialFailureCategory.Validation, "STATE_REQUIRED");
                return false;
            }

            if (!Enum.IsDefined(typeof(TutorialCheckpointId), requested) || requested == TutorialCheckpointId.None)
            {
                failure = Failure(TutorialFailureCategory.Validation, "CHECKPOINT_INVALID");
                return false;
            }

            if (state.CompletedCheckpoints.Contains(requested))
            {
                failure = Failure(TutorialFailureCategory.AlreadyCompleted, "CHECKPOINT_ALREADY_ACKNOWLEDGED");
                return false;
            }

            TutorialCheckpointId predecessor = (TutorialCheckpointId)((int)requested - 1);
            if (requested != TutorialCheckpointId.OpeningCinematic && !state.CompletedCheckpoints.Contains(predecessor))
            {
                failure = Failure(TutorialFailureCategory.PreconditionRequired, "CHECKPOINT_PREDECESSOR_REQUIRED");
                return false;
            }

            return true;
        }

        public static void ApplyAcknowledgement(TutorialState state, TutorialCheckpointId checkpoint)
        {
            if (!state.CompletedCheckpoints.Contains(checkpoint))
            {
                state.CompletedCheckpoints.Add(checkpoint);
            }

            state.CurrentCheckpoint = checkpoint;
            if (checkpoint == TutorialCheckpointId.EndMandatoryTutorial)
            {
                state.MandatoryTutorialCompleted = true;
            }
        }

        private static TutorialFailure Failure(TutorialFailureCategory category, string code)
        {
            return new TutorialFailure { Category = category, Code = code };
        }
    }

    public static class TutorialContractValidator
    {
        public static bool IsRequestIdValid(string requestId)
        {
            return !string.IsNullOrWhiteSpace(requestId);
        }

        public static bool ValidateFormation(SavedFormation formation, ISet<string> knownCardIds, out TutorialFailure failure)
        {
            failure = null;
            if (formation == null || formation.Front == null || formation.Middle == null || formation.Back == null)
            {
                failure = Failure("FORMATION_REQUIRED");
                return false;
            }

            var placed = new HashSet<string>(StringComparer.Ordinal);
            foreach (string cardId in EnumerateCards(formation))
            {
                if (string.IsNullOrWhiteSpace(cardId) || !placed.Add(cardId))
                {
                    failure = Failure("FORMATION_CARD_DUPLICATE_OR_BLANK");
                    return false;
                }

                if (knownCardIds == null || !knownCardIds.Contains(cardId))
                {
                    failure = Failure("FORMATION_CARD_UNKNOWN");
                    return false;
                }
            }

            if (placed.Count == 0)
            {
                failure = Failure("FORMATION_EMPTY");
                return false;
            }

            return true;
        }

        public static TutorialBattleResumeInstruction ResolveInterruption(bool resultConfirmed)
        {
            return resultConfirmed
                ? TutorialBattleResumeInstruction.ShowConfirmedResult
                : TutorialBattleResumeInstruction.ResumeFromFormation;
        }

        public static bool CanIssueStarterGrant(TutorialState state, out TutorialFailure failure)
        {
            failure = null;
            if (state != null && state.StarterGrantIssued)
            {
                failure = Failure("STARTER_GRANT_ALREADY_ISSUED");
                return false;
            }

            return true;
        }

        public static bool CanApplyVictoryReward(TutorialState state, out TutorialFailure failure)
        {
            failure = null;
            if (state != null && state.FirstVictoryConfirmed)
            {
                failure = Failure("VICTORY_REWARD_ALREADY_RECONCILED");
                return false;
            }

            return true;
        }

        private static IEnumerable<string> EnumerateCards(SavedFormation formation)
        {
            foreach (string cardId in formation.Front) yield return cardId;
            foreach (string cardId in formation.Middle) yield return cardId;
            foreach (string cardId in formation.Back) yield return cardId;
        }

        private static TutorialFailure Failure(string code)
        {
            return new TutorialFailure { Category = TutorialFailureCategory.Validation, Code = code };
        }
    }
}