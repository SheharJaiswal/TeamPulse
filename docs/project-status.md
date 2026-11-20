# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: feature/backend-foundation.
- Previous checkpoint: bcbf39e0e6bc19253867ba70019acfe2bc254a74.
- Current live HEAD before this checkpoint update: 9553839ffdb4bb75620c0e1376befa1e7127deb9.
- Latest code commit covered by CI: 1031fa830ce26496b2b4d8f98c0ad5f87c306eb3.
- Product policy baseline: ADR-0002; all nine initial open questions are resolved using the project owner's instruction to apply recommended MVP defaults.
- Completed SDD increment: docs/changes/identity-inactive-login-guard/.
- Next SDD increment: docs/changes/identity-hr-admin-bootstrap/.
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
- [x] Created SDD proposal/spec/design/tasks for a one-time, operator-invoked HR administrator bootstrap utility.

## In progress
- [ ] Implement docs/changes/identity-hr-admin-bootstrap/ as the next focused SDD increment.

## Blockers / decisions
- No unresolved product questions remain for MVP design.
- Retention defaults (90-day evidence, 12-month identifiable activity/participation, 24-month security/admin audit, up to 5-year anonymized aggregates) require jurisdiction-specific privacy/legal sign-off before production.
- HRIS integration, HR provisioning/lifecycle commands, lifecycle audit persistence, transactional outbox and downstream employee-status projections remain future implementation work, not completed capabilities.
- The HR administrator bootstrap utility is not implemented yet; no production HR admin provisioning path currently exists.
- This automation environment has no .NET SDK or Docker CLI; local restore/build/test/Compose execution is unavailable. GitHub Actions is the Docker-capable verification environment.

## Verification evidence

### Latest code increment
- GitHub Actions run #36: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44678135548
- Head commit: 1031fa830ce26496b2b4d8f98c0ad5f87c306eb3.
- Run conclusion: success; job "Restore, build and test" completed successfully.
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: passed; 0 warnings, 0 errors.
- `docker compose config --quiet`: passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: 40 passed, 0 failed, 0 skipped.
- MySQL 8.4 Testcontainers integration tests ran as part of the test step.
- Test artifact: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44678135548/artifacts/14602534207.
- The later commits in this checkpoint are documentation-only; the workflow ignores docs-only changes.

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

## Latest run record
- Reconciled live branch feature/backend-foundation at bcbf39e0e6bc19253867ba70019acfe2bc254a74 and read AGENTS.md, README.md, project status, open questions, Identity spec/contract, service ownership, communication conventions, ADR-0002, Identity controller/entity/repository/DbContext, unit/integration tests and CI workflow.
- Inspected GitHub Actions run #36 and its job logs. Restore, Release build, Compose validation and all 40 tests passed on code commit 1031fa830ce26496b2b4d8f98c0ad5f87c306eb3.
- Confirmed the unit test verifies the supplied password exactly once for an inactive employee and asserts no rehash, persistence or token issuance.
- Corrected stale design/task/spec wording that incorrectly described zero password-verification calls.
- Created the HR administrator bootstrap SDD artifacts. They define a dedicated operator-invoked console utility, secret-backed password configuration, a one-time guard, no HTTP bootstrap route, and database-level protection against concurrent creation.
- Latest branch HEAD before this checkpoint update: 9553839ffdb4bb75620c0e1376befa1e7127deb9.

## Single next task
Implement the HR administrator bootstrap SDD change. First inspect project/solution conventions and Testcontainers fixture, then implement the console project with secret-backed configuration and a database-level one-time guard. Add unit and MySQL integration/concurrency tests, run the full GitHub Actions workflow, and update this checkpoint with exact evidence. Keep all changes on feature/backend-foundation; do not modify main.
