using System;
using System.Collections.Generic;
using System.Linq;
using MyriadOfDragons.AI;
using MyriadOfDragons.Cards;
using UnityEngine;

namespace MyriadOfDragons.Battle
{
    /// <summary>
    /// Which stage of a match is running. Added 2026-08-05 to replace turn-by-turn manual card
    /// loading, which was the main source of play fatigue: you deployed a couple of cards, hit
    /// End Turn, and repeated that a dozen times. Now the whole squad goes down once, up front,
    /// and the fight plays itself out while you respond with Avatar spells.
    /// </summary>
    public enum BattlePhase
    {
        /// <summary>Deploying the squad. Nothing clashes yet; cards can be freely placed.</summary>
        Formation,

        /// <summary>Formation is locked. Lanes clash automatically each tick; the player's only
        /// input is casting Avatar spells.</summary>
        Combat,

        /// <summary>One Avatar has reached 0 Health.</summary>
        Resolved,
    }

    /// <summary>
    /// Everything about how one match ended, in one immutable value - the payload of
    /// BattleController.OnMatchCompleted. Top-level (not nested in BattleController) since this
    /// is meant to be the primary integration point a non-battle system reads, and a bare
    /// MyriadOfDragons.Battle.MatchResult is a cleaner reference for that than a nested type.
    ///
    /// Reports remaining HP alongside each side's maximum, not remaining alone - a raw number
    /// means nothing without it: Avatar HP pools scale with Empire progression (100 to 540+, see
    /// PlayerEmpireData), so "40 HP remaining" describes a near-death Avatar at the starting
    /// profile and a comfortable lead at the max one. PlayerHealthFraction/EnemyHealthFraction
    /// are provided for the common case that only cares about the proportion.
    /// </summary>
    public readonly struct MatchResult
    {
        public readonly bool IsVictory;

        /// <summary>How many combat ticks the match ran for - named Ticks, not Turns, to match
        /// this game's own vocabulary (BattleController.TickCount, the "Clash N/12" HUD text).
        /// There is no turn-by-turn play here; combat auto-advances on a timer, so "turns" would
        /// describe a mechanic this game doesn't have.</summary>
        public readonly int TicksTaken;

        public readonly int PlayerHealthRemaining;
        public readonly int PlayerMaxHealth;
        public readonly int EnemyHealthRemaining;
        public readonly int EnemyMaxHealth;

        /// <summary>
        /// Human-readable explanation of how the match ended - the same text
        /// BattleController.OutcomeReason already carries, included here so a consumer of this
        /// event doesn't also need a live reference back into BattleController to explain the
        /// result.
        ///
        /// Empty for a straightforward knockout (an Avatar hitting 0 HP mid-combat) - that is the
        /// existing, intentional behaviour of OutcomeReason itself, not something new introduced
        /// by this struct: it is only ever populated for a tick-cap decision, where the reason
        /// genuinely needs explaining (nobody actually fell). GameBootstrap's own result screen
        /// already supplies a generic "VICTORY/DEFEAT" fallback when this is empty; a consumer
        /// of this event should do the same rather than assume it is always populated.
        /// </summary>
        public readonly string OutcomeReason;

        public MatchResult(bool isVictory, int ticksTaken, int playerHealthRemaining, int playerMaxHealth,
            int enemyHealthRemaining, int enemyMaxHealth, string outcomeReason)
        {
            IsVictory = isVictory;
            TicksTaken = ticksTaken;
            PlayerHealthRemaining = playerHealthRemaining;
            PlayerMaxHealth = playerMaxHealth;
            EnemyHealthRemaining = enemyHealthRemaining;
            EnemyMaxHealth = enemyMaxHealth;
            OutcomeReason = outcomeReason ?? string.Empty;
        }

        public float PlayerHealthFraction => PlayerMaxHealth > 0 ? (float)PlayerHealthRemaining / PlayerMaxHealth : 0f;
        public float EnemyHealthFraction => EnemyMaxHealth > 0 ? (float)EnemyHealthRemaining / EnemyMaxHealth : 0f;
    }

