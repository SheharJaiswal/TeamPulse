import test from 'node:test';
import assert from 'node:assert/strict';
import { buildDailyActivity } from '../src/lib/insight-summary.js';

test('buildDailyActivity fills missing dates with zero counts and keeps dates inclusive', () => {
  const days = buildDailyActivity([
    { activityDate: '2026-11-26', habitActivityCount: 2, challengeActivityCount: 1 },
    { activityDate: '2026-11-28', habitActivityCount: 0, challengeActivityCount: 3 }
  ], '2026-11-26', '2026-11-28');

  assert.deepEqual(days, [
    { activityDate: '2026-11-26', habitActivityCount: 2, challengeActivityCount: 1, totalActivityCount: 3 },
    { activityDate: '2026-11-27', habitActivityCount: 0, challengeActivityCount: 0, totalActivityCount: 0 },
    { activityDate: '2026-11-28', habitActivityCount: 0, challengeActivityCount: 3, totalActivityCount: 3 }
  ]);
});

test('buildDailyActivity returns an empty list for an inverted range', () => {
  assert.deepEqual(buildDailyActivity([], '2026-11-29', '2026-11-28'), []);
});
