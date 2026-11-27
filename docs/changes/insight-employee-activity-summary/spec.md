# Specification: Employee Activity Summary Insights

**Status:** Proposed; requires owner approval before implementation.

## Scope and ownership
1. Insight owns only the derived daily summary and event inbox.
2. Event Tracking remains the source of truth for submissions; Insight does not query or write its database.
3. Personal activity summaries are private to the authenticated employee. No manager/HR read endpoint is included.
4. Use .NET 8 Clean Architecture and the existing Insight-owned DynamoDB table.
5. The existing employee lifecycle projection and consumer remain unchanged.

## Event contract
Accept only schema-version-1 `HabitActivityRecorded` and `ChallengeActivityRecorded` envelopes as emitted by Event Tracking:
- Validate eventId, eventType, schemaVersion, occurredAtUtc, correlationId and required payload identifiers/date/quantity.
- Habit event payload contains employeeId, submissionId, habitId, activityDate and positive quantity.
- Challenge event payload contains employeeId, submissionId, challengeId, activityDate and positive quantity.
- Ignore unknown additive properties; do not persist raw JSON or habit/challenge IDs.
- Derive category from the exact eventType; do not trust client-supplied category.
- Reject malformed or unsupported messages before persistence.

## Daily projection
For each employee and activityDate, store:
- employeeId
- activityDate
- habitActivityCount
- challengeActivityCount

Each unique accepted activity event increments exactly one category count by one. Quantity is intentionally not summed because habit and challenge units may differ and challenge goals can use different units. Do not store individual activity identifiers, names, quantity, notes or raw payloads in Insight.

## Idempotency and atomicity
- Deduplicate permanently by eventId in the Insight-owned inbox.
- Update the daily aggregate and insert the inbox item in one DynamoDB TransactWriteItems transaction.
- The inbox condition must reject duplicate eventId; a duplicate must not increment the aggregate.
- Use atomic ADD for the appropriate daily category count so concurrent Insight replicas cannot lose increments.
- Acknowledge only after transaction success or safely observed duplicate.
- Persistence errors remain unacknowledged for bounded retry/session recovery.

## RabbitMQ
- Use a dedicated Insight activity queue bound to `teampulse.domain.events` for `habit.activity.recorded` and `challenge.activity.recorded`.
- Keep the existing `teampulse.insight.events` lifecycle queue and bindings unchanged.
- Use manual acknowledgement, prefetch 1, bounded retry and a dedicated durable Insight activity DLQ route.
- Queue topology changes are code/configuration only; do not mutate a shared/persistent broker during this feature.

## API
- Authenticated GET endpoint returns the caller's daily summaries for a requested inclusive date range.
- Employee ID comes only from validated JWT `sub`; never accept employeeId from query/body.
- Default date range is the latest 7 calendar days; maximum range is 30 calendar days.
- Return daily counts by category, not employee profile data or raw activity entries.
- Invalid ranges return 400; unauthenticated/invalid subject returns 401.
- No manager/HR access, organization/team aggregation or cross-employee lookup.

## Privacy and retention
- Persist only daily counts and event metadata needed for deduplication.
- Do not log payloads, activity IDs, habit/challenge IDs or wellness details.
- Inbox retention is permanent initially, matching existing Insight lifecycle inbox policy; no TTL/cleanup in this increment.

## Acceptance criteria
- IAS-01 valid habit event increments habit count for its employee/date.
- IAS-02 valid challenge event increments challenge count for its employee/date.
- IAS-03 duplicate eventId does not increment twice.
- IAS-04 concurrent distinct events produce the exact total.
- IAS-05 malformed/unsupported events do not mutate projection.
- IAS-06 aggregate and inbox are atomic.
- IAS-07 API returns only authenticated employee's requested dates.
- IAS-08 date range default/max/invalid behavior is tested.
- IAS-09 RabbitMQ ack follows committed processing; poison messages use mandatory confirmed DLQ routing.
- IAS-10 lifecycle projection tests and behavior remain unchanged.
- IAS-11 CI runs unit, DynamoDB Local, API authorization and RabbitMQ integration/topology tests.

## Deferred
Streaks, recommendations, goals, team/manager dashboards, per-habit/challenge breakdown, event replay/correction, retention cleanup, AWS provisioning and EventBridge/SQS adapter.
