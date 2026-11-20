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
- [ ] Restore and build the full .NET 8 solution.
- [ ] Run all unit and MySQL/RabbitMQ Testcontainers tests.
- [ ] Validate Docker Compose and local RabbitMQ topology.
- [ ] Update README, open questions and project checkpoint with exact results.
- [ ] Commit the coherent implementation to feature/backend-foundation only.
