# Specification: Challenge Employee Status Projection Consumer

**Status:** Approved by owner on 2026-11-20. The implementation already exists on the live branch; this document is the authoritative behavior for reconciliation and acceptance.  
**Authority:** AGENTS.md, ADR-0001/0002, docs/architecture/service-ownership.md, docs/architecture/communication.md, docs/contracts/event-envelope.md and docs/contracts/employee-status-projection.md.

## Service boundary

1. Challenge is one of the existing six services. It owns programmes, tasks, eligibility, participation and authoritative challenge state.
2. Challenge owns a separate logical MySQL database named `teampulse_challenge` locally and separately provisioned production credentials.
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

## Processed-event inbox and retention

The Challenge-owned `processed_domain_events` inbox stores only:

- `eventId` (primary key / unique deduplication key)
- `eventType`
- `schemaVersion`
- `employeeId`
- `lifecycleVersion`
- `processedAtUtc`
- `fingerprintAlgorithm` (version identifier, initially `rfc8785-sha256-v1`)
- `contentFingerprint` (SHA-256 digest of the normalized recognized envelope)

Do not persist the original JSON, optional HR note, unknown additive properties or other employee profile information. Retain inbox rows without automatic expiry or cleanup initially. Do not add a TTL, scheduled cleanup job or archival process in this change. Revisit retention before production with privacy/legal review; this initial technical choice is not a legal retention sign-off.

## Supported messages and validation

Accept only the complete shared schema-v1 envelope directly in the RabbitMQ message body:

- `EmployeeDeactivated`: previousStatus `active`, newStatus `inactive`, resulting `isActive=false`.
- `EmployeeReactivated`: previousStatus `inactive`, newStatus `active`, resulting `isActive=true`.

Validate all recognized required fields before persistence:

- Envelope: `eventId` UUID, `eventType` from the two supported values, `schemaVersion=1`, non-default UTC `occurredAtUtc`, and UUID `correlationId`.
- Payload: UUID `employeeId` and `actorEmployeeId`; exact case-sensitive `source=hr-admin-api`; valid and internally consistent `previousStatus`/`newStatus`; positive 64-bit `lifecycleVersion`; and non-empty, non-whitespace bounded `reasonCode`.
- Optional `note` may be accepted as a string when present, but must never be persisted, included in the fingerprint, or logged.
- Unknown additional JSON properties are tolerated and ignored. They do not change the fingerprint.
- Reject malformed JSON, missing or invalid recognized required fields, unsupported event/schema versions, invalid producer values and inconsistent transitions before invoking the persistence store.
- Enforce documented maximum lengths for bounded string fields. Do not silently truncate values.

RabbitMQ does not carry an EventBridge outer wrapper. A future SQS adapter extracts the envelope from EventBridge `detail` and calls the same application processor; it is out of scope.

## Fingerprint and duplicate identity

1. Build a typed normalized representation from the recognized schema-v1 envelope and payload fields: `eventId`, `eventType`, `schemaVersion`, `occurredAtUtc`, `correlationId`, `employeeId`, `actorEmployeeId`, `source`, `previousStatus`, `newStatus`, `lifecycleVersion` and `reasonCode`.
2. Exclude optional `note` and every unknown additive JSON property. JSON whitespace and input property order are not part of event identity.
3. Normalize UUIDs to lowercase D form, `occurredAtUtc` to fixed UTC ISO-8601 form `yyyy-MM-dd'T'HH:mm:ss.fffffff'Z'`, and enum/source values to exact contract values. Represent the 64-bit `lifecycleVersion` as an invariant base-10 string in the fingerprint object so RFC 8785's IEEE-754 number model cannot round large integer values.
4. Serialize the normalized fingerprint object using RFC 8785 JSON Canonicalization Scheme (JCS), then calculate SHA-256. Store the lowercase hexadecimal digest and algorithm identifier `rfc8785-sha256-v1`; never store the original payload.
5. When an `eventId` already exists:
   - Same algorithm and fingerprint: return `Duplicate`; do not mutate inbox or projection.
   - Different fingerprint: return `DuplicateEventIntegrityViolation`; do not overwrite the original inbox row or apply the conflicting payload. Route the delivery to the lifecycle DLQ.
