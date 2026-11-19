# Tasks: MySQL-backed Identity API Integration Tests

- [x] Reconcile the live branch HEAD and read engineering instructions, Identity spec, API contract, service ownership, event conventions and previous checkpoints.
- [x] Inspect the current Identity API, EF model/migration, test project and CI tree.
- [x] Define real-MySQL integration scope without changing business behavior.
- [ ] Add Testcontainers.MySql and ASP.NET Core WebApplicationFactory dependencies.
- [ ] Add an xUnit MySQL fixture and real API factory with test-only JWT configuration.
- [ ] Add migration/restart, persistence/unique-index and authenticated /me tests.
- [ ] Fix the top-level Program partial declaration if needed for WebApplicationFactory access.
- [ ] Add .NET 8 GitHub Actions restore/build/test workflow.
- [ ] Update Identity foundation tasks and project status with exact verification results.
- [ ] Execute tests on a Docker-capable .NET 8 runner; do not mark them passed until CI evidence exists.
