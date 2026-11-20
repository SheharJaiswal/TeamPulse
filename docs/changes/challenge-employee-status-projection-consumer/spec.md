# Specification: Challenge Employee Status Projection Consumer

**Status:** Proposed; implementation not started.  
**Authority:** AGENTS.md, ADR-0001/0002, docs/architecture/service-ownership.md, docs/architecture/communication.md, docs/contracts/event-envelope.md and docs/contracts/employee-status-projection.md.

## Service boundary
1. Challenge is one of the existing six services. It owns programmes, tasks, eligibility, participation and authoritative challenge state.
2. Challenge owns a separate logical MySQL database named teampulse_challenge locally and separately provisioned production credentials.
3. Challenge must not query or reference Identity or Habit databases. Employee IDs are stable external identifiers; no cross-service foreign keys.
4. Use ASP.NET Core .NET 8, EF Core 8 and Pomelo.EntityFrameworkCore.MySql with MySQL 8.4, consistent with ADR-0001.
5. Commit schema changes as EF Core migrations. Apply checked-in migrations at Challenge host startup before hosted consumers start or the API accepts traffic. Multi-replica production uses a coordinated migration deployment step.
6. Add only the Challenge foundation required for this projection consumer. Do not create placeholder business endpoints or invent programme/participation behavior.

## Projection contract
Store exactly the Employee Status Projection Contract v1 fields:

| Field | Type | Constraint |
|---|---|---|
| employeeId | UUID | Primary key; external Identity identifier |
| isActive | boolean | Latest accepted lifecycle state |
| lifecycleVersion | signed 64-bit integer | Non-negative persisted version; incoming version positive |
| updatedAtUtc | UTC timestamp | Challenge processing time when a newer version is applied |

Do not copy email, display name, organization/team IDs, role, credentials, JWTs, password hashes, reasonCode, note, habits, activity submissions, evidence or wellness data.

A Challenge-owned processed_domain_events inbox stores only eventId, eventType, schemaVersion, employeeId, lifecycleVersion and processedAtUtc. It must not persist original JSON or optional HR note. Unique eventId is the deduplication fence.

## Supported messages
Accept only the complete shared schema-v1 envelope directly in the RabbitMQ message body:
- EmployeeDeactivated: previousStatus active, newStatus inactive, resulting isActive false.
- EmployeeReactivated: previousStatus inactive, newStatus active, resulting isActive true.

Validate eventId, correlationId, non-default UTC occurredAtUtc, schemaVersion, eventType and payload identifiers. Require positive lifecycleVersion, non-empty source/reasonCode and internally consistent transition values. Optional note may be accepted but never persisted or logged. Reject malformed JSON, unsupported event/schema, invalid identifiers/versions and inconsistent transitions before persistence.

RabbitMQ does not carry an EventBridge outer wrapper. A future SQS adapter extracts the envelope from EventBridge detail and calls the same application processor; it is out of scope.

## Idempotency, ordering and transaction
1. Assume at-least-once delivery and deduplicate by eventId in the Challenge-owned inbox.
2. For a new eventId, insert inbox marker and evaluate projection in one MySQL transaction.
3. Insert projection when absent. Update only when incoming lifecycleVersion is strictly greater than stored.
4. A valid new event with equal/lower version is recorded as processed but does not change projection.
5. Repeated eventId returns Duplicate without another projection/inbox mutation.
6. Inbox and projection decision commit atomically; any failure rolls back both.
7. Concurrent events for one employee must not regress lifecycleVersion. Use atomic conditional MySQL upsert or equivalent locking and test concurrent N/N+1 events with MySQL 8.4 Testcontainers.
8. Set updatedAtUtc from Challenge's UTC processing clock only when applying a newer version.

## Challenge domain behavior boundary
This change materializes the shared lifecycle projection only. It does not create, cancel, suspend, complete or otherwise mutate programmes, tasks, eligibility, participation, completion records, scores or evidence when employee status changes. It adds no Challenge business endpoints.

When employee-facing Challenge resource operations are introduced, they must consult this local projection at the Challenge authorization boundary in addition to validated JWT claims and normal tenant/resource authorization. The policy for historical participation reads versus new participation/task mutations by inactive employees remains an explicit product question; do not infer or implement it in this projection-only increment. No global immediate token-revocation claim is permitted.

