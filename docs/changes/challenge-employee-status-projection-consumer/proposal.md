# Proposal: Challenge Employee Status Projection Consumer

**Status:** Proposed — awaiting explicit owner approval.

## Context
Identity owns employee lifecycle state and publishes schema-v1 EmployeeDeactivated and EmployeeReactivated events through its transactional outbox. The shared Employee Status Projection Contract v1 requires each receiving service to maintain a minimal, version-aware local projection and deduplicate at-least-once delivery. Habit is the first completed consumer. Challenge is the next receiving service in the six-service architecture; its local RabbitMQ queue exists, but no Challenge service project exists on this branch.

## Goal
Create the Challenge-owned employee-status projection and local lifecycle-event consumer using the existing contracts. Establish only the Challenge foundation required for this consumer.

## Scope
- Add Challenge API, Application, Domain and Infrastructure projects plus focused tests.
- Give Challenge its own MySQL database and checked-in EF Core/Pomelo migration, applied at host startup.
- Store only employeeId, isActive, lifecycleVersion and updatedAtUtc in the projection, plus minimal processed-event inbox metadata.
- Process events atomically, deduplicate by eventId and apply only strictly newer lifecycle versions.
- Consume the direct schema-v1 envelope from the existing local RabbitMQ Challenge queue with manual acknowledgement after commit.
- Add Challenge-owned bounded retry, poison-message handling and durable DLQ topology.
- Keep the application processor transport-independent for a future SQS adapter.

## Non-goals
- Programmes, tasks, eligibility, participation, scoring, evidence or challenge workflows.
- Changing challenge/participation state on employee deactivation/reactivation.
- Defining inactive-employee historical-read or participation-mutation policy beyond the shared projection contract.
- Cross-service database access, foreign keys or Identity/Habit project references.
- EventBridge/SQS implementation or AWS provisioning; cloud setup is deferred by the owner.
- Global immediate JWT revocation claims.

Follow AGENTS.md, ADR-0001/0002, service ownership, event envelope and Employee Status Projection Contract v1. Challenge owns programmes, tasks, eligibility, participation and authoritative challenge state. RabbitMQ is local development transport; EventBridge remains intended production fan-out.

Detailed test-mapped acceptance criteria are in spec.md. Implementation must not begin until this SDD package is reviewed and approved under mandatory SDD workflow.
