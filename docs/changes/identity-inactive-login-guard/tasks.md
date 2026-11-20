# Tasks: Deny Authentication for Inactive Employees

- [x] Reconcile the live branch checkpoint and inspect Identity login, Employee.IsActive, controller tests and MySQL integration fixture.
- [x] Create the SDD proposal, specification, design and task checklist before implementation.
- [x] Verify the submitted password for an existing employee, then reject inactive status before hash rehash, persistence or token issuance.
- [x] Add unit coverage for generic 401, exactly one password verification, no hash generation/rehash, no persistence and no token.
- [x] Add MySQL integration coverage using an inactive persisted employee; confirm the stored hash is unchanged.
- [x] Update Identity specification, API contract and Identity foundation checklist.
- [x] Run .NET 8 restore, Release build, Compose validation and full tests in GitHub Actions run #36.
- [x] Record exact CI results and final branch SHA in docs/project-status.md.
