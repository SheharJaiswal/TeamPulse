# Tasks: Employee Activity Summary Insights

**Status:** Proposed; implementation blocked until owner approves proposal/spec/design.

## SDD
- [ ] Confirm metric and API scope with owner.
- [ ] Read/align ownership, event contracts, RabbitMQ conventions and existing Insight lifecycle spec.
- [ ] Finalize proposal/spec/design and acceptance mapping.

## Implementation
- [ ] Add daily activity summary domain/application contracts and processor.
- [ ] Add DynamoDB transaction/inbox and bounded date query.
- [ ] Add dedicated Insight activity RabbitMQ consumer and DLQ handling.
- [ ] Add employee-self-only API and JWT subject validation.
- [ ] Add queue/DLQ bindings and topology contract tests.
- [ ] Add unit, DynamoDB Local, API and RabbitMQ integration tests.
- [ ] Update README/local configuration and project status.
- [ ] Run Release build and complete backend/Insight/topology CI.

## Review and handoff
- [ ] Review privacy, cross-employee access, atomicity, event compatibility and queue topology.
- [ ] Open PR to `feature/backend-foundation`.
- [ ] Merge only after review and required CI pass.
