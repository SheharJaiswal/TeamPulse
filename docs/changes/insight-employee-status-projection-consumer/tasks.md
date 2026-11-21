## Latest verification checkpoint — CI #227 completed with one test assertion failure

- CI #227 completed for code commit `801d01c9f2de1c5278fd5f4b12ea38450fa631cd`. Restore/Release build, Compose validation, RabbitMQ startup/topology, all 19 Python topology tests and pinned DynamoDB Local image pull passed.
- Existing service suites passed 251/251: Identity 100, Habit 24, Challenge 57, Event Tracking 27, Notification 28 and Insight 15; zero failures/skips in those projects.
- Queue-migration integration suite: 7/8 passed. The only failure was the successful-copy header assertion: RabbitMQ.Client returned AMQP header value `"1"` as UTF-8 bytes `[49]`, but the test expected a string. The utility's header-copy implementation copies the received value unchanged.
- Corrected the assertion to compare `Encoding.UTF8.GetBytes("1")` with the returned byte[] using `Assert.IsType<byte[]>`. Fix commit: `5bf2b0e97b5234af4c9dd438b0742e119623582b`. Fresh CI verification is pending.
- Do not mark migration acceptance complete or execute the utility against the persistent broker until the full test suite passes and the utility/runbook/topology review is completed.

## Latest verification checkpoint — CI #227 pending

- CI #227 is running against code commit `801d01c9f2de1c5278fd5f4b12ea38450fa631cd`; latest branch HEAD is documentation-only commit `22539aa448e94a62ab38cd98e40919525c596662`.
- Restore and Release build passed. Docker Compose validation, RabbitMQ startup/topology checks, all 19 Python RabbitMQ topology contract tests and the pinned DynamoDB Local image pull passed.
- The full .NET test step (including the eight queue-migration Testcontainers tests) is still in progress. Do not mark the CI task complete or infer any test totals until the workflow reports success.
- CI #227 includes the mandatory-return exception normalization and corrected Testcontainers `user_id` fixture. The bounded-resume test uses a temporary ledger file.
- Added the AMQP `user_id` migration precondition to the utility README and runbook: inventory non-null values using an approved privacy-safe method and verify they match the migration connection identity. If incompatible or variable values exist, stop and seek explicit owner approval for a property-handling alternative; never silently strip/rewrite the property.
- Next: obtain completed CI evidence, then jointly review utility behavior, the message-preserving runbook and exact current-topology contract tests. Keep the current durable classic Insight queue unchanged; no persistent broker or AWS resource may be changed as part of this verification.

# Tasks: Insight Employee Status Projection Consumer

**Status:** Approved by project owner on 2026-11-21.

## Latest verification checkpoint — CI #223

- CI #223 compiled the full Release solution and passed Docker Compose validation, RabbitMQ startup/topology checks, all 19 Python topology contract tests and the pinned DynamoDB Local image pull.
- Existing service suites passed: Identity 100/100, Habit 24/24, Challenge 57/57, Event Tracking 27/27, Notification 28/28 and Insight 15/15 (251/251 total).
- The new queue-migration integration suite ran 8 tests: 6 passed and 2 failed. One failure showed RabbitMQ.Client 7.2.2 throws `PublishReturnException` directly from mandatory `BasicPublishAsync`; the production helper now normalizes that to its documented `InvalidOperationException`. The other exposed RabbitMQ's broker-side `user_id` validation; the fixture now uses the authenticated Testcontainers username while still asserting the property is preserved.
- Also isolated the bounded-resume test's digest ledger in a temporary file to avoid leaving an artifact in the repository workspace.
- These fixes are committed in `e806e899c33018cdc9161f8707ccae589c8150ab` and `ca88300cf2d85fc47512489dc37a66fcd778d6fd`. CI verification of these fixes is pending. Do not mark full migration acceptance complete or execute the utility against the persistent broker.

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
- [x] Extract the utility's mandatory publisher-confirm/Basic.Return handling into `MandatoryPublisher` and add a deterministic Testcontainers broker test that publishes mandatorily to a missing routing key and asserts the production helper throws on the broker return. This tests the same production component used by the copy loop without relying on a timing-sensitive queue-deletion race.
- [ ] Run the complete Release build and test suite in CI; local .NET SDK is unavailable. Do not execute against the persistent broker.
- [x] Register the standalone utility project in `backend/TeamPulse.sln`; CI compilation and runtime verification remain pending.
- [ ] Review the migration utility, pending-message-preserving runbook and exact topology contract tests together after the full Release build and integration suite pass. Keep the committed Insight topology on its existing durable classic declaration until a separately scheduled maintenance window is approved.
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
