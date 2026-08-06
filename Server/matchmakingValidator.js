'use strict';

/**
 * Server-side validation for the 10-Level Loot Lock PvP protection rule
 * (Game Mechanics v2, Part VII §2). This must run on the authoritative backend - a disabled
 * attack button on the client is a UI hint, not enforcement, and is trivially bypassed.
 *
 * `isRetaliation` is accepted as an input rather than computed here: whether the defender
 * attacked the attacker within the last 48 hours is a lookup against attack-log storage,
 * which belongs in the data layer, not in this pure rule-check function.
 *
 * `serverAgeDays` is accepted for forward compatibility with the separate Kingdom Era
 * migration lock (Part VII §1) - a different rule (blocks moving an older-kingdom account
 * into a newer kingdom for 60 days) that is not yet implemented in this function. It is
 * unused by the 10-level check itself; do not assume it does anything here yet.
 *
 * @param {number} attackerLevel
 * @param {number} defenderLevel
 * @param {number} serverAgeDays - reserved for the Kingdom Era check; unused in this function.
 * @param {boolean} [isRetaliation=false] - true if the defender attacked the attacker within
 *   the last 48 hours, opening the Retaliation Window exception.
 * @returns {{ authorized: boolean, reason: string }}
 */
function validateAttackTarget(attackerLevel, defenderLevel, serverAgeDays, isRetaliation = false) {
  const LEVEL_LOCK_THRESHOLD = 10;
  const levelGap = attackerLevel - defenderLevel;
  const isLootLocked = levelGap >= LEVEL_LOCK_THRESHOLD;

  if (!isLootLocked) {
    return { authorized: true, reason: 'WITHIN_LEVEL_RANGE' };
  }

  if (isRetaliation) {
    return { authorized: true, reason: 'RETALIATION_WINDOW_ACTIVE' };
  }

  return { authorized: false, reason: 'LOOT_LOCK_BLOCKED' };
}

module.exports = { validateAttackTarget };
