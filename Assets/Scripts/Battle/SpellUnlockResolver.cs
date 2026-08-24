using System.Collections.Generic;
using System.Linq;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// SPELL_CATALOG_v1.md's per-spell "Unlock" column, resolved against real player progress with
    /// zero new Save schema - both inputs this needs (Avatar level, unlocked campaign stage ids)
    /// already exist on PlayerProfile (avatarLevel, unlockedStageIds) and PlayerEmpireData
    /// (AvatarLevel). A catalog row like "Ch1-2" maps directly onto PlayerProfile.
    /// unlockedStageIds' own "1-2" stage-id format (the existing sequential campaign unlock flow -
    /// see PlayerProfile.unlockedStageIds' own doc comment); a row like "Avatar L5" maps onto
    /// avatar level directly.
    ///
    /// Two catalog rows (Sun Lance "Ch2 spell book", Tempest Brand "Ch3 spell book") name an
    /// acquisition method this resolver's own inputs (Avatar level, unlocked stage ids) cannot
    /// see - "do you own the Ch2/Ch3 finale spell book," not a level or a stage. RESOLVED
    /// 2026-08-24: that acquisition is now real, via <see cref="SpellBookGrant"/> and
    /// PlayerProfile.ownedSpellIds (owner-authorized). This resolver still never unlocks those two
    /// itself (their gate genuinely isn't Avatar-level/stage progression, so folding them into
    /// this resolver's Rules would mean guessing a stand-in condition, exactly what this class
    /// used to warn against) - but the class-level claim "nothing tracks this" is no longer true,
    /// which is what <see cref="HasUnresolvableSpellBookGates"/> now reports.
    /// </summary>
    public static class SpellUnlockResolver
    {
        private enum UnlockKind
        {
            Starter,
            Stage,
            AvatarLevel,

            /// <summary>Gated on the Ch2/Ch3 finale Spell Book grant (see SpellBookGrant), a real
            /// but different acquisition channel this resolver's own (Avatar level, stage)
            /// inputs cannot evaluate. Never unlocked BY THIS RESOLVER - not because nothing
            /// tracks it (something now does), but because the resolver genuinely has no way to
            /// check spell-book ownership from just a level and a stage list.</summary>
            SpellBookGrant,
        }

        private class Rule
        {
            public readonly string SpellName;
            public readonly UnlockKind Kind;
            public readonly string StageId;
            public readonly int RequiredAvatarLevel;

            public Rule(string spellName, UnlockKind kind, string stageId = null, int requiredAvatarLevel = 0)
            {
                SpellName = spellName;
                Kind = kind;
                StageId = stageId;
                RequiredAvatarLevel = requiredAvatarLevel;
            }
        }

        /// <summary>SPELL_CATALOG_v1.md's own "Unlock" column for all 14 Phase-1 spells, verbatim.</summary>
        private static readonly List<Rule> Rules = new List<Rule>
        {
            new Rule("Firestorm", UnlockKind.Starter),
            new Rule("Mend", UnlockKind.Starter),
            new Rule("War Cry", UnlockKind.Starter),
            new Rule("Divine Bolt", UnlockKind.Starter),
            new Rule("Cinder Lash", UnlockKind.Stage, stageId: "1-2"),
            new Rule("Ember Wave", UnlockKind.AvatarLevel, requiredAvatarLevel: 5),
            new Rule("Fault Line", UnlockKind.Stage, stageId: "2-4"),
            new Rule("Vital Spark", UnlockKind.Stage, stageId: "1-6"),
            new Rule("Renewal", UnlockKind.Stage, stageId: "2-8"),
            new Rule("Rallying Gale", UnlockKind.AvatarLevel, requiredAvatarLevel: 8),
            new Rule("Banner of Ashes", UnlockKind.Stage, stageId: "3-3"),
            new Rule("Sun Lance", UnlockKind.SpellBookGrant), // catalog: "Ch2 spell book"
            new Rule("Stone Judgment", UnlockKind.AvatarLevel, requiredAvatarLevel: 12),
            new Rule("Tempest Brand", UnlockKind.SpellBookGrant), // catalog: "Ch3 spell book"
        };

        /// <summary>RESOLVED 2026-08-24 - always false now. Kept (rather than deleted outright) as
        /// a permanent regression flag: if this property or its underlying Rule kind is ever used
        /// to mean "this can never become true" again, that would be reintroducing exactly the gap
        /// SpellBookGrant closed. The two SpellBookGrant-kind spells are real and ownable today -
        /// just not unlocked BY THIS RESOLVER (see the class doc comment for why).</summary>
        public static bool HasUnresolvableSpellBookGates => false;

        public static bool IsUnlocked(AvatarSpell spell, int avatarLevel, IReadOnlyCollection<string> unlockedStageIds)
        {
            Rule rule = Rules.FirstOrDefault(r => r.SpellName == spell.Name);
            if (rule == null) return false; // Not a Phase-1 catalog spell at all - never auto-unlocked.

            switch (rule.Kind)
            {
                case UnlockKind.Starter: return true;
                case UnlockKind.Stage: return unlockedStageIds != null && unlockedStageIds.Contains(rule.StageId);
                case UnlockKind.AvatarLevel: return avatarLevel >= rule.RequiredAvatarLevel;
                default: return false; // SpellBookGrant - see SpellBookGrant, not this resolver
            }
        }

        /// <summary>Every Phase-1 catalog spell currently reachable for this player's progress.</summary>
        public static List<AvatarSpell> ResolveUnlockedSpells(int avatarLevel, IReadOnlyCollection<string> unlockedStageIds)
        {
            return AvatarSpell.CreatePhase1Catalog()
                .Where(s => IsUnlocked(s, avatarLevel, unlockedStageIds))
                .ToList();
        }
    }
}
