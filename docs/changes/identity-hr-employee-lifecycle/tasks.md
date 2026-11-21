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

## Additional header-validation regression coverage — CI #233 passed

- Added a MySQL API-host integration test for whitespace-only and overlength idempotency keys, repeated Idempotency-Key values, and repeated X-Correlation-ID values.
- Rejected requests are checked for unchanged employee status/version, no audit rows, and no outbox rows for the supplied correlation IDs.
- Added acceptance criterion ILC-14 to the lifecycle specification; the test passed in GitHub Actions CI #233.
- CI #233 passed against code commit `d7dc8b8663e28e25c9beec395acd0ae48c628185`: Identity 103, Habit 24, Challenge 57, Event Tracking 27, Notification 28, Insight 15 and Insight.QueueMigration.IntegrationTests 8 — **262 passed, 0 failed, 0 skipped**.
- [CI #233](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44900792522). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44900792522/artifacts/14706141699).


## Idempotency-key character boundary regression — CI #235 passed

- Added API-host integration coverage for accepted one-character and 128-character visible-ASCII keys and rejected ASCII control, DEL and non-ASCII values.
- Rejected requests return HTTP 400 without changing employee status/version or creating lifecycle audit/outbox records; accepted keys complete the expected transition.
- CI #235 passed against code commit `0525db1487d96e35b92552cb586cb8fc508ebb70`: Identity 104, Habit 24, Challenge 57, Event Tracking 27, Notification 28, Insight 15 and Insight.QueueMigration.IntegrationTests 8 — **263 passed, 0 failed, 0 skipped**.
- [CI #235](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44901319799). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44901319799/artifacts/14705773167).

## JWT subject-claim validation regression — implementation added

- Added an API-host integration test using correctly signed HrAdmin tokens with either no `sub` claim or a non-GUID `sub`.
- Both requests must return 401 before lifecycle mutation; the test verifies employee status/version, audit and correlation-scoped outbox remain unchanged.
- Implementation commit: `d0b20c32c1c1ec7b7af8b138c571730d8f7be10a`. CI verification pending.
