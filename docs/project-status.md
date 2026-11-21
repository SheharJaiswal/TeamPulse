## Identity correlation-ID empty-value boundary regression — implementation added; CI pending

- Strengthened ILC-20 beyond the HTTP-host whitespace test with a controller-level regression for a header key that is present but whose `StringValues` has zero parsed values. This reproduces the exact ambiguity that originally caused the empty header to be mistaken for an absent header.
- The test asserts the header key remains present, the parsed value count is zero, the endpoint returns HTTP 400, and the lifecycle service is never invoked.
- Added `EmployeeLifecycleControllerTests.Deactivate_RejectsPresentEmptyCorrelationHeaderEvenWhenItHasZeroParsedValues`. No production code or API behavior changed in this increment; CI verification is pending.
- No schema, queue topology, persistent broker data or AWS resource changed.

Updated: 2026-11-21

## Identity lifecycle correlation-ID validation — CI #241 passed

- CI #241 passed against correction commit `68dae7896fb1cb264f7792c731b0558e3419e16a`. The corrected controller distinguishes an absent X-Correlation-ID header from a present empty/whitespace value; only absence generates a UUID, while a present malformed value returns HTTP 400 before mutation.
- ILC-20 passed in Identity **109/109**. Full .NET results: Identity 109, Habit 24, Challenge 57, Event Tracking 27, Notification 28 and Insight 15, plus Insight.QueueMigration.IntegrationTests 8 — **268 passed, 0 failed, 0 skipped**.
- Restore, Release build, Compose validation, RabbitMQ startup/topology, all 19 Python topology contract tests, both pinned Testcontainers image pulls and test-results artifact upload passed. Release build reported 3 warnings and 0 errors.
- [CI #241](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44904270358). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44904270358/artifacts/14706578896).
- No production schema, queue topology, persistent broker data or AWS resource changed in this increment.

Updated: 2026-11-21

## Identity correlation-ID replay and empty-header validation — CI #239 passed; ILC-20 later verified by CI #241

- CI #239 passed for test commit `dd1c2a310c4bd70ba86499e5ad4349a6097b2812`. ILC-19 verified same-command idempotent replay preserves the original event/correlation IDs even when the retry supplies a different correlation ID.
- Full .NET results: Identity 108/108, Habit 24/24, Challenge 57/57, Event Tracking 27/27, Notification 28/28, Insight 15/15 and Insight.QueueMigration.IntegrationTests 8/8 — **267 passed, 0 failed, 0 skipped**.
- All 19 Python RabbitMQ topology tests, restore, Release build, Compose validation, RabbitMQ startup/topology, both pinned Testcontainers image pulls and test-result artifact upload passed. Release build reported 3 warnings and 0 errors.
- [CI #239](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44903148180). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44903148180/artifacts/14706841604).
- Fixed a header-validation edge case: Identity now generates X-Correlation-ID only when the header is absent; an explicitly supplied empty value is rejected as malformed. Added a MySQL API-host test verifying HTTP 400 and no employee, audit or outbox mutation.
- Added ILC-20 to the lifecycle SDD. Controller commit: `62b84ee4a68c9fbca1b48c8329a029b6183dc4ee`; test commit: `df805822b958cae6cd9580af019ca3cbf07d2558`. New CI verification is pending.
- No schema, queue topology, persistent broker data or AWS resources changed.

Updated: 2026-11-21

## Identity correlation-ID replay semantics — CI #238 passed; ILC-19 pending verification

- CI #238 passed for test commit `ff9c87b53c8943ed0719528b280304ac2e5199db`. ILC-17 and ILC-18 verified supplied and generated correlation-ID propagation through the response, audit, outbox and event envelope.
- Full .NET results: Identity 107/107, Habit 24/24, Challenge 57/57, Event Tracking 27/27, Notification 28/28, Insight 15/15 and Insight.QueueMigration.IntegrationTests 8/8 — **266 passed, 0 failed, 0 skipped**.
- Restore, Release build, Docker Compose validation, RabbitMQ startup/topology, all 19 Python topology contract tests and both pinned Testcontainers image pulls passed. Build reported 3 warnings and 0 errors.
- [CI #238](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44902665384). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44902665384/artifacts/14706737222).
- Added ILC-19 and a sequential MySQL API-host test for same-command idempotent replay with a different retry correlation ID. The replay must preserve the original event/correlation IDs and produce only one audit/outbox row.
- Implementation commit: `dd1c2a310c4bd70ba86499e5ad4349a6097b2812`; specification commit: `9d4a7130dbd4dc745611cda4356d73f199e47318`; task evidence commit: `c5de9fc11f3d3eac4abb359073e49250fa7d9ec5`.
- CI #239 is running against test commit `dd1c2a310c4bd70ba86499e5ad4349a6097b2812`: [workflow](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44903148180).
- This increment changes test coverage and SDD/status documentation only; no production behavior, database schema, persistent RabbitMQ topology/data or AWS resource changed.

Updated: 2026-11-21

## Identity JWT subject validation and CI Testcontainers image setup — 2026-11-21

- Added ILC-16 API-host regression coverage: correctly signed HrAdmin tokens with a missing `sub` or a non-GUID `sub` are rejected with HTTP 401 before mutation. The test verifies the seeded employee remains active at lifecycle version 0 and that no audit or correlation-scoped outbox rows are created.
- CI #236 Identity suite passed **105/105**, including ILC-16. The overall workflow failed only in `Insight.QueueMigration.IntegrationTests`: its `rabbitmq:4.0-alpine` Testcontainers image had not been pre-pulled on the runner (7/8 tests passed). Restore, Release build, Compose validation, RabbitMQ topology checks and all 19 Python topology tests passed.
- Added an explicit CI step to pull `rabbitmq:4.0-alpine` before .NET tests. Workflow fix commit: `819242fe43a446fe1574e3025458d12a14331188`. Verification of this correction is pending.
- [CI #236](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44901781762).
- No production application code, database schema, persistent RabbitMQ topology/data, or AWS resource was changed by this CI correction.
- Next: verify the workflow image pre-pull correction in CI, then continue with correlation-ID propagation/generation integration coverage.

Updated: 2026-11-21

## Identity idempotency-key character boundary tests — implementation pending

- Next focused regression: accept one-character and 128-character visible-ASCII idempotency keys; reject control, DEL and non-ASCII characters before any lifecycle mutation.
- Acceptance criterion ILC-15 is recorded in the Identity lifecycle specification. Implementation and CI verification are pending.

Updated: 2026-11-21

## Identity lifecycle header-validation tests — CI #233 passed

- Added API-host integration coverage for whitespace-only and overlength idempotency keys, repeated idempotency headers and repeated correlation headers. Rejected requests leave employee state/version unchanged, create no lifecycle audit rows and create no outbox rows for the supplied correlation IDs.
- Added specification acceptance criterion ILC-14. The complete header-validation regression passed in the Identity MySQL API-host suite.
- CI #233 passed against code commit `d7dc8b8663e28e25c9beec395acd0ae48c628185`: restore, Release build, Compose validation, RabbitMQ startup/topology, all 19 Python topology contract tests, pinned DynamoDB Local image pull and the complete .NET suite passed.
- Exact .NET results: Identity 103/103, Habit 24/24, Challenge 57/57, Event Tracking 27/27, Notification 28/28, Insight 15/15 and Insight.QueueMigration.IntegrationTests 8/8 — **262 passed, 0 failed, 0 skipped**.
- [CI #233](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44900792522). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44900792522/artifacts/14706141699).
- This increment changed tests and SDD documentation only; no production code, schema, queue topology, persistent broker or AWS resource was changed.

Updated: 2026-11-21

## Identity lifecycle request-validation boundary tests — CI #231 passed

- Added MySQL API-host integration coverage for blank and overlength reason codes, overlength notes, and the guarantee that rejected requests do not mutate employee status, lifecycle audit or outbox rows.
- Added a maximum-boundary success case: a 100-character reason code and 1,000-character note are accepted; reason-code surrounding whitespace is normalized before persistence.
- Tests are isolated using a dedicated correlation ID for outbox non-mutation assertions, avoiding assumptions about other test rows in the shared MySQL fixture.
- GitHub Actions CI #231 passed for code commit `55c192484534a54dd2aad62ccd2f1c0d26ab3774`: restore, Release build, Compose validation, RabbitMQ startup/topology, all 19 Python topology contract tests, pinned DynamoDB Local image pull and the full .NET suite passed.
- Exact .NET results: Identity 102/102, Habit 24/24, Challenge 57/57, Event Tracking 27/27, Notification 28/28, Insight 15/15 and Insight.QueueMigration.IntegrationTests 8/8 — **261 passed, 0 failed, 0 skipped**.
- [CI #231](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44900300510). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44900300510/artifacts/14706002282).
- No production code, schema, queue topology, persistent broker or AWS resource was changed by this test-coverage increment.

Updated: 2026-11-21

## Identity employee invitations — SDD and delivery-secret threat model drafted for owner review

- Drafted `docs/changes/identity-employee-invitations/{proposal,spec,design,tasks}.md` and added `security-review.md`. This remains documentation-only; no application code, database schema, API contract or runtime behavior changed.
- Proposed scope remains HrAdmin-only invitation management, anonymous one-time activation, Employee-only role, 256-bit token, digest-only invitation lookup, proposed 24-hour expiry, resend rotation, cancellation, atomic Employee creation and password hashing. Public registration remains disabled outside Development.
- The security review compares asynchronous protected-secret delivery with synchronous post-commit delivery. It documents Data Protection purpose isolation, persistent/shared protected key-ring requirements, worker access boundaries, generation checks, resend races, at-least-once duplicate delivery, backup-retention limits and fail-closed recovery.
- Preferred candidate is a dedicated short-lived Data Protection-protected secret record, separate from the ordinary outbox. The ordinary outbox must contain only opaque invitation reference/generation metadata. This candidate is **not approved** and is not implemented.
- Owner approval remains required to select the delivery model, approve key protection/backup operations if Option A is chosen, and confirm invitation policy defaults, local mail-catcher boundary and provider-acceptance deletion behavior. Elevated-role assignment remains separately blocked by open question 11.
- No CI run was required for this documentation-only update. No AWS resource, persistent RabbitMQ topology, MySQL data or service runtime was changed.

Updated: 2026-11-21

## Insight queue migration — CI #228 passed; review completed

- CI #228 completed successfully for code commit `5bf2b0e97b5234af4c9dd438b0742e119623582b`: Release restore/build, Docker Compose validation, RabbitMQ startup/topology checks, all 19 Python RabbitMQ topology contract tests, pinned DynamoDB Local image pull and the complete .NET test step passed.
- Exact .NET results: Identity 100/100, Habit 24/24, Challenge 57/57, Event Tracking 27/27, Notification 28/28, Insight 15/15 and Insight.QueueMigration.IntegrationTests 8/8 — **259 passed, 0 failed, 0 skipped**.
- The queue-migration integration tests passed the corrected AMQP header byte assertion and all execution safety/failure-path scenarios, including mandatory-return recovery and the at-least-once duplicate window after confirmed destination publish followed by ledger failure.
- Reviewed the migration utility, its README, the runbook and the exact RabbitMQ definitions/topology contract tests together. The runbook accurately describes the tool's limits and operator preconditions. Current definitions/tests intentionally preserve the Insight source as durable classic with no arguments and its existing lifecycle binding; no Insight DLQ or target quorum topology has been activated.
- Operational migration remains a separate, gated activity: obtain explicit maintenance-window approval, verify all publishers/consumers are quiesced, and inventory non-null AMQP `user_id` values through an approved privacy-safe method to confirm compatibility with the migration connection identity. Do not silently remove or rewrite that property.
- CI used disposable infrastructure; no persistent/shared RabbitMQ broker, shared volume, MySQL data or AWS resource was modified.
- [CI #228](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44899197310). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44899197310/artifacts/14705491683).

Updated: 2026-11-21

## Insight queue migration — CI #227 header assertion failure and correction

- CI #227 completed for code commit `801d01c9f2de1c5278fd5f4b12ea38450fa631cd`. Restore and Release build passed; Compose validation, RabbitMQ startup/topology, all 19 Python topology tests and the pinned DynamoDB Local image pull passed.
- Existing service suites all passed: Identity 100/100, Habit 24/24, Challenge 57/57, Event Tracking 27/27, Notification 28/28 and Insight 15/15 (**251/251**). The new Insight.QueueMigration.IntegrationTests suite passed 7/8; one successful-copy property test failed at its header assertion.
- Failure detail: RabbitMQ.Client exposes the AMQP short-string header `x-contract-version` as the UTF-8 byte array `[49]` when the copied message is read. The test incorrectly expected a .NET string. The migration code copies the received header value as-is; no production behavior change was indicated by the failure.
- Corrected the integration assertion to compare the UTF-8 bytes for `"1"` (commit `5bf2b0e97b5234af4c9dd438b0742e119623582b`). A fresh CI run is required to verify this correction; do not mark the migration utility operationally accepted yet.
- [CI #227](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44898745526). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44898745526/artifacts/14705235902).
- No persistent/shared RabbitMQ queue or volume, MySQL volume or AWS resource was modified. Insight queue definitions remain unchanged pending review and an approved maintenance window.

Updated: 2026-11-21

## Insight queue migration — CI #227 verification pending

- CI #227 is running against code commit `801d01c9f2de1c5278fd5f4b12ea38450fa631cd`. The Release build, Docker Compose validation, RabbitMQ startup/topology verification, all 19 Python topology contract tests and pinned DynamoDB Local image pull have passed; the full .NET test step remains in progress. No .NET test totals are claimed yet.
- CI #227 includes the fixes for RabbitMQ.Client 7.2.2 mandatory-return handling, the authenticated-user AMQP `user_id` test fixture and temporary ledger cleanup. The latest branch head is documentation-only commit `22539aa448e94a62ab38cd98e40919525c596662`, which adds the `user_id` operational precondition to the migration runbook.
- Before any migration execution, inspect all non-null AMQP `user_id` values using an approved privacy-safe method and verify they match the migration connection identity. If values are incompatible or vary across users, stop and obtain explicit approval for a different property-handling policy; never silently strip or rewrite the property.
- After CI completes, record the exact .NET suite totals and review the utility, runbook and current topology contract tests together. The utility remains unapproved for operational use until that review and an explicitly approved maintenance window.
- The committed Insight source queue remains durable classic with no arguments and its original lifecycle binding; no persistent/shared RabbitMQ broker, MySQL volume or AWS resource was changed.

Updated: 2026-11-21

## Insight queue migration — CI #223 failure analysis and corrections

- GitHub Actions run #223 for code commit `607ed2f64be41a3fa295045c2177c4519027fa2a` completed with failure in the .NET test step. Release build, Compose validation, RabbitMQ startup/topology, all 19 Python topology contract tests and pinned DynamoDB Local image pull passed.
- Existing service suites passed: Identity 100/100, Habit 24/24, Challenge 57/57, Event Tracking 27/27, Notification 28/28 and Insight 15/15 (**251/251**). The new Insight queue-migration Testcontainers suite passed 6/8.
- Failure 1: RabbitMQ.Client 7.2.2 surfaces an unroutable mandatory publish as `PublishReturnException` directly from `BasicPublishAsync`, before the helper's post-await event check. `MandatoryPublisher` now catches this client exception and wraps it in the documented `InvalidOperationException`, preserving the original exception as inner detail.
- Failure 2: RabbitMQ rejects a copied AMQP `user_id` property when it differs from the authenticated broker username. The integration fixture now sets the property to its Testcontainers authentication username and still verifies it is copied unchanged.
- The bounded-resume test now uses a temporary digest-ledger file and deletes it after assertions rather than leaving the utility's default ledger in the repository workspace.
- Fix commits: `e806e899c33018cdc9161f8707ccae589c8150ab` and `ca88300cf2d85fc47512489dc37a66fcd778d6fd`. A fresh CI run is required before marking the utility verified or proceeding to the joint operational review.
- The Insight queue definitions remain durable classic with no arguments and the existing lifecycle binding; no persistent/shared RabbitMQ, MySQL, DynamoDB Local or AWS resources were changed.

Updated: 2026-11-21

## Insight queue migration — Release build and runbook reconciliation

- GitHub Actions run #222 for code commit `607ed2f64be41a3fa295045c2177c4519027fa2a` has successfully completed the Release solution build, Docker Compose validation, local RabbitMQ startup/topology verification, all Python RabbitMQ topology contract tests, and the pinned DynamoDB Local image pull.
- The .NET test step (including the new RabbitMQ Testcontainers queue-migration tests) is still running; overall workflow status remains in progress. Do not record the full suite as passed until the job completes successfully.
- Reviewed the runbook against the actual definitions and topology tests. Clarified at-least-once reconciliation: a crash between confirmed destination publish and source acknowledgement can create extra destination copies without a matching digest record, so exact equality between original source count, destination count and ledger digest count is not a valid invariant. The runbook now requires documenting and reconciling the duplicate window before deleting staging data.
- The committed Insight source queue remains durable classic with no arguments and its existing lifecycle binding; the Insight DLQ remains absent in repository definitions pending approved maintenance.
- No persistent/shared broker, MySQL, DynamoDB Local or AWS resources were changed.

Updated: 2026-11-21

## Insight queue migration — CI compile correction

- Inspected GitHub Actions runs #217 and #218. Both failed during the solution Release build before tests ran, with the same two RabbitMQ.Client 7.2.2 compile errors: the publish helper accepted `IReadOnlyBasicProperties` although `BasicPublishAsync` requires its generic properties type to implement `IAmqpHeader`, and the delivery-mode property requires the `DeliveryModes` enum rather than an integer.
- Updated `MandatoryPublisher.PublishAsync` to accept `BasicProperties`, set the migration copy to `DeliveryModes.Persistent`, and use `DeliveryModes.Transient` / `DeliveryModes.Persistent` consistently in integration-test fixtures and assertions.
- GitHub Actions run #221 exposed one additional API-name mismatch: RabbitMQ.Client 7.2.2 names delivery mode `Transient`, not `NonPersistent`. The fixture was corrected. A fresh full CI run is pending; do not treat the utility as build-verified or operationally approved until it passes.
- No queue definitions, persistent broker state, MySQL, DynamoDB Local or AWS resources were changed.

Updated: 2026-11-21

## Insight queue migration — deterministic mandatory-return handling

- Extracted mandatory publication and broker-return detection into the production `MandatoryPublisher` component; the migration copy loop now calls this component before it appends the digest ledger or acknowledges the source delivery.
- Added a Testcontainers integration test that invokes this exact production component against a disposable RabbitMQ broker and publishes with `mandatory=true` to a non-existent destination routing key. The test asserts that the broker's Basic.Return is surfaced as a failure, without a timing-sensitive queue deletion race.
- The same test then publishes successfully to a valid queue using the same publisher instance, confirming that a previous returned publication does not poison subsequent copy attempts.
- Expanded successful-copy property assertions to include expiration, timestamp, user ID and cluster ID in addition to the previously covered AMQP properties, headers and body.
- These are committed tests. The latest CI run has successfully compiled the complete Release solution and passed Compose, local RabbitMQ startup/topology and Python topology-contract validation; the .NET test step is still running. The local environment has no .NET SDK. No persistent/shared broker was accessed.

Updated: 2026-11-21


- Added an isolated Testcontainers scenario that intentionally makes the ledger append fail after the destination publish has completed. It verifies the destination copy exists while the source delivery remains unacknowledged and is requeued when the utility channel closes.
- The test then retries with a valid ledger and verifies the source is acknowledged while the destination contains the duplicate copy, proving the documented at-least-once crash window rather than claiming exactly-once behavior.
- Added a separate destination-consumer guard test: the utility refuses execution if the destination queue has active consumers. A missing-destination preflight test also confirms the source message remains untouched.
- Expanded the successful-copy contract assertions to cover AMQP expiration, timestamp, user ID and cluster ID in addition to content type/encoding, persistent delivery mode, priority, correlation ID, reply-to, message ID, type, app ID, headers and body. This is source-level test coverage only until CI compiles and runs the project.
- This coverage runs only against a disposable RabbitMQ Testcontainers broker. No persistent/shared broker was accessed. CI build/runtime verification remains pending because the local environment does not provide the .NET SDK.
- Remaining before operational review: complete the current CI .NET test run, then jointly review the utility, runbook and exact topology contract tests. The deterministic mandatory-return test has since been added.

Updated: 2026-11-21

## Insight queue migration — isolated integration test harness

- Added `Insight.QueueMigration.IntegrationTests`, a dedicated .NET 8 test project using RabbitMQ Testcontainers and invoking the actual migration console executable against a disposable RabbitMQ broker.
- Coverage verifies passive preflight leaves source messages untouched (including when the destination queue is missing), successful copy preserves the payload and supported AMQP basic properties/headers while upgrading delivery mode to persistent, bounded copy can resume, queue counts reconcile, and active consumers on either queue block execution.
- Registered the test project in `backend/TeamPulse.sln`. The tests are now compiled successfully in Release by CI; their runtime result is pending in the current workflow. The local execution environment has no .NET SDK.
- Remaining verification: complete the full CI test suite and review the utility, runbook and exact topology contract together. Mandatory unroutable publish handling and the documented crash/retry duplicate window now have isolated Testcontainers coverage. The utility remains unapproved for operational use until the full suite and review pass.
- All broker interactions in these tests use disposable Testcontainers instances. No persistent RabbitMQ queues, volumes, MySQL, DynamoDB Local or AWS resources were modified.

Updated: 2026-11-21

## Insight RabbitMQ migration utility — first implementation increment

- Added a standalone .NET 8 console utility under `backend/src/Tools/Insight.QueueMigration`, using the repository-pinned RabbitMQ.Client 7.2.2.
- The default mode performs passive preflight only; it does not declare or mutate queues. Execution requires both `--execute` and explicit `--confirm-quiesced` operator attestation, and refuses to copy while the source reports active consumers.
- The copy path uses manual source acknowledgement, mandatory routing through the default exchange, publisher confirms, persistent delivery mode, supported basic-property/header copying and a payload SHA-256-only JSONL ledger. It never declares, deletes, purges or reconfigures queues.
- Registered the standalone tool in `backend/TeamPulse.sln` so the repository Release build includes it.
- At-least-once duplicate window is documented: a crash after destination confirmation but before source acknowledgement can result in a duplicate. The tool is intended for source-to-staging and staging-to-recreated-source invocations.
- This increment has **not** been compiled or exercised against RabbitMQ Testcontainers; it is not yet approved for operational use. Next: add isolated integration tests, run Release build and full CI, review message-property preservation and ledger reconciliation, then update the runbook. Existing topology definitions and persistent broker remain unchanged.
- No queue migration was executed and no persistent RabbitMQ, MySQL, DynamoDB Local or AWS resource was changed.

Updated: 2026-11-21

## Next execution — Insight queue migration preparation

- CI #195 passed for code commit `64879f7585fff2f6ad8dd2fb964582e7ab2ae9ca`: all 251 .NET tests and 19 Python RabbitMQ topology tests passed; Release build, Compose validation, RabbitMQ startup/topology and pinned DynamoDB Local image pull passed.
- Challenge API health-route acceptance is verified; no further Challenge implementation work is pending in this increment.
- The next approved Insight task is to implement and test a standalone, message-preserving RabbitMQ queue migration utility. The existing runbook is prepared for review, but the utility is not yet present.
- Keep the currently deployed/repository Insight source queue as durable classic with no arguments until the utility, dry-run/confirmation safeguards, migration ledger and exact contract tests are reviewed. Do not change the committed definitions, execute a migration, delete/purge a queue, or reset the shared RabbitMQ volume in this preparation increment.
- AWS/EventBridge/SQS provisioning and production table setup remain deferred.

Updated: 2026-11-21

## Challenge API health routes — HTTP-level independence verified

- Added a real `WebApplicationFactory` integration test using the Challenge MySQL 8.4 fixture and an enabled consumer pointed at a reserved-but-closed loopback AMQP port. The test verifies `/health/live` and MySQL-only `/health/ready` return HTTP 200 while `/health/consumer` returns HTTP 503.
- CI #194 exposed a test-only synchronization assumption: the connection-attempt counter did not advance within the test's five-second wait. The test now asserts the consumer configuration is enabled and its status is disconnected before checking the HTTP routes; no production health logic changed.
- CI #195 passed for code commit `64879f7585fff2f6ad8dd2fb964582e7ab2ae9ca`: Challenge **57/57**, Habit 24/24, Identity 100/100, Event Tracking 27/27, Notification 28/28 and Insight 15/15 — **251/251 .NET tests**, zero failed or skipped. All **19/19 Python RabbitMQ topology tests**, Release build, Compose validation, RabbitMQ startup/topology, pinned DynamoDB Local image pull and test-result upload passed.
- Build reported two nullable warnings in EventTracking and Notification test helpers; no Challenge warning or error was reported. [CI #195](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44896073551). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44896073551/artifacts/14704755053).
- CEPS-15 is now verified at the real API-host HTTP route boundary. The consumer remains disabled by default; no persistent RabbitMQ topology, MySQL volume or AWS resource was changed.

Updated: 2026-11-21

## Insight consumer privacy regression and full-suite CI rerun verified

- Added a broker-backed privacy regression test using sentinel raw payload, optional note, credential and connection-string values; the test captures consumer logs and asserts none of those values are emitted.
- CI #193 attempt 1 passed the Insight tests (15/15) but failed during unrelated Habit and Challenge Testcontainers cleanup. CI #193 attempt 2 completed successfully for code commit `40b8e80d649af6b5d2eb8d4d14d6ec22b37601e0` (run attempt 2).
- The successful rerun passed restore, Release build, Docker Compose validation, RabbitMQ startup/topology, all **19/19 Python RabbitMQ topology tests**, the pinned DynamoDB Local image pull, the full .NET suite and test-result artifact upload.
- .NET totals: Identity 100/100, Habit 24/24, Challenge 56/56, Event Tracking 27/27, Notification 28/28 and Insight 15/15 — **250/250 passed**, zero failed or skipped. The Challenge broker-loss replay/prefetch test remains green within its 56-test suite.
- Build succeeded with two nullable warnings confined to EventTracking and Notification test helpers; no Challenge build warning or error was reported.
- [CI #193, attempt 2](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44894055289). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44894055289/artifacts/14703730990).
- This CI run validates code commit `40b8e80`; current branch HEAD at this update is a documentation-only descendant. No production code, persistent RabbitMQ queue/volume, MySQL volume or AWS resource was changed in this verification increment.

Updated: 2026-11-21

## Insight DynamoDB CI acceptance verified; RabbitMQ consumer tests added

- CI #188 passed for commit `faf0cac9ccc2c13f62fe4a9db25567959222d84f` on `feature/backend-foundation`.
- Release build, Docker Compose validation, local RabbitMQ startup/topology checks and the full .NET test suite passed. Test totals: Identity 100, Habit 24, Challenge 56, Event Tracking 27, Notification 28 and Insight 11 — **246/246 passed**, zero failed or skipped.
- The Insight DynamoDB Local integration tests verified minimal projection/inbox attributes, duplicate delivery, stale/equal/newer version behavior and concurrent version ordering.
- Added dedicated Insight RabbitMQ Testcontainers coverage for ack-after-processing, classified terminal DLQ delivery and source-message recovery when the DLQ route is unavailable. The acknowledgement test uses a prefetch-1 sentinel delivery to verify the first message was acknowledged before the next is processed.
- CI #190 passed for code commit `9ca613c41f9116296935270c19b8f8b136c78c5a`: **249/249 .NET tests passed**, zero failed or skipped (Identity 100, Habit 24, Challenge 56, Event Tracking 27, Notification 28, Insight 14). This includes the three new RabbitMQ Testcontainers tests for ack-after-processing, classified terminal DLQ delivery, and source-message recovery when the DLQ route is unavailable. Release build, Compose validation, RabbitMQ startup/topology checks, topology contract tests and test-result artifact upload also passed. [CI #190](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44893324977).
- No persistent RabbitMQ queue/volume, MySQL volume or AWS resource was modified.

Updated: 2026-11-21

## Insight lifecycle projection — implementation in progress

- Owner approval of the Insight lifecycle-projection SDD was received on 2026-11-21. Implementation is confined to `feature/backend-foundation`.
- Added Insight.Domain, Insight.Application, Insight.Infrastructure, Insight.API and Insight.UnitTests projects to the solution. The transport-independent schema-v1 processor validates Identity lifecycle envelopes, transitions, recognized fields and the optional note's string/1,000-character bound; the note is excluded from the persistence contract.
- Added a DynamoDB-native store using strongly consistent reads and `TransactWriteItems` for the inbox/projection decision, conditional version fences and bounded optimistic-concurrency retries. The store records only approved projection/inbox attributes.
- Added Development-only opt-in DynamoDB Local table initialization, DynamoDB readiness health, independent consumer health and a disabled-by-default RabbitMQ consumer with manual acknowledgement, bounded transient retry/reconnect and confirmed DLQ publication.
- Added DynamoDB Local to Compose without changing or resetting existing MySQL/RabbitMQ volumes. Insight source/DLQ topology remains unchanged: the committed definitions retain the existing durable classic source queue and original binding. Target quorum/DLQ topology is documented only; activation awaits a reviewed migration utility and controlled maintenance window.
- The processor foundation and consumer/store code are committed; CI restore/build/test validation is in progress or pending. Do not treat the Insight increment as complete until DynamoDB Local, RabbitMQ integration, topology and full CI evidence pass.
- No AWS resources, EventBridge/SQS adapters, analytics, dashboards or read APIs were added. The migration procedure is documented at `docs/changes/insight-employee-status-projection-consumer/rabbitmq-migration-runbook.md`; it is not authorized for execution against a live persistent broker.

Updated: 2026-11-21

## Notification employee-status projection — acceptance verified by CI #168

- CI #168 passed for commit `bd27c4a118e851d02625d5e2002a9be357acec0a` on `feature/backend-foundation`.
- Test results: Notification **28/28**, EventTracking **27/27**, Challenge **56/56**, Habit **24/24**, Identity **100/100** — **235/235 .NET tests passed**, zero failed or skipped; RabbitMQ topology contract **17/17** passed.
- Release build succeeded with 0 errors. Docker Compose validation, RabbitMQ startup/topology checks and test-result artifact upload passed.
- Notification's malformed UTF-8 test verified original message bytes and `InvalidEvent` classification reached its DLQ. The unavailable-DLQ-route test verified that the source delivery remained unacknowledged and was recoverable after the consumer session ended. Both are isolated RabbitMQ Testcontainers tests.
- The Notification employee-status projection SDD acceptance criteria NESP-01–NESP-14 are now verified by the combined implementation tests and CI #168. Scope remains projection-only; this does not authorize notification sending/preferences changes, employee-facing APIs, or an inactive-employee access policy.
- [CI #168](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44889070669). Test results artifact: [teampulse-test-results](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44889070669/artifacts/14700589953).
- Build reported two nullable warnings in integration-test helper code (one Notification, one EventTracking); there were no build errors. These are recorded for a later cleanup and do not invalidate the passing run.
- No persistent RabbitMQ broker, existing database volume, or AWS resource was changed.

Updated: 2026-11-21

## Notification DLQ failure-path test added

- Added a second broker-backed failure-path test: temporarily remove the Notification DLQ binding, publish malformed UTF-8 to the source queue, and verify the mandatory DLQ publish failure ends the consumer session without acknowledging the source delivery. After channel disposal, the test reads and acknowledges the redelivered source message to leave its disposable broker clean.
- Together with the malformed-message DLQ success-path test, this covers both confirmed DLQ publication and unavailable DLQ routing. CI verification is pending.
- These tests use isolated RabbitMQ Testcontainers instances; they do not alter the persistent local broker or production topology.

Updated: 2026-11-21

## Notification consumer CI verification and DLQ test extension

- CI #165 passed for commit `13095cc3effb30a11862ccc68a64fc3ebf60425e` on `feature/backend-foundation`: restore, Release build, Docker Compose validation, RabbitMQ startup/topology contract tests and the full .NET suite (including MySQL 8.4 and RabbitMQ Testcontainers) all succeeded.
- This verifies the Notification API host, persistence, stale/equal lifecycle-version classification and existing broker replay integration coverage.
- Added a focused Testcontainers case for malformed UTF-8 delivery: assert original bytes and `InvalidEvent` classification are published to the Notification DLQ, then source queue empties after confirmed publication. Verification is pending in the next CI run.
- Remaining Notification acceptance work: verify DLQ routing failure leaves the source delivery unacknowledged/recoverable, then update acceptance evidence. No persistent/shared RabbitMQ broker or database volume was changed.

Updated: 2026-11-21

## Notification lifecycle consumer and broker topology added

- Added the opt-in Notification RabbitMQ consumer with manual ack after MySQL commit, bounded transient retry/backoff, reconnect, dedicated publisher-confirmed DLQ publishing and independent `/health/consumer`.
- Added Notification quorum source/DLQ topology with delivery limit 5 and at-least-once dead-lettering. Updated only Notification topology; all other service queues retain their prior definitions.
- Added Notification RabbitMQ/MySQL replay integration coverage, consumer options/retry tests, API-host consumer health coverage and Python topology contract assertions.
- Added `docs/runbooks/notification-rabbitmq-queue-migration.md`. It requires a maintenance window, stopping publisher/consumer, buffering pending messages, recreating only Notification's immutable queue, restoring messages, and verifying before resuming. No live RabbitMQ queue or shared volume was modified.
- Implementation commit containing consumer and topology: `b580ca14308d25cd72f63083a9839c62ef509998` and `22598c2d58e9befd537c29578c62f5e6f10017f6`; replay tests and checklist are included in the current execution.
- CI verification pending. The last confirmed Notification API build was CI #158 before consumer addition; CI #161 is running against the consumer/topology increment.

Updated: 2026-11-21

## Notification projection — MySQL persistence increment added

- Added Notification.Infrastructure with EF Core 8/Pomelo, a checked-in initial migration/model snapshot, a transactional inbox + conditional lifecycle-version upsert store, and a read-only projection reader abstraction.
- Added MySQL 8.4 Testcontainers coverage for exact schema columns, migration idempotency, duplicate event IDs, stale/equal versions and concurrent delivery preserving the maximum lifecycle version.
- Added `04-notification-database.sql`, mounted it in Compose, and documented safe manual database/grant creation for existing persistent MySQL volumes. No volume reset or live database mutation was performed.
- Commit: `7f92cf4128f9ca3fd2f72ca4d8a8f4ac88654732`. CI verification pending. Next increment is Notification.API host/startup migration and separate health endpoints.

Updated: 2026-11-21

## Notification projection implementation — application core added

- Owner approval for the Notification employee-status projection SDD was received on 2026-11-21. The proposal/spec/design/tasks are now marked owner-approved; the scope remains projection-only.
- Added Notification.Domain, Notification.Application and Notification.UnitTests projects to the six-service solution. The first increment implements minimal projection/inbox domain entities, strict schema-v1 recognized-field validation and a transport-independent processor.
- Added deterministic tests for valid lifecycle transitions, exact `hr-admin-api` source, reason-code bounds, malformed/missing fields, unsupported schema, transition mismatch, unknown additive fields and optional-note exclusion.
- Implementation commit: `d8e207f7a7650bc1747baf8509e17c5f4e2b305f`. CI verification for this commit is pending; no local .NET or Docker build/test is claimed.
- Next focused increment: Notification-owned EF Core/MySQL persistence and checked-in migration. RabbitMQ source-queue migration remains a later increment and must preserve pending messages; no live broker or existing database volume has been altered.

Updated: 2026-11-21

## Next increment prepared — Notification lifecycle projection SDD draft

- Prepared `docs/changes/notification-employee-status-projection-consumer/` with proposal, spec, design and acceptance-mapped tasks on 2026-11-21.
- Scope is limited to the Notification-owned employee-status projection/inbox and opt-in local RabbitMQ consumer. No notification sending, preference changes, delivery-history mutations, AWS provisioning or retention cleanup are included.
- The current local Notification queue is durable but has no quorum/DLX arguments. The draft requires an explicit pending-message-preserving migration before changing its immutable queue type/arguments; no live broker was modified.
- The SDD is **awaiting owner approval**. No Notification application code, database migration, Compose change or RabbitMQ topology change has been implemented. Implementation remains blocked until approval.

Updated: 2026-11-21

## Event Tracking lifecycle projection consumer acceptance — CI #151 verified

- Live implementation head: `d9402b56fd91ad59a1de34a8aee6fcfcde905db0` on `feature/backend-foundation`; current branch documentation is updated after that tested code commit.
- The owner-approved Event Tracking projection-consumer SDD acceptance criteria ETESP-01–ETESP-14 are complete. The API-host test verifies the real opt-in RabbitMQ consumer becomes healthy, then reports consumer HTTP 503 after broker shutdown while MySQL readiness remains HTTP 200.
- The broker interruption/replay integration test uses the production Event Tracking processor and MySQL inbox. It commits the first delivery, interrupts RabbitMQ before acknowledgement, restarts the same broker container, and verifies redelivery is classified Duplicate without a second projection mutation.
- CI #151 passed: EventTracking **27/27**, Challenge **56/56**, Habit **24/24**, Identity **100/100** — **207/207 .NET tests**; **14/14 Python RabbitMQ topology tests**; Docker Compose validation and RabbitMQ startup/topology checks passed. Release build had 0 errors and one nullable warning in `EventTrackingRabbitMqMySqlReplayIntegrationTests.cs`.
- [CI #151](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44884885148).
- This closes only the Event Tracking employee-status projection foundation. Activity submission/evidence APIs and post-deactivation access policy, Insight/Notification consumers, AWS EventBridge/SQS provisioning, inbox retention/legal sign-off and global token revocation remain deferred. No AWS resources were provisioned.

Updated: 2026-11-21

## Challenge lifecycle projection acceptance — CI #151 verified

- Live code head verified: d9402b56fd91ad59a1de34a8aee6fcfcde905db0 on feature/backend-foundation. The deterministic Challenge fingerprint-vector test is present in this head's history.
- CI #151 completed successfully for that exact head: Release build succeeded with 0 errors and one nullable warning in EventTrackingRabbitMqMySqlReplayIntegrationTests.cs (outside Challenge scope); Docker Compose validation, RabbitMQ startup/topology and all 14 Python topology tests passed.
- All .NET tests passed: **207/207** — Challenge 56, EventTracking 27, Habit 24 and Identity 100; zero failed or skipped. The Challenge known-answer test verified digest f17c59bcb345810da58183dc56a79b7eaa6e742ee8e9f5fb4432993eb8909fc5 across property-order/whitespace equivalence, UUID case normalization, equivalent UTC timestamps and long.MaxValue.
- [CI #151](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44884885148).
- The approved Challenge projection acceptance criteria CEPS-01–CEPS-18 are now verified by the CI evidence in the Challenge SDD tasks. This closes projection implementation acceptance only; it does not authorize Challenge business workflows. Open question 19, privacy/legal retention sign-off and owner-deferred AWS provisioning remain unchanged.

Updated: 2026-11-21

## Previous repository checkpoint before CI #151 — 2026-11-21

- Active branch: `feature/backend-foundation`. Current head at this documentation update: `ee99cdb24994a5a06c9efcdc5eeb8c624ff1b2a2` (an EventTracking-only follow-up; no Challenge files changed).
- CI #145 ran against Challenge commit `5fb1d41f6a775af88968d3277414fe77627edc10`. Restore, Release build (0 warnings/errors), Docker Compose validation, RabbitMQ startup/topology and all 14 Python topology tests passed. Challenge passed 55/55, Habit 24/24 and Identity 100/100. EventTracking passed 25/26; its sole failure was the API-host consumer-health test waiting for `/health/consumer` to return 503 after RabbitMQ shutdown. Thus 204/205 .NET tests passed and all 55 Challenge tests passed. [CI #145](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44882807000).
- The concurrent equal-lifecycle-version outcome race is closed: the MySQL 8.4 Testcontainers regression verifies exactly one Applied and one Stale, with the committed projection matching the winning event. The connection-scoped result flag is set by the atomic duplicate-key conditional upsert; CI #145 verified the full Challenge test project.
- CI #146 is running for the EventTracking-only follow-up commit `ee99cdb`; it is not a Challenge acceptance change. The current full-workflow failure remains outside the approved Challenge increment.
- Challenge scope remains projection-only. Open question 19, permanent inbox retention pending privacy/legal review and AWS EventBridge/SQS/CloudWatch provisioning remain deferred.


## Event Tracking RabbitMQ Testcontainers coverage

- Added broker-backed tests using RabbitMQ 4.1 Testcontainers for manual acknowledgement after processing completes, terminal outcome DLQ confirmation/classification, transient retry success and exhaustion, source redelivery when the mandatory DLQ route is unavailable, and log privacy.
- These tests use a dedicated disposable broker and the exact Event Tracking source/DLQ quorum topology. They do not modify the user's existing persistent broker volume.
- CI #137 completed successfully: EventTracking 17, Habit 24, Identity 100 and Challenge 54 — 195 .NET tests passed, plus all 14 Python topology tests. The API-host startup/MySQL integration test passed.
- The Event Tracking RabbitMQ broker tests were included in CI #142/#143. Both runs passed the new broker/consumer cases but failed the API-host consumer-health isolation test: 25/26 EventTracking tests passed; the failure waited for `/health/consumer` to return 503 after broker shutdown. This Event Tracking issue is outside the Challenge SDD scope.

Updated: 2026-11-21


## Event Tracking API-host startup CI verification

- CI #137 passed for commit `454e9496d8d53c6ea89c425e535704400a11c397`: Release build, Compose validation, RabbitMQ startup/topology and all automated tests succeeded. [CI #137](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44880083557).
- Test results: EventTracking 17, Habit 24, Identity 100 and Challenge 54 — **195 .NET tests passed**; all 14 Python RabbitMQ topology tests passed.
- EventTracking's MySQL 8.4 Testcontainers API-host startup test passed, proving the production API host applies its migration and resolves the production projection store/reader.
- Added RabbitMQ Testcontainers consumer tests for acknowledgement ordering, terminal DLQ classification/confirmation, transient retry success and exhaustion, DLQ route failure/redelivery, and logging privacy. CI verification for these newly added tests is pending.

Updated: 2026-11-21


## RabbitMQ integration-test compile correction

- CI #138 caught four compile errors in the new broker tests: xUnit selected its DateTime overload for `Assert.Equal(0, uintQueueCount)`. The queue-count assertions now use an explicit unsigned expected value (`0u`).
- This is a test-only compile correction. The test suite did not execute in CI #138; do not treat the new RabbitMQ integration tests as verified until the next CI run succeeds.

Updated: 2026-11-21

- Added an isolated Testcontainers scenario that intentionally makes the ledger append fail after the destination publish has completed. It verifies the destination copy exists while the source delivery remains unacknowledged and is requeued when the utility channel closes.
- The test then retries with a valid ledger and verifies the source is acknowledged while the destination contains the duplicate copy, proving the documented at-least-once crash window rather than claiming exactly-once behavior.
- Added a separate destination-consumer guard test: the utility refuses execution if the destination queue has active consumers. A missing-destination preflight test also confirms the source message remains untouched.
- Expanded the successful-copy contract assertions to cover AMQP expiration, timestamp, user ID and cluster ID in addition to content type/encoding, persistent delivery mode, priority, correlation ID, reply-to, message ID, type, app ID, headers and body. This is source-level test coverage only until CI compiles and runs the project.
- This coverage runs only against a disposable RabbitMQ Testcontainers broker. No persistent/shared broker was accessed. CI build/runtime verification remains pending because the local environment does not provide the .NET SDK.
- Remaining before operational review: deterministic mandatory-unroutable publication race coverage, complete Release build/test run, then joint review of utility, runbook and exact topology contract tests.

Updated: 2026-11-21

## Insight queue migration — isolated integration test harness

- Added `Insight.QueueMigration.IntegrationTests`, a dedicated .NET 8 test project using RabbitMQ Testcontainers and invoking the actual migration console executable against a disposable RabbitMQ broker.
- Coverage verifies passive preflight leaves source messages untouched (including when the destination queue is missing), successful copy preserves the payload and supported AMQP basic properties/headers while upgrading delivery mode to persistent, bounded copy can resume, queue counts reconcile, and active consumers on either queue block execution.
- Registered the test project in `backend/TeamPulse.sln`. The tests have not yet been compiled or run; CI must provide the first authoritative build/runtime result. The local execution environment has no .NET SDK.
- Remaining tests: mandatory unroutable publish failure, publish-confirm/source-ack ordering and the documented crash/retry duplicate window. The utility remains unapproved for operational use until the full suite and runbook/topology review pass.
- All broker interactions in these tests use disposable Testcontainers instances. No persistent RabbitMQ queues, volumes, MySQL, DynamoDB Local or AWS resources were modified.

Updated: 2026-11-21

## Insight RabbitMQ migration utility — first implementation increment

- Added a standalone .NET 8 console utility under `backend/src/Tools/Insight.QueueMigration`, using the repository-pinned RabbitMQ.Client 7.2.2.
- The default mode performs passive preflight only; it does not declare or mutate queues. Execution requires both `--execute` and explicit `--confirm-quiesced` operator attestation, and refuses to copy while the source reports active consumers.
- The copy path uses manual source acknowledgement, mandatory routing through the default exchange, publisher confirms, persistent delivery mode, supported basic-property/header copying and a payload SHA-256-only JSONL ledger. It never declares, deletes, purges or reconfigures queues.
- Registered the standalone tool in `backend/TeamPulse.sln` so the repository Release build includes it.
- At-least-once duplicate window is documented: a crash after destination confirmation but before source acknowledgement can result in a duplicate. The tool is intended for source-to-staging and staging-to-recreated-source invocations.
- This increment has **not** been compiled or exercised against RabbitMQ Testcontainers; it is not yet approved for operational use. Next: add isolated integration tests, run Release build and full CI, review message-property preservation and ledger reconciliation, then update the runbook. Existing topology definitions and persistent broker remain unchanged.
- No queue migration was executed and no persistent RabbitMQ, MySQL, DynamoDB Local or AWS resource was changed.

Updated: 2026-11-21

## Next execution — Insight queue migration preparation

- CI #195 passed for code commit `64879f7585fff2f6ad8dd2fb964582e7ab2ae9ca`: all 251 .NET tests and 19 Python RabbitMQ topology tests passed; Release build, Compose validation, RabbitMQ startup/topology and pinned DynamoDB Local image pull passed.
- Challenge API health-route acceptance is verified; no further Challenge implementation work is pending in this increment.
- The next approved Insight task is to implement and test a standalone, message-preserving RabbitMQ queue migration utility. The existing runbook is prepared for review, but the utility is not yet present.
- Keep the currently deployed/repository Insight source queue as durable classic with no arguments until the utility, dry-run/confirmation safeguards, migration ledger and exact contract tests are reviewed. Do not change the committed definitions, execute a migration, delete/purge a queue, or reset the shared RabbitMQ volume in this preparation increment.
- AWS/EventBridge/SQS provisioning and production table setup remain deferred.

Updated: 2026-11-21

## Challenge API health routes — HTTP-level independence verified

- Added a real `WebApplicationFactory` integration test using the Challenge MySQL 8.4 fixture and an enabled consumer pointed at a reserved-but-closed loopback AMQP port. The test verifies `/health/live` and MySQL-only `/health/ready` return HTTP 200 while `/health/consumer` returns HTTP 503.
- CI #194 exposed a test-only synchronization assumption: the connection-attempt counter did not advance within the test's five-second wait. The test now asserts the consumer configuration is enabled and its status is disconnected before checking the HTTP routes; no production health logic changed.
- CI #195 passed for code commit `64879f7585fff2f6ad8dd2fb964582e7ab2ae9ca`: Challenge **57/57**, Habit 24/24, Identity 100/100, Event Tracking 27/27, Notification 28/28 and Insight 15/15 — **251/251 .NET tests**, zero failed or skipped. All **19/19 Python RabbitMQ topology tests**, Release build, Compose validation, RabbitMQ startup/topology, pinned DynamoDB Local image pull and test-result upload passed.
- Build reported two nullable warnings in EventTracking and Notification test helpers; no Challenge warning or error was reported. [CI #195](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44896073551). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44896073551/artifacts/14704755053).
- CEPS-15 is now verified at the real API-host HTTP route boundary. The consumer remains disabled by default; no persistent RabbitMQ topology, MySQL volume or AWS resource was changed.

Updated: 2026-11-21

## Insight consumer privacy regression and full-suite CI rerun verified

- Added a broker-backed privacy regression test using sentinel raw payload, optional note, credential and connection-string values; the test captures consumer logs and asserts none of those values are emitted.
- CI #193 attempt 1 passed the Insight tests (15/15) but failed during unrelated Habit and Challenge Testcontainers cleanup. CI #193 attempt 2 completed successfully for code commit `40b8e80d649af6b5d2eb8d4d14d6ec22b37601e0` (run attempt 2).
- The successful rerun passed restore, Release build, Docker Compose validation, RabbitMQ startup/topology, all **19/19 Python RabbitMQ topology tests**, the pinned DynamoDB Local image pull, the full .NET suite and test-result artifact upload.
- .NET totals: Identity 100/100, Habit 24/24, Challenge 56/56, Event Tracking 27/27, Notification 28/28 and Insight 15/15 — **250/250 passed**, zero failed or skipped. The Challenge broker-loss replay/prefetch test remains green within its 56-test suite.
- Build succeeded with two nullable warnings confined to EventTracking and Notification test helpers; no Challenge build warning or error was reported.
- [CI #193, attempt 2](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44894055289). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44894055289/artifacts/14703730990).
- This CI run validates code commit `40b8e80`; current branch HEAD at this update is a documentation-only descendant. No production code, persistent RabbitMQ queue/volume, MySQL volume or AWS resource was changed in this verification increment.

Updated: 2026-11-21

## Insight DynamoDB CI acceptance verified; RabbitMQ consumer tests added

- CI #188 passed for commit `faf0cac9ccc2c13f62fe4a9db25567959222d84f` on `feature/backend-foundation`.
- Release build, Docker Compose validation, local RabbitMQ startup/topology checks and the full .NET test suite passed. Test totals: Identity 100, Habit 24, Challenge 56, Event Tracking 27, Notification 28 and Insight 11 — **246/246 passed**, zero failed or skipped.
- The Insight DynamoDB Local integration tests verified minimal projection/inbox attributes, duplicate delivery, stale/equal/newer version behavior and concurrent version ordering.
- Added dedicated Insight RabbitMQ Testcontainers coverage for ack-after-processing, classified terminal DLQ delivery and source-message recovery when the DLQ route is unavailable. The acknowledgement test uses a prefetch-1 sentinel delivery to verify the first message was acknowledged before the next is processed.
- CI #190 passed for code commit `9ca613c41f9116296935270c19b8f8b136c78c5a`: **249/249 .NET tests passed**, zero failed or skipped (Identity 100, Habit 24, Challenge 56, Event Tracking 27, Notification 28, Insight 14). This includes the three new RabbitMQ Testcontainers tests for ack-after-processing, classified terminal DLQ delivery, and source-message recovery when the DLQ route is unavailable. Release build, Compose validation, RabbitMQ startup/topology checks, topology contract tests and test-result artifact upload also passed. [CI #190](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44893324977).
- No persistent RabbitMQ queue/volume, MySQL volume or AWS resource was modified.

Updated: 2026-11-21

## Insight lifecycle projection — implementation in progress

- Owner approval of the Insight lifecycle-projection SDD was received on 2026-11-21. Implementation is confined to `feature/backend-foundation`.
- Added Insight.Domain, Insight.Application, Insight.Infrastructure, Insight.API and Insight.UnitTests projects to the solution. The transport-independent schema-v1 processor validates Identity lifecycle envelopes, transitions, recognized fields and the optional note's string/1,000-character bound; the note is excluded from the persistence contract.
- Added a DynamoDB-native store using strongly consistent reads and `TransactWriteItems` for the inbox/projection decision, conditional version fences and bounded optimistic-concurrency retries. The store records only approved projection/inbox attributes.
- Added Development-only opt-in DynamoDB Local table initialization, DynamoDB readiness health, independent consumer health and a disabled-by-default RabbitMQ consumer with manual acknowledgement, bounded transient retry/reconnect and confirmed DLQ publication.
- Added DynamoDB Local to Compose without changing or resetting existing MySQL/RabbitMQ volumes. Insight source/DLQ topology remains unchanged: the committed definitions retain the existing durable classic source queue and original binding. Target quorum/DLQ topology is documented only; activation awaits a reviewed migration utility and controlled maintenance window.
- The processor foundation and consumer/store code are committed; CI restore/build/test validation is in progress or pending. Do not treat the Insight increment as complete until DynamoDB Local, RabbitMQ integration, topology and full CI evidence pass.
- No AWS resources, EventBridge/SQS adapters, analytics, dashboards or read APIs were added. The migration procedure is documented at `docs/changes/insight-employee-status-projection-consumer/rabbitmq-migration-runbook.md`; it is not authorized for execution against a live persistent broker.

Updated: 2026-11-21

## Notification employee-status projection — acceptance verified by CI #168

- CI #168 passed for commit `bd27c4a118e851d02625d5e2002a9be357acec0a` on `feature/backend-foundation`.
- Test results: Notification **28/28**, EventTracking **27/27**, Challenge **56/56**, Habit **24/24**, Identity **100/100** — **235/235 .NET tests passed**, zero failed or skipped; RabbitMQ topology contract **17/17** passed.
- Release build succeeded with 0 errors. Docker Compose validation, RabbitMQ startup/topology checks and test-result artifact upload passed.
- Notification's malformed UTF-8 test verified original message bytes and `InvalidEvent` classification reached its DLQ. The unavailable-DLQ-route test verified that the source delivery remained unacknowledged and was recoverable after the consumer session ended. Both are isolated RabbitMQ Testcontainers tests.
- The Notification employee-status projection SDD acceptance criteria NESP-01–NESP-14 are now verified by the combined implementation tests and CI #168. Scope remains projection-only; this does not authorize notification sending/preferences changes, employee-facing APIs, or an inactive-employee access policy.
- [CI #168](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44889070669). Test results artifact: [teampulse-test-results](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44889070669/artifacts/14700589953).
- Build reported two nullable warnings in integration-test helper code (one Notification, one EventTracking); there were no build errors. These are recorded for a later cleanup and do not invalidate the passing run.
- No persistent RabbitMQ broker, existing database volume, or AWS resource was changed.

Updated: 2026-11-21

## Notification DLQ failure-path test added

- Added a second broker-backed failure-path test: temporarily remove the Notification DLQ binding, publish malformed UTF-8 to the source queue, and verify the mandatory DLQ publish failure ends the consumer session without acknowledging the source delivery. After channel disposal, the test reads and acknowledges the redelivered source message to leave its disposable broker clean.
- Together with the malformed-message DLQ success-path test, this covers both confirmed DLQ publication and unavailable DLQ routing. CI verification is pending.
- These tests use isolated RabbitMQ Testcontainers instances; they do not alter the persistent local broker or production topology.

Updated: 2026-11-21

## Notification consumer CI verification and DLQ test extension

- CI #165 passed for commit `13095cc3effb30a11862ccc68a64fc3ebf60425e` on `feature/backend-foundation`: restore, Release build, Docker Compose validation, RabbitMQ startup/topology contract tests and the full .NET suite (including MySQL 8.4 and RabbitMQ Testcontainers) all succeeded.
- This verifies the Notification API host, persistence, stale/equal lifecycle-version classification and existing broker replay integration coverage.
- Added a focused Testcontainers case for malformed UTF-8 delivery: assert original bytes and `InvalidEvent` classification are published to the Notification DLQ, then source queue empties after confirmed publication. Verification is pending in the next CI run.
- Remaining Notification acceptance work: verify DLQ routing failure leaves the source delivery unacknowledged/recoverable, then update acceptance evidence. No persistent/shared RabbitMQ broker or database volume was changed.

Updated: 2026-11-21

## Notification lifecycle consumer and broker topology added

- Added the opt-in Notification RabbitMQ consumer with manual ack after MySQL commit, bounded transient retry/backoff, reconnect, dedicated publisher-confirmed DLQ publishing and independent `/health/consumer`.
- Added Notification quorum source/DLQ topology with delivery limit 5 and at-least-once dead-lettering. Updated only Notification topology; all other service queues retain their prior definitions.
- Added Notification RabbitMQ/MySQL replay integration coverage, consumer options/retry tests, API-host consumer health coverage and Python topology contract assertions.
- Added `docs/runbooks/notification-rabbitmq-queue-migration.md`. It requires a maintenance window, stopping publisher/consumer, buffering pending messages, recreating only Notification's immutable queue, restoring messages, and verifying before resuming. No live RabbitMQ queue or shared volume was modified.
- Implementation commit containing consumer and topology: `b580ca14308d25cd72f63083a9839c62ef509998` and `22598c2d58e9befd537c29578c62f5e6f10017f6`; replay tests and checklist are included in the current execution.
- CI verification pending. The last confirmed Notification API build was CI #158 before consumer addition; CI #161 is running against the consumer/topology increment.

Updated: 2026-11-21

## Notification projection — MySQL persistence increment added

- Added Notification.Infrastructure with EF Core 8/Pomelo, a checked-in initial migration/model snapshot, a transactional inbox + conditional lifecycle-version upsert store, and a read-only projection reader abstraction.
- Added MySQL 8.4 Testcontainers coverage for exact schema columns, migration idempotency, duplicate event IDs, stale/equal versions and concurrent delivery preserving the maximum lifecycle version.
- Added `04-notification-database.sql`, mounted it in Compose, and documented safe manual database/grant creation for existing persistent MySQL volumes. No volume reset or live database mutation was performed.
- Commit: `7f92cf4128f9ca3fd2f72ca4d8a8f4ac88654732`. CI verification pending. Next increment is Notification.API host/startup migration and separate health endpoints.

Updated: 2026-11-21

## Notification projection implementation — application core added

- Owner approval for the Notification employee-status projection SDD was received on 2026-11-21. The proposal/spec/design/tasks are now marked owner-approved; the scope remains projection-only.
- Added Notification.Domain, Notification.Application and Notification.UnitTests projects to the six-service solution. The first increment implements minimal projection/inbox domain entities, strict schema-v1 recognized-field validation and a transport-independent processor.
- Added deterministic tests for valid lifecycle transitions, exact `hr-admin-api` source, reason-code bounds, malformed/missing fields, unsupported schema, transition mismatch, unknown additive fields and optional-note exclusion.
- Implementation commit: `d8e207f7a7650bc1747baf8509e17c5f4e2b305f`. CI verification for this commit is pending; no local .NET or Docker build/test is claimed.
- Next focused increment: Notification-owned EF Core/MySQL persistence and checked-in migration. RabbitMQ source-queue migration remains a later increment and must preserve pending messages; no live broker or existing database volume has been altered.

Updated: 2026-11-21

## Next increment prepared — Notification lifecycle projection SDD draft

- Prepared `docs/changes/notification-employee-status-projection-consumer/` with proposal, spec, design and acceptance-mapped tasks on 2026-11-21.
- Scope is limited to the Notification-owned employee-status projection/inbox and opt-in local RabbitMQ consumer. No notification sending, preference changes, delivery-history mutations, AWS provisioning or retention cleanup are included.
- The current local Notification queue is durable but has no quorum/DLX arguments. The draft requires an explicit pending-message-preserving migration before changing its immutable queue type/arguments; no live broker was modified.
- The SDD is **awaiting owner approval**. No Notification application code, database migration, Compose change or RabbitMQ topology change has been implemented. Implementation remains blocked until approval.

Updated: 2026-11-21

## Event Tracking lifecycle projection consumer acceptance — CI #151 verified

- Live implementation head: `d9402b56fd91ad59a1de34a8aee6fcfcde905db0` on `feature/backend-foundation`; current branch documentation is updated after that tested code commit.
- The owner-approved Event Tracking projection-consumer SDD acceptance criteria ETESP-01–ETESP-14 are complete. The API-host test verifies the real opt-in RabbitMQ consumer becomes healthy, then reports consumer HTTP 503 after broker shutdown while MySQL readiness remains HTTP 200.
- The broker interruption/replay integration test uses the production Event Tracking processor and MySQL inbox. It commits the first delivery, interrupts RabbitMQ before acknowledgement, restarts the same broker container, and verifies redelivery is classified Duplicate without a second projection mutation.
- CI #151 passed: EventTracking **27/27**, Challenge **56/56**, Habit **24/24**, Identity **100/100** — **207/207 .NET tests**; **14/14 Python RabbitMQ topology tests**; Docker Compose validation and RabbitMQ startup/topology checks passed. Release build had 0 errors and one nullable warning in `EventTrackingRabbitMqMySqlReplayIntegrationTests.cs`.
- [CI #151](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44884885148).
- This closes only the Event Tracking employee-status projection foundation. Activity submission/evidence APIs and post-deactivation access policy, Insight/Notification consumers, AWS EventBridge/SQS provisioning, inbox retention/legal sign-off and global token revocation remain deferred. No AWS resources were provisioned.

Updated: 2026-11-21

## Challenge lifecycle projection acceptance — CI #151 verified

- Live code head verified: d9402b56fd91ad59a1de34a8aee6fcfcde905db0 on feature/backend-foundation. The deterministic Challenge fingerprint-vector test is present in this head's history.
- CI #151 completed successfully for that exact head: Release build succeeded with 0 errors and one nullable warning in EventTrackingRabbitMqMySqlReplayIntegrationTests.cs (outside Challenge scope); Docker Compose validation, RabbitMQ startup/topology and all 14 Python topology tests passed.
- All .NET tests passed: **207/207** — Challenge 56, EventTracking 27, Habit 24 and Identity 100; zero failed or skipped. The Challenge known-answer test verified digest f17c59bcb345810da58183dc56a79b7eaa6e742ee8e9f5fb4432993eb8909fc5 across property-order/whitespace equivalence, UUID case normalization, equivalent UTC timestamps and long.MaxValue.
- [CI #151](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44884885148).
- The approved Challenge projection acceptance criteria CEPS-01–CEPS-18 are now verified by the CI evidence in the Challenge SDD tasks. This closes projection implementation acceptance only; it does not authorize Challenge business workflows. Open question 19, privacy/legal retention sign-off and owner-deferred AWS provisioning remain unchanged.

Updated: 2026-11-21

## Previous repository checkpoint before CI #151 — 2026-11-21

- Active branch: `feature/backend-foundation`. Current head at this documentation update: `ee99cdb24994a5a06c9efcdc5eeb8c624ff1b2a2` (an EventTracking-only follow-up; no Challenge files changed).
- CI #145 ran against Challenge commit `5fb1d41f6a775af88968d3277414fe77627edc10`. Restore, Release build (0 warnings/errors), Docker Compose validation, RabbitMQ startup/topology and all 14 Python topology tests passed. Challenge passed 55/55, Habit 24/24 and Identity 100/100. EventTracking passed 25/26; its sole failure was the API-host consumer-health test waiting for `/health/consumer` to return 503 after RabbitMQ shutdown. Thus 204/205 .NET tests passed and all 55 Challenge tests passed. [CI #145](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44882807000).
- The concurrent equal-lifecycle-version outcome race is closed: the MySQL 8.4 Testcontainers regression verifies exactly one Applied and one Stale, with the committed projection matching the winning event. The connection-scoped result flag is set by the atomic duplicate-key conditional upsert; CI #145 verified the full Challenge test project.
- CI #146 is running for the EventTracking-only follow-up commit `ee99cdb`; it is not a Challenge acceptance change. The current full-workflow failure remains outside the approved Challenge increment.
- Challenge scope remains projection-only. Open question 19, permanent inbox retention pending privacy/legal review and AWS EventBridge/SQS/CloudWatch provisioning remain deferred.


## Event Tracking RabbitMQ Testcontainers coverage

- Added broker-backed tests using RabbitMQ 4.1 Testcontainers for manual acknowledgement after processing completes, terminal outcome DLQ confirmation/classification, transient retry success and exhaustion, source redelivery when the mandatory DLQ route is unavailable, and log privacy.
- These tests use a dedicated disposable broker and the exact Event Tracking source/DLQ quorum topology. They do not modify the user's existing persistent broker volume.
- CI #137 completed successfully: EventTracking 17, Habit 24, Identity 100 and Challenge 54 — 195 .NET tests passed, plus all 14 Python topology tests. The API-host startup/MySQL integration test passed.
- The Event Tracking RabbitMQ broker tests were included in CI #142/#143. Both runs passed the new broker/consumer cases but failed the API-host consumer-health isolation test: 25/26 EventTracking tests passed; the failure waited for `/health/consumer` to return 503 after broker shutdown. This Event Tracking issue is outside the Challenge SDD scope.

Updated: 2026-11-21


## Event Tracking API-host startup CI verification

- CI #137 passed for commit `454e9496d8d53c6ea89c425e535704400a11c397`: Release build, Compose validation, RabbitMQ startup/topology and all automated tests succeeded. [CI #137](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44880083557).
- Test results: EventTracking 17, Habit 24, Identity 100 and Challenge 54 — **195 .NET tests passed**; all 14 Python RabbitMQ topology tests passed.
- EventTracking's MySQL 8.4 Testcontainers API-host startup test passed, proving the production API host applies its migration and resolves the production projection store/reader.
- Added RabbitMQ Testcontainers consumer tests for acknowledgement ordering, terminal DLQ classification/confirmation, transient retry success and exhaustion, DLQ route failure/redelivery, and logging privacy. CI verification for these newly added tests is pending.

Updated: 2026-11-21


## RabbitMQ integration-test compile correction

- CI #138 caught four compile errors in the new broker tests: xUnit selected its DateTime overload for `Assert.Equal(0, uintQueueCount)`. The queue-count assertions now use an explicit unsigned expected value (`0u`).
- This is a test-only compile correction. The test suite did not execute in CI #138; do not treat the new RabbitMQ integration tests as verified until the next CI run succeeds.

Updated: 2026-11-21

## Identity lifecycle API regression checkpoint — 2026-11-21

- CI #237 completed successfully against workflow/code commit `819242fe43a446fe1574e3025458d12a14331188`. The explicit pre-pull of `rabbitmq:4.0-alpine` fixed the Testcontainers image setup issue observed in CI #236.
- Full suite: Identity 105, Habit 24, Challenge 57, Event Tracking 27, Notification 28, Insight 15 and Insight.QueueMigration.IntegrationTests 8 — **264 passed, 0 failed, 0 skipped**. The 19 Python RabbitMQ topology contract tests, Compose validation, broker startup/topology and image pulls passed. Release build: 3 warnings, 0 errors.
- [CI #237](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44902295123); [test artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44902295123/artifacts/14705604839).
- ILC-16 (missing/non-GUID JWT subject rejected before mutation) is verified by Identity 105/105.
- Added ILC-17/18 tests for supplied and generated correlation UUID propagation across API response, lifecycle audit, outbox row and serialized event envelope. Test commit: `ff9c87b53c8943ed0719528b280304ac2e5199db`. Their CI verification is pending.
- Next action: wait for CI on the correlation propagation test commit before adding another code increment. Preserve the existing deferrals: invitations/activation design approval, role catalogue, Organization Admin request intake, HRIS, retention/legal sign-off, EventBridge cloud provisioning and Insight queue migration operational approval.

Updated: 2026-11-21



## Identity lifecycle correlation-header validation — CI #240 failure and correction

- CI #240 completed against `df805822b958cae6cd9580af019ca3cbf07d2558`. Release build, Compose validation, RabbitMQ startup/topology, all 19 Python topology tests and both pinned Testcontainers image pulls passed; test artifact uploaded.
- .NET results: Identity **108/109** (one failure in the newly added ILC-20 test); Habit 24, Challenge 57, Event Tracking 27, Notification 28, Insight 15 and Insight.QueueMigration.IntegrationTests 8. The failure was specific: the test expected HTTP 400 for an explicitly empty header but received HTTP 200. This is not a passing ILC-20 verification.
- Root cause was using parsed header value count to detect omission. Updated the controller to test header-key presence separately and reject a present header unless exactly one UUID value parses. Updated the regression to send a whitespace-only value. The correction is awaiting CI.
- [CI #240](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44903663508); [test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44903663508/artifacts/14706973358).

Updated: 2026-11-21
