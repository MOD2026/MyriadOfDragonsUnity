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
    /// acquisition method - a spell book reward - that has no tracking anywhere in the codebase:
    /// Items/ItemDatabase.cs has one generic SpellBook ItemId shared
    /// across every use of that word in the game, no per-chapter variant, and PlayerProfile has no
    /// inventory count for it at all. Guessing a stand-in condition (e.g. "unlocked once Chapter 2
    /// is reached") would silently misrepresent a reward the player was never actually given.
    /// Instead this resolver leaves those two permanently locked - see
    /// <see cref="HasUnresolvableSpellBookGates"/> - until that acquisition system is real.
    /// </summary>
    public static class SpellUnlockResolver
    {
        private enum UnlockKind
        {
            Starter,
            Stage,
            AvatarLevel,

            /// <summary>Gated on a per-chapter "spell book" acquisition that nothing in the
            /// codebase tracks yet - see the class doc comment. Never unlocked by this resolver.</summary>
            SpellBookNotYetTracked,
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
            new Rule("Sun Lance", UnlockKind.SpellBookNotYetTracked), // catalog: "Ch2 spell book"
            new Rule("Stone Judgment", UnlockKind.AvatarLevel, requiredAvatarLevel: 12),
            new Rule("Tempest Brand", UnlockKind.SpellBookNotYetTracked), // catalog: "Ch3 spell book"
        };

        /// <summary>True while any catalog spell is gated on the untracked "spell book"
        /// acquisition method - exposed so a caller or test can assert this known gap is still
        /// open rather than silently assuming every catalog spell became reachable.</summary>
        public static bool HasUnresolvableSpellBookGates => Rules.Any(r => r.Kind == UnlockKind.SpellBookNotYetTracked);

        public static bool IsUnlocked(AvatarSpell spell, int avatarLevel, IReadOnlyCollection<string> unlockedStageIds)
        {
            Rule rule = Rules.FirstOrDefault(r => r.SpellName == spell.Name);
            if (rule == null) return false; // Not a Phase-1 catalog spell at all - never auto-unlocked.

            switch (rule.Kind)
            {
                case UnlockKind.Starter: return true;
                case UnlockKind.Stage: return unlockedStageIds != null && unlockedStageIds.Contains(rule.StageId);
                case UnlockKind.AvatarLevel: return avatarLevel >= rule.RequiredAvatarLevel;
                default: return false; // SpellBookNotYetTracked
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
