# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: feature/backend-foundation.
- Previous checkpoint: 8277b642608651f517f1ff7479fa7aa842c60a6d.
- Current live HEAD before this checkpoint update: c71fe14349537eebc288330eefbd7e7fdbf13663.
- Latest code commit covered by CI: 71e2208442ec0224726185f8cae1a15da9767135.
- Latest completed documentation increment: c71fe14349537eebc288330eefbd7e7fdbf13663.
- Product policy baseline: ADR-0002; all nine initial open questions are resolved using the project owner's instruction to apply recommended MVP defaults.
- Completed SDD increment: docs/changes/identity-inactive-login-guard/.
- Completed SDD increment: docs/changes/identity-hr-admin-bootstrap/.
- Completed SDD increment: docs/changes/identity-hr-employee-lifecycle/ (approved contract and design; implementation pending).
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
- [x] Added unit test asserting generic 401, one password verification, no hash generation/rehash, no save and no token.
- [x] Added MySQL Testcontainers integration test for inactive employee login and unchanged stored password hash.
- [x] Updated Identity specification, API contract and Identity foundation checklist.
- [x] Corrected inactive-login SDD test strategy/checklist to match the implemented password-verification-first flow.
- [x] Added Identity.AdminBootstrap console project to backend/TeamPulse.sln.
- [x] Bootstrap utility reads credentials only from protected environment configuration, requires explicit --execute, hashes the password, refuses any existing HrAdmin (active or inactive), refuses duplicate email and serializes concurrent calls with a MySQL advisory lock and transaction.
- [x] Added unit and MySQL integration tests for configuration validation, password hashing, existing admin, duplicate email and concurrent invocation.
- [x] Added docs/runbooks/identity-hr-admin-bootstrap.md.

## In progress
- [ ] Implement the approved HR-only employee lifecycle command slice: JWT role authorization, idempotent activate/deactivate transitions, lifecycle version, atomic audit and outbox persistence, EF migration, and unit/MySQL integration tests.

## Blockers / decisions
- The nine initial MVP product questions remain resolved by ADR-0002. Additional implementation decisions discovered during the lifecycle SDD are tracked in docs/open-questions.md.
- Retention defaults (90-day evidence, 12-month identifiable activity/participation, 24-month security/admin audit, up to 5-year anonymized aggregates) require jurisdiction-specific privacy/legal sign-off before production.
- HR lifecycle commands, audit/outbox persistence and downstream employee-status projections are specified but not implemented. The outbox is a durable hand-off only; no EventBridge delivery is claimed.
- The bootstrap utility is implemented but has not been executed against a production database; no claim is made that a production HR administrator account exists.
- Employee invitation/provisioning is deferred pending invitation delivery, token expiry, initial credential activation and password setup decisions. The bootstrap utility creates only the first HrAdmin.
- Organization Admins have no direct employee profile/lifecycle write access. Their HR request-intake workflow is not yet defined. HR role catalogue/elevated-role assignment and HRIS service authentication are also open.
- EventBridge bus name, dispatcher lease/retry/DLQ and delivery monitoring are not configured; the current SDD increment intentionally stops at transactional outbox persistence. Receiving-service projections and global revocation remain future work.
- Retention defaults (90-day evidence, 12-month identifiable activity/participation, 24-month security/admin audit, up to 5-year anonymized aggregates) still require jurisdiction-specific privacy/legal sign-off before production.
- This automation environment has no .NET SDK or Docker CLI; local restore/build/test/Compose execution is unavailable. GitHub Actions is the Docker-capable verification environment.

## Verification evidence

### Latest code increment
- GitHub Actions run #51: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44680155186
- Head commit: 71e2208442ec0224726185f8cae1a15da9767135.
- Run conclusion: success; restore, Release build, Docker Compose validation and full test step all completed successfully.
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: passed; 0 warnings, 0 errors.
- `docker compose config --quiet`: passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: 49 passed, 0 failed, 0 skipped.
- MySQL 8.4 Testcontainers integration tests, including concurrent bootstrap invocations, ran successfully.
- Test artifact: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44680155186/artifacts/14603449182.
- The initial implementation run #48 passed all 49 tests but exposed three warnings; run #51 is the clean verification after aligning EF Core package versions and correcting xUnit analyzer usage.

