# Design: Notification Employee Status Projection Consumer

**Status:** Draft; implementation requires owner approval.

## Architecture

```text
Identity transactional outbox
  -> local RabbitMQ publisher
  -> teampulse.domain.events
  -> teampulse.notification.events (Notification-owned queue)
  -> Notification.API BackgroundService
  -> Notification.Application lifecycle processor
  -> Notification.Infrastructure transactional store
  -> Notification MySQL: employee_status_projections + processed_domain_events
```

Production EventBridge -> SQS -> adapter is deferred. A future transport adapter must call the same application processor; do not add AWS dependencies in this increment.

## Project layout

- Notification.API: host, configuration, startup migration, health endpoints and hosted consumer.
- Notification.Application: envelope validation, processor and store/reader abstractions.
- Notification.Domain: minimal employee-status projection and lifecycle version behavior.
- Notification.Infrastructure: EF Core/Pomelo context, migration, transactional store and RabbitMQ adapter.
- Notification.UnitTests: unit, MySQL and RabbitMQ integration tests.

Dependencies point inward. Notification must not reference another service project.

## Notification-owned persistence

Database: `teampulse_notification`.

`employee_status_projections`:
- employee_id: char(36), primary key.
- is_active: boolean, not null.
- lifecycle_version: bigint, not null.
- updated_at_utc: datetime(6), not null.

`processed_domain_events`:
- event_id: char(36), primary key.
- event_type: varchar(100), not null.
- schema_version: int, not null.
- employee_id: char(36), not null.
- lifecycle_version: bigint, not null.
- processed_at_utc: datetime(6), not null.

No raw JSON, note, reasonCode, actor/profile, preference, notification, delivery or wellness fields. No cross-database foreign keys. Do not add inbox expiry/cleanup.

## Atomic processor/store

The Application processor validates and maps the complete recognized schema-v1 envelope before persistence. It is transport-independent and uses an injected TimeProvider.

Within one MySQL transaction:
1. Insert the processed-event inbox marker.
2. On the specific eventId uniqueness conflict, roll back and return Duplicate; unrelated database errors remain failures.
3. Execute a parameterized conditional upsert keyed by employeeId. Insert when absent; update state/version/time only when incoming lifecycleVersion is strictly greater.
4. Commit inbox and projection decision together.
5. Return Applied or Stale. A new event with equal/lower version is recorded as Stale.

Database uniqueness and the conditional upsert enforce correctness across replicas. Do not use an unprotected read-then-write or in-memory per-employee lock.

## RabbitMQ adapter and topology

Use RabbitMQ.Client 7.x asynchronous APIs. Consume the direct envelope from the existing `teampulse.notification.events` queue. Use manual acknowledgements, prefetch 1, bounded transient retry, cancellation-aware reconnect and separate consumer health.

The current local definitions show the Notification source queue is durable but has no queue arguments, and no Notification DLQ/binding exists. After approval, change only Notification topology:
- Preserve source queue name and existing binding to `teampulse.domain.events` with routing key `identity.employee.lifecycle`.
- Upgrade the source to a durable quorum queue with delivery limit 5, at-least-once dead-lettering to `teampulse.domain.events.dlx`, routing key `notification.employee.lifecycle.dead`, and reject-publish overflow.
- Add durable quorum queue `teampulse.notification.events.dlq` and bind it to the existing direct DLX with the Notification routing key.
- Do not change Habit, Challenge, Event Tracking or Insight topology.

**Persistent-broker migration is mandatory:** RabbitMQ queue type/arguments cannot be changed by redeclaring an existing queue. Before importing new definitions, stop Notification consumer and Identity publisher, inspect and preserve/drain pending messages, then deliberately recreate only the Notification queue. Never delete queued lifecycle events or reset the shared RabbitMQ volume as a shortcut. Add exact topology contract tests.

## Local MySQL initialization

Add `infra/mysql/init/04-notification-database.sql` to create `teampulse_notification` with the established utf8mb4 collation and grant the local `teampulse_app` user access; mount it in Compose. Existing persistent MySQL volumes do not rerun init scripts, so document safe manual database/grant creation without resetting the volume.

## Startup, health and configuration

- Consumer is opt-in and disabled by default; validate RabbitMQ options only when enabled.
- Apply checked-in EF migrations before hosted services start.
- Use a Notification-only connection string.
- Host-run uses localhost MySQL; Compose-hosted API uses the mysql service name.
- `/health/live` is process-only, `/health/ready` checks Notification MySQL only, and `/health/consumer` independently reports RabbitMQ state.

## Test strategy

- Unit: schema/event validation, transition mapping, unknown additive properties, note exclusion, duplicate/stale outcomes and retry policy.
- MySQL 8.4 Testcontainers: clean/repeated migration, exact schema, inbox/projection rollback, duplicate eventId, stale/equal/newer and concurrent version ordering.
- RabbitMQ Testcontainers: ack-after-commit, commit-before-ack replay, poison DLQ, transient retry/exhaustion, failed DLQ routing leaves source unacked, broker startup/reconnect and established-session loss.
- API-host: production startup migration, reader/store registration, DB readiness independent of broker health and consumer health transitions.
- Topology: exact Notification quorum source/DLQ arguments and bindings; assert other service queues are unchanged.
- Privacy: raw event body/note/secrets/connection strings absent from persistence and logs.

## Operational constraints

No notification side effects are permitted in this projection increment. Do not provision AWS, alter EventBridge, implement retention cleanup, or claim immediate global JWT revocation.
