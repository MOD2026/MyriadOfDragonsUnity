using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Tutorial
{
    public enum TutorialCheckpointId
    {
        None = 0,
        OpeningCinematic = 1,
        CastleRestoration = 2,
        BarracksRestoration = 3,
        StarterCardGrant = 4,
        FormationTutorial = 5,
        FirstControlledBattle = 6,
        ReturnToEmpire = 7,
        TreasuryIntroduction = 8,
        DragonRoostForeshadowing = 9,
        EndMandatoryTutorial = 10,
    }

    public enum TutorialFailureCategory
    {
        None = 0,
        Authentication = 1,
        Authorization = 2,
        Validation = 3,
        Conflict = 4,
        Unavailable = 5,
        AlreadyCompleted = 6,
        PreconditionRequired = 7,
        Unknown = 8,
    }

    public enum StarterGrantStatus
    {
        None = 0,
        Granted = 1,
        AlreadyGranted = 2,
    }

    public enum TutorialBattleOutcome
    {
        Unconfirmed = 0,
        Defeat = 1,
        Victory = 2,
    }

    public enum TutorialBattleResumeInstruction
    {
        None = 0,
        ResumeFromFormation = 1,
        ShowConfirmedResult = 2,
    }

    public enum TutorialReconciliationStatus
    {
        None = 0,
        Applied = 1,
        AlreadyReconciled = 2,
        ResumeFromFormation = 3,
    }

    [Serializable]
    public sealed class TutorialFailure
    {
        public TutorialFailureCategory Category;
        public string Code;
    }

    [Serializable]
    public sealed class TutorialStateResult
    {
        public bool Success;
        public TutorialState State;
        public TutorialFailure Failure;
    }

    [Serializable]
    public sealed class TutorialState
    {
        public TutorialCheckpointId CurrentCheckpoint;
        public List<TutorialCheckpointId> CompletedCheckpoints = new List<TutorialCheckpointId>();
        public bool StarterGrantIssued;
        public SavedFormation SavedFormation;
        public bool FirstVictoryConfirmed;
        public bool MandatoryTutorialCompleted;
    }

    [Serializable]
    public sealed class AcknowledgeTutorialCheckpointRequest
    {
        public TutorialCheckpointId CheckpointId;
        public string RequestId;
    }

    [Serializable]
    public sealed class AcknowledgeTutorialCheckpointResult
    {
        public bool Success;
        public bool AlreadyAcknowledged;
        public TutorialState State;
        public TutorialFailure Failure;
    }

    [Serializable]
    public sealed class StarterGrantRequest
    {
        public string RequestId;
    }

    [Serializable]
    public sealed class StarterGrantResult
    {
        public bool Success;
        public StarterGrantStatus Status;
        public string GrantTransactionId;
        public List<string> CardIds = new List<string>();
        public TutorialFailure Failure;
    }

    [Serializable]
    public sealed class SavedFormation
    {
        public List<string> Front = new List<string>();
        public List<string> Middle = new List<string>();
        public List<string> Back = new List<string>();
    }

    [Serializable]
    public sealed class SaveFormationRequest
    {
        public string RequestId;
        public SavedFormation Formation;
    }

    [Serializable]
    public sealed class SaveFormationResult
    {
        public bool Success;
        public SavedFormation Formation;
        public TutorialFailure Failure;
    }

    [Serializable]
    public sealed class TutorialBattleStartContext
    {
        public string RequestId;
        public SavedFormation Formation;
    }

    [Serializable]
    public sealed class TutorialBattleStartResult
    {
        public bool Success;
        public string BattleTransactionId;
        public TutorialBattleResumeInstruction ResumeInstruction;
        public SavedFormation Formation;
        public TutorialFailure Failure;
    }

    [Serializable]
    public sealed class ReconcileTutorialBattleResultRequest
    {
        public string RequestId;
        public string BattleTransactionId;
        public TutorialBattleOutcome Outcome;
    }

    [Serializable]
    public sealed class ReconcileTutorialBattleResultResult
    {
        public bool Success;
        public TutorialReconciliationStatus Status;
        public bool VictoryCheckpointAdvanced;
        public bool RewardTransactionApplied;
        public TutorialState State;
        public TutorialFailure Failure;
    }
}