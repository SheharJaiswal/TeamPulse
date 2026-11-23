# Identity API Contract v1

Base path: `/api/identity`

## POST /register

Development bootstrap only; replace with HR provisioning for production.  
Request: email, password, displayName and organizationId are required; teamId is optional.  
Success in Development: 200 with accessToken, tokenType, expiresAtUtc and employee summary. Errors: 400 validation; 409 duplicate email. Outside Development: 404 Not Found; no employee is created.

## POST /login

Request: email and password. Success: 200 token response. Failure: generic 401 Invalid email or password. This same response is used for unknown email, incorrect password and an inactive employee. Inactive employees receive no token; the submitted password is checked, but no password hash is upgraded or persisted. For active employees whose password verification requires an upgrade, Identity persists the replacement hash before returning the successful token response. The response contract is unchanged.

## GET /me

Bearer token required. Returns subject, email, displayName, organizationId, optional teamId and role from validated claims. The teamId field is null when the employee has not been assigned to a team.

## HR lifecycle commands — implemented and verified

Both endpoints require a bearer token whose validated `role` claim is `HrAdmin`. The actor employee ID is read from validated `sub`; caller-supplied actor IDs are not accepted. OrganizationAdmin and Employee roles cannot execute these commands.

### POST /employees/{employeeId}/deactivate

### POST /employees/{employeeId}/reactivate

Headers:
- `Authorization: Bearer <token>` (required)
- `Idempotency-Key: <1–128 visible ASCII characters>` (required; whitespace-only or multiple values are rejected)
- `X-Correlation-ID: <UUID>` (optional; Identity generates one when absent; malformed or repeated values are rejected)

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

The employee state, immutable lifecycle audit row and schema-v1 outbox record are committed atomically. A same-state request is a no-op and creates no audit/event. A same-key/same-command retry returns the original result without duplicate rows. Stale commands are checked against the employee lifecycle version while holding a MySQL row lock.

Events: `EmployeeDeactivated` and `EmployeeReactivated`, schema version 1. The outbox is durable storage only; it is not proof of EventBridge delivery. See [Domain Event Envelope](event-envelope.md) and [Employee Status Projection Contract](employee-status-projection.md).

## HR employee invitations — implemented; delivery verification pending

All invitation-management routes require a bearer token whose validated `role` claim is `HrAdmin`. The actor is taken from validated JWT `sub`; no caller-supplied actor ID or role is accepted. There is no invitation list endpoint.

### POST /invitations

Creates a pending invitation.

Request JSON:
- `email`: valid email address, maximum 320 characters.
- `displayName`: required, maximum 150 characters.
- `organizationId`: required, maximum 100 characters.
- `teamId`: optional; when supplied, maximum 100 characters. Omit it or send null/blank when the employee has not joined a team.

Success: 201 Created with an invitation summary containing `invitationId`, `email`, `displayName`, `organizationId`, nullable `teamId`, `status`, `createdAtUtc`, `expiresAtUtc` and `deliveryGeneration`. The response contains no activation token or URL.

Errors: 400 invalid fields; 401 unauthenticated; 403 non-HrAdmin; 409 an employee or pending invitation already exists for the normalized email.

### GET /invitations/{invitationId}

Returns the non-secret invitation summary described above.

Errors: 401 unauthenticated; 403 non-HrAdmin; 404 unknown invitation ID.

### POST /invitations/{invitationId}/resend

For a pending, unexpired invitation, atomically rotates the token digest, protected delivery secret, generation and expiry; the previous link becomes invalid.

Success: 200 OK with the updated non-secret invitation summary.

Errors: 401 unauthenticated; 403 non-HrAdmin; 404 unknown invitation; 410 expired invitation; 409 invitation is already accepted or cancelled.

### POST /invitations/{invitationId}/cancel

Cancels a pending invitation. Cancellation is terminal and prevents activation.

Success: 200 OK with the updated non-secret invitation summary.

Errors: 401 unauthenticated; 403 non-HrAdmin; 404 unknown invitation; 410 expired invitation; 409 invitation is already accepted or cancelled.

### POST /invitations/activate

Anonymous invitee activation; no bearer token is required.

Request JSON:
- `token`: URL-safe invitation token.
- `password`: 12–256 characters.

Success: 200 with the generic message `Your account is active. Sign in to continue.`; no JWT or token material is returned.

Errors: 400 for malformed request/password or the generic message `This invitation is invalid or can no longer be used.` for invalid, expired, cancelled or already-used tokens; 429 when the IP-based limit of five attempts per minute is exceeded.

A successful activation atomically consumes the invitation and creates an active employee with the fixed `Employee` role. Activation failures do not disclose whether an email or employee exists. The existing Development-only registration route remains unchanged and returns 404 outside Development.

Delivery is asynchronous through the Identity worker and configured SMTP adapter. The ordinary event outbox does not contain the activation token, URL or protected token ciphertext. The dedicated Data Protection-protected delivery-secret row is deleted after SMTP acceptance; at-least-once delivery means duplicate emails are possible. SMTP delivery is not yet CI-verified; see [Invitation Delivery Runbook](../runbooks/identity-employee-invitations.md) before local testing.

Organization Admins have no direct employee profile or lifecycle write endpoint. They may request HR changes, but request intake/approval is not yet specified. Profile/role mutation remains outside this contract.

## Health endpoints

- GET /health/live: process liveness only; does not check MySQL and should be used for orchestrator liveness probes.
- GET /health/ready: checks the Identity-owned MySQL database and returns HTTP 503 when it is unavailable.
- GET /health: compatibility alias for /health/ready; retains the previous database-readiness behavior.

Health responses expose only aggregate health status, not dependency exception details or connection information.
