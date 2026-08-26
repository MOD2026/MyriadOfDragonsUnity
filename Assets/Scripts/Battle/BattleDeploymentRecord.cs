namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// One unit the player put on the board, and when.
    ///
    /// Lives in Battle rather than Empire on purpose: BattleController must not depend on the
    /// Solo Circuit to record its own history. Empire maps these into its own rule type, keeping
    /// the dependency pointing one way (Empire -> Battle) instead of tangling the two.
    /// </summary>
    public readonly struct BattleDeploymentRecord
    {
        public readonly Lane Lane;

        /// <summary>Tick the unit landed on. 0 for the initial formation, since lock-in happens
        /// before any combat tick has run.</summary>
        public readonly int Tick;

        /// <summary>Resource actually paid for this unit.
        ///
        /// Recorded per deployment rather than read back off the card, because a Resource-total
        /// restriction has to count what was SPENT during this battle. A unit that died is still
        /// Resource the player committed, and the card definition alone cannot say how many times
        /// it was deployed.</summary>
        public readonly int ResourceSpent;

        public BattleDeploymentRecord(Lane lane, int tick, int resourceSpent)
        {
            Lane = lane;
            Tick = tick;
            ResourceSpent = resourceSpent;
        }
    }
}
