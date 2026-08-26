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

        public BattleDeploymentRecord(Lane lane, int tick)
        {
            Lane = lane;
            Tick = tick;
        }
    }
}
