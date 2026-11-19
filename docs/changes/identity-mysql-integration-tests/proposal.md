# Proposal: MySQL-backed Identity API Integration Tests

## Problem
Identity unit tests cover controller logic and token/password primitives, but they do not exercise EF Core migrations, MySQL constraints, ASP.NET Core hosting, JWT bearer middleware or the authenticated /me endpoint. These boundaries are required to establish that the deployed service works as a complete API.

## Scope
Add an integration-test fixture that starts a disposable MySQL 8.4 container with Testcontainers, hosts the real Identity API through WebApplicationFactory, overrides test-only database/JWT configuration, and verifies migration startup, employee persistence, normalized-email uniqueness, bearer authentication and /me. Add a .NET 8 GitHub Actions workflow that runs restore, build and all tests on a Docker-capable runner.

## Out of scope
Changing Identity business behavior, HR provisioning, inactive-account lifecycle, refresh tokens, production migration orchestration, cross-service integration or replacing MySQL with an in-memory provider.

## Success criteria
- Tests run against a real MySQL 8.4 instance, not EF InMemory or SQLite.
- The API applies the checked-in migration before serving requests.
- Repeated application startup against the same database succeeds and preserves data.
- Registration persists an employee and the database unique index rejects a duplicate normalized email.
- Anonymous /me is denied and a valid issued bearer token returns identity claims from the authenticated principal.
- CI restores, builds and runs the full solution test suite using .NET 8 and Docker.
