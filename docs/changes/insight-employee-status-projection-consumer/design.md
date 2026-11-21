# Design: Insight Employee Status Projection Consumer

**Status:** Draft — awaiting owner approval.

## Architecture
Identity transactional outbox -> local RabbitMQ publisher -> teampulse.domain.events -> teampulse.insight.events -> Insight.API BackgroundService -> Insight.Application lifecycle processor -> Insight.Infrastructure DynamoDB store -> Insight-owned DynamoDB table.

Production EventBridge -> SQS -> Insight SQS adapter is deferred. A future transport adapter calls the same application processor. Do not add AWS deployment or credentials in this increment.

## Project layout
- Insight.API: host, configuration, local table bootstrap, health endpoints and hosted consumer registration.
- Insight.Application: envelope validation, processor, minimal contracts and persistence abstractions.
- Insight.Domain: minimal employee-status projection and lifecycle transition model.
- Insight.Infrastructure: AWS SDK DynamoDB client/store, DynamoDB Local configuration and RabbitMQ adapter.
- Insight.UnitTests: domain/application unit tests plus DynamoDB Local and RabbitMQ integration tests.

Dependencies point inward. Insight must not reference Identity, Habit, Challenge, Event Tracking or Notification projects. No EF Core or MySQL provider is added to Insight.

## DynamoDB table
Proposed configured table name, local default teampulse-insight-lifecycle, with string PK and SK and on-demand billing.

Projection item:
- PK=EMPLOYEE#<lowercase UUID D>; SK=STATUS.
- employeeId: lowercase UUID D string.
- isActive: BOOL.
- lifecycleVersion: DynamoDB Number (signed 64-bit integer).
- updatedAtUtc: fixed UTC ISO-8601 string.

Inbox item:
- PK=EVENT#<lowercase UUID D>; SK=INBOX.
- eventId: lowercase UUID D string.
- eventType: string; schemaVersion: number.
- employeeId: lowercase UUID D string; lifecycleVersion: number.
- processedAtUtc: fixed UTC ISO-8601 string.

No GSI, TTL, raw payload, optional note, reasonCode, actor/profile or source-service attributes. No analytics items are created in this change.

## Transactional persistence algorithm
The store strongly reads the employee projection and event inbox, then chooses one atomic TransactWriteItems branch.

### New or newer event
- Conditional Put the inbox item with attribute_not_exists(PK).
- Update the projection in the same transaction.
- If projection was absent, require attribute_not_exists(PK).
- If projection existed at an older version, require lifecycleVersion = observedVersion.
- Set employeeId, isActive, lifecycleVersion and updatedAtUtc.
- If the condition fails due to a concurrent event, re-read and retry within a bounded budget.

### Stale/equal event
- Conditional Put the inbox item.
- Add a ConditionCheck for the projection requiring lifecycleVersion >= incomingVersion.
- Do not update the projection.
- If the condition no longer holds, re-read and retry.

### Duplicate and failure classification
- If the event inbox item exists, return Duplicate.
- If a transaction is cancelled, strongly read the event item before classifying. Only an existing inbox key is a duplicate; projection-version races are retried.
- Propagate non-conflict persistence errors. The RabbitMQ adapter applies bounded retry only to classified transient DynamoDB/network failures.
- Inbox Put and projection Update/ConditionCheck are always one transaction. No window exists in which one commits without the other.

Use AWS SDK transaction cancellation reasons carefully. Integration tests must prove duplicate and optimistic-conflict classification against DynamoDB Local, not rely only on exception message strings.

## RabbitMQ adapter and topology
Use RabbitMQ.Client 7.x async APIs, direct envelope body, manual ack, prefetch 1, bounded transient retry, cancellation-aware reconnect and independent consumer health.

The current local definitions show Insight's source queue is durable classic topology with no queue arguments and no Insight DLQ/binding. After approval, change only Insight topology:
- Preserve source queue name and binding to teampulse.domain.events with routing key identity.employee.lifecycle.
- Upgrade source to durable quorum with delivery limit 5, at-least-once dead-lettering to teampulse.domain.events.dlx, routing key insight.employee.lifecycle.dead and reject-publish overflow.
- Add durable quorum DLQ teampulse.insight.events.dlq and bind it to the existing direct DLX with that routing key.
- Do not change Habit, Challenge, Event Tracking or Notification topology.

**Persistent-broker migration is mandatory:** queue type/arguments cannot be changed by redeclaring an existing queue. Stop Insight consumer and Identity publisher, preserve/buffer pending and unacknowledged messages, verify counts, recreate only Insight queue, restore messages and verify bindings/counts before resuming. Never purge messages or reset the shared RabbitMQ volume. The runbook must be reviewed before a live migration. This SDD changes only repository-local definitions/tests; no live broker is modified.

## Local DynamoDB initialization
- Add DynamoDB Local for development, separate from AWS services.
- Add a Development-only table bootstrapper guarded by explicit configuration; create the configured local table if absent and wait for ACTIVE.
- Production configuration points to a provisioned table and never auto-creates cloud tables on API startup.
- Integration tests use isolated DynamoDB Local Testcontainers instances. No real AWS account or credentials are required.

## Startup, health and configuration
- Consumer is opt-in and disabled by default; validate RabbitMQ options only when enabled.
- Configure table name and endpoint through Insight-specific options. Do not hard-code AWS account IDs, regions, ARNs or credentials.
- Local endpoint uses dummy development credentials only where required by the AWS SDK.
- /health/live is process-only; /health/ready checks the Insight table; /health/consumer independently reports RabbitMQ state.
- No API readiness dependency on RabbitMQ.

## Test strategy
- Unit: schema/event validation, transition mapping, unknown additive fields, note exclusion, duplicate/stale outcomes, retry classification and options.
- DynamoDB Local: table bootstrap/idempotency, exact keys/attributes, transaction atomicity, duplicate eventId, stale/equal/newer versions, cancellation classification and concurrent maximum-version preservation.
- RabbitMQ Testcontainers: ack-after-commit, commit-before-ack replay, poison DLQ, transient retry/exhaustion, failed DLQ routing leaves source unacked, broker startup/reconnect and session loss.
- API-host: local table initialization, production-mode no-auto-create, readiness independent of broker and consumer health transitions.
- Topology: exact Insight quorum source/DLQ arguments and bindings; assert other service queues remain unchanged.
- Privacy: raw body/note/secrets/connection strings absent from DynamoDB and logs.

## Operational constraints
No analytics or notification side effects are permitted. Do not provision AWS, alter EventBridge/SQS or add inbox cleanup.
