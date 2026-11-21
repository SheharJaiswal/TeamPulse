# Tasks: Event Tracking Employee Status Projection Consumer

**Status:** Owner-approved SDD; implementation and CI verification in progress.

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
- [x] Add initial processor unit tests and MySQL 8.4 Testcontainers store tests.
- [x] Add opt-in manual-ack RabbitMQ consumer, bounded retry/reconnect and independent consumer health.
- [x] Upgrade only the Event Tracking source queue to quorum with delivery limit 5, at-least-once dead-lettering and reject-publish overflow; add dedicated quorum DLQ and exact routing-key contract tests.
- [x] Document safe migration of the existing persistent RabbitMQ queue using a durable migration buffer and message-count verification; never silently discard messages or reset shared volume.
- [x] Add retry/options unit tests and topology contract tests.
- [x] Verify consumer/topology increment with GitHub Actions CI #136: Release build, Compose validation, RabbitMQ startup/topology and 194 .NET tests plus 14 Python topology tests passed.
- [x] Add production API-host startup integration test against fresh MySQL 8.4, proving startup migration and production reader registration.
- [x] Add RabbitMQ Testcontainers coverage for post-processing acknowledgement, terminal-outcome DLQ confirmation, transient retry success/exhaustion, failed DLQ route redelivery and log privacy.
- [x] Verify API-host startup with GitHub Actions CI #137: all 17 EventTracking tests passed, including the fresh MySQL 8.4 API-host migration/readback test; total solution tests 195 passed.
- [x] Verify RabbitMQ consumer integration suite with GitHub Actions CI #140 attempt 2: EventTracking 24, Habit 24, Identity 100 and Challenge 54 tests passed; 14 Python topology tests passed.
- [x] Verify production processor replay integration test against MySQL 8.4 inbox, including conflicting duplicate payload (CI #142: passed).
- [ ] Verify actual API-host consumer health isolation against RabbitMQ and MySQL (CI #146 confirmed a one-second test heartbeat does not resolve the HTTP 503 timeout. Next fix marks the status disconnected directly in connection/channel shutdown callbacks; production heartbeat remains five seconds).
- [ ] Add commit-before-ack broker interruption/replay integration test using the actual persistent inbox.
- [ ] Review full CI test output and acceptance matrix before marking this SDD complete.

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

Updated: 2026-11-21
