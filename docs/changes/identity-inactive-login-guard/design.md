# Design: Deny Authentication for Inactive Employees

## Login flow
After normalized-email lookup, verify the submitted password against the stored hash. If verification fails, return the generic unauthorized response. If verification succeeds or requests rehash but the employee is inactive, return the same generic unauthorized response before any rehash, persistence write or token issuance. Performing password verification for an existing inactive record avoids a distinct fast path for that status.

## Test strategy
- Unit test with an inactive Employee and a spy password hasher/token service. Assert generic 401, exactly one password verification, zero hash-generation/rehash calls, zero persistence calls and zero token calls.
- MySQL integration test provisions an employee, marks the existing row inactive directly in the test database (without adding a production lifecycle API), then attempts login through the real API. Assert generic 401 and unchanged persisted hash.
- Retain existing active-login, legacy-hash-upgrade and generic-invalid-credential tests.

## Lifecycle boundary
HR authority, status transitions, reason codes, audit persistence, outbox events and downstream projections are specified in ADR-0002 but intentionally not implemented here.
