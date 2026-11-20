# Tasks: Lease-Safe Identity Outbox Dispatcher

## Reconciliation and SDD

- [x] Fetch the live `feature/backend-foundation` branch and inspect recent commits and repository tree.
- [x] Reconcile the live branch with `docs/project-status.md`; confirm HR lifecycle implementation is complete and CI run #72 is the latest verified code result.
- [x] Read AGENTS.md, README.md, Identity specification, ownership, communication conventions, ADR-0001/0002, event envelope/catalog, lifecycle SDD, open questions and current outbox persistence.
- [x] Confirm the repository has no deployment/IaC tree, EventBridge bus definition, workload IAM role, EventBridge rules/targets, SQS queue/DLQ definitions or monitoring resources.
- [x] Define configuration-gated EventBridge publishing, AWS credential/IAM boundary and event mapping.
- [x] Define lease-safe MySQL claim/acknowledgement, partial batch results, retry/backoff, producer-side quarantine and target-side SQS DLQ boundary.
- [x] Create proposal.md, spec.md, design.md and this task checklist before implementation.

## Implementation — next increment

- [ ] Add Application publisher/dispatcher abstractions and validated options.
- [ ] Add outbox lease, retry, quarantine and acknowledgement domain behavior.
- [ ] Add MySQL claim/acknowledgement persistence using `FOR UPDATE SKIP LOCKED` and conditional lease-token updates.
- [ ] Add the EventBridge AWS SDK publisher; preserve the full schema-v1 envelope and inspect every result entry.
- [ ] Add a BackgroundService hosted by Identity API; disabled by default and cancellation-safe.
- [ ] Add checked-in EF migration and model snapshot updates for lease/retry/quarantine fields and dispatch index.
- [ ] Add unit tests for configuration, event mapping, partial failures, retry/backoff, error classification and logging redaction.
- [ ] Add MySQL 8.4 Testcontainers tests for concurrent claims, lease expiry, stale lease tokens, success acknowledgement, retry and quarantine.
- [ ] Add crash-after-acceptance replay coverage proving duplicate eventId behavior.
- [ ] Run restore, Release build, Compose validation and complete tests in CI; record exact workflow/run and results.
- [ ] Update operational documentation and project checkpoint with the verified code commit.

## Deployment enablement — blocked on infrastructure

- [ ] Provision a regional EventBridge custom bus and record its name/ARN in deployment configuration.
- [ ] Attach an Identity workload role with `events:PutEvents` scoped to that bus ARN.
- [ ] Provision EventBridge rules and target queues; configure SQS DLQs and queue policies for target-delivery failures.
- [ ] Provision CloudWatch metrics/alarms for pending age, quarantined rows and systemic publisher failures.
- [ ] Verify a real event is accepted by the bus and target delivery/DLQ behavior works in a non-production AWS environment.
- [ ] Enable `OutboxDispatcher__Enabled` only after the above checks.

## Explicitly out of scope

- Employee-status projection consumers in Habit, Challenge, Event Tracking, Insight and Notification.
- Global JWT revocation and exactly-once delivery.
- Employee invitations, HRIS integration and role/profile mutation.
