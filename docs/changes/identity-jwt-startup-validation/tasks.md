# Tasks: Fail-Fast Identity JWT Signing-Key Validation

- [x] Reconcile the live branch HEAD, recent commit history, repository tree and previous checkpoint.
- [x] Read engineering instructions, Identity specification/contract, service ownership, communication conventions, ADR-0001 and existing Identity tests.
- [x] Verify PostgreSQL cleanup CI run #24 completed successfully.
- [x] Define missing/blank/short key and exact 32-byte boundary behavior.
- [x] Tighten the startup guard to reject blank/whitespace signing keys explicitly.
- [x] Add real-entry-point startup tests for missing, weak and exactly-32-byte keys.
- [x] Update the approved Identity specification and previous cleanup checklist.
- [x] Update docs/project-status.md with implementation commit, current CI state and the single next task.
- [ ] Run .NET 8 restore/build/test and Docker Compose validation in GitHub Actions (run #25 in progress).
- [ ] Record final CI evidence and update Identity acceptance checklists after the run completes.
