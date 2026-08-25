using System.Collections.Generic;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// Suppressible triggered-ability package (LOCKED 2026-08-25, GPT, register commit 2b54084) -
    /// four existing cards each get one once-per-unit-per-match passive, magnitude 1. Keyed by
    /// <see cref="MyriadOfDragons.Cards.Card"/>.Id specifically, not CardClass - Cleric and Novice
    /// Knight are both Knight-class but get two different abilities, so a class-keyed lookup
    /// (the existing pattern in BattleController.ApplyOnPlayClassHook for Strategist/Perfect)
    /// would not distinguish them.
    /// </summary>
    public enum CardTriggerAbility
    {
        None,

        /// <summary>Goblin Caster, "Hex Spark": first clash this unit is alive for, deal 1 damage
        /// to a living opposing unit in the same lane.</summary>
        HexSpark,

        /// <summary>Cleric, "Battle Mend": after this unit's first clash, if a friendly unit in
        /// the same lane was damaged during that clash, restore 1 HP to the most-damaged one.</summary>
        BattleMend,

        /// <summary>Novice Knight, "Shield Discipline": this unit's first incoming damage this
        /// match is reduced by 1 (floored at 0, never negative).</summary>
        ShieldDiscipline,

        /// <summary>Phoenix, "Ash Rebirth": the first time this unit would be defeated, it stays
        /// at 1 HP instead - once per match.</summary>
        AshRebirth,
    }

    public static class CardTriggerAbilities
    {
        private static readonly Dictionary<string, CardTriggerAbility> ByCardId = new Dictionary<string, CardTriggerAbility>
        {
            ["goblin_caster"] = CardTriggerAbility.HexSpark,
            ["cleric"] = CardTriggerAbility.BattleMend,
            ["novice_knight"] = CardTriggerAbility.ShieldDiscipline,
            ["phoenix"] = CardTriggerAbility.AshRebirth,
        };

        /// <summary>None for every card outside the four named in the locked package - the same
        /// honest "no Rule = nothing happens" pattern used elsewhere in this catalog, not a
        /// missing-data error.</summary>
        public static CardTriggerAbility For(string cardId) =>
            !string.IsNullOrEmpty(cardId) && ByCardId.TryGetValue(cardId, out CardTriggerAbility ability)
                ? ability
                : CardTriggerAbility.None;
    }
}
