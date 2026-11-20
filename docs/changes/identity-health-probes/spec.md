# Specification: Identity Health Probe Separation

## Acceptance criteria
- HP-01: `GET /health/live` returns HTTP 200 when the API process is running and does not execute the database health check.
- HP-02: `GET /health/ready` returns HTTP 200 when the Identity database is reachable.
- HP-03: `GET /health/ready` returns HTTP 503 when the Identity database is unavailable.
- HP-04: `GET /health/live` continues returning HTTP 200 while MySQL is unavailable after the API has started.
- HP-05: Existing `GET /health` remains an alias for readiness and returns the same status as `/health/ready`.
- HP-06: Health responses do not expose connection strings, credentials, SQL errors or other dependency diagnostics.
- HP-07: The checked-in EF Core migrations and `Database.MigrateAsync()` startup behavior remain unchanged.

## Probe semantics
- Liveness: process-only check, tagged `live`; no external dependency checks.
- Readiness: database connectivity check, tagged `ready`, with a bounded three-second timeout.
- Compatibility: `/health` maps to the same readiness check set as `/health/ready`.

## Compatibility
The existing `/health` route remains available with its existing database-readiness semantics. New deployments should configure orchestrator liveness against `/health/live` and readiness against `/health/ready`.
