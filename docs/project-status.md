# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: feature/backend-foundation.
- Starting live HEAD for this increment: cd93bdc24b760907765a48132578540ad4bb565f.
- Previous verified increment: Identity health probes, CI run #31 (44675962776), 38 passed.
- Current policy decision: ADR-0002, docs/decisions/0002-product-policy-baseline.md; initial nine open questions are resolved with the project owner's instruction to apply recommended MVP answers.
- Current implementation change: docs/changes/identity-inactive-login-guard/.
- Latest implementation commit: 83259537f2e27caa1509de5c874a154d98bea038.
- Latest documentation commit before this checkpoint: ccd608efcdf2fd620a17ec75ce255ea776b9d4fa.
- Resume rule: fetch live branch HEAD, inspect recent commits/tree and read this checkpoint before choosing work. Continue the single next task; do not recreate completed policy or login-guard work.

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
- [x] CI run #31 (44675962776): restore, Release build (0 warnings/errors), Compose validation and 38 tests passed.
- [x] ADR-0002 accepted: all nine initial product open questions resolved with recommended MVP defaults.
- [x] Updated docs/open-questions.md, README.md and ADR-0002 with HR provisioning, Identity/HRIS ownership, normalized challenge scoring, cohort threshold 10, evidence policy, habit reuse, manager privacy scope, retention defaults and HR-controlled account lifecycle.
- [x] IA-09 login guard implemented: password is verified for an existing employee, but inactive status returns the same generic 401 before password rehash, persistence or token issuance.
- [x] Added unit test asserting generic 401, password verification performed, no rehash, no save and no token for inactive employee.
- [x] Added MySQL Testcontainers integration test that marks a persisted employee inactive, attempts login, verifies generic 401 and confirms password hash remains unchanged.
- [x] Updated Identity specification, API contract and Identity foundation checklist.

## In progress
- [ ] Verify the GitHub Actions run triggered by the latest code commit 83259537f2e27caa1509de5c874a154d98bea038; correct any compile/test failures and record final evidence.

## Blockers / decisions
- No unresolved product questions remain for MVP design.
- Retention defaults (90-day evidence, 12-month identifiable activity/participation, 24-month security/admin audit, up to 5-year anonymized aggregates) require jurisdiction-specific privacy/legal sign-off before production.
- HRIS integration, HR provisioning/lifecycle commands, lifecycle audit persistence, transactional outbox and downstream employee-status projections remain future implementation work, not completed capabilities.
- The current increment intentionally does not add activation/deactivation APIs or lifecycle audit schema.
- This automation environment has no .NET SDK or Docker CLI; local restore/build/test/Compose execution is unavailable. GitHub Actions is the Docker-capable verification environment.

## Verification evidence
Previously verified baseline:
- CI run #31: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44675962776
- dotnet restore backend/TeamPulse.sln: passed.
- dotnet build backend/TeamPulse.sln --configuration Release --no-restore: passed; 0 warnings, 0 errors.
- docker compose config --quiet: passed.
- dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx": 38 passed, 0 failed, 0 skipped.

Current increment:
- CI is triggered by code changes on feature/backend-foundation; docs-only pushes are ignored by workflow paths-ignore.
- Code commits include 2e0b875b09a7e08f05c187418bc163004669c9a1 (final controller flow), 1031fa830ce26496b2b4d8f98c0ad5f87c306eb3 (unit test) and 83259537f2e27caa1509de5c874a154d98bea038 (MySQL integration test).
- No successful CI result has yet been confirmed for these commits. Do not claim current build/tests passed.
- Local command capability remains unavailable: dotnet and docker executables are not installed.

## Changed files in this increment
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
- docs/project-status.md

## Last run record
- Confirmed branch feature/backend-foundation exists and reconciled the live checkpoint. The live branch matched commit cd93bdc24b760907765a48132578540ad4bb565f at the start (4 commits ahead of the earlier checkpoint base, 0 behind).
- Read AGENTS.md, README.md, docs/project-status.md, docs/open-questions.md, service ownership, communication conventions, Identity spec/contract, ADR-0001, health probe SDD, Identity controller, Employee entity, repository, unit tests, MySQL Testcontainers fixture/integration tests, Program.cs and CI workflow.
- Created ADR-0002 and SDD proposal/spec/design/tasks before implementation.
- Resolved initial questions 1-9 with recommended MVP decisions and recorded retention legal review as a production gate.
- Implemented the inactive login check. Password verification still runs for an existing inactive account; after a successful or rehash-needed verification the status check returns generic 401 before any hash upgrade, DB write or token generation. This avoids a fast inactive-account path while keeping account status private.
- Added unit and MySQL integration coverage; test sets IsActive false directly in the test database without introducing a production lifecycle API.
- Updated Identity spec, contract, README and task checklists.
- Confirmed branch remained at ccd608efcdf2fd620a17ec75ce255ea776b9d4fa before the checkpoint update (compare status identical, ahead 0 / behind 0).
- Implementation is committed; current CI verification is pending.

## Single next task
Verify the GitHub Actions workflow triggered by the latest code commit 83259537f2e27caa1509de5c874a154d98bea038. If it fails, inspect job logs, fix the exact failure on feature/backend-foundation and rerun. If it succeeds, record exact restore/build/Compose/test results and select HR provisioning/lifecycle workflow as the next SDD increment.
