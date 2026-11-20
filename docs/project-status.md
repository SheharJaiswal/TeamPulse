# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: feature/backend-foundation.
- Starting live HEAD for this increment: 0dcff844249bfe9fc3e602f400969e230b4df6ef.
- SDD pre-implementation commit: 98a318888df0214dae7a9d7e762579950384b81e.
- Initial health-probe implementation commit: 272869801926a1396b95f3b41994fd2adf1c9525 (CI run #30 failed at build; it is superseded by the corrective commit below).
- Corrected implementation commit: 3782940507dee022a611cbe34f92d3a689400bfb.
- CI run #31: [44675962776](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44675962776), conclusion success.
- Current SDD change: docs/changes/identity-health-probes/ (implemented and CI-verified).
- Resume rule: fetch the live branch HEAD, recent commits and recursive tree first; reconcile this file with the live source before selecting work. Never repeat completed tasks.

## Completed
- [x] Exactly six service boundaries and data ownership documented.
- [x] Identity Clean Architecture, registration/login/me, password hashing and JWT implementation.
- [x] MySQL 8.4 / EF Core 8 / Pomelo persistence and checked-in startup EF migration.
- [x] Identity unit, controller and MySQL Testcontainers integration test foundations.
- [x] .NET 8 GitHub Actions restore/build/Compose/test workflow.
- [x] Removed obsolete PostgreSQL bootstrap artifact.
- [x] IA-08: fail-fast JWT signing-key validation; no committed signing-key fallback.
- [x] IA-10: bootstrap registration is Development-only, returning 404 without persistence in other environments.
- [x] IA-11 / PH-01 through PH-07: successful legacy password verification upgrades the stored hash before token issuance; normal hashes are not rewritten; invalid credentials remain generic 401; persistence failure prevents token issuance.
- [x] HP-01 through HP-07: separated process liveness and database readiness endpoints; preserved /health as a readiness compatibility alias; readiness probe is bounded to three seconds and health responses do not expose provider details.
- [x] Added MySQL Testcontainers integration test proving /health/live stays HTTP 200 while MySQL is stopped after startup, whereas /health/ready and /health return HTTP 503; database is restarted in finally.
- [x] Updated README, Identity API contract and SDD artifacts for health probe semantics.
- [x] CI run #31 (44675962776): restore passed; Release build passed with 0 warnings and 0 errors; Docker Compose validation passed; 38 tests passed, 0 failed, 0 skipped (38-second test duration).
- [x] Test result artifact: [teampulse-test-results, artifact 14601438633](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44675962776/artifacts/14601438633).

## In progress
- [ ] No implementation task is currently in progress. Health probe separation is complete and verified.

## Blockers
- Employee account lifecycle policy remains unresolved in docs/open-questions.md question 9. This blocks implementing account activation/deactivation commands, authorities and audit/reason requirements. It does not block a defensive login check against an already-inactive Employee.IsActive record.
- This automation container has no .NET SDK or Docker CLI; local restore/build/test/Compose execution is unavailable. The Docker-capable GitHub Actions runner is the verification environment.
- No blocker remains for the health probe increment.

## Verification evidence
Local capability check:
- command -v dotnet: no executable found.
- dotnet --info: command not found.
- command -v docker: no executable found.
- docker --version: command not found.
- command -v git: /usr/bin/git.
No local build/test result is claimed.

First CI attempt:
- Run #30: [44675833320](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44675833320), conclusion failure at Release build.
- Errors: AddDbContextCheck in this .NET 8 package version has no timeout named argument; HealthCheckOptions is in Microsoft.AspNetCore.Diagnostics.HealthChecks.
- Corrected in commit 3782940507dee022a611cbe34f92d3a689400bfb using a scoped IdentityDatabaseHealthCheck and HealthCheckRegistration timeout.

Successful CI:
- Run #31: [44675962776](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44675962776), commit 3782940507dee022a611cbe34f92d3a689400bfb, conclusion success.
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: passed; 0 warnings, 0 errors.
- `docker compose config --quiet`: passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: passed 38, failed 0, skipped 0; test duration 38 seconds.
- Uploaded test result artifact ID: 14601438633.

## Changed files in the health probe increment
- backend/src/Services/Identity/Identity.API/Program.cs
- backend/src/Services/Identity/Identity.API/HealthChecks/IdentityDatabaseHealthCheck.cs
- backend/tests/Identity.UnitTests/MySqlIntegrationFixture.cs
- backend/tests/Identity.UnitTests/IdentityHealthProbeIntegrationTests.cs
- README.md
- docs/contracts/identity-api.md
- docs/changes/identity-health-probes/{proposal,spec,design,tasks}.md
- docs/project-status.md

## Last run record
- Fetched the live feature/backend-foundation branch HEAD; starting SHA was 0dcff844249bfe9fc3e602f400969e230b4df6ef. Read the live branch commit history (15 recent commits) and recursive Git tree (116 entries; not truncated).
- Read AGENTS.md, README.md, docs/project-status.md, docs/open-questions.md, service ownership, communication conventions, Identity spec and contract, ADR-0001, relevant SDD artifacts, Program.cs, API controller, repository, Employee entity, MySQL fixture, migration tests, controller tests, test project and CI workflow.
- Reconciled the checkpoint: password-hash upgrade was complete; IA-09 lifecycle mutation policy remained unresolved. Chose an independent operational-readiness improvement rather than inventing lifecycle rules.
- Created docs/changes/identity-health-probes/{proposal,spec,design,tasks}.md before implementation.
- First implementation commit 272869801926a1396b95f3b41994fd2adf1c9525 triggered CI run #30 and failed with three compile errors. The exact errors were read from job logs and corrected; the failed commit was not marked complete.
- Corrective commit 3782940507dee022a611cbe34f92d3a689400bfb fixed the HealthCheckOptions namespace and replaced the unsupported AddDbContextCheck timeout argument with a dedicated scoped health check registered with a three-second HealthCheckRegistration timeout.
- Before the corrective branch update, fetched the branch and confirmed it still matched the expected previous HEAD 272869801926a1396b95f3b41994fd2adf1c9525. Updated using an expected-SHA guard with force=false.
- CI run #31 completed successfully: restore, Release build (0 warnings/errors), Compose validation and 38 tests passed. Test logs confirm 38 passed, 0 failed, 0 skipped.
- MySQL provider, schema, checked-in migration and startup Database.MigrateAsync() behavior were unchanged.
- Corrected implementation commit: 3782940507dee022a611cbe34f92d3a689400bfb.

## Single next task
Add a fail-closed login guard for employees whose existing `IsActive` flag is false: return the same generic 401 as other invalid credentials, do not rehash the password and do not issue a token. Add unit and MySQL integration tests. Do not add activation/deactivation APIs, choose lifecycle authorities, or invent audit/reason rules; those remain blocked by open question 9.
