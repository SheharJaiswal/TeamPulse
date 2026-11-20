# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: feature/backend-foundation.
- Starting live HEAD for this increment: 3206d8fddb1b5735959ba059fad9c56c4f2273b7.
- Current implementation commit: 2c0551cb6b988e9d5d5970bce3a6abc6123fd118.
- Current SDD change: docs/changes/identity-password-hash-upgrade/ (implemented and CI-verified).
- CI run #29: [44670262475](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44670262475), conclusion success.
- Resume rule: fetch the live branch ref, recent commits and recursive tree first; reconcile this file with the live source before selecting work. Never repeat completed tasks.

## Completed
- [x] Six service boundaries and ownership documented.
- [x] Identity Clean Architecture, registration/login/me, password hashing and JWT implementation.
- [x] MySQL 8.4 / EF Core 8 / Pomelo persistence and checked-in startup EF migration.
- [x] Identity unit, controller and MySQL Testcontainers integration test foundations.
- [x] .NET 8 GitHub Actions restore/build/Compose/test workflow.
- [x] Removed obsolete PostgreSQL bootstrap artifact.
- [x] IA-08: fail-fast JWT signing-key validation; no committed signing-key fallback.
- [x] IA-10: bootstrap registration is Development-only, returning 404 without persistence in all other environments.
- [x] CI run #28 (44663321579): restore, Release build (0 warnings/errors), Docker Compose validation and 33 tests passed.
- [x] IA-11 / PH-01 through PH-07: successful legacy password verification upgrades the stored hash using the configured hasher before token issuance; normal hashes are not rewritten; invalid credentials remain generic 401; persistence failure prevents token issuance.
- [x] Added deterministic unit tests for SuccessRehashNeeded, Success, failed verification and failed persistence.
- [x] Added MySQL-backed integration test that seeds an IdentityV2 legacy hash, logs in through the API and verifies the persisted hash using the current hasher.
- [x] Updated Identity specification, API contract, foundation task checklist and dedicated SDD artifacts.
- [x] CI run #29 (44670262475): restore, Release build (0 warnings/errors), Docker Compose validation and 37 tests passed.

## In progress
- [ ] No implementation task is currently in progress. The password-hash upgrade is complete and verified.

## Blockers
- IA-09 inactive-employee enforcement remains blocked by unresolved account lifecycle authority, deactivation/reactivation events and audit/reason requirements in docs/open-questions.md question 9. Do not invent these rules or implement IA-09 until approved.
- This automation container has no .NET SDK or Docker CLI. Local restore/build/test/Compose execution is unavailable; the Docker-capable GitHub Actions runner was used.
- No blocker remains for the password-hash upgrade increment.

## Verification evidence
Local capability check:
- command -v dotnet: no executable found.
- command -v docker: no executable found.
- command -v git: /usr/bin/git.
- dotnet --info: command not found.
- docker --version: command not found.
No local build/test result is claimed.

GitHub Actions run #29: [44670262475](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44670262475), commit 2c0551cb6b988e9d5d5970bce3a6abc6123fd118, conclusion success:
- dotnet restore backend/TeamPulse.sln: passed.
- dotnet build backend/TeamPulse.sln --configuration Release --no-restore: passed; 0 warnings, 0 errors.
- docker compose config --quiet: passed.
- dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx": passed 37, failed 0, skipped 0; duration 28 seconds.
- Test results artifact: teampulse-test-results, artifact ID 14599354058.
- Job log confirms all four commands completed successfully and the test result summary above.

Previous verified baseline:
- Run #28 (44663321579): 33 passed, 0 failed, 0 skipped.

## Changed files in the password-hash upgrade increment
- backend/src/Services/Identity/Identity.API/Controllers/IdentityController.cs
- backend/src/Services/Identity/Identity.Domain/Entities/Employee.cs
- backend/tests/Identity.UnitTests/IdentityControllerTests.cs
- backend/tests/Identity.UnitTests/IdentityApiIntegrationTests.cs
- docs/specs/identity-service.md
- docs/contracts/identity-api.md
- docs/changes/identity-service-foundation/tasks.md
- docs/changes/identity-password-hash-upgrade/{proposal,spec,design,tasks}.md
- docs/project-status.md

## Last run record
- Fetched the live feature/backend-foundation ref; starting HEAD was 3206d8fddb1b5735959ba059fad9c56c4f2273b7 (parent 43a3fb81a849d7c25c60418982081846a81c3f10).
- Inspected 15 recent commits and the recursive tree (111 entries, not truncated).
- Read AGENTS.md, README.md, docs/open-questions.md, this checkpoint, Identity specification and contract, service ownership, communication conventions, ADR-0001, registration SDD artifacts, Identity controller, Program, repository abstraction/implementation, Employee entity, unit tests, integration fixture/tests, test project and CI workflow.
- Reconciled live branch with the checkpoint: password-hash upgrade was the single next task; no implementation files for it existed yet.
- Created SDD proposal/spec/design/tasks before implementation.
- Implemented hash replacement only for SuccessRehashNeeded. The tracked employee is updated and SaveChangesAsync is awaited before CreateResponse generates the JWT. Unknown verification enum values fail closed.
- Added unit tests for rehash ordering, no rewrite on Success, no write on Failed, and no token on persistence failure.
- Added MySQL Testcontainers integration coverage using a legacy IdentityV2 hash.
- Before branch update, fetched the branch again and confirmed it still matched the starting HEAD 3206d8fddb1b5735959ba059fad9c56c4f2273b7. Updated the branch using an expected-SHA guard; no force push.
- Implementation commit: 2c0551cb6b988e9d5d5970bce3a6abc6123fd118.
- Verified resulting branch HEAD after update: 2c0551cb6b988e9d5d5970bce3a6abc6123fd118.
- CI run #29 completed successfully with 37 passed, 0 failed, 0 skipped; Release build had 0 warnings/errors; Compose validation passed.
- MySQL provider, schema, checked-in migration and startup Database.MigrateAsync() behavior were unchanged.

## Single next task
Resolve employee account lifecycle policy in open question 9: define which events can deactivate/reactivate an employee, which authority may perform each transition, and required audit/reason data. Do not implement IA-09 until this policy is approved. Once approved, create an SDD change and tests before modifying authentication behavior.
