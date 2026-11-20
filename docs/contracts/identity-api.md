# Identity API Contract v1
Base path: `/api/identity`

## POST /register
Development bootstrap only; replace with HR onboarding for production.
Request: email, password, displayName, organizationId, teamId.
Success in Development: 200 with accessToken, tokenType, expiresAtUtc and employee summary. Errors: 400 validation; 409 duplicate email. Outside Development: 404 Not Found; no employee is created.

## POST /login
Request: email and password. Success: 200 token response. Failure: generic 401 `Invalid email or password.`. When verification requires a password-hash upgrade, the service persists the upgraded hash before returning the successful token response. The response contract is unchanged.

## GET /me
Bearer token required. Returns subject, email, displayName, organizationId, teamId and role from validated claims.

## Health endpoints
- `GET /health/live`: process liveness only; does not check MySQL and should be used for orchestrator liveness probes.
- `GET /health/ready`: checks the Identity-owned MySQL database and returns HTTP 503 when it is unavailable.
- `GET /health`: compatibility alias for `/health/ready`; retains the previous database-readiness behavior.
Health responses expose only aggregate health status, not dependency exception details or connection information.
