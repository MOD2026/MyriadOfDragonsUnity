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

        // MVP constitution 2026-08-21: +1 Avatar level per win, +0 on loss.
        // The old +3/+1 placeholders jumped AI difficulty tiers every couple of fights and
        // rewarded losses with free power. Castle/Barracks still do not advance from matches
        // in MVP - only AvatarLevel moves (see docs/MVP_COMBAT_PROGRESSION_CONSTITUTION_2026-08-21.md).
        private const int AvatarLevelsGainedPerWin = 1;
        private const int AvatarLevelsGainedPerLoss = 0;

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

        /// <summary>Starting (Turn 1 / Formation) match resource - a fraction of
        /// <see cref="ResourceCap"/> (see Turn1ResourceFraction). Ramps +1/turn after that up
        /// to the cap. Kept below the full cap so Formation is a real choice: leftover Resource
        /// funds reinforcement windows (ticks 4 and 8), and first-tick overflow cannot one-shot
        /// a full dump of both boards.</summary>
        public int Turn1Resource => _turn1Resource;

        /// <summary>Match-starting Avatar HP, from a base plus Avatar-level and Castle-level
        /// bonuses - a fresh level-1 player's 20 HP was reported as too fragile once matches
        /// have real resource/deck depth behind them; this scales the same way ResourceCap does.</summary>
        public int StartingAvatarHealth => _startingAvatarHealth;

        /// <summary>Multiplier on how fast "Grip of Titans" stamina regenerates between matches.</summary>
        public float ResourceRegenRate => _resourceRegenRate;

        /// <summary>Fraction (0-1) of lost card HP/soldier count restored after a match ends.</summary>
        public float PostMatchReplenishRate => _postMatchReplenishRate;

        // Deck slots: paid Barracks milestones only (docs/EMPIRE_SCHEMA_LOCK_2026-08-22.md).
        // The old ÷5 formula reached 20 slots at Barracks 50 and granted empty +1 levels
        // (2, 3, 4…) that construction must never sell. Lookup, not arithmetic.
        private static readonly int[] PaidBarracksMilestones = { 1, 5, 10, 15, 20, 25, 30 };
        private static readonly int[] DeckSlotsAtPaidMilestone = { 10, 11, 12, 14, 16, 18, 20 };
        // Gold to BUY that milestone from the previous paid tier (L1 is free start).
        // Pacing vs Campaign first-clear Gold (~6.2k Ch1, ~155k through Ch3, ~650k through Ch5,
        // ~2.2M through Ch8): early tiers after Ch1–2; L30 needs late-campaign Gold and must
        // compete with Castle/Gate sinks — Barracks-only dump still cannot finish before deep Ch5.
        private static readonly int[] GoldCostAtPaidMilestone =
        {
            0,       // L1 start
            1_200,   // → L5
            5_000,   // → L10
            15_000,  // → L15
            40_000,  // → L20
            100_000, // → L25
            250_000, // → L30
        };
        private const int MaxDeckSlotCount = 20;

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

        /// <summary>Hard cap for Castle building level (Empire construction + cost tables).</summary>
        public const int MaxCastleLevel = 30;
        // Restored to 0.6 on 2026-08-21 (Chapter 1 systemic combat-balance audit).
        // The 1.0 "full cap from Turn 1" experiment let both sides dump near-full boards in
        // Formation; with a full Turn-1 dump that produced 1-tick Chapter 1 clears under a
        // full legal formation and erased reinforcement / spell decision space. 0.6 matches the
        // PlayerBattleState comment contract (~60% of ResourceCap) and the original economy notes
        // in README: Formation spends a real budget, leftovers fund ticks 4/8 reinforcements, and
        // combat length returns to the BalanceSimulationTargets band without retuning individual
        // Chapter 1 enemy rosters.
        public const float Turn1ResourceFraction = 0.6f;

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

        // Barracks no longer scales unused regen/replenish. Those rates are not match-read
        // today; tying them to Barracks level would be fake depth when the building's only
        // shipped combat effect is deck slots.
        private const float BaseResourceRegenRate = 1f;
        private const float BasePostMatchReplenishRate = 0.5f;

        /// <summary>
        /// Deck slots granted by a stored Barracks level. Interstitial levels keep the last
        /// paid milestone's slots; levels past 30 stay capped at 20.
        /// </summary>
        public static int DeckSlotsForBarracksLevel(int barracksLevel)
        {
            int level = Mathf.Max(1, barracksLevel);
            int slots = DeckSlotsAtPaidMilestone[0];
            for (int i = 0; i < PaidBarracksMilestones.Length; i++)
            {
                if (level >= PaidBarracksMilestones[i])
                    slots = DeckSlotsAtPaidMilestone[i];
                else
                    break;
            }

            return Mathf.Min(MaxDeckSlotCount, slots);
        }

        /// <summary>
        /// Next construction target. Never current+1. 0 means Barracks is already at the L30 cap.
        /// </summary>
        public static int NextPaidBarracksMilestone(int currentBarracksLevel)
        {
            int current = Mathf.Max(1, currentBarracksLevel);
            for (int i = 0; i < PaidBarracksMilestones.Length; i++)
            {
                if (PaidBarracksMilestones[i] > current)
                    return PaidBarracksMilestones[i];
            }

            return 0;
        }

        // Gate = meta-only chapter route clearance (EMPIRE_SCHEMA_LOCK). Necessary ≠ sufficient:
        // Campaign still requires stage unlock (+ later Avatar/collection/mini-game gates).
        private static readonly int[] GateLevelForChapter =
        {
            0,  // unused index 0
            1,  // Ch1
            3,  // Ch2
            6,  // Ch3
            9,  // Ch4
            12, // Ch5
            15, // Ch6
            18, // Ch7
            21, // Ch8
            24, // Ch9
            27, // Ch10
            30, // Ch11 — The Storm's Price
            30, // Ch12 — The Mortal Host (Gate ladder caps at L30 until a higher paid milestone lands)
        };

        /// <summary>
        /// Highest Campaign chapter (1–12) this Gate level may attempt.
        /// </summary>
        public static int GetHighestCampaignChapterAllowed(int gateLevel)
        {
            int level = Mathf.Max(1, gateLevel);
            int highest = 1;
            for (int chapter = 1; chapter <= 12; chapter++)
            {
                if (level >= GateLevelForChapter[chapter])
                    highest = chapter;
                else
                    break;
            }

            return highest;
        }

        /// <summary>True if Gate alone permits this chapter. Does not check stage unlock.</summary>
        public static bool IsCampaignChapterAllowedByGate(int gateLevel, int chapter)
        {
            if (chapter < 1 || chapter > 12)
                return false;
            return gateLevel >= GateLevelForChapter[chapter];
        }

        /// <summary>Block W.1: read-only lookup for test/harness setup - the exact Gate level a
        /// profile needs to reach before <see cref="IsCampaignChapterAllowedByGate"/> allows this
        /// chapter. Returns 0 for an out-of-range chapter (no valid minimum). Does not change
        /// GateLevelForChapter itself.</summary>
        public static int MinimumGateLevelForChapter(int chapter)
        {
            if (chapter < 1 || chapter > 12)
                return 0;
            return GateLevelForChapter[chapter];
        }

        /// <summary>
        /// Minimum Castle level required before a Gate upgrade to <paramref name="targetGateLevel"/>
        /// may start (feasibility ladder). Returns 0 if target is not a Gate route milestone.
        /// </summary>
        public static int MinimumCastleForGateLevel(int targetGateLevel)
        {
            switch (targetGateLevel)
            {
                case 1: return 1;
                case 3: return 5;
                case 6: return 10;
                case 9: return 15;
                case 12: return 20;
                case 15: return 22;
                case 18: return 24;
                case 21: return 26;
                case 24: return 28;
                case 27: return 30;
                case 30: return 30;
                default: return 0;
            }
        }

        /// <summary>
        /// Minimum Castle level required before a Barracks upgrade to
        /// <paramref name="targetBarracksLevel"/> may start (feasibility ladder). Real curve locked
        /// 2026-08-24 (GPT round, closes the "Open, real" Castle-interlock gap in
        /// docs/LOCKED_DECISIONS_REGISTER.md's Empire construction section) - lighter/lower than
        /// Gate's own curve, using only Barracks' existing purchasable milestones (1/5/10/15/20/25/
        /// 30). Returns 0 if target is not one of those milestones - same sparse/breakpoint-only
        /// shape as MinimumCastleForGateLevel, not a full 1-30 curve.
        /// </summary>
        public static int MinimumCastleForBarracksLevel(int targetBarracksLevel)
        {
            switch (targetBarracksLevel)
            {
                case 1: return 1;
                case 5: return 3;
                case 10: return 7;
                case 15: return 12;
                case 20: return 18;
                case 25: return 24;
                case 30: return 30;
                default: return 0;
            }
        }

        // Gate Gold — purchasable milestones only (CASTLE_GATE_GOLD_CC_ACCEPT_2026-08-22).
        private static readonly int[] PaidGateMilestones = { 3, 6, 9, 12, 15, 18, 21, 24, 27, 30 };
        private static readonly int[] GoldCostAtPaidGateMilestone =
        {
            1_900, 7_000, 12_000, 20_000, 35_000, 55_000, 80_000, 110_000, 140_000, 180_000
        };

        /// <summary>Next purchasable Gate milestone above current. 0 if at L30 cap.</summary>
        public static int NextPaidGateMilestone(int currentGateLevel)
        {
            int current = Mathf.Max(1, currentGateLevel);
            for (int i = 0; i < PaidGateMilestones.Length; i++)
            {
                if (PaidGateMilestones[i] > current)
                    return PaidGateMilestones[i];
            }

            return 0;
        }

        /// <summary>
        /// Gold to start upgrade to <paramref name="targetGateMilestone"/>. 0 unless target is
        /// exactly the next paid Gate milestone.
        /// </summary>
        public static int GoldCostForGateUpgrade(int currentGateLevel, int targetGateMilestone)
        {
            int next = NextPaidGateMilestone(currentGateLevel);
            if (next == 0 || targetGateMilestone != next)
                return 0;

            for (int i = 0; i < PaidGateMilestones.Length; i++)
            {
                if (PaidGateMilestones[i] == targetGateMilestone)
                    return GoldCostAtPaidGateMilestone[i];
            }

            return 0;
        }

        // Castle Gold L2–L30 (CASTLE_GATE_GOLD — publish gate passed 2026-08-22).
        // Index 0 unused; index N = Gold to buy Castle level N from N-1.
        private static readonly int[] GoldCostToReachCastleLevel =
        {
            0,
            0,       // L1 start
            250, 400, 550, 750,                 // L2–L5
            1_100, 1_400, 1_800, 2_200, 2_800, // L6–L10
            4_000, 5_000, 6_200, 7_500, 9_000, // L11–L15
            12_000, 14_500, 17_000, 20_000, 24_000, // L16–L20
            28_000, 28_000, 34_000, 40_000, 50_000, // L21–L25
            60_000, 70_000, 82_000, 95_000, 110_000 // L26–L30
        };

        /// <summary>Gold to raise Castle from current to current+1. 0 at cap or invalid.</summary>
        public static int GoldCostForCastleUpgrade(int currentCastleLevel)
        {
            int current = Mathf.Max(1, currentCastleLevel);
            int target = current + 1;
            if (target < 2 || target > MaxCastleLevel)
                return 0;
            return GoldCostToReachCastleLevel[target];
        }

        /// <summary>Total Gold to climb Castle from <paramref name="currentCastleLevel"/> to max.</summary>
        public static int RemainingGoldToMaxCastle(int currentCastleLevel)
        {
            int total = 0;
            int level = Mathf.Max(1, currentCastleLevel);
            while (level < MaxCastleLevel)
            {
                int cost = GoldCostForCastleUpgrade(level);
                if (cost <= 0)
                    break;
                total += cost;
                level++;
            }

            return total;
        }

        /// <summary>
        /// Gold charged to start the upgrade that lands on <paramref name="targetMilestone"/>.
        /// 0 if the target is not a paid milestone above the current level.
        /// </summary>
        public static int GoldCostForBarracksUpgrade(int currentBarracksLevel, int targetMilestone)
        {
            int next = NextPaidBarracksMilestone(currentBarracksLevel);
            if (next == 0 || targetMilestone != next)
                return 0;

            for (int i = 0; i < PaidBarracksMilestones.Length; i++)
            {
                if (PaidBarracksMilestones[i] == targetMilestone)
                    return GoldCostAtPaidMilestone[i];
            }

            return 0;
        }

        /// <summary>Total Gold to climb from <paramref name="currentBarracksLevel"/> to L30.</summary>
        public static int RemainingGoldToMaxBarracks(int currentBarracksLevel)
        {
            int total = 0;
            int level = Mathf.Max(1, currentBarracksLevel);
            while (true)
            {
                int next = NextPaidBarracksMilestone(level);
                if (next == 0)
                    return total;

                total += GoldCostForBarracksUpgrade(level, next);
                level = next;
            }
        }

        /// <summary>
        /// Recomputes every meta-progression value this Empire currently grants. Call after
        /// loading a save, after a building/Avatar finishes leveling, or before entering a
        /// match so deck-building and match economy reflect the player's current levels.
        /// </summary>
        public void InitializeTCGModifiers()
        {
            _deckSlotCount = DeckSlotsForBarracksLevel(_barracksLevel);

            int avatarResourceBonus = Mathf.Min(MaxAvatarResourceBonus, (_avatarLevel / LevelsPerResourceTier) * ResourceBonusPerTier);
            int castleResourceBonus = CastleResourceBonusForLevel(_castleLevel);
            _resourceCap = BaseResourceCap + avatarResourceBonus + castleResourceBonus;
            _turn1Resource = Mathf.RoundToInt(_resourceCap * Turn1ResourceFraction);

            int avatarHealthBonus = Mathf.Min(MaxAvatarHealthBonus,
                (_avatarLevel / LevelsPerResourceTier) * HealthBonusPerTier);
            int castleHealthBonus = CastleHealthBonusForLevel(_castleLevel);
            _startingAvatarHealth = BaseAvatarHealth + avatarHealthBonus + castleHealthBonus
                + OnboardingBonusFor(_avatarLevel);

            _resourceRegenRate = BaseResourceRegenRate;
            _postMatchReplenishRate = BasePostMatchReplenishRate;
        }

        /// <summary>Castle-only Resource Cap contribution at a given Castle level (Empire UI payoff).
        /// Block AA fix: input is clamped to [1, MaxCastleLevel] - the per-tier formula's own Min
        /// cap already coincided with level 30 for Resource, but the clamp is made explicit here
        /// too so both readers are symmetric and robust to an out-of-range input (Castle can never
        /// exceed MaxCastleLevel via EmpireConstructionRules; no in-range output changes).</summary>
        public static int CastleResourceBonusForLevel(int castleLevel)
        {
            int level = Mathf.Clamp(castleLevel, 1, MaxCastleLevel);
            return Mathf.Min(MaxCastleResourceBonus, (level / LevelsPerResourceTier) * ResourceBonusPerTier);
        }

        /// <summary>Castle-only starting Avatar HP contribution at a given Castle level (Empire UI payoff).
        /// Block AA fix: input clamped to [1, MaxCastleLevel] - MaxCastleHealthBonus (210) sat
        /// above what the per-tier formula actually reaches at level 30 (120), so without this
        /// clamp an out-of-range level above 30 could silently produce more HP bonus than the
        /// real maximum level - unreachable in production, but a real reader defect this
        /// relationship test caught. Level 30's own bonus is unchanged (still 120).</summary>
        public static int CastleHealthBonusForLevel(int castleLevel)
        {
            int level = Mathf.Clamp(castleLevel, 1, MaxCastleLevel);
            return Mathf.Min(MaxCastleHealthBonus, (level / LevelsPerResourceTier) * HealthBonusPerTier);
        }
    }
}
