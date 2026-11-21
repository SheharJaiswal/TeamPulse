# Design: Habit Activity Submissions

## Status

Approved policy; implementation complete. Business rules are recorded in ADR-0003.

## Service boundaries

```text
SvelteKit employee UI
    | same-origin server action; forwards employee bearer token
    v
Event Tracking API ----------------------> Habit API
    |                                      | validates owner/status/date range
    v                                      v
Event Tracking MySQL                  Habit database
(activity + outbox, one transaction)   (habit definition source of truth)
    |
    | lease-based outbox dispatcher; persistent publish + broker confirm
    v
RabbitMQ topic exchange: teampulse.domain.events
    | routing key: habit.activity.recorded
    v
teampulse.activity.events (bounded quorum queue; future consumers)
```

- Browser never calls Event Tracking or Habit directly and never receives the JWT.
- Event Tracking derives employee identity from validated token claims.
- Event Tracking calls Habit API with caller bearer token to validate ownership and date range. No cross-service database access or foreign key is introduced.
- Habit remains authoritative for active status and configured start/end dates.

## HTTP contract

- `POST /api/habit-activities`: submit a self-reported activity; request fields are `habitId`, `activityDate`, `quantity`, `idempotencyKey`.
- `GET /api/habit-activities`: list caller submissions; optional `habitId`, `fromDate`, `toDate` filters.
- `GET /api/habit-activities/{id}`: retrieve one caller-owned submission.
- Employee ID, submission ID, event ID, timestamps and correlation ID are server-owned.

## Persistence and event delivery

- `habit_activity_submissions` is the append-only activity source of truth with employee-scoped unique idempotency index.
- `event_tracking_outbox_messages` stores the complete schema-v1 `HabitActivityRecorded` envelope.
- Both rows are written in one Event Tracking MySQL transaction. A failed insert rolls back both.
- Dispatcher claims batches with `FOR UPDATE SKIP LOCKED`, uses lease tokens to prevent stale acknowledgement, retries failures with bounded backoff and quarantines exhausted messages.
- RabbitMQ publisher uses persistent messages, mandatory routing and publisher confirms. It publishes to `teampulse.domain.events` / `habit.activity.recorded`.
- The new local `teampulse.activity.events` quorum queue is bounded and isolated from lifecycle queues. No consumer is added in this change.
- EventBridge deployment remains deferred until cloud infrastructure is provisioned. Do not run RabbitMQ topology reset/purge operations against shared persistent broker data.

## Future consumer idempotency contract

The activity queue intentionally has no consumer in this change. When a downstream service is approved to consume `HabitActivityRecorded`, it must implement the following boundary inside that service; this document does not authorize a progress, streak or manager-insight projection.

- Treat the envelope `EventId` as the stable delivery identity. Do not deduplicate by employee, habit, activity date, quantity, correlation ID or RabbitMQ delivery tag; distinct valid submissions may share the same business values.
- Persist a durable inbox/processed-event record with a unique constraint on the event identity within the consumer. If one service hosts multiple independently versioned handlers, include a stable handler/consumer identity in that uniqueness scope.
- Apply the consumer's local state change and insert the inbox record in the same database transaction. Commit both before acknowledging the RabbitMQ delivery. Never acknowledge first and persist later.
- If the event ID is already committed, acknowledge the redelivery without repeating the state change. A duplicate event ID with a different event type, schema version or canonical payload is an integrity conflict; do not overwrite the original inbox record or process the conflicting body.
- For transient failures, leave the event eligible for retry and do not write a completed inbox record. Permanent validation failures and unsupported schema versions must follow an explicit quarantine/operational-review path, not be silently acknowledged as successful business processing.
- Validate the event type and schema version before applying business logic. Additive or breaking schema evolution must be handled deliberately by the consumer contract and tests.
- Keep the inbox in the consuming service's own database. Do not introduce cross-service database writes or claim global exactly-once delivery; the transport remains at-least-once and the inbox makes each consumer's local effect idempotent.

Until a consumer's business purpose and state transition are approved, do not create a no-op consumer that merely drains and discards activity events.

## Security and privacy

- Require authenticated employee access and local employee-status enforcement.
- Validate habit ownership through Habit API using caller token; return non-disclosing 404 for inaccessible habits.
- Never accept employee ID from the request.
- Keep activity history employee-private; do not expose individual activity to managers or aggregate endpoints.
- Event contains only employee ID, submission ID, habit ID, activity date and quantity. No notes, health/device/location data or credentials are included.
- Streak and target-progress calculations are not defined by ADR-0003 and must not be inferred.
- Production retention remains blocked by ADR-0002 legal/privacy signoff.
