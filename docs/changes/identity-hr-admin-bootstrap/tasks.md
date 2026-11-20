# Tasks: Controlled Initial HR Administrator Bootstrap

- [x] Reconcile the live branch and inspect Identity dependency injection, project/solution structure, EF model and Testcontainers fixture.
- [x] Add a dedicated console bootstrap project without adding a seventh microservice.
- [x] Reuse the Identity database, EF model and ASP.NET Core password hashing.
- [x] Implement validated secret-backed configuration; the password is not accepted in process arguments.
- [x] Implement a one-time HR administrator guard with a MySQL connection-scoped advisory lock and transaction.
- [x] Ensure duplicate email, existing active/inactive HR administrator and persistence failures make no account mutation.
- [x] Add unit tests for configuration validation and password policy.
- [x] Add MySQL integration tests for successful creation, password hashing, duplicate email, existing active/inactive administrator and concurrent invocation.
- [x] Add an operator runbook documenting deployment ordering and secret removal/rotation.
- [ ] Run .NET 8 restore, Release build, Compose validation and full Testcontainers suite in GitHub Actions.
- [ ] Record exact CI evidence and final branch SHA in docs/project-status.md.
