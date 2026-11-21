# Tasks: Notification Employee Status Projection Consumer

**Status:** Draft — awaiting owner approval. No implementation is authorized.

## SDD and contract review

- [x] Read AGENTS.md, ADR-0001, service ownership, communication conventions and shared event/projection contracts.
- [x] Confirm Notification is one of the existing six services and owns preferences, notification records and delivery history.
- [x] Confirm local `teampulse.notification.events` exists as a durable queue with no queue arguments and no Notification DLQ/binding.
- [x] Reuse Employee Status Projection Contract v1 without changing the shared contract.
- [x] Keep this increment projection-only; no notification sends, preference changes or delivery-history mutations.
- [ ] Owner approval of proposal.md, spec.md, design.md and tasks.md.

## Implementation — blocked pending approval

- [ ] Add Notification API, Application, Domain and Infrastructure projects with inward-only references.
- [ ] Add Notification.UnitTests and register projects in backend/TeamPulse.sln.
- [ ] Add Notification-owned MySQL database initialization, Compose mount and safe existing-volume migration runbook.
- [ ] Implement minimal projection, processed-event inbox, validator and transport-independent processor.
- [ ] Add checked-in EF migration, startup migration and production projection reader/store.
- [ ] Add opt-in RabbitMQ consumer with manual ack, bounded retries, reconnect and independent consumer health.
- [ ] Upgrade only Notification source queue to quorum/DLX/DLQ with pending-message-preserving migration instructions.
- [ ] Add unit, MySQL 8.4, RabbitMQ Testcontainers, API-host and topology contract tests.
- [ ] Verify no notification business side effects and no raw payload/note/secrets persistence/logging.
- [ ] Document local host/Compose run configuration and existing MySQL/RabbitMQ volume migrations.
- [ ] Run full restore, Release build, solution tests, Compose validation and topology tests in CI.
- [ ] Update acceptance evidence and docs/project-status.md.

## Acceptance mapping

| Acceptance IDs | Required evidence |
|---|---|
| NESP-01–03 | Release build, ownership review, clean/repeated MySQL host migration |
| NESP-04–05 | Envelope validation and exact persistence schema |
| NESP-06–08 | Atomicity, duplicate, ordering and concurrent MySQL tests |
| NESP-09–11 | RabbitMQ ack/replay, DLQ, reconnect and API health tests |
| NESP-12–13 | Persistence/log privacy and no-notification-side-effect assertions |
| NESP-14 | Full GitHub Actions workflow |

## Deferred

- Notification delivery/preferences and lifecycle-triggered notification behavior.
- Employee-facing APIs and inactive-employee authorization policy.
- Insight consumer and AWS EventBridge/SQS provisioning.
- Inbox cleanup, privacy/legal retention approval and global token revocation.

## Infrastructure constraints

- Existing Notification RabbitMQ queue is durable classic topology with no queue arguments. Preserve pending messages before changing immutable queue arguments/type.
- Existing MySQL volumes do not rerun init scripts; do not reset the volume.
- AWS EventBridge/SQS and cloud provisioning remain deferred until owner-managed cloud setup.
- Never log raw event bodies, optional notes, credentials or broker URIs.
