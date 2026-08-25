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
    ///
    /// Wave 2 (LOCKED 2026-08-24, "expand to 19"): iterates AvatarSpell.CreateCatalog() (19) now,
    /// not just the Phase-1 14. Blood Price ("Avatar L18") gets a real AvatarLevel Rule, same shape
    /// as Ember Wave/Rallying Gale/Stone Judgment. Magma Rend/Grave Mend/Celestial Verdict
    /// ("Ch4/Ch9/Ch10 finale spell book") get SpellBookGrant-kind Rules, same shape as Sun Lance/
    /// Tempest Brand - never unlocked BY THIS RESOLVER, real via SpellBookGrant instead. Aegis
    /// Return ("Event book later") deliberately has NO Rule at all: there is no real acquisition
    /// channel for it yet, and inventing a stand-in condition here would be exactly what this
    /// class's own class-doc has always warned against - the `rule == null` branch below already
    /// does the honest thing (never auto-unlocked) without a special case.
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

            // Wave 2 (LOCKED 2026-08-24): Aegis Return ("Event book later") deliberately has no
            // Rule here - see the class doc comment.
            new Rule("Magma Rend", UnlockKind.SpellBookGrant), // catalog: "Ch4 spell book"
            new Rule("Blood Price", UnlockKind.AvatarLevel, requiredAvatarLevel: 18),
            new Rule("Grave Mend", UnlockKind.SpellBookGrant), // catalog: "Ch9 spell book"
            new Rule("Celestial Verdict", UnlockKind.SpellBookGrant), // catalog: "Ch10 finale book"

            // Wave 3 (LOCKED 2026-08-24): Ember Guard/Earthward/Gale Break deliberately have no
            // Rule here - their Phase-2 catalog Unlock column only gives a bare chapter number
            // ("ChN"), not the stage-level precision (e.g. "Ch1-2") every Stage-kind Rule above
            // this comment actually has, so there is no real stage id to build one from. Stonewall
            // and Veil of Zeus are SpellBookGrant-kind, same shape as Sun Lance/Tempest Brand.
            // Cleansing Root's "Avatar L16" is precise and gets a real AvatarLevel Rule.
            new Rule("Stonewall", UnlockKind.SpellBookGrant), // catalog: "Ch6 spell book"
            new Rule("Veil of Zeus", UnlockKind.SpellBookGrant), // catalog: "Ch8 spell book"
            new Rule("Cleansing Root", UnlockKind.AvatarLevel, requiredAvatarLevel: 16),
            new Rule("Infernal Mark", UnlockKind.SpellBookGrant), // catalog: "Ch6 spell book"

            new Rule("Oracle Sight", UnlockKind.AvatarLevel, requiredAvatarLevel: 20),

            // Acquisition channels for all remaining spells (LOCKED 2026-08-25, GPT): resolves 8 of
            // the 12 spells that had no Rule after Wave 5 (Ashfall/Stormchain/Windstep/Seismic Swap
            // are not part of this spec and stay unresolved - see the comment above). Ember Guard/
            // Earthward/Gale Break get real Stage rules now that precise first-clear stage ids
            // exist (previously blocked on the catalog doc's own bare-chapter-number imprecision).
            // Scorched Sky/Volcanic Prison/Leyline Draw/Thunder Decree are SpellBookGrant-kind,
            // joining an already-occupied Ch8/Ch9/Ch10 finale book as a second deliberate 2-spell
            // grant (the same "going forward" exception shape Ch6 already had, not a new pattern -
            // see SpellBookGrant's own dictionary comment). Titan Seal's "Avatar L30" is precise
            // and gets a real AvatarLevel Rule - deliberately the progression capstone, chosen
            // specifically to avoid crowding Ch10's finale book further.
            new Rule("Ember Guard", UnlockKind.Stage, stageId: "4-15"),
            new Rule("Earthward", UnlockKind.Stage, stageId: "5-15"),
            new Rule("Gale Break", UnlockKind.Stage, stageId: "7-15"),
            new Rule("Scorched Sky", UnlockKind.SpellBookGrant), // catalog: "Ch7 finale book"
            new Rule("Volcanic Prison", UnlockKind.SpellBookGrant), // catalog: "Ch8 finale book"
            new Rule("Leyline Draw", UnlockKind.SpellBookGrant), // catalog: "Ch9 finale book"
            new Rule("Thunder Decree", UnlockKind.SpellBookGrant), // catalog: "Ch10 finale book"
            new Rule("Titan Seal", UnlockKind.AvatarLevel, requiredAvatarLevel: 30),

            // Final 4 spell acquisition channels (LOCKED 2026-08-25, GPT): the catalog's last four
            // unresolved spells. Ashfall/Stormchain get real Stage rules now that precise first-
            // clear stage ids exist (same shape as Wave 5's Ember Guard/Earthward/Gale Break -
            // previously blocked on the catalog doc's own bare-chapter-number imprecision).
            // Windstep/Seismic Swap get real AvatarLevel rules - deliberately NOT SpellBookGrant
            // (the spec is explicit: "no new finale books, one-spell-per-finale rule preserved").
            // With this, every one of the 36 catalog spells has a real Rule or a real
            // SpellBookGrant entry - genuinely 36/36 reachable, not just 36/36 implemented.
            new Rule("Ashfall", UnlockKind.Stage, stageId: "4-30"),
            new Rule("Stormchain", UnlockKind.Stage, stageId: "6-30"),
            new Rule("Windstep", UnlockKind.AvatarLevel, requiredAvatarLevel: 18),
            new Rule("Seismic Swap", UnlockKind.AvatarLevel, requiredAvatarLevel: 24),
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
            if (rule == null) return false; // No Rule for this spell (e.g. Aegis Return) - never auto-unlocked.

            switch (rule.Kind)
            {
                case UnlockKind.Starter: return true;
                case UnlockKind.Stage: return unlockedStageIds != null && unlockedStageIds.Contains(rule.StageId);
                case UnlockKind.AvatarLevel: return avatarLevel >= rule.RequiredAvatarLevel;
                default: return false; // SpellBookGrant - see SpellBookGrant, not this resolver
            }
        }

        /// <summary>Every catalog spell (19 as of Wave 2) currently reachable for this player's
        /// progress via a Starter/Stage/AvatarLevel rule - SpellBookGrant-kind spells and any
        /// spell with no Rule at all (Aegis Return) never come back from this, by design.</summary>
        public static List<AvatarSpell> ResolveUnlockedSpells(int avatarLevel, IReadOnlyCollection<string> unlockedStageIds)
        {
            return AvatarSpell.CreateCatalog()
                .Where(s => IsUnlocked(s, avatarLevel, unlockedStageIds))
                .ToList();
        }
    }
}