6. A duplicate-key race must be resolved by reading the committed inbox fingerprint after rolling back the losing insert transaction; do not classify every unique-key conflict as a harmless duplicate without comparing fingerprints.
7. Because `note` and unknown properties are excluded, differences in those fields alone do not constitute event-ID reuse. Differences in any included recognized field do.

## Idempotency, ordering and transaction

1. Assume at-least-once delivery.
2. For a new event ID, insert the inbox metadata and evaluate the projection in one MySQL transaction.
3. Insert the projection when absent. Update only when incoming `lifecycleVersion` is strictly greater than the stored version.
4. A valid new event ID with equal or lower version is recorded as processed but does not change the projection; classify it as `Stale`.
5. A same-ID/same-fingerprint redelivery is `Duplicate`; it must not create another inbox row or change the projection.
6. Inbox and projection decision commit atomically; any failure rolls back both.
7. Concurrent events for one employee must not regress lifecycleVersion. Use database-enforced atomic conditional upsert or equivalent transactional locking; do not rely on process-local locks.
8. Set `updatedAtUtc` from Challenge's UTC processing clock only when applying a newer version.
9. Database uniqueness on `eventId` and the conditional upsert must protect correctness across multiple Challenge replicas.

## Challenge domain behavior boundary

This change materializes the shared lifecycle projection only. It does not create, cancel, suspend, complete or otherwise mutate programmes, tasks, eligibility, participation, completion records, scores or evidence when employee status changes. It adds no Challenge business endpoints.

When employee-facing Challenge resource operations are introduced, they must consult this local projection at the Challenge authorization boundary in addition to validated JWT claims and normal tenant/resource authorization. The policy for historical participation reads versus new participation/task mutations by inactive employees remains open question 19; do not infer or implement it in this projection-only increment. No global immediate token-revocation claim is permitted.

## RabbitMQ consumer, acknowledgement and failure handling

1. Consume only from `teampulse.challenge.events`; use Challenge-owned topology and do not modify Habit or other service queues.
2. Use RabbitMQ.Client 7.x async APIs, manual acknowledgements and prefetch 1 for one active handler per channel.
3. Acknowledge `Applied`, `Stale` and matching `Duplicate` outcomes only after the MySQL transaction has committed (or the existing inbox fingerprint has been safely compared).
4. For malformed/unsupported events and `DuplicateEventIntegrityViolation`, dead-letter the delivery with a distinct failure classification. The original delivery may be acknowledged only after successful dead-letter handling is confirmed. If dead-letter handling fails, leave the original eligible for redelivery.
5. Reuse the Challenge lifecycle DLQ for invalid events and integrity violations. At minimum distinguish `InvalidEvent`, `UnsupportedSchemaVersion`, `InvalidProducer` and `DuplicateEventIntegrityViolation` in safe diagnostic metadata.
6. Keep the Challenge source queue and DLQ durable quorum queues with at-least-once dead-lettering and reject-publish overflow behavior so a DLX publishing failure does not silently lose the original delivery.
7. Retry known transient MySQL/network failures using bounded exponential backoff with full jitter. Maximum attempts, base delay and maximum delay are configurable. After exhaustion, publish to the same lifecycle DLQ with classification `TransientRetryExhausted`.
8. Do not retry arbitrary programming/configuration errors indefinitely or classify them as transient. Publish non-transient processing exceptions to the same lifecycle DLQ with classification `NonTransientProcessingFailure`. For both terminal paths, acknowledge the original delivery only after mandatory routing and positive publisher confirmation; otherwise close the source channel without acknowledgement.
9. Reconnect with bounded delay and cancellation-aware shutdown. Broker availability must not make Challenge database readiness misleading.
10. Consumer is disabled by default and enabled explicitly for local development. No AWS credentials are required.

## Host, persistence and health

