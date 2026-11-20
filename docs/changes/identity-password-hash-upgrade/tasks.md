# Tasks: Identity Password Hash Upgrade

- [x] Reconcile live branch HEAD, recent commits, recursive tree and project checkpoint.
- [x] Read engineering instructions, README, open questions, Identity specification/contract, service ownership, communication conventions and ADR-0001.
- [x] Confirm login currently accepts SuccessRehashNeeded without replacing the stored hash.
- [x] Add Employee password-hash update behavior.
- [x] Upgrade and persist hashes before issuing tokens.
- [x] Add unit tests for rehash, no-rehash, generic failure and persistence-failure behavior.
- [x] Add MySQL-backed legacy IdentityV2 hash upgrade integration test.
- [x] Update Identity specification, API contract and foundation checklist.
- [x] Run CI workflow #29 (44670262475): restore passed; Release build passed with 0 warnings and 0 errors; Docker Compose validation passed; 37 tests passed, 0 failed, 0 skipped.
- [x] Record exact workflow evidence and implementation commit 2c0551cb6b988e9d5d5970bce3a6abc6123fd118 in docs/project-status.md.