    /// <summary>
    /// Orchestrates one match: setup, the Formation -> Combat -> Resolved phase flow, playing
    /// cards, spell casting, and triggering resolution. No UI or AI here - a scene wires a UI
    /// layer and an opponent-decision source on top of this.
    /// </summary>
    public class BattleController : MonoBehaviour
    {
        public PlayerBattleState PlayerState { get; private set; }
        public PlayerBattleState EnemyState { get; private set; }

        public BattlePhase Phase { get; private set; } = BattlePhase.Formation;

        /// <summary>
        /// Player's spell energy during Combat. Accrues per *tick* rather than per frame - the
        /// obvious per-frame version (`energy += rate * Time.deltaTime` rounded to int) rounds
        /// a 5/sec rate to 0 every single frame at 60fps and never accrues anything at all, and
        /// a float timer couldn't be exercised by the EditMode test suite either way.
        /// </summary>
        public int Energy { get; private set; }
        public int EnemyEnergy { get; private set; }
        public int MaxEnergy { get; private set; } = 100;
        public int EnergyPerTick { get; private set; } = 18;

        /// <summary>
        /// Tutorial-only: directly sets Energy for the scripted Chapter 1 spell lesson, which
        /// must let the player cast the one approved spell right after the first combat exchange
        /// rather than waiting through several real ticks to accrue enough naturally. Not called
        /// from any normal-match code path - GameBootstrap gates every call site on
        /// IsTutorialMatch. Does not touch MaxEnergy, EnergyPerTick, or any other rule.
        /// </summary>
        public void SetEnergyForTutorial(int energy) => Energy = Math.Min(MaxEnergy, Math.Max(0, energy));

        /// <summary>EditMode-only hook for AI spell tests.</summary>
        public void SetEnemyEnergyForTests(int energy) => EnemyEnergy = Math.Min(MaxEnergy, Math.Max(0, energy));

        /// <summary>
        /// Energy each living Back-lane card adds per clash.
        ///
        /// The Back lane previously did nothing at all: Front grants +1 Attack, Middle grants +1
        /// Health, and Back granted neither - only a niche draw hook for Strategist and Perfect.
        /// It existed as overflow space. This gives it the "engine" role without inventing a
        /// second economy: Back cards still fight like any others, they simply trade a stat
        /// bonus for spell tempo. Three of them take regen from 18 to 24, which is a real
        /// difference without being able to run away with a match.
        /// </summary>
        public const int BackLaneEnergyPerCard = 2;

        public static int BackLaneEnergy(PlayerBattleState side) =>
            side.Lanes[Lane.Back].Cards.Count(c => c.IsAlive) * BackLaneEnergyPerCard;

        /// <summary>How many combat ticks have resolved. Drives spell cooldowns.</summary>
        public int TickCount { get; private set; }

        /// <summary>
        /// Hard cap on combat length. Without this a match can hang forever: once both squads
        /// wipe each other out, every lane is empty, total Attack on both sides is 0, and no
        /// further damage is possible - the fight simply never ends. That exact state was hit in
        /// play ("all the cards went missing... the health does not move"), with both boards
        /// empty at Clash 7 and both Avatars stuck above half HP.
        /// </summary>
        public const int MaxCombatTicks = 12;

        /// <summary>
        /// SPELL_CATALOG_v1.md §2 "Direct-strike safety rule": an AvatarStrike spell cannot be
        /// cast before combat tick/clash 3 - it prevents a direct spell from ending an early fight
        /// before lane counterplay exists. Applies uniformly to every AvatarStrike spell (Divine
        /// Bolt included, not just the later-added Sun Lance/Stone Judgment) and to both the
        /// player and the mirrored PvE cast path, since the catalog's own AI policy section
        /// states the AI gets "the same...tick-3 direct-strike gate as the player" once it casts.
        /// </summary>
        public const int MinimumCombatTickForAvatarStrike = 3;

