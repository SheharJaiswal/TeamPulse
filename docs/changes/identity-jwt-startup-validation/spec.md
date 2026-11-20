# Specification: Fail-Fast Identity JWT Signing-Key Validation

## Acceptance criteria
- JWT-01: If the JWT signing-key configuration value is absent, Identity API startup fails with an actionable configuration error before database configuration or migration.
- JWT-02: If the signing key is blank, whitespace-only or fewer than 32 UTF-8 bytes, startup fails with the same actionable error.
- JWT-03: A signing key exactly 32 UTF-8 bytes long passes the signing-key validation and startup proceeds to the next configuration check.
- JWT-04: Existing valid test configuration continues to start the real Identity API and apply checked-in MySQL migrations through the existing integration suite.
- JWT-05: No local placeholder key, token, password or other secret is copied into the startup-validation test configuration.

## Constraints
- Preserve the approved .NET 8, EF Core 8, Pomelo and MySQL 8.4 baseline.
- Do not change the Identity API contract or token claims.
- Do not introduce key rotation or account lifecycle behavior in this increment.
