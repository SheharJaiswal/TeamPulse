# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: feature/backend-foundation.
- Previous checkpoint: 436f4ad2e28f9fec6f2a566c2c5611fddb9d7e5f.
- Current live HEAD before this checkpoint update: 71e2208442ec0224726185f8cae1a15da9767135.
- Latest code commit covered by CI: 71e2208442ec0224726185f8cae1a15da9767135.
- Product policy baseline: ADR-0002; all nine initial open questions are resolved using the project owner's instruction to apply recommended MVP defaults.
- Completed SDD increment: docs/changes/identity-inactive-login-guard/.
- Completed SDD increment: docs/changes/identity-hr-admin-bootstrap/.
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
- [ ] Create the next SDD change for HR-authorized employee provisioning and lifecycle commands, including transition audit and transactional outbox semantics.

## Blockers / decisions
- No unresolved product questions remain for MVP design.
- Retention defaults (90-day evidence, 12-month identifiable activity/participation, 24-month security/admin audit, up to 5-year anonymized aggregates) require jurisdiction-specific privacy/legal sign-off before production.
- HRIS integration, HR provisioning/lifecycle commands, lifecycle audit persistence, transactional outbox publication and downstream employee-status projections remain future implementation work, not completed capabilities.
- The bootstrap utility is implemented but has not been executed against a production database; no claim is made that a production HR administrator account exists.
- The bootstrap utility does not implement employee invitations, password reset/change, HTTP provisioning endpoints, HRIS integration or lifecycle audit/outbox publication.
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

## Latest run record
- Reconciled live branch at 436f4ad2e28f9fec6f2a566c2c5611fddb9d7e5f and read the engineering instructions, status, Identity specs/contracts, project/solution structure, persistence model and Testcontainers fixture.
- Created the HR administrator bootstrap SDD before implementation.
- Implemented a dedicated console utility; no new microservice or public HTTP endpoint was added.
- Used a MySQL named advisory lock on the same open connection, then a ReadCommitted transaction, to serialize first-admin creation. Existing active or inactive HrAdmin records block another bootstrap. Duplicate normalized email is rejected.
- Added a runbook that requires protected environment injection, an already-migrated Identity database, explicit operator invocation and removal/rotation of the one-time secret.
- First CI run #48 completed with 49 passing tests but 3 warnings (EF Core Relational version conflict and two xUnit analyzer warnings). Fixed by aligning EF Core/Relational package references to 8.0.11, avoiding a record-generated password ToString, and using Count assertions in the concurrency test.
- Final CI run #51 completed successfully on 71e2208442ec0224726185f8cae1a15da9767135: restore passed; Release build had 0 warnings/0 errors; Compose validation passed; 49 tests passed, 0 failed, 0 skipped.
- The only subsequent commits in this checkpoint are documentation updates; the workflow ignores docs-only changes.

## Single next task
Create an SDD proposal/spec/design/tasks for HR-authorized employee provisioning and lifecycle commands. Define how the bootstrapped HrAdmin role is authorized, which fields can be changed by HR versus Organization Admin, atomic lifecycle audit persistence and outbox publication, event schema/versioning, idempotency and failure behavior, and the contract for downstream employee-status projections. Then implement only the approved scoped increment on feature/backend-foundation, add unit and MySQL integration tests, run GitHub Actions, and update this checkpoint with exact evidence. Keep main untouched.