        /// <summary>
        /// Ticks on which the reinforcement window opens. Cards left in hand after formation -
        /// and leftover Resource - had no use whatsoever once the fight began; these windows are
        /// what give both of them a purpose. Deliberately gated to specific ticks rather than
        /// always-open: a player who can top up a lane every single tick can chump-block
        /// indefinitely, which is the stalling problem the round cap exists to prevent.
        /// </summary>
        public static readonly int[] ReinforcementTicks = { 4, 8 };

        public bool IsReinforcementWindowOpen =>
            Phase == BattlePhase.Combat && System.Array.IndexOf(ReinforcementTicks, TickCount) >= 0;

        /// <summary>Set when a match ends on the tick cap rather than by an Avatar dying, so the
        /// result screen can explain *why* it ended.</summary>
        public string OutcomeReason { get; private set; } = string.Empty;

        public List<AvatarSpell> Spellbook { get; private set; } = new List<AvatarSpell>();
        public List<AvatarSpell> EnemySpellbook { get; private set; } = new List<AvatarSpell>();

        /// <summary>
        /// Mirrored enemy spell casting (Option B) — PvE production only.
        /// Defaults false on every StartMatch so EditMode isolation / balance sims never get surprise casts.
        /// Production enables via <see cref="EnableMirroredEnemySpellsForPvE"/> after StartMatch.
        /// </summary>
        public bool MirroredEnemySpellsEnabled { get; private set; }

        /// <summary>Production (Option B): enable mirrored enemy spells for normal/Campaign PvE. Not used by tutorial.</summary>
        public void EnableMirroredEnemySpellsForPvE() => MirroredEnemySpellsEnabled = true;

        /// <summary>EditMode-only: opt in/out for AI spell unit tests. Never call from production.</summary>
        public void SetMirroredEnemySpellsEnabledForTests(bool enabled) => MirroredEnemySpellsEnabled = enabled;

        private readonly List<CombatTickRecord> _combatLedger = new List<CombatTickRecord>();

        /// <summary>Read-only, chronological record of every combat tick actually resolved this
        /// match - data foundation for a future hardcore combat-log UI, not a UI feature itself.
        /// Cleared by StartMatch, appended to once per AdvanceCombatTick call that resolves
        /// (never for a call outside Combat, which resolves nothing).</summary>
        public IReadOnlyList<CombatTickRecord> CombatLedger => _combatLedger;

        private readonly List<SpellCastRecord> _spellCastLog = new List<SpellCastRecord>();

        /// <summary>Combat Tick Feed data (2026-08-22): read-only, chronological record of every
        /// spell actually cast this match (player or AI - the AI never casts today per MOS §6/§20,
        /// but this does not assume that stays true). Cleared by StartMatch, appended to only on a
        /// successful TryCastSpell - a rejected cast changes nothing and logs nothing.</summary>
        public IReadOnlyList<SpellCastRecord> SpellCastLog => _spellCastLog;

        public event Action<TurnResolutionResult> OnTurnResolved;
        public event Action<bool> OnMatchEnded; // argument: true if the player won

