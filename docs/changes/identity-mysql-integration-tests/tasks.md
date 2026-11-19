# Tasks: MySQL-backed Identity API Integration Tests

- [x] Reconcile the live branch HEAD and read engineering instructions, Identity spec, API contract, service ownership, event conventions and previous checkpoints.
- [x] Inspect the current Identity API, EF model/migration, test project and CI tree.
- [x] Define real-MySQL integration scope without changing business behavior.
- [x] Add Testcontainers.MySql and ASP.NET Core WebApplicationFactory dependencies.
- [x] Add an xUnit MySQL fixture and real API factory with test-only JWT configuration.
- [x] Add migration/restart, persistence/unique-index and authenticated /me tests.
- [x] Fix the top-level Program partial declaration for WebApplicationFactory access.
- [x] Add .NET 8 GitHub Actions restore/build/test workflow.
- [x] Update README and Identity foundation tasks; project status is the final checkpoint for this run.
- [ ] Execute tests on a Docker-capable .NET 8 runner; do not mark them passed until CI evidence exists.
