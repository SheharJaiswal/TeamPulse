# Tasks: Fail-Fast Identity JWT Signing-Key Validation

- [x] Reconcile the live branch HEAD, recent commit history, repository tree and previous checkpoint.
- [x] Read engineering instructions, Identity specification/contract, service ownership, communication conventions, ADR-0001 and existing Identity tests.
- [x] Verify PostgreSQL cleanup CI run #24 completed successfully.
- [x] Define missing/blank/short key and exact 32-byte boundary behavior.
- [x] Tighten the startup guard and extract the reusable UTF-8 byte validator.
- [x] Remove the committed local signing-key placeholder from appsettings.
- [x] Update README with local environment-variable setup and production secret guidance.
- [x] Add real-entry-point startup tests for missing and weak keys plus UTF-8 byte-boundary unit tests.
- [x] Diagnose CI run #25 failure: the configuration-source clearing approach did not override appsettings as intended, so tests used the committed placeholder and attempted MySQL startup.
- [x] Replace the test-host configuration setup with the proven UseSetting/in-memory override pattern and remove the appsettings fallback.
- [ ] Run .NET 8 restore/build/test and Docker Compose validation in GitHub Actions.
- [ ] Record final CI evidence and update docs/project-status.md with the next single task.