        /// <summary>
        /// Fires once, at the same moment OnMatchEnded does (a battle either resolves by a
        /// knockout or the tick cap - there is no other way out), carrying every fact about how
        /// it ended in one struct instead of a bare bool. Built for the battle/metagame split
        /// (2026-08-06): this is meant to be the seam a non-battle system (campaign map, rewards)
        /// consumes, without needing to reach into BattleController's live PlayerState/EnemyState
        /// at all.
        ///
        /// Deliberately does NOT include a reward amount (gold, XP, or otherwise). There is no
        /// economy in this project yet to source one from (see docs/Mechanics_Gap_Analysis.md,
        /// "Economy / items: STUB") - inventing a number here would mean the battle layer making
        /// a metagame balance decision it has no basis for. MatchResult reports what happened;
        /// deciding what that's worth belongs entirely to whichever system consumes this event.
        ///
        /// Fires at resolution, not on a later "return to city" UI action - it describes a battle
        /// fact (the match ended), which is true the moment it happens, not whenever a player
        /// later dismisses a results screen. A UI button re-firing a battle-logic event on click
        /// would be backwards: the event already carries everything a listener needs, so it can
        /// react immediately (e.g. computing a reward in the background) independent of when the
        /// player chooses to leave the screen. See GameBootstrap's "Return to City" button, which
        /// only hides the battle canvas locally and does not touch this event at all.
        /// </summary>
        public event Action<MatchResult> OnMatchCompleted;

        private const int FrontLaneAttackBonus = 1; // Part II §2.3
        private const int MiddleLaneHealthBonus = 1; // Part II §2.3

        // Hardcore-CCG rework (Part II §2.2): 1 card/turn from a small deck was too high-
        // variance to make Strategist/tutoring synergies reliable - Marvel Snap's 75%-of-deck-
        // seen consistency at a 12-card/6-turn ratio doesn't hold without this.
        private const int DrawsPerTurn = 2;

        /// <summary>
        /// Lets each side's match economy scale with its Empire progression (see
        /// PlayerEmpireData) instead of a single hardcoded value shared by everyone - a level-1
        /// and a leveled-up player should not play with the same resource cap or HP pool.
        /// A struct (not more StartMatch parameters) specifically so adding another per-side
        /// economy value later doesn't mean touching every StartMatch call site again.
        /// </summary>
        public readonly struct MatchEconomy
        {
            public readonly int ResourceCap;
            public readonly int Turn1Resource;
            public readonly int StartingAvatarHealth;

            public MatchEconomy(int resourceCap, int turn1Resource, int startingAvatarHealth)
            {
                ResourceCap = resourceCap;
                Turn1Resource = turn1Resource;
                StartingAvatarHealth = startingAvatarHealth;
            }
        }

        public void StartMatch(List<Card> playerDeck, List<Card> enemyDeck,
            MatchEconomy playerEconomy, MatchEconomy enemyEconomy)
        {
            PlayerState = new PlayerBattleState(playerDeck,
                playerEconomy.ResourceCap, playerEconomy.Turn1Resource, playerEconomy.StartingAvatarHealth);
            EnemyState = new PlayerBattleState(enemyDeck,
                enemyEconomy.ResourceCap, enemyEconomy.Turn1Resource, enemyEconomy.StartingAvatarHealth);

            Phase = BattlePhase.Formation;
            TickCount = 0;
            Energy = 0;
            EnemyEnergy = 0;
            Spellbook = AvatarSpell.CreateDefaultSpellbook();
            EnemySpellbook = AvatarSpell.CreateDefaultSpellbook();
            MirroredEnemySpellsEnabled = false;
            _combatLedger.Clear();
            _spellCastLog.Clear();

            BeginTurn();
        }

        public void BeginTurn()
        {
            for (int i = 0; i < DrawsPerTurn; i++)
            {
                PlayerState.DrawCard();
                EnemyState.DrawCard();
            }
            PlayerState.GainResourceForTurn();
            EnemyState.GainResourceForTurn();
        }

        // ---------- Formation phase ----------

        /// <summary>
        /// Deals the whole formation hand at once, so the squad can be built in a single sitting
        /// instead of two cards per turn over a dozen turns. Drawn up to the number of board
        /// slots that exist (3 lanes x MaxSlots), since nothing beyond that can be deployed
        /// anyway - a bigger hand would just be cards you cannot use.
        /// </summary>
        public void DealFormationHand(PlayerBattleState side)
        {
            int boardCapacity = 3 * LaneState.MaxSlots;
            while (side.Hand.Count < boardCapacity && side.DrawPile.Count > 0)
            {
                side.DrawCard();
            }
        }

