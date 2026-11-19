# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: `feature/backend-foundation`.
- Branch HEAD at the start of this run: `a9531f494867cfc9e128e40c0c90d866d04aefb6`.
- Latest code increment committed in this run: `cee5ce9dd3e13cced0f06fb4bb2c267250adfc10`.
- Current SDD change: `docs/changes/identity-controller-tests/`.
- Resume rule: fetch the live branch HEAD and recent commit history first; reconcile this checkpoint with the tree; continue the single next task below without repeating completed work.

## Completed
- [x] SDD workflow, six service boundaries and ownership documentation established.
- [x] Identity Clean Architecture foundation, registration/login/`/me` endpoints and JWT/password hashing implemented.
- [x] Identity persistence uses MySQL 8.4, EF Core 8 and Pomelo; checked-in migration is applied at API startup for local/single-instance use.
- [x] Mandatory backend guidance aligned with ADR-0001 and MySQL.
- [x] Added .NET 8 xUnit project to `backend/TeamPulse.sln`.
- [x] Added password-hashing tests for one-way storage, successful verification and wrong-password rejection.
- [x] Added JWT tests for signature, issuer, audience, expiry and identity claims.
- [x] Added controller unit tests for missing registration fields, short passwords, normalized duplicate email, successful hashed registration, generic login failures and successful normalized login.
- [x] Added the Identity.API project reference to the unit-test project.
- [x] Added SDD proposal/spec/design/tasks for controller test coverage.

## Current / in progress
- Controller and security unit tests are committed as source, but execution/compilation remains unverified.
- MySQL-backed integration testing has not started; no database migration or HTTP authentication behavior is claimed as tested.

## Blockers
- `dotnet --info`: exit 127; `dotnet` is not installed in this execution environment.
- `docker version`: exit 127; `docker` is not installed in this execution environment.
- Consequently `dotnet restore backend/TeamPulse.sln`, `dotnet build backend/TeamPulse.sln --no-restore`, `dotnet test backend/TeamPulse.sln` and `docker compose config` could not be executed here.
- No .NET test result is claimed as passing. A .NET 8 SDK and Docker/MySQL-capable runner are required for runtime verification.
- Public registration remains development bootstrap only; local database credentials and JWT signing key are placeholders. HR onboarding and other open business rules remain in `docs/open-questions.md`.

## Remaining backlog (not the immediate next task)
- Verify EF migrations against MySQL 8.4 and repeated startup behavior.
- Complete Identity security hardening after requirements are approved (inactive employee lifecycle, HR provisioning, secret rotation/injection, refresh tokens and audit).
- Implement Habit, Challenge, Event Tracking, Insight and Notification services within the six-service ownership model.
- Implement EventBridge/SQS, transactional outbox, idempotent consumers and DLQs.
- Implement frontend and AWS deployment.
- Review and remove the obsolete PostgreSQL bootstrap artifact under `infrastructure/docker/postgres/` once no remaining references depend on it.

## Last run record
- Change: `docs/changes/identity-controller-tests/`.
- Added: `proposal.md`, `spec.md`, `design.md`, `tasks.md`, `backend/tests/Identity.UnitTests/IdentityControllerTests.cs`.
- Updated: `backend/tests/Identity.UnitTests/Identity.UnitTests.csproj`, `docs/changes/identity-service-foundation/tasks.md`.
- Source-level review: controller result types, fake repository contract, password hasher use and DTO property names checked against current source. No compiler/test execution.
- Environment checks: `git --version` succeeded (2.47.3); `dotnet --info` failed (127, command not found); `docker version` failed (127, command not found).
- Code commit: `cee5ce9dd3e13cced0f06fb4bb2c267250adfc10`.

## Single next task
Add a MySQL-backed Identity integration-test harness using ASP.NET Core `WebApplicationFactory` and a disposable MySQL 8.4 Testcontainers instance, covering migration startup, persistence/unique-email behavior, bearer authentication and `/me`. Include the test execution in .NET 8 CI so the suite can run on a capable GitHub-hosted runner.
