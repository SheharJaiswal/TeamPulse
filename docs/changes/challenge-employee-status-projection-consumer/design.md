# Design: Challenge Employee Status Projection Consumer

**Status:** Approved by owner on 2026-11-20.

## Existing context and implementation reconciliation

Identity owns employee lifecycle state and publishes the shared schema-v1 envelope through its transactional outbox. The local RabbitMQ topic exchange and service queues already exist; Habit's employee-status projection consumer is complete.

The live `feature/backend-foundation` branch already contains the Challenge API/Application/Domain/Infrastructure projects, Challenge-owned MySQL projection and inbox, checked-in EF migration and startup migration, transport-independent processor, RabbitMQ consumer, health endpoints, Challenge-owned quorum queue/DLX/DLQ, tests and local setup documentation. The SDD revision must not recreate this work. Existing implementation and CI evidence are not approval of this revised design.

The design below records the target behavior. The task checklist identifies which acceptance tests are already verified and which remain outstanding or require new implementation.

## Proposed flow

```text
Identity MySQL outbox
  -> Identity RabbitMqPublisher
  -> teampulse.domain.events
  -> teampulse.challenge.events (Challenge-owned quorum queue)
  -> Challenge.API BackgroundService
  -> Challenge.Application envelope validator / fingerprint / processor
  -> Challenge.Infrastructure transactional store
  -> Challenge MySQL: employee_status_projections + processed_domain_events
       |-> Applied / Stale / Duplicate -> ack after commit / safe comparison
       |-> Invalid / integrity violation -> Challenge lifecycle DLQ
       |-> transient failure -> bounded exponential full-jitter retry -> DLQ on exhaustion
```

## Project layout

- Challenge.API: host, configuration validation, startup migration, liveness/readiness and consumer-health endpoints.
- Challenge.Application: envelope contracts, validator, normalized fingerprint builder, processor/store/reader abstractions and processing outcomes.
- Challenge.Domain: minimal EmployeeStatusProjection and ProcessedDomainEvent entities.
- Challenge.Infrastructure: EF Core/Pomelo DbContext, checked-in migration, transactional store and RabbitMQ adapter.
- Challenge.UnitTests: domain/application unit tests, MySQL and RabbitMQ Testcontainers integration tests.

Dependencies point inward. Domain references no outer project; Application references Domain; Infrastructure references Application/Domain; API composes the host. Do not add project references to Identity or Habit.

## Challenge-owned persistence

Database: `teampulse_challenge`.

`employee_status_projections`:

- `employee_id`: char(36), primary key.
- `is_active`: boolean, not null.
- `lifecycle_version`: bigint, not null.
- `updated_at_utc`: datetime(6), not null.

`processed_domain_events`:

- `event_id`: char(36), primary key.
- `event_type`: varchar(100), not null.
- `schema_version`: int, not null.
- `employee_id`: char(36), not null.
- `lifecycle_version`: bigint, not null.
- `processed_at_utc`: datetime(6), not null.
- `fingerprint_algorithm`: varchar(40), not null; initially `rfc8785-sha256-v1`.
- `content_fingerprint`: char(64), not null; lowercase SHA-256 hex.

No FK to Identity/Habit. No copied employee profile, raw payload, note or Challenge participation data. Event ID uniqueness is mandatory. Inbox rows have no automatic expiry or cleanup in the initial design.

## Validation and normalized fingerprint

The application processor first validates the recognized schema-v1 envelope and maps it to a typed representation. Unknown additive JSON properties are ignored; required recognized fields are strict. Require valid envelope UUIDs and UTC timestamp, supported event type/schema, payload employee and actor UUIDs, exact case-sensitive source `hr-admin-api`, consistent status transition, positive lifecycle version and non-empty bounded reason code. Accept optional note only as contract permits; never persist or log it.

Build the fingerprint input from the recognized envelope/payload fields: eventId, eventType, schemaVersion, occurredAtUtc, correlationId, employeeId, actorEmployeeId, source, previousStatus, newStatus, lifecycleVersion and reasonCode. Exclude note and unknown properties. Normalize UUIDs to lowercase D form, `occurredAtUtc` to `yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'`, enum/source values to exact contract representation, and encode lifecycleVersion as an invariant base-10 string to preserve the full signed 64-bit value.

Serialize this normalized object with RFC 8785 JSON Canonicalization Scheme, then calculate SHA-256. Store only the algorithm identifier and lowercase hexadecimal digest. Do not use ordinary serializer property ordering as a substitute for JCS.

## Application processor and atomic store

The processor validates and fingerprints before calling persistence. It has no RabbitMQ or AWS references. Inject TimeProvider for deterministic `updatedAtUtc` tests.

For a new event ID, within a MySQL ReadCommitted transaction:

1. Insert the inbox metadata and fingerprint.
2. Execute a parameterized atomic conditional upsert on `employee_id`: insert if absent; on conflict update status/version/time only when incoming lifecycleVersion is strictly greater than stored.
3. Commit inbox and projection decision together.
4. Return Applied or Stale.

A valid new event ID with equal/lower version is recorded and returns Stale. Any persistence failure rolls back the inbox marker and projection decision. Do not implement an unprotected read-then-write race.

