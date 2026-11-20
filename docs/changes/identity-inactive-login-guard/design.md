# Design: Deny Authentication for Inactive Employees

## Login flow
After normalized-email lookup, check IsActive before password verification. If inactive, return the existing generic unauthorized response. This avoids password-hasher work, password rehash, persistence writes and token issuance for inactive records.

## Test strategy
- Unit test with an inactive Employee and a spy password hasher/token service. Assert generic 401, zero password verification/hash calls, zero persistence calls and zero token calls.
- MySQL integration test provisions an employee, marks the existing row inactive directly in the test database (without adding a production lifecycle API), then attempts login through the real API. Assert generic 401 and unchanged persisted hash.
- Retain existing active-login, legacy-hash-upgrade and generic-invalid-credential tests.

## Lifecycle boundary
HR authority, status transitions, reason codes, audit persistence, outbox events and downstream projections are specified in ADR-0002 but intentionally not implemented here.
