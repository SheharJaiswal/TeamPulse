# Project Status
Updated: 2026-11-20

## Continuation checkpoint

- Active branch: `feature/backend-foundation` only.
- Live branch HEAD at the start of this run: `6a85445f71692b436c8289c1de24cfbf964e4429`.
- Latest verified code commit: `3f0d8bfb9b99fa758cfa3d187a39099809ebf81f` (GitHub Actions run #72).
- Documentation commits after that verified code commit were reconciled from live Git history: `6a3e3e845493051cb34667b904a6e5940778bdaf` and `6a85445f71692b436c8289c1de24cfbf964e4429`.
- Current SDD increment: `docs/changes/identity-outbox-eventbridge-dispatcher/`; SDD is complete and committed in `3f6f3b9041acb9d681de9c993e626d79c55b1e6b`.
- Product policy baseline: ADR-0002; all nine initial product open questions are resolved using the project owner's instruction to apply recommended MVP defaults.
- Completed SDD increments include inactive-login guard, HR administrator bootstrap, HR employee lifecycle and the new Identity outbox/EventBridge dispatcher design.
- Resume rule: fetch the live branch HEAD, inspect recent commits and repository tree, read this checkpoint and relevant specs/contracts, then continue the single next task. Do not recreate completed work.

## Completed

- [x] Exactly six service boundaries and ownership documented; no cross-service database access.
- [x] Identity Clean Architecture, registration/login/me, password hashing and JWT implementation.
- [x] MySQL 8.4 / EF Core 8 / Pomelo persistence and checked-in startup EF migration.
- [x] Identity unit, controller and MySQL Testcontainers integration test foundations.
- [x] .NET 8 GitHub Actions restore/build/Compose/test workflow.
- [x] Removed obsolete PostgreSQL bootstrap artifact.
- [x] IA-08: fail-fast JWT signing-key validation; no committed signing-key fallback.
- [x] IA-10: bootstrap registration is Development-only, returning 404 without persistence in other environments.
- [x] IA-11: legacy password hashes upgrade before token issuance; normal hashes are not rewritten; persistence failure prevents token issuance.
- [x] HP-01 through HP-07: separate liveness/readiness endpoints; /health compatibility alias; bounded three-second DB probe; no sensitive health details.
- [x] Health integration test proves liveness remains healthy while MySQL is stopped and readiness returns 503.
- [x] CI run #31 (44675962776): 38 tests passed, 0 failed, 0 skipped; Release build had 0 warnings/errors; Compose validation passed.
- [x] ADR-0002 accepted: all nine initial product open questions resolved with recommended MVP defaults.
- [x] IA-09 inactive login guard: password verification runs for an existing employee; inactive status returns the same generic 401 before hash rehash, persistence or token issuance.
- [x] Added unit and MySQL integration coverage for inactive employee login and unchanged password hash.
- [x] Added Identity.AdminBootstrap console project to backend/TeamPulse.sln. It reads credentials only from protected environment configuration, requires explicit --execute, hashes the password, refuses any existing HrAdmin (active or inactive), refuses duplicate email and serializes concurrent calls with a MySQL advisory lock and transaction.
- [x] Added unit and MySQL integration tests for HR administrator bootstrap. CI run #51 passed 49 tests with 0 warnings and 0 errors.
- [x] Added HR lifecycle commands: HrAdmin-only deactivate/reactivate API, actor attribution from validated JWT sub, reason/note/correlation validation and idempotency-key handling.
- [x] Added Employee.LifecycleVersion and domain transition method; MySQL row locking plus EF optimistic concurrency reject stale commands.
- [x] Added immutable employee lifecycle audit and transactional outbox entities; employee, audit and outbox writes commit in one transaction.
- [x] Added SHA-256 canonical request fingerprint, unique actor/idempotency-key index, replay handling and conflicting-key 409 behavior.
- [x] Added checked-in migration 20261120060000_EmployeeLifecycleOutbox and updated IdentityDbContext model snapshot.
- [x] Added unit and MySQL Testcontainers coverage for role authorization, actor spoof prevention, both transition directions, no-op, same-key concurrent retry, conflicting key reuse, event envelope, rollback atomicity and invalid headers.
- [x] Updated Identity API contract, Identity service specification and lifecycle SDD task checklists.
- [x] Created outbox dispatcher SDD proposal/spec/design/tasks before implementation.
- [x] Defined EventBridge mapping: source `teampulse.identity`, detail type = eventType, time = occurredAtUtc, detail = complete existing envelope, bus name from deployment configuration.
- [x] Defined AWS SDK default credential chain, workload-role least privilege, configuration-gated worker, MySQL SKIP LOCKED leases, per-entry acknowledgement, retry/quarantine, producer-vs-target DLQ boundary and monitoring requirements.
- [x] Updated communication conventions, event envelope contract and open questions to reflect the dispatcher contract and deployment prerequisite.

## In progress

- [ ] Implement the Identity outbox dispatcher according to `docs/changes/identity-outbox-eventbridge-dispatcher/`. The current branch contains the SDD only; no dispatcher code, lease migration, AWS SDK integration or worker tests have been added yet.

## Blockers and decisions

- **Deployment enablement blocker:** The repository has no deployment/IaC tree (the inspected root contains .github, backend, docs and docker-compose.yml; `deploy/` and `infra/` do not exist). No EventBridge bus, Identity workload IAM role, EventBridge rules/targets, SQS queues/DLQs or CloudWatch alarms are defined. The dispatcher may be implemented and tested with a mocked EventBridge client while disabled, but `OutboxDispatcher__Enabled` must remain false in deployed environments until those resources are provisioned and reviewed.
- Bus name is intentionally deployment-supplied through `EventBridge__BusName`; region uses `AWS_REGION`; the fixed source is `teampulse.identity`. No account-specific ARN or credential is to be invented or committed.
- The bootstrap utility has not been executed against a production database; no claim is made that a production HR administrator account exists.
- Employee invitation/provisioning and credential activation remain deferred pending invitation delivery, token expiry and password setup decisions.
- Organization Admins have no direct employee profile/lifecycle write access. Their HR request-intake workflow is not defined. HR role catalogue/elevated-role assignment and HRIS service authentication are also deferred.
- Downstream employee-status projections and global revocation remain future work. Existing stateless JWTs may remain usable by receiving services until expiry.
- Retention defaults (90-day evidence, 12-month identifiable activity/participation, 24-month security/admin audit, up to 5-year anonymized aggregates) require jurisdiction-specific privacy/legal sign-off before production.
- This execution environment has no .NET SDK or Docker CLI. Local restore/build/test/Compose validation cannot be performed here; GitHub Actions is the verification environment.
- No unresolved initial MVP product questions remain. Dispatcher deployment resources are an infrastructure prerequisite, not a reason to hard-code a bus or credentials in application code.

## Verification evidence

### Latest verified code

- GitHub Actions run #72: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44684723718
- Verified code commit: `3f0d8bfb9b99fa758cfa3d187a39099809ebf81f`.
- Run conclusion: success.
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: passed; 0 warnings, 0 errors.
- `docker compose config --quiet`: passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: 56 passed, 0 failed, 0 skipped.
- MySQL 8.4 Testcontainers integration tests passed, including lifecycle authorization, concurrency/idempotency and rollback atomicity.
- Test artifact: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44684723718/artifacts/14605692876.
- Earlier run #70 failed build due missing explicit threading imports in the Infrastructure project; fixed in commit 7e607b351efb6b1fa1010bbc9d80615856f7247d.
- Run #71 passed build but exposed reversed arguments in the concurrent replay test; corrected in 3f0d8bfb9b99fa758cfa3d187a39099809ebf81f. Run #72 is the clean verification for the final lifecycle code.

### This run — documentation-only SDD increment

- Reconciled the live branch at `6a85445f71692b436c8289c1de24cfbf964e4429`, including recent commit history and repository tree. The existing checkpoint's last code commit `3f0d8bfb...` was correct; two subsequent docs commits were present in live history.
- Read AGENTS.md, README.md, docs/project-status.md, docs/open-questions.md, ADR-0001/0002, service ownership, communication conventions, Identity specification, event catalog/envelope, lifecycle SDD and current outbox entity/context/migration/tests.
- Confirmed no `deploy/` or `infra/` directory and no bus, IAM, EventBridge target, SQS DLQ or CloudWatch definitions.
- Created the dispatcher SDD and updated communication/event contracts and open questions.
- `dotnet --info`: unavailable, exit 127 (`dotnet: command not found`).
- `docker --version`: unavailable, exit 127 (`docker: command not found`).
- `git --version`: succeeded, 2.47.3.
- `dotnet restore backend/TeamPulse.sln`, `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`, `dotnet test backend/TeamPulse.sln --configuration Release --no-build` and `docker compose config --quiet`: not run because their required .NET/Docker executables are absent.
- No new CI run was triggered or claimed: the workflow ignores `docs/**` and README-only changes. The previous run #72 remains the latest code verification.
- SDD commit: `3f6f3b9041acb9d681de9c993e626d79c55b1e6b`.

## Changed files in this run

- docs/changes/identity-outbox-eventbridge-dispatcher/proposal.md
- docs/changes/identity-outbox-eventbridge-dispatcher/spec.md
- docs/changes/identity-outbox-eventbridge-dispatcher/design.md
- docs/changes/identity-outbox-eventbridge-dispatcher/tasks.md
- docs/architecture/communication.md
- docs/contracts/event-envelope.md
- docs/open-questions.md
- docs/project-status.md

## Single next task

Implement the Identity outbox dispatcher core described by the new SDD: add application abstractions and validated options; extend OutboxMessage and add a checked-in EF migration for lease/retry/quarantine metadata; implement MySQL `FOR UPDATE SKIP LOCKED` claims and lease-token-conditional acknowledgement; add an EventBridge publisher using the AWS SDK with per-entry partial-failure handling; host a BackgroundService disabled by default; and add unit plus MySQL 8.4 Testcontainers tests for concurrent claims, expiry, retries, quarantine and crash-after-acceptance replay. Use fake/mock AWS clients in CI; do not require live AWS credentials. Keep the worker disabled until deployment resources are provisioned.
