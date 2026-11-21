# Tasks: Insight Employee Status Projection Consumer

**Status:** Approved by project owner on 2026-11-21.

## SDD and contract review
- [x] Read AGENTS.md, ADR-0001, service ownership, communication conventions and shared event/projection contracts.
- [x] Confirm Insight is one of the six services and owns aggregated/derived read models only.
- [x] Confirm the repository currently has no Insight service projects and has an existing durable classic teampulse.insight.events queue without queue arguments or a DLQ.
- [x] Confirm Insight persistence must use DynamoDB, not the MySQL/Pomelo pattern used by transactional services.
- [x] Keep the increment limited to employee lifecycle projection/inbox; no analytics, dashboards, aggregates or employee-facing APIs.
- [x] Owner approval of proposal.md, spec.md, design.md and tasks.md — approved 2026-11-21.

## Implementation
- [x] Add Insight.Application, Insight.Domain and Insight.UnitTests foundation with inward-only references.
- [x] Add Insight.API and Insight.Infrastructure hosts/adapters with inward-only references.
- [x] Add DynamoDB SDK client configuration, Insight-owned store abstraction and single-table key/item model.
- [x] Implement schema-v1 envelope validation and transport-independent lifecycle processor, including Identity's optional-note type/1000-character constraint without persisting or logging the note.
- [x] Implement DynamoDB TransactWriteItems inbox/projection persistence with conditional version fences, duplicate detection and bounded optimistic-concurrency retries.
- [x] Add DynamoDB Local for local execution and isolated integration tests; no AWS account or credentials. CI #188 passed all 11 Insight tests.
- [x] Add opt-in RabbitMQ consumer with manual ack after committed transaction, bounded transient retries, reconnect and separate consumer health.
- [x] Add confirmed DLQ publishing and failure classifications; preserve source delivery if DLQ routing/confirmation fails.
- [x] Implement the first standalone Insight queue migration utility increment: read-only passive preflight, explicit execution plus quiescence attestation, source/destination consumer guards, mandatory publisher-confirmed copy with returned-message detection before source acknowledgement, persistent delivery mode, supported basic-property/header copying, payload SHA-256-only ledger, and no queue declaration/deletion/purge/reconfiguration. Utility and usage guide are under backend/src/Tools/Insight.QueueMigration. Build and integration verification remain pending.
- [x] Add an isolated RabbitMQ Testcontainers integration-test project and process-level coverage for read-only preflight/non-mutation (including missing destination), message body/header/basic-property preservation with persistent delivery mode, bounded batch resume, source/destination count checks and source/destination active-consumer refusal. Tests run the actual utility executable against a disposable broker.
- [x] Add isolated failure-path coverage that forces the ledger append to fail after the confirmed destination publish, verifies the source remains unacknowledged/requeued, and demonstrates the documented at-least-once duplicate on retry. The same test establishes that a confirmed destination copy exists before source acknowledgement.
- [ ] Add a deterministic utility-level mandatory-unroutable-publish race test, then run the complete Release build and test suite. Do not execute against the persistent broker.
- [x] Register the standalone utility project in `backend/TeamPulse.sln`; CI compilation and runtime verification remain pending.
- [ ] Review the migration utility, pending-message-preserving runbook and exact topology contract tests together after the remaining failure-path tests pass.
- [ ] Review the migration utility, pending-message-preserving runbook and exact topology contract tests together. Keep the committed Insight topology on its existing durable classic declaration until a separately scheduled maintenance window is approved.
- [ ] Update only Insight source/DLQ topology after the migration utility, runbook and exact contract tests are reviewed and the controlled maintenance window is approved.
- [x] Add API host health checks and Development-only local table initialization; production must not auto-create cloud tables.
- [x] Add privacy assertions proving raw payload, note and credentials are not stored/logged — Consumer_DoesNotLogRawPayloadNotesCredentialsOrConnectionStrings passed in Insight.UnitTests (15/15) in CI #193 attempt 1.
- [x] Add runbooks/configuration docs and update project status.
- [x] Verify full Release build, DynamoDB Local integration, RabbitMQ Testcontainers, Compose and topology tests in CI — CI #190 passed for code commit `9ca613c41f9116296935270c19b8f8b136c78c5a`; 249/249 .NET tests passed, including all 14 Insight tests; zero failed or skipped.
- [x] Cross-service regression and Insight privacy verification: CI #193 attempt 2 passed 250/250 .NET tests; CI #195 passed 251/251 .NET tests (including 15 Insight tests), all 19 Python RabbitMQ topology tests, Release build, Compose validation and pinned DynamoDB Local image pull.

## Acceptance mapping
| Acceptance IDs | Required evidence |
|---|---|
| IESP-01–02 | Solution/project-reference boundary and DynamoDB-only persistence verification |
| IESP-03–04 | DynamoDB Local item schema and processor validation/privacy tests |
| IESP-05–08 | DynamoDB transaction, duplicate, stale/equal, newer and concurrent version tests |
| IESP-09–11 | RabbitMQ ack/replay, DLQ, bounded retry, reconnect and API health tests |
| IESP-12–13 | Persistence/log privacy assertions and transport-independent/no-analytics-side-effect review |
| IESP-14 | Full GitHub Actions workflow |

## Deferred
- EventBridge/SQS adapter, AWS provisioning, IAM, encryption, backup and cloud deployment.
- Insight dashboards, cohort metrics, aggregation pipelines and read APIs.
- Policy for excluding inactive employees from future aggregate calculations or modifying historical aggregates.
- Inbox TTL/cleanup and privacy/legal retention approval.
- DLQ redrive tooling and production observability.

## Infrastructure constraints
- Preserve the existing Insight RabbitMQ queue and all pending messages until an approved maintenance-window migration.
- RabbitMQ queue type/arguments are immutable after declaration; never delete/purge the queue or reset the shared broker volume as a shortcut.
- DynamoDB Local is for local development/test only. Production table and IAM provisioning remain deferred until owner-managed cloud setup.
- Do not add MySQL, EF Core or cross-service persistence references to Insight.
- Never log raw event bodies, optional notes, credentials, tokens or connection strings.