        /// <summary>
        /// Locks the formation in and starts automated combat. Returns false if the player has
        /// deployed nothing at all - starting a fight with an empty board is never an intended
        /// action, only an accidental one.
        /// </summary>
        public bool ConfirmFormation()
        {
            if (Phase != BattlePhase.Formation) return false;

            bool anyDeployed = PlayerState.Lanes.Values.Any(l => l.Cards.Count > 0);
            if (!anyDeployed) return false;

            // Formation synergy is resolved once, here, rather than continuously: the squad is
            // locked from this moment on, so the bonus can't drift, and applying it at lock-in
            // means a unit that dies mid-fight doesn't retroactively weaken its surviving
            // squadmates (which would make the bonus feel like it was being taken away).
            PlayerSynergy = ApplySynergy(PlayerState);
            EnemySynergy = ApplySynergy(EnemyState);

            Phase = BattlePhase.Combat;
            return true;
        }

        /// <summary>The formation bonus each side earned when its squad locked in - exposed so
        /// the HUD can show the player what their composition actually bought them.</summary>
        public SynergyBonus PlayerSynergy { get; private set; } = SynergyBonus.None;
        public SynergyBonus EnemySynergy { get; private set; } = SynergyBonus.None;

        private static SynergyBonus ApplySynergy(PlayerBattleState side)
        {
            List<Card> deployed = side.Lanes.Values
                .SelectMany(l => l.Cards)
                .Select(c => c.Definition)
                .ToList();

            SynergyBonus bonus = FormationSynergy.Calculate(deployed);
            if (!bonus.HasAny) return bonus;

            foreach (BattleCardInstance unit in side.Lanes.Values.SelectMany(l => l.Cards))
            {
                unit.BuffAttack(bonus.AttackBonus);
                unit.BuffMaxHealth(bonus.HealthBonus);
            }

            return bonus;
        }

        /// <summary>
        /// Takes a card back out of a lane during Formation, refunding its Resource and putting
        /// the card back in hand.
        ///
        /// Required by the lane picker: a deployment UI you can't undo isn't a picker, it's a
        /// one-way commit, and the whole point of choosing a lane's three cards together is
        /// being able to swap one out while comparing them. Formation only - once combat starts
        /// the squad is locked, which is what makes the reinforcement windows meaningful.
        /// </summary>
        public bool TryRecallCard(Lane lane, int slotIndex)
        {
            if (Phase != BattlePhase.Formation) return false;

            LaneState laneState = PlayerState.Lanes[lane];
            if (slotIndex < 0 || slotIndex >= laneState.Cards.Count) return false;

            BattleCardInstance instance = laneState.Cards[slotIndex];
            laneState.Cards.RemoveAt(slotIndex);

            // Refund from the definition's cost, not the instance's live stats - lane bonuses
            // change Attack/Health but never what the card cost to play.
            PlayerState.Resource = Math.Min(PlayerState.ResourceCap,
                PlayerState.Resource + instance.Definition.ResourceCost);
            PlayerState.Hand.Add(instance.Definition);

            // Note: an on-play class hook that already fired (Strategist's draw) is deliberately
            // NOT undone. Un-drawing a card would mean deciding which card to put back and in
            // what order, and a player who recalls a Strategist to try a different lane hasn't
            // done anything abusive - the draw was earned by the play.
            return true;
        }

        // ---------- Combat phase ----------

