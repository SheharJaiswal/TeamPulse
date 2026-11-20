# Tasks: Fail-Fast Identity JWT Signing-Key Validation

- [x] Reconcile the live branch HEAD, recent commit history, repository tree and previous checkpoint.
- [x] Read engineering instructions, Identity specification/contract, service ownership, communication conventions, ADR-0001 and existing Identity tests.
- [x] Verify PostgreSQL cleanup CI run #24 completed successfully.
- [x] Define missing/blank/short key and exact 32-byte boundary behavior.
- [x] Tighten the startup guard and extract the reusable UTF-8 byte validator.
- [x] Remove the committed local signing-key placeholder from appsettings.
- [x] Update README with local environment-variable setup and production secret guidance.
- [x] Add real-entry-point startup tests for missing and weak keys plus UTF-8 byte-boundary unit tests.
- [x] Diagnose CI run #25 failure and correct test-host configuration isolation.
- [x] Diagnose CI run #26 failure and add the required System import and nullable context.
- [x] Run .NET 8 restore/build/test and Docker Compose validation in GitHub Actions.
- [x] Verify CI run #27: restore passed; Release build passed with 0 warnings/errors; Compose validation passed; 28 tests passed, 0 failed, 0 skipped.
- [x] Update Identity acceptance checklists and docs/project-status.md with verification evidence and the next single task.
