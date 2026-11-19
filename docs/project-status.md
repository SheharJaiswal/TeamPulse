# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: feature/backend-foundation.
- Latest committed checkpoint before this increment: 708dda5fd48224730cff3436c3b3323510c17fcb.
- Latest commit from this increment: 9698f33f45a9a9c896684abf761e88a0e5286a14.
- Current change: docs/changes/identity-unit-tests/.
- Resume rule: every run must fetch the live branch HEAD, inspect recent commits and this file, then continue the first unfinished task. Do not repeat completed tasks or trust stale SHAs.

## Completed
- [x] SDD instructions and repository workflow established.
- [x] Six service boundaries and ownership documented.
- [x] Identity Clean Architecture foundation and initial API endpoints scaffolded.
- [x] Identity persistence configured for MySQL 8.4 with EF Core 8 / Pomelo.
- [x] Checked-in initial Identity EF migration and startup Database.MigrateAsync() configured.
- [x] Mandatory backend conventions aligned with the accepted MySQL ADR.
- [x] Added .NET 8 xUnit test project to backend/TeamPulse.sln.
- [x] Added password hashing tests: plaintext is not stored, original password verifies, different password fails.
- [x] Added JWT tests for signature, issuer, audience, lifetime and subject/email/name/organization/team/role claims.

## In progress / next
- [ ] Add controller tests for required-field and minimum-password-length validation, duplicate normalized email and generic login failures.
- [ ] Add MySQL-backed integration tests for EF persistence, uniqueness, migrations, authentication middleware and /me.
- [ ] Run dotnet restore backend/TeamPulse.sln, dotnet build backend/TeamPulse.sln --no-restore, dotnet test backend/TeamPulse.sln, and docker compose config.
- [ ] Verify startup migration against MySQL 8.4 and repeated startup behavior.
- [ ] Complete Identity security hardening (inactive employee enforcement, HR provisioning decision, secret injection/key rotation, refresh-token and audit requirements).
- [ ] Implement Habit, Challenge, Event Tracking, Insight and Notification services in approved SDD increments.
- [ ] Implement EventBridge/SQS, transactional outbox, idempotent consumers and DLQs.
- [ ] Implement frontend and AWS deployment.

## Blockers / limitations
- No .NET SDK, Docker daemon or MySQL runtime is available through this repository-editing session. Restore, compilation, test execution, Compose validation and live migration were not run.
- The added tests are source artifacts only until executed in a .NET 8 environment; no passing test result is claimed.
- Controller tests and MySQL integration tests are not implemented yet.
- Identity registration remains development bootstrap only; local DB credentials and JWT signing key are placeholders.
- Open business questions remain in docs/open-questions.md; do not invent answers.

## Last run record
- Change: docs/changes/identity-unit-tests/.
- Files added: proposal.md, spec.md, design.md, tasks.md; backend/tests/Identity.UnitTests/Identity.UnitTests.csproj, Usings.cs, PasswordHashingTests.cs, JwtTokenServiceTests.cs.
- Files updated: backend/TeamPulse.sln; docs/changes/identity-service-foundation/tasks.md.
- Checks performed: source-level review of test references and JWT claim mapping; no executable commands were run.
- Commit: 9698f33f45a9a9c896684abf761e88a0e5286a14.
- Next task: implement controller unit tests for registration/login behavior, then add MySQL integration test infrastructure.
