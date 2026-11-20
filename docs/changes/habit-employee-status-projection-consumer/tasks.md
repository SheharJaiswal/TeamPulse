# Tasks: Habit Employee Status Projection Consumer

## SDD and contract review

- [x] Reconcile live feature branch, project checkpoint and existing Identity/RabbitMQ implementation.
- [x] Read service ownership, communication conventions, event envelope and employee-status projection contract.
- [x] Define Habit-only scope, database boundary, event validation, idempotency, version ordering and acknowledgement semantics.
- [x] Map acceptance criteria to unit, MySQL and RabbitMQ tests.

## Implementation

- [ ] Add Habit.API, Habit.Application, Habit.Domain and Habit.Infrastructure projects targeting .NET 8.
- [ ] Add Habit.UnitTests and register all projects in backend/TeamPulse.sln.
- [ ] Add Habit-owned MySQL connection configuration and local teampulse_habit database initialization/grants.
- [ ] Implement EmployeeStatusProjection domain model and application processor/reader abstractions.
- [ ] Implement processed-event inbox and atomic conditional projection upsert.
- [ ] Add checked-in EF Core/Pomelo migration and model snapshot; apply migrations at Habit host startup.
- [ ] Add RabbitMQ consumer with manual acknowledgements, one in-flight delivery per channel and reconnect behavior.
- [ ] Add bounded retry handling and Habit-owned quorum queue/DLX/DLQ local topology.
- [ ] Add safe structured logs, configuration validation and health endpoints.

## Verification

- [ ] Unit-test event validation, transition mapping, deduplication outcomes and strict lifecycleVersion ordering.
- [ ] Run MySQL 8.4 Testcontainers migration, transaction rollback, duplicate and concurrency tests.
- [ ] Run RabbitMQ Testcontainers acknowledgement, redelivery, DLQ and reconnect tests.
- [ ] Validate Compose configuration and actual local Habit queue/DLQ topology.
- [ ] Run full solution restore, Release build and test workflow.
- [ ] Review project references and EF model to prove no Identity database access or cross-service foreign keys.
- [ ] Update README, open questions if necessary and docs/project-status.md with exact results.
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
