# Design: Fail-Fast Identity JWT Signing-Key Validation

## Production change
Keep the guard in the Identity API entry point, before the connection string is read and before the service provider/database is built. Check both `string.IsNullOrWhiteSpace` and `Encoding.UTF8.GetByteCount`; the configured secret must contain at least 32 UTF-8 bytes. Use one stable, actionable error message for absent, blank and short values.

## Test design
Add a dedicated WebApplicationFactory that clears application configuration sources and supplies only JWT issuer, audience and token lifetime. This prevents appsettings' local placeholder key from leaking into the missing-key test. Optionally add a test signing key.

- Missing-key test omits the SigningKey key entirely.
- Weak-key theory covers a short value, a 31-byte value and whitespace-only input.
- Boundary test supplies exactly 32 ASCII bytes and expects startup to progress beyond JWT validation to the intentionally absent database connection string.
- Existing Testcontainers integration tests prove a complete valid configuration still starts and migrates MySQL.

## Isolation and security
Tests use non-production test identifiers and no real credentials. The validation host intentionally omits the database connection string, so no database connection or migration is attempted by these tests.
