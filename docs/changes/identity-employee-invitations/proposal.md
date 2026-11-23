# Proposal: HR-Controlled Employee Invitations and Credential Activation

## Problem
Production self-registration is disabled and Identity owns canonical employee records. The initial HR administrator can execute lifecycle commands, but there is no supported workflow to invite a new employee and let that employee establish their first password. Manual database inserts or reopening public registration would bypass the HR-controlled onboarding boundary.

## Proposed change
Add an Identity-owned invitation workflow. An authenticated HrAdmin creates an invitation for a new employee, Identity stores a pending invitation and sends a one-time activation link through a pluggable delivery adapter, and the invitee redeems it to create an active Employee account and establish a password. HR can resend or cancel a pending invitation and inspect its non-secret status.

The owner approved the invitation feature on 2026-11-21, including asynchronous delivery with a dedicated Data Protection-protected secret store (Option A), 24-hour expiry, resend/cancel semantics, direct activation to Employee, generic failures/rate limiting and Mailpit for local development. Production email provider and production key protection/storage remain deferred.

## Scope
- HR-only create, resend, cancel and status endpoints in Identity.
- Invitation persistence with normalized email, required organization identifier and optional team identifier, expiry/status, and a SHA-256 token digest only.
- Cryptographically secure 256-bit random, single-use activation tokens; never persist or log the raw token.
- Proposed 24-hour expiry; resend invalidates the prior token; cancellation and redemption are atomic.
- Public/anonymous activation endpoint that accepts the opaque token and a new password, without disclosing whether an email address has an account or invitation.
- Successful redemption creates one active Employee with the canonical `Employee` role and an ASP.NET Core PasswordHasher hash.
- Delivery abstraction with a development-only local mail catcher adapter; no production email vendor or AWS service selected here.
- Unit, MySQL 8.4 Testcontainers, API authorization, token replay/expiry/concurrency and delivery-failure tests.
- Runbook and API contract updates.

## Out of scope
- Public self-registration, password reset/change, MFA or external identity providers.
- Organization Admin invitation or approval requests.
- Assigning HrAdmin, OrganizationAdmin, Manager or any elevated role through invitations.
- HRIS feed/reconciliation, bulk import, production email vendor selection and AWS provisioning.
- Employee profile editing or lifecycle transition changes.
- Changes to the existing HR lifecycle audit/outbox event contract unless a separately approved design establishes a required event.

## Approved security defaults (owner approval: 2026-11-21)
- Token: 32 bytes from a cryptographically secure random generator, URL-safe encoded; only SHA-256 digest stored.
- Lifetime: 24 hours from issuance, evaluated using UTC.
- Single use: consume exactly once in the same transaction that creates the employee.
- Resend: issue a new token and invalidate the previous token in one transaction.
- Cancel: terminal cancellation; cancelled invitations cannot be redeemed or resent.
- Role: fixed `Employee`; no client-controlled role field.
- Password: existing Identity minimum policy and ASP.NET Core PasswordHasher.
- Failure: no partial employee creation; do not log tokens, activation URLs, passwords, password hashes or invitation secrets.
- Delivery: persist invitation before dispatch; a delivery failure leaves a safely retryable pending invitation and never returns the raw token in API responses or logs.

## Acceptance
An HR administrator can securely invite a new employee; the employee can activate once using a valid, unexpired invitation; replay, expiry, cancellation, resend races, duplicate email and delivery failures cannot create duplicate accounts or bypass authorization; public registration remains disabled outside Development.
