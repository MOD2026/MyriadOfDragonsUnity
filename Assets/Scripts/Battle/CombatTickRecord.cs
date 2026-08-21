using System.Collections.Generic;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// One resolved combat tick's numbers, immutable once created - the raw data foundation for
    /// a future hardcore combat-log UI. Not itself a UI feature; BattleController.CombatLedger is
    /// the only thing that produces these, one per actually-resolved tick (see AdvanceCombatTick).
    ///
    /// PlayerAvatarHealthAfter/EnemyAvatarHealthAfter are the state *after* this tick's damage
    /// already landed - LaneBattleResolver.ResolveTurn mutates AvatarHealth before returning, so
    /// by the time BattleController builds this record the values it reads are already post-tick.
    /// </summary>
    public readonly struct CombatTickRecord
    {
        public readonly int TickNumber;
        public readonly int DamageToPlayerAvatar;
        public readonly int DamageToEnemyAvatar;
        public readonly int PlayerAvatarHealthAfter;
        public readonly int EnemyAvatarHealthAfter;

        /// <summary>Front/Middle/Back overflow in both directions for this tick, reusing
        /// LaneBattleResolver's own per-lane result rather than re-deriving it.</summary>
        public readonly IReadOnlyList<LaneClashResult> LaneResults;

        /// <summary>Combat Tick Feed data (2026-08-22): the portion of DamageToPlayerAvatar/
        /// DamageToEnemyAvatar that came from the exposed-Avatar siege rule specifically, rather
        /// than lane overflow - see TurnResolutionResult.SiegeDamageToSideA/B, which this just
        /// carries into the ledger unchanged.</summary>
        public readonly int SiegeDamageToPlayerAvatar;
        public readonly int SiegeDamageToEnemyAvatar;

        public CombatTickRecord(int tickNumber, int damageToPlayerAvatar, int damageToEnemyAvatar,
            int playerAvatarHealthAfter, int enemyAvatarHealthAfter, IReadOnlyList<LaneClashResult> laneResults,
            int siegeDamageToPlayerAvatar = 0, int siegeDamageToEnemyAvatar = 0)
        {
            TickNumber = tickNumber;
            DamageToPlayerAvatar = damageToPlayerAvatar;
            DamageToEnemyAvatar = damageToEnemyAvatar;
            PlayerAvatarHealthAfter = playerAvatarHealthAfter;
            EnemyAvatarHealthAfter = enemyAvatarHealthAfter;
            LaneResults = laneResults;
            SiegeDamageToPlayerAvatar = siegeDamageToPlayerAvatar;
            SiegeDamageToEnemyAvatar = siegeDamageToEnemyAvatar;
        }
    }
}
