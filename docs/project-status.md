# Project Status
Updated: 2026-11-20

## Continuation checkpoint

- Active branch: `feature/backend-foundation` only.
- Start-of-run live HEAD: `3bb9d715d2abad78747c51da9fee3ab39c16f10c`. The live branch ref, recent commits, recursive tree, AGENTS.md, README.md, this checkpoint, open questions, service ownership, communication conventions, approved Habit SDD and event/projection contracts were fetched and reconciled. The prior checkpoint's recorded start HEAD was stale (`bf0c09e`); the actual live HEAD matched the prior final commit and the next task was unchanged.
- Approved SDD: `docs/changes/habit-employee-status-projection-consumer/`. Habit projection/persistence core was already implemented and was not recreated.
- Completed this run: Habit RabbitMQ.Client 7.2.2 BackgroundService; disabled-by-default configuration and validation; scoped transport-independent processor invocation; prefetch 1/manual ack after processor return; bounded full-jitter retries including EF Core/MySQL/network inner-exception chains; poison/exhaustion DLQ routing; channel close on ack/nack failure; explicit reconnect loop; separate `/health/consumer`; Habit quorum queue (delivery limit 5), at-least-once DLX and durable quorum DLQ; README and communication docs; eight Python topology contract tests; CI queue/exchange checks; RabbitMQ Testcontainers tests for ack-after-processor-completion, invalid-message DLQ, transient retry success/exhaustion and startup reconnect.
- Implementation commits: `b8ce918` (consumer/topology), `7dc7323` (health-state constructor in tests), `212af2d` (ephemeral host-port binding in reconnect test), `29a49bb` (retry classifier unwraps EF Core/provider exception chains).
- CI history: run #94 failed because the first test constructor omitted the new consumer status; fixed in `7dc7323`. Run #95 failed because Testcontainers port-binding arguments caused a host port 5672 collision; fixed in `212af2d`. Run #96 passed 123 .NET tests and eight topology tests. Run #97 passed again with the wrapped transient exception test, confirming EF-style exception chains are retried.
- In progress: the consumer implementation and current acceptance tests are verified. One dedicated Testcontainers scenario remains: lose the broker connection after an established session while a message is unacknowledged, then verify broker redelivery and idempotent processing after reconnect. Keep the corresponding SDD verification task unchecked until this scenario passes.
- Blockers/deferred: no business-rule blocker for local RabbitMQ. EventBridge provisioning, SQS consumer adapter and CloudWatch export remain deferred until the owner configures cloud infrastructure. Privacy/legal retention sign-off remains a production prerequisite. Existing RabbitMQ volumes created with the previous classic Habit queue may require a deliberate disposable-volume recreation; never delete a volume containing messages/data the owner needs.
- Single next task: add and verify the established-session broker-loss/unacknowledged-message redelivery Testcontainers test. After it passes, mark the Habit consumer SDD complete and select the next unfinished service-owned employee-status projection. Do not repeat Habit persistence, Identity publisher, current consumer implementation or completed CI.
- Verification: CI run #97 (`44739492656`) passed `dotnet restore backend/TeamPulse.sln`, Release build (0 warnings/errors), `docker compose config --quiet`, actual RabbitMQ Compose startup and queue/exchange checks, `python -m unittest discover -s infra/rabbitmq/tests -v` (8 passed), and `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"` (123 passed: 23 Habit, 100 Identity; 0 failed/skipped). Habit MySQL 8.4 and RabbitMQ Testcontainers tests passed.
- Local execution: `dotnet --info` and `docker --version` are unavailable in the current execution environment (exit 127); no local .NET/Docker success is claimed. GitHub Actions is the verification source.
- Changed files: `backend/src/Services/Habit/Habit.Infrastructure/Messaging/HabitRabbitMqConsumerOptions.cs`, `HabitRabbitMqConsumer.cs`, `HabitRabbitMqConsumerStatus.cs`, `backend/src/Services/Habit/Habit.Infrastructure/Habit.Infrastructure.csproj`, `backend/src/Services/Habit/Habit.API/Program.cs`, `appsettings.json`, `HealthChecks/HabitRabbitMqConsumerHealthCheck.cs`, `backend/tests/Habit.UnitTests/Habit.UnitTests.csproj`, `HabitRabbitMqConsumerIntegrationTests.cs`, `HabitRabbitMqConsumerReconnectTests.cs`, `infra/rabbitmq/definitions.json`, `infra/rabbitmq/tests/test_definitions.py`, `.github/workflows/dotnet.yml`, `README.md`, `docs/architecture/communication.md`, this SDD tasks file and this checkpoint.
- Resume rule: fetch the live branch before every run and continue from the single next task above.


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
- [x] Created the Habit Employee Status Projection consumer SDD (proposal/spec/design/tasks), deriving behavior only from the existing projection/event contracts; the Habit application/persistence core is implemented and CI-verified, with the RabbitMQ consumer remaining in progress.
- [x] Added four Python topology contract tests and CI verification that starts the actual Compose RabbitMQ service and checks the exchange and all five queues.
- [x] Updated README local run instructions, communication architecture and event-envelope transport mapping. Updated open questions to defer EventBridge provisioning and CloudWatch telemetry until cloud setup.
- [x] Earlier .NET CI run `44715089935` passed restore, Release build, Compose validation, actual RabbitMQ Compose startup/topology checks, four Python topology tests and 100 .NET tests (0 failed, 0 skipped).

