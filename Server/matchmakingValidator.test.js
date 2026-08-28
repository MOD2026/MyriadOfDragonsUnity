'use strict';

const test = require('node:test');
const assert = require('node:assert/strict');

const { validateAttackTarget } = require('./matchmakingValidator');

// These tests assert the rule *relationships* described in Game Mechanics v2, Part VII §2
// (the 10-Level Loot Lock), not incidental magnitudes, so a future threshold tuning that keeps
// the same rule shape does not read as a regression.

test('attacks within the level range are always authorized', () => {
  const result = validateAttackTarget(12, 10, 0);
  assert.equal(result.authorized, true);
  assert.equal(result.reason, 'WITHIN_LEVEL_RANGE');
});

test('a gap one below the lock threshold is still authorized', () => {
  // Threshold is a >=10 gap, so a gap of 9 must remain allowed.
  const result = validateAttackTarget(19, 10, 0);
  assert.equal(result.authorized, true);
  assert.equal(result.reason, 'WITHIN_LEVEL_RANGE');
});

test('a gap exactly at the lock threshold blocks a non-retaliation attack', () => {
  const result = validateAttackTarget(20, 10, 0);
  assert.equal(result.authorized, false);
  assert.equal(result.reason, 'LOOT_LOCK_BLOCKED');
});

test('a gap far above the threshold blocks a non-retaliation attack', () => {
  const result = validateAttackTarget(50, 1, 0);
  assert.equal(result.authorized, false);
  assert.equal(result.reason, 'LOOT_LOCK_BLOCKED');
});

test('the retaliation window re-opens an otherwise loot-locked target', () => {
  const blocked = validateAttackTarget(30, 10, 0, false);
  const retaliating = validateAttackTarget(30, 10, 0, true);
  assert.equal(blocked.authorized, false);
  assert.equal(retaliating.authorized, true);
  assert.equal(retaliating.reason, 'RETALIATION_WINDOW_ACTIVE');
});

test('retaliation does not change the outcome when already within range', () => {
  // The retaliation exception only matters once the target is loot-locked; below the threshold
  // the target is authorized for the ordinary reason regardless of the retaliation flag.
  const result = validateAttackTarget(11, 10, 0, true);
  assert.equal(result.authorized, true);
  assert.equal(result.reason, 'WITHIN_LEVEL_RANGE');
});

test('attacking a higher-level defender is always authorized (negative gap)', () => {
  const result = validateAttackTarget(5, 40, 0);
  assert.equal(result.authorized, true);
  assert.equal(result.reason, 'WITHIN_LEVEL_RANGE');
});

test('serverAgeDays is reserved and does not affect the 10-level check', () => {
  // Documented forward-compat parameter for the separate Kingdom Era lock; varying it must not
  // change this rule's decision.
  const young = validateAttackTarget(25, 10, 0);
  const old = validateAttackTarget(25, 10, 9999);
  assert.deepEqual(young, old);
});
