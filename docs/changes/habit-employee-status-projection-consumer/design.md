# Design: Habit Employee Status Projection Consumer

## Architecture

Add the first downstream consumer without changing Identity or the six-service architecture.

```text
Identity MySQL outbox
        |
Identity RabbitMqPublisher (local transport)
        |
teampulse.domain.events
        |
teampulse.habit.events (durable quorum queue)
        |
Habit.API BackgroundService
        |
Habit.Application IEmployeeStatusProjectionProcessor
        |
Habit.Infrastructure processor/store
        |
Habit MySQL: employee_status_projections + processed_domain_events
```

The future production path will be EventBridge -> Habit SQS queue -> an SQS transport adapter -> the same application processor. Do not add AWS dependencies to Habit in this increment.

## Project layout

- Habit.API: host, configuration validation, health endpoints, startup migration and hosted consumer registration.
- Habit.Application: envelope/command contracts, processor and projection reader abstractions.
- Habit.Domain: minimal EmployeeStatusProjection model and version transition behavior.
- Habit.Infrastructure: EF Core/Pomelo DbContext, checked-in migration, transactional store and RabbitMQ transport adapter.
- Habit.UnitTests: domain, application, persistence and broker integration tests.

Keep dependencies inward: API and Infrastructure may reference Application/Domain; Application may reference Domain; Domain references no outer layer. Do not reference Identity projects.

## Habit-owned schema

Create two tables in the Habit database.

### employee_status_projections

- employee_id: char(36), primary key.
- is_active: boolean, not null.
- lifecycle_version: bigint, not null.
- updated_at_utc: datetime(6), not null.

No email, profile, tenant, role, note or wellness fields are stored. There is no foreign key to Identity.

### processed_domain_events

- event_id: char(36), primary key.
- event_type: varchar(100), not null.
- schema_version: int, not null.
- employee_id: char(36), not null.
- lifecycle_version: bigint, not null.
- processed_at_utc: datetime(6), not null.

The inbox stores only metadata required for deduplication and operations. It must not persist the original JSON body, reasonCode or note. event_id uniqueness is the idempotency fence. The employee_id/lifecycle_version pair may be indexed for support diagnostics but must not replace eventId as the deduplication key.

## Atomic processing algorithm

The Application processor validates the envelope before entering persistence. It passes a minimal typed lifecycle event to the Habit-owned store.

Within one MySQL transaction at Read Committed isolation:

1. Insert a processed_domain_events marker for eventId.
2. If the unique eventId already exists, roll back the attempted transaction and return Duplicate; do not update the projection.
3. Apply an atomic conditional upsert keyed by employeeId. Insert the projection if absent. On conflict, update isActive, lifecycleVersion and updatedAtUtc only when the incoming lifecycleVersion is strictly greater than the stored version.
4. Commit the inbox marker and projection decision together.
5. Return Applied or Stale. Both are successfully processed messages and may be acknowledged after commit.

A stale/equal event with a new eventId is still recorded in the inbox, while the projection remains unchanged. This prevents repeated stale delivery from consuming broker capacity.

Use a parameterized EF ExecuteSqlInterpolated/ExecuteSqlRaw command or an equivalent provider-supported operation for the conditional upsert. Do not perform an unprotected read-then-write. The MySQL integration test must race versions N and N+1 and prove that the final row is N+1 regardless of arrival order.

If any operation fails, roll back the transaction. A duplicate-key race on eventId is treated as Duplicate only when it is specifically the inbox primary-key conflict; unrelated database constraint errors remain failures.

## RabbitMQ adapter and acknowledgement boundary

Use RabbitMQ.Client 7.x asynchronous APIs, consistent with the Identity publisher.

- Connect to the configured broker with a stable client-provided connection name and automatic recovery.
- Consume only teampulse.habit.events.
- Use manual acknowledgement, prefetch 1 and one in-flight handler per channel for the initial implementation.
- Parse the complete envelope from the RabbitMQ body. Do not expect an EventBridge outer wrapper on the local transport.
- Invoke the Application processor with a CancellationToken.
- BasicAck only after the application store confirms the transaction committed.
- Duplicate and Stale outcomes are successful processing outcomes and are acknowledged after the transaction boundary.
- If the process crashes after commit and before BasicAck, RabbitMQ redelivers; the eventId inbox fence makes the replay a no-op.
- Malformed/unsupported events are BasicNack(requeue:false) so the queue's dead-letter policy captures them.
- Retry transient database errors a bounded number of times with configurable exponential delay and jitter. After exhaustion, reject without requeue.
- Do not swallow cancellation as a successful acknowledgement.
- On broker loss, stop consuming and reconnect with bounded backoff. Do not fail the database readiness check solely because RabbitMQ is unavailable.

Because RabbitMQ channels are not shared concurrently, the first implementation processes one delivery at a time per channel. Horizontal replicas provide queue-level concurrency. Increasing per-process prefetch/concurrency requires a separate channel/ack concurrency review.

## Local broker topology

Extend only the Habit-owned portion of infra/rabbitmq/definitions.json:

- Change teampulse.habit.events to a durable quorum queue.
- Configure a finite delivery limit (initial local default 5).
- Configure a durable direct dead-letter exchange teampulse.domain.events.dlx.
- Route exhausted/poison messages with routing key habit.employee.lifecycle.dead.
- Create durable quorum queue teampulse.habit.events.dlq and bind it to that dead-letter route.
- Enable at-least-once dead-lettering and reject-publish overflow for the quorum source queue so an unavailable DLQ cannot silently lose dead-lettered messages.

Do not change the other four service queues. The shared definitions file is a local development seed only; production Habit queue, SQS permissions and DLQ remain Habit deployment ownership.

## Host and database startup

Habit.API binds a HabitDatabase connection string and calls Database.MigrateAsync before app.Run starts hosted services. Use a checked-in initial migration and EF model snapshot. The local Compose MySQL initialization creates teampulse_habit and grants the existing local teampulse_app user access to that schema. Host-run and container-run connection strings must be documented separately (localhost vs mysql).

Keep the consumer disabled by default. When enabled, validate RabbitMQ URI, queue, retry and prefetch options before the host starts. Provide aggregate /health/live and /health/ready endpoints; readiness checks Habit MySQL, while broker connectivity is exposed as a separate consumer-health signal and does not make the database probe misleading.

## Testing strategy

- Domain tests: active/inactive mapping, strictly newer version and stale/equal no-op behavior.
- Application contract tests: valid event types, schema and payload validation, malformed JSON and transition mismatch.
- MySQL 8.4 Testcontainers: clean startup migration, repeat startup, atomic inbox/projection commit, rollback, duplicate eventId, stale/equal version, and concurrent N/N+1 upserts.
- RabbitMQ Testcontainers: manual ack after commit, redelivery after commit-before-ack, poison DLQ routing, bounded retry exhaustion, source-message retention when DLQ is unavailable, and reconnect.
- Keep test fixtures isolated from Identity's database. Do not add cross-service project references.
