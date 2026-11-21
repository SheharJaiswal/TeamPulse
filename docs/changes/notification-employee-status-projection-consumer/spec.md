# Specification: Notification Employee Status Projection Consumer

**Status:** Owner-approved on 2026-11-21.  
**Authority:** `AGENTS.md`, service ownership, communication conventions, ADR-0001, event-envelope and employee-status-projection contracts.

## Service and persistence boundary

1. Notification remains one of the six services and owns notification preferences, notification records and delivery history.
2. Notification owns a separate logical MySQL database named `teampulse_notification` locally, with separately provisioned production credentials.
3. No reads/references to Identity, Habit, Challenge or Event Tracking databases; employee IDs are stable external identifiers and no cross-service foreign keys are allowed.
4. Use ASP.NET Core .NET 8, EF Core 8, Pomelo.EntityFrameworkCore.MySql and MySQL 8.4.
5. Commit EF migrations and apply them at host startup before hosted consumers or API traffic.
6. The shared local MySQL container is development-only.

## Projection and inbox

Store exactly the shared Employee Status Projection Contract v1 fields:

| Field | Type | Constraint |
|---|---|---|
| employeeId | UUID | Primary key |
| isActive | boolean | Latest accepted lifecycle state |
| lifecycleVersion | signed 64-bit integer | Incoming value positive |
| updatedAtUtc | UTC timestamp | Notification processing time when a newer version is applied |

The processed-event inbox stores only the minimum metadata needed for eventId deduplication and stale ordering: eventId, eventType, schemaVersion, employeeId, lifecycleVersion and processedAtUtc.

Do not persist raw payloads, optional note, reasonCode, actor identity, employee profile, credentials, notification preferences, notification records, delivery history, activity or wellness data in the projection/inbox.

## Supported events and validation

Accept only schema-version-1 `EmployeeDeactivated` and `EmployeeReactivated` envelopes.

Validate eventId and correlationId UUIDs, non-default UTC occurredAtUtc, supported schemaVersion/eventType, employeeId and actorEmployeeId UUIDs, exact case-sensitive `source=hr-admin-api`, trimmed non-empty reasonCode within the Identity contract's 1–100 character bound, positive lifecycleVersion and transition consistency before persistence.

| eventType | previousStatus | newStatus | isActive |
|---|---|---|---|
| EmployeeDeactivated | active | inactive | false |
| EmployeeReactivated | inactive | active | true |

Reject malformed JSON, missing/invalid recognized fields, unsupported versions and inconsistent transitions without mutation. Ignore unknown additive properties. Never persist or log optional note.

## Idempotency, ordering and atomicity

1. Assume at-least-once delivery and deduplicate by eventId.
2. Insert the inbox marker and apply the projection decision in one Notification MySQL transaction.
3. Insert a projection if absent; update only when incoming lifecycleVersion is strictly greater.
4. A new eventId with equal/lower version is recorded as processed but leaves the projection unchanged (Stale).
5. A repeated eventId is Duplicate and must not mutate state.
6. Database uniqueness and conditional upsert/transactional locking must preserve the maximum lifecycle version across concurrent deliveries; do not rely on process-local locks.
7. Set updatedAtUtc from Notification's UTC processing clock only when applying a newer version.
8. Roll back inbox and projection together on failure.

## Consumer and health

- Consume only `teampulse.notification.events`; direct schema-v1 envelope is the RabbitMQ body.
- Manual acknowledgement; prefetch 1 and one in-flight handler per channel initially.
- Ack Applied, Stale and Duplicate only after commit or safe duplicate determination.
- Poison/unsupported events go to a Notification-owned durable DLQ.
- Retry only known transient database/network failures with bounded configurable backoff; exhausted failures go to the DLQ.
- If mandatory DLQ routing/publisher confirmation fails, do not acknowledge the source delivery.
- Reconnect with bounded delay and cancellation-aware shutdown.
- `/health/live` is process liveness; `/health/ready` checks Notification MySQL only; `/health/consumer` reports RabbitMQ state independently.
- Never log raw bodies, notes, credentials, tokens or connection strings.

## Business boundary

This projection is only a local lifecycle authorization signal for future Notification-owned employee resources. This increment does not send notifications, alter preferences, create notification records, or change delivery history in response to lifecycle events. Any future Notification API must enforce tenant/resource authorization and the local inactive-state policy; that business behavior requires its own approved SDD.

## Acceptance criteria

| ID | Acceptance criterion | Verification |
|---|---|---|
| NESP-01 | Clean Architecture and six-service ownership are preserved. | Release build and project-reference review |
| NESP-02 | Notification owns its MySQL DB; no cross-service DB references/FKs. | Connection/model/dependency tests |
| NESP-03 | Checked-in migration applies on clean/repeated host startup. | MySQL 8.4 Testcontainers |
| NESP-04 | Only valid schema-v1 lifecycle events are accepted. | Envelope validation tests |
| NESP-05 | Projection/inbox contain only approved minimal fields. | EF model/migration assertions |
| NESP-06 | Inbox and projection decisions are atomic. | MySQL rollback tests |
| NESP-07 | Duplicate, stale and equal versions are safe. | Unit/MySQL tests |
| NESP-08 | Concurrent events preserve maximum lifecycle version. | Concurrent MySQL Testcontainers |
| NESP-09 | Manual ack follows commit; redelivery is idempotent. | RabbitMQ replay test |
| NESP-10 | Poison/exhausted deliveries reach DLQ; failed routing leaves source unacked. | RabbitMQ DLQ tests |
| NESP-11 | Reconnect and database/consumer health isolation work. | RabbitMQ/API integration tests |
| NESP-12 | No raw payload/note/secrets are persisted or logged. | Persistence/log privacy assertions |
| NESP-13 | Processor is transport-independent and does not mutate notification business records. | Application tests/code review |
| NESP-14 | Unit, MySQL and RabbitMQ tests run in CI. | GitHub Actions workflow |

## Deferred

- Notification delivery, preference and lifecycle-triggered notification behavior.
- Employee-facing APIs and inactive-employee access policy.
- SQS/EventBridge adapter and AWS provisioning.
- Inbox retention/cleanup and global token revocation.
