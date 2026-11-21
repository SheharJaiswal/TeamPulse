# Tasks: Event Tracking Employee Status Projection Consumer

**Status:** Owner-approved SDD; implementation in progress.

## SDD and contract review

- [x] Owner reviewed and approved proposal/spec/design/tasks on 2026-11-21.
- [x] Reuse Employee Status Projection Contract v1 and the shared event envelope without changing them.
- [x] Preserve Event Tracking ownership of activity submissions and audit history; keep this increment projection-only.
- [x] Map acceptance criteria to unit, MySQL 8.4 and RabbitMQ Testcontainers verification.

## Implementation

- [x] Add EventTracking API, Application, Domain and Infrastructure projects targeting .NET 8.
- [x] Add EventTracking.UnitTests and register all projects in backend/TeamPulse.sln.
- [x] Add Event Tracking-owned MySQL database configuration, initialization script and Compose mount.
- [x] Document the explicit database/grant migration required for existing persistent MySQL volumes.
- [x] Implement minimal employee-status projection, inbox, application processor and projection reader.
- [x] Add checked-in EF migration/model snapshot and startup migration.
- [x] Add initial processor unit tests and MySQL 8.4 Testcontainers store tests (CI verification pending).
- [ ] Verify clean Release build and all existing/new tests in GitHub Actions.
- [ ] Add production API-host startup integration test against fresh MySQL, proving migrations and production reader.
- [ ] Add manual-ack RabbitMQ consumer, bounded retry/reconnect and independent consumer health.
- [ ] Upgrade only the Event Tracking source queue to quorum with delivery limit 5, at-least-once dead-lettering and reject-publish overflow; add dedicated quorum DLQ and exact routing-key contract test.
- [x] Document safe migration of the existing persistent RabbitMQ queue using a durable migration buffer and message-count verification; never silently discard messages or reset shared volume.
- [ ] Add unit, MySQL 8.4 and RabbitMQ Testcontainers coverage for consumer acknowledgement, retry, DLQ, reconnect, replay and privacy. Initial retry/options unit tests are authored; consumer integration coverage remains pending.
- [ ] Add local run documentation and update README/project status after consumer topology is complete.

## Acceptance mapping

| Acceptance IDs | Required evidence |
|---|---|
| ETESP-01–03 | Release build, ownership review and clean MySQL host-startup migration |
| ETESP-04–05 | Envelope and projection schema contract tests |
| ETESP-06–08 | Transaction, duplicate, ordering and concurrency tests |
| ETESP-09–11 | RabbitMQ ack/replay, DLQ/reconnect and health isolation tests |
| ETESP-12–13 | Persistence/log privacy and application boundary tests |
| ETESP-14 | Full GitHub Actions workflow |

## Deferred

- Activity submission/evidence APIs and access policy.
- SQS/EventBridge production adapter and AWS provisioning.
- Insight and Notification consumers.
- Global immediate token revocation, automated DLQ redrive and retention cleanup.

## Infrastructure constraints

- Existing Event Tracking source queue: durable classic queue with no queue arguments; binding is `teampulse.domain.events` -> `teampulse.event-tracking.events` with routing key `identity.employee.lifecycle`.
- The intended upgrade is Event Tracking-owned quorum queue with delivery limit 5, at-least-once dead-lettering, reject-publish overflow and a dedicated Event Tracking DLQ. Queue type/arguments are immutable on redeclaration; migration must deliberately preserve/drain pending lifecycle messages before recreation.
- Existing MySQL volumes do not rerun init scripts. Follow `docs/runbooks/event-tracking-local-migrations.md`.
- AWS EventBridge/SQS and cloud provisioning remain deferred until owner cloud setup.
- Inbox retention is initially permanent and requires privacy/legal review before production.
- Never log raw event bodies, optional notes, credentials or broker URIs.
