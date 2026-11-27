# Tasks: Employee Activity Summary Insights

**Status:** Approved for implementation.

## SDD
- [x] Confirm metric, storage and self-only API scope with owner.
- [x] Align ownership, event contracts, RabbitMQ conventions and existing Insight lifecycle spec.
- [x] Finalize proposal/spec/design and acceptance mapping.

## Implementation
- [ ] Add daily activity summary domain/application contracts and processor.
- [ ] Add MySQL schema/migration and transactional inbox + aggregate store.
- [ ] Add dedicated Insight activity RabbitMQ consumer and DLQ handling.
- [ ] Add employee-self-only API and JWT subject validation.
- [ ] Add queue/DLQ bindings and topology contract tests.
- [ ] Add unit, MySQL, API and RabbitMQ integration tests.
- [ ] Update README/local configuration and project status.
- [ ] Run Release build and complete backend/Insight/topology CI.

## Review and handoff
- [ ] Review privacy, cross-employee access, atomicity, event compatibility and queue topology.
- [ ] Open/update PR to `feature/backend-foundation`.
- [ ] Merge only after review and required CI pass.
