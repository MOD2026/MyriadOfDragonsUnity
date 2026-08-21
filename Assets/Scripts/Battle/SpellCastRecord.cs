namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// One successfully-cast spell's facts, immutable once created - Combat Tick Feed data
    /// (2026-08-22), the spell-cast counterpart to CombatTickRecord. Only BattleController.
    /// TryCastSpell produces these, and only on a successful cast (a rejected cast changes
    /// nothing and logs nothing). Always the player's own cast: TryCastSpell has no "which side"
    /// parameter and only ever casts from PlayerState against EnemyState - the AI does not cast
    /// spells today (MOS §6/§20, asymmetric by design) - so there is no CastByPlayer field to get
    /// wrong or drift out of sync with that rule.
    /// </summary>
    public readonly struct SpellCastRecord
    {
        public readonly int TickNumber;
        public readonly string SpellName;
        public readonly Lane TargetLane;
        public readonly int AvatarDamageDealt;

        public SpellCastRecord(int tickNumber, string spellName, Lane targetLane, int avatarDamageDealt)
        {
            TickNumber = tickNumber;
            SpellName = spellName;
            TargetLane = targetLane;
            AvatarDamageDealt = avatarDamageDealt;
        }
    }
}
