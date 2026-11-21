# Proposal: Notification Employee Status Projection Consumer

**Status:** Owner-approved on 2026-11-21. Implementation is authorized only within this approved scope.

## Context

Identity is the authoritative owner of employee lifecycle state and publishes schema-v1 `EmployeeDeactivated` and `EmployeeReactivated` events. The shared Employee Status Projection Contract v1 requires Notification to maintain its own minimal employee-status projection rather than read Identity's database.

Notification owns notification preferences, notification records and delivery history. This increment adds only the lifecycle projection foundation; it does not define notification campaigns, delivery behavior, preference changes or employee-facing APIs.

The local RabbitMQ `teampulse.notification.events` queue already exists. Habit, Challenge and Event Tracking provide established consumer patterns.

## Goal

Add a Notification-owned, idempotent employee-status projection consumer over local RabbitMQ. Keep the application processor transport-independent so a future SQS adapter can reuse it after owner-managed AWS infrastructure setup.

## Scope

- Add Notification API, Application, Domain and Infrastructure projects to the existing six-service solution.
- Add a Notification-owned logical MySQL database, EF Core model, checked-in migration and startup migration.
- Persist only the four shared projection fields and a minimal processed-event inbox.
- Validate recognized schema-v1 lifecycle events and apply only strictly newer lifecycle versions.
- Commit inbox and projection decisions atomically.
- Add an opt-in RabbitMQ consumer for the existing Notification queue, manual acknowledgements, bounded retry/reconnect, separate consumer health and a Notification-owned DLQ.
- Add unit, MySQL 8.4, RabbitMQ Testcontainers and topology tests plus local run/migration documentation.

## Non-goals

- Sending, scheduling, retrying or suppressing notifications.
- Changing notification preferences, records or delivery history.
- Employee profile/role replication or cross-service database access.
- Activity, Habit or Challenge business workflows.
- SQS/EventBridge production adapter, AWS credentials or cloud provisioning.
- Global immediate JWT revocation, inbox cleanup or DLQ operator tooling.

## Approval boundary

The shared event and projection contracts and Notification service ownership are already established. This proposal introduces no new product behavior. Owner approval of this SDD package is required before implementation begins.
