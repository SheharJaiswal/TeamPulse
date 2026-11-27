import test from 'node:test';
import assert from 'node:assert/strict';
import { isCalendarDate, isGuid, parseUtcTimestamp, toLocalDateInputValue } from '../src/lib/validation.js';

test('accepts a valid versioned GUID', () => {
  assert.equal(isGuid('8f14e45f-ea5e-4f2b-9c3a-123456789abc'), true);
});

test('rejects malformed and unsupported GUID values', () => {
  assert.equal(isGuid('not-a-guid'), false);
  assert.equal(isGuid('8f14e45f-ea5e-0f2b-9c3a-123456789abc'), false);
  assert.equal(isGuid(''), false);
});

test('accepts valid calendar dates including leap day', () => {
  assert.equal(isCalendarDate('2024-02-29'), true);
  assert.equal(isCalendarDate('2026-11-21'), true);
});

test('rejects impossible dates and invalid date formats', () => {
  assert.equal(isCalendarDate('2025-02-29'), false);
  assert.equal(isCalendarDate('2026-04-31'), false);
  assert.equal(isCalendarDate('2026-13-01'), false);
  assert.equal(isCalendarDate('21-11-2026'), false);
  assert.equal(isCalendarDate(''), false);
});

test('formats the browser-local calendar date for date inputs', () => {
  const localMidnight = new Date(2026, 10, 22, 0, 15, 0);
  assert.equal(toLocalDateInputValue(localMidnight), '2026-11-22');
});


test('interprets timezone-less API timestamps as UTC', () => {
  assert.equal(parseUtcTimestamp('2026-11-27T20:14:00').toISOString(), '2026-11-27T20:14:00.000Z');
});

test('preserves an explicit timestamp offset', () => {
  assert.equal(parseUtcTimestamp('2026-11-27T20:14:00+05:30').toISOString(), '2026-11-27T14:44:00.000Z');
});
