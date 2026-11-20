# Specification: Deny Authentication for Inactive Employees

## Acceptance criteria
- IA-09a: When an employee exists but IsActive is false, POST /api/identity/login returns HTTP 401 with the same generic "Invalid email or password." response used for other invalid credentials.
- IA-09b: An inactive employee receives no access token.
- IA-09c: The password hasher is not invoked for an inactive employee; therefore no legacy password-hash upgrade occurs.
- IA-09d: The stored password hash remains unchanged after an inactive login attempt.
- IA-09e: Active employee login, password-hash upgrade and token behavior remain unchanged.
- IA-09f: The inactive account response does not reveal whether the email exists or the account is inactive.

## Constraints
- Do not add activation/deactivation endpoints or change IsActive through this change.
- Do not add an audit schema without a separate approved SDD change.
- Keep Identity as the employee record owner; no cross-service database access.
- Preserve MySQL 8.4, EF Core 8, Pomelo and checked-in startup migrations.