## In progress

- Habit application, projection and MySQL persistence foundation are implemented and CI-verified. The local RabbitMQ consumer, manual acknowledgements, bounded retry/reconnect and Habit-owned DLQ are the single active implementation task.
- Challenge, Event Tracking, Insight and Notification still need their own service-owned employee-status consumers/projections.
- EventBridge remains the intended production transport; live AWS provisioning is deferred until the owner sets up cloud infrastructure.

## Blockers and decisions

- **EventBridge deployment — intentionally deferred by owner:** The CloudFormation template and production EventBridge adapter are committed and CI-verified. Do not deploy AWS resources, request credentials or make EventBridge a prerequisite for local development. The owner will configure EventBridge during cloud infrastructure setup. The local RabbitMQ adapter is available meanwhile.
- **Producer CloudWatch telemetry — deferred with cloud infrastructure:** Identity emits .NET Meter instruments but no CloudWatch/OpenTelemetry exporter is configured. Add exporter, namespace/dimensions, least-privilege permissions and producer-side alarms only during/after AWS setup.
- **Downstream projection rollout — in progress:** Habit's version-aware projection/inbox persistence core is implemented; its RabbitMQ consumer is the next task. Challenge, Event Tracking, Insight and Notification consumers remain unimplemented. Do not claim global immediate token revocation.
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

## This run — Habit Employee Status Projection consumer SDD

- Start-of-run live branch HEAD: `845e4f5b85a94ab549e5d02446b735dfd42f3b30`. Fetched the live branch ref, last 10 commits, recursive tree and required instructions, README, project checkpoint, open questions, service ownership, communication conventions, event envelope, employee-status projection contract, Identity specifications and local RabbitMQ implementation/tests.
- Confirmed the repository has no Habit project yet; only Identity exists in the backend solution. The local RabbitMQ Habit queue is already durable and bound to the Identity lifecycle routing key. The existing contract defines only employeeId, isActive, lifecycleVersion and updatedAtUtc; no profile or wellness fields are needed.
- Created and committed `docs/changes/habit-employee-status-projection-consumer/{proposal,spec,design,tasks}.md` in commit `775be1f3fe5767ba84a5de70ba11492d25f70588`.
- The SDD defines a Habit-owned `teampulse_habit` MySQL database, EF Core 8/Pomelo startup migrations, a minimal processed-event inbox, atomic conditional projection updates, eventId deduplication, strict lifecycleVersion ordering, local RabbitMQ manual acknowledgements, bounded retries and a Habit-owned quorum-queue DLQ.
- The application processor is transport-independent so a future SQS adapter can reuse it. AWS/SQS, Habit product APIs, other consumers and global token revocation are explicitly out of scope.
- Artifact checks executed during authoring: all four SDD files present and non-empty; specification includes HESP-14 acceptance mapping; design includes the required Database.MigrateAsync startup behavior. These checks passed.
- No .NET build, test or Docker Compose command was run because this increment changes documentation only. The existing .NET workflow explicitly ignores `docs/**` and README-only changes, so no CI run was triggered or claimed. The latest code verification remains run #93 (100 .NET tests passed; four RabbitMQ topology tests passed) on commit `b26eb0710c1708fb0bbb027fb349636daa4c3a35`.
- Changed files: `docs/changes/habit-employee-status-projection-consumer/{proposal,spec,design,tasks}.md` and `docs/project-status.md`. `docs/open-questions.md` was not changed because no new business rule was required by the approved projection contract.
- SDD commit: `775be1f3fe5767ba84a5de70ba11492d25f70588`. The following documentation checkpoint commit records this SHA and the single next task.