        /// <summary>
        /// Advances combat by one tick: energy accrues, spell cooldowns count down, and both
        /// sides' lanes clash. This is the whole combat loop, exposed as a plain method rather
        /// than being driven from inside Update() so the EditMode test suite can step a battle
        /// deterministically - a real fight can be simulated tick by tick with no Play Mode.
        /// The scene layer just calls this on a timer.
        /// </summary>
        public TurnResolutionResult AdvanceCombatTick()
        {
            if (Phase != BattlePhase.Combat) return default;

            TickCount++;
            Energy = Math.Min(MaxEnergy, Energy + EnergyPerTick + BackLaneEnergy(PlayerState));
            EnemyEnergy = Math.Min(MaxEnergy, EnemyEnergy + EnergyPerTick + BackLaneEnergy(EnemyState));
            foreach (AvatarSpell spell in Spellbook)
            {
                spell.TickCooldown();
            }

            foreach (AvatarSpell spell in EnemySpellbook)
            {
                spell.TickCooldown();
            }

            if (MirroredEnemySpellsEnabled)
                AISpellCaster.TryCastDuringCombatTick(this);

            TurnResolutionResult result = ResolveTurnAndAdvance(TickCount);

            // One ledger entry per actually-resolved tick - AvatarHealth on both sides is already
            // post-damage here, since LaneBattleResolver.ResolveTurn (called inside
            // ResolveTurnAndAdvance above) mutates it before returning.
            _combatLedger.Add(new CombatTickRecord(
                tickNumber: TickCount,
                damageToPlayerAvatar: result.DamageDealtToSideA,
                damageToEnemyAvatar: result.DamageDealtToSideB,
                playerAvatarHealthAfter: PlayerState.AvatarHealth,
                enemyAvatarHealthAfter: EnemyState.AvatarHealth,
                laneResults: new List<LaneClashResult>(result.LaneResults),
                siegeDamageToPlayerAvatar: result.SiegeDamageToSideA,
                siegeDamageToEnemyAvatar: result.SiegeDamageToSideB));

            // Tick cap - see MaxCombatTicks. Checked after resolution so a killing blow on the
            // final tick still counts as a real win rather than being downgraded to a decision.
            if (Phase == BattlePhase.Combat && TickCount >= MaxCombatTicks)
            {
                ResolveOnTickCap();
            }

            return result;
        }

        /// <summary>
        /// Decides a match that reached the tick cap, on remaining Avatar Health as a *fraction*
        /// of each side's maximum. Comparing raw HP would hand the win to whoever simply started
        /// with a bigger pool, which the AI often does at higher difficulty tiers - proportion is
        /// the only fair comparison when the two sides' maximums differ by design.
        /// </summary>
        private void ResolveOnTickCap()
        {
            float playerFraction = PlayerState.MaxAvatarHealth > 0
                ? (float)PlayerState.AvatarHealth / PlayerState.MaxAvatarHealth : 0f;
            float enemyFraction = EnemyState.MaxAvatarHealth > 0
                ? (float)EnemyState.AvatarHealth / EnemyState.MaxAvatarHealth : 0f;

            Phase = BattlePhase.Resolved;

            if (Mathf.Approximately(playerFraction, enemyFraction))
            {
                // A draw is reported as a loss for progression purposes but named honestly, so
                // the result screen never claims a victory the player didn't earn.
                OutcomeReason = $"DRAW after {MaxCombatTicks} clashes - both Avatars at " +
                                $"{playerFraction * 100f:F0}% Health.";
                RaiseMatchEnded(false);
                return;
            }

            bool playerWon = playerFraction > enemyFraction;
            OutcomeReason = playerWon
                ? $"VICTORY on Health after {MaxCombatTicks} clashes - {playerFraction * 100f:F0}% vs {enemyFraction * 100f:F0}%."
                : $"DEFEAT on Health after {MaxCombatTicks} clashes - {playerFraction * 100f:F0}% vs {enemyFraction * 100f:F0}%.";
            RaiseMatchEnded(playerWon);
        }

