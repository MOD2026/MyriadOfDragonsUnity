using System;
using MyriadOfDragons.Empire;
using UnityEngine;

namespace MyriadOfDragons.AI
{
    /// <summary>
    /// AI difficulty bands for solo play. Thresholds are deliberately NOT the 1-300 range the
    /// original spec for this system used: this game's player stat growth caps out much earlier
    /// (PlayerEmpireData's Avatar/Castle resource + health bonuses both stop increasing at level
    /// 30), so tiers spread across 1-300 would have made Veteran/Master/Titan functionally
    /// identical - the player's own numbers stop moving long before level 150. These bands map
    /// onto the curve this game actually has: growth through ~30, then flat.
    /// </summary>
    public enum AIDifficultyTier
    {
        Novice,      // Avatar 1-10:  new player still learning lanes; AI handicapped
        Apprentice,  // Avatar 11-25: player economy still growing; AI roughly even
        Veteran,     // Avatar 26-50: player economy capped; AI takes a small edge
        Master,      // Avatar 51-80: pure skill test; AI clearly favoured on paper
        Titan        // Avatar 81+:   endgame; AI is a boss-tier check
    }

    /// <summary>
    /// How an AI opponent distributes its cards across lanes. Front grants +1 Attack and Middle
    /// grants +1 Health (BattleController.TryPlayCard), so these are genuinely different
    /// strategies rather than cosmetic labels.
    /// </summary>
    public enum AIArchetype
    {
        Aggressive,  // Front lane first - trade attack for fragility
        Defensive,   // Middle/Back first - soak damage, win on attrition
        Balanced,    // Spread across whichever lane is emptiest
        Tactical     // Contest whichever lane the player has committed to most
    }

    /// <summary>
    /// What a battle needs to know about an opponent, independent of whether a bot or a person
    /// is behind it. Kept as an interface specifically so the multiplayer/guild phase can supply
    /// a human opponent's profile to the same battle setup path without changing BattleController
    /// or GameBootstrap - that was the most valuable idea in the spec this file is adapted from,
    /// and it costs nothing to honour now while solo is still the only mode.
    /// </summary>
    public interface IBattleOpponentConfig
    {
        string DisplayName { get; }
        int MaxAvatarHealth { get; }
        int StartingResourceCap { get; }
        bool IsAI { get; }
    }

    /// <summary>
    /// A resolved opponent, generated per match from the player's current progression.
    /// </summary>
    [Serializable]
    public class AIBattleProfile : IBattleOpponentConfig
    {
        public string DisplayName { get; set; }
        public int MaxAvatarHealth { get; set; }
        public int StartingResourceCap { get; set; }
        public int Turn1Resource { get; set; }
        public bool IsAI => true;

        public AIDifficultyTier DifficultyTier;
        public AIArchetype Archetype;

        // ------------------------------------------------------------------
        // Declared but NOT yet applied anywhere - see SoloAIScalingSystem's
        // class comment for why each one is parked rather than wired up.
        // ------------------------------------------------------------------

        /// <summary>NOT APPLIED. See SoloAIScalingSystem's notes on the Integer Model.</summary>
        public float CardStatMultiplier = 1f;

        /// <summary>NOT APPLIED. There is no RNG-triggered skill system in this game to boost.</summary>
        public float SkillTriggerBonus;

        /// <summary>NOT APPLIED. The AI currently resolves synchronously; see the notes.</summary>
        public float DecisionDelaySeconds;
    }

    /// <summary>
    /// Generates a per-match AI opponent scaled to the player's current Empire progression.
    ///
    /// This replaces the previous arrangement where the enemy was pinned at Avatar 1 / Castle 1
    /// forever (GameBootstrap) while the player gained Avatar levels per win - which meant the
    /// game got monotonically *easier* the longer it was played, the concrete "it's unbalanced"
    /// problem this system exists to solve.
    ///
    /// Adapted from an external design spec. Three fields from that spec are represented on
    /// AIBattleProfile but deliberately left unapplied, because each depends on a system this
    /// game does not have:
    ///
    /// - CardStatMultiplier: there is no card-stat scaling mechanism, and adding one cuts against
    ///   a documented design rule (Game Mechanics v2 Part II §3's "Integer Model" - card
    ///   Attack/Health stay small whole numbers, 1-12, so board math is computable in the
    ///   player's head). A 1.45x multiplier turns a 3-Attack card into 4.35. Whether to break
    ///   that rule for AI difficulty is a design call, not a coding one, so this is left as an
    ///   explicit unapplied hook rather than silently changed.
    /// - SkillTriggerBonus: every class hook in this game is deterministic (Knight always taunts,
    ///   Strategist always draws on play, Perfect draws in the Back lane). There is no trigger
    ///   *chance* anywhere to give a bonus to.
    /// - DecisionDelaySeconds: the AI runs synchronously inside the End Turn handler. Simulated
    ///   think-time needs a coroutine, which only runs in Play Mode and therefore can't be
    ///   covered by this project's EditMode test suite.
    ///
    /// What IS applied - and is enough to fix the imbalance on its own - is HP and Resource
    /// scaling relative to the player, plus archetype-driven placement (see SimpleAIOpponent),
    /// which changes how the AI actually plays rather than just how big its numbers are.
    /// </summary>
    public class SoloAIScalingSystem
    {
        // Avatar-health ratio applied to the player's own HP pool, per tier. Below 1.0 the AI is
        // handicapped; above 1.0 it out-bulks the player and the match runs longer.
        private const float NoviceHpRatio = 0.85f;
        private const float ApprenticeHpRatio = 1.00f;
        private const float VeteranHpRatio = 1.15f;
        private const float MasterHpRatio = 1.30f;
        private const float TitanHpRatio = 1.50f;

