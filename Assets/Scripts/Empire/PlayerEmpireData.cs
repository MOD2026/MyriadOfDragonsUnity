using UnityEngine;

namespace MyriadOfDragons.Empire
{
    /// <summary>
    /// A player's Empire (base-building) progression, and the meta-progression values it
    /// derives for TCG play - deck capacity, match-economy pacing, and post-match replenishment.
    ///
    /// Per Game Mechanics v2, Part II §4, Barracks/Gate combat *multipliers* were originally
    /// pulled out of match logic entirely, and Empire levels deliberately did not modify a
    /// card's live-match stats. That still holds for individual cards (no Barracks/Gate combat
    /// multiplier chain - stat math stays small, integer, and mentally computable mid-match).
    /// It no longer holds for Avatar/Castle progression's effect on the *match economy itself*
    /// (ResourceCap, StartingAvatarHealth) - see the 2026-08-05 notes below, both explicitly
    /// requested directly: a level-1 and a leveled-up player should not play with the same
    /// resource ceiling or the same Avatar Health pool.
    ///
    /// 2026-08-05: rescaled deck slots from a 30+ baseline to 10-20 (small-deck direction,
    /// closer to Marvel Snap's 12-card decks than a traditional 30-40 card TCG), and added
    /// AvatarLevel + ResourceCap/StartingAvatarHealth formulas - both deliberately split across
    /// two separate, separately-capped tracks (Avatar = battle mastery, earned by playing/
    /// winning matches; Castle = economic investment, earned by base-building) rather than one
    /// number, so investing in either track alone can't trivialize the game, and rebalancing
    /// one later doesn't require touching the other.
    /// </summary>
    [System.Serializable]
    public class PlayerEmpireData
    {
        [Header("Progression Levels")]
        [SerializeField] private int _avatarLevel = 1;
        [SerializeField] private int _castleLevel = 1;
        [SerializeField] private int _barracksLevel = 1;
        [SerializeField] private int _gateLevel = 1;

        public int AvatarLevel => _avatarLevel;
        public int CastleLevel => _castleLevel;
        public int BarracksLevel => _barracksLevel;
        public int GateLevel => _gateLevel;

        /// <summary>
        /// Sets all four progression tracks at once. Follow with InitializeTCGModifiers() to
        /// recompute the derived values - this deliberately does not do it for you, because
        /// loading a profile sets levels and applies items before the derived values are wanted,
        /// and recomputing after each step would be wasted work at best and half-applied at worst.
        /// </summary>
        public void SetLevels(int avatarLevel, int castleLevel, int barracksLevel, int gateLevel = 1)
        {
            _avatarLevel = avatarLevel;
            _castleLevel = castleLevel;
            _barracksLevel = barracksLevel;
            _gateLevel = gateLevel;
        }

        /// <summary>
        /// Historical name from when there was no save system and every caller setting levels was
        /// a test or a hardcoded profile. Real progression now loads through
        /// MyriadOfDragons.Save.PlayerProfile; this remains only so existing tests and the
        /// GameBootstrap debug profile keep reading honestly about what they are.
        /// </summary>
        public void SetLevelsForTesting(int avatarLevel, int castleLevel, int barracksLevel, int gateLevel = 1)
            => SetLevels(avatarLevel, castleLevel, barracksLevel, gateLevel);

        // How many Avatar levels a match win/loss grants - the concrete "how do I get stronger
        // and beat the Avatar" loop that was missing entirely: winning genuinely made the next
        // match easier (higher ResourceCap/HP), losing didn't compound the difficulty further.
        // Placeholder numbers (not sourced from the design doc) - real XP curves/loss-forgiveness
        // rules are a design decision for later, this just makes the loop exist and be testable.
        private const int AvatarLevelsGainedPerWin = 3;
        private const int AvatarLevelsGainedPerLoss = 1;

        /// <summary>Call after a match ends, then InitializeTCGModifiers() to apply the new
        /// level to the next match. Only persists for the current Play session - see the note
        /// on SetLevelsForTesting; there's no save system yet for this to survive a restart.</summary>
        public void ApplyMatchResult(bool won)
        {
            _avatarLevel += won ? AvatarLevelsGainedPerWin : AvatarLevelsGainedPerLoss;
        }

        [Header("Derived Meta-Progression (recomputed by InitializeTCGModifiers)")]
        [SerializeField] private int _deckSlotCount;
        [SerializeField] private int _resourceCap;
        [SerializeField] private int _turn1Resource;
        [SerializeField] private int _startingAvatarHealth;
        [SerializeField] private float _resourceRegenRate;
        [SerializeField] private float _postMatchReplenishRate;

