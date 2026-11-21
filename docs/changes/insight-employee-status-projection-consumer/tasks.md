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
- [x] Implement the first standalone Insight queue migration utility increment: read-only passive preflight, explicit execution plus quiescence attestation, source-consumer guard, mandatory publisher-confirmed copy before source acknowledgement, persistent delivery mode, supported basic-property/header copying, payload SHA-256-only ledger, and no queue declaration/deletion/purge/reconfiguration. Utility and usage guide are under backend/src/Tools/Insight.QueueMigration. Build and integration verification remain pending.
- [ ] Add isolated RabbitMQ Testcontainers end-to-end coverage for preflight non-mutation, message/body/header/property preservation (including persistent delivery mode), confirmed copy/ack ordering, crash/retry duplicate semantics, bounded batch resume and source/destination queue-count reconciliation. Do not execute against the persistent broker.
- [ ] Add the utility project to the solution/CI build after the implementation and tests compile cleanly. Review the migration utility, pending-message-preserving runbook and exact topology contract tests together.
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
