# Proposal: Fail-Fast Identity JWT Signing-Key Validation

## Problem
The Identity API has a minimum 32 UTF-8-byte signing-key requirement, but the committed appsettings file contains a local placeholder key. If a deployment omits its environment secret, that placeholder can silently remain active. The startup tests also need to isolate configuration using the same proven WebApplicationFactory override pattern as the existing integration tests.

## Outcome
Require a signing key to be supplied externally, remove the committed placeholder, reject blank/whitespace/short values before database setup, and add startup-level tests against the real minimal-hosting entry point.

## Scope
- Extract the key-length/blank check into a small API validation helper used by Program.
- Remove the signing key from checked-in appsettings.
- Document local environment-variable setup and production secret injection.
- Add WebApplicationFactory tests for a missing key and weak keys, plus UTF-8 byte-boundary unit tests.
- Update the approved Identity specification and continuation records.

## Out of scope
Secret Manager integration, key rotation, asymmetric signing, refresh tokens, account lifecycle, HR provisioning and changes to the six-service architecture.

## Success criteria
- No JWT signing key is stored in appsettings or source control.
- Missing, blank, whitespace-only and fewer-than-32-byte signing keys fail before database configuration/migration.
- Exactly 32 UTF-8 bytes is accepted by the validator.
- Existing MySQL migration-at-startup and authentication integration tests remain unchanged and continue to pass.
