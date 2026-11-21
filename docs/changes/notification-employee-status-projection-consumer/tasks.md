# Tasks: Notification Employee Status Projection Consumer

**Status:** Owner-approved on 2026-11-21; implementation in progress.

## SDD and contract review

- [x] Read AGENTS.md, ADR-0001, service ownership, communication conventions and shared event/projection contracts.
- [x] Confirm Notification is one of the existing six services and owns preferences, notification records and delivery history.
- [x] Confirm local `teampulse.notification.events` exists as a durable queue with no queue arguments and no Notification DLQ/binding.
- [x] Reuse Employee Status Projection Contract v1 without changing the shared contract.
- [x] Keep this increment projection-only; no notification sends, preference changes or delivery-history mutations.
- [x] Owner approved proposal.md, spec.md, design.md and tasks.md on 2026-11-21.

## Implementation

- [x] Add Notification Domain and Application projects with inward-only references (initial application-core increment).
- [x] Add Notification.UnitTests and register Domain, Application and test projects in backend/TeamPulse.sln.
- [ ] Add Notification-owned MySQL database initialization, Compose mount and safe existing-volume migration runbook.
- [x] Implement minimal projection/inbox domain entities, schema-v1 envelope validator and transport-independent application processor.
- [ ] Add checked-in EF migration, startup migration and production projection reader/store.
- [ ] Add opt-in RabbitMQ consumer with manual ack, bounded retries, reconnect and independent consumer health.
- [ ] Upgrade only Notification source queue to quorum/DLX/DLQ with pending-message-preserving migration instructions.
- [ ] Add unit, MySQL 8.4, RabbitMQ Testcontainers, API-host and topology contract tests.
- [x] Add processor tests for strict recognized-field validation, additive unknown-field tolerance, transition consistency, note exclusion and zero persistence calls on invalid input.
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


## Execution checkpoint — application core

- [x] Owner approval recorded on 2026-11-21.
- [x] Commit `d8e207f7a7650bc1747baf8509e17c5f4e2b305f` adds Notification.Domain, Notification.Application and Notification.UnitTests, plus solution registration.
- [x] Deterministic processor tests cover valid deactivation/reactivation, exact producer source, reason-code bounds, unsupported schema, malformed/missing fields, inconsistent transitions, unknown additive fields and note non-persistence.
- [ ] CI verification pending for the new solution projects and tests.
- [ ] Continue with Notification infrastructure persistence/migration, API host and consumer/topology in separate increments.
