# Specification: Fail-Fast Identity JWT Signing-Key Validation

## Acceptance criteria
- [x] JWT-01: If the JWT signing-key configuration value is absent, Identity API startup fails with an actionable configuration error before database configuration or migration.
- [x] JWT-02: If the signing key is blank, whitespace-only or fewer than 32 UTF-8 bytes, startup fails with the same actionable error.
- [x] JWT-03: A signing key exactly 32 UTF-8 bytes long is accepted by the same validator invoked during startup.
- [x] JWT-04: Existing valid test configuration continues to start the real Identity API and apply checked-in MySQL migrations through the existing integration suite.
- [x] JWT-05: No local placeholder key, token, password or other secret is copied into the startup-validation test configuration.
- [x] JWT-06: The checked-in appsettings file contains no JWT signing key; local and deployed environments supply it externally.

## Verification
GitHub Actions run #27 for commit `a83fb6464f51b1989a9a53d36e0308ff0cbb33fe` completed successfully:
- Restore succeeded.
- Release build succeeded with 0 warnings and 0 errors.
- Docker Compose validation succeeded.
- All 28 tests passed; 0 failed and 0 skipped.

## Constraints
- Preserve the approved .NET 8, EF Core 8, Pomelo and MySQL 8.4 baseline.
- Do not change the Identity API contract or token claims.
- Do not introduce key rotation or account lifecycle behavior in this increment.
