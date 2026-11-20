# Design: Controlled Initial HR Administrator Bootstrap

## Execution model
Add a dedicated console project (not a controller route) that composes the existing Identity Infrastructure registration and MySQL persistence. It is invoked explicitly by a deployment/operator command after the Identity schema has been migrated. Normal API startup does not create or seed privileged accounts.

## Configuration
Read the initial administrator email, display name, organization ID, team ID and password from environment variables or the deployment secret store. The password must not be accepted in command-line arguments. Reuse the normal Identity database connection string and ASP.NET Core `PasswordHasher<Employee>`. Validate all values before attempting a write. Do not provide committed defaults.

## One-time guard and transaction
Within one database transaction:
1. Query for any employee whose role is `HrAdmin`, including inactive employees.
2. If one exists, abort without mutation; do not reactivate or alter that account.
3. Normalize and check the configured email; abort if already used by any employee.
4. Create one active Employee with role `HrAdmin` and a one-way password hash.
5. Commit once. Any failure rolls back and returns a non-zero exit code.

The database unique normalized-email index remains the final duplicate-email guard. Concurrent bootstrap attempts must not create multiple HR administrators; enforce this with a database-level serialization/locking strategy or a narrowly scoped bootstrap lock, not only an in-process check.

## Operational safeguards
- Require explicit invocation and emit a clear success/failure result without printing sensitive configuration.
- Never echo the password or hash, including exception details.
- Require the operator to remove/rotate the bootstrap secret after successful use.
- The utility must refuse to run when the target database has not been migrated; it does not run migrations itself.
- Keep the Development-only registration endpoint unchanged and unavailable in non-Development environments.

## Test strategy
- Unit tests for configuration validation and role/hash construction.
- MySQL-backed integration tests for first-run success, duplicate HR administrator refusal, existing-email refusal, and no mutation on failure.
- A concurrency test or database-lock test proves two simultaneous invocations cannot create two HR administrators.
- CI must run restore, Release build, Compose validation and the full Testcontainers suite.

## Follow-on boundary
This utility establishes the initial HR authority only. A separate SDD increment will implement HR-authorized employee provisioning and lifecycle commands, audit persistence, transactional outbox publication and downstream employee-status projections.
