# Identity API Contract v1

Base path: `/api/identity`

## POST /register

Development bootstrap only; replace with HR provisioning for production.  
Request: email, password, displayName, organizationId, teamId.  
Success in Development: 200 with accessToken, tokenType, expiresAtUtc and employee summary. Errors: 400 validation; 409 duplicate email. Outside Development: 404 Not Found; no employee is created.

## POST /login

Request: email and password. Success: 200 token response. Failure: generic 401 Invalid email or password. This same response is used for unknown email, incorrect password and an inactive employee. Inactive employees receive no token; the submitted password is checked, but no password hash is upgraded or persisted. For active employees whose password verification requires an upgrade, Identity persists the replacement hash before returning the successful token response. The response contract is unchanged.

## GET /me

Bearer token required. Returns subject, email, displayName, organizationId, teamId and role from validated claims.

## HR lifecycle commands — approved contract, implementation pending

Both endpoints require a bearer token whose validated `role` claim is `HrAdmin`. The actor employee ID is read from validated `sub`; caller-supplied actor IDs are ignored/not accepted. OrganizationAdmin and Employee roles cannot execute these commands.

### POST /employees/{employeeId}/deactivate

### POST /employees/{employeeId}/reactivate

Headers:
- `Authorization: Bearer <token>` (required)
- `Idempotency-Key: <1–128 visible ASCII characters>` (required)
- `X-Correlation-ID: <UUID>` (optional; Identity generates one when absent)

Body:
- `reasonCode`: required string, 1–100 characters. No controlled vocabulary is approved; stored as a trimmed opaque value.
- `note`: optional string, maximum 1,000 characters.

Success: 200 with outcome (`transitioned`, `alreadyInDesiredState` or `replayed`), employeeId, isActive, lifecycleVersion, eventId when a transition exists, and correlationId.

Errors:
- 400 invalid body, idempotency header or correlation header.
- 401 missing/invalid bearer token.
- 403 authenticated caller is not HrAdmin.
- 404 target employee does not exist.
- 409 idempotency key reused for a different command or stale concurrent transition.
- 5xx persistence failure; no partial employee/audit/outbox write is committed.

The employee state, immutable lifecycle audit row and schema-v1 outbox record are committed atomically. A same-state request is a no-op and creates no audit/event. A same-key/same-command retry returns the original result without duplicate rows.

Events: `EmployeeDeactivated` and `EmployeeReactivated`, schema version 1. See [Domain Event Envelope](event-envelope.md) and [Employee Status Projection Contract](employee-status-projection.md).

Organization Admins have no direct employee profile or lifecycle write endpoint. They may request HR changes, but request intake/approval is not yet specified. Employee provisioning/invitation and profile/role mutation are separate future contracts.

## Health endpoints

- GET /health/live: process liveness only; does not check MySQL and should be used for orchestrator liveness probes.
- GET /health/ready: checks the Identity-owned MySQL database and returns HTTP 503 when it is unavailable.
- GET /health: compatibility alias for /health/ready; retains the previous database-readiness behavior.
Health responses expose only aggregate health status, not dependency exception details or connection information.
