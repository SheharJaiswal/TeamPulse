# Proposal: Event Tracking Employee Status Projection Consumer

**Status:** Draft — awaiting owner approval. No implementation is authorized by this proposal alone.

## Context

Identity is the authoritative owner of employee lifecycle state and publishes versioned `EmployeeDeactivated` and `EmployeeReactivated` events. The shared Employee Status Projection Contract v1 requires Event Tracking to maintain a local status projection rather than query Identity's database.

Event Tracking owns submitted activity records and audit history. It must enforce its own employee/resource authorization boundary without taking ownership of lifecycle state or copying employee profile data.

Identity's local RabbitMQ transport and the durable `teampulse.event-tracking.events` queue already exist. Habit and Challenge have implemented projection-consumer patterns that can inform this service without changing the shared event contract.

## Goal

Add the Event Tracking service foundation needed to consume schema-v1 employee lifecycle events over local RabbitMQ and maintain an Event Tracking-owned employee-status projection and processed-event inbox in its own MySQL database.

The application processor must remain transport-independent so a future SQS adapter can reuse it after owner-managed EventBridge infrastructure setup.

## Scope

- Add EventTracking API, Application, Domain and Infrastructure projects to the existing six-service solution.
- Add an Event Tracking-owned MySQL database, EF Core model, checked-in migration and startup migration.
- Implement the minimal employee status projection and processed-event inbox.
- Validate supported lifecycle envelopes and apply only strictly newer lifecycle versions.
- Process inbox and projection changes atomically and deduplicate at-least-once delivery.
- Add a local RabbitMQ consumer for the existing Event Tracking queue with manual acknowledgements, bounded retries, reconnect and a service-owned DLQ.
- Add unit, MySQL 8.4 and RabbitMQ Testcontainers coverage plus local run documentation.

## Non-goals

- Activity submission, evidence, audit-history or retention workflows.
- Employee profile, credential, role, organization or team replication.
- Reads or writes against Identity, Habit or Challenge databases.
- Challenge programme/participation behavior or Insight aggregation.
- AWS credentials, EventBridge/SQS deployment or CloudWatch setup.
- Global immediate JWT revocation, DLQ redrive tooling or operator APIs.

## Approval boundary

The shared projection contract and service ownership are already established. This proposal deliberately does not introduce new business rules. Owner approval of this SDD package is required before implementation begins.
