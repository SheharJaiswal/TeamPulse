# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: feature/backend-foundation.
- Live branch HEAD before the final checkpoint update: 43a3fb81a849d7c25c60418982081846a81c3f10.
- Current implementation commit: 68a68211f21dc5e0883e7bd6fc664001c9953bc7.
- Final implementation CI: [run #28](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44663321579), conclusion success.
- Current SDD change: docs/changes/identity-registration-environment-guard/ (completed and CI-verified).
- Resume rule: fetch live branch HEAD, recent commits and tree first; inspect this checkpoint and relevant specs/contracts; continue the single next task without repeating completed work.

## Completed
- [x] Exactly six service boundaries and service-owned data documented.
- [x] Identity Clean Architecture, registration/login/me, password hashing and JWT implementation.
- [x] MySQL 8.4 / EF Core 8 / Pomelo persistence and checked-in startup EF migration.
- [x] Identity unit, controller and MySQL Testcontainers integration test foundations.
- [x] .NET 8 GitHub Actions restore/build/Compose/test workflow.
- [x] Removed obsolete PostgreSQL bootstrap artifact.
- [x] IA-08: fail-fast JWT signing-key validation; no committed signing-key fallback.
- [x] IA-01 through IA-08 and IA-DB-01/02 were verified by CI run #27.
- [x] IA-10 / REG-01 through REG-06: bootstrap registration is allowed only in Development; all other environments return 404 before validation or repository access.
- [x] Unit tests cover Production, Staging and Testing with zero repository mutations.
- [x] MySQL-backed integration tests cover Production and Testing and assert that no employee row is persisted; existing Development registration tests remain successful.
- [x] Identity specification, API contract, README, foundation task checklist and dedicated SDD change updated.

## In progress
- [ ] No implementation task is currently in progress. The registration environment guard is complete and verified.

## Blockers
- IA-09 remains blocked by unresolved employee lifecycle requirements in docs/open-questions.md question 9. Do not implement inactive-account enforcement until those rules are approved.
- This automation environment has no .NET SDK or Docker CLI; local executable validation is unavailable. GitHub Actions provides the Docker-capable .NET 8 runner.
- No blocker remains for the current Identity registration environment guard.

## Verification evidence
GitHub Actions run #28: [44663321579](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44663321579), implementation commit 68a68211f21dc5e0883e7bd6fc664001c9953bc7, conclusion success:
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: passed; 0 warnings, 0 errors.
- `docker compose config --quiet`: passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: passed 33, failed 0, skipped 0.
- The suite includes new controller unit cases for Production/Staging/Testing and real API + MySQL Testcontainers cases for Production/Testing, while existing Development registration integration coverage remains enabled.
Previous verified baseline: run #27 (44655706057) passed 28 tests.

Local capability check in this automation environment:
- `command -v dotnet`: no executable found.
- `command -v docker`: no executable found.
- `command -v git`: /usr/bin/git.
No local build/test is claimed; the successful checks above were executed by GitHub Actions.

## Changed files in the completed increment
- backend/src/Services/Identity/Identity.API/Controllers/IdentityController.cs
- backend/tests/Identity.UnitTests/IdentityControllerTests.cs
- backend/tests/Identity.UnitTests/IdentityApiFactory.cs
- backend/tests/Identity.UnitTests/MySqlIntegrationFixture.cs
- backend/tests/Identity.UnitTests/IdentityApiIntegrationTests.cs
- docs/specs/identity-service.md
- docs/contracts/identity-api.md
- docs/changes/identity-registration-environment-guard/{proposal,spec,design,tasks}.md
- docs/changes/identity-service-foundation/tasks.md
- README.md
- docs/project-status.md

## Last run record
- Starting live HEAD: 40e6a37b92b1def5cce6ddf988f576a0a363e194.
- Inspected the live ref, latest 10 commits, recursive repository tree, CI history, required docs, Identity specification/contract, service ownership, communication conventions, ADR-0001, Identity controller, Program, unit/integration tests, factories and workflow.
- Confirmed and implemented the previous checkpoint's single next task: Development-only bootstrap registration.
- Created implementation commit 68a68211f21dc5e0883e7bd6fc664001c9953bc7 after checking that the branch still pointed to the starting HEAD.
- Verified CI run #28 completed successfully: restore, Release build (0 warnings/errors), Docker Compose validation, 33 passed / 0 failed / 0 skipped.
- The MySQL provider, Compose database, checked-in EF migration and startup Database.MigrateAsync() behavior were unchanged.
- Checkpoint commit before this update: 43a3fb81a849d7c25c60418982081846a81c3f10.

## Single next task
Create an SDD change to handle ASP.NET Core PasswordHasher returning PasswordVerificationResult.SuccessRehashNeeded during login: rehash the successfully verified password using the currently configured hasher and persist the upgraded hash before issuing the token. Add deterministic unit tests for rehash and no-rehash paths, preserve generic login failures, verify persistence behavior, then run the complete .NET 8 CI workflow. Do not implement IA-09 until the employee lifecycle requirements are approved.
