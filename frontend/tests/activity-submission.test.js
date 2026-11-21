import test from 'node:test';
import assert from 'node:assert/strict';
import { submitActivity } from '../src/lib/server/activity-submission.js';

const validSubmission = {
  habitId: '8f14e45f-ea5e-4f2b-9c3a-123456789abc',
  activityDate: '2026-11-21',
  quantity: 2,
  idempotencyKey: '9f14e45f-ea5e-4f2b-9c3a-123456789abc'
};

function dependencies(overrides = {}) {
  return {
    send: async () => new Response(null, { status: 201 }),
    createIdempotencyKey: () => '7f14e45f-ea5e-4f2b-9c3a-123456789abc',
    onResponse: async () => {},
    getResponseMessage: async (_response, fallback) => fallback,
    ...overrides
  };
}

test('rejects invalid input without calling Event Tracking', async () => {
  let sendCount = 0;
  const result = await submitActivity(
    { ...validSubmission, activityDate: '2025-02-29' },
    dependencies({ send: async () => { sendCount += 1; return new Response(null, { status: 201 }); } })
  );

  assert.equal(result.kind, 'invalid');
  assert.equal(result.status, 400);
  assert.equal(result.idempotencyKey, validSubmission.idempotencyKey);
  assert.equal(sendCount, 0);
});

test('creates a replacement key only when the submitted key is invalid', async () => {
  let sendCount = 0;
  const result = await submitActivity(
    { ...validSubmission, idempotencyKey: 'invalid-key' },
    dependencies({ send: async () => { sendCount += 1; return new Response(null, { status: 201 }); } })
  );

  assert.equal(result.kind, 'invalid');
  assert.equal(result.idempotencyKey, '7f14e45f-ea5e-4f2b-9c3a-123456789abc');
  assert.equal(sendCount, 0);
});

test('retains the same key when the Event Tracking request fails', async () => {
  const result = await submitActivity(
    validSubmission,
    dependencies({ send: async () => { throw new Error('connection reset'); } })
  );

  assert.equal(result.kind, 'unavailable');
  assert.equal(result.status, 503);
  assert.equal(result.idempotencyKey, validSubmission.idempotencyKey);
});

test('preserves service-unavailable responses and the idempotency key', async () => {
  const result = await submitActivity(
    validSubmission,
    dependencies({
      send: async () => new Response(JSON.stringify({ message: 'Habit service is temporarily unavailable.' }), {
        status: 503,
        headers: { 'content-type': 'application/json' }
      }),
      getResponseMessage: async (response, fallback) => {
        const body = await response.json();
        return body.message || fallback;
      }
    })
  );

  assert.equal(result.kind, 'rejected');
  assert.equal(result.status, 503);
  assert.equal(result.message, 'Habit service is temporarily unavailable.');
  assert.equal(result.idempotencyKey, validSubmission.idempotencyKey);
});

test('retains the same key when Event Tracking rejects the submission', async () => {
  let observedStatus = 0;
  const result = await submitActivity(
    validSubmission,
    dependencies({
      send: async () => new Response(null, { status: 409 }),
      getResponseMessage: async (response) => {
        observedStatus = response.status;
        return 'Duplicate submission conflict.';
      }
    })
  );

  assert.equal(result.kind, 'rejected');
  assert.equal(result.status, 409);
  assert.equal(result.message, 'Duplicate submission conflict.');
  assert.equal(result.idempotencyKey, validSubmission.idempotencyKey);
  assert.equal(observedStatus, 409);
});

test('rotates the key only after Event Tracking confirms success', async () => {
  let responseHandled = false;
  const result = await submitActivity(
    validSubmission,
    dependencies({
      onResponse: async () => { responseHandled = true; }
    })
  );

  assert.equal(result.kind, 'success');
  assert.equal(result.idempotencyKey, '7f14e45f-ea5e-4f2b-9c3a-123456789abc');
  assert.notEqual(result.idempotencyKey, validSubmission.idempotencyKey);
  assert.equal(responseHandled, true);
});