## RabbitMQ consumer behavior
1. Consume only from teampulse.challenge.events; declare Challenge-owned topology in local definitions and tests.
2. Use RabbitMQ.Client 7.x async APIs, manual acknowledgements and prefetch 1 for one active handler per channel.
3. Acknowledge only after inbox/projection transaction commits. Applied, Stale and Duplicate are successful outcomes.
4. On process/broker loss before ack, allow redelivery; eventId deduplication makes replay harmless.
5. Reject malformed/unsupported messages without requeue to a durable Challenge-owned DLQ.
6. Retry known transient MySQL/network failures a bounded number of times with configurable delay/jitter; reject after exhaustion. Do not classify arbitrary programming errors as transient.
7. Reconnect with bounded delay. Broker availability must not make Challenge database readiness misleading.
8. Consumer is disabled by default and enabled explicitly for local development. No AWS credentials are required.

Queue delivery-limit/retry settings are Challenge-owned configuration/topology values and must be validated by tests. Do not modify Habit queue or DLQ.

## Host, persistence and health
- Add Challenge.API, Challenge.Application, Challenge.Domain and Challenge.Infrastructure with inward-only references; add Challenge.UnitTests to the solution.
- Configure a Challenge-only connection string and local teampulse_challenge database initialization/grant.
- Apply checked-in EF migrations at host startup before hosted consumers.
- Expose process liveness, Challenge database readiness and separate consumer-health signals. RabbitMQ availability is not a database-readiness dependency.
- Do not log event bodies, HR notes, credentials or connection strings.

## Acceptance criteria and test mapping

| ID | Acceptance criterion | Verification |
|---|---|---|
| CEPS-01 | Challenge projects are added within the six-service solution and obey Clean Architecture references. | Release build and project-reference review. |
| CEPS-02 | Challenge owns its MySQL DB and has no Identity/Habit references or cross-service FKs. | Connection/model and dependency-boundary tests. |
| CEPS-03 | Checked-in EF migration creates projection/inbox schema; startup applies it on clean and repeated startup. | MySQL 8.4 Testcontainers startup tests. |
| CEPS-04 | Projection/inbox schemas contain only approved fields/metadata. | EF model and migration assertions. |
| CEPS-05 | Only valid schema-v1 deactivation/reactivation envelopes are accepted. | Processor tests for valid, malformed, unsupported and inconsistent messages. |
| CEPS-06 | New eventId records inbox and projection decision atomically. | MySQL transaction/rollback integration tests. |
| CEPS-07 | Duplicate eventId is a no-op; stale/equal versions cannot regress projection. | MySQL integration tests. |
| CEPS-08 | Concurrent N/N+1 processing leaves maximum lifecycle version. | Concurrent MySQL Testcontainers test. |
| CEPS-09 | Manual ack occurs only after commit; crash-before-ack redelivery is deduplicated. | RabbitMQ Testcontainers commit/replay test. |
| CEPS-10 | Poison messages and exhausted transient failures reach Challenge-owned DLQ. | RabbitMQ DLQ/retry integration tests. |
| CEPS-11 | Broker startup and established-session loss reconnect without losing unacknowledged messages. | RabbitMQ Testcontainers reconnect/redelivery tests. |
| CEPS-12 | DB readiness is independent of broker health; consumer health is separately observable. | API health integration tests. |
| CEPS-13 | Event payload, note, credentials and connection strings are not logged/persisted. | Logging/configuration tests and persistence assertions. |
| CEPS-14 | Unit, MySQL 8.4 and RabbitMQ integration tests run in existing CI. | GitHub Actions restore, Release build, Compose and full test run. |
| CEPS-15 | Processor is transport-independent and lifecycle processing does not mutate Challenge business state. | Application boundary tests and code review. |

## Deferred
- Employee-facing Challenge authorization policy for historical reads and participation/task mutations (open question 19).
- Programmes, tasks, eligibility, participation, scoring, evidence and other Challenge APIs.
- SQS/EventBridge adapter and AWS deployment.
- Global JWT revocation, DLQ redrive tooling and operational dashboards.
