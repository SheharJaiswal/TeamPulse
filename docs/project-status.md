# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: `feature/backend-foundation`.
- Branch HEAD at the start of this automation run: `c22e5e2aa23e75c1febfd2127ae26be31a04cf36`.
- Branch HEAD before this checkpoint update: `ea9db58a8dfbc188fd8ca4198678328dd6322c70`; the branch lease was checked before the cleanup commit.
- Current SDD change: `docs/changes/remove-obsolete-postgres-bootstrap/`.
- Latest code/config validation commit: `dfd597720aaa6e52739487f9c7c8680f94f6eac9`.
- Successful baseline CI run: [#7 .NET 8 CI](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44641433504).
- PostgreSQL cleanup commit: `ea9db58a8dfbc188fd8ca4198678328dd6322c70`.
- Resume rule: every run must fetch the live branch HEAD and recent commits, inspect this file and the relevant specs/contracts, then continue the single next task. Never repeat completed work.

## Completed
- [x] SDD workflow and six service ownership boundaries remain documented and unchanged.
- [x] Identity Clean Architecture foundation, registration/login/`/me` endpoints, password hashing and JWT implementation retained.
- [x] MySQL 8.4 / EF Core 8 / Pomelo persistence and checked-in startup migration retained.
- [x] Added MySQL-backed integration tests using Testcontainers.MySql 3.10.0 and ASP.NET Core WebApplicationFactory.
- [x] Added isolated MySQL 8.4 fixtures and test-only host/JWT settings; tests use the real API startup, EF migrations, repository, password hasher and JWT bearer middleware.
- [x] Added integration coverage for first migration startup, repeated startup against an existing schema, data preservation, stored password hash, duplicate normalized email response, database unique index, anonymous /me denial and authenticated /me claims.
- [x] Split the migration-startup test into its own fixture/class so it always receives a fresh database rather than depending on test execution order.
- [x] Fixed the top-level Program declaration for WebApplicationFactory and added the explicit framework/configuration imports and configuration-binder dependency required by the actual .NET 8 build.
- [x] Corrected the JWT invalid-signature test to accept the derived signature-key exception.
- [x] Enabled nullable annotations in Identity Domain, Application and Infrastructure projects; the verified build now has zero warnings.
- [x] Added a .NET 8 GitHub Actions workflow with concurrency cancellation, docs-only path exclusions, restore/build, Docker Compose validation and the full Testcontainers-backed test suite.
- [x] Documented local test prerequisites in README.
- [x] Removed the orphaned PostgreSQL bootstrap SQL file; the active MySQL 8.4 Compose service, Pomelo provider, EF migration and startup migration path were left unchanged.
- [x] Updated Identity unit/controller/integration task lists and the Identity acceptance checklist. IA-01 through IA-07 and IA-DB-01/02 are verified. IA-08 and IA-09 remain unchecked.

## Verification evidence
Successful GitHub Actions run #7, commit `dfd597720aaa6e52739487f9c7c8680f94f6eac9`:
- `dotnet restore backend/TeamPulse.sln`: succeeded.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: succeeded, 0 warnings, 0 errors.
- `docker compose config --quiet`: succeeded.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: passed 21, failed 0, skipped 0 (including the real MySQL 8.4 Testcontainers integration tests).
- Run URL: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44641433504
- PostgreSQL artifact review: the recursive branch tree contained exactly one file under `infrastructure/docker/postgres/`; the SQL used PostgreSQL-only `CREATE USER` / `CREATE DATABASE ... OWNER` syntax and was not referenced by the active README, AGENTS, Compose file, Identity runtime configuration, API Dockerfile, CI workflow, service ownership, communication conventions, Identity specification or API contract.
- Post-deletion tree validation: no paths remain under `infrastructure/docker/postgres/`; no active PostgreSQL source/runtime/deployment configuration remains. The historical SDD record intentionally describes the removed artifact.

Local execution limitation:
- This automation container has no .NET SDK or Docker CLI. Local attempts of `dotnet restore backend/TeamPulse.sln`, `dotnet build backend/TeamPulse.sln --no-restore`, `dotnet test backend/TeamPulse.sln` and `docker compose config` each exited 127 because the executable was not installed. The Docker-capable GitHub Actions runner executed and passed the equivalent restore/build/test plus Compose validation.

## Remaining / not yet implemented
- [ ] IA-08: automated startup test proving a missing/weak JWT signing key fails startup.
- [ ] IA-09: inactive employee authentication denial, pending implementation of account lifecycle support.
- [ ] Identity security hardening after approved requirements: HR provisioning, secret rotation/injection, refresh-token policy and audit requirements.
- [ ] Implement Habit, Challenge, Event Tracking, Insight and Notification services within the six-service architecture.
- [ ] Implement EventBridge/SQS, transactional outbox, idempotent consumers and DLQs.
- [ ] Implement frontend and AWS deployment.


## Blockers
- No blocker remains for the MySQL integration-test increment; baseline CI run #7 is green.
- Cleanup-specific CI run `44648716325` is currently in progress; its final result must be recorded before closing this SDD change.
- Local .NET/Docker commands cannot run in this automation container, so future executable validation should continue through the Docker-capable GitHub Actions workflow.
- Business decisions still open in `docs/open-questions.md` must not be invented.

## Last run record
- Change: `docs/changes/remove-obsolete-postgres-bootstrap/`.
- SDD commit: `f00415ff773412a821d1ae0361cda2f2cfae6464`.
- Artifact deletion commit: `ea9db58a8dfbc188fd8ca4198678328dd6322c70`.
- Deleted: `infrastructure/docker/postgres/init/001-create-identity-db.sql`.
- Validation: fetched the recursive Git tree after deletion; no `infrastructure/docker/postgres/` path remains. Reviewed the active runtime/deployment files and architecture/Identity documentation listed in the change design; no active dependency on the script was found.
- Local checks: `dotnet` and `docker` are not installed in this automation container (only `git` is available), so local restore/build/test/Compose commands could not run.
- Existing baseline CI: run #7 passed restore, Release build (0 warnings/errors), Compose validation and all 21 tests. Cleanup-specific workflow run `44648716325` is in progress; no result is claimed yet.
- Changed paths: four SDD documents, deletion of the obsolete SQL file, and this project checkpoint.
- The MySQL Compose service, Pomelo package, Identity EF migration and `Database.MigrateAsync()` startup behavior were not changed.

## Single next task
Check and record the final result of GitHub Actions run `44648716325` for commit `ea9db58a8dfbc188fd8ca4198678328dd6322c70`. If green, begin the next SDD increment for IA-08: automated startup tests proving missing and weak JWT signing keys prevent the Identity API from starting. Do not implement IA-09 inactive-account behavior until the account lifecycle requirements are approved.
