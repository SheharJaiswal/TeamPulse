# Project Status
Updated: 2026-11-20

## Continuation checkpoint

- Active branch: `feature/backend-foundation` only.
- Live branch HEAD reconciled at run start: `f490cbe53288e15d953d61acd8fbd408fbb9fb0f`. Recent history and the working tree were inspected. The previous checkpoint was one documentation commit behind the live branch.
- This run's final verified implementation/CI commit: `b26eb0710c1708fb0bbb027fb349636daa4c3a35` (GitHub Actions .NET CI run #93). The documentation checkpoint commit will be a docs-only child of this verified commit.
- SDD: `docs/changes/identity-local-rabbitmq-transport/` is implemented and CI-verified. SDD commit: `91def68254b2cbf1937267e3d706f659921e8d6a`.
- Implementation: `RabbitMQ.Client` 7.2.2 adapter, explicit `RabbitMq` / `EventBridge` provider selection, durable topic exchange, publisher confirms, mandatory routing, persistent envelope messages and systemic retry outcomes are committed.
- Local runtime: Docker Compose starts RabbitMQ 4.1 management with the imported exchange and five durable receiving-service queues. The API dispatcher remains disabled by default; local runs explicitly set `OutboxDispatcher__Enabled=true` and `OutboxDispatcher__Transport=RabbitMq`.
- Production architecture is unchanged: EventBridge remains the intended production transport. The owner will provision it during cloud infrastructure setup. Do not attempt AWS deployment, ask for AWS credentials or build CloudWatch exporters until that setup.
- Completed baseline: Identity HR lifecycle and transactional MySQL outbox; lease-safe dispatcher; EventBridge production adapter and CloudFormation fan-out template; local RabbitMQ adapter/topology; unit, MySQL and RabbitMQ Testcontainers coverage.
- In progress: no active implementation task in the local transport change. Five downstream consumer projects/projections are not implemented yet.
- Blockers/deferred: EventBridge provisioning and producer CloudWatch exporter/alarms are intentionally deferred with cloud infrastructure and do not block local development. Privacy/legal retention sign-off remains a production prerequisite. Employee invitation, HRIS, role mutation and Organization Admin request intake remain separately unresolved.
- Single next task: create the SDD for the first downstream consumer, Habit's version-aware Employee Status Projection consumer, limited to the existing `docs/contracts/employee-status-projection.md` contract and the Habit-owned persistence boundary. Do not invent Habit business behavior or global token-revocation guarantees.
- Resume rule: fetch the live branch HEAD and reconcile this checkpoint before every run; continue from the exact committed files and tests without repeating the completed RabbitMQ or dispatcher work.

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
- [x] Added seven Python CloudFormation contract tests for exact queue/DLQ counts, paired event-type filtering, scoped publisher/queue policies, encryption, alarm coverage and absence of consumer read permissions.
- [x] CloudFormation workflow run #2 passed cfn-lint 1.61.0 and all seven contract tests; .NET CI run #86 passed restore, Release build, Compose validation and 92 tests.

- [x] Created local RabbitMQ transport SDD (proposal/spec/design/tasks) in `docs/changes/identity-local-rabbitmq-transport/`; EventBridge deployment explicitly deferred by the owner.
- [x] Added transport selection to Identity outbox dispatch. `EventBridge` remains the code default for deployment compatibility; local development explicitly selects `RabbitMq`; the dispatcher remains disabled in checked-in appsettings.
- [x] Added RabbitMQ.Client 7.2.2 publisher with a serialized reusable connection/channel, automatic recovery, durable topic exchange, persistent envelope body, message/correlation/type/schema metadata, publisher-confirm tracking and mandatory routing. Broker failures remain systemic/retryable and do not mark outbox rows published.
- [x] Added local RabbitMQ 4.1 Compose service, health check, persistent volume, imported topology and five durable queues for Habit, Challenge, Event Tracking, Insight and Notification.
- [x] Added RabbitMQ Testcontainers integration tests for confirmed fan-out to all five queues, envelope/AMQP metadata preservation and unroutable publication, plus RabbitMQ configuration tests.
- [x] Added four Python topology contract tests and CI verification that starts the actual Compose RabbitMQ service and checks the exchange and all five queues.
- [x] Updated README local run instructions, communication architecture and event-envelope transport mapping. Updated open questions to defer EventBridge provisioning and CloudWatch telemetry until cloud setup.
- [x] .NET CI run #93 passed restore, Release build (0 warnings/errors), Compose validation, actual RabbitMQ Compose startup/topology checks, four Python topology tests and 100 .NET tests (0 failed, 0 skipped; 1m09s). MySQL 8.4 and RabbitMQ Testcontainers tests passed.

