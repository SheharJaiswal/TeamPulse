# Design: Identity Health Probe Separation

## Implementation
Register a lightweight `self` health check tagged `live`. Register a dedicated `IdentityDatabaseHealthCheck` tagged `ready`; it creates an async DI scope for each execution, resolves the scoped IdentityDbContext, and calls `Database.CanConnectAsync`. Its HealthCheckRegistration has a three-second timeout so a stalled dependency cannot hold a probe indefinitely.

Map separate health endpoints using tag predicates:
- `/health/live` selects only `live` checks.
- `/health/ready` selects only `ready` checks.
- `/health` selects only `ready` checks to preserve compatibility.

Use the default health-check response writer so responses contain only the aggregate status, not individual exception details. The database health check returns a generic failure description and never returns provider exceptions.

## Tests
Add an integration test using the existing disposable MySQL 8.4 Testcontainers fixture. Start the real API host and assert all endpoints are healthy. Stop the fixture database after host startup; assert liveness remains HTTP 200 while readiness and the compatibility endpoint return HTTP 503. Restart the database in a `finally` block so fixture cleanup remains deterministic.

## Risks and safeguards
- The test must restore MySQL even if an assertion fails.
- The health-check registration timeout bounds dependency probe duration.
- Do not change startup migrations; the host still requires MySQL during initial startup as required by ADR-0001.
- This change does not make the API start when MySQL is unavailable; it only separates runtime liveness from readiness after startup.
