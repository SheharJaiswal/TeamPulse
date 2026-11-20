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
- [x] Run .NET 8 restore, Release build, Compose validation and full Testcontainers suite in GitHub Actions run #51.
- [x] Record exact CI evidence: restore passed; Release build passed with 0 warnings/0 errors; Compose validation passed; 49 tests passed, 0 failed, 0 skipped.
- [x] Record final code commit 71e2208442ec0224726185f8cae1a15da9767135 in docs/project-status.md.
