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
- [x] Add RabbitMQ.Client consumer with manual acknowledgements, prefetch 1, explicit reconnect loop and separate consumer connection health.
- [x] Add bounded transient-error retries, poison/exhaustion dead-lettering and Habit-owned quorum queue/DLX/DLQ local topology.
- [x] Add Habit database liveness/readiness endpoints; readiness checks only the Habit-owned database.
- [x] Add consumer-specific configuration validation and payload/connection-secret-safe structured delivery logging.

## Verification

- [x] Add unit tests for envelope validation/transition mapping and MySQL integration tests for duplicate eventId, stale/equal and strict lifecycleVersion ordering.
- [x] Run MySQL 8.4 Testcontainers migration/repeated startup, inbox rollback, duplicate, stale/equal and concurrent N/N+1 version tests (CI run #93).
- [x] Run a broker-connection-loss redelivery test after an established session; verify the first MySQL inbox/projection transaction commits before broker loss, the unacknowledged message is redelivered after reconnect, the replay is deduplicated by the same eventId, and the projection remains unchanged (CI run #100).
- [x] Validate Docker Compose configuration and shared RabbitMQ exchange/five service queues plus Habit quorum queue/DLX/DLQ definitions; actual Compose startup is covered by CI.
- [x] Run full solution restore, Release build and test workflow (CI run #100: 124 passed, 0 failed, 0 skipped; 0 build warnings/errors).
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
- RabbitMQ consumer, manual acknowledgement, bounded retry, startup reconnect, Habit-owned quorum DLQ topology and established-session broker-loss redelivery are implemented and verified (CI run #100).


## Verification result — Habit projection core

- GitHub Actions .NET CI run #93: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44731041383
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: passed, 0 warnings and 0 errors.
- `docker compose config --quiet`: passed.
- `docker compose up -d --wait --wait-timeout 90 rabbitmq`: passed; existing exchange and five service queues verified.
- `python -m unittest discover -s infra/rabbitmq/tests -v`: 4 passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: 112 passed (12 Habit, 100 Identity), 0 failed, 0 skipped. Habit MySQL 8.4 Testcontainers migration/repeat, inbox rollback, duplicate, stale and concurrent-version tests passed.
- Remaining unchecked implementation and verification tasks are specifically the Habit RabbitMQ consumer, manual acknowledgement/reconnect, bounded retry and Habit-owned quorum DLQ topology/tests.


## Increment checkpoint — Habit RabbitMQ consumer adapter

- Added a Habit-owned RabbitMQ.Client 7.2.2 BackgroundService. It is disabled by default and uses the existing transport-independent application processor through a fresh DI scope for each attempt.
- The consumer uses manual acknowledgements, prefetch 1, one active delivery per channel, strict UTF-8 decoding, bounded full-jitter retries for known transient network/MySQL failures, poison/exhaustion dead-lettering and an explicit bounded reconnect loop. Ack/nack failures close the channel so unacknowledged messages are eligible for broker redelivery.
- Added a separate `/health/consumer` signal; database readiness remains independent of broker availability.
- Updated only Habit's local queue to quorum with delivery limit 5, at-least-once dead-lettering and reject-publish overflow; added a Habit-owned quorum DLQ and direct DLX. Other service queues remain unchanged.
- Added RabbitMQ Testcontainers tests for ack-after-processor-completion, invalid-envelope DLQ routing, transient retry success, retry exhaustion and startup reconnect; added eight Python topology contract tests and extended CI to assert the Habit DLQ and DLX exist.
- Established-session broker-loss redelivery is covered by `HabitRabbitMqBrokerLossIntegrationTests`; the test gates acknowledgement after the real MySQL transaction, restarts RabbitMQ on the same host port, verifies the same eventId is redelivered and deduplicated, and checks that one inbox row and the version-1 inactive projection remain.


## Verification result — Habit RabbitMQ consumer

- GitHub Actions run #97: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44739492656
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: passed, 0 warnings and 0 errors.
- `docker compose config --quiet`: passed; actual RabbitMQ Compose startup and queue/exchange checks passed.
- `python -m unittest discover -s infra/rabbitmq/tests -v`: 8 passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: 123 passed (23 Habit, 100 Identity), 0 failed, 0 skipped.
- The transient retry integration test wraps a `TimeoutException` in an `InvalidOperationException`, verifying the classifier walks inner exceptions as EF Core does.
- Established-session broker-loss redelivery and idempotent replay are verified by CI run #100; no Habit consumer verification item remains unchecked.
