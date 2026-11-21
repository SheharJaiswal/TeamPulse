# Specification: Insight Employee Status Projection Consumer

**Status:** Draft — awaiting owner approval.
**Authority:** AGENTS.md, ADR-0001, service ownership, communication conventions, event-envelope and employee-status-projection contracts.

## Service and persistence boundary
1. Insight is one of the six services and owns aggregated/derived read models only.
2. Insight owns its DynamoDB table. It must not read or reference another service's database or persistence project.
3. Employee IDs are stable external Identity identifiers; no cross-service foreign keys.
4. Use ASP.NET Core .NET 8 and Clean Architecture (API, Application, Domain, Infrastructure).
5. Use AWS SDK for .NET DynamoDB APIs through Insight-owned interfaces. Do not use EF Core, Pomelo or MySQL for Insight lifecycle projection/inbox persistence.
6. The repository currently has no Insight service projects. This increment creates only the lifecycle projection foundation; it adds no analytics/business endpoints.

## Projection and inbox data
Store exactly the shared Employee Status Projection Contract v1 fields:

| Field | Type | Constraint |
|---|---|---|
| employeeId | UUID | Stable external Identity identifier |
| isActive | boolean | Latest accepted lifecycle state |
| lifecycleVersion | signed 64-bit integer | Incoming value positive |
| updatedAtUtc | UTC timestamp | Insight processing time when a newer version is applied |

The inbox stores only eventId, eventType, schemaVersion, employeeId, lifecycleVersion and processedAtUtc. Do not persist raw JSON, optional note, reasonCode, actor identity, employee profile, organization/team/role, credentials, JWTs, activity, wellness data or source-service records. Do not add a content fingerprint unless a separately approved contract change requires it.

## DynamoDB single-table model
Use one Insight-owned table, configured by name and endpoint. Proposed local default table name: teampulse-insight-lifecycle. String partition key PK and string sort key SK; on-demand billing is proposed.

| Item | PK | SK | Attributes |
|---|---|---|---|
| Employee status projection | EMPLOYEE#<employeeId> | STATUS | employeeId, isActive, lifecycleVersion (DynamoDB Number), updatedAtUtc |
| Processed-event inbox | EVENT#<eventId> | INBOX | eventId, eventType, schemaVersion, employeeId, lifecycleVersion, processedAtUtc |

No GSI is required. Do not enable TTL or automatic deletion. Production encryption, point-in-time recovery, backup, deletion protection, capacity and IAM are later infrastructure decisions; do not create or deploy AWS resources in this change.

## Supported events and validation
Accept only schema-version-1 EmployeeDeactivated and EmployeeReactivated envelopes.

Validate all recognized required fields before persistence:
- Envelope: eventId UUID, supported eventType, schemaVersion=1, non-default UTC occurredAtUtc and UUID correlationId.
- Payload: UUID employeeId and actorEmployeeId; exact case-sensitive source=hr-admin-api; valid previousStatus/newStatus; positive signed 64-bit lifecycleVersion; trimmed non-empty reasonCode within Identity's 1–100 character bound.
- EmployeeDeactivated requires active -> inactive and results in isActive=false.
- EmployeeReactivated requires inactive -> active and results in isActive=true.
- Optional note may be accepted as a string but must never be persisted or logged.
- Unknown additive JSON properties are tolerated and ignored.
- Reject malformed JSON, missing/invalid recognized fields, unsupported schema/event types and inconsistent transitions before invoking persistence.
- Never silently truncate recognized fields.

## Idempotency, ordering and atomicity
Assume at-least-once delivery. Deduplicate by eventId and apply only strictly newer lifecycle versions. The inbox write and projection decision must be one DynamoDB TransactWriteItems transaction.

The store uses strongly consistent reads and optimistic conditional writes:
1. Strongly read the employee projection and event inbox item.
2. If the inbox item exists, return Duplicate without changing either item.
3. If no projection exists, transact a conditional Put of the inbox item (attribute_not_exists(PK)) and a projection Update/upsert conditioned on the projection item still being absent.
4. If the observed projection version is lower than the incoming version, transact the conditional inbox Put and a projection Update conditioned on lifecycleVersion equaling the version observed by the strongly consistent read. Write incoming status/version/time.
5. If the observed projection version is equal to or greater than incoming, transact the conditional inbox Put and a ConditionCheck requiring the projection lifecycleVersion to remain greater than or equal to incoming. Record the stale event without mutating the projection.
6. If a transaction is cancelled, strongly read the inbox again. If the event item now exists, return Duplicate. Otherwise re-read the projection and retry the appropriate branch within a bounded retry budget. Do not misclassify a projection race as a duplicate.
7. Return Applied only after the newer projection and inbox commit together. Return Stale only after the inbox commits while the projection remains equal/newer. A same-event redelivery is Duplicate.
8. Set updatedAtUtc from an injected UTC TimeProvider only when applying a newer version; set processedAtUtc when recording a new inbox item.
9. Propagate non-conflict persistence errors so the transport adapter can apply bounded retry or terminal handling.

