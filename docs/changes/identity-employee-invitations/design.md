# Design: HR-Controlled Employee Invitations and Credential Activation

**Status:** Option A asynchronous protected-secret delivery approved by owner on 2026-11-21. Local key-ring persistence/protection requirements are accepted for development; production key protection/storage is deferred to cloud infrastructure setup. The full policy and local mail-catcher profile were approved on 2026-11-21.

## Service boundary
All invitation state, canonical employee creation and password hashing remain in Identity. No new service or cross-service database dependency is introduced. Use the existing .NET 8, EF Core 8, Pomelo and MySQL 8.4 conventions.

## Components
- API: HrAdmin-protected create, inspect, resend and cancel endpoints plus anonymous activation are implemented. SvelteKit provides the invitee password setup page.
- Application: transactional invitation commands, atomic activation and delivery queue/email sender abstractions.
- Domain: invitation state transitions and invariants.
- Infrastructure: EF mappings/migrations, transactional invitation repository, token generator/hash service, durable MySQL delivery queue and SMTP Mailpit adapter.
- Tests: unit tests, MySQL 8.4 Testcontainers, API-host activation integration and delivery queue integration coverage. Hosted-worker SMTP delivery remains unverified: a direct SMTP probe succeeds in CI, but the worker has not yet delivered an invitation to Mailpit. Use the captured safe diagnostics before changing delivery behavior.

## Token lifecycle
Generate 32 random bytes with `RandomNumberGenerator`; encode for a URL-safe link. Persist only SHA-256(token bytes) as the activation lookup value. The raw token must never enter logs, exception text, API response bodies, telemetry or ordinary business/lifecycle outbox records. Store UTC creation and expiry timestamps. Use fixed-length digest values and an approved constant-time comparison or indexed digest lookup.

## Persistence and concurrency
Use an Identity-owned invitation table with a unique normalized-email constraint for pending invitations (or an equivalent transactionally enforced uniqueness strategy supported by MySQL). Add a unique normalized-email index for Employees if not already guaranteed by the existing model.

Redemption runs in one transaction:
1. Hash the supplied token and locate the matching pending invitation.
2. Lock the invitation row or use a conditional state update so only one transaction can consume it.
3. Validate pending state and expiry against an injected UTC clock.
4. Insert the active Employee with role Employee and PasswordHasher output.
5. Mark the invitation Accepted and clear the token digest if approved by retention/security review.
6. Commit; any failure rolls back both account creation and invitation consumption.

Resend rotates the digest and expiry in a transaction and invalidates the previous token before dispatch. Cancellation conditionally changes Pending to Cancelled. Redemption, resend and cancellation must serialize on the same invitation row so races have one deterministic winner. A unique Employee normalized-email constraint is the final guard.

## Delivery boundary
Use an `IInvitationEmailSender` abstraction. Development uses Mailpit over SMTP (`localhost:1025`) with the SvelteKit activation page as the link destination. The durable worker leases protected-secret rows, validates pending state/expiry/generation, unprotects only at the delivery boundary, and sends the activation URL without logging it. Transient failures receive bounded exponential retry; SMTP acceptance deletes the secret only when the generation and lease still match. Resend clears any previous lease and retry schedule. Production adapter/provider is deliberately not selected in this change.

The ordinary outbox must contain only an opaque invitation reference and delivery generation/metadata; it must never contain the raw token, activation URL or protected token ciphertext. If delivery is asynchronous/retryable, recoverable token material requires a separate protected-secret store and a narrowly scoped unprotect boundary. See [security-review.md](security-review.md) for the threat model, required invariants and owner decisions.

## Delivery-secret operating model — Option A approved
The preferred candidate is a dedicated short-lived delivery-secret row containing an ASP.NET Core Data Protection-protected token. The invitation table continues to store only the SHA-256 lookup digest. The Data Protection purpose must be specific to TeamPulse Identity invitation delivery and versioned; the key ring must be persistent, protected at rest, shared by all eligible workers/instances, backed up and retained long enough to decrypt outstanding records. Keys must not be committed or stored in the same database as ciphertext without an independent protection boundary.

Only the delivery worker may read/unprotect the secret. Before each send it must verify the invitation is still Pending, unexpired and on the same generation as the outbox item. Resend atomically rotates the digest and protected secret. Delete the live secret after the provider accepts the message or after cancellation/expiry cleanup; provider acceptance does not prove recipient receipt. Email is at-least-once and duplicates may occur. Missing/invalid ciphertext or unavailable keys fail closed and require HR-initiated resend after recovery.

The owner approved this operating model on 2026-11-21. Production key protection/storage, backup/restore and worker identity remain deployment decisions; no AWS/KMS integration is selected or provisioned.

## Rate limiting and responses
Apply ASP.NET Core rate limiting to anonymous activation by IP and a privacy-safe token-derived key where feasible. Use a generic invalid-invitation response for invalid, expired, cancelled and used tokens. Do not reveal whether a matching email or employee exists. HR endpoints can return actionable conflict details because the caller is authenticated and authorized.

## Configuration and operations
No committed credentials or provider secrets. Local mail catcher is development-only. Production delivery provider, encryption key storage and rotation are deployment decisions and remain out of scope. No AWS resource is provisioned in this increment.

## Test strategy
- Inject `TimeProvider` for deterministic expiry and race tests.
- Verify token entropy/encoding and that persisted state contains only digest/protected ciphertext, never raw token.
- Test Data Protection round-trip, purpose isolation, tampered ciphertext, missing/rotated keys, and failure-closed behavior.
- Test current-generation checks, retry after process restart, resend races, duplicate delivery behavior and secret deletion/expiry. A MySQL integration test now simulates worker termination after lease acquisition and verifies that a new queue instance reclaims the expired lease while the old lease token cannot acknowledge it.
- Test atomic first redemption, concurrent duplicate redemption, resend-vs-redemption and cancel-vs-redemption.
- Test duplicate normalized email, existing employee conflict and transaction rollback.
- Test delivery retry without leaking token and token invalidation after resend.
- Add log/trace/exception redaction tests proving tokens, URLs, ciphertext and key material are absent.
- Start the real Identity API host against fresh MySQL 8.4 Testcontainers and exercise production DI/migrations.