        // Resource ratios stay in a much tighter band than HP on purpose: resource directly
        // controls how many cards hit the board per turn, and the board is hard-capped at 3 lanes
        // x LaneState.MaxSlots. Handing the AI a large resource edge mostly wastes it against
        // that cap, whereas an HP edge always translates into a longer match.
        private const float NoviceResourceRatio = 0.90f;
        private const float ApprenticeResourceRatio = 1.00f;
        private const float VeteranResourceRatio = 1.05f;
        private const float MasterResourceRatio = 1.10f;
        private const float TitanResourceRatio = 1.20f;

        /// <summary>
        /// Builds an opponent profile scaled to the supplied player progression. `playerEmpire`
        /// must already have had InitializeTCGModifiers() called on it - this reads its derived
        /// values, it does not recompute them.
        /// </summary>
        public AIBattleProfile GenerateAIOpponent(PlayerEmpireData playerEmpire,
            AIArchetype archetype = AIArchetype.Balanced)
        {
            if (playerEmpire == null) throw new ArgumentNullException(nameof(playerEmpire));

            AIDifficultyTier tier = DetermineTier(playerEmpire.AvatarLevel);

            int playerHealth = playerEmpire.StartingAvatarHealth;
            int playerResourceCap = playerEmpire.ResourceCap;

            int scaledHealth = Mathf.RoundToInt(playerHealth * GetHpRatio(tier));
            int scaledResourceCap = Mathf.RoundToInt(playerResourceCap * GetResourceRatio(tier));

            return new AIBattleProfile
            {
                DisplayName = GetOpponentName(tier),
                DifficultyTier = tier,
                Archetype = archetype,
                MaxAvatarHealth = Mathf.Max(1, scaledHealth),
                StartingResourceCap = Mathf.Max(1, scaledResourceCap),
                // Matches the player's own Turn-1 fraction (PlayerEmpireData.Turn1ResourceFraction
                // = 0.6). Production match construction in GameBootstrap currently passes the
                // player's exact Turn1Resource for both sides (AI formation-resource parity), so
                // this field is the profile's documented baseline rather than a second economy.
                Turn1Resource = Mathf.Max(1, Mathf.RoundToInt(scaledResourceCap * 0.6f)),
            };
        }

        /// <summary>Maps an Avatar level onto its difficulty band. See AIDifficultyTier.</summary>
        public AIDifficultyTier DetermineTier(int avatarLevel)
        {
            if (avatarLevel <= 10) return AIDifficultyTier.Novice;
            if (avatarLevel <= 25) return AIDifficultyTier.Apprentice;
            if (avatarLevel <= 50) return AIDifficultyTier.Veteran;
            if (avatarLevel <= 80) return AIDifficultyTier.Master;
            return AIDifficultyTier.Titan;
        }

        private static float GetHpRatio(AIDifficultyTier tier) => tier switch
        {
            AIDifficultyTier.Novice => NoviceHpRatio,
            AIDifficultyTier.Apprentice => ApprenticeHpRatio,
            AIDifficultyTier.Veteran => VeteranHpRatio,
            AIDifficultyTier.Master => MasterHpRatio,
            AIDifficultyTier.Titan => TitanHpRatio,
            _ => 1f,
        };

        private static float GetResourceRatio(AIDifficultyTier tier) => tier switch
        {
            AIDifficultyTier.Novice => NoviceResourceRatio,
            AIDifficultyTier.Apprentice => ApprenticeResourceRatio,
            AIDifficultyTier.Veteran => VeteranResourceRatio,
            AIDifficultyTier.Master => MasterResourceRatio,
            AIDifficultyTier.Titan => TitanResourceRatio,
            _ => 1f,
        };

        /// <summary>
        /// A themed opponent name per tier, so the difficulty band is legible in the HUD instead
        /// of every match being against a generic "Enemy". Deterministic per tier (not random) so
        /// a given progression point always faces a consistently-named opponent - a randomised
        /// name would make it look like a different opponent each match when nothing had changed.
        /// </summary>
        public static string GetOpponentName(AIDifficultyTier tier) => tier switch
        {
            AIDifficultyTier.Novice => "Border Scout",
            AIDifficultyTier.Apprentice => "Ironclad Sentry",
            AIDifficultyTier.Veteran => "Wyvern Tamer Kaelen",
            AIDifficultyTier.Master => "High Warlord Andras",
            AIDifficultyTier.Titan => "Ancient Titan Lord",
            _ => "Enemy",
        };
    }
}
