# Tasks: Local RabbitMQ Outbox Transport

## SDD and contracts
- [x] Reconcile the live feature branch and existing dispatcher/infra contracts.
- [x] Record the owner's decision to defer EventBridge provisioning and use a local .NET-compatible broker.
- [x] Define provider selection, RabbitMQ exchange/queue contract and at-least-once semantics.
- [x] Update communication and event-envelope contracts.

## Implementation
- [x] Add RabbitMQ options and transport validation.
- [x] Add RabbitMQ.Client publisher with durable topic exchange, persistent messages, confirms and mandatory routing.
- [x] Select RabbitMQ or EventBridge explicitly in Identity API dependency injection.
- [x] Add RabbitMQ service and imported local topology to Docker Compose.
- [x] Add RabbitMQ Testcontainers fan-out and unroutable-publish tests.
- [x] Add local topology contract tests and CI validation.

## Verification and completion
- [x] Restore and build the full .NET 8 solution.
- [x] Run all unit and MySQL/RabbitMQ Testcontainers tests.
- [x] Validate Docker Compose and local RabbitMQ topology.
- [x] Update README, open questions and project checkpoint with exact results.
- [x] Commit the coherent implementation to feature/backend-foundation only.

## Verification results

- GitHub Actions run #92: 100 .NET tests passed, 0 failed, 0 skipped; four Python topology contract tests passed.
- GitHub Actions run #93: restore, Release build (0 warnings/errors), Compose config, real RabbitMQ Compose startup/health, exact exchange/five-queue assertions, four Python topology tests and 100 .NET tests all passed.
- The first implementation run failed compilation on an integer-to-`DeliveryModes` assignment; corrected in commit `4e923bd96d727caa4f1c067ce8adaa5d714698d6`. No failing code was left in the final verified commit.
- No AWS deployment or EF migration was required.
