# Design: Event Tracking Employee Status Projection Consumer

## Architecture

```text
Identity transactional outbox
        |
Identity local RabbitMQ publisher
        |
teampulse.domain.events
        |
teampulse.event-tracking.events (existing durable queue)
        |
EventTracking.API BackgroundService
        |
EventTracking.Application lifecycle processor
        |
EventTracking.Infrastructure transactional store
        |
Event Tracking MySQL: employee_status_projections + processed_domain_events
```

The future production path is EventBridge -> Event Tracking SQS queue -> SQS transport adapter -> the same application processor. Do not add AWS dependencies in this increment.

## Project layout

- EventTracking.API: host, configuration validation, startup migration, health endpoints and hosted consumer.
- EventTracking.Application: lifecycle event contract, processor and projection-reader abstraction.
- EventTracking.Domain: minimal EmployeeStatusProjection and version transition behavior.
- EventTracking.Infrastructure: EF Core/Pomelo context, migration, transactional store and RabbitMQ adapter.
- EventTracking.UnitTests: domain, application, MySQL and broker integration tests.

Dependencies point inward. Event Tracking must not reference Identity, Habit or Challenge projects.

## Event Tracking-owned schema

### employee_status_projections

- employee_id: char(36), primary key.
- is_active: boolean, not null.
- lifecycle_version: bigint, not null.
- updated_at_utc: datetime(6), not null.

### processed_domain_events

- event_id: char(36), primary key.
- event_type: varchar(100), not null.
- schema_version: int, not null.
- employee_id: char(36), not null.
- lifecycle_version: bigint, not null.
- processed_at_utc: datetime(6), not null.

No raw JSON, note, reasonCode, profile, activity or evidence fields. No cross-database foreign keys. Inbox retention/cleanup is not introduced by this change.

## Atomic processing

The Application layer validates the complete recognized envelope before persistence, including the Identity-owned `source=hr-admin-api` value and the trimmed 1–100-character `reasonCode` bound. These constraints mirror `docs/changes/identity-hr-employee-lifecycle/spec.md`; do not accept arbitrary source identifiers or weaken the producer's reason-code validation. The Infrastructure store uses one MySQL transaction:

1. Insert the inbox marker for eventId.
2. On the specific inbox eventId uniqueness conflict, roll back and return Duplicate; unrelated database errors remain failures.
3. Apply a parameterized atomic conditional upsert keyed by employeeId. Insert if absent; update isActive, lifecycleVersion and updatedAtUtc only if the incoming version is strictly greater.
4. Commit the inbox marker and projection decision together.
5. Return Applied or Stale. Both are acknowledged only after commit.

A valid new eventId with an equal/lower version is recorded as processed while the projection remains unchanged. Concurrent N/N+1 events must leave N+1 regardless of arrival order.

## RabbitMQ adapter

Use RabbitMQ.Client 7.x asynchronous APIs, consistent with the existing Identity publisher and Habit/Challenge consumers.

- Consume the existing `teampulse.event-tracking.events` queue only.
- Manual acknowledgements; initial prefetch 1 and one active handler per channel.
- Parse the direct shared envelope from the RabbitMQ body; no EventBridge wrapper exists on local RabbitMQ.
- Invoke the transport-independent Application processor with cancellation.
- Ack only after committed Applied/Stale/Duplicate outcomes.
- Malformed/unsupported events are dead-lettered without requeue.
- Known transient database/network errors receive bounded configurable retry; exhaustion is dead-lettered.
- If DLQ publication cannot be confirmed, close the source channel without ack.
- Reconnect with bounded delay; broker state does not affect database readiness.
- Log only safe identifiers, event type, lifecycle version and sanitized outcome/classification.

## Local topology and existing-queue migration

The current `infra/rabbitmq/definitions.json` has a durable `teampulse.event-tracking.events` queue bound to `teampulse.domain.events` with routing key `identity.employee.lifecycle`, but its queue arguments are empty. It is not currently a quorum queue and has no delivery limit or dead-letter policy. Do not assume it already has the Habit/Challenge topology.

After approval, update only the Event Tracking queue:
- Preserve the existing queue name and binding/routing key.
- Convert it to a durable quorum queue with delivery limit 5.
- Set `x-dead-letter-exchange=teampulse.domain.events.dlx`, `x-dead-letter-routing-key=event-tracking.employee.lifecycle.dead`, `x-dead-letter-strategy=at-least-once` and `x-overflow=reject-publish`.
- Add durable quorum queue `teampulse.event-tracking.events.dlq` and bind it to the existing direct `teampulse.domain.events.dlx` exchange using the new routing key.
- Do not modify Habit, Challenge, Insight or Notification queues/bindings.

**Existing local broker migration is a prerequisite:** RabbitMQ cannot redeclare an existing queue with incompatible queue-type/arguments. Before importing the changed definitions into a persistent local broker, stop the Event Tracking consumer and Identity publisher, inspect and drain/preserve any queued messages, then explicitly delete/recreate the Event Tracking queue with the approved arguments. Never silently discard pending lifecycle messages or reset the shared RabbitMQ volume without confirming it is disposable. Fresh CI brokers can import the new definitions directly. Add a topology contract test asserting the exact source/DLQ queue arguments and binding.

The MySQL local bootstrap currently creates `teampulse_habit` and `teampulse_challenge` only. Add `infra/mysql/init/03-event-tracking-database.sql` to create `teampulse_event_tracking` with the existing utf8mb4 collation and grant `teampulse_app` access, then mount it in Compose after the existing initialization scripts. Existing persistent MySQL volumes do not rerun init scripts; document the safe manual database/grant command for those volumes.

## Startup and health

- Use the `EventTrackingDatabase` connection string and local `teampulse_event_tracking` database initialization/grant.
- Apply migrations before hosted consumers start.
- Consumer disabled by default; validate broker options only when enabled.
- /health/live is process liveness; /health/ready checks MySQL only; /health/consumer independently reports broker consumer state.
- Document host-run localhost and Compose mysql connection strings separately.

## Test strategy

- Domain/application: transition mapping, required fields, malformed JSON, unsupported schema/type, invalid UUID/version, stale/equal behavior.
- MySQL 8.4 Testcontainers: fresh/repeated migration, inbox+projection atomicity, rollback, duplicate eventId, stale/equal and concurrent N/N+1.
- RabbitMQ Testcontainers: ack-after-commit, commit-before-ack redelivery, poison DLQ, bounded retry/exhaustion, failed DLQ route leaves source unacknowledged, startup and established-session reconnect.
- API-host integration: clean database migration at startup, MySQL readiness and production projection reader.
- Log privacy: assert event body, note, credentials and broker URI are absent.
