# Design: Challenge Employee Status Projection Consumer

## Existing context
The live branch contains Identity's transactional outbox publisher, local RabbitMQ topic exchange, five service queues and a completed Habit projection consumer. Challenge has no service project yet. Its local queue teampulse.challenge.events is durable and bound to identity.employee.lifecycle, but has no Challenge-specific quorum/DLX policy.

Reuse proven patterns and fixtures where appropriate, but own Challenge types, DbContext, migration, configuration, consumer, queue and DLQ. Do not reference Habit or Identity assemblies.

## Proposed flow
```text
Identity MySQL outbox
  -> Identity RabbitMqPublisher
  -> teampulse.domain.events
  -> teampulse.challenge.events (Challenge-owned quorum queue)
  -> Challenge.API BackgroundService
  -> Challenge.Application IEmployeeStatusProjectionProcessor
  -> Challenge.Infrastructure transactional store
  -> Challenge MySQL: employee_status_projections + processed_domain_events
```

## Project layout
- Challenge.API: host, configuration validation, startup migration, liveness/readiness and consumer-health endpoints.
- Challenge.Application: envelope contracts, validator/processor, processor/store/reader abstractions and processing outcomes.
- Challenge.Domain: minimal EmployeeStatusProjection and ProcessedDomainEvent entities.
- Challenge.Infrastructure: EF Core/Pomelo DbContext, checked-in migration, transactional store and RabbitMQ adapter.
- Challenge.UnitTests: domain/application unit tests, MySQL and RabbitMQ Testcontainers integration tests.

Dependencies point inward. Domain references no outer project; Application references Domain; Infrastructure references Application/Domain; API composes the host. Do not add project references to Identity or Habit.

## Challenge-owned persistence
Database: teampulse_challenge.

employee_status_projections:
- employee_id: char(36), primary key.
- is_active: boolean, not null.
- lifecycle_version: bigint, not null.
- updated_at_utc: datetime(6), not null.

processed_domain_events:
- event_id: char(36), primary key.
- event_type: varchar(100), not null.
- schema_version: int, not null.
- employee_id: char(36), not null.
- lifecycle_version: bigint, not null.
- processed_at_utc: datetime(6), not null.

No FK to Identity/Habit. No copied employee profile or challenge participation data. eventId uniqueness is mandatory.

## Application processor and atomic store
The processor validates the complete envelope and maps it to a minimal typed lifecycle command. Inject TimeProvider for deterministic updatedAtUtc tests. It has no RabbitMQ or AWS references.

Within a MySQL ReadCommitted transaction:
1. Insert inbox row for eventId.
2. If and only if the inbox primary-key unique constraint conflicts, return Duplicate without further mutation. Other constraint/database errors propagate.
3. Execute a parameterized atomic conditional upsert on employee_id. Insert if absent; on conflict update is_active, lifecycle_version and updated_at_utc only when incoming lifecycle_version is greater than stored.
4. Commit inbox and projection decision together.
5. Return Applied or Stale.

A new eventId with equal/lower version is recorded and returns Stale. On any failure, rollback removes the inbox marker too. Do not implement an unprotected read-then-write race. Invalid envelopes must not invoke the store.

## RabbitMQ adapter
- Use RabbitMQ.Client 7.x asynchronous connection/channel APIs.
- Consume the complete direct envelope body from teampulse.challenge.events.
- Use manual ack, prefetch 1 and one in-flight callback per channel.
- Ack only after Applied, Stale or Duplicate returns from the processor after committed persistence.
- Reject Invalid without requeue to the Challenge DLQ.
- Retry only known transient MySQL/network exceptions with bounded attempts and full-jitter delay. Walk known provider wrapper/inner exceptions; unknown programming/configuration failures must not retry indefinitely.
- On ack/nack/channel failure, close/discard channel so unacknowledged deliveries can be redelivered.
- Reconnect with bounded delay and cancellation-aware shutdown.
- Log only approved operational metadata; never log raw body, note, credentials or connection string.

## Challenge-owned local topology
Update only Challenge entries in infra/rabbitmq/definitions.json:
- Change teampulse.challenge.events to durable quorum.
- Add finite broker delivery limit and Challenge-specific DLX routing key challenge.employee.lifecycle.dead.
- Add durable direct DLX binding to teampulse.challenge.events.dlq, a durable quorum DLQ.
- Enable at-least-once dead-lettering and reject-publish overflow for the source quorum queue.
- Preserve topic exchange and identity routing binding.
- Do not modify Habit queue/DLX/DLQ or other service queues.

Queue declaration arguments in Testcontainers must match JSON topology. Shared definitions are a local development seed; production queue policy and permissions remain Challenge deployment ownership.

## Startup and health
Use a Challenge-only connection string. Local MySQL initialization creates/grants teampulse_challenge to the development application user; production uses separate credentials. Run Database.MigrateAsync before hosted services. Consumer is disabled by default and options validate when enabled.

Expose /health/live, /health/ready (Challenge MySQL only) and /health/consumer (separate RabbitMQ state).

## Testing
- Unit: envelope validation, transition mapping, invalid event does not call store, note is not copied, injected clock.
- MySQL 8.4 Testcontainers: clean/repeated migration, duplicate eventId, stale/equal/newer version, concurrent N/N+1, rollback, exact model schema and reader.
- RabbitMQ Testcontainers: ack-after-commit, duplicate redelivery after commit-before-ack, poison DLQ, bounded transient retry/exhaustion, broker startup unavailable/reconnect, established-session loss/redelivery.
- Host tests: DB readiness independent of broker; consumer health separate; migration before consumer starts.
- Contract/topology tests: Challenge quorum queue, delivery limit, DLX/DLQ binding and no changes to Habit topology.
- CI: existing solution restore/build/test workflow and Python RabbitMQ topology tests.
