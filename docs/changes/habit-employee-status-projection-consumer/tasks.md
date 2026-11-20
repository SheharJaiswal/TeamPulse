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
- [x] Add Habit database liveness/readiness endpoints; readiness checks only the Habit-owned database.
- [ ] Add consumer-specific configuration validation and safe delivery logging with the RabbitMQ adapter.

## Verification

- [x] Add unit tests for envelope validation/transition mapping and MySQL integration tests for duplicate eventId, stale/equal and strict lifecycleVersion ordering.
- [x] Run MySQL 8.4 Testcontainers migration/repeated startup, inbox rollback, duplicate, stale/equal and concurrent N/N+1 version tests (CI run #93).
- [ ] Run RabbitMQ Testcontainers acknowledgement, redelivery, DLQ and reconnect tests.
- [x] Validate Docker Compose configuration and the existing shared RabbitMQ exchange/five-queue topology; Habit-owned quorum queue/DLQ topology remains part of the consumer increment.
- [x] Run full solution restore, Release build and test workflow (CI run #93: 112 passed, 0 failed, 0 skipped; 0 build warnings/errors).
- [x] Review project references and EF model: Habit references no Identity project and has no cross-service foreign key.
- [x] Update README and project checkpoint; no new business questions were introduced.
- [x] Commit the coherent persistence/application increment to feature/backend-foundation only.

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
- Added processor unit tests, MySQL 8.4 Testcontainers tests for startup/repeated migration, inbox rollback, duplicate event IDs, stale/equal events and concurrent N/N+1 processing. GitHub Actions run #93 passed all 12 Habit tests.
- Remaining implementation is the RabbitMQ consumer, acknowledgement/reconnect behavior, bounded retries and Habit-owned quorum DLQ topology.


## Verification result — Habit projection core

- GitHub Actions .NET CI run #93: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44731041383
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: passed, 0 warnings and 0 errors.
- `docker compose config --quiet`: passed.
- `docker compose up -d --wait --wait-timeout 90 rabbitmq`: passed; existing exchange and five service queues verified.
- `python -m unittest discover -s infra/rabbitmq/tests -v`: 4 passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: 112 passed (12 Habit, 100 Identity), 0 failed, 0 skipped. Habit MySQL 8.4 Testcontainers migration/repeat, inbox rollback, duplicate, stale and concurrent-version tests passed.
- Remaining unchecked implementation and verification tasks are specifically the Habit RabbitMQ consumer, manual acknowledgement/reconnect, bounded retry and Habit-owned quorum DLQ topology/tests.