        /// <summary>Total deck slots available (10-20), from Barracks milestones.</summary>
        public int DeckSlotCount => _deckSlotCount;

        /// <summary>Max match resource, from a base plus Avatar-level and Castle-level bonuses.</summary>
        public int ResourceCap => _resourceCap;

        /// <summary>Starting (Turn 1) match resource - full ResourceCap per direct request
        /// (2026-08-05), so a match is playable at full strength from the first turn rather
        /// than ramping up into it.</summary>
        public int Turn1Resource => _turn1Resource;

        /// <summary>Match-starting Avatar HP, from a base plus Avatar-level and Castle-level
        /// bonuses - a fresh level-1 player's 20 HP was reported as too fragile once matches
        /// have real resource/deck depth behind them; this scales the same way ResourceCap does.</summary>
        public int StartingAvatarHealth => _startingAvatarHealth;

        /// <summary>Multiplier on how fast "Grip of Titans" stamina regenerates between matches.</summary>
        public float ResourceRegenRate => _resourceRegenRate;

        /// <summary>Fraction (0-1) of lost card HP/soldier count restored after a match ends.</summary>
        public float PostMatchReplenishRate => _postMatchReplenishRate;

        // Deck slots: 10 (new player) up to 20, +1 per 5 Barracks levels (reaches 20 at level 50).
        private const int BaseDeckSlotCount = 10;
        private const int MaxDeckSlotCount = 20;
        private const int BarracksLevelsPerDeckSlotTier = 5;

        // Resource cap rescaled 2026-08-05 alongside Avatar HP - the two had drifted out of
        // proportion (HP went to the hundreds/thousands, Resource stayed at 8-20), and a
        // mid-level player correctly pointed out that a big HP pool with a small resource cap
        // meant they still couldn't afford to play most of a full hand. Card costs (1-7) are
        // unchanged - deck size (10-20 cards) and lane space (3x3) are the real strategic
        // constraints now, not resource, matching how Marvel Snap's mana isn't usually the
        // bottleneck either. 20 at level 1, ~60 at the mid-test profile, ~80 at max.
        private const int BaseResourceCap = 20;
        private const int LevelsPerResourceTier = 5;
        private const int ResourceBonusPerTier = 5;
        private const int MaxAvatarResourceBonus = 30; // reached at Avatar level 30
        private const int MaxCastleResourceBonus = 30; // reached at Castle level 30
        private const float Turn1ResourceFraction = 1.0f; // full cap from Turn 1, per direct request

        // Starting Avatar Health. Card combat stats (Attack/Health 1-12) are deliberately
        // UNCHANGED - still the small, mentally-computable Integer Model from Part II §3. What
        // connects the HP pool to small per-card numbers is
        // LaneBattleResolver.AvatarDamageMultiplier; see that constant's comment.
        //
        // Rescaled DOWN 2026-08-06, from 100/1300/1900 to 120/400/540. The previous pool was
        // roughly 4x too large for the damage the board actually produces, and the consequence
        // was measured rather than guessed: simulating 4,000 matches against the shipped combat
        // maths, only 23% ended with an Avatar actually falling - 77% ran out the 12-tick clock
        // and were settled by comparing Health percentages.
        //
        // Worse, it degraded with progression: at the max profile (1900 HP) 95% of matches were
        // decided on the clock rather than won, so the further a player advanced the less
        // decisive their matches became. The escalating damage multiplier was meant to force
        // conclusions and could not, because the pool it was fighting was too deep.
        //
        // Revised again 2026-08-06 after moving the simulation INSIDE Unity, driving the real
        // combat code (see BalanceSimulationTests) instead of an external replica. The replica
        // predicted ~89% knockouts at 400 HP; the real game produced ~50%, reproducibly across
        // three runs. The replica was wrong because it filled all nine board slots while
        // ignoring Resource cost - real boards are smaller, deal less damage, and run longer.
        //
        // That gap is the entire argument for simulating in-engine: a model of the combat maths
        // drifts from the game the moment it omits a constraint, and it omitted one silently.
        // 100 at level 1, 260 at the mid-test profile (Avatar 25/Castle 15), 340 at max.
        private const int BaseAvatarHealth = 100;
        private const int HealthBonusPerTier = 20;
        private const int MaxAvatarHealthBonus = 120; // reached at Avatar level 30
        private const int MaxCastleHealthBonus = 210; // reached at Castle level 30

