# Proposal: Fail-Fast Identity JWT Signing-Key Validation

## Problem
The Identity API already rejects signing keys shorter than 32 UTF-8 bytes, but the guard only checks byte length. A blank or whitespace-only configuration value should be rejected explicitly with the same actionable startup error. The existing acceptance criterion IA-08 also lacks direct automated coverage for missing and weak keys.

## Outcome
Make the signing-key guard explicitly reject missing/blank values and add startup-level tests against the real minimal-hosting entry point. Verify that a key exactly 32 UTF-8 bytes long passes the key guard.

## Scope
- Tighten the existing startup validation without changing JWT claims, issuer/audience validation, token lifetime or persistence.
- Add WebApplicationFactory tests for a missing key, blank/short keys and the 32-byte boundary.
- Update the approved Identity specification and continuation records.

## Out of scope
Secret Manager integration, key rotation, asymmetric signing, refresh tokens, account lifecycle, HR provisioning and changes to the six-service architecture.

## Success criteria
- Missing, blank, whitespace-only and fewer-than-32-byte signing keys fail before database configuration/migration.
- Exactly 32 UTF-8 bytes passes the signing-key validation.
- Existing MySQL migration-at-startup and authentication integration tests remain unchanged and continue to pass.
