# Design: Development-Only Identity Registration

## API guard
Inject `IHostEnvironment` into `IdentityController`. At the very beginning of `Register`, check `IsDevelopment()`. If false, return `NotFound()` before validation, repository reads, password hashing or writes. The route remains mapped, but the bootstrap capability is not discoverable through a successful response outside Development.

## Tests
- Unit tests use a small fake `IHostEnvironment` to assert 404 and zero repository mutations in Production, Staging and Testing. Existing successful registration unit tests run in Development.
- Integration factory accepts an environment name. Its default is Development for existing registration bootstrap tests. New tests run the real API host against the existing disposable MySQL 8.4 fixture in Production and Testing and assert 404 plus no matching employee row.
- No schema migration is required.

## Operational notes
Set `ASPNETCORE_ENVIRONMENT=Development` only for local bootstrap workflows. Hosted deployments must use a non-Development environment. The guard is not a substitute for future HR provisioning.
