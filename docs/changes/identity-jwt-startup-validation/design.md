# Design: Fail-Fast Identity JWT Signing-Key Validation

## Production change
Keep signing-key validation in the Identity API startup path, before the connection string is read and before the service provider/database is built. Extract the reusable check into `JwtSigningKeyValidator`; reject null, blank and whitespace-only values and require at least 32 UTF-8 bytes. Use one stable, actionable error message. Remove the committed local placeholder from appsettings so a missing deployment secret cannot silently fall back to a known value.

## Test design
Use a dedicated WebApplicationFactory and the same `UseSetting` plus in-memory configuration override pattern as the existing `IdentityApiFactory`. Do not clear configuration sources: the earlier CI attempt showed that the test host still reached the appsettings MySQL connection string when the callback was used incorrectly.

- Missing-key test leaves SigningKey unset; appsettings no longer provides a fallback.
- Weak-key theory covers empty, short, 31-byte and whitespace-only values.
- Boundary unit tests verify exactly 32 UTF-8 bytes is accepted and 31 bytes is rejected.
- Existing Testcontainers integration tests prove a fully configured API starts and applies MySQL migrations.

## Security and isolation
No signing key is stored in appsettings or committed source. Tests use only ephemeral test values. Startup-validation tests fail before the database connection/migration when the key is missing or weak.
