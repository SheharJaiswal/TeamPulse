# Project Status
Updated: 2026-11-20

## Continuation checkpoint

- Active branch: `feature/backend-foundation` only.
- Live branch HEAD before this checkpoint update: `28693d45d2d556e1f3c20d1064b9257795b20f3a`.
- `feature/backend-foundation` is ahead of `main` and 0 commits behind; `main` was not modified.
- Latest verified code commit: `38c90810ff1fece5382c69d81fa025478d0a8ce6` (GitHub Actions run #84).
- Current SDD increment: `docs/changes/identity-eventbridge-infrastructure/`; the parameterized CloudFormation stack and dedicated cfn-lint workflow are committed. Static lint is pending; live AWS deployment and production activation remain blocked.
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
- [x] Extracted a deterministic full-jitter retry-delay policy; the worker supplies a runtime random sample while unit tests verify exponential ceilings and jitter boundaries.
- [x] Hardened EventBridge response handling: an omitted aggregate `FailedEntryCount` does not override valid per-entry results; a present count that contradicts entry results makes the whole batch retryable rather than acknowledging ambiguous events.
- [x] Added acceptance tests for disabled/enabled configuration, missing bus/region, invalid option ranges, retry/backoff boundaries, systemic/permanent error classification, payload-free worker logs, persisted retry due times and crash-after-acceptance replay preserving the original eventId.
- [x] Added domain, EventBridge publisher and MySQL Testcontainers coverage for envelope mapping, partial failures, concurrent claims, lease expiry, stale-owner fencing, acknowledgement, quarantine and queue metrics.
- [x] Added parameterized Identity EventBridge CloudFormation stack with existing-role PutEvents policy, schema-v1 lifecycle routing, five service-owned SQS queues, per-target DLQs, scoped queue policies and AWS-managed delivery alarms.
- [x] Added dedicated cfn-lint GitHub Actions workflow, infrastructure deployment guide and SQS/EventBridge message-body contract.

## In progress

- The Identity EventBridge fan-out CloudFormation template, deployment README and dedicated cfn-lint workflow are committed. The workflow result is pending at this checkpoint; do not claim the template is validated until it passes.
- No AWS stack has been deployed. The dispatcher remains disabled. The five queue consumers and their service-owned receive permissions are not implemented.
- Producer-side outbox alarms remain incomplete: the worker's .NET Meter instruments have no CloudWatch/OpenTelemetry exporter.

## Blockers and decisions

- **AWS deployment blocker:** The CloudFormation template now defines the bus, scoped Identity role policy, lifecycle rule, five SQS targets, target-delivery DLQs, queue policies and AWS-managed delivery alarms, but no stack has been deployed. A non-production AWS account/region, authorized provisioning role, existing Identity workload role name and approved queue-age threshold are required. Supply an existing SNS topic ARN if alarm notifications are required. Keep `OutboxDispatcher__Enabled=false` until the stack is reviewed and verified.
- Bus name is a required deployment parameter and is passed to the API as `EventBridge__BusName`; region uses `AWS_REGION`; the fixed source is `teampulse.identity`. No account-specific ARN or credential is committed. The stack attaches policy to an existing same-account role and does not create or alter its trust relationship.
- **Producer telemetry blocker:** The worker's `System.Diagnostics.Metrics` instruments are not exported to CloudWatch. The template therefore does not claim to alarm on oldest pending outbox age, quarantined rows or systemic publisher failures. Approve and implement a telemetry exporter, namespace/dimensions and thresholds before adding those alarms.
- **Consumer readiness blocker:** Habit, Challenge, Event Tracking, Insight and Notification status projections and their queue receive policies are not implemented. The shared stack intentionally grants no consumer read access. Do not enable production dispatch until all five consumers are ready.
- The bootstrap utility has not been executed against a production database; no claim is made that a production HR administrator account exists.
- Employee invitation/provisioning and credential activation remain deferred pending invitation delivery, token expiry and password setup decisions.
- Organization Admins have no direct employee profile/lifecycle write access. Their HR request-intake workflow is not defined. HR role catalogue/elevated-role assignment and HRIS service authentication are also deferred.
- Downstream employee-status projections and global revocation remain future work. Existing stateless JWTs may remain usable by receiving services until expiry.
- Retention defaults (90-day evidence, 12-month identifiable activity/participation, 24-month security/admin audit, up to 5-year anonymized aggregates) require jurisdiction-specific privacy/legal sign-off before production.
- This execution environment has no .NET SDK or Docker CLI. Local restore/build/test/Compose validation cannot be performed here; GitHub Actions is the verification environment.
- No unresolved initial MVP product questions remain. Dispatcher deployment resources are an infrastructure prerequisite, not a reason to hard-code a bus or credentials in application code.

## Verification evidence

### Latest verified code

- GitHub Actions run #84: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44698318341
- Verified code commit: `38c90810ff1fece5382c69d81fa025478d0a8ce6`.
- Run conclusion: success.
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: passed; 0 warnings, 0 errors.
- `docker compose config --quiet`: passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: 92 passed, 0 failed, 0 skipped (58 seconds).
- MySQL 8.4 Testcontainers integration tests passed, including persisted retry scheduling/due-time gating and crash-after-EventBridge-acceptance replay with the same eventId/envelope.
- Dispatcher acceptance tests passed for disabled/enabled options, configuration bounds, deterministic full-jitter boundaries, systemic/permanent/transient entry classification, inconsistent aggregate failure counts, missing per-entry event IDs and payload-free worker logging.
- No live AWS credentials or EventBridge account were used; publisher behavior is tested with a mocked AWS SDK client.
- Earlier attempts are retained in history: runs #81 and #82 failed during build because the retry test fixture contained over-escaped JSON; corrected in commit `d757910d28a612a20bb5c5d37c056bdafdd414f9`. Run #83 built cleanly but 2 of 92 tests exposed that the AWS SDK response's aggregate failure count may be absent; the publisher now checks the aggregate only when supplied, and run #84 verifies the fix.
- Run #80 remains the previous verified dispatcher-core baseline (64 passed, 0 failed/skipped, 0 warnings/errors); run #84 verifies the acceptance-test hardening and response-handling fix.


### Prior implementation increment — dispatcher core (historical checkpoint)

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

### This run — dispatcher acceptance-test hardening

- At run start, fetched live `feature/backend-foundation` HEAD `fce20738676ac466f6efc3d0beda705ac381439c`, inspected the recent commit history and recursive repository tree, and reconciled the checkpoint. The dispatcher core and metrics were already complete; the single unfinished task was acceptance-test hardening.
- Read `AGENTS.md`, `README.md`, this checkpoint, `docs/open-questions.md`, ADR-0001/0002, Identity ownership/specification, event envelope/catalog, employee-status projection, lifecycle SDD, dispatcher proposal/spec/design/tasks, implementation and existing tests.
- Added a deterministic full-jitter delay policy and wired the worker to use it. Hardened EventBridge response handling so a missing aggregate `FailedEntryCount` does not override valid per-entry responses, while a contradictory supplied count causes conservative batch retry without acknowledgement.
- Added tests for options validation, retry/backoff boundaries, EventBridge transient/systemic/permanent classification, aggregate/per-entry mismatch, payload-free structured logs, persisted retry due-time gating and crash-after-acceptance replay with the same eventId/envelope.
- Commits on the feature branch: `7bae0c99e5ca5c312f26b4e4004403d2b2ed751c` (acceptance tests and publisher guard), `64e4ef2598a5ef30c018019e845f127254780548` (retry scheduling/replay coverage), `d757910d28a612a20bb5c5d37c056bdafdd414f9` (correct escaped retry fixture), and `38c90810ff1fece5382c69d81fa025478d0a8ce6` (optional aggregate failure-count handling).
- CI run #81 failed at build because the initial fixture JSON was over-escaped; run #82 had the same build failure on the subsequent test commit. Run #83 built cleanly and passed Compose validation but reported 90 passed / 2 failed because the mocked AWS SDK response omitted the optional aggregate failure count. Both issues were corrected without rewriting history.
- CI run #84 on `38c90810ff1fece5382c69d81fa025478d0a8ce6` passed: restore, Release build (0 warnings/errors), Compose validation, and 92 tests (0 failed, 0 skipped; 58 seconds), including MySQL 8.4 Testcontainers.
- Local `dotnet --info` and `docker --version` are unavailable (exit 127); `git --version` is 2.47.3. Local restore/build/test/Compose commands were not run; CI run #84 is the executed verification.
- No AWS credentials, EventBridge bus, workload IAM role, target queues/DLQs or alarms were available, so no live AWS deployment or dispatcher enablement was attempted.

## Changed files in this acceptance-test hardening increment

- backend/src/Services/Identity/Identity.Infrastructure/Messaging/EventBridgePublisher.cs
- backend/src/Services/Identity/Identity.API/BackgroundServices/OutboxDispatcherWorker.cs
- backend/src/Services/Identity/Identity.API/BackgroundServices/OutboxRetryDelayPolicy.cs (new)
- backend/tests/Identity.UnitTests/OutboxDispatcherAcceptanceTests.cs (new)
- docs/changes/identity-outbox-eventbridge-dispatcher/tasks.md
- docs/changes/identity-outbox-eventbridge-dispatcher/spec.md
- docs/changes/identity-outbox-eventbridge-dispatcher/design.md
- docs/open-questions.md
- docs/project-status.md

### This run — parameterized Identity EventBridge infrastructure

- Reconciled live `feature/backend-foundation` HEAD `28693d45d2d556e1f3c20d1064b9257795b20f3a`, recent commits and recursive repository tree. The prior single next task was the AWS fan-out infrastructure SDD/template; no `infra/` or `deploy/` tree existed.
- Read AGENTS.md, README.md, project status, open questions, service ownership, ADR-0001/0002, communication conventions, Identity event envelope, employee-status projection contract, event catalog, dispatcher proposal/spec/design/tasks, Identity worker configuration and existing CI workflow.
- Created the infrastructure SDD package at `docs/changes/identity-eventbridge-infrastructure/`.
- Added `infra/cloudformation/identity-eventbridge.yaml`: required custom bus and existing Identity role parameters; `events:PutEvents` restricted to the bus; one schema-v1 lifecycle rule; five service-owned standard SQS targets; five EventBridge target-delivery DLQs; exact source-ARN/account queue policies; configurable retry/retention; queue-age/DLQ-depth/EventBridge DLQ-write alarms; optional SNS alarm actions.
- Queue read permissions and consumer-processing DLQs remain service-owned. The template does not create the Identity compute role/trust policy or any consumer. The worker remains disabled.
- Added a dedicated cfn-lint workflow, deployment README, event/communication contract updates, dispatcher task updates and open-question #18 for missing producer-side CloudWatch telemetry export.
- AWS documentation review confirmed custom EventBridge bus rule metrics use EventBusName and RuleName dimensions, and EventBridge targets support per-target SQS DLQs. References: https://docs.aws.amazon.com/eventbridge/latest/userguide/eb-monitoring.html and https://docs.aws.amazon.com/AWSCloudFormation/latest/TemplateReference/aws-resource-events-rule.html.
- Local .NET/Docker validation is not relevant to this IaC-only increment and no AWS credentials were available. The cfn-lint workflow is the static template validation gate; live CloudFormation deployment is not attempted.
- Changed files: `.github/workflows/cloudformation.yml`, `infra/cloudformation/identity-eventbridge.yaml`, `infra/cloudformation/README.md`, `docs/changes/identity-eventbridge-infrastructure/{proposal,spec,design,tasks}.md`, `README.md`, `docs/architecture/communication.md`, `docs/contracts/event-envelope.md`, `docs/changes/identity-outbox-eventbridge-dispatcher/{spec,tasks}.md`, `docs/open-questions.md`, `docs/project-status.md`.
- Static lint result is pending; no live AWS deployment or enablement is claimed.

## Single next task

Create the SDD for exporting Identity outbox Meter instruments to CloudWatch and provisioning producer-side alarms for oldest pending age, quarantined rows and systemic publisher failures. First approve the exporter/telemetry ownership, metric namespace/dimensions, least-privilege permissions and alert thresholds; keep the dispatcher disabled.
