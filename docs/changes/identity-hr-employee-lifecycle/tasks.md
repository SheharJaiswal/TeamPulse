## ILC-20 exact empty-value boundary regression — CI #241 verified

- [x] Add a controller-level test with an explicitly present X-Correlation-ID key and zero parsed StringValues.
- [x] Assert HTTP 400 and verify IEmployeeLifecycleService.ExecuteAsync is never called.
- [x] Verify the regression in GitHub Actions; CI #241 passed after the controller/header test correction (Identity 109/109; full suite 268/268).

## ILC-20 — explicit malformed correlation-header validation verified

- [x] Detect X-Correlation-ID header presence independently of parsed StringValues count.
- [x] Generate a UUID only when the header is absent; reject a present whitespace-only/malformed value with HTTP 400 before mutation.
- [x] Verify employee status/version remain unchanged and no lifecycle audit/outbox rows are created for the rejected request.
- [x] GitHub Actions CI #241 passed: Identity 109/109; Habit 24/24; Challenge 57/57; Event Tracking 27/27; Notification 28/28; Insight 15/15; Insight.QueueMigration.IntegrationTests 8/8 — **268 passed, 0 failed, 0 skipped**.
- [x] Restore, Release build, Compose validation, RabbitMQ startup/topology, all 19 Python topology tests, both pinned Testcontainers image pulls and test-results artifact upload passed; build had 3 warnings and 0 errors.
- [x] [CI #241](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44904270358); [test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44904270358/artifacts/14706578896).
- [x] No schema, queue topology, persistent broker data or AWS resource changed.

# Tasks: HR-Authorized Employee Lifecycle Commands

## SDD and policy reconciliation

- [x] Reconcile live `feature/backend-foundation` HEAD, recent commits, repository tree and prior checkpoint.
- [x] Read `AGENTS.md`, README, project status, open questions, Identity specification, ADR-0002, service ownership, communication conventions and API/event contracts.
- [x] Define the HR / Organization Admin field-ownership boundary without granting Organization Admin direct employee writes.
- [x] Separate the lifecycle command scope from employee invitation/provisioning, role-catalogue and HRIS decisions.
- [x] Define schema-v1 lifecycle events, idempotency, concurrency, audit/outbox atomicity and downstream projection contract.
- [x] Create proposal, specification, design and task checklist before implementation.

## Implementation

- [x] Add `LifecycleVersion` to Employee and an explicit status-transition domain method.
- [x] Add HrAdmin-only deactivate/reactivate endpoints; actor ID is taken only from validated JWT `sub`.
- [x] Add Application command/result abstractions and Infrastructure transaction implementation.
- [x] Add immutable lifecycle audit and transactional outbox entities/mappings.
- [x] Add checked-in EF Core migration `20261120060000_EmployeeLifecycleOutbox` and update model snapshot.
- [x] Implement SHA-256 request fingerprint and unique actor/idempotency-key guard.
- [x] Implement row-lock stale-command detection plus EF optimistic concurrency and safe conflict mapping.
- [x] Add unit tests for lifecycle version transitions.
- [x] Add MySQL integration tests for deactivate/reactivate, atomic rollback, no-op, replay, conflicting key reuse and concurrent retries.
- [x] Add API integration tests for anonymous, Employee, OrganizationAdmin and HrAdmin authorization, JWT actor attribution and request validation.
- [x] Add MySQL API-host tests for blank/overlength reason codes, overlength notes, no-mutation guarantees on 400 responses, and accepted maximum-length values with reason-code trimming.
- [x] Run restore, Release build, Compose validation and full Testcontainers suite in GitHub Actions run #72.
- [x] Record exact CI evidence: restore passed; Release build had 0 warnings/0 errors; Compose validation passed; 56 tests passed, 0 failed, 0 skipped.
- [x] Update Identity API/service contracts and project checkpoint with resulting code commit SHA.

## Explicitly deferred

- [ ] Employee invitations/provisioning and credential activation.
- [ ] Organization Admin request-intake/approval workflow.
- [ ] Role catalogue and elevated-role assignment policy.
- [ ] HRIS service authentication and feed reconciliation.
- [ ] EventBridge outbox dispatcher, retries, DLQ and delivery monitoring.
- [ ] Employee-status projection consumers in the five receiving services.


## Additional request-validation regression coverage — CI #231 passed

- Added integration tests for blank and overlength reason codes, overlength notes, and no employee/audit/outbox mutation on rejected requests.
- Added a successful maximum-boundary case for a 100-character reason code and 1,000-character note, including reason-code trimming.
- The tests use an explicit correlation ID to isolate the outbox assertion from unrelated rows in the shared MySQL test database.
- GitHub Actions CI #231 passed against code commit `55c192484534a54dd2aad62ccd2f1c0d26ab3774`; the full .NET suite passed: Identity 102, Habit 24, Challenge 57, Event Tracking 27, Notification 28, Insight 15 and Insight.QueueMigration.IntegrationTests 8 — **261 passed, 0 failed, 0 skipped**.
- [CI #231](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44900300510).

## Additional header-validation regression coverage — CI #233 passed

- Added a MySQL API-host integration test for whitespace-only and overlength idempotency keys, repeated Idempotency-Key values, and repeated X-Correlation-ID values.
- Rejected requests are checked for unchanged employee status/version, no audit rows, and no outbox rows for the supplied correlation IDs.
- Added acceptance criterion ILC-14 to the lifecycle specification; the test passed in GitHub Actions CI #233.
- CI #233 passed against code commit `d7dc8b8663e28e25c9beec395acd0ae48c628185`: Identity 103, Habit 24, Challenge 57, Event Tracking 27, Notification 28, Insight 15 and Insight.QueueMigration.IntegrationTests 8 — **262 passed, 0 failed, 0 skipped**.
- [CI #233](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44900792522). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44900792522/artifacts/14706141699).


## Idempotency-key character boundary regression — CI #235 passed

- Added API-host integration coverage for accepted one-character and 128-character visible-ASCII keys and rejected ASCII control, DEL and non-ASCII values.
- Rejected requests return HTTP 400 without changing employee status/version or creating lifecycle audit/outbox records; accepted keys complete the expected transition.
- CI #235 passed against code commit `0525db1487d96e35b92552cb586cb8fc508ebb70`: Identity 104, Habit 24, Challenge 57, Event Tracking 27, Notification 28, Insight 15 and Insight.QueueMigration.IntegrationTests 8 — **263 passed, 0 failed, 0 skipped**.
- [CI #235](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44901319799). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44901319799/artifacts/14705773167).

## JWT subject-claim validation regression — implementation added

- Added an API-host integration test using correctly signed HrAdmin tokens with either no `sub` claim or a non-GUID `sub`.
- Both requests must return 401 before lifecycle mutation; the test verifies employee status/version, audit and correlation-scoped outbox remain unchanged.
- Implementation commit: `d0b20c32c1c1ec7b7af8b138c571730d8f7be10a`. Identity 105/105 passed in CI #237; full workflow verification is recorded below.

## JWT subject-claim validation regression — CI #236 test passed; workflow image setup corrected

- Added an API-host integration test using correctly signed HrAdmin tokens with either no `sub` claim or a non-GUID `sub`. Both requests returned HTTP 401; the employee remained active at lifecycle version 0, with no audit rows and no outbox rows for either supplied correlation ID.
- Identity passed **105/105** tests in CI #236, confirming the new ILC-16 regression test passed.
- CI #236 overall failed for an unrelated environment setup issue: the Insight queue-migration Testcontainers test requested `rabbitmq:4.0-alpine`, but the GitHub runner had not pulled that image. Seven of eight Insight queue-migration tests passed; the failing test could not create its container.
- Corrected CI workflow to pre-pull the exact RabbitMQ image used by the Testcontainers fixture before running the suite. Workflow fix commit: `819242fe43a446fe1574e3025458d12a14331188`. Full workflow verification passed in CI #237 (264/264 .NET tests).
- [CI #236](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44901781762).


## Correlation-ID propagation regression coverage — implementation added

- Added a MySQL API-host integration test proving a supplied X-Correlation-ID is returned unchanged and persisted identically in the lifecycle audit, outbox row and serialized event envelope.
- Added a second integration test proving omission of X-Correlation-ID generates one non-empty UUID that is identical in the response, audit, outbox and event envelope.
- Added acceptance criteria ILC-17 and ILC-18 to the lifecycle specification.
- Test implementation commit: `ff9c87b53c8943ed0719528b280304ac2e5199db`. Verified by CI #238 (Identity 107/107).

## CI #237 — JWT subject validation and RabbitMQ image setup verified

- CI #237 passed for workflow-fix/code head `819242fe43a446fe1574e3025458d12a14331188`.
- The workflow now explicitly pulls `rabbitmq:4.0-alpine` before Testcontainers tests. The Insight queue-migration suite passed all 8 tests; the previous image-not-found failure is resolved.
- Full .NET results: Identity 105, Habit 24, Challenge 57, Event Tracking 27, Notification 28, Insight 15 and Insight.QueueMigration.IntegrationTests 8 — **264 passed, 0 failed, 0 skipped**.
- Restore, Release build, Docker Compose validation, RabbitMQ startup/topology, all 19 Python topology contract tests and both pinned test-image pulls passed. Build reported 3 warnings and 0 errors.
- [CI #237](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44902295123). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44902295123/artifacts/14705604839).



## CI #238 — correlation-ID propagation verified

- CI #238 passed for test commit `ff9c87b53c8943ed0719528b280304ac2e5199db`.
- ILC-17 and ILC-18 passed: supplied and generated correlation IDs were identical across the API response, lifecycle audit, outbox row and serialized event envelope.
- Full .NET results: Identity 107, Habit 24, Challenge 57, Event Tracking 27, Notification 28, Insight 15 and Insight.QueueMigration.IntegrationTests 8 — **266 passed, 0 failed, 0 skipped**.
- Restore, Release build, Docker Compose validation, RabbitMQ startup/topology, all 19 Python topology contract tests, and both pinned Testcontainers image pulls passed. Build reported 3 warnings and 0 errors (including one nullable warning in the lifecycle integration-test project).
- [CI #238](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44902665384). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44902665384/artifacts/14706737222).

## Correlation-ID idempotency replay regression — implementation added

- Added a sequential MySQL API-host integration test that submits the same lifecycle command twice using the same actor/idempotency key and different correlation IDs.
- The test asserts the first response is `transitioned`, the second is `replayed`, both expose the original event and correlation IDs, and exactly one employee lifecycle audit/outbox row exists with the original correlation ID also present in the serialized envelope.
- Added acceptance criterion ILC-19 to the lifecycle specification.
- Test commit: `dd1c2a310c4bd70ba86499e5ad4349a6097b2812`. Specification commit: `9d4a7130dbd4dc745611cda4356d73f199e47318`. Verified in CI #239 (Identity 108/108; full .NET suite 267/267).


## CI #239 — correlation-ID idempotency replay verified

- CI #239 passed for test commit `dd1c2a310c4bd70ba86499e5ad4349a6097b2812`.
- ILC-19 passed: sequential same-actor/same-command replay with a different retry correlation ID returned the original event and correlation IDs and created exactly one audit/outbox row.
- Full .NET results: Identity 108, Habit 24, Challenge 57, Event Tracking 27, Notification 28, Insight 15 and Insight.QueueMigration.IntegrationTests 8 — **267 passed, 0 failed, 0 skipped**.
- Restore, Release build, Compose validation, RabbitMQ startup/topology, all 19 Python topology contract tests, both pinned Testcontainers image pulls and test-result artifact upload passed. Build reported 3 warnings and 0 errors.
- [CI #239](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44903148180). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44903148180/artifacts/14706841604).

## Explicit empty correlation-ID validation — implementation added

- Corrected lifecycle header handling so UUID generation occurs only when X-Correlation-ID is absent. An explicitly supplied empty value now follows UUID parsing and is rejected with HTTP 400, consistent with the documented contract.
- Added a MySQL API-host regression test proving an explicitly empty correlation header returns 400 before changing employee status/version, creating a lifecycle audit or inserting an outbox row.
- Added acceptance criterion ILC-20. Verified in CI #241; Identity 109/109 and the full .NET suite 268/268 passed.
- Controller commit: `62b84ee4a68c9fbca1b48c8329a029b6183dc4ee`; test commit: `df805822b958cae6cd9580af019ca3cbf07d2558`.


## ILC-20 CI #240 failure analysis and correction

- CI #240 completed with failure against `df805822b958cae6cd9580af019ca3cbf07d2558`. Identity passed 108/109; the new explicit-empty-header regression expected HTTP 400 but received HTTP 200. The other suites passed: Habit 24, Challenge 57, Event Tracking 27, Notification 28, Insight 15 and Insight.QueueMigration.IntegrationTests 8. The 19 Python topology tests, build, Compose validation, RabbitMQ startup/topology and pinned image pulls passed.
- Root cause: the controller inferred header omission from `StringValues.Count == 0`. An explicitly present empty header can have zero parsed values, so this conflated header presence with value count. The test client's empty header serialization also did not preserve a distinguishable empty field.
- Corrected controller to use `Request.Headers.ContainsKey("X-Correlation-ID")` to distinguish absence from presence, then require exactly one parseable UUID when present. Changed the API-host regression to send a whitespace-only value, which exercises a present malformed header.
- The correction was verified in CI #241: ILC-20 passed with the whitespace-only header case returning HTTP 400 before mutation; the full .NET suite passed 268/268.
- [CI #240](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44903663508). [Test-results artifact](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44903663508/artifacts/14706973358).
