# Tasks: HR-Authorized Employee Lifecycle Commands

## SDD and policy reconciliation

- [x] Reconcile live `feature/backend-foundation` HEAD, recent commits, repository tree and prior checkpoint.
- [x] Read `AGENTS.md`, README, project status, open questions, Identity specification, ADR-0002, service ownership, communication conventions and event/API contracts.
- [x] Define the HR / Organization Admin field-ownership boundary without granting Organization Admin direct employee writes.
- [x] Separate the lifecycle command scope from employee invitation/provisioning, role-catalogue and HRIS decisions.
- [x] Define schema-v1 lifecycle events, idempotency, concurrency, audit/outbox atomicity and downstream projection contract.
- [x] Create proposal, specification, design and task checklist before implementation.

## Implementation (next focused increment)

- [ ] Add `EmployeeLifecycleVersion` to Employee and an explicit status-transition domain method.
- [ ] Add HrAdmin-only lifecycle command endpoints; actor ID must come from validated JWT `sub`.
- [ ] Add Application command/result abstractions and Infrastructure transaction implementation.
- [ ] Add immutable lifecycle audit and transactional outbox entities/mappings.
- [ ] Add checked-in EF Core migration and update model snapshot.
- [ ] Implement idempotency fingerprint and unique actor/key guard.
- [ ] Implement optimistic concurrency and safe conflict mapping.
- [ ] Add unit tests for validation, fingerprint determinism and event schema serialization.
- [ ] Add MySQL integration tests for both transitions, atomic rollback, no-op, replay, conflicting key reuse and concurrent requests.
- [ ] Add API integration tests for anonymous, Employee, OrganizationAdmin and HrAdmin authorization and actor attribution.
- [ ] Run restore, Release build, Compose validation and full Testcontainers suite in GitHub Actions; record exact run and results.
- [ ] Update project status with changed files, verification evidence and resulting code commit SHA.

## Explicitly deferred

- [ ] Employee invitations/provisioning and credential activation.
- [ ] Organization Admin request-intake/approval workflow.
- [ ] Role catalogue and elevated-role assignment policy.
- [ ] HRIS service authentication and feed reconciliation.
- [ ] EventBridge dispatcher, retries, DLQ and delivery monitoring.
- [ ] Employee-status projection consumers in the five receiving services.
