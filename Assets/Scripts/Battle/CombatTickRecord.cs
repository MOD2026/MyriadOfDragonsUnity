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

        public CombatTickRecord(int tickNumber, int damageToPlayerAvatar, int damageToEnemyAvatar,
            int playerAvatarHealthAfter, int enemyAvatarHealthAfter, IReadOnlyList<LaneClashResult> laneResults)
        {
            TickNumber = tickNumber;
            DamageToPlayerAvatar = damageToPlayerAvatar;
            DamageToEnemyAvatar = damageToEnemyAvatar;
            PlayerAvatarHealthAfter = playerAvatarHealthAfter;
            EnemyAvatarHealthAfter = enemyAvatarHealthAfter;
            LaneResults = laneResults;
        }
    }
}