### Historical checkpoint after Habit SDD (superseded)

At the end of the SDD-only increment, the next task was the full Habit consumer implementation. The current run completed the application/persistence portion; the active next task is now the RabbitMQ consumer adapter as recorded in the continuation checkpoint.



### Final CI verification — Habit projection core

- Final verified implementation commit: `16f498a69840d107944cc77a24e27137684583d5`.
- GitHub Actions .NET 8 CI run #93: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44731041383 — success.
- Exact executed commands and results:
  - `dotnet restore backend/TeamPulse.sln` — passed.
  - `dotnet build backend/TeamPulse.sln --configuration Release --no-restore` — passed; 0 warnings, 0 errors.
  - `docker compose config --quiet` — passed.
  - `docker compose up -d --wait --wait-timeout 90 rabbitmq` — passed; broker health check succeeded.
  - `docker compose exec -T rabbitmq rabbitmqctl list_queues name --no-table-headers` — executed; workflow asserted all five existing service queues.
  - `docker compose exec -T rabbitmq rabbitmqctl list_exchanges name type --no-table-headers` — executed; workflow asserted `teampulse.domain.events`.
  - `python -m unittest discover -s infra/rabbitmq/tests -v` — 4 passed.
  - `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"` — 112 passed, 0 failed, 0 skipped (12 Habit, 100 Identity). Habit MySQL 8.4 Testcontainers migration/repeat, rollback, eventId duplicate, stale/equal and concurrent N/N+1 tests passed.
- CI correction history: initial core commit `a17bae7` failed build because Habit.API lacked the configuration namespace for `GetConnectionString`; fixed in `2e7b96d`. The next successful build exposed one MSB3277 EF Relational 8.0.2/8.0.11 assembly conflict; aligned the direct relational dependency to 8.0.11 in `97ae87c`. Final run #93 passed with zero warnings/errors. Added rollback test and projection-reader assertion in `16f498a`, verified by the final run.
- Local execution environment: `dotnet --info` — exit 127 (dotnet not installed); `docker --version` — exit 127 (Docker CLI not installed); `git --version` — 2.47.3. CI is the source of build/test/Compose evidence for this run.
- No Habit RabbitMQ consumer, Habit DLQ, SQS/EventBridge adapter or AWS deployment is claimed complete. Those remain the single next implementation increment and deferred cloud work respectively.


## Verification result — Habit RabbitMQ consumer

- CI run #97: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44739492656
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: passed, 0 warnings and 0 errors.
- `docker compose config --quiet`: passed.
- `docker compose up -d --wait --wait-timeout 90 rabbitmq`: passed; Habit quorum queue, Habit DLQ, domain exchange and Habit DLX were present.
- `python -m unittest discover -s infra/rabbitmq/tests -v`: 8 passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: 123 passed (23 Habit, 100 Identity), 0 failed, 0 skipped. Habit MySQL 8.4 and RabbitMQ Testcontainers tests passed.
- The wrapped-transient test throws an EF-style `InvalidOperationException` with a `TimeoutException` inner exception and verifies the retry path.


