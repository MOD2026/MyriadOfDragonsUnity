using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Cards
{
    /// <summary>
    /// A card's synergy affinity. Squads that stack matching tags get a formation bonus (see
    /// <see cref="FormationSynergy"/>), which is what makes *which* cards you field matter
    /// beyond raw Attack + Health.
    ///
    /// Adapted from an external spec. Two things from that spec are deliberately not here:
    /// per-card skill XP/levelling and duplicate fusion (level caps 25 -> 100). Both need a
    /// collection and a save system to mean anything - there is no persistence in this project
    /// yet, so a card's level would reset every time the game restarted. They belong with the
    /// collection/meta layer, not the battle layer.
    /// </summary>
    public enum SkillTag
    {
        None,
        TitanSlayer,
        ElementalSurge,
        AegisGuard,
        TacticalCommand,
        VenomStrike,
        DivineHeal,
    }

    /// <summary>
    /// What a squad's tag composition grants. These are flat stat bonuses applied to every
    /// deployed unit at the moment formation locks.
    ///
    /// The source spec returned `GlobalSkillTriggerBonus` (+% skill trigger chance) and
    /// `UltimateMeterGainMultiplier` instead. Neither exists in this game: every class hook is
    /// deterministic (Knight always taunts, Strategist always draws) so there is no trigger
    /// *chance* to raise, and there is no Ultimate meter at all. A percentage bonus on nothing
    /// is invisible, so this converts the same idea into flat Attack/Health - which is both
    /// visible on the board and consistent with the Integer Model (Part II §3), where a
    /// percentage on a 3-Attack card would round away to nothing anyway.
    /// </summary>
    public readonly struct SynergyBonus
    {
        public readonly int AttackBonus;
        public readonly int HealthBonus;
        public readonly IReadOnlyList<SkillTag> ActiveTags;

        public SynergyBonus(int attackBonus, int healthBonus, IReadOnlyList<SkillTag> activeTags)
        {
            AttackBonus = attackBonus;
            HealthBonus = healthBonus;
            ActiveTags = activeTags;
        }

        public bool HasAny => AttackBonus > 0 || HealthBonus > 0;

        public static SynergyBonus None => new SynergyBonus(0, 0, new List<SkillTag>());
    }

    /// <summary>
    /// Formation synergy: field 2+ cards sharing a tag and the whole squad gets stronger.
    /// </summary>
    public static class FormationSynergy
    {
        // Thresholds mirror the source spec's 2-of-a-kind / 3-of-a-kind split. Magnitudes are
        // sized for the Integer Model: +1 Attack is already a ~10-25% swing on a real card, so
        // these are deliberately small numbers rather than the spec's percentages.
        private const int PairAttackBonus = 1;
        private const int TrioAttackBonus = 2;
        private const int TrioHealthBonus = 1;

        /// <summary>
        /// A card's tag, derived from its Class and Element rather than stored per card. The
        /// card data (card_data.json) has no tag field and hand-authoring one for 85 cards would
        /// be guesswork; deriving it means every card has a coherent tag immediately and the
        /// mapping stays in one readable place. If tags are ever authored per card, this is the
        /// single method to replace.
        /// </summary>
        public static SkillTag TagFor(Card card)
        {
            return card.Class switch
            {
                CardClass.Knight => SkillTag.AegisGuard,        // the defensive/Taunt identity
                CardClass.Strategist => SkillTag.TacticalCommand, // the draw/tempo identity
                CardClass.Perfect => SkillTag.TitanSlayer,      // the flexible high-end identity
                _ => card.Element switch                        // Warriors split by element
                {
                    CardElement.Andras => SkillTag.DivineHeal,
                    CardElement.Ktini => SkillTag.VenomStrike,
                    _ => SkillTag.ElementalSurge,
                },
            };
        }

        /// <summary>
        /// Evaluates a deployed squad. Bonuses from different tags stack, so a squad running two
        /// separate pairs is rewarded for both - the intent is to reward deliberate composition,
        /// not to force every card into a single tag.
        /// </summary>
        public static SynergyBonus Calculate(IEnumerable<Card> squad)
        {
            if (squad == null) return SynergyBonus.None;

            var counts = squad
                .Select(TagFor)
                .Where(t => t != SkillTag.None)
                .GroupBy(t => t)
                .ToDictionary(g => g.Key, g => g.Count());

            int attack = 0;
            int health = 0;
            var active = new List<SkillTag>();

            foreach (KeyValuePair<SkillTag, int> entry in counts)
            {
                if (entry.Value >= 3)
                {
                    attack += TrioAttackBonus;
                    health += TrioHealthBonus;
                    active.Add(entry.Key);
                }
                else if (entry.Value == 2)
                {
                    attack += PairAttackBonus;
                    active.Add(entry.Key);
                }
            }

            return new SynergyBonus(attack, health, active);
        }

        /// <summary>Short human-readable summary for the HUD, e.g. "AegisGuard, VenomStrike:
        /// +2 ATK / +1 HP". Empty when no synergy is active.</summary>
        public static string Describe(SynergyBonus bonus)
        {
            if (!bonus.HasAny) return string.Empty;

            string tags = string.Join(", ", bonus.ActiveTags);
            var parts = new List<string>();
            if (bonus.AttackBonus > 0) parts.Add($"+{bonus.AttackBonus} ATK");
            if (bonus.HealthBonus > 0) parts.Add($"+{bonus.HealthBonus} HP");
            return $"{tags}: {string.Join(" / ", parts)}";
        }
    }
}
