# Tasks: Employee Activity Summary Insights

**Status:** Implementation in progress; CI verification pending.

## SDD
- [x] Confirm metric, storage and self-only API scope with owner.
- [x] Align ownership, event contracts, RabbitMQ conventions and existing Insight lifecycle spec.
- [x] Finalize proposal/spec/design and acceptance mapping.

## Implementation
- [x] Add daily activity summary contracts and event processor.
- [x] Add MySQL schema/migration and transactional inbox + aggregate store.
- [x] Add dedicated Insight activity RabbitMQ consumer and DLQ handling.
- [x] Add employee-self-only API and JWT subject validation.
- [x] Add queue/DLQ bindings and topology contract tests.
- [x] Add event processor unit tests.
- [x] Update README/local configuration.
- [x] Add MySQL Testcontainers coverage for inbox deduplication and concurrent aggregate increments (CI verification pending).
- [ ] Add/verify API authorization and RabbitMQ acknowledgement/DLQ integration tests.
- [ ] Run Release build and complete backend/Insight/topology CI; fix any failures.

## Review and handoff
- [ ] Review privacy, cross-employee access, atomicity, event compatibility and queue topology.
- [ ] Update PR to ready for review only after checks pass.
- [ ] Merge only after review and required CI pass.
