# Tasks: Challenge Employee Status Projection Consumer

## SDD and contract review

- [x] Fetch the live `feature/backend-foundation` HEAD and reconcile the existing Challenge implementation and CI evidence.
- [x] Read AGENTS.md, README.md, project status, open questions, service ownership, communication conventions, ADR-0001/0002 and shared event/projection contracts.
- [x] Correct the proposal/design context: Challenge projects, persistence, consumer, topology and tests already exist on the live branch.
- [x] Preserve projection-only scope; do not invent Challenge programme/participation rules.
- [x] Keep inactive-employee historical-read versus mutation policy as open question 19.
- [x] Map acceptance criteria to implementation and verification.
- [x] Incorporate the 14 owner-selected decisions recorded in the SDD review (minimal projection; initial permanent inbox retention; tolerant additive JSON; exact source; invalid-event DLQ; separate health; strict version ordering; DB atomicity; conflicting duplicate integrity detection; SHA-256; fingerprint field exclusions; RFC 8785 JCS; shared DLQ classifications; bounded exponential full-jitter retry).
- [x] Owner explicitly approved this revised proposal/spec/design/tasks package on 2026-11-20; implementation reconciliation is authorized.

## Existing implementation already present on the live branch

- [x] Challenge.API, Challenge.Application, Challenge.Domain and Challenge.Infrastructure with inward-only references.
- [x] Challenge.UnitTests registered in backend/TeamPulse.sln.
- [x] Challenge-owned MySQL database initialization/grant and connection configuration.
- [x] Minimal employee status projection, inbox entity and transport-independent application processor.
- [x] Atomic inbox + conditional lifecycle-version upsert in Challenge-owned MySQL.
- [x] Checked-in EF Core/Pomelo migration and model snapshot; startup migration.
- [x] Separate liveness/readiness and consumer-health endpoints.
- [x] Challenge RabbitMQ consumer with manual ack after processor completion, prefetch 1, bounded transient retry and reconnect.
- [x] Challenge-owned quorum queue, DLX and DLQ without changing Habit or other service queues.
- [x] Consumer disabled by default and configuration validated when enabled.
- [x] Startup-reconnect test and local README/communication documentation; CI #105 passed.

## Reconcile implementation against approved SDD

- [x] Validate actorEmployeeId UUID, exact case-sensitive `hr-admin-api` source, tolerant unknown properties and all required schema-v1 fields.
- [x] Add normalized RFC 8785 JCS + SHA-256 fingerprint and algorithm identifier to inbox persistence.
- [x] Compare fingerprints on duplicate event IDs; distinguish matching Duplicate from conflicting DuplicateEventIntegrityViolation without mutating the original inbox/projection.
- [x] Ensure fingerprint excludes optional note and unknown additive properties and includes the complete recognized normalized envelope fields.
- [x] Implement explicit classified publish-confirm handling for invalid events, unsupported schema versions, invalid producers and duplicate-event integrity violations; source acknowledgement follows successful mandatory publish confirmation.
- [ ] Verify permanent invalid/integrity events reach the shared lifecycle DLQ and original delivery is acknowledged only after confirmed dead-letter handling in RabbitMQ Testcontainers CI.
- [ ] Verify bounded exponential full-jitter transient retry, configurable limits and DLQ on exhaustion.
- [ ] Verify inbox has no TTL/automatic cleanup and stores no raw payload or note.
- [ ] Align health response states with the separate liveness, MySQL readiness and RabbitMQ consumer-health contract.

## Verification gaps

- [ ] MySQL 8.4 Testcontainers: clean and repeated host-startup migration, exact schema/model assertions and projection reader.
- [ ] MySQL 8.4 Testcontainers: same event ID/same fingerprint and same event ID/different fingerprint.
- [ ] MySQL 8.4 Testcontainers: duplicate-key race safely reads the committed fingerprint after rolling back the losing insert.
- [ ] MySQL 8.4 Testcontainers: concurrent N/N+1 processing leaves maximum lifecycle version; rollback leaves neither inbox nor projection mutation.
- [ ] RabbitMQ Testcontainers: duplicate redelivery after commit-before-ack and idempotent replay.
- [ ] RabbitMQ Testcontainers: invalid event and integrity violation DLQ handling, including DLX failure and redelivery.
- [ ] RabbitMQ Testcontainers: bounded exponential retry and exhaustion.
- [x] RabbitMQ Testcontainers: broker unavailable at startup and reconnect; startup-reconnect test passed in CI #105.
- [ ] RabbitMQ Testcontainers: established-session loss/reconnect and duplicate redelivery after commit-before-ack.
- [ ] Host health integration tests prove DB readiness is independent of RabbitMQ and consumer health is separately observable.
- [ ] Privacy/logging tests prove no event body, note, credentials or connection string is logged/persisted.
- [x] CI #103 passed: Release build (0 warnings/errors), Compose validation, RabbitMQ topology, 10 Python tests and 144 .NET tests.
- [x] CI #104 passed: Release build (0 warnings/errors), Compose validation, RabbitMQ topology, 10 Python tests and 146 .NET tests.
- [x] CI #105 passed for `787bf97b1fcd4083eb6c59bc0595363145e3c83e`: Release build (0 warnings/errors), Compose validation, RabbitMQ topology, 11 Python tests and 147 .NET tests (23 Challenge, 24 Habit, 100 Identity).
- [x] README and communication documentation include Challenge local run/configuration guidance and transport contract.

## Deferred

- [ ] Implement Challenge programme/task/eligibility/participation APIs only under a separate approved SDD.
- [ ] Resolve inactive employee access to historical participation versus new mutations before employee-facing Challenge APIs (open question 19).
- [ ] Add SQS/EventBridge adapter and deploy AWS resources only after owner cloud setup.
- [ ] Do not claim global immediate JWT revocation.
- [ ] Do not implement inbox expiry/cleanup until retention/privacy/legal policy is approved.
