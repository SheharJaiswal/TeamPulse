# Tasks: Challenge Employee Status Projection Consumer

## SDD and contract review
- [x] Fetch live feature/backend-foundation HEAD and inspect recent commits/tree.
- [x] Read AGENTS.md, README.md, project status, open questions, service ownership, communication conventions, ADR-0001/0002 and shared event/projection contracts.
- [x] Confirm Challenge has no existing service project or tests; inspect Habit processor, MySQL store, RabbitMQ consumer and topology tests as references.
- [x] Define projection-only scope without inventing Challenge programme/participation rules.
- [x] Record inactive employee historical-read versus mutation policy as an open question for later Challenge API work.
- [x] Map CEPS-01 through CEPS-15 to implementation and verification.

## Implementation — not started
- [ ] Add Challenge.API, Challenge.Application, Challenge.Domain and Challenge.Infrastructure with inward-only references.
- [ ] Add Challenge.UnitTests and register all projects in backend/TeamPulse.sln.
- [ ] Add Challenge-owned MySQL database initialization/grant and connection configuration.
- [ ] Implement minimal employee status projection, inbox entity and transport-independent application processor.
- [ ] Implement atomic inbox + conditional lifecycle-version upsert in Challenge-owned MySQL.
- [ ] Add checked-in EF Core/Pomelo migration and model snapshot; apply migrations at host startup.
- [ ] Add database liveness/readiness and separate consumer-health endpoints.
- [ ] Implement Challenge RabbitMQ consumer with manual ack after commit, prefetch 1, bounded retry and reconnect.
- [ ] Add Challenge-owned quorum queue, DLX and DLQ without changing Habit or other service queues.
- [ ] Keep consumer disabled by default and validate configuration when enabled.

## Verification — not started
- [ ] Unit-test envelope validation, event mapping, invalid input and note non-persistence.
- [ ] MySQL 8.4 Testcontainers: clean/repeated startup migration, duplicate eventId, stale/equal/newer versions, concurrent N/N+1 and rollback.
- [ ] RabbitMQ Testcontainers: ack boundary, duplicate redelivery, poison DLQ, retry/exhaustion and broker reconnect/loss.
- [ ] Verify database readiness is independent of RabbitMQ and consumer health is separate.
- [ ] Verify project references, database ownership, schema privacy and no Challenge business-state mutation.
- [ ] Extend RabbitMQ topology contract tests and run full .NET CI.
- [ ] Update README, communication documentation and checkpoint with exact commands/results.

## Deferred
- [ ] Implement Challenge programme/task/eligibility/participation APIs only under a separate approved SDD.
- [ ] Resolve inactive employee access to historical participation versus new mutations before employee-facing Challenge APIs.
- [ ] Add SQS/EventBridge adapter and deploy AWS resources only after owner cloud setup.
- [ ] Do not claim global immediate JWT revocation.
