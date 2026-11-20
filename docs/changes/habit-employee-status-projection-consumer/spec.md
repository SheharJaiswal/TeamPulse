# Specification: Habit Employee Status Projection Consumer

**Status:** Ready for implementation under the existing approved contract.  
**Authority:** AGENTS.md, docs/architecture/service-ownership.md, docs/architecture/communication.md, docs/contracts/event-envelope.md and docs/contracts/employee-status-projection.md.

## Service and persistence boundary

1. Habit is one of the existing six services. No new business service is introduced.
2. Habit owns a separate logical MySQL database named teampulse_habit in local development and a separately provisioned production database/credential in deployment.
3. Habit must not connect to, query or reference Identity's database. employeeId is a stable external identifier, not a cross-database foreign key.
4. The service uses .NET 8, EF Core 8 and Pomelo.EntityFrameworkCore.MySql, consistent with ADR-0001 and the existing Identity foundation.
5. All Habit schema changes use checked-in EF Core migrations. The Habit host applies pending migrations at startup before starting the consumer or accepting traffic.
6. The shared local MySQL container may host both logical databases for development only. Its initialization must grant the local application user access to teampulse_habit without changing production database ownership.

## Projection contract

Habit stores exactly the fields required by Employee Status Projection Contract v1:

| Field | Type | Constraint |
|---|---|---|
| employeeId | UUID | Primary key; external Identity identifier only |
| isActive | boolean | Latest accepted lifecycle state |
| lifecycleVersion | signed 64-bit integer | Non-negative persisted version; incoming events must have a positive version |
| updatedAtUtc | UTC timestamp | Time Habit applied the newer projection version |

Do not copy email, display name, organization/team IDs, role, credentials, JWTs, password hashes, reasonCode, note, health data, habits, activity submissions or evidence into this projection.

## Supported messages

The consumer accepts only the shared envelope with schemaVersion 1 and one of these event types:

- EmployeeDeactivated
- EmployeeReactivated

The envelope must contain a valid UUID eventId and correlationId, a non-default UTC occurredAtUtc, and a payload containing valid UUID employeeId and actorEmployeeId, a non-empty source and reasonCode, valid previousStatus/newStatus values, and a positive lifecycleVersion.

The transition fields must agree with the event type:

| eventType | previousStatus | newStatus | resulting isActive |
|---|---|---|---|
| EmployeeDeactivated | active | inactive | false |
| EmployeeReactivated | inactive | active | true |

An optional note may be present in the envelope but must not be persisted or logged by Habit. Validation rejects malformed JSON, missing required fields, unsupported schema versions, unsupported event types, invalid identifiers/versions and inconsistent transition values before any database mutation.

## Idempotency and ordering

1. Delivery is at least once. Habit deduplicates by eventId using a unique primary key in its own processed-event inbox.
2. For a new eventId, Habit records a minimal inbox marker and evaluates the employee projection in the same database transaction.
3. The projection is inserted when no row exists. An existing projection is changed only when the incoming lifecycleVersion is strictly greater than the stored version.
4. Equal or lower versions do not change the projection, but a valid new eventId is still recorded as processed so repeated stale deliveries can be acknowledged safely.
5. A repeated eventId is a duplicate and causes no projection or inbox mutation.
6. The inbox marker and projection change commit atomically. A failed transaction leaves neither a processed marker nor a partial projection change.
7. Concurrent events for one employee must not regress the stored version. Use an atomic conditional MySQL upsert or an equivalent row-locking strategy and verify it with concurrent MySQL integration tests.
8. Habit sets updatedAtUtc from its own UTC processing clock only when a strictly newer version is applied. It does not substitute occurredAtUtc for the local application timestamp.

## Consumer acknowledgement and failure behavior

