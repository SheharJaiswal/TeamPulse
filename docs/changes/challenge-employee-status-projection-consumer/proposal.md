# Proposal: Challenge Employee Status Projection Consumer

**Status:** Revised — awaiting explicit owner approval of this SDD package.

## Context

Identity owns authoritative employee lifecycle state and publishes schema-v1 `EmployeeDeactivated` and `EmployeeReactivated` events through its transactional outbox. The shared Employee Status Projection Contract v1 requires each receiving service to maintain a minimal, version-aware local projection and deduplicate at-least-once delivery. Habit is complete.

Challenge is the next receiving service in the six-service architecture. The live `feature/backend-foundation` branch already contains the Challenge API/Application/Domain/Infrastructure projects, MySQL projection and inbox, migration, transport-independent processor, RabbitMQ consumer, health endpoints, Challenge-owned queue/DLX/DLQ and tests. This implementation predates completion of this SDD review. Its presence and passing CI are repository facts, not evidence that this proposal was approved.

The original proposal contained outdated statements that no Challenge service project existed. This revision corrects that context and records the owner's decisions from the SDD review.

## Goal

Establish an accurate, approved specification for Challenge's minimal employee-status projection and local lifecycle-event consumer; reconcile the existing implementation against it; and close the remaining acceptance-test gaps without introducing Challenge product behavior.

## Scope

- Maintain only `employeeId`, `isActive`, `lifecycleVersion` and `updatedAtUtc` in the Challenge employee-status projection.
- Retain minimal processed-event inbox metadata, including a versioned SHA-256 content fingerprint; do not retain original event payloads.
- Validate the recognized schema-v1 envelope strictly while tolerating unknown additive JSON properties.
- Accept only the exact producer source `hr-admin-api`.
- Detect duplicate event-ID reuse with different recognized business content using RFC 8785 JSON Canonicalization Scheme (JCS) followed by SHA-256.
- Process inbox insertion and conditional projection upsert atomically in Challenge-owned MySQL; only strictly newer lifecycle versions may update the projection.
- Consume the direct schema-v1 envelope from the local RabbitMQ Challenge queue, acknowledge only after committed processing or confirmed dead-letter handling, and use bounded exponential retry with full jitter for transient failures.
- Reuse the Challenge lifecycle DLQ for invalid events and integrity violations, with distinct failure classifications.
- Expose independent liveness, MySQL readiness and RabbitMQ consumer-health endpoints.
- Keep inbox records without automatic expiry or cleanup initially; revisit retention before production following privacy/legal review.
- Preserve a transport-independent processor for a future SQS adapter. EventBridge/SQS provisioning remains deferred until owner cloud setup.
- Reconcile the existing code and tests against the revised acceptance criteria and record gaps honestly.

## Non-goals

- Programmes, tasks, eligibility, participation, scoring, evidence or Challenge workflows.
- Changing programme, task, eligibility, participation, completion, score or evidence state when an employee is deactivated/reactivated.
- Defining inactive-employee historical-read or participation-mutation policy beyond the shared projection contract.
- Cross-service database access, foreign keys or Identity/Habit project references.
- EventBridge/SQS implementation, AWS provisioning or CloudWatch setup in this increment.
- Global immediate JWT revocation claims.
- Inbox expiry, cleanup, archival or deletion before a separately approved retention policy.

## Owner decisions incorporated

1. Minimal status projection only; Identity remains authoritative for lifecycle history.
2. Retain minimal inbox metadata permanently initially; no TTL or cleanup job.
3. Tolerate unknown additive JSON properties while validating recognized required fields and schema version.
4. Require exact, case-sensitive source `hr-admin-api`.
5. Dead-letter permanently invalid events; acknowledge the original only after successful dead-letter handling.
6. Separate `/health/live`, `/health/ready` (MySQL only) and `/health/consumer` (RabbitMQ).
7. Apply only strictly newer lifecycle versions; equal and lower versions are stale.
8. Use database-enforced atomic inbox insertion and conditional projection upsert.
9. Treat an existing event ID with different recognized content as an integrity violation and quarantine it.
10. Compare duplicate content using a SHA-256 fingerprint.
11. Fingerprint recognized business-envelope fields; exclude optional `note` and unknown additive properties.
12. Canonicalize with RFC 8785 JCS before SHA-256.
13. Reuse the lifecycle DLQ with distinct failure classifications.
14. Use bounded exponential retry with full jitter for transient failures; exhausted retries go to the lifecycle DLQ.

Follow AGENTS.md, ADR-0001/0002, service ownership, event envelope and Employee Status Projection Contract v1. Challenge owns programmes, tasks, eligibility, participation and authoritative challenge state. RabbitMQ is local development transport; EventBridge remains intended production fan-out.

The revised package is not yet marked approved. Further Challenge feature implementation must wait for explicit owner approval of this revised SDD. Existing implementation may be tested and reconciled against the approved package once that approval is recorded.
