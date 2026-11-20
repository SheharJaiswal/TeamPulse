# Proposal: Habit Employee Status Projection Consumer

**Status:** Ready for implementation under the existing Employee Status Projection Contract v1.

## Context

Identity is the authoritative owner of employee lifecycle state and already publishes schema-v1 EmployeeDeactivated and EmployeeReactivated events through its transactional outbox. The local RabbitMQ transport and the durable Habit queue are implemented, but Habit does not yet have a service project or consumer. Until each receiving service maintains its own projection, a deactivation event cannot inform that service's local authorization boundary.

The approved shared contract at docs/contracts/employee-status-projection.md defines the complete projection shape and consumer invariants. This change is the first downstream implementation and must not expand that contract or invent Habit product behavior.

## Goal

Create the Habit service foundation required to consume Identity lifecycle events over local RabbitMQ, validate the existing envelope, and atomically maintain a Habit-owned employee-status projection and event inbox in a separate MySQL database.

The processing core must be transport-independent so a later SQS adapter can reuse it when the owner provisions EventBridge infrastructure.

## Scope

- Add Habit API/host, Application, Domain and Infrastructure projects to the existing six-service solution.
- Add Habit-owned MySQL persistence using EF Core 8 and Pomelo, with checked-in migrations applied at host startup.
- Add the employee-status projection and a minimal processed-event inbox.
- Add an application processor that validates supported lifecycle events, deduplicates by eventId and applies only strictly newer lifecycleVersion values.
- Add a local RabbitMQ consumer for teampulse.habit.events with manual acknowledgements.
- Add bounded processing retries and a Habit-owned poison-message DLQ in the local RabbitMQ topology.
- Add unit, MySQL 8.4 and RabbitMQ Testcontainers tests, plus local run documentation.

## Non-goals

- Habit CRUD, schedules, preferences, goals or other wellness features.
- Employee profile, credential, role or organization data replication.
- Any read or write access to Identity's database.
- Challenge, Event Tracking, Insight or Notification consumers.
- AWS credentials, EventBridge provisioning, SQS consumption or CloudWatch configuration.
- A claim of immediate global JWT revocation. Habit resource endpoints do not yet exist; this increment prepares the status data for their future authorization boundary.
- Automated DLQ redrive or operator administration APIs.

## Compatibility

The consumer processes the same immutable schema-v1 envelope used by Identity. RabbitMQ is only the local development transport. EventBridge/SQS remains the intended production transport and will receive a separate transport adapter without changing the projection processor or event contract.
