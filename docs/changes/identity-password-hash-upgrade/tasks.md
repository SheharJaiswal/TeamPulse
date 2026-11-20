# Tasks: Identity Password Hash Upgrade

- [x] Reconcile live branch HEAD, recent commits, recursive tree and project checkpoint.
- [x] Read engineering instructions, README, open questions, Identity specification/contract, service ownership, communication conventions and ADR-0001.
- [x] Confirm login currently accepts SuccessRehashNeeded without replacing the stored hash.
- [x] Add Employee password-hash update behavior.
- [x] Upgrade and persist hashes before issuing tokens.
- [x] Add unit tests for rehash, no-rehash, generic failure and persistence-failure behavior.
- [x] Add MySQL-backed legacy IdentityV2 hash upgrade integration test.
- [x] Update Identity specification, API contract and foundation checklist.
- [ ] Run .NET 8 restore/build/test and Docker Compose validation in CI; record exact result and commit SHA.