- Add/retain Challenge.API, Challenge.Application, Challenge.Domain and Challenge.Infrastructure with inward-only references; add/retain Challenge.UnitTests in the solution.
- Configure a Challenge-only connection string and local `teampulse_challenge` database initialization/grant.
- Apply checked-in EF migrations at host startup before hosted consumers.
- `/health/live` reports process liveness only.
- `/health/ready` checks Challenge MySQL only; RabbitMQ availability must not make API readiness fail.
- `/health/consumer` independently reports RabbitMQ consumer state, including disconnected/reconnecting/healthy states.
- Health responses and logs must not expose event bodies, optional notes, credentials, connection strings or sensitive payload data.

## Acceptance criteria and test mapping

| ID | Acceptance criterion | Verification |
|---|---|---|
| CEPS-01 | Challenge projects obey Clean Architecture references and remain within the six-service architecture. | Release build and project-reference review. |
| CEPS-02 | Challenge owns its MySQL DB and has no Identity/Habit references or cross-service FKs. | Connection/model and dependency-boundary tests. |
| CEPS-03 | Checked-in EF migration creates projection/inbox schema; startup applies it on clean and repeated startup. | MySQL 8.4 Testcontainers host-startup tests. |
| CEPS-04 | Projection and inbox schemas contain only approved fields and minimal metadata, including fingerprint/version. | EF model and migration assertions. |
| CEPS-05 | Schema-v1 validation is strict for recognized required fields, tolerates unknown additive properties and enforces exact `hr-admin-api` source. | Processor tests for valid, malformed, unknown-property, invalid actor/source and unsupported-schema cases. |
| CEPS-06 | Fingerprint uses normalized recognized fields, RFC 8785 JCS and SHA-256; note and unknown fields are excluded. | Canonicalization vectors and fingerprint tests, including int64 boundaries and timestamp normalization. |
| CEPS-07 | Same event ID and fingerprint is Duplicate; same event ID with a different fingerprint is an integrity violation and is not applied. | Unit and MySQL integration tests. |
| CEPS-08 | New event ID records inbox and projection decision atomically; failures roll back both. | MySQL transaction/rollback integration tests. |
| CEPS-09 | Equal/lower lifecycle versions are Stale; strictly newer versions apply. | MySQL integration tests. |
| CEPS-10 | Concurrent N/N+1 processing leaves the maximum lifecycle version across replicas. | Concurrent MySQL Testcontainers test. |
| CEPS-11 | Manual ack occurs only after commit; crash-before-ack redelivery is deduplicated. | RabbitMQ Testcontainers commit/replay test. |
| CEPS-12 | Invalid and conflicting duplicate deliveries reach the shared Challenge DLQ with distinct classifications; ack follows confirmed dead-letter handling. | RabbitMQ DLQ integration tests including DLX failure. |
| CEPS-13 | Transient failures use bounded exponential full-jitter retry and exhaustion reaches the DLQ. | Deterministic retry-policy unit tests and RabbitMQ integration tests. |
| CEPS-14 | Broker startup and established-session loss reconnect without losing unacknowledged messages. | RabbitMQ Testcontainers reconnect/redelivery tests. |
| CEPS-15 | DB readiness is independent of broker health; consumer health is separately observable. | API health integration tests. |
| CEPS-16 | Inbox has no automatic expiry/cleanup; payload, note, credentials and connection strings are not persisted/logged. | Persistence, migration and logging assertions. |
| CEPS-17 | Processor is transport-independent and lifecycle processing does not mutate Challenge business state. | Application boundary tests and code review. |
| CEPS-18 | Unit, MySQL 8.4 and RabbitMQ integration tests run in existing CI. | GitHub Actions restore, Release build, Compose and full test run. |

## Deferred

- Employee-facing Challenge authorization policy for historical reads and participation/task mutations (open question 19).
- Programmes, tasks, eligibility, participation, scoring, evidence and other Challenge APIs.
- SQS/EventBridge adapter and AWS deployment.
- Inbox cleanup/archival and legal retention sign-off.
- DLQ redrive tooling and operational dashboards.
