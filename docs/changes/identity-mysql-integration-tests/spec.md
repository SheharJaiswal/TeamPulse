# Specification: MySQL-backed Identity API Integration Tests

## Acceptance criteria
- IMT-01: The test fixture starts MySQL 8.4 in a disposable Testcontainers container and supplies an isolated test connection string to the real Identity API host.
- IMT-02: Starting the API applies the checked-in InitialIdentitySchema migration and creates the employees table and EF migration history.
- IMT-03: Starting a second API host against the same database is safe, leaves the migration applied once and preserves previously persisted employee data.
- IMT-04: POST /api/identity/register persists the employee and a one-way password hash in MySQL.
- IMT-05: Re-registering the same normalized email returns the documented HTTP 409 response.
- IMT-06: The MySQL unique index on NormalizedEmail rejects a second row with the same normalized email, independently of controller pre-check behavior.
- IMT-07: GET /api/identity/me returns HTTP 401 without a bearer token.
- IMT-08: A bearer token issued by the real login/register endpoint is accepted by JWT middleware and /me returns subject, email, display name, organization, team and role claims.
- IMT-09: GitHub Actions uses .NET 8 to restore, build and execute the full solution test suite on a Docker-capable runner.

## Constraints
- Use MySQL 8.4, EF Core 8 and Pomelo as specified in ADR-0001.
- Use the checked-in migrations and the production Program/DI/authentication pipeline; do not substitute an in-memory database.
- Test-only JWT values are ephemeral and must not be copied to production configuration.
- Tests use unique email addresses and must not rely on execution order.
- No new business rules or public API contract changes are introduced.
