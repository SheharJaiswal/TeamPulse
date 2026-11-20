# Proposal: Upgrade Legacy Identity Password Hashes During Login

## Problem
ASP.NET Core PasswordHasher can successfully verify a password and return SuccessRehashNeeded when the stored hash uses an older compatibility format. The current login action accepts that result but does not replace the weaker/legacy hash, so successful logins never migrate those credentials to the current configured format.

## Scope
When verification returns SuccessRehashNeeded, create a new hash with the currently registered password hasher, update the tracked Employee password hash and persist it before issuing the access token. Preserve the existing generic failure response and response contract. Add deterministic controller tests and a MySQL-backed integration test using an IdentityV2 legacy hash.

## Out of scope
Password policy changes, password reset, refresh tokens, account lifecycle/deactivation, changes to hasher configuration, schema changes and HR provisioning.

## Success criteria
- Legacy valid passwords are rehashed using the currently configured hasher.
- The replacement hash is persisted before token creation.
- A persistence failure prevents token issuance.
- Current hashes are not rewritten unnecessarily.
- Invalid passwords still receive the same generic 401 response.
- MySQL schema and migrations remain unchanged.
