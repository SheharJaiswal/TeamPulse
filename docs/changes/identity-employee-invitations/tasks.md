# Tasks: HR-Controlled Employee Invitations and Credential Activation

**Status:** Implementation in progress; initial domain, persistence, token-protection foundation and local Mailpit service are committed.

## Reconciliation
- [x] Read AGENTS.md, Identity service specification, API contract, ADR-0002, HR lifecycle SDD and HR administrator bootstrap SDD.
- [x] Preserve Identity ownership and HR-only provisioning; public self-registration remains disabled outside Development.
- [x] Keep elevated-role assignment separate; invitation role is fixed to Employee in this proposal.
- [x] Identify token delivery/retry design as a security blocker: hash-only persistence cannot reconstruct an activation link after process restart.

## SDD and security review
- [x] Draft proposal, specification, design and task checklist.
- [x] Define acceptance criteria for authorization, token entropy/hash storage, expiry, replay, concurrency, resend/cancel, delivery failure and privacy.
- [x] Record unresolved owner decisions and implementation blockers.
- [x] Threat-model the protected-secret candidate: key-ring persistence/protection/rotation, worker boundary, stale-generation checks, retry behavior, deletion/backup limitations and fail-closed recovery.
- [x] Keep ordinary outbox payload limited to opaque invitation reference and generation metadata; prohibit raw token, activation URL and ciphertext.
- [x] Owner selected Option A: asynchronous delivery with a dedicated Data Protection-protected short-lived secret store (2026-11-21).
- [x] Owner accepted persistent, Git-ignored local key-ring storage with restrictive filesystem permissions; production key protection/storage and backup/restore implementation are deferred to cloud infrastructure setup.
- [x] Owner approved deleting the live secret after provider acceptance, with expiry cleanup as a backstop, and at-least-once delivery where duplicate emails may occur (2026-11-21).
- [x] Owner approved 24-hour expiry, resend/cancel semantics, direct activation to active Employee, generic activation failures and rate limiting (2026-11-21).
- [x] Owner selected Mailpit as the local development mail catcher; production delivery provider remains deferred (2026-11-21).

## Implementation — authorized
- [x] Add initial invitation domain entities, EF mappings and MySQL migration for invitation records and the separate protected delivery-secret store.
- [x] Clear token digests to SQL NULL on accepted, cancelled and expired terminal states so the unique digest index does not collide on consumed invitations.
- [x] Add 256-bit cryptographic token generation, SHA-256 digest derivation, purpose-scoped Data Protection wrapping and a persistent local key-ring configuration. Digest lookup and atomic redemption remain pending.
- [x] Implement the transactional application service for HR create/get/resend/cancel operations, including 24-hour expiry, expired-invitation replacement, atomic resend generation rotation, protected-secret replacement and opaque-reference outbox metadata.
- [x] Expose create/get/resend/cancel through the HrAdmin-protected API; add authorization, validation, lifecycle and unknown-ID integration coverage.
- [x] Add the anonymous activation endpoint with generic invalid-token responses and an IP-based fixed-window rate limit (5 attempts/minute).
- [x] Implement Employee-only role assignment, ASP.NET Core password hashing, single-use token consumption and atomic Employee creation.
- [x] Implement the owner-approved protected-secret delivery queue with MySQL leases, generation-aware acknowledgement, retry scheduling and expiry cleanup; the ordinary outbox remains limited to invitation reference and generation metadata.
- [x] Add activation rate limiting and generic invalid-token responses.
- [x] Add MySQL integration coverage for delivery retry scheduling, resend generation rotation, stale-lease acknowledgement rejection, expiry cleanup and recovery of an expired delivery lease through a fresh queue instance after simulated worker termination. Activation API-host coverage includes successful activation/login, duplicate redemption race, generic failures for invalid/expired/cancelled/used tokens, weak-password rejection and rate limiting.
- [x] Add Mailpit to the root local Docker Compose profile (SMTP 1025, web UI 8025).
- [x] Add a Mailpit Testcontainers end-to-end test covering HR invitation creation, real SMTP delivery, resend generation rotation, rejection of the old link, activation through the anonymous API and successful Employee login.
- [x] Capture the hosted worker's existing safe warning diagnostics in the Mailpit test timeout (exception types, SMTP status and configured endpoint only); do not capture exception messages, email bodies, tokens or URLs.
- [x] Re-run backend CI #44927041146: Restore, Release build, Compose validation and RabbitMQ topology checks passed; 131/132 Identity tests passed. Safe diagnostics narrowed the worker failure to SmtpException with an inner SocketException and SMTP GeneralFailure; no socket error code was captured yet.
- [ ] Capture the socket error code and identify the underlying endpoint/connectivity failure before changing delivery behavior.
- [x] Update the broader Identity API contract and add the local Mailpit invitation-delivery runbook; document that hosted-worker SMTP delivery remains unverified.
- [ ] Add operational verification for application restart during a leased delivery and SMTP outage recovery.
- [ ] Verify Release build, full solution tests, Compose and topology tests in CI for the latest delivery-worker commits after the SMTP failure is diagnosed and corrected.

## Deferred
- [ ] Production email provider selection and deployment configuration.
- [ ] Organization Admin request intake, HRIS integration, elevated-role assignment, password reset, MFA and AWS provisioning.
