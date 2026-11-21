# Tasks: Event Tracking Employee Status Projection Consumer

**Status:** Draft — implementation blocked on owner approval of this SDD.

## SDD and contract review

- [ ] Owner reviews and approves proposal/spec/design/tasks.
- [x] Reuse the existing Employee Status Projection Contract v1 and shared event envelope without changing them.
- [x] Preserve Event Tracking ownership of activity submissions and audit history; keep this increment projection-only.
- [x] Map acceptance criteria to unit, MySQL 8.4 and RabbitMQ Testcontainers verification.

## Implementation — only after approval

- [ ] Add EventTracking API, Application, Domain and Infrastructure projects targeting .NET 8.
- [ ] Add EventTracking.UnitTests and register all projects in backend/TeamPulse.sln.
- [ ] Add Event Tracking-owned MySQL database configuration and local initialization/grants.
- [ ] Implement minimal projection, inbox, application processor and projection reader.
- [ ] Add checked-in EF migration/model snapshot and startup migration.
- [ ] Add manual-ack RabbitMQ consumer, bounded retry/reconnect and independent consumer health.
- [ ] Add Event Tracking-owned durable DLX/DLQ topology without changing other queues.
- [ ] Add unit, MySQL 8.4 and RabbitMQ Testcontainers coverage, including API-host startup and privacy assertions.
- [ ] Add local run documentation and update README/project status.

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
