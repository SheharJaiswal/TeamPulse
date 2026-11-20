# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: `feature/backend-foundation`.
- Branch HEAD at the start of this automation run: `4f4c5f5a7790e742ae23f899ea7883f177ffca2c`.
- IA-08 implementation commits: `877cb49ebdc378e0eef627c1cfa7fa0d8668e170`, `0eee18d741770940b9f4a7dac0fb1a52109f5401`, `a83fb6464f51b1989a9a53d36e0308ff0cbb33fe`.
- Current SDD change: `docs/changes/identity-jwt-startup-validation/` (completed and CI-verified).
- PostgreSQL cleanup CI run #24: [44648716325](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44648716325) succeeded.
- IA-08 CI run #27: [44655706057](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44655706057) succeeded for implementation commit `a83fb6464f51b1989a9a53d36e0308ff0cbb33fe`.
- This checkpoint update is committed as a child of the previous checkpoint; the next run must fetch the live branch ref.
- Resume rule: fetch live branch HEAD and recent commits, inspect this file and relevant specs/contracts, then continue the single next task. Never repeat completed work.

## Completed
- [x] SDD workflow and exactly six service ownership boundaries remain documented and unchanged.
- [x] Identity Clean Architecture foundation, registration/login/`/me`, password hashing and JWT implementation retained.
- [x] MySQL 8.4 / EF Core 8 / Pomelo persistence and checked-in startup migration retained.
- [x] Unit, controller and MySQL Testcontainers integration tests cover password hashing, JWT claims/signature, registration/login, duplicate email, migrations/restart, persistence and authenticated `/me`.
- [x] .NET 8 GitHub Actions CI covers restore, Release build, Docker Compose validation and Testcontainers-backed tests.
- [x] Removed the orphaned PostgreSQL bootstrap SQL file; active MySQL configuration was unchanged.
- [x] Confirmed PostgreSQL cleanup CI run #24 succeeded.
- [x] IA-08 implemented: missing, blank, whitespace-only or fewer-than-32-UTF-8-byte signing keys fail before database configuration/migration.
- [x] Extracted and tested `JwtSigningKeyValidator`, including exactly-32-byte and 31-byte UTF-8 boundary cases.
- [x] Removed the committed JWT signing-key placeholder from `appsettings.json`; local/deployed environments must supply `Jwt__SigningKey` externally.
- [x] Updated README local setup instructions and Identity design/specification documentation to prohibit committing signing keys.
- [x] Corrected two CI-discovered issues: ineffective test-host configuration isolation (run #25) and missing System/nullable declarations in the validator (run #26).
- [x] Updated Identity acceptance checklist; IA-01 through IA-08 and IA-DB-01/02 are verified.

## In progress
- [ ] No code task is currently in progress. The next approved increment is development-only enforcement for the bootstrap registration endpoint.

## Verification evidence
GitHub Actions run #27: [44655706057](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44655706057), commit `a83fb6464f51b1989a9a53d36e0308ff0cbb33fe`, conclusion `success`:
- `dotnet restore backend/TeamPulse.sln`: succeeded.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: succeeded, 0 warnings, 0 errors.
- `docker compose config --quiet`: succeeded.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: passed 28, failed 0, skipped 0.
- The test suite includes startup tests for missing/blank/weak signing keys and UTF-8 byte boundaries, plus existing MySQL 8.4 Testcontainers integration tests.

Earlier corrected failures:
- Run #25 failed 5 new startup tests because the first test factory did not override appsettings as expected; the committed placeholder was subsequently removed and test configuration now follows the proven `UseSetting`/in-memory pattern.
- Run #26 failed at build with CS0246 (missing System import) and CS8632 (nullable annotations without nullable context); both were corrected before run #27.
- Cleanup run #24 and baseline run #7 also succeeded; baseline run #7 passed 21 tests.

Local execution limitation:
- This automation environment has Git but no .NET SDK or Docker CLI. `command -v dotnet` and `command -v docker` returned no executable; `command -v git` returned `/usr/bin/git`. Validation was executed by the Docker-capable GitHub Actions runner.

## Remaining / not yet implemented
- [ ] Enforce development-only access to `POST /api/identity/register`. The controller comment, README, Identity specification and API contract all describe registration as a development bootstrap, but the current action is still mapped without an environment guard.
- [ ] IA-09: inactive employee authentication denial, pending approved account lifecycle requirements.
- [ ] Identity security hardening after requirements are approved: HR provisioning, production secret rotation/operational controls, refresh-token policy and audit requirements.
- [ ] Implement Habit, Challenge, Event Tracking, Insight and Notification services within the six-service architecture.
- [ ] Implement EventBridge/SQS, transactional outbox, idempotent consumers and DLQs.
- [ ] Implement frontend and AWS deployment.

## Blockers
- IA-09 is blocked by unresolved employee lifecycle rules. `docs/open-questions.md` question 9 now records the required decision about deactivation/reactivation authority, reasons and audit requirements.
- This automation environment has no .NET SDK or Docker CLI; continue using the GitHub Actions runner for executable validation.
- No blocker remains for IA-08 or the MySQL 8.4 / EF Core 8 / Pomelo baseline.

## Last run record
- Closed SDD change: `docs/changes/identity-jwt-startup-validation/`.
- Implementation commits: `877cb49ebdc378e0eef627c1cfa7fa0d8668e170`, `0eee18d741770940b9f4a7dac0fb1a52109f5401`, `a83fb6464f51b1989a9a53d36e0308ff0cbb33fe`.
- Final verification: run #27 passed restore, Release build (0 warnings/errors), Docker Compose validation and all 28 tests.
- Updated files: Identity startup validator/Program/appsettings, startup-validation test factory/tests, README, Identity service spec and foundation design/spec/tasks, JWT startup-validation SDD artifacts, open questions and project checkpoint.
- The MySQL Compose service, Pomelo provider, checked-in EF migration and `Database.MigrateAsync()` startup migration behavior were not changed.

## Single next task
Create an SDD change and enforce the existing requirement that `POST /api/identity/register` is development-only. Add tests proving registration succeeds in Development and is unavailable outside Development, without changing login, `/me`, MySQL persistence or the API contract. Do not implement IA-09 until account lifecycle requirements are approved.
