# Identity API Contract v1
Base path: `/api/identity`

## POST /register
Development bootstrap only; replace with HR onboarding for production.
Request: email, password, displayName, organizationId, teamId.
Success in Development: 200 with accessToken, tokenType, expiresAtUtc and employee summary. Errors: 400 validation; 409 duplicate email. Outside Development: 404 Not Found; no employee is created.

## POST /login
Request: email and password. Success: 200 token response. Failure: generic 401 `Invalid email or password.`.

## GET /me
Bearer token required. Returns subject, email, displayName, organizationId, teamId and role from validated claims.

## GET /health
Health check.