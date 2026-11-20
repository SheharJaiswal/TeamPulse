# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: feature/backend-foundation.
- Previous verified code checkpoint: 71e2208442ec0224726185f8cae1a15da9767135.
- Current live code HEAD before this documentation checkpoint: 3f0d8bfb9b99fa758cfa3d187a39099809ebf81f.
- Latest code commit covered by CI: 3f0d8bfb9b99fa758cfa3d187a39099809ebf81f.
- Product policy baseline: ADR-0002; all nine initial open questions are resolved using the project owner's instruction to apply recommended MVP defaults.
- Completed SDD increment: docs/changes/identity-inactive-login-guard/.
- Completed SDD increment: docs/changes/identity-hr-admin-bootstrap/.
- Completed SDD increment: docs/changes/identity-hr-employee-lifecycle/.
- Resume rule: fetch live branch HEAD, inspect recent commits/tree and read this checkpoint before choosing work. Continue the single next task; do not recreate completed work.

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
- [x] Updated Identity API contract, Identity service specification and SDD task checklists.

## In progress
- [ ] Create the next SDD change for the Identity transactional outbox dispatcher and EventBridge delivery boundary. Define bus/environment configuration, publisher abstraction, lease-safe claiming, at-least-once retries, DLQ, IAM, monitoring and safe published-state acknowledgement before implementation.

## Blockers / decisions
- No unresolved initial MVP product questions remain.
- Retention defaults (90-day evidence, 12-month identifiable activity/participation, 24-month security/admin audit, up to 5-year anonymized aggregates) require jurisdiction-specific privacy/legal sign-off before production.
- The outbox currently persists lifecycle events but does not dispatch them. No EventBridge delivery is claimed.
- The bootstrap utility has not been executed against a production database; no claim is made that a production HR administrator account exists.
- Employee invitation/provisioning and credential activation remain deferred pending invitation delivery, token expiry and password setup decisions.
- Organization Admins have no direct employee profile/lifecycle write access. Their HR request-intake workflow is not defined. HR role catalogue/elevated-role assignment and HRIS service authentication are also deferred.
- EventBridge bus name, dispatcher lease/retry/DLQ and delivery monitoring are not configured; receiving-service projections and global revocation remain future work.
- This environment has no .NET SDK or Docker CLI, so local restore/build/test/Compose execution is unavailable. GitHub Actions is the verification environment.

## Verification evidence

### Latest lifecycle implementation
- GitHub Actions run #72: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44684723718
- Head commit: 3f0d8bfb9b99fa758cfa3d187a39099809ebf81f.
- Run conclusion: success.
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: passed; 0 warnings, 0 errors.
- `docker compose config --quiet`: passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: 56 passed, 0 failed, 0 skipped.
- MySQL 8.4 Testcontainers integration tests passed, including lifecycle authorization, concurrency/idempotency and rollback atomicity.
- Test artifact: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44684723718/artifacts/14605692876.
- Earlier run #70 failed build due missing explicit threading imports in the Infrastructure project; fixed in commit 7e607b351efb6b1fa1010bbc9d80615856f7247d.
- Run #71 passed build but exposed reversed arguments in the concurrent replay test; corrected in 3f0d8bfb9b99fa758cfa3d187a39099809ebf81f. Run #72 is the clean verification for the final code.

### Prior verified baseline
- CI run #51: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44680155186.
- Restore, Release build (0 warnings/0 errors), Compose validation and 49 tests passed for the HR administrator bootstrap increment.
- CI run #31: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44675962776.
- Restore, Release build, Compose validation and 38 tests passed.

## Changed files in the HR lifecycle implementation
- backend/src/Services/Identity/Identity.Domain/Entities/Employee.cs
- backend/src/Services/Identity/Identity.Domain/Entities/EmployeeLifecycleAudit.cs
- backend/src/Services/Identity/Identity.Domain/Entities/OutboxMessage.cs
- backend/src/Services/Identity/Identity.Application/Contracts/EmployeeLifecycleContracts.cs
- backend/src/Services/Identity/Identity.Application/Abstractions/IEmployeeLifecycleService.cs
- backend/src/Services/Identity/Identity.API/Controllers/EmployeeLifecycleController.cs
- backend/src/Services/Identity/Identity.API/Program.cs
- backend/src/Services/Identity/Identity.Infrastructure/Persistence/EmployeeLifecycleService.cs
- backend/src/Services/Identity/Identity.Infrastructure/Persistence/IdentityDbContext.cs
- backend/src/Services/Identity/Identity.Infrastructure/Persistence/Migrations/20261120060000_EmployeeLifecycleOutbox.cs
- backend/src/Services/Identity/Identity.Infrastructure/Persistence/Migrations/IdentityDbContextModelSnapshot.cs
- backend/tests/Identity.UnitTests/EmployeeLifecycleDomainTests.cs
- backend/tests/Identity.UnitTests/EmployeeLifecycleApiIntegrationTests.cs
- docs/changes/identity-hr-employee-lifecycle/{proposal,spec,design,tasks}.md
- docs/contracts/identity-api.md
- docs/specs/identity-service.md

## Latest run record
- Reconciled live branch at 6e0e4eef6a65ade6e1383f1d13f938443bb56c35, read AGENTS.md, project status, open questions, Identity SDD, API/event/projection contracts, ownership and policy decisions, and inspected current Identity API/domain/EF/test infrastructure.
- Confirmed the HR lifecycle SDD was already complete; proceeded with its single specified implementation slice.
- Added lifecycle versioning, HR-only endpoints, actor attribution from JWT sub, idempotency fingerprint, MySQL row lock and optimistic concurrency, immutable audit, schema-v1 event envelope and transactional outbox persistence.
- Added migration and MySQL-backed integration coverage.
- Run #70 failed build because the Infrastructure project does not enable implicit usings; added explicit System.Threading and System.Threading.Tasks imports.
- Run #71 exposed a test argument-order error in the concurrent idempotency scenario; corrected the test so both requests use the same idempotency key and semantically identical normalized reason code.
- Run #72 succeeded on code commit 3f0d8bfb9b99fa758cfa3d187a39099809ebf81f: restore passed; Release build 0 warnings/0 errors; Compose validation passed; 56 tests passed, 0 failed, 0 skipped.
- Documentation-only checkpoint updates follow the verified code commit. The CI workflow ignores docs/**; no additional CI run is expected for those documentation updates.

## Single next task
Create an SDD proposal/spec/design/tasks for a lease-safe Identity outbox dispatcher that publishes to the approved EventBridge transport. Reconcile the communication ADR and deployment/IaC tree first. Resolve bus naming/configuration, publisher credentials/IAM, event detail envelope, claim/lease concurrency, retry/backoff, poison-message/DLQ handling, monitoring, and the exact point at which PublishedAtUtc is written. Keep delivery at-least-once and idempotent; never mark published before EventBridge acknowledges acceptance. Do not implement downstream service projections in this increment. Keep all work on feature/backend-foundation; do not modify main.