## Current continuation checkpoint — established-session Habit broker-loss redelivery

- Start-of-run live branch HEAD: `d5acf601f1ad9d2dafbd8e059a5171be8dea8a20`. Fetched the branch ref, recent commits, recursive repository tree and reconciled this checkpoint before selecting work.
- Read `AGENTS.md`, `README.md`, `docs/project-status.md`, `docs/open-questions.md`, the approved Habit consumer SDD, service ownership, communication conventions, employee-status projection contract, Habit consumer implementation, RabbitMQ topology, existing unit/MySQL/RabbitMQ tests and CI workflow.
- Confirmed the single unfinished Habit acceptance item was HESP-11's established-session broker-loss scenario. Existing coverage already verified startup reconnect, ack-after-processor-completion, poison-message DLQ routing, transient retry and retry exhaustion; those tests were not recreated.
- Added `backend/tests/Habit.UnitTests/HabitRabbitMqBrokerLossIntegrationTests.cs`. The test starts the real Habit API against the existing MySQL 8.4 Testcontainers fixture (thereby applying the checked-in migration), publishes one valid lifecycle event to a durable quorum queue, and uses a test decorator that waits until the real application processor has committed its inbox/projection transaction before pausing acknowledgement.
- The test stops RabbitMQ after the commit but before ack, waits for the consumer session to observe disconnection, allows the first ack attempt to fail, restarts the same broker container, and verifies the established consumer reconnects and processes the same message again. The real processor must return `Duplicate` for the same `eventId`; the test verifies one inbox row and the unchanged inactive/version-1 projection.
- Updated `docs/changes/habit-employee-status-projection-consumer/tasks.md` to map this scenario to HESP-11. No production code or business contract was changed.
- In progress: GitHub Actions verification of the new broker-loss Testcontainers scenario and the full solution.
- Blockers: none for local RabbitMQ/MySQL development. AWS EventBridge/SQS and CloudWatch remain deferred until the owner configures cloud infrastructure; they are not prerequisites for this work.
- Next task while verification is pending: complete the CI run for this exact commit, fix only any failure revealed by that run, and record the final result. Once verified, the next development task is the Challenge employee-status projection SDD.
- Local execution limitation remains: this environment has no `.NET` SDK or Docker CLI; do not claim local build/test/Compose results. Use the Docker-capable GitHub Actions workflow as the verification source.


### CI correction — first established-session broker-loss attempt

- GitHub Actions run #98: https://github.com/SheharJaiswal/TeamPulse/actions/runs/44747085459.
- `dotnet restore backend/TeamPulse.sln`: passed.
- `dotnet build backend/TeamPulse.sln --configuration Release --no-restore`: build succeeded but emitted one nullable warning (CS8602) in the new test at the post-replay consumer-status assertion; the next increment removes it.
- `docker compose config --quiet`: passed.
- `docker compose up -d --wait --wait-timeout 90 rabbitmq`: passed; the shared local broker started and all required queues/exchanges were present.
- `python -m unittest discover -s infra/rabbitmq/tests -v`: 8 passed.
- `dotnet test backend/TeamPulse.sln --configuration Release --no-build --logger "trx;LogFileName=teampulse-tests.trx"`: 123 passed, 1 failed, 0 skipped (Identity 100/100; Habit 23/24). The new established-session test failed only at the post-restart reconnect wait; the first database commit, broker-loss detection and acknowledgement gate were reached.
- Corrective attempt: bind the RabbitMQ test container to a stable host port (following the existing startup-reconnect test pattern), reuse the same URI across stop/start, and assert the mapped URI remains stable. Keep HESP-11 unchecked until the corrected workflow passes. This is a test-harness correction; no production consumer behavior is changed without evidence.
- The test is not considered complete yet. The single next task remains to pass the established-session broker-loss test and full CI, then record the verified result.
