# Tasks: Identity EventBridge Fan-out Infrastructure

## Reconciliation and SDD

- [x] Fetch the live `feature/backend-foundation` branch, recent commits and recursive tree.
- [x] Reconcile `docs/project-status.md`; dispatcher core and acceptance hardening are complete, while AWS fan-out infrastructure is the next increment.
- [x] Read AGENTS.md, README.md, project status, open questions, service ownership, ADRs, communication conventions, Identity lifecycle event contracts and dispatcher SDD.
- [x] Confirm no prior `infra/` or `deploy/` tree and no bus, target queues, DLQs, IAM attachment or deployment alarms existed.
- [x] Create proposal.md, spec.md, design.md and this task checklist before infrastructure implementation.

## CloudFormation and validation

- [x] Add a parameterized custom EventBridge bus and existing-role least-privilege PutEvents attachment.
- [x] Add the schema-v1 Identity lifecycle rule and five existing-service SQS projection targets.
- [x] Add a distinct target-delivery DLQ and exact rule/account queue policy for each service.
- [x] Add SSE-SQS, configurable queue/target retry retention and queue resource retention.
- [x] Add target queue age, DLQ depth and EventBridge DLQ-write failure alarms with optional SNS actions.
- [x] Keep consumer receive permissions and consumer-processing DLQs outside the shared stack.
- [x] Document deployment parameters, rollout gates, message body and observability limitations.
- [x] Add a dedicated GitHub Actions cfn-lint and Python contract-test workflow.
- [x] Pair outer detail type and inner event type with EventBridge `$or` branches to reject mismatched lifecycle envelopes.
- [x] Add Python contract tests for exact queue/DLQ counts, event pairing, IAM/queue policy scope, SSE-SQS and alarm coverage.
- [ ] Confirm cfn-lint and Python contract tests pass in GitHub Actions.

## Live deployment — blocked

- [ ] Supply a non-production AWS account/region and authorized provisioning role.
- [ ] Supply the existing Identity workload role name and approved operations SNS topic ARN (or explicitly omit notification actions for non-production).
- [ ] Review and deploy a CloudFormation change set; verify the role policy is scoped to the bus.
- [ ] Deploy consumer-owned receive permissions and idempotent projections in Habit, Challenge, Event Tracking, Insight and Notification.
- [ ] Verify one non-sensitive event reaches all five queues and a forced target failure reaches only its matching DLQ.
- [ ] Verify CloudWatch alarms and notification routing.
- [ ] Keep `OutboxDispatcher__Enabled=false` until non-production verification and consumer readiness are complete.

## Explicitly deferred

- Producer-side outbox oldest-age/quarantine/systemic-failure alarms; the Identity Meter has no CloudWatch exporter yet.
- Consumer-processing DLQs, cross-account routing, archive/replay and global token revocation.
