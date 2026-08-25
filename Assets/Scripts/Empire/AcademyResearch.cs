using System;
using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Empire
{
    /// <summary>
    /// The four locked research branches. Note what is ABSENT: there is no combat branch, because
    /// the locked design states Academy "never directly increases card Attack/HP" and "never
    /// bypasses campaign gates". Encoding that as a missing enum value rather than a runtime check
    /// means a combat-boost research option cannot be added without editing this list and
    /// confronting the rule.
    /// </summary>
    public enum AcademyResearchBranch
    {
        CapacitySupport,
        ConstructionPlanning,
        CodexAccess,
        ExpeditionUtility,
    }

    public enum AcademyResearchPhase
    {
        /// <summary>No research running and no choices offered.</summary>
        Idle,

        /// <summary>Choices are on the table; the player has not committed.</summary>
        AwaitingChoice,

        /// <summary>A choice is committed and in progress.</summary>
        InProgress,

        /// <summary>Committed research finished and is ready to be collected.</summary>
        ReadyToCollect,
    }

    /// <summary>
    /// One offered research option. The numbers are deliberately absent: the locked design says
    /// build the STRUCTURE, and that specific research options "need their own design pass". This
    /// matches the "structure locked, numbers open" pattern already used across this codebase - an
    /// option carries its identity and branch, never an invented magnitude.
    /// </summary>
    [Serializable]
    public sealed class AcademyResearchOption
    {
        public string OptionId;
        public AcademyResearchBranch Branch;

        /// <summary>Display copy. Safe to show; carries no numeric promise.</summary>
        public string Title;
    }

    public enum AcademyChoiceStatus
    {
        Committed,
        NoChoicesAvailable,
        UnknownOption,
        AlreadyInProgress,
        NotReadyToCollect,
        Collected,
    }

    public sealed class AcademyChoiceResult
    {
        public AcademyChoiceStatus Status;
        public AcademyResearchOption Option;
        public string Message;

        public bool Ok => Status == AcademyChoiceStatus.Committed || Status == AcademyChoiceStatus.Collected;
    }

    /// <summary>
    /// Persistent Academy state. Self-contained because PlayerProfile.cs is FROZEN - the additive
    /// fields go to the owner for sign-off before any save-shape change, same as Memory Expedition.
    /// </summary>
    [Serializable]
    public sealed class AcademyResearchState
    {
        public AcademyResearchPhase Phase = AcademyResearchPhase.Idle;

        /// <summary>Options currently on the table. Empty unless Phase is AwaitingChoice.</summary>
        public List<AcademyResearchOption> AvailableChoices = new List<AcademyResearchOption>();

        /// <summary>The committed option id, or null.</summary>
        public string CommittedOptionId;
        public AcademyResearchBranch CommittedBranch;

        /// <summary>Branches the player has ever completed - the real record of the choices made.
        /// A branching model is only meaningful if past choices persist.</summary>
        public List<AcademyResearchBranch> CompletedBranches = new List<AcademyResearchBranch>();

        public AcademyResearchState Clone() => (AcademyResearchState)MemberwiseClone();
    }

    /// <summary>
    /// Academy research: a real branching CHOICE model (register: "Academy + Prison real design
    /// LOCKED", 2026-08-25). Explicitly NOT idle production, NOT a linear click-bar, NOT a hidden
    /// combat-stat booster.
    ///
    /// This is the state machine only. It deliberately invents NO research options, magnitudes or
    /// durations - the locked design says those need their own design pass, and inventing them here
    /// is exactly the "never invented" failure the register warns about elsewhere. A future research
    /// system supplies options; this owns offer -> commit -> complete -> collect and the guarantee
    /// that only one runs at a time.
    ///
    /// Plain static logic, no MonoBehaviour, fully EditMode-testable.
    /// </summary>
    public static class AcademyResearch
    {
        /// <summary>
        /// Puts a set of options on the table. Refuses while research is already running - the
        /// locked model is a CHOICE between branches, which is meaningless if several run at once.
        /// </summary>
        public static AcademyChoiceResult OfferChoices(
            AcademyResearchState state, IReadOnlyList<AcademyResearchOption> options)
        {
            var result = new AcademyChoiceResult();

            if (state.Phase == AcademyResearchPhase.InProgress || state.Phase == AcademyResearchPhase.ReadyToCollect)
            {
                result.Status = AcademyChoiceStatus.AlreadyInProgress;
                result.Message = "Research is already running; only one may run at a time.";
                return result;
            }
            if (options == null || options.Count == 0)
            {
                result.Status = AcademyChoiceStatus.NoChoicesAvailable;
                result.Message = "No research options were offered.";
                return result;
            }

            state.AvailableChoices = options.ToList();
            state.Phase = AcademyResearchPhase.AwaitingChoice;
            result.Status = AcademyChoiceStatus.Committed;
            return result;
        }

        /// <summary>
        /// Commits one offered option. The unchosen options are cleared - that is what makes this a
        /// real choice rather than a queue: taking one branch means not taking the others.
        /// </summary>
        public static AcademyChoiceResult Commit(AcademyResearchState state, string optionId)
        {
            var result = new AcademyChoiceResult();

            if (state.Phase == AcademyResearchPhase.InProgress || state.Phase == AcademyResearchPhase.ReadyToCollect)
            {
                result.Status = AcademyChoiceStatus.AlreadyInProgress;
                result.Message = "Research is already running.";
                return result;
            }
            if (state.Phase != AcademyResearchPhase.AwaitingChoice || state.AvailableChoices.Count == 0)
            {
                result.Status = AcademyChoiceStatus.NoChoicesAvailable;
                result.Message = "There is nothing to choose from.";
                return result;
            }

            AcademyResearchOption chosen = state.AvailableChoices.FirstOrDefault(o => o.OptionId == optionId);
            if (chosen == null)
            {
                result.Status = AcademyChoiceStatus.UnknownOption;
                result.Message = "That option was not on offer.";
                return result;
            }

            state.CommittedOptionId = chosen.OptionId;
            state.CommittedBranch = chosen.Branch;
            state.AvailableChoices.Clear();
            state.Phase = AcademyResearchPhase.InProgress;

            result.Status = AcademyChoiceStatus.Committed;
            result.Option = chosen;
            return result;
        }

        /// <summary>Marks committed research finished. Duration/pacing belongs to whatever system
        /// eventually drives this - deliberately not invented here.</summary>
        public static void MarkReadyToCollect(AcademyResearchState state)
        {
            if (state.Phase == AcademyResearchPhase.InProgress)
                state.Phase = AcademyResearchPhase.ReadyToCollect;
        }

        /// <summary>
        /// Collects finished research and records the branch. Idempotent: a second collect returns
        /// NotReadyToCollect and records nothing, so a retry cannot double-credit a branch.
        /// </summary>
        public static AcademyChoiceResult Collect(AcademyResearchState state)
        {
            var result = new AcademyChoiceResult();

            if (state.Phase != AcademyResearchPhase.ReadyToCollect)
            {
                result.Status = AcademyChoiceStatus.NotReadyToCollect;
                result.Message = "No finished research to collect.";
                return result;
            }

            state.CompletedBranches.Add(state.CommittedBranch);
            result.Option = new AcademyResearchOption
            {
                OptionId = state.CommittedOptionId,
                Branch = state.CommittedBranch,
            };

            state.CommittedOptionId = null;
            state.Phase = AcademyResearchPhase.Idle;
            state.AvailableChoices.Clear();

            result.Status = AcademyChoiceStatus.Collected;
            return result;
        }

        public static bool HasCompleted(AcademyResearchState state, AcademyResearchBranch branch) =>
            state != null && state.CompletedBranches.Contains(branch);

        public static int CompletedCount(AcademyResearchState state, AcademyResearchBranch branch) =>
            state == null ? 0 : state.CompletedBranches.Count(b => b == branch);
    }
}
