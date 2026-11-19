# Design: Identity Unit Test Baseline

## Test architecture
Create backend/tests/Identity.UnitTests as an xUnit test project referencing Identity.Infrastructure. Use an in-memory Employee instance and test-only JWT options. Validate the JWT through JwtSecurityTokenHandler.ValidateToken with issuer, audience, lifetime and signing-key checks enabled.

Use ASP.NET Core's PasswordHasher<Employee> directly for hashing tests. Avoid mocking infrastructure because these tests target deterministic framework/security primitives.

## Dependencies
- Microsoft.NET.Test.Sdk
- xunit and xunit.runner.visualstudio
- Framework reference to Microsoft.AspNetCore.App
- Project reference to Identity.Infrastructure (which already references Domain and Application)

## Risks / limitations
These tests do not validate controller request validation, database uniqueness constraints, EF migration execution, authentication middleware or /me authorization. Those remain separate integration-test work.
