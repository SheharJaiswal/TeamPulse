# Specification: Controlled Initial HR Administrator Bootstrap

## Acceptance criteria
- HRB-01: A dedicated command-line utility creates an initial Identity employee with the canonical role `HrAdmin`.
- HRB-02: The utility is operator-invoked and is not exposed through the Identity HTTP API.
- HRB-03: Initial administrator email, display name, organization ID and team ID are validated before persistence.
- HRB-04: The initial password is read only from protected environment/secret-store configuration, must meet the existing minimum password length, and is never accepted as a command-line argument.
- HRB-05: The password is persisted only as an ASP.NET Core PasswordHasher hash.
- HRB-06: If any employee already has role `HrAdmin` (active or inactive), the command refuses to create another bootstrap administrator and makes no database mutation.
- HRB-07: The command never changes the role or status of an existing employee.
- HRB-08: Missing configuration, invalid input, database errors or failed persistence produce a non-zero process exit code and no success message.
- HRB-09: Logs never contain passwords, password hashes, signing keys or database credentials.
- HRB-10: Existing Development-only registration behavior and production 404 behavior remain unchanged.

## Security and data constraints
- Identity remains the sole owner of employee identity and roles.
- MySQL 8.4, EF Core 8, Pomelo and the checked-in migration workflow remain unchanged.
- Bootstrap must run against the already-migrated Identity database using a least-privilege deployment/operator connection.
- No secret is committed to source control or included in process arguments.
