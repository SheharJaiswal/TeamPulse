# Specification: HR-Controlled Employee Invitations and Credential Activation

**Status:** Owner approved the full invitation SDD on 2026-11-21, including Option A asynchronous protected-secret delivery, 24-hour expiry, resend/cancel rules, direct Employee activation, generic failures/rate limiting and Mailpit for local development. Production email provider and production key-management implementation remain deferred.

## Actors and authority
- `HrAdmin`: may create, inspect, resend and cancel invitations.
- Invitee: may redeem a valid invitation and set the initial password.
- `OrganizationAdmin`, `Manager` and `Employee`: cannot create, resend or cancel invitations.
- Only Identity creates the canonical Employee record. Invitation payloads cannot select a role; successful redemption always assigns `Employee`.

## Proposed API surface
All paths are under `/api/identity`.

- `POST /invitations` — HrAdmin creates an invitation.
- `GET /invitations/{invitationId}` — HrAdmin reads non-secret invitation status.
- `POST /invitations/{invitationId}/resend` — HrAdmin invalidates the old token and issues a replacement.
- `POST /invitations/{invitationId}/cancel` — HrAdmin cancels a pending invitation.
- `POST /invitations/activate` — anonymous invitee redeems token and sets password. The activation email links to the SvelteKit `/activate` route.

The activation request contains only the opaque token and new password. Raw tokens and activation URLs are never returned by HR APIs; they are delivered through the configured mail adapter. Responses do not expose token digests or secrets. The HR create, inspect, resend and cancel endpoints, anonymous activation endpoint and SvelteKit activation page are implemented.

## Invitation data and state
Proposed states: `Pending`, `Accepted`, `Cancelled`, `Expired`. Expiry may be computed from `ExpiresAtUtc`; a background cleanup job is not required for correctness.

A pending invitation stores invitation ID, normalized email, display name, organization/team IDs, role fixed to Employee, token digest, created/expiry timestamps, status and minimal audit metadata. Do not persist the raw token or password. The exact storage schema and whether invitation PII should be minimized further require design review.

## Proposed business rules
- Only a validated HrAdmin JWT may create, inspect, resend or cancel an invitation. Actor ID comes from validated JWT `sub`.
- Validate and normalize email and required employee profile fields before persistence. Verify organization/team references belong to the same Identity-owned tenant boundary.
- A new invitation cannot silently overwrite an existing employee or another pending invitation for the same normalized email. Return a documented conflict to the authenticated HR caller.
- Generate a 256-bit cryptographically random token; store only its SHA-256 digest. Compare using a fixed-time comparison where applicable.
- Proposed expiry is 24 hours from issuance, based on UTC.
- Redemption must be single-use and atomic: lock/conditionally consume the invitation and create the active Employee in one Identity MySQL transaction. A second or concurrent redemption must fail without a second Employee.
- A resend is allowed only for a pending, unexpired invitation. It rotates the token and expiry and invalidates the previous token atomically. A cancelled, accepted or expired invitation cannot be resent; HR creates a new invitation instead.
- Cancellation is allowed only while pending and prevents later redemption.
- Successful redemption creates an active Employee with role exactly `Employee`, normalized unique email and ASP.NET Core PasswordHasher output. No role is accepted from the client.
- The password must satisfy the existing Identity password policy. No password or token is logged, persisted in plaintext, or included in telemetry.
- The activation endpoint returns a generic invalid/expired/cancelled/used-token response and does not disclose whether an email or employee exists. Apply rate limiting to activation attempts.
- Delivery is asynchronous/retryable. The outbox carries only invitation ID and generation; a separate Data Protection-protected secret store retains the token until SMTP acceptance or expiry cleanup. A delivery outage must not expose the raw token through logs or an API response and must not create an active account.
- Existing Development-only `POST /register` behavior remains unchanged; every non-Development environment continues returning 404.

## Acceptance criteria
- EINV-01: Only HrAdmin can create, inspect, resend or cancel invitations.
- EINV-02: Actor identity is taken from validated JWT `sub`; no caller-supplied actor is trusted.
- EINV-03: Invitation email is normalized and unique against existing employees and active pending invitations.
- EINV-04: Raw invitation token is generated with a cryptographically secure RNG and only its SHA-256 digest is persisted.
- EINV-05: Invitation token expires after the approved lifetime and expiry is evaluated in UTC.
- EINV-06: A valid token can be redeemed exactly once; concurrent/replayed attempts create at most one Employee.
- EINV-07: Employee creation and invitation consumption are atomic; failure leaves no partial account.
- EINV-08: Redemption creates an active Employee with role exactly Employee and a one-way password hash.
- EINV-09: Resend invalidates the previous token atomically; only pending, unexpired invitations can be resent.
- EINV-10: Cancellation prevents redemption; accepted/cancelled/expired invitations cannot be reused.
- EINV-11: No endpoint response, log, exception, trace or metric contains raw tokens, activation URLs, passwords, hashes or connection secrets.
- EINV-12: Invalid/expired/used/cancelled token responses do not disclose account existence; activation is rate-limited.
- EINV-13: Delivery failure is safely retryable and does not create an active Employee or leak the activation token. MySQL leases prevent concurrent workers from claiming the same delivery generation; resend invalidates stale leases.
- EINV-14: Existing Development-only registration behavior and production 404 behavior remain unchanged.
- EINV-15: MySQL 8.4 Testcontainers verify migration, uniqueness, concurrent redemption, resend/cancel races and transaction rollback.
- EINV-16: CI Release build, complete solution tests, Compose validation and topology contract tests pass.

## Approved decisions (2026-11-21)
1. Invitation lifetime is 24 hours from issuance, evaluated in UTC.
2. Resend is allowed only for pending, unexpired invitations; it atomically rotates the token and expiry and invalidates the previous token.
3. Cancellation is terminal; cancelled invitations cannot be redeemed or resent.
4. Successful activation immediately creates an active Employee with fixed role `Employee`; no second HR approval is required.
5. Invalid, expired, cancelled and used tokens return a generic activation failure; activation attempts are rate-limited.
6. Mailpit is the local development mail catcher. Production email provider selection remains deferred.
7. Delivery uses Option A: dedicated protected secret storage, deletion after provider acceptance with expiry cleanup, and at-least-once delivery where duplicate messages may occur.

## Explicitly excluded
Role catalogue/elevated-role assignment remains governed by open question 11. No invitation may grant HrAdmin, OrganizationAdmin or Manager. Organization Admin request intake, HRIS integration, password reset and MFA remain separate SDD increments.
