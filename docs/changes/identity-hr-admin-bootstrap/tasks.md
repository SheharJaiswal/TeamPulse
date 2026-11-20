# Tasks: Controlled Initial HR Administrator Bootstrap

- [ ] Reconcile live branch and inspect Identity dependency injection, project/solution structure, EF model and Testcontainers fixture.
- [ ] Add console bootstrap project without adding a seventh microservice.
- [ ] Reuse Identity database and ASP.NET Core password hashing.
- [ ] Implement validated secret-backed configuration; do not accept password in process arguments.
- [ ] Implement one-time HR administrator guard with database-level concurrency protection.
- [ ] Ensure duplicate email, existing HR administrator and persistence failures make no mutation.
- [ ] Add unit tests for validation and hash construction.
- [ ] Add MySQL integration tests for success, duplicate/admin guard and concurrent invocation.
- [ ] Add an operator runbook documenting deployment ordering and secret removal/rotation.
- [ ] Run .NET 8 restore, Release build, Compose validation and full tests in GitHub Actions.
- [ ] Record exact CI evidence and final branch SHA in docs/project-status.md.
