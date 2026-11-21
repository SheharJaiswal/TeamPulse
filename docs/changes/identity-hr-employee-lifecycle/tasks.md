# Tasks: HR-Authorized Employee Lifecycle Commands

## SDD and policy reconciliation

- [x] Reconcile live `feature/backend-foundation` HEAD, recent commits, repository tree and prior checkpoint.
- [x] Read `AGENTS.md`, README, project status, open questions, Identity specification, ADR-0002, service ownership, communication conventions and API/event contracts.
- [x] Define the HR / Organization Admin field-ownership boundary without granting Organization Admin direct employee writes.
- [x] Separate the lifecycle command scope from employee invitation/provisioning, role-catalogue and HRIS decisions.
- [x] Define schema-v1 lifecycle events, idempotency, concurrency, audit/outbox atomicity and downstream projection contract.
- [x] Create proposal, specification, design and task checklist before implementation.

## Implementation

- [x] Add `LifecycleVersion` to Employee and an explicit status-transition domain method.
- [x] Add HrAdmin-only deactivate/reactivate endpoints; actor ID is taken only from validated JWT `sub`.
- [x] Add Application command/result abstractions and Infrastructure transaction implementation.
- [x] Add immutable lifecycle audit and transactional outbox entities/mappings.
- [x] Add checked-in EF Core migration `20261120060000_EmployeeLifecycleOutbox` and update model snapshot.
- [x] Implement SHA-256 request fingerprint and unique actor/idempotency-key guard.
- [x] Implement row-lock stale-command detection plus EF optimistic concurrency and safe conflict mapping.
- [x] Add unit tests for lifecycle version transitions.
- [x] Add MySQL integration tests for deactivate/reactivate, atomic rollback, no-op, replay, conflicting key reuse and concurrent retries.
- [x] Add API integration tests for anonymous, Employee, OrganizationAdmin and HrAdmin authorization, JWT actor attribution and request validation.
- [x] Add MySQL API-host tests for blank/overlength reason codes, overlength notes, no-mutation guarantees on 400 responses, and accepted maximum-length values with reason-code trimming.
- [x] Run restore, Release build, Compose validation and full Testcontainers suite in GitHub Actions run #72.
- [x] Record exact CI evidence: restore passed; Release build had 0 warnings/0 errors; Compose validation passed; 56 tests passed, 0 failed, 0 skipped.
- [x] Update Identity API/service contracts and project checkpoint with resulting code commit SHA.

## Explicitly deferred

- [ ] Employee invitations/provisioning and credential activation.
- [ ] Organization Admin request-intake/approval workflow.
- [ ] Role catalogue and elevated-role assignment policy.
- [ ] HRIS service authentication and feed reconciliation.
- [ ] EventBridge outbox dispatcher, retries, DLQ and delivery monitoring.
- [ ] Employee-status projection consumers in the five receiving services.


## Additional request-validation regression coverage — CI #231 passed

- Added integration tests for blank and overlength reason codes, overlength notes, and no employee/audit/outbox mutation on rejected requests.
- Added a successful maximum-boundary case for a 100-character reason code and 1,000-character note, including reason-code trimming.
- The tests use an explicit correlation ID to isolate the outbox assertion from unrelated rows in the shared MySQL test database.
- GitHub Actions CI #231 passed against code commit `55c192484534a54dd2aad62ccd2f1c0d26ab3774`; the full .NET suite passed: Identity 102, Habit 24, Challenge 57, Event Tracking 27, Notification 28, Insight 15 and Insight.QueueMigration.IntegrationTests 8 — **261 passed, 0 failed, 0 skipped**.
- [CI #231](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44900300510).

## Additional header-validation regression coverage — CI pending

- Added a MySQL API-host integration test for whitespace-only and overlength idempotency keys, repeated Idempotency-Key values, and repeated X-Correlation-ID values.
- Rejected requests are checked for unchanged employee status/version, no audit rows, and no outbox rows for the supplied correlation IDs.
- Added acceptance criterion ILC-14 to the lifecycle specification. GitHub Actions verification is pending.
