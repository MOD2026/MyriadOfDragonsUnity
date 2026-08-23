namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// One successfully-cast spell's facts, immutable once created - Combat Tick Feed data
    /// (2026-08-22), the spell-cast counterpart to CombatTickRecord. Only BattleController.
    /// TryCastSpell produces these, and only on a successful cast (a rejected cast changes
    /// nothing and logs nothing). <see cref="CastByPlayer"/> distinguishes player vs mirrored
    /// PvE AI casts (Option B, 2026-08-22).
    /// </summary>
    public readonly struct SpellCastRecord
    {
        public readonly int TickNumber;
        public readonly string SpellName;
        public readonly Lane TargetLane;
        public readonly int AvatarDamageDealt;
        public readonly bool CastByPlayer;

        public SpellCastRecord(int tickNumber, string spellName, Lane targetLane, int avatarDamageDealt, bool castByPlayer = true)
        {
            TickNumber = tickNumber;
            SpellName = spellName;
            TargetLane = targetLane;
            AvatarDamageDealt = avatarDamageDealt;
            CastByPlayer = castByPlayer;
        }
    }
}
