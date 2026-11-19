# Design: MySQL-backed Identity API Integration Tests

## Hosting and database lifecycle
Use Testcontainers.MySql with the MySQL 8.4 image. An xUnit class fixture owns one container for the integration-test class. Each test creates a WebApplicationFactory<Program> configured with the container connection string and test-only JWT issuer, audience and signing key through in-memory configuration. The host uses the actual Program.cs registrations and calls Database.MigrateAsync() during startup.

## Test scenarios
1. Start an API host, confirm the migration is applied and register an employee. Dispose that host, start a second host against the same database, and verify startup succeeds, the migration remains applied and the employee is still present.
2. Register an employee through HTTP, verify the stored hash with ASP.NET Core PasswordHasher, retry with a case/whitespace variation of the email and expect HTTP 409. Attempt a direct second insert with the same NormalizedEmail and expect EF Core DbUpdateException from MySQL's unique index.
3. Call /me anonymously and expect HTTP 401. Register or log in through the API, send the issued bearer token and assert /me returns all documented identity claims.

## CI
Add a GitHub Actions workflow triggered by pushes and pull requests targeting main or feature/backend-foundation. Use actions/setup-dotnet with .NET 8, run restore/build/test against backend/TeamPulse.sln, and retain TRX output. GitHub-hosted Ubuntu runners provide Docker for Testcontainers.

## Production boundaries
No production data or configuration is changed. Tests use a separate ephemeral container/database and deterministic test-only JWT settings. Startup migrations remain as currently approved for local/single-instance use; production multi-instance migration orchestration remains a separate deployment concern.

## Risks
Testcontainers requires a working Docker daemon. Local environments without Docker can run unit tests only; CI is the authoritative Docker-capable integration-test runner. Package versions are pinned and should be upgraded only with SDK/provider compatibility validation.
