# ADR-0003: Habit Activity Submission Policy

- Status: Accepted by the project owner.
- Date: 2026-11-21.
- Scope: Employee self-reported personal habit activity submissions and personal history.

## Decisions

1. **Cardinality:** An employee may create multiple activity submissions for the same habit and activity date. Each submission contributes a count quantity; derived progress calculations are a separate future increment.
2. **Immutability:** Accepted submissions are append-only. The MVP does not support editing or deleting a submission.
3. **Activity date:** The employee selects a calendar date. It must fall on or after the habit's configured start date and on or before its optional end date. It is a date value, not a timestamp interpreted in UTC.
4. **Quantity:** Each submission has a positive integer quantity from 1 through 1,000.
5. **Idempotency:** The client supplies a GUID idempotency key. It is unique per employee. A retry with the same key and identical habit/date/quantity returns the original submission; reuse with different values returns HTTP 409 Conflict.
6. **Ownership:** Event Tracking derives employee identity from validated JWT `sub`, validates habit ownership through the Habit API using the caller's bearer token, and never reads the Habit database. A caller cannot select an employee ID.
7. **Privacy:** Activity history is employee-private. Manager and organization insight surfaces do not expose individual submissions or personal habit details.
8. **Timestamps:** The activity date is a calendar date; the server-generated submission timestamp is UTC.

## Consequences

- Event Tracking owns the activity submission source of truth and its history. Habit continues to own habit definitions.
- Event Tracking must persist the submission and its publishable event in one transaction using a transactional outbox.
- Event delivery is at least once and consumers must deduplicate by event ID.
- Streak and target-progress calculations are not defined by this decision and must not be inferred from quantity alone.
- Production retention remains subject to ADR-0002 privacy/legal review. EventBridge deployment remains deferred; local development uses RabbitMQ.
