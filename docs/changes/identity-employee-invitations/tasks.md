# Tasks: HR-Controlled Employee Invitations and Credential Activation

**Status:** SDD drafted for owner review; no implementation authorization yet.

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
- [ ] Add invitation Domain/Application/API/Infrastructure components and MySQL migration.
- [ ] Add cryptographic token generation, digest lookup, UTC expiry and atomic single-use redemption.
- [ ] Implement HrAdmin create/get/resend/cancel endpoints and anonymous activation endpoint.
- [ ] Implement Employee-only role assignment and atomic Employee creation/password hashing.
- [ ] Implement the owner-approved secure delivery hand-off with no plaintext token in ordinary outbox/logs.
- [ ] Add activation rate limiting and generic invalid-token responses.
- [ ] Add MySQL 8.4 Testcontainers, API-host and delivery failure/concurrency/security tests.
- [ ] Update Identity API contract, service spec, runbooks and project status.
- [ ] Verify Release build, full solution tests, Compose and topology tests in CI.

## Deferred
- [ ] Production email provider selection and deployment configuration.
- [ ] Organization Admin request intake, HRIS integration, elevated-role assignment, password reset, MFA and AWS provisioning.
