# Tasks: Notification Employee Status Projection Consumer

**Status:** Owner-approved on 2026-11-21; implementation and CI acceptance verified on 2026-11-21 (CI #168).

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
- [x] Add Notification-owned MySQL database initialization, Compose mount and safe existing-volume migration runbook.
- [x] Implement minimal projection/inbox domain entities, schema-v1 envelope validator and transport-independent application processor.
- [x] Add checked-in EF migration, transactional projection/inbox store, reader abstraction and API startup migration.
- [x] Add opt-in RabbitMQ consumer with manual ack, bounded retries, reconnect and independent consumer health.
- [x] Upgrade only Notification source queue to quorum/DLX/DLQ definitions and add pending-message-preserving migration instructions.
- [x] Add MySQL 8.4 Testcontainers coverage for migration idempotency/schema, duplicate event IDs, stale/equal ordering and concurrent maximum-version preservation, plus RabbitMQ/API-host integration tests.
- [x] Add processor tests for strict recognized-field validation, additive unknown-field tolerance, transition consistency, note exclusion and zero persistence calls on invalid input.
- [x] Document existing MySQL/RabbitMQ volume migrations; API host configuration is documented in appsettings and consumer is disabled by default.
- [x] Run full restore, Release build, solution tests, Compose validation and topology tests in CI (#165 passed for `13095cc3effb30a11862ccc68a64fc3ebf60425e`).
- [x] Verify malformed-event DLQ delivery and unavailable-DLQ-route source redelivery in RabbitMQ Testcontainers.

## Acceptance mapping

| Acceptance IDs | Required evidence |
|---|---|
| NESP-01–03 | Verified by CI #168 Release build and MySQL 8.4 Testcontainers migration/API-host tests. |
| NESP-04–05 | Verified by Notification processor validation and exact-schema persistence tests in CI #168. |
| NESP-06–08 | Verified by MySQL transaction, duplicate, stale/equal ordering and concurrent-version tests in CI #168. |
| NESP-09–11 | Verified by RabbitMQ replay, confirmed DLQ, unavailable-route redelivery, consumer/API health and reconnect coverage in CI #168. |
| NESP-12–13 | Verified by minimal-field persistence/privacy and transport-independent processor tests in CI #168. |
| NESP-14 | Verified by successful full GitHub Actions workflow #168. |

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


## Execution checkpoint — Notification persistence

- [x] Commit `7f92cf4128f9ca3fd2f72ca4d8a8f4ac88654732` adds Notification.Infrastructure, the checked-in EF migration/model snapshot, transactional inbox/projection store, MySQL fixture/integration tests, database init SQL, Compose mount and existing-volume migration runbook.
- [x] Persistence tests assert exact projection/inbox columns, idempotent migration, duplicate eventId, stale/equal version safety and concurrent maximum lifecycle version.
- [ ] CI verification pending for persistence increment.
- [ ] Add API host/startup migration, then RabbitMQ consumer/topology with safe persistent-broker migration.


## Execution checkpoint — RabbitMQ consumer and topology

- [x] Added opt-in Notification RabbitMQ consumer using RabbitMQ.Client 7.x, manual acknowledgements after committed processing, bounded transient retries, reconnect and independent consumer status/health.
- [x] Added publisher-confirmed mandatory DLQ publishing with failure classification headers and source acknowledgement only after broker confirmation.
- [x] Updated only Notification's source queue to quorum with delivery limit 5, at-least-once DLX and reject-publish overflow; added Notification quorum DLQ and exact routing binding.
- [x] Added Notification RabbitMQ persistent-message replay integration test based on the already-proven Event Tracking harness, plus options/retry and API consumer-health tests.
- [x] Added `docs/runbooks/notification-rabbitmq-queue-migration.md`; it requires pausing publisher/consumer and buffering pending messages before queue recreation. No live broker topology was changed.
- [x] CI #165 passed for commit `13095cc3effb30a11862ccc68a64fc3ebf60425e`; restore, Release build, Compose, RabbitMQ topology and full .NET/Testcontainers suite passed.
- [x] CI #168 passed for commit `bd27c4a118e851d02625d5e2002a9be357acec0a`; both DLQ integration tests passed. NESP-10 is verified.