## In progress

- No active implementation task remains in the local RabbitMQ transport increment; SDD, implementation, documentation and CI verification are complete.
- Habit, Challenge, Event Tracking, Insight and Notification still need their own version-aware employee-status projections and consumers. The local queues exist, but no downstream consumer is implemented yet.
- EventBridge remains the intended production transport; live AWS deployment is deferred until the owner provisions cloud infrastructure.

## Blockers and decisions

- **EventBridge deployment — intentionally deferred by owner:** The CloudFormation template and production EventBridge adapter are committed and CI-verified. Do not deploy AWS resources, request credentials or make EventBridge a prerequisite for local development. The owner will configure EventBridge during cloud infrastructure setup. The local RabbitMQ adapter is available meanwhile.
- **Producer CloudWatch telemetry — deferred with cloud infrastructure:** Identity emits .NET Meter instruments but no CloudWatch/OpenTelemetry exporter is configured. Add exporter, namespace/dimensions, least-privilege permissions and producer-side alarms only during/after AWS setup.
- **Downstream projection rollout — implementation not started:** The five service queues are seeded locally, but no Habit/Challenge/Event Tracking/Insight/Notification consumers or local projections exist. Do not claim global immediate token revocation. The next increment starts with Habit.
- **Retention/legal sign-off — production blocker:** ADR-0002 retention defaults still require jurisdiction-specific privacy/legal approval.
- Employee invitation/provisioning, HRIS integration, role mutation and Organization Admin request intake remain deferred pending the decisions in `docs/open-questions.md`.
- The bootstrap utility has not been executed against a production database; no claim is made that a production HR administrator account exists.
- This execution environment has no local .NET SDK or Docker CLI. GitHub Actions ran all required .NET, Docker Compose, RabbitMQ and MySQL checks; no local build/test/Compose success is claimed.
- No AWS deployment, IAM simulation, EventBridge/SQS live-delivery test or CloudWatch export test was performed or required for this increment.

## Verification evidence

### Local RabbitMQ transport — implementation and CI verification