        /// <summary>
        /// Single point that fires both match-end events - keeps OnMatchEnded and
        /// OnMatchCompleted from being able to drift out of sync (e.g. one call site remembering
        /// to fire the new event and another forgetting), since every resolution path already had
        /// its own OnMatchEnded?.Invoke(...) before OnMatchCompleted existed.
        /// </summary>
        private void RaiseMatchEnded(bool playerWon)
        {
            OnMatchEnded?.Invoke(playerWon);
            OnMatchCompleted?.Invoke(new MatchResult(
                isVictory: playerWon,
                ticksTaken: TickCount,
                playerHealthRemaining: PlayerState.AvatarHealth,
                playerMaxHealth: PlayerState.MaxAvatarHealth,
                enemyHealthRemaining: EnemyState.AvatarHealth,
                enemyMaxHealth: EnemyState.MaxAvatarHealth,
                outcomeReason: OutcomeReason));
        }

        /// <summary>
        /// Deploys a card from hand into a lane mid-combat, during a reinforcement window.
        /// Costs Resource (not Energy) deliberately: leftover Resource had no post-formation use
        /// at all, and keeping the two economies separate means reinforcing never competes with
        /// casting for the same pool.
        /// </summary>
        public bool TryDeployReinforcement(Card card, Lane lane)
        {
            if (!IsReinforcementWindowOpen) return false;
            if (!PlayerState.Hand.Contains(card)) return false;
            if (card.ResourceCost > PlayerState.Resource) return false;
            if (!PlayerState.Lanes[lane].HasRoomFor(card)) return false;

            // Reuses TryPlayCard so reinforcements get the same lane bonuses and on-play class
            // hooks as a formation deployment - a Knight reinforcing into Middle should still
            // taunt and still get its +1 Health.
            bool played = TryPlayCard(PlayerState, card, lane);
            if (!played) return false;

            // Formation synergy is recalculated so a reinforcement can complete a pair or trio.
            // Only the *new* unit is buffed - the squad already standing had its bonus applied at
            // lock-in, and re-buffing them would stack the same bonus repeatedly every window.
            SynergyBonus current = FormationSynergy.Calculate(
                PlayerState.Lanes.Values.SelectMany(l => l.Cards).Select(c => c.Definition));
            BattleCardInstance deployed = PlayerState.Lanes[lane].Cards[^1];
            deployed.BuffAttack(current.AttackBonus);
            deployed.BuffMaxHealth(current.HealthBonus);
            PlayerSynergy = current;

            return true;
        }

        /// <summary>
        /// Casts a spell from the Spellbook at `targetLane`. Owns the affordability rules
        /// (energy, cooldown, phase) so AvatarSpell.Cast can stay a pure effect. Returns false
        /// with nothing changed if the cast isn't legal.
        /// </summary>
        public bool TryCastSpell(int spellIndex, Lane targetLane, out int avatarDamageDealt)
        {
            avatarDamageDealt = 0;

            if (Phase != BattlePhase.Combat) return false;
            if (spellIndex < 0 || spellIndex >= Spellbook.Count) return false;

            AvatarSpell spell = Spellbook[spellIndex];
            if (!spell.IsOffCooldown) return false;
            if (spell.EnergyCost > Energy) return false;
            if (spell.Effect == SpellEffect.AvatarStrike && TickCount < MinimumCombatTickForAvatarStrike) return false;

            Energy -= spell.EnergyCost;
            spell.PutOnCooldown();
            avatarDamageDealt = spell.Cast(PlayerState, EnemyState, targetLane);

            // Combat Tick Feed data (2026-08-22): logged only once the cast is confirmed legal
            // and has actually happened - never for a rejected attempt (see the early returns
            // above, none of which reach this line).
            _spellCastLog.Add(new SpellCastRecord(TickCount, spell.Name, targetLane, avatarDamageDealt, castByPlayer: true));

            // A spell that kills the enemy Avatar outright must end the match immediately, not
            // leave it running until the next tick happens to notice.
            if (EnemyState.IsDefeated)
            {
                Phase = BattlePhase.Resolved;
                RaiseMatchEnded(true);
            }

            return true;
        }

