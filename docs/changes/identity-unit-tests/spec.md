# Specification: Identity Unit Test Baseline

## Acceptance criteria
- UT-01: Password hashing produces a value different from the supplied password.
- UT-02: The password hasher verifies the original password successfully.
- UT-03: The password hasher rejects a different password.
- UT-04: Identity token contains the employee subject, email, display name, organization, team and role claims.
- UT-05: Token signature validates against the configured signing key.
- UT-06: Token issuer and audience match configured values.
- UT-07: Token lifetime is present and is in the future when generated.

## Constraints
- Tests target .NET 8 and use the same ASP.NET Core password hasher and JWT implementation as the service.
- Tests must not connect to MySQL or require secrets from appsettings.
- Integration behavior remains explicitly unverified until a MySQL-backed test environment is added.