- Start-of-run live HEAD: `f490cbe53288e15d953d61acd8fbd408fbb9fb0f`. Reconciled the recent commits, repository tree, `AGENTS.md`, `README.md`, this status file, open questions, service ownership, communication conventions, Identity/event contracts and existing outbox implementation/tests.
- SDD commit: `91def68254b2cbf1937267e3d706f659921e8d6a`.
- Initial implementation commit: `4e4512e83c221e6e22980fe8084acb2a9acf1966`. CI run #91 (44714606365) caught one compile error: RabbitMQ.Client 7.2.2 exposes `BasicProperties.DeliveryMode` as `DeliveryModes`, not `int`. Corrected to `DeliveryModes.Persistent` in `4e923bd96d727caa4f1c067ce8adaa5d714698d6`.
- Documentation commit: `35efa2221abca851d7e2365bff7f0f88f3ffd27f`.
- CI topology-verification commit: `b26eb0710c1708fb0bbb027fb349636daa4c3a35`.
- CI run #92: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44714787175 — passed restore, Release build, Docker Compose config, four Python topology tests and 100 .NET tests (0 failed, 0 skipped; 54 seconds).
- CI run #93: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44715089935 — passed all checks on the final verified code/workflow commit `b26eb0710c1708fb0bbb027fb349636daa4c3a35`.
- Exact executed commands:
  - `dotnet restore backend/TeamPulse.sln` — passed.
  - `dotnet build backend/TeamPulse.sln --configuration Release --no-restore` — passed, 0 warnings, 0 errors.
  - `docker compose config --quiet` — passed.
  - `docker compose up -d --wait --wait-timeout 90 rabbitmq` — passed; Compose health check succeeded.
  - `docker compose exec -T rabbitmq rabbitmqctl list_queues name --no-table-headers` — executed; CI asserted all five expected queue names.
  - `docker compose exec -T rabbitmq rabbitmqctl list_exchanges name type --no-table-headers` — executed; CI asserted `teampulse.domain.events` exists.
  - `python -m unittest discover -s infra/rabbitmq/tests -v` — 4 passed, 0 failed.
  - `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"` — 100 passed, 0 failed, 0 skipped (1m09s in run #93). MySQL 8.4 and RabbitMQ Testcontainers integration tests passed.
- Run #91's compile failure is retained in history and was fixed in a separate commit; run #92 and #93 passed. The latest workflow verifies both the real local Compose topology and the .NET publisher against a disposable RabbitMQ Testcontainer.
- No EF Core model change or migration was required. No AWS credentials or live EventBridge resources were used.

### Latest verified code and infrastructure

- .NET CI run #86: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44705631388
- Verified commit: `1f85ef9f3e65ef9047dfb6720bb61b88b9dba818`.
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: passed, 0 warnings and 0 errors.
- `docker compose config --quiet`: passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: 92 passed, 0 failed, 0 skipped (53 seconds). MySQL 8.4 Testcontainers integration tests passed.
- CloudFormation lint run #2: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44705631530
- `python -m pip install --upgrade pip cfn-lint`: passed; cfn-lint 1.61.0 installed.
- `cfn-lint --non-zero-exit-code warning infra/cloudformation/identity-eventbridge.yaml`: passed with no diagnostics.
- `python -m unittest discover -s infra/cloudformation/tests -v`: 7 passed, 0 failed (0.052 seconds).
- Earlier CloudFormation run #1 also passed cfn-lint before the event-pairing hardening.
- No AWS CloudFormation deployment, IAM simulation or live EventBridge/SQS delivery test was performed; AWS account/credentials and the existing workload role are not available. Dispatcher remains disabled.

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
- Changed files: `.github/workflows/cloudformation.yml`, `infra/cloudformation/identity-eventbridge.yaml`, `infra/cloudformation/README.md`, `docs/changes/identity-eventbridge-infrastructure/{proposal,spec,design,tasks}.md`, `README.md`, `docs/architecture/communication.md`, `docs/contracts/event-envelope.md`, `docs/changes/identity-outbox-eventbridge-dispatcher/{spec,tasks}.md`, `infra/cloudformation/tests/test_template_contract.py`, `docs/open-questions.md`, `docs/project-status.md`.
- CloudFormation workflow run #1 (44705245155) passed cfn-lint 1.61.0. After adding exact `$or` event-type pairing and seven resource/security contract tests, run #2 (44705631530) passed cfn-lint and all seven tests.
- Infrastructure commits: `4ccf753f4086097a6af12302c52f68688c7632e7` (stack/SDD) and `1f85ef9f3e65ef9047dfb6720bb61b88b9dba818` (exact event pairing and contract tests). Checkpoint documentation commit: `30cb9a57bfa78614199fec355c11e1e2fc2b2f96`.
- .NET CI run #86 (44705631388) on commit `1f85ef9f3e65ef9047dfb6720bb61b88b9dba818` passed `dotnet restore backend/TeamPulse.sln`, `dotnet build backend/TeamPulse.sln --configuration Release --no-restore` (0 warnings, 0 errors), `docker compose config --quiet`, and `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"` (92 passed, 0 failed, 0 skipped; 53 seconds). MySQL 8.4 Testcontainers integration tests passed.
- CloudFormation run #2 executed `python -m pip install --upgrade pip cfn-lint`, `cfn-lint --non-zero-exit-code warning infra/cloudformation/identity-eventbridge.yaml`, and `python -m unittest discover -s infra/cloudformation/tests -v` (7 passed; 0 failed; 0.052 seconds).

## Changed files in this local RabbitMQ increment

- `.github/workflows/dotnet.yml`
- `docker-compose.yml`
- `README.md`
- `backend/src/Services/Identity/Identity.API/Program.cs`
- `backend/src/Services/Identity/Identity.API/BackgroundServices/OutboxDispatcherWorker.cs`
- `backend/src/Services/Identity/Identity.API/appsettings.json`
- `backend/src/Services/Identity/Identity.Infrastructure/Identity.Infrastructure.csproj`
- `backend/src/Services/Identity/Identity.Infrastructure/Messaging/RabbitMqOptions.cs`
- `backend/src/Services/Identity/Identity.Infrastructure/Messaging/RabbitMqPublisher.cs`
- `backend/tests/Identity.UnitTests/Identity.UnitTests.csproj`
- `backend/tests/Identity.UnitTests/RabbitMqPublisherIntegrationTests.cs`
- `infra/rabbitmq/rabbitmq.conf`
- `infra/rabbitmq/definitions.json`
- `infra/rabbitmq/tests/test_definitions.py`
- `docs/architecture/communication.md`
- `docs/contracts/event-envelope.md`
- `docs/changes/identity-local-rabbitmq-transport/{proposal,spec,design,tasks}.md`
- `docs/open-questions.md`
- `docs/project-status.md`

## Single next task

Create the SDD for Habit's first downstream consumer of the versioned Employee Status Projection contract, then implement only the projection persistence and RabbitMQ consumer behavior required by that contract. Preserve Habit ownership, idempotent eventId handling, strict lifecycleVersion ordering and atomic projection/deduplication. Do not add unrelated Habit business features or invent global token-revocation guarantees.
