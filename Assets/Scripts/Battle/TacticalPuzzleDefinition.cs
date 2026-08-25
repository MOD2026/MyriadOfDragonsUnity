using System;
using System.Collections.Generic;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// Which side of the board a reference points at. Puzzles are authored from the player's
    /// perspective, but the enemy board has to be describable too - it is fixed scenery in a
    /// puzzle, not an actor.
    /// </summary>
    public enum TacticalPuzzleSide
    {
        Player,
        Enemy,
    }

    /// <summary>
    /// Points at a unit already on the board WITHOUT holding a live object reference.
    ///
    /// This is the piece that makes a puzzle authorable as data. <see cref="TacticalPuzzleAction"/>
    /// takes <see cref="BattleCardInstance"/> objects, which only exist once a state has been
    /// built - so a stored definition cannot name them directly. It names a coordinate instead,
    /// and <see cref="TacticalPuzzleAuthoring"/> resolves coordinates to instances after
    /// materialisation.
    ///
    /// The coordinate is stable because a puzzle has NO DRAW and materialisation places units in
    /// authored order, so "player, Front, index 1" means the same unit on every attempt. That is
    /// only true for the STARTING board; an action that deploys or swaps changes indices, which is
    /// why every expectation replays from a fresh materialisation rather than a carried-over one.
    /// </summary>
    [Serializable]
    public class TacticalPuzzleUnitRef
    {
        public TacticalPuzzleSide Side;
        public Lane Lane;

        /// <summary>Position within the authored lane list, not a slot index - a rarity 5+ unit
        /// occupies two slots (Card.SlotWeight) so the two do not line up.</summary>
        public int IndexInLane;

        public override string ToString() => Side + "/" + Lane + "[" + IndexInLane + "]";
    }

    /// <summary>One authored unit on a starting board. The card itself is named by id and
    /// resolved through a card source, so no stats are duplicated into puzzle data - a puzzle that
    /// carried its own copy of a card's Attack would silently drift from the real card.</summary>
    [Serializable]
    public class TacticalPuzzleUnitSpec
    {
        public string CardId;
        public Lane Lane;

        /// <summary>Optional damage already taken, for puzzles that want to start a unit hurt.
        /// 0 = undamaged. Applied through the real ApplyDamage path rather than by assigning
        /// health, so any damage-side rule stays in effect.</summary>
        public int PreDamage;
    }

    /// <summary>
    /// A serialisable action, expressed in coordinates instead of object references. Mirrors
    /// <see cref="TacticalPuzzleAction"/> one-for-one; TacticalPuzzleAuthoring.Resolve converts
    /// between them. Deliberately NOT merged with the runtime action type: the runtime one should
    /// keep taking real instances so the verifier stays usable directly from live UI code, which
    /// is how a played solution gets checked.
    /// </summary>
    [Serializable]
    public class TacticalPuzzleActionSpec
    {
        public TacticalPuzzleActionKind Kind;

        /// <summary>Deploy: index into the authored starting hand.</summary>
        public int HandIndex;

        /// <summary>Deploy and Windstep: destination lane. Unused by SeismicSwap.</summary>
        public Lane Lane;

        /// <summary>Windstep: the unit to move. SeismicSwap: the first unit. Unused by Deploy.</summary>
        public TacticalPuzzleUnitRef UnitA;

        /// <summary>SeismicSwap only: the partner unit.</summary>
        public TacticalPuzzleUnitRef UnitB;
    }

    /// <summary>
    /// One entry in the expected legal-action envelope: a sequence the author claims produces a
    /// specific verifier outcome.
    ///
    /// This is what turns a puzzle definition from data into a CHECKABLE artifact. An author
    /// states "this line solves it", "this deploy is illegal - the lane is full", "this one is
    /// unaffordable", and TacticalPuzzleAuthoring.CheckEnvelope replays each against the real
    /// verifier. A puzzle whose intended solution does not actually solve it is the most likely
    /// authoring mistake there is, and it is the one that cannot be caught by reading the data.
    /// </summary>
    [Serializable]
    public class TacticalPuzzleExpectation
    {
        /// <summary>Author-facing label, surfaced in mismatch messages. Never used as a key.</summary>
        public string Description;

        public List<TacticalPuzzleActionSpec> Actions = new List<TacticalPuzzleActionSpec>();

        public TacticalPuzzleStatus ExpectedStatus;

        /// <summary>Which action the author expects to be rejected, for the non-solving cases.
        /// -1 means "do not care" - an expectation about the outcome only.</summary>
        public int ExpectedFailedActionIndex = -1;
    }

    /// <summary>
    /// The authored objective. Separate from <see cref="TacticalPuzzleObjective"/> because the
    /// runtime one holds a live BattleCardInstance for DefeatMarkedTarget, which data cannot.
    /// </summary>
    [Serializable]
    public class TacticalPuzzleObjectiveSpec
    {
        public TacticalPuzzleObjectiveKind Kind;

        /// <summary>SurviveClashes only.</summary>
        public int ClashCount;

        /// <summary>DefeatMarkedTarget only. Normally an Enemy-side ref.</summary>
        public TacticalPuzzleUnitRef MarkedTarget;

        /// <summary>ProtectLane only.</summary>
        public Lane ProtectedLane;

        /// <summary>MinimalResourceSolve only.</summary>
        public int ResourceBudget;
    }

    /// <summary>
    /// A complete, self-contained Tactical Puzzle: fixed board, fixed hand, fixed Resource,
    /// objective, and the expected legal-action envelope.
    ///
    /// STRUCTURE ONLY - NO CONTENT. Every number here is an author-supplied field with no baked
    /// default, because puzzle content and tuning are an explicitly separate design pass. Nothing
    /// in this file decides how much Resource a puzzle grants, how many clashes counts as hard, or
    /// which cards appear. Validation checks that a definition is COHERENT (a lane that fits, an
    /// objective whose required field is set, a card id that resolves), never that it is
    /// well-tuned.
    ///
    /// Plain fields and [Serializable] throughout so JsonUtility can round-trip a puzzle without a
    /// custom writer, which is what "data-driven" has to mean if puzzles are ever authored outside
    /// the editor or delivered as a seeded daily.
    /// </summary>
    [Serializable]
    public class TacticalPuzzleDefinition
    {
        public string PuzzleId;
        public string DisplayName;

        /// <summary>Starting Resource for the player. No default: 0 is a legitimate authored
        /// value (a puzzle solvable only by repositioning), so it cannot double as "unset".</summary>
        public int StartingResource;

        /// <summary>Must be >= StartingResource. Puzzles do not gain Resource per turn - there
        /// are no turns - but PlayerBattleState requires a cap, and the verifier reports Resource
        /// remaining against it.</summary>
        public int ResourceCap;

        /// <summary>Avatar health for both sides. Puzzles resolve lane clashes, not Avatar
        /// damage, so this exists to construct a valid state rather than to be a puzzle
        /// parameter.</summary>
        public int AvatarHealth;

        /// <summary>The fixed hand, in order. Deploy actions index into this.</summary>
        public List<string> Hand = new List<string>();

        public List<TacticalPuzzleUnitSpec> PlayerBoard = new List<TacticalPuzzleUnitSpec>();
        public List<TacticalPuzzleUnitSpec> EnemyBoard = new List<TacticalPuzzleUnitSpec>();

        public TacticalPuzzleObjectiveSpec Objective = new TacticalPuzzleObjectiveSpec();

        public List<TacticalPuzzleExpectation> Envelope = new List<TacticalPuzzleExpectation>();
    }
}
