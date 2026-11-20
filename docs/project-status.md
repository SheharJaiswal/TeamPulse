# Project Status
Updated: 2026-11-20

## Continuation checkpoint

- Active branch: `feature/backend-foundation` only.
- Live branch HEAD before implementation: `501fc4bad3983978df294051607b0d46e192abd0`.
- Latest verified code commit: `9b908203b44954b56b8a13b49c96979626be126d` (GitHub Actions run #80).
- Current SDD increment: `docs/changes/identity-outbox-eventbridge-dispatcher/`; proposal/spec/design/tasks were completed before implementation. Core dispatcher implementation is now committed and CI-verified; additional acceptance-test hardening and production infrastructure remain.
- Product policy baseline: ADR-0002; all nine initial product open questions are resolved using the project owner's instruction to apply recommended MVP defaults.
- Completed SDD increments include inactive-login guard, HR administrator bootstrap, HR employee lifecycle and the new Identity outbox/EventBridge dispatcher design.
- Resume rule: fetch the live branch HEAD, inspect recent commits and repository tree, read this checkpoint and relevant specs/contracts, then continue the single next task. Do not recreate completed work.

## Completed

- [x] Exactly six service boundaries and ownership documented; no cross-service database access.
- [x] Identity Clean Architecture, registration/login/me, password hashing and JWT implementation.
- [x] MySQL 8.4 / EF Core 8 / Pomelo persistence and checked-in startup EF migration.
- [x] Identity unit, controller and MySQL Testcontainers integration test foundations.
- [x] .NET 8 GitHub Actions restore/build/Compose/test workflow.
- [x] Removed obsolete PostgreSQL bootstrap artifact.
- [x] IA-08: fail-fast JWT signing-key validation; no committed signing-key fallback.
- [x] IA-10: bootstrap registration is Development-only, returning 404 without persistence in other environments.
- [x] IA-11: legacy password hashes upgrade before token issuance; normal hashes are not rewritten; persistence failure prevents token issuance.
- [x] HP-01 through HP-07: separate liveness/readiness endpoints; /health compatibility alias; bounded three-second DB probe; no sensitive health details.
- [x] Health integration test proves liveness remains healthy while MySQL is stopped and readiness returns 503.
- [x] CI run #31 (44675962776): 38 tests passed, 0 failed, 0 skipped; Release build had 0 warnings/errors; Compose validation passed.
- [x] ADR-0002 accepted: all nine initial product open questions resolved with recommended MVP defaults.
- [x] IA-09 inactive login guard: password verification runs for an existing employee; inactive status returns the same generic 401 before hash rehash, persistence or token issuance.
- [x] Added unit and MySQL integration coverage for inactive employee login and unchanged password hash.
- [x] Added Identity.AdminBootstrap console project to backend/TeamPulse.sln. It reads credentials only from protected environment configuration, requires explicit --execute, hashes the password, refuses any existing HrAdmin (active or inactive), refuses duplicate email and serializes concurrent calls with a MySQL advisory lock and transaction.
- [x] Added unit and MySQL integration tests for HR administrator bootstrap. CI run #51 passed 49 tests with 0 warnings and 0 errors.
- [x] Added HR lifecycle commands: HrAdmin-only deactivate/reactivate API, actor attribution from validated JWT sub, reason/note/correlation validation and idempotency-key handling.
- [x] Added Employee.LifecycleVersion and domain transition method; MySQL row locking plus EF optimistic concurrency reject stale commands.
- [x] Added immutable employee lifecycle audit and transactional outbox entities; employee, audit and outbox writes commit in one transaction.
- [x] Added SHA-256 canonical request fingerprint, unique actor/idempotency-key index, replay handling and conflicting-key 409 behavior.
- [x] Added checked-in migration 20261120060000_EmployeeLifecycleOutbox and updated IdentityDbContext model snapshot.
- [x] Added unit and MySQL Testcontainers coverage for role authorization, actor spoof prevention, both transition directions, no-op, same-key concurrent retry, conflicting key reuse, event envelope, rollback atomicity and invalid headers.
- [x] Updated Identity API contract, Identity service specification and lifecycle SDD task checklists.
- [x] Created outbox dispatcher SDD proposal/spec/design/tasks before implementation.
- [x] Defined EventBridge mapping: source `teampulse.identity`, detail type = eventType, time = occurredAtUtc, detail = complete existing envelope, bus name from deployment configuration.
- [x] Defined AWS SDK default credential chain, workload-role least privilege, configuration-gated worker, MySQL SKIP LOCKED leases, per-entry acknowledgement, retry/quarantine, producer-vs-target DLQ boundary and monitoring requirements.
- [x] Updated communication conventions, event envelope contract and open questions to reflect the dispatcher contract and deployment prerequisite.
- [x] Implemented Identity outbox lease/retry/quarantine metadata and EF migration 20261120070000_OutboxDispatcher.
- [x] Added MySQL ReadCommitted + FOR UPDATE SKIP LOCKED batch claims, unique lease tokens and lease-token concurrency fencing for publish/retry/quarantine updates.
- [x] Added AWS SDK EventBridge publisher preserving the full schema-v1 envelope, per-entry partial failure classification, and retryable/systemic/permanent outcomes.
- [x] Added Identity API BackgroundService, disabled by default, with validated bus/region/options and a 20-second AWS request timeout beneath the 60-second default lease.
- [x] Added full-jitter bounded retries, systemic-failure pause, publisher-side DB quarantine and no premature PublishedAtUtc updates.
- [x] Added System.Diagnostics.Metrics gauges/counters for pending count/age, active/expired leases, quarantined rows, claims, accepted/retried/quarantined events, systemic failures and lost-lease acknowledgements; publisher records PutEvents latency and failed entries.
- [x] Added domain, EventBridge publisher and MySQL Testcontainers coverage for envelope mapping, partial failures, concurrent claims, lease expiry, stale-owner fencing, acknowledgement, quarantine and queue metrics.

## In progress

- [ ] Complete remaining acceptance-test coverage: options validation, retry scheduling/backoff boundaries, systemic/permanent error classification, log-payload redaction and crash-after-EventBridge-acceptance replay with the same eventId.
- [ ] Review CI evidence and update the SDD task checklist after those tests are complete.
- [ ] Keep production activation blocked until EventBridge/IAM/targets/SQS DLQs/alarms are provisioned and verified in a non-production AWS environment.

## Blockers and decisions

- **Deployment enablement blocker:** The repository has no deployment/IaC tree (the inspected root contains .github, backend, docs and docker-compose.yml; `deploy/` and `infra/` do not exist). No EventBridge bus, Identity workload IAM role, EventBridge rules/targets, SQS queues/DLQs or CloudWatch alarms are defined. The dispatcher is implemented and tested with a mocked EventBridge client while disabled, but `OutboxDispatcher__Enabled` must remain false in deployed environments until those resources are provisioned and reviewed.
- Bus name is intentionally deployment-supplied through `EventBridge__BusName`; region uses `AWS_REGION`; the fixed source is `teampulse.identity`. No account-specific ARN or credential is to be invented or committed.
- The bootstrap utility has not been executed against a production database; no claim is made that a production HR administrator account exists.
- Employee invitation/provisioning and credential activation remain deferred pending invitation delivery, token expiry and password setup decisions.
- Organization Admins have no direct employee profile/lifecycle write access. Their HR request-intake workflow is not defined. HR role catalogue/elevated-role assignment and HRIS service authentication are also deferred.
- Downstream employee-status projections and global revocation remain future work. Existing stateless JWTs may remain usable by receiving services until expiry.
- Retention defaults (90-day evidence, 12-month identifiable activity/participation, 24-month security/admin audit, up to 5-year anonymized aggregates) require jurisdiction-specific privacy/legal sign-off before production.
- This execution environment has no .NET SDK or Docker CLI. Local restore/build/test/Compose validation cannot be performed here; GitHub Actions is the verification environment.
- No unresolved initial MVP product questions remain. Dispatcher deployment resources are an infrastructure prerequisite, not a reason to hard-code a bus or credentials in application code.

## Verification evidence

### Latest verified code

- GitHub Actions run #80: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44692124602
- Verified code commit: `9b908203b44954b56b8a13b49c96979626be126d`.
- Run conclusion: success.
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: passed; 0 warnings, 0 errors.
- `docker compose config --quiet`: passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: 64 passed, 0 failed, 0 skipped.
- MySQL 8.4 Testcontainers integration tests passed for concurrent claims, lease expiry, stale-token fencing, published acknowledgement, quarantine and queue snapshot metrics.
- EventBridge publisher tests passed for complete envelope mapping and per-entry partial failure handling; no live AWS credentials or EventBridge account were required.
- Run #78 exposed a test assumption that the outbox table was empty; the test was corrected to compare against a baseline snapshot in commit `b6c39fccdd7056d68307e4b66a543ae3ea70bccc`. Run #79 passed 64 tests; run #80 verifies the final publisher latency/failure metrics code.
- Run #77 verified the warning-free dispatcher core (63 passed, 0 warnings/errors).

### This run — dispatcher implementation

- Reconciled the live branch at `6a85445f71692b436c8289c1de24cfbf964e4429`, including recent commit history and repository tree. The existing checkpoint's last code commit `3f0d8bfb...` was correct; two subsequent docs commits were present in live history.
- Read AGENTS.md, README.md, docs/project-status.md, docs/open-questions.md, ADR-0001/0002, service ownership, communication conventions, Identity specification, event catalog/envelope, lifecycle SDD and current outbox entity/context/migration/tests.
- Confirmed no `deploy/` or `infra/` directory and no bus, IAM, EventBridge target, SQS DLQ or CloudWatch definitions.
- Created the dispatcher SDD and updated communication/event contracts and open questions.
- `dotnet --info`: unavailable, exit 127 (`dotnet: command not found`).
- `docker --version`: unavailable, exit 127 (`docker: command not found`).
- `git --version`: succeeded, 2.47.3.
- `dotnet restore backend/TeamPulse.sln`, `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`, `dotnet test backend/TeamPulse.sln --configuration Release --no-build` and `docker compose config --quiet`: not run because their required .NET/Docker executables are absent.
- No new CI run was triggered or claimed: the workflow ignores `docs/**` and README-only changes. The previous run #72 remains the latest code verification.
- SDD commit: `3f6f3b9041acb9d681de9c993e626d79c55b1e6b`.

## Changed files in this implementation increment

- backend/src/Services/Identity/Identity.Domain/Entities/OutboxMessage.cs
- backend/src/Services/Identity/Identity.Application/Abstractions/OutboxDispatchContracts.cs
- backend/src/Services/Identity/Identity.Infrastructure/Persistence/IdentityDbContext.cs
- backend/src/Services/Identity/Identity.Infrastructure/Persistence/OutboxMessageStore.cs
- backend/src/Services/Identity/Identity.Infrastructure/Persistence/Migrations/20261120070000_OutboxDispatcher.cs
- backend/src/Services/Identity/Identity.Infrastructure/Persistence/Migrations/IdentityDbContextModelSnapshot.cs
- backend/src/Services/Identity/Identity.Infrastructure/Messaging/EventBridgePublisher.cs
- backend/src/Services/Identity/Identity.Infrastructure/Identity.Infrastructure.csproj
- backend/src/Services/Identity/Identity.API/Program.cs
- backend/src/Services/Identity/Identity.API/appsettings.json
- backend/src/Services/Identity/Identity.API/BackgroundServices/OutboxDispatcherWorker.cs
- backend/tests/Identity.UnitTests/Identity.UnitTests.csproj
- backend/tests/Identity.UnitTests/OutboxDispatcherTests.cs
- docs/changes/identity-outbox-eventbridge-dispatcher/tasks.md
- docs/changes/identity-outbox-eventbridge-dispatcher/proposal.md
- docs/changes/identity-outbox-eventbridge-dispatcher/spec.md
- docs/project-status.md

## Single next task

Complete the remaining dispatcher acceptance tests: validate disabled/enabled configuration, retry/backoff boundaries, systemic versus permanent EventBridge errors, payload-free structured logging and ambiguous publish-after-acceptance replay preserving the same eventId. Then reconcile the test checklist and keep the worker disabled until EventBridge bus/IAM/rules/targets/SQS DLQs/alarms are provisioned and verified in non-production.
