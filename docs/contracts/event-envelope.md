# Domain Event Envelope

Fields:
- `eventId` (UUID)
- `eventType`
- `schemaVersion` (integer)
- `occurredAtUtc` (UTC)
- `correlationId` (UUID)
- `payload` (event-specific, minimal data)

Events are versioned contracts. A breaking payload change requires a new schema version; consumers must reject or safely quarantine unsupported versions. Never include access tokens, passwords, password hashes, credentials or unnecessary private wellness data.

## Employee lifecycle event schema v1

Identity emits `EmployeeDeactivated` and `EmployeeReactivated` using this envelope. The payload contains:
- `employeeId` (UUID)
- `actorEmployeeId` (UUID from the validated HR administrator subject)
- `source` (for this API: `hr-admin-api`)
- `previousStatus` and `newStatus` (`active` or `inactive`)
- `lifecycleVersion` (monotonically increasing 64-bit integer per employee)
- `reasonCode` (opaque bounded code supplied by the HR actor)
- `note` (optional explanatory note)

The `eventId` is also the immutable lifecycle audit ID. The outbox row, audit row and employee state transition are committed in one Identity database transaction. The outbox record is not evidence of EventBridge delivery; dispatch is a separate at-least-once operation.

## EventBridge transport mapping

For Identity lifecycle events, the EventBridge entry maps:
- `EventBusName`: deployment-supplied `EventBridge__BusName`.
- `Source`: fixed `teampulse.identity`.
- `DetailType`: the exact envelope `eventType`.
- `Time`: the envelope `occurredAtUtc`.
- `Detail`: the complete serialized envelope above, without a second wrapper.

The publisher processes each `PutEvents` response entry independently. Only an entry with a returned EventBridge event ID and no entry error is accepted. The outbox `PublishedAtUtc` is written after that individual acknowledgement and means only that the bus accepted the event. It does not mean that any rule target or consumer received or processed it.

A crash after bus acceptance but before the MySQL acknowledgement can result in the same `eventId` being published again. This is expected at-least-once behavior; consumers must deduplicate by `eventId` and apply lifecycle events only when their `lifecycleVersion` is newer than the local projection.

Producer-side EventBridge publish failures are retried or quarantined in the Identity outbox. SQS DLQs are configured on EventBridge rule targets for failures after bus acceptance. Do not treat an EventBridge target DLQ as a substitute for the producer's outbox retry/quarantine state.


## SQS projection target body

The Identity lifecycle EventBridge rule does not configure `Input`, `InputPath` or `InputTransformer`. Each SQS target receives the standard outer EventBridge event JSON. The TeamPulse schema-v1 envelope described above is the outer event's `detail` object. Consumers must parse `detail`, validate the event envelope, deduplicate by its `eventId` and apply only strictly newer `payload.lifecycleVersion` values. The SQS body wrapper does not introduce a new TeamPulse event ID or schema version.


## Local RabbitMQ transport mapping

For local development only, Identity publishes the same complete envelope directly as the RabbitMQ message body:

- Exchange: durable topic exchange `teampulse.domain.events`.
- Routing key: `identity.employee.lifecycle`.
- AMQP `messageId`: envelope `eventId`.
- AMQP `type`: envelope `eventType`.
- AMQP `correlationId`: envelope `correlationId`.
- AMQP `contentType`: `application/json`; delivery mode is persistent; `schemaVersion` is included as a header.
- Publisher-confirm tracking and mandatory routing must succeed before the outbox row is marked published.

The local broker has one durable queue for each of Habit, Challenge, Event Tracking, Insight and Notification. The shared local definitions file only seeds development topology; each production service owns its queue and consumer access/failure policy. RabbitMQ and EventBridge are alternative transport adapters for the same immutable event envelope, not simultaneous publishers for one outbox worker.


## Habit activity event schema v1

Event Tracking emits `HabitActivityRecorded` when an employee's append-only activity submission is committed. The schema-v1 envelope uses the same top-level fields as the domain event contract. Its `payload` contains:
- `employeeId` (UUID derived from the validated caller identity)
- `submissionId` (UUID of the immutable activity submission)
- `habitId` (UUID owned by Habit)
- `activityDate` (employee-selected calendar date, serialized as ISO 8601 date)
- `quantity` (integer from 1 through 1,000)

The event is a private domain integration contract, not authorization to expose individual activity to managers or organization dashboards. Consumers must not infer streak or target-completion rules from this event; those remain separately undefined. The outbox row and activity submission commit in one Event Tracking MySQL transaction.

For local RabbitMQ only, Event Tracking publishes the complete envelope to the existing durable topic exchange `teampulse.domain.events` with routing key `habit.activity.recorded`. A dedicated bounded quorum queue `teampulse.activity.events` is seeded by local definitions for future consumers; it has a separate DLQ and does not reuse lifecycle queues. Production transport remains deferred until cloud infrastructure and retention/privacy approvals are in place.