The conditional version fence must preserve the maximum lifecycleVersion across multiple Insight replicas. Do not use process-local locks or a non-transactional inbox/projection sequence.

## RabbitMQ consumer and failure handling
- Consume the direct schema-v1 envelope from teampulse.insight.events; do not expect an EventBridge wrapper on RabbitMQ.
- Use RabbitMQ.Client 7.x asynchronous APIs, manual acknowledgement and prefetch 1 initially.
- Acknowledge Applied, Stale and Duplicate only after the DynamoDB transaction commits or the existing inbox item is safely observed.
- Poison/unsupported messages and exhausted transient failures go to an Insight-owned durable DLQ with distinct failure-classification metadata.
- Retry only known transient DynamoDB/network errors with bounded exponential backoff and jitter. Do not retry programming/configuration errors indefinitely.
- If mandatory DLQ routing or publisher confirmation fails, do not acknowledge the source delivery; close the source channel/session for redelivery.
- Reconnect with bounded delay and cancellation-aware shutdown.
- Consumer is disabled by default and explicitly enabled for local development.
- RabbitMQ availability must not make Insight persistence readiness misleading.

## Local DynamoDB and host behavior
- Use DynamoDB Local for local development and integration tests; local configuration supplies an endpoint and development-only dummy credentials, not AWS credentials.
- Provide an explicit local table-initialization option for Development/test only. Production startup must not create or mutate cloud tables; production table provisioning is deferred.
- /health/live reports process liveness.
- /health/ready checks the configured Insight DynamoDB table only.
- /health/consumer independently reports RabbitMQ consumer state.
- A future SQS adapter extracts the shared envelope from EventBridge detail and calls the same transport-independent processor.

## Business boundary
This increment stores only lifecycle projection and processed-event metadata. It does not implement aggregation pipelines, dashboards, cohort metrics, Insight read APIs, employee analytics or recomputation/deletion of previously derived metrics. Any policy for excluding inactive employees from future aggregate calculations or handling historical aggregates requires a separate approved SDD.

## Acceptance criteria
| ID | Acceptance criterion | Verification |
|---|---|---|
| IESP-01 | Insight is added as one of six services with Clean Architecture boundaries and no cross-service project references. | Release build and project-reference boundary tests |
| IESP-02 | Insight uses DynamoDB only for this projection/inbox; no MySQL/EF Core persistence. | Dependency/configuration tests and code review |
| IESP-03 | Projection and inbox items contain only approved minimal fields and key structure. | DynamoDB Local item/schema assertions |
| IESP-04 | Only valid schema-v1 events are accepted; unknown additive fields are ignored and note is excluded. | Processor validation tests |
| IESP-05 | Inbox and projection decisions are atomic using DynamoDB transactions. | DynamoDB Local transaction/rollback tests |
| IESP-06 | Duplicate event IDs do not mutate state. | DynamoDB Local duplicate-delivery tests |
| IESP-07 | Stale/equal versions are recorded without projection regression; newer versions apply. | DynamoDB Local ordering tests |
| IESP-08 | Concurrent deliveries preserve maximum lifecycleVersion across workers. | Concurrent DynamoDB Local integration test |
| IESP-09 | Manual ack follows committed persistence; replay after commit-before-ack is idempotent. | RabbitMQ + DynamoDB Local integration test |
| IESP-10 | Poison messages reach Insight DLQ; unavailable DLQ routing leaves source unacknowledged. | RabbitMQ Testcontainers DLQ/failure tests |
| IESP-11 | Bounded retry, reconnect and database/consumer health isolation work. | Retry and RabbitMQ/API integration tests |
| IESP-12 | No raw payload, note, credentials or profile data is persisted/logged. | DynamoDB item and log privacy assertions |
| IESP-13 | Processor is transport-independent and has no analytics/business side effects. | Application tests and code review |
| IESP-14 | Unit, DynamoDB Local, RabbitMQ and topology tests run in CI. | GitHub Actions workflow |

## Deferred
- SQS/EventBridge adapter, AWS provisioning, IAM, encryption/backup policy and cloud deployment.
- Insight aggregation, dashboards, cohort metrics and read APIs.
- Policy for future aggregate inclusion/exclusion and treatment of historical aggregates for inactive employees.
- Inbox retention/cleanup and privacy/legal sign-off.
- DLQ redrive tooling and production observability.