        /// <summary>
        /// Extra starting Health for a brand-new player, at full strength only at Avatar level 1
        /// and linearly gone by <see cref="LevelsPerResourceTier"/> - the level the first real
        /// per-level Health tier kicks in. Addresses docs/Mechanics_Gap_Analysis.md section 1.3 (T2c):
        /// a genuine level-1 profile at the base 100 HP resolves matches in ~2.9 ticks
        /// (BalanceSimulationTests.Balance_Level1StartingHealth_SweepForOnboardingViability),
        /// too short for even the cheapest spell (25 Energy at 18/tick) to reliably come off
        /// cooldown before the match is already over. 200 HP measured at that same sweep: 5.1
        /// ticks, 87.0% knockouts (with the siege rule included, since it is on by default) -
        /// real margin for a spell cast without the knockout rate dropping anywhere near the
        /// design floor.
        ///
        /// Deliberately a taper on top of the existing formula, NOT a change to
        /// <see cref="BaseAvatarHealth"/>: Base is additive into every profile, so raising it
        /// directly would have inflated the mid profile 260 -> 360 HP (+38%) and max 340 -> 440
        /// HP (+29%), silently invalidating the siege-rule tuning that was measured and adopted
        /// at the current HP scale (Mechanics_Gap_Analysis.md section 1.1). This bonus is zero from
        /// Avatar level <see cref="LevelsPerResourceTier"/> onward, so it touches nothing past
        /// early onboarding and the mid/max profile numbers are exactly unchanged.
        ///
        /// Tapers on Avatar level only, not Castle: Castle/Barracks/Gate levelling has no way to
        /// be raised yet (see Mechanics_Gap_Analysis.md "Not built"), so every profile that can
        /// currently exist has castleLevel stuck at 1 - Avatar level is the only track that
        /// actually moves, so tapering on it alone covers every real case and avoids inventing a
        /// two-track taper for a track nothing can raise.
        /// </summary>
        private const int OnboardingHealthBonus = 100;

        /// <summary>Linear taper from <see cref="OnboardingHealthBonus"/> at Avatar level 1 down
        /// to 0 at <see cref="LevelsPerResourceTier"/> (level 5 today) - reuses that existing tier
        /// boundary rather than a second hardcoded breakpoint, so retuning tier size can't leave
        /// this taper ending at a level the rest of the formula no longer agrees is "the first
        /// tier".</summary>
        private static int OnboardingBonusFor(int avatarLevel)
        {
            int taperSpan = LevelsPerResourceTier - 1; // levels 1..4 taper, level 5+ is 0
            if (taperSpan <= 0) return 0;

            int progress = Mathf.Clamp(avatarLevel - 1, 0, taperSpan);
            float remaining = 1f - (progress / (float)taperSpan);
            return Mathf.RoundToInt(OnboardingHealthBonus * remaining);
        }

        private const float BaseResourceRegenRate = 1f;
        private const float ResourceRegenPerBarracksLevel = 0.01f;

        private const float BasePostMatchReplenishRate = 0.5f;
        private const float ReplenishPerBarracksLevel = 0.005f;

        /// <summary>
        /// Recomputes every meta-progression value this Empire currently grants. Call after
        /// loading a save, after a building/Avatar finishes leveling, or before entering a
        /// match so deck-building and match economy reflect the player's current levels.
        /// </summary>
        public void InitializeTCGModifiers()
        {
            int deckSlotTiers = _barracksLevel / BarracksLevelsPerDeckSlotTier;
            _deckSlotCount = Mathf.Min(MaxDeckSlotCount, BaseDeckSlotCount + deckSlotTiers);

            int avatarResourceBonus = Mathf.Min(MaxAvatarResourceBonus, (_avatarLevel / LevelsPerResourceTier) * ResourceBonusPerTier);
            int castleResourceBonus = Mathf.Min(MaxCastleResourceBonus, (_castleLevel / LevelsPerResourceTier) * ResourceBonusPerTier);
            _resourceCap = BaseResourceCap + avatarResourceBonus + castleResourceBonus;
            _turn1Resource = Mathf.RoundToInt(_resourceCap * Turn1ResourceFraction);

            int avatarHealthBonus = Mathf.Min(MaxAvatarHealthBonus,
                (_avatarLevel / LevelsPerResourceTier) * HealthBonusPerTier);
            int castleHealthBonus = Mathf.Min(MaxCastleHealthBonus,
                (_castleLevel / LevelsPerResourceTier) * HealthBonusPerTier);
            _startingAvatarHealth = BaseAvatarHealth + avatarHealthBonus + castleHealthBonus
                + OnboardingBonusFor(_avatarLevel);

            _resourceRegenRate = BaseResourceRegenRate + (_barracksLevel * ResourceRegenPerBarracksLevel);

            float rawReplenishRate = BasePostMatchReplenishRate + (_barracksLevel * ReplenishPerBarracksLevel);
            _postMatchReplenishRate = Mathf.Clamp01(rawReplenishRate);
        }
    }
}
