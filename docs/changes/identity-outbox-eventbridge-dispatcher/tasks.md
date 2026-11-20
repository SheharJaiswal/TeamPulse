# Tasks: Lease-Safe Identity Outbox Dispatcher

## Reconciliation and SDD

- [x] Fetch the live `feature/backend-foundation` branch and inspect recent commits and repository tree.
- [x] Reconcile the live branch with `docs/project-status.md`; confirm HR lifecycle implementation is complete and CI run #80 is the latest verified code result.
- [x] Read AGENTS.md, README.md, Identity specification, ownership, communication conventions, ADR-0001/0002, event envelope/catalog, lifecycle SDD, open questions and current outbox persistence.
- [x] Confirm the repository has no deployment/IaC tree, EventBridge bus definition, workload IAM role, EventBridge rules/targets, SQS queue/DLQ definitions or monitoring resources.
- [x] Define configuration-gated EventBridge publishing, AWS credential/IAM boundary and event mapping.
- [x] Define lease-safe MySQL claim/acknowledgement, partial batch results, retry/backoff, producer-side quarantine and target-side SQS DLQ boundary.
- [x] Create proposal.md, spec.md, design.md and this task checklist before implementation.

## Implementation — core complete; acceptance-test hardening remains

- [x] Add Application publisher/dispatcher abstractions and validated options.
- [x] Add outbox lease, retry, quarantine and acknowledgement domain behavior.
- [x] Add MySQL claim/acknowledgement persistence using `FOR UPDATE SKIP LOCKED` and conditional lease-token updates.
- [x] Add the EventBridge AWS SDK publisher; preserve the full schema-v1 envelope and inspect every result entry.
- [x] Add a BackgroundService hosted by Identity API; disabled by default and cancellation-safe.
- [x] Add checked-in EF migration and model snapshot updates for lease/retry/quarantine fields and dispatch index.
- [x] Add tests for event mapping, partial failures, lease domain transitions, concurrent claims, lease expiry, stale tokens, acknowledgement, quarantine and queue metrics.
- [ ] Add focused tests for options validation, retry/backoff boundaries, systemic versus permanent error classification and payload-free log behavior.
- [ ] Add retry scheduling and ambiguous publish-after-acceptance replay tests proving stable eventId behavior.
- [x] Run restore, Release build, Compose validation and full tests in CI: run #80 passed 64 tests, 0 failed/skipped, 0 warnings/errors.
- [x] Update the project checkpoint with the verified code commit and deployment blockers.

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