1. Consume only from the Habit-owned local queue teampulse.habit.events, using manual acknowledgements and a durable queue.
2. Use a single active handler per channel with bounded prefetch (initial local default 1) so delivery processing and channel acknowledgements are serialized. Multiple service replicas may compete for the queue.
3. Acknowledge a message only after the projection/inbox transaction commits.
4. If the database commit succeeds but the acknowledgement is lost, RabbitMQ may redeliver the message; the inbox makes that replay harmless.
5. Malformed, unsupported or internally inconsistent messages are poison messages and must be rejected without requeue so the configured Habit DLQ receives them.
6. Transient database/processing failures use bounded, configurable retry with delay. After the configured processing attempts are exhausted, reject without requeue to the Habit DLQ. A process/connection loss before acknowledgement leaves the message eligible for broker redelivery.
7. The local Habit queue uses a quorum queue with a finite delivery limit and an at-least-once dead-letter route to a durable Habit-owned DLQ. The dead-letter exchange, routing key, queue arguments and retry values are configuration/topology, not domain rules.
8. If the DLQ route is unavailable, the broker's at-least-once dead-letter behavior must retain the source message rather than silently discard it.
9. The consumer reconnects with bounded backoff when RabbitMQ is unavailable. Broker availability must not be reported as database health.
10. Never log raw message bodies, HR notes, credentials, tokens or connection strings. Log only eventId, eventType, correlationId, employeeId, lifecycleVersion, sanitized outcome/error code and attempt metadata.

## Authorization boundary

The projection is the local status signal for future Habit employee-owned resource authorization. Any future Habit resource endpoint must check this projection in addition to its normal JWT validation, tenant isolation and resource authorization. This increment adds no habit-resource endpoints and does not claim that existing stateless JWTs are globally revoked.

## Runtime configuration

- The consumer is disabled by default so the host can start without RabbitMQ when not configured for local event processing.
- When enabled, require and validate the RabbitMQ URI, queue name, prefetch count, processing-attempt limit and retry delay bounds.
- Local development explicitly enables the Habit consumer and selects the local RabbitMQ queue.
- No AWS settings or credentials are required.
- A future SQS adapter must call the same application processor and preserve eventId/schema/lifecycle-version semantics; it is not part of this change.

## Acceptance criteria and test mapping

| ID | Acceptance criterion | Verification |
|---|---|---|
| HESP-01 | Habit is added to the six-service solution with API, Application, Domain and Infrastructure boundaries. | Release solution build and project-reference review. |
| HESP-02 | Habit uses its own MySQL database and no Identity database reference or cross-service foreign key. | EF model/connection configuration tests and code review. |
| HESP-03 | A checked-in EF migration creates the projection and inbox schema; startup applies it on a clean database and safely on repeat startup. | MySQL 8.4 Testcontainers startup/migration test. |
| HESP-04 | Only schema-v1 EmployeeDeactivated and EmployeeReactivated envelopes with valid, internally consistent payloads are accepted. | Contract/unit tests for valid and malformed/unsupported envelopes. |
| HESP-05 | The projection contains only employeeId, isActive, lifecycleVersion and updatedAtUtc. | EF model and migration assertions. |
| HESP-06 | A new eventId records an inbox marker and projection decision atomically. | MySQL transaction rollback integration test. |
| HESP-07 | Duplicate eventId delivery is acknowledged without duplicate writes. | MySQL and RabbitMQ redelivery integration tests. |
| HESP-08 | Only strictly newer lifecycleVersion values change the projection; stale/equal versions are recorded but do not regress state. | Unit and MySQL ordering tests. |
| HESP-09 | Concurrent out-of-order events cannot leave a lower lifecycleVersion than the maximum successfully processed version. | Concurrent MySQL Testcontainers test. |
| HESP-10 | RabbitMQ uses manual acknowledgement; ack occurs only after database commit. | RabbitMQ Testcontainers commit/replay test. |
| HESP-11 | Poison messages and exhausted transient failures reach the durable Habit DLQ; broker outage reconnects without losing the source message. | RabbitMQ Testcontainers topology, DLQ and reconnect tests. |
| HESP-12 | Logs/configuration do not expose event body, HR note, secrets or connection string. | Logging/configuration tests and review. |
| HESP-13 | Habit's status projection can be read through an application abstraction for future resource authorization without querying Identity. | Application-level reader test and dependency-boundary review. |
| HESP-14 | Unit, MySQL 8.4 and RabbitMQ integration tests run in the existing CI workflow. | GitHub Actions restore, Release build, Compose and full test run. |

## Deferred work

- SQS/EventBridge production consumer adapter and AWS deployment.
- Consumer implementations for Challenge, Event Tracking, Insight and Notification.
- Habit CRUD, schedules, preferences and all other wellness capabilities.
- Habit API resource authorization until those resource endpoints are introduced.
- Global token revocation, DLQ redrive tooling and operational dashboards.
