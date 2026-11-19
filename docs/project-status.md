# Project Status
Updated: 2026-11-20

## Continuation checkpoint
- Last inspected branch: `feature/backend-foundation`.
- Previous implementation checkpoint: `f3692a40413f5033592d8a4ac08abfd97eae1df7` (Identity persistence switched to MySQL and startup EF migrations configured).
- Current work completed in this run: persistence guidance alignment; see `docs/changes/persistence-guidance-alignment/`.
- Resume rule: always fetch the live branch HEAD and inspect this file plus recent commits before choosing work. Continue the first unfinished item below; do not repeat completed checklist items.

## Completed
- [x] SDD instructions and repository workflow established.
- [x] Six service boundaries and ownership documented.
- [x] Identity Clean Architecture foundation and initial API endpoints scaffolded.
- [x] Identity persistence configured for MySQL 8.4 with EF Core 8 / Pomelo.
- [x] Checked-in initial Identity EF migration and startup `Database.MigrateAsync()` configured.
- [x] Mandatory backend conventions aligned with the accepted MySQL ADR.

## In progress / next
- [ ] Add Identity unit tests for registration validation, password hashing and JWT claims.
- [ ] Add Identity integration tests for MySQL persistence, duplicate email handling, authentication and `/me`.
- [ ] Execute .NET 8 restore/build/test and Docker Compose configuration validation; verify migration against MySQL.
- [ ] Complete Identity security hardening (inactive employee enforcement, HR provisioning decision, key rotation/secret injection, refresh-token and audit requirements).
- [ ] Implement Habit, Challenge, Event Tracking, Insight and Notification services in approved SDD increments.
- [ ] Implement EventBridge/SQS, transactional outbox, idempotent consumers and DLQs.
- [ ] Implement frontend and AWS deployment.

## Blockers / limitations
- No .NET/Docker execution environment was available during this run, so restore, build, tests, Docker validation and live MySQL migration were not executed.
- Identity registration remains development bootstrap only; local database credentials and JWT signing key are placeholders.
- Open business questions remain in `docs/open-questions.md`; do not invent answers.
- The Identity migration and Pomelo/provider compatibility remain unverified at runtime.

## Last run record
- Change: `docs/changes/persistence-guidance-alignment/`.
- Files changed: `AGENTS.md`, `docs/changes/persistence-guidance-alignment/{proposal,spec,design,tasks}.md`, `docs/project-status.md`.
- Checks: documentation cross-check against `README.md` and `docs/decisions/0001-backend-baseline.md`; no executable checks run.
- Next task: create an SDD change for Identity automated tests, then implement unit tests first.
