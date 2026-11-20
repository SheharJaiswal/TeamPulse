# Tasks: Challenge Employee Status Projection Consumer

## SDD and contract review
- [x] Fetch live feature/backend-foundation HEAD and inspect recent commits/tree.
- [x] Read AGENTS.md, README.md, project status, open questions, service ownership, communication conventions, ADR-0001/0002 and shared event/projection contracts.
- [x] Confirm Challenge has no existing service project or tests; inspect Habit processor, MySQL store, RabbitMQ consumer and topology tests as references.
- [x] Define projection-only scope without inventing Challenge programme/participation rules.
- [x] Record inactive employee historical-read versus mutation policy as an open question for later Challenge API work.
- [x] Map CEPS-01 through CEPS-15 to implementation and verification.

## Approval gate — blocked
- [ ] Owner reviews and explicitly approves or requests changes to this SDD package. The proposal requires approval before implementation; no approval record is present in the repository or prior continuation context.
- Existing Challenge implementation was found on the live branch. Treat it as existing repository state, not evidence that the SDD was approved. Do not add further Challenge feature scope until approval is recorded.

## Implementation already present on the live branch — approval gate pending
- [x] Add Challenge.API, Challenge.Application, Challenge.Domain and Challenge.Infrastructure with inward-only references.
- [x] Add Challenge.UnitTests and register all projects in backend/TeamPulse.sln.
- [x] Add Challenge-owned MySQL database initialization/grant and connection configuration.
- [x] Implement minimal employee status projection, inbox entity and transport-independent application processor.
- [x] Implement atomic inbox + conditional lifecycle-version upsert in Challenge-owned MySQL.
- [x] Add checked-in EF Core/Pomelo migration and model snapshot; apply migrations at host startup.
- [x] Add database liveness/readiness and separate consumer-health endpoints.
- [x] Implement Challenge RabbitMQ consumer with manual ack after processor completion, prefetch 1, bounded retry and reconnect.
- [x] Add Challenge-owned quorum queue, DLX and DLQ without changing Habit or other service queues.
- [x] Keep consumer disabled by default and validate configuration when enabled.

## Verification — in progress
- [x] Unit-test envelope validation, event mapping, invalid input and note non-persistence.
- [x] MySQL 8.4 Testcontainers: duplicate eventId, stale/equal/newer versions, concurrent N/N+1 and rollback.
- [ ] MySQL 8.4 Testcontainers: clean and repeated host-startup migration, exact schema/model assertions and projection reader.
- [x] RabbitMQ Testcontainers: processor-completion acknowledgement boundary, poison-message DLQ, transient retry success and retry exhaustion.
- [ ] RabbitMQ Testcontainers: duplicate redelivery after commit-before-ack and idempotent replay.
- [ ] RabbitMQ Testcontainers: broker unavailable at startup and established-session loss/reconnect. Startup-reconnect coverage is authored in `ChallengeRabbitMqConsumerReconnectTests.cs` and awaits CI #105; established-session loss/redelivery remains outstanding.
- [ ] Verify database readiness is independent of RabbitMQ and consumer health is separately observable through host integration tests.
- [x] Review project references, database ownership, minimal projection/inbox fields and absence of Challenge business-state mutation.
- [x] Extend RabbitMQ topology contract tests for Challenge quorum queue, delivery limit and dedicated DLQ binding.
- [x] Baseline CI run #103 passed for commit `121bba186bb46df139f7e49def8082ab9d541afa`: Release build (0 warnings/errors), Compose validation, RabbitMQ startup/topology checks, 10 Python topology tests and 144 .NET tests (20 Challenge, 24 Habit, 100 Identity).
- [x] CI run #104 passed for `65d4a3bd82294f484dd136f4788975f26da85651`: Release build (0 warnings/errors), Compose validation, RabbitMQ startup/topology checks, 10 Python topology tests and 146 .NET tests (22 Challenge, 24 Habit, 100 Identity).
- [ ] CI run #105 is verifying `787bf97b1fcd4083eb6c59bc0595363145e3c83e`. Restore, Release build, Compose validation and RabbitMQ startup/topology checks passed; the full test step is still running. Do not mark the startup-reconnect test verified until this run completes.
- [x] Update README and communication documentation with Challenge local run/configuration guidance and the consumer transport contract.

## Deferred
- [ ] Implement Challenge programme/task/eligibility/participation APIs only under a separate approved SDD.
- [ ] Resolve inactive employee access to historical participation versus new mutations before employee-facing Challenge APIs.
- [ ] Add SQS/EventBridge adapter and deploy AWS resources only after owner cloud setup.
- [ ] Do not claim global immediate JWT revocation.
