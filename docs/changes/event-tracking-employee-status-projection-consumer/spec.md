# Specification: Event Tracking Employee Status Projection Consumer

**Status:** Draft — awaiting owner approval.  
**Authority:** AGENTS.md, docs/architecture/service-ownership.md, docs/architecture/communication.md, docs/contracts/event-envelope.md and docs/contracts/employee-status-projection.md.

## Service and persistence boundary

1. Event Tracking is one of the existing six services and owns submitted activity records and audit history.
2. Event Tracking owns a separate logical MySQL database named `teampulse_event_tracking` locally and separately provisioned production credentials.
3. Event Tracking must not query or reference Identity, Habit or Challenge databases. Employee IDs are stable external identifiers; no cross-service foreign keys.
4. Use ASP.NET Core .NET 8, EF Core 8, Pomelo.EntityFrameworkCore.MySql and MySQL 8.4, consistent with ADR-0001.
5. All schema changes use checked-in EF migrations. Apply migrations at host startup before the consumer starts or the API accepts traffic.
6. The shared local MySQL container may host this logical database for development only.

## Projection contract

Store exactly the Employee Status Projection Contract v1 fields:

| Field | Type | Constraint |
|---|---|---|
| employeeId | UUID | Primary key; stable external Identity identifier |
| isActive | boolean | Latest accepted lifecycle state |
| lifecycleVersion | signed 64-bit integer | Non-negative stored value; incoming value positive |
| updatedAtUtc | UTC timestamp | Event Tracking processing time when a newer version is applied |

Do not copy employee profile, organization/team, role, credential, JWT, password, reasonCode, note, activity submission, evidence or wellness data into this projection.

## Supported lifecycle events

Accept only schema-version-1 `EmployeeDeactivated` and `EmployeeReactivated` envelopes using the shared event envelope.

Validate eventId and correlationId UUIDs, non-default UTC occurredAtUtc, schemaVersion, eventType, employeeId and actorEmployeeId UUIDs, non-empty source/reasonCode, positive lifecycleVersion, and transition consistency before persistence:

| eventType | previousStatus | newStatus | isActive |
|---|---|---|---|
| EmployeeDeactivated | active | inactive | false |
| EmployeeReactivated | inactive | active | true |

Reject malformed JSON, missing/invalid recognized fields, unsupported event/schema versions and inconsistent transitions without mutating the database. Optional note must never be persisted or logged. Unknown additive fields may be ignored in accordance with the shared envelope contract.

## Idempotency, ordering and transaction

1. Assume at-least-once delivery and deduplicate by eventId in the Event Tracking-owned inbox.
2. For a new eventId, insert the minimal inbox marker and apply the projection decision in one MySQL transaction.
3. Insert the projection if absent. Update only when incoming lifecycleVersion is strictly greater than the stored version.
4. A valid new eventId with an equal or lower version is recorded as processed but leaves the projection unchanged (Stale).
5. A repeated eventId is Duplicate and must not mutate the projection or inbox.
6. Inbox and projection decision commit atomically; failures roll back both.
7. Use a database-enforced conditional upsert or equivalent transactional locking; do not rely on process-local locks.
8. Set updatedAtUtc from Event Tracking's UTC processing clock only when applying a newer version.

## Consumer and acknowledgement

1. Consume only from the existing `teampulse.event-tracking.events` queue, using manual acknowledgements.
2. Use bounded prefetch and one in-flight handler per channel initially.
3. Acknowledge Applied, Stale and Duplicate only after the database transaction commits or the duplicate is safely established.
4. Poison/unsupported events are dead-lettered to an Event Tracking-owned durable DLQ.
5. Retry known transient database/network failures with bounded configurable backoff; exhausted failures go to the DLQ.
6. If dead-letter routing cannot be confirmed, do not acknowledge the source delivery.
7. Reconnect with bounded backoff and cancellation-aware shutdown. RabbitMQ availability must not make MySQL readiness misleading.
8. Never log raw event bodies, optional notes, credentials, tokens or connection strings.

## Host, health and authorization boundary

- Add API, Application, Domain, Infrastructure and UnitTests projects with inward-only references.
- Apply checked-in EF migrations before hosted consumers start.
- `/health/live` reports process liveness; `/health/ready` checks Event Tracking MySQL only; consumer health is independently observable.
- The projection is a local authorization signal for future employee-owned activity resources. Any future resource endpoint must check it alongside validated JWT claims, tenant isolation and normal resource authorization.
- This increment adds no activity submission/evidence endpoints and does not define how existing activity/audit records are read or retained after deactivation.

## Acceptance criteria

| ID | Acceptance criterion | Verification |
|---|---|---|
| ETESP-01 | Event Tracking projects obey Clean Architecture and six-service ownership. | Release build and project-reference review. |
| ETESP-02 | Event Tracking owns its MySQL DB with no cross-service DB references/FKs. | Connection/model and dependency-boundary tests. |
| ETESP-03 | Startup applies checked-in migrations on a clean database. | MySQL 8.4 Testcontainers host-startup test. |
| ETESP-04 | Only supported schema-v1 lifecycle events are accepted. | Envelope validation tests. |
| ETESP-05 | Projection stores only the four contract fields. | EF model/migration assertions. |
| ETESP-06 | Inbox and projection decision commit atomically. | MySQL rollback tests. |
| ETESP-07 | Duplicate events are harmless and stale/equal versions cannot regress state. | Unit/MySQL tests. |
| ETESP-08 | Concurrent lifecycle events preserve the maximum version. | Concurrent MySQL Testcontainers test. |
| ETESP-09 | Manual ack follows commit; redelivery is idempotent. | RabbitMQ Testcontainers replay test. |
| ETESP-10 | Poison and exhausted transient deliveries reach the service DLQ; failed routing leaves source unacknowledged. | RabbitMQ DLQ integration tests. |
| ETESP-11 | Broker reconnect and health isolation behave as specified. | RabbitMQ and API health integration tests. |
| ETESP-12 | No payload/note/secrets are persisted or logged. | Persistence/logging assertions. |
| ETESP-13 | Processor is transport-independent and does not mutate activity/audit business records. | Application tests and code review. |
| ETESP-14 | Unit, MySQL and RabbitMQ tests run in CI. | GitHub Actions full workflow. |

## Deferred

- Activity submission/evidence APIs and post-deactivation access policy.
- SQS/EventBridge production adapter and AWS deployment.
- Insight and Notification consumers.
- Retention/deletion policy changes and global token revocation.
