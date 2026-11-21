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
- [ ] Update only Insight source/DLQ topology after pending-message-preserving migration runbook and exact contract tests are in place.
- [x] Add API host health checks and Development-only local table initialization; production must not auto-create cloud tables.
- [ ] Add privacy assertions proving raw payload, note and credentials are not stored/logged.
- [x] Add runbooks/configuration docs and update project status.
- [ ] Verify full Release build, DynamoDB Local integration, RabbitMQ Testcontainers, Compose and topology tests in CI.

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
