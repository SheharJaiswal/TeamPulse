# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: `feature/backend-foundation`.
- Branch HEAD at the start of this automation run: `3224717bd209466e58792911402275d20567393b`.
- Branch HEAD before this checkpoint update: `1c49e140a165936d67bdfeb08fa493bf095f461a`; recent commits were fetched and reconciled before updating this file.
- Current SDD change: `docs/changes/identity-mysql-integration-tests/`.
- Latest code/config validation commit: `dfd597720aaa6e52739487f9c7c8680f94f6eac9`.
- Successful CI run: [#7 .NET 8 CI](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44641433504).
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
- [x] Updated Identity unit/controller/integration task lists and the Identity acceptance checklist. IA-01 through IA-07 and IA-DB-01/02 are verified. IA-08 and IA-09 remain unchecked.

## Verification evidence
Successful GitHub Actions run #7, commit `dfd597720aaa6e52739487f9c7c8680f94f6eac9`:
- `dotnet restore backend/TeamPulse.sln`: succeeded.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: succeeded, 0 warnings, 0 errors.
- `docker compose config --quiet`: succeeded.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: passed 21, failed 0, skipped 0 (including the real MySQL 8.4 Testcontainers integration tests).
- Run URL: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44641433504

Local execution limitation:
- This automation container has no .NET SDK or Docker CLI. Local attempts of `dotnet restore backend/TeamPulse.sln`, `dotnet build backend/TeamPulse.sln --no-restore`, `dotnet test backend/TeamPulse.sln` and `docker compose config` each exited 127 because the executable was not installed. The Docker-capable GitHub Actions runner executed and passed the equivalent restore/build/test plus Compose validation.

## Remaining / not yet implemented
- [ ] IA-08: automated startup test proving a missing/weak JWT signing key fails startup.
- [ ] IA-09: inactive employee authentication denial, pending implementation of account lifecycle support.
- [ ] Identity security hardening after approved requirements: HR provisioning, secret rotation/injection, refresh-token policy and audit requirements.
- [ ] Implement Habit, Challenge, Event Tracking, Insight and Notification services within the six-service architecture.
- [ ] Implement EventBridge/SQS, transactional outbox, idempotent consumers and DLQs.
- [ ] Implement frontend and AWS deployment.
- [ ] Review the obsolete PostgreSQL bootstrap artifact under `infrastructure/docker/postgres/` and remove it only after confirming no active references.

## Blockers
- No blocker remains for the MySQL integration-test increment; CI is green.
- Local .NET/Docker commands cannot run in this automation container, so future executable validation should continue through the Docker-capable GitHub Actions workflow.
- Business decisions still open in `docs/open-questions.md` must not be invented.

## Last run record
- Change: `docs/changes/identity-mysql-integration-tests/`.
- Added: SDD proposal/spec/design/tasks; `MySqlIntegrationFixture.cs`, `IdentityApiFactory.cs`, `IdentityApiIntegrationTests.cs`, `IdentityMigrationStartupTests.cs`.
- Updated: test project dependencies, JWT test, Identity API Program/controller/project dependencies, Domain/Application/Infrastructure project settings and explicit imports, README, CI workflow, Identity foundation/unit/controller/integration task checklists and Identity acceptance checklist.
- CI initially exposed missing framework/configuration imports, a strict exception-type assertion and test-host configuration override problems. These were corrected; final run #7 passed the complete suite and Compose validation.
- Code/config commit: `dfd597720aaa6e52739487f9c7c8680f94f6eac9`; subsequent documentation checklist commits: `9df77439845d2e82b42f8bea5211a1102e6a7719`, `d4f4011f20ef1533fe4f4b7f157ddaa9bcea4329`, `e33893c80e9e1cbec6421b27c8706a9885c96453`, `4fff10b118d0896df44cc5dce66af47804610860`, `1c49e140a165936d67bdfeb08fa493bf095f461a`.

## Single next task
Inspect the remaining repository references to the obsolete PostgreSQL bootstrap artifact under `infrastructure/docker/postgres/`; if no active source, deployment or documentation depends on it, remove that stale artifact and update the architecture/continuation docs. Do not change the MySQL persistence setup.
