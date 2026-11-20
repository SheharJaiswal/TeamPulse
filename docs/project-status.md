# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: `feature/backend-foundation`.
- Branch HEAD at the start of this automation run: `4f4c5f5a7790e742ae23f899ea7883f177ffca2c`.
- First IA-08 implementation commit: `877cb49ebdc378e0eef627c1cfa7fa0d8668e170`.
- CI-failure diagnosis/fix commit: `0eee18d741770940b9f4a7dac0fb1a52109f5401`.
- This checkpoint update is a direct child of the fix commit; next run must fetch the live branch ref.
- Current SDD change: `docs/changes/identity-jwt-startup-validation/`.
- PostgreSQL cleanup CI run #24: [44648716325](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44648716325) completed successfully.
- IA-08 first CI run #25: [44655219417](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44655219417) failed in tests; see verification details below.
- IA-08 corrected CI run #26: [44655601591](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44655601591) was in progress at checkpoint time.
- Resume rule: fetch live branch HEAD and recent commits, inspect this file and relevant specs/contracts, then continue the single next task. Never repeat completed work.

## Completed
- [x] SDD workflow and exactly six service ownership boundaries remain documented and unchanged.
- [x] Identity Clean Architecture foundation, registration/login/`/me`, password hashing and JWT implementation retained.
- [x] MySQL 8.4 / EF Core 8 / Pomelo persistence and checked-in startup migration retained.
- [x] Unit, controller and MySQL Testcontainers integration tests cover password hashing, JWT claims/signature, registration/login, duplicate email, migrations/restart, persistence and authenticated `/me`.
- [x] .NET 8 GitHub Actions CI covers restore, Release build, Docker Compose validation and Testcontainers-backed tests.
- [x] Removed the orphaned PostgreSQL bootstrap SQL file; active MySQL configuration was unchanged.
- [x] Confirmed cleanup CI run #24 completed successfully: restore, build, Compose validation and test steps all succeeded.
- [x] Defined IA-08: absent, blank, whitespace-only or fewer than 32 UTF-8 bytes must fail startup with an actionable error.
- [x] Tightened startup validation and extracted `JwtSigningKeyValidator` used by the real Program entry point.
- [x] Removed the committed JWT signing-key placeholder from `appsettings.json`; the key must now be supplied through `Jwt__SigningKey` or a secret store.
- [x] Updated README local setup instructions and Identity design/specification documentation to prohibit committing signing keys.
- [x] Corrected the startup-test factory to use the same `UseSetting` and in-memory configuration override pattern as the proven Identity integration factory.
- [x] Added UTF-8 byte-boundary tests for 31 and exactly 32 bytes.

## In progress
- [ ] IA-08 startup tests are corrected and committed, but final verification is pending CI run #26.

## Verification evidence
- Cleanup run #24: [GitHub Actions](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44648716325) concluded `success`. Restore, Release build, Docker Compose validation and test steps all completed successfully.
- Baseline run #7: [GitHub Actions](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44641433504) passed 21 tests, 0 failed, 0 skipped; Release build had 0 warnings and 0 errors.
- IA-08 run #25: [GitHub Actions](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44655219417) concluded `failure`. Restore, Release build (0 warnings, 0 errors) and Compose validation succeeded. Tests: 21 passed, 5 failed, 0 skipped (26 total).
- Run #25 failure diagnosis: the first test factory cleared application configuration sources, but the actual minimal-hosting test startup still loaded the appsettings placeholder. The API therefore reached the default MySQL connection instead of failing at the JWT key guard. The valid-key boundary test also unexpectedly attempted MySQL. No production MySQL migration or database configuration was changed by that failed test run.
- Corrective commit `0eee18d741770940b9f4a7dac0fb1a52109f5401`: removed the checked-in signing-key fallback; startup tests now use the same `UseSetting` plus in-memory configuration pattern as the existing integration factory; extracted and directly tests the UTF-8 byte validator.
- IA-08 run #26: [GitHub Actions](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44655601591) was `in_progress` at checkpoint time. No success is claimed for it.
- Local environment probe: `command -v dotnet` and `command -v docker` returned no executable; `command -v git` returned `/usr/bin/git`. No local .NET or Docker validation was possible.

## Remaining / not yet implemented
- [ ] IA-08: await run #26; if green, mark startup validation verified in the Identity acceptance checklist.
- [ ] IA-09: inactive employee authentication denial, pending approved account lifecycle requirements.
- [ ] Identity security hardening after requirements are approved: HR provisioning, production secret rotation/operational controls, refresh-token policy and audit requirements.
- [ ] Implement Habit, Challenge, Event Tracking, Insight and Notification services within the six-service architecture.
- [ ] Implement EventBridge/SQS, transactional outbox, idempotent consumers and DLQs.
- [ ] Implement frontend and AWS deployment.

## Blockers
- CI run #26 is pending; its final result is required before IA-08 can be marked complete.
- This automation environment has Git but no .NET SDK or Docker CLI; executable validation must run in GitHub Actions.
- IA-09 must not be implemented until employee account lifecycle/inactivation requirements are approved. Existing `docs/open-questions.md` remains authoritative for unresolved business rules.
- No known blocker remains for the MySQL 8.4 / EF Core 8 / Pomelo baseline.

## Last run record
- Change: `docs/changes/identity-jwt-startup-validation/`.
- Implementation commit: `877cb49ebdc378e0eef627c1cfa7fa0d8668e170`.
- Corrective implementation commit: `0eee18d741770940b9f4a7dac0fb1a52109f5401`.
- Files changed in the correction:
  - `backend/src/Services/Identity/Identity.API/Program.cs`
  - `backend/src/Services/Identity/Identity.API/Security/JwtSigningKeyValidator.cs`
  - `backend/src/Services/Identity/Identity.API/appsettings.json`
  - `backend/tests/Identity.UnitTests/IdentityStartupValidationFactory.cs`
  - `backend/tests/Identity.UnitTests/IdentityStartupValidationTests.cs`
  - `README.md`
  - `docs/changes/identity-jwt-startup-validation/{proposal,spec,design,tasks}.md`
  - `docs/specs/identity-service.md`
  - `docs/changes/identity-service-foundation/design.md`
  - `docs/project-status.md`
- Security change: no JWT signing key is checked into appsettings; local developers set `Jwt__SigningKey`, and deployed environments must inject it from a secret store.
- Failure correction: run #25 demonstrated that clearing test configuration sources did not isolate the minimal host as intended. The corrected factory now follows the proven configuration override pattern and appsettings has no fallback key.
- New CI run #26 is pending; final outcome is intentionally not recorded as passed.

## Single next task
Fetch and inspect GitHub Actions run #26 (`44655601591`) for corrective commit `0eee18d741770940b9f4a7dac0fb1a52109f5401`. If restore, Release build, Docker Compose validation and all tests pass, mark IA-08 verified in the Identity acceptance checklist and choose the next approved task. If any step fails, inspect job logs and continue fixing this existing SDD change. Do not implement IA-09 until account lifecycle requirements are approved.