        /// <summary>Mirrored PvE spell cast — same energy/cooldown rules as the player path.</summary>
        public bool TryCastEnemySpell(int spellIndex, Lane targetLane, out int avatarDamageDealt)
        {
            avatarDamageDealt = 0;

            if (Phase != BattlePhase.Combat) return false;
            if (spellIndex < 0 || spellIndex >= EnemySpellbook.Count) return false;

            AvatarSpell spell = EnemySpellbook[spellIndex];
            if (!spell.IsOffCooldown) return false;
            if (spell.EnergyCost > EnemyEnergy) return false;
            if (spell.Effect == SpellEffect.AvatarStrike && TickCount < MinimumCombatTickForAvatarStrike) return false;

            EnemyEnergy -= spell.EnergyCost;
            spell.PutOnCooldown();
            avatarDamageDealt = spell.Cast(EnemyState, PlayerState, targetLane);

            _spellCastLog.Add(new SpellCastRecord(TickCount, spell.Name, targetLane, avatarDamageDealt, castByPlayer: false));

            if (PlayerState.IsDefeated)
            {
                Phase = BattlePhase.Resolved;
                RaiseMatchEnded(false);
            }

            return true;
        }

        /// <summary>
        /// Attempts to play a card from `side`'s hand into `lane`. Returns false (no state
        /// changed) if the card isn't in hand, the resource cost can't be paid, or the lane is
        /// full - callers should treat false as "input rejected," not an error.
        /// </summary>
        public bool TryPlayCard(PlayerBattleState side, Card card, Lane lane)
        {
            if (!side.Hand.Contains(card)) return false;
            if (card.ResourceCost > side.Resource) return false;
            if (!side.Lanes[lane].HasRoomFor(card)) return false;

            side.Resource -= card.ResourceCost;
            side.Hand.Remove(card);

            int attackBonus = lane == Lane.Front ? FrontLaneAttackBonus : 0;
            int healthBonus = lane == Lane.Middle ? MiddleLaneHealthBonus : 0;
            var instance = new BattleCardInstance(card, side == PlayerState, attackBonus, healthBonus);

            side.Lanes[lane].Cards.Add(instance);

            ApplyOnPlayClassHook(side, card, lane);

            return true;
        }

        /// <summary>
        /// Resolves the current board state (both sides' committed lane plays clash), fires
        /// <see cref="OnTurnResolved"/>, and either ends the match or advances to the next turn.
        /// </summary>
        public TurnResolutionResult ResolveTurnAndAdvance(int tickNumber = 1)
        {
            TurnResolutionResult result = LaneBattleResolver.ResolveTurn(PlayerState, EnemyState, tickNumber);
            OnTurnResolved?.Invoke(result);

            if (result.SideADefeated || result.SideBDefeated)
            {
                bool playerWon = result.SideBDefeated && !result.SideADefeated;
                Phase = BattlePhase.Resolved;
                RaiseMatchEnded(playerWon);
            }
            else if (Phase != BattlePhase.Combat)
            {
                // Drawing and resource gain belong to the old turn-by-turn flow only. Once
                // combat is running the squad is locked, so refilling a hand nobody can play
                // from - and topping up resource nobody can spend - would just be dead work.
                BeginTurn();
            }

            return result;
        }

        /// <summary>
        /// Strategist's on-play draw always fires, regardless of lane. Perfect no longer pays
        /// a flat cost tax (hardcore-CCG rework, Part II §3.2) - instead it dynamically adopts
        /// whichever lane's specialist behavior applies: Front/Middle stat bonuses already
        /// apply to every card equally (see TryPlayCard), so the only *new* behavior Perfect
        /// needs here is Strategist's draw hook when played specifically into the Back lane.
        /// </summary>
        private void ApplyOnPlayClassHook(PlayerBattleState side, Card card, Lane lane)
        {
            bool triggersDraw = card.Class == CardClass.Strategist
                || (card.Class == CardClass.Perfect && lane == Lane.Back);

            if (triggersDraw)
            {
                side.DrawCard();
            }
        }
    }
}