For an event-ID unique-key conflict, roll back the losing insert transaction and read the committed inbox fingerprint in a fresh read. Matching algorithm/fingerprint returns Duplicate. A mismatch returns DuplicateEventIntegrityViolation. Never overwrite the original inbox row or apply the conflicting payload. This comparison is required even when concurrent deliveries race on the unique key.

Database uniqueness and the conditional upsert enforce correctness across multiple Challenge instances; do not rely on in-memory per-employee locks.

## RabbitMQ adapter and failure policy

- Use RabbitMQ.Client 7.x asynchronous connection/channel APIs.
- Consume the complete direct envelope body from `teampulse.challenge.events`.
- Use manual ack, prefetch 1 and one in-flight callback per channel.
- Ack Applied, Stale and matching Duplicate only after the transaction commits or the stored fingerprint comparison completes.
- Route malformed/unsupported events, invalid producers and conflicting event IDs to the existing Challenge lifecycle DLQ with distinct `x-teampulse-failure-classification` metadata. Publish persistently through a dedicated RabbitMQ.Client channel with publisher confirmations and mandatory routing; acknowledge the source only after positive confirmation. If publish, routing or confirmation fails, close the source channel without acknowledgement so the original remains eligible for redelivery.
- Keep the Challenge source queue and DLQ durable quorum queues with at-least-once dead-lettering and reject-publish overflow behavior. Do not change Habit or other service topology.
- Retry only known transient MySQL/network failures using bounded exponential backoff with full jitter and configurable attempt/base/max-delay values. The deterministic retry policy chooses a delay in `[0, min(maxDelay, baseDelay × 2^(attempt-1)))`. Exhausted transient failures go to the same DLQ with classification `TransientRetryExhausted`.
- Do not retry arbitrary programming/configuration errors. Publish non-transient processing exceptions to the same DLQ with classification `NonTransientProcessingFailure`; both terminal paths use mandatory routing and publisher confirms before acknowledging the source delivery. If publish/route/confirm fails, close the source channel without acknowledgement.
- The retry-delay policy accepts an explicit jitter sample so its exponential ceiling, cap and full-jitter range can be tested deterministically; production supplies a fresh random sample for each retry.
- On publish/confirm/ack failure, signal session termination from the delivery callback; do not await closure of that callback's own consumer channel. The session owner cancels and drains the callback, then disposes the session channels/connection so the original delivery remains unacknowledged and eligible for redelivery.
- Observe RabbitMQ connection and channel shutdown notifications as well as channel state, so an established session exits promptly on broker loss.
- Each RabbitMQ session links callback processing to a session-scoped cancellation token. When the connection/channel session ends, stop accepting new callbacks, cancel the active callback, and await its bounded unwind before marking the session disconnected and disposing its channels. If a callback is canceled after its inbox/projection transaction committed, leave the source delivery unacknowledged so replay is resolved as Duplicate. A callback that ignores cancellation cannot hold reconnect indefinitely; the old channel remains unavailable and the delivery is eligible for replay.
- Bound RabbitMQ connection attempts, AMQP handshake and protocol continuations with `ChallengeConsumer:ConnectionTimeoutMilliseconds` (default 5000 ms; valid range 1000–30000 ms). This prevents the RabbitMQ.Client 7.2.2 default 30-second connection-attempt timeout from monopolizing a reconnect iteration while a restarted broker is accepting TCP but not yet completing AMQP startup.
- Reconnect with bounded delay and cancellation-aware shutdown.
- Log only approved operational metadata and failure classifications; never log raw body, note, credentials or connection strings.

## Startup and health

Use a Challenge-only connection string. Local MySQL initialization creates/grants `teampulse_challenge` to the development application user; production uses separate credentials. Run Database.MigrateAsync before hosted services. Consumer is disabled by default and options validate when enabled.

- `/health/live`: process liveness only.
- `/health/ready`: Challenge MySQL only; broker outage does not fail API readiness.
- `/health/consumer`: separate RabbitMQ connection/consumer state, including disconnected, reconnecting and healthy.

## Testing

- Unit: envelope validation, unknown additive properties, exact producer, actor UUID, transition mapping, note exclusion, JCS canonicalization vectors, stable fingerprint and changed-field mismatch, injected clock and retry-delay bounds.
- MySQL 8.4 Testcontainers: clean/repeated migration, exact model schema, duplicate same/different fingerprint, stale/equal/newer versions, concurrent N/N+1, rollback and reader.
- RabbitMQ Testcontainers: ack-after-commit, duplicate redelivery after commit-before-ack, invalid-event DLQ, conflicting duplicate DLQ, DLX failure/redelivery, bounded transient retry/exhaustion, broker startup unavailable/reconnect and established-session loss/redelivery.
- Host tests: DB readiness independent of broker; consumer health separate; migration before consumer starts.
- Contract/topology tests: Challenge quorum queue, finite delivery limit, at-least-once DLX/DLQ binding and no changes to Habit topology.
- CI: existing solution restore/build/test workflow and Python RabbitMQ topology tests.
