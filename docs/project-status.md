# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: `feature/backend-foundation`.
- Branch HEAD at the start of this run: `4f4c5f5a7790e742ae23f899ea7883f177ffca2c`.
- Identity JWT startup-validation implementation commit: `877cb49ebdc378e0eef627c1cfa7fa0d8668e170`.
- This checkpoint update is a direct child of the implementation commit; the next run must fetch the live branch ref rather than rely on a recorded SHA.
- Current SDD change: `docs/changes/identity-jwt-startup-validation/`.
- PostgreSQL cleanup CI run #24: [44648716325](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44648716325) completed successfully for deletion commit `ea9db58a8dfbc188fd8ca4198678328dd6322c70`.
- Current Identity JWT validation CI run #25: [44655219417](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44655219417), commit `877cb49ebdc378e0eef627c1cfa7fa0d8668e170`, was in progress at checkpoint time (Restore step).
- Resume rule: fetch live branch HEAD and recent commits, inspect this file and relevant specs/contracts, then continue the single next task. Never repeat completed work.

## Completed
- [x] SDD workflow and exactly six service ownership boundaries remain documented and unchanged.
- [x] Identity Clean Architecture foundation, registration/login/`/me`, password hashing and JWT implementation retained.
- [x] MySQL 8.4 / EF Core 8 / Pomelo persistence and checked-in startup migration retained.
- [x] Unit, controller and MySQL Testcontainers integration tests added for password hashing, JWT claims/signature, registration/login, duplicate email, migrations/restart, persistence and authenticated `/me`.
- [x] .NET 8 GitHub Actions CI added for restore, Release build, Docker Compose validation and Testcontainers-backed tests.
- [x] Removed the orphaned PostgreSQL bootstrap SQL file; the active MySQL configuration was unchanged.
- [x] Confirmed PostgreSQL cleanup CI run #24 completed successfully: restore, build, Compose validation and test steps all succeeded.
- [x] Defined IA-08 signing-key requirements in the approved Identity spec: absent, blank, whitespace-only or fewer than 32 UTF-8 bytes must fail startup.
- [x] Tightened the Identity startup guard to reject null/blank/whitespace signing keys explicitly before database configuration or migration.
- [x] Added real-entry-point WebApplicationFactory tests for an absent key, short/31-byte/whitespace keys and the exactly-32-byte boundary.

## In progress
- [ ] IA-08 implementation is committed, but its .NET 8 CI run #25 is still running; do not mark IA-08 verified until the full run succeeds.

## Verification evidence
- Cleanup run #24: [GitHub Actions](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44648716325) concluded `success`. Restore, Release build, Docker Compose validation and Testcontainers test steps all completed successfully.
- Earlier baseline run #7: [GitHub Actions](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44641433504) passed 21 tests, 0 failed, 0 skipped; Release build had 0 warnings and 0 errors.
- Current run #25: [GitHub Actions](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44655219417) was `in_progress` at checkpoint time. Set up job, checkout and .NET 8 setup succeeded; Restore was running; build, Compose validation and tests were pending. No success is claimed for this run.
- Repository tree was inspected recursively at the live branch HEAD before this increment; the tracked tree has the six-service documentation, Identity API, MySQL migration/test harness and no PostgreSQL bootstrap directory.
- Local environment probe: `command -v dotnet` and `command -v docker` returned no executable; `command -v git` returned `/usr/bin/git`. Therefore no local .NET or Docker validation was possible in this run.

## Remaining / not yet implemented
- [ ] IA-08: await run #25; if green, mark startup validation verified in the Identity acceptance checklist.
- [ ] IA-09: inactive employee authentication denial, pending approved account lifecycle requirements.
- [ ] Identity security hardening after requirements are approved: HR provisioning, production secret injection/rotation, refresh-token policy and audit requirements.
- [ ] Implement Habit, Challenge, Event Tracking, Insight and Notification services within the six-service architecture.
- [ ] Implement EventBridge/SQS, transactional outbox, idempotent consumers and DLQs.
- [ ] Implement frontend and AWS deployment.

## Blockers
- CI run #25 has not completed at checkpoint time. Its final result is required before IA-08 can be marked complete.
- This automation environment has Git but no .NET SDK or Docker CLI; executable validation must run in GitHub Actions.
- IA-09 must not be implemented until employee account lifecycle/inactivation requirements are approved. Existing `docs/open-questions.md` remains authoritative for unresolved business rules.
- No blocker is known for the current MySQL 8.4 / EF Core 8 / Pomelo baseline.

## Last run record
- Change: `docs/changes/identity-jwt-startup-validation/`.
- Implementation commit: `877cb49ebdc378e0eef627c1cfa7fa0d8668e170`.
- Changed files:
  - `backend/src/Services/Identity/Identity.API/Program.cs`
  - `backend/tests/Identity.UnitTests/IdentityStartupValidationFactory.cs`
  - `backend/tests/Identity.UnitTests/IdentityStartupValidationTests.cs`
  - `docs/changes/identity-jwt-startup-validation/{proposal,spec,design,tasks}.md`
  - `docs/specs/identity-service.md`
  - `docs/changes/identity-service-foundation/spec.md`
  - `docs/changes/remove-obsolete-postgres-bootstrap/tasks.md`
  - `docs/project-status.md`
- Production behavior change: startup now rejects blank/whitespace signing keys explicitly and requires at least 32 UTF-8 bytes; database migration order and MySQL configuration are unchanged.
- Tests added: missing key, short key, 31-byte key, whitespace-only key, and exactly-32-byte boundary.
- CI: run #25 was still in progress at checkpoint time; exact final result is intentionally pending.
- Local validation: unavailable because .NET and Docker executables are not installed in this environment.

## Single next task
Fetch the result of GitHub Actions run #25 (`44655219417`) for commit `877cb49ebdc378e0eef627c1cfa7fa0d8668e170`. If restore, build, Compose validation and tests all pass, mark IA-08 verified in the Identity specs/checklists and select the next approved task. If any step fails, inspect its job steps/logs, fix the cause in the existing SDD change and rerun CI before marking IA-08 complete. Do not implement IA-09 until account lifecycle requirements are approved.
