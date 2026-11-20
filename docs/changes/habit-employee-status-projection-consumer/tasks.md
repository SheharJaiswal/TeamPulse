# Tasks: Habit Employee Status Projection Consumer

## SDD and contract review

- [x] Reconcile live feature branch, project checkpoint and existing Identity/RabbitMQ implementation.
- [x] Read service ownership, communication conventions, event envelope and employee-status projection contract.
- [x] Define Habit-only scope, database boundary, event validation, idempotency, version ordering and acknowledgement semantics.
- [x] Map acceptance criteria to unit, MySQL and RabbitMQ tests.

## Implementation

- [x] Add Habit.API, Habit.Application, Habit.Domain and Habit.Infrastructure projects targeting .NET 8.
- [x] Add Habit.UnitTests and register all projects in backend/TeamPulse.sln.
- [x] Add Habit-owned MySQL connection configuration and local teampulse_habit database initialization/grants.
- [x] Implement EmployeeStatusProjection domain model and application processor/reader abstractions.
- [x] Implement processed-event inbox and atomic conditional projection upsert.
- [x] Add checked-in EF Core/Pomelo migration and model snapshot; apply migrations at Habit host startup.
- [ ] Add RabbitMQ consumer with manual acknowledgements, one in-flight delivery per channel and reconnect behavior.
- [ ] Add bounded retry handling and Habit-owned quorum queue/DLX/DLQ local topology.
- [ ] Add safe structured logs, configuration validation and health endpoints.

## Verification

- [x] Add unit tests for envelope validation and transition mapping; MySQL integration tests for eventId deduplication and strict lifecycleVersion ordering (CI execution pending).
- [ ] Run MySQL 8.4 Testcontainers migration, duplicate and concurrent-version tests (CI execution pending).
- [ ] Run RabbitMQ Testcontainers acknowledgement, redelivery, DLQ and reconnect tests.
- [ ] Validate Compose configuration and actual local Habit queue/DLQ topology.
- [ ] Run full solution restore, Release build and test workflow.
- [x] Review project references and EF model: Habit references no Identity project and has no cross-service foreign key.
- [x] Update README and project checkpoint; no new business questions were introduced.
- [ ] Commit the coherent implementation to feature/backend-foundation only.

## Acceptance mapping

| Acceptance IDs | Required evidence |
|---|---|
| HESP-01–03 | Solution build, project-reference review and clean/repeated MySQL migration tests |
| HESP-04–05 | Envelope validation and EF schema contract tests |
| HESP-06–09 | Transaction, duplicate, stale/equal and concurrent-version MySQL tests |
| HESP-10–12 | RabbitMQ acknowledgement, replay, poison/DLQ, retry, reconnect and redaction tests |
| HESP-13 | Application projection-reader tests and ownership review |
| HESP-14 | Full GitHub Actions .NET CI run |

## Deferred

- SQS/EventBridge adapter and live AWS deployment.
- The other four downstream consumers.
- Habit product APIs and employee-owned habit features.
- Global token revocation or automated DLQ redrive.


## Increment checkpoint — Habit projection core

- Implemented the Habit Clean Architecture projects, application envelope validator/processor, minimal projection and inbox entities, EF Core/Pomelo context, transactional store, initial migration/model snapshot, startup migration and database-only health checks.
- The atomic MySQL upsert compares the incoming lifecycleVersion against the stored version in the database; the inbox marker is inserted in the same transaction. Duplicate eventId conflicts are isolated to the inbox insert and returned as Duplicate.
- Added processor unit tests, MySQL 8.4 Testcontainers tests for startup/repeated migration, duplicate event IDs, stale events and concurrent N/N+1 processing. Full CI execution is pending.
- Remaining implementation is the RabbitMQ consumer, acknowledgement/reconnect behavior, bounded retries and Habit-owned quorum DLQ topology.
