# Identity HR Administrator Bootstrap Runbook

Use this procedure once per new TeamPulse Identity database, after the checked-in Identity migrations have been applied and before enabling HR-only administrative workflows.

## Preconditions
- Run from a trusted deployment/operator environment with network access to the Identity MySQL database.
- Use a least-privilege database identity scoped to the Identity database.
- Store the initial administrator password in the deployment secret manager; do not put it in a shell argument, script, command history or source control.
- Confirm that the Identity schema is already migrated. The bootstrap utility does not apply migrations.
- Use the exact organization and team identifiers provisioned for the initial HR administrator.

## Configure protected environment
Set the database connection string through the standard `ConnectionStrings__IdentityDatabase` environment variable. Set these additional values through the protected environment/secret injection mechanism:

- `IdentityBootstrap__HrAdmin__Email`
- `IdentityBootstrap__HrAdmin__DisplayName`
- `IdentityBootstrap__HrAdmin__OrganizationId`
- `IdentityBootstrap__HrAdmin__TeamId`
- `IdentityBootstrap__HrAdmin__Password`

The password must contain at least 12 characters. Never pass it as a command-line argument.

## Execute
From the repository root, run:

`dotnet run --project backend/src/Tools/Identity.AdminBootstrap -- --execute`

Exit code 0 means the initial HR administrator was created. Exit code 3 means an HR administrator or the configured email already exists; no account was changed. Other non-zero codes indicate configuration, lock or execution failure.

## After execution
- Remove the injected bootstrap password from the execution environment and rotate/delete the one-time secret.
- Restrict access to the operator logs and deployment record.
- Verify the HR administrator can authenticate through the normal Identity API.
- Do not enable public registration in production. Do not manually change an existing employee's role to bypass the bootstrap guard.

The utility uses a MySQL advisory lock and a database transaction to serialize concurrent bootstrap attempts. A crashed process releases its connection-scoped lock when the database connection closes.
