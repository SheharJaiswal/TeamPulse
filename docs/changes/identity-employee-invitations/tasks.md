# Tasks: HR-Controlled Employee Invitations and Credential Activation

**Status:** SDD drafted for owner review; no implementation authorization yet.

## Reconciliation
- [x] Read AGENTS.md, Identity service specification, API contract, ADR-0002, HR lifecycle SDD and HR administrator bootstrap SDD.
- [x] Preserve Identity ownership and HR-only provisioning; public self-registration remains disabled outside Development.
- [x] Keep elevated-role assignment separate; invitation role is fixed to Employee in this proposal.
- [x] Identify token delivery/retry design as a security blocker: hash-only persistence cannot reconstruct an activation link after process restart.

## SDD
- [x] Draft proposal, specification, design and task checklist.
- [x] Define acceptance criteria for authorization, token entropy/hash storage, expiry, replay, concurrency, resend/cancel, delivery failure and privacy.
- [x] Record unresolved owner decisions and implementation blockers.
- [ ] Owner approval of the proposed policy defaults and SDD package.
- [ ] Approve recoverable delivery-secret design and key-management threat model.
- [ ] Select/approve development mail-catcher profile and production delivery-provider boundary.

## Implementation — blocked pending approval
- [ ] Add invitation Domain/Application/API/Infrastructure components and MySQL migration.
- [ ] Add cryptographic token generation, digest lookup, UTC expiry and atomic single-use redemption.
- [ ] Implement HrAdmin create/get/resend/cancel endpoints and anonymous activation endpoint.
- [ ] Implement Employee-only role assignment and atomic Employee creation/password hashing.
- [ ] Implement secure delivery hand-off with no plaintext token in ordinary outbox/logs.
- [ ] Add activation rate limiting and generic invalid-token responses.
- [ ] Add MySQL 8.4 Testcontainers, API-host and delivery failure/concurrency tests.
- [ ] Update Identity API contract, service spec, runbooks and project status.
- [ ] Verify Release build, full solution tests, Compose and topology tests in CI.

## Deferred
- [ ] Production email provider selection and deployment configuration.
- [ ] Organization Admin request intake, HRIS integration, elevated-role assignment, password reset, MFA and AWS provisioning.
