import test from 'node:test';
import assert from 'node:assert/strict';
import { buildDailyActivity, getLocalSevenDayActivityRange } from '../src/lib/insight-summary.js';

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

test('buildDailyActivity handles month and leap-day boundaries', () => {
  const days = buildDailyActivity([], '2028-02-28', '2028-03-01');

  assert.deepEqual(days.map((day) => day.activityDate), [
    '2028-02-28',
    '2028-02-29',
    '2028-03-01'
  ]);
  assert.equal(days.every((day) => day.totalActivityCount === 0), true);
});

test('getLocalSevenDayActivityRange uses inclusive browser-local dates across year boundaries', () => {
  const now = new Date(2027, 0, 3, 0, 15, 0);
  assert.deepEqual(getLocalSevenDayActivityRange(now), {
    fromDate: '2026-12-28',
    toDate: '2027-01-03'
  });
});
