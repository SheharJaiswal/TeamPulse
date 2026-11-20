# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: feature/backend-foundation.
- Starting live HEAD for this increment: 3206d8fddb1b5735959ba059fad9c56c4f2273b7.
- Starting parent: 43a3fb81a849d7c25c60418982081846a81c3f10.
- Current increment: docs/changes/identity-password-hash-upgrade/.
- Implementation commit: pending branch update and CI.
- Resume rule: always fetch the live branch ref, recent commits and recursive tree first; reconcile this file with the live source before selecting work. Never repeat completed tasks.

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
- [x] Previous Identity tests cover password hashing, JWT claims/signature, controller validation, duplicate email, MySQL persistence/migrations and registration environment guard.
- [x] This increment implements IA-11: successful legacy hash verification upgrades the stored hash before token issuance.

## In progress
- [ ] Await CI execution for the password-hash upgrade increment. The implementation and tests are committed; final verification is not yet recorded.

## Blockers
- IA-09 inactive-employee enforcement remains blocked by unresolved account lifecycle authority, deactivation/reactivation events and audit/reason requirements in docs/open-questions.md question 9. Do not invent these rules.
- This automation container has no .NET SDK or Docker CLI. Local restore/build/test/Compose execution is unavailable; use the Docker-capable GitHub Actions runner.
- No known implementation blocker for IA-11.

## Verification
Local capability check:
- command -v dotnet: not found.
- command -v docker: not found.
- command -v git: /usr/bin/git.
No local build/test result is claimed.

Previous verified baseline:
- GitHub Actions run #28 (44663321579), conclusion success.
- dotnet restore backend/TeamPulse.sln: passed.
- dotnet build backend/TeamPulse.sln --configuration Release --no-restore: passed, 0 warnings, 0 errors.
- docker compose config --quiet: passed.
- dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx": 33 passed, 0 failed, 0 skipped.

## Changed files in current increment
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
- Fetched live branch feature/backend-foundation: 3206d8fddb1b5735959ba059fad9c56c4f2273b7.
- Inspected 15 recent commits and recursive tree (111 entries, not truncated).
- Read AGENTS.md, README.md, this checkpoint, docs/open-questions.md, Identity specification and API contract, service ownership, communication conventions, ADR-0001, registration SDD artifacts, Identity controller, repository abstraction/implementation, Employee entity, unit tests, MySQL integration fixture/tests, test project and CI workflow.
- Reconciled live branch: the previous response's claimed future password-hash increment was not present in the repository; the live checkpoint explicitly identified it as the single next task.
- Added deterministic unit tests for SuccessRehashNeeded, Success, generic failure and persistence failure; added real MySQL integration coverage for IdentityV2 legacy hash upgrade.
- MySQL schema, Pomelo provider, checked-in EF migration and startup Database.MigrateAsync() remain unchanged.
- Exact current-increment CI results and resulting commit SHA will be recorded after the branch update and workflow completion.

## Single next task
Resolve employee account lifecycle policy in open question 9 (who can deactivate/reactivate, which events trigger state changes, and required audit/reason data). Do not implement IA-09 until those requirements are approved. After policy approval, create an SDD change and tests before modifying login behavior.
