# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Active branch: feature/backend-foundation.
- Live branch HEAD at start of this run: 40e6a37b92b1def5cce6ddf988f576a0a363e194.
- Recent history was reconciled from the live Git ref and commit list; prior conversation-only SHAs not present on the branch were not treated as repository state.
- Previous verified implementation: IA-08 JWT startup validation; CI run #27 (44655706057) succeeded with 28 tests, 0 failures, 0 skipped.
- Current change: docs/changes/identity-registration-environment-guard/.
- Implementation and SDD artifacts are committed in this increment; push-triggered CI result is pending.
- Resume rule: fetch live branch HEAD, recent commits and tree first; inspect this checkpoint and relevant specs/contracts; continue the single next task without repeating completed work.

## Completed
- [x] Exactly six service boundaries and service-owned data documented.
- [x] Identity Clean Architecture, registration/login/me, password hashing and JWT implementation.
- [x] MySQL 8.4 / EF Core 8 / Pomelo persistence and checked-in startup EF migration.
- [x] Identity unit, controller and MySQL Testcontainers integration test foundations.
- [x] .NET 8 GitHub Actions restore/build/Compose/test workflow.
- [x] Removed obsolete PostgreSQL bootstrap artifact.
- [x] IA-08: fail-fast JWT signing-key validation; no committed signing-key fallback.
- [x] IA-01 through IA-08 and IA-DB-01/02 were previously recorded as verified by CI run #27.
- [x] This increment: enforce Development-only bootstrap registration; add unit and MySQL-backed integration coverage; update Identity specification, API contract, README and SDD tasks.

## In progress
- [ ] Run the .NET 8 CI workflow for the registration environment guard and record its exact result.

## Blockers
- IA-09 remains blocked by unresolved employee lifecycle requirements in docs/open-questions.md question 9. Do not implement inactive-account enforcement until those rules are approved.
- This execution environment has no .NET SDK or Docker CLI; executable validation must run on the Docker-capable GitHub Actions runner.
- No known requirement blocker remains for the current registration guard; CI is pending.

## Verification
- Previous verified baseline: GitHub Actions run #27, 44655706057, success; restore, Release build (0 warnings/errors), Docker Compose validation and 28 tests passed.
- Current increment: no build/test result is claimed yet. The push-triggered workflow must validate restore, Release build, docker compose config --quiet, and the full test suite including MySQL 8.4 Testcontainers cases.

## Changed files in current increment
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

## Last run record
- Starting HEAD: 40e6a37b92b1def5cce6ddf988f576a0a363e194.
- Inspected the live branch ref, last 10 commits, recursive Git tree and CI history. Latest CI before this increment: run #27 succeeded on implementation commit a83fb6464f51b1989a9a53d36e0308ff0cbb33fe.
- Read AGENTS.md, README.md, this checkpoint, docs/open-questions.md, Identity specification and API contract, service ownership, communication conventions, ADR-0001, Identity change tasks, controller, Program, controller tests, integration tests, factories and workflow.
- Confirmed the exact next task in the prior checkpoint: enforce Development-only bootstrap registration.
- Current change uses IHostEnvironment.IsDevelopment() and returns 404 before validation or persistence in non-Development environments.
- CI evidence for this increment: pending.

## Single next task
Verify the registration environment guard with the triggered GitHub Actions workflow. If it passes, update this checkpoint with the exact run URL, test counts and final commit SHA. If it fails, diagnose workflow logs and fix only the failing issue before marking this change complete. IA-09 must remain blocked until employee lifecycle rules are approved; do not invent that policy.