### Current documentation-only increment
- SDD commit: c71fe14349537eebc288330eefbd7e7fdbf13663.
- Added proposal/spec/design/tasks for HR-authorized lifecycle commands; updated Identity specification, API contract, event envelope/catalog and open questions; added the downstream employee-status projection contract.
- No source code or migrations changed in this increment. The GitHub Actions workflow ignores docs/**, so no CI run was triggered. No build, test or Compose command was executed in this environment.

### Prior verified baseline
- CI run #31: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44675962776.
- Restore, Release build, Compose validation and 38 tests passed.

## Changed files in the inactive-login increment
- docs/decisions/0002-product-policy-baseline.md
- docs/open-questions.md
- README.md
- docs/changes/identity-inactive-login-guard/{proposal,spec,design,tasks}.md
- backend/src/Services/Identity/Identity.API/Controllers/IdentityController.cs
- backend/tests/Identity.UnitTests/IdentityControllerTests.cs
- backend/tests/Identity.UnitTests/IdentityApiIntegrationTests.cs
- docs/specs/identity-service.md
- docs/contracts/identity-api.md
- docs/changes/identity-service-foundation/tasks.md

## Changed files in the HR administrator bootstrap increment
- backend/src/Tools/Identity.AdminBootstrap/{Identity.AdminBootstrap.csproj,Program.cs,BootstrapOptions.cs,HrAdminBootstrapper.cs}
- backend/tests/Identity.UnitTests/{BootstrapOptionsTests.cs,HrAdminBootstrapperIntegrationTests.cs,Identity.UnitTests.csproj}
- backend/TeamPulse.sln
- docs/changes/identity-hr-admin-bootstrap/{proposal,spec,design,tasks}.md
- docs/runbooks/identity-hr-admin-bootstrap.md

## Changed files in the HR lifecycle SDD increment
- docs/changes/identity-hr-employee-lifecycle/{proposal,spec,design,tasks}.md
- docs/contracts/employee-status-projection.md
- docs/contracts/identity-api.md
- docs/contracts/event-envelope.md
- docs/events/initial-event-catalog.md
- docs/specs/identity-service.md
- docs/open-questions.md

## Latest run record
- Reconciled the live branch at 8277b642608651f517f1ff7479fa7aa842c60a6d, inspected recent commits and the recursive repository tree, and read AGENTS.md, README.md, project status, open questions, ADR-0002, Identity service specification, service ownership, communication conventions, API/event contracts, Identity persistence/security code and existing tests.
- Confirmed the HR administrator bootstrap SDD and implementation are complete; CI run #51 passed 49 tests with a clean Release build and Compose validation.
- Created the HR lifecycle SDD before changing any source code. The approved implementation scope is the HR-only status transition command plus atomic audit/outbox persistence in Identity.
- Documented field ownership: HR owns canonical employee identity fields; Organization Admins cannot directly write employee profile or lifecycle state and may only request HR changes through a workflow that remains undefined.
- Added event schema v1 and a monotonic lifecycle-version projection contract to protect consumers against duplicate/out-of-order delivery.
- Explicitly deferred employee invitation/provisioning, role catalogue/elevation, HRIS authentication, Organization Admin request intake, EventBridge dispatch and downstream consumer implementation because their required product/deployment decisions or service code are not yet available.
- SDD files committed as c71fe14349537eebc288330eefbd7e7fdbf13663. This was a documentation-only increment; no CI run was triggered and no new checks are claimed.

## Single next task
Implement the approved HR-only lifecycle command slice on feature/backend-foundation. Add role-protected deactivate/reactivate endpoints, validated actor attribution, idempotency and concurrency guards, lifecycle version, immutable audit and transactional outbox records in one EF Core transaction, plus a checked-in migration. Add unit, API authorization and MySQL Testcontainers tests; run GitHub Actions and record exact results. Do not implement employee invitations, role/profile mutation, Organization Admin request intake, HRIS or EventBridge dispatch in this slice. Keep main untouched.
