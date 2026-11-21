# Design: HR-Controlled Employee Invitations and Credential Activation

**Status:** Proposed design; implementation is blocked on owner approval.

## Service boundary
All invitation state, canonical employee creation and password hashing remain in Identity. No new service or cross-service database dependency is introduced. Use the existing .NET 8, EF Core 8, Pomelo and MySQL 8.4 conventions.

## Components
- API: thin HrAdmin-protected invitation controller and anonymous activation action.
- Application: create/get/resend/cancel/redeem command handlers and delivery abstraction.
- Domain: invitation state transitions and invariants.
- Infrastructure: EF mappings/migration, transactional repository, token generator/hash service and mail adapter.
- Tests: unit tests, MySQL 8.4 Testcontainers, API-host integration and delivery adapter tests.

## Token lifecycle
Generate 32 random bytes with `RandomNumberGenerator`; encode for a URL-safe link. Persist only SHA-256(token bytes). The raw token exists only long enough to construct the outbound message and must never enter logs, exception text, API response bodies or persistent queues. Store UTC creation and expiry timestamps. Use an approved constant-time comparison or indexed digest lookup with fixed-length digest values.

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
Use an `IInvitationDelivery` abstraction. Development uses a local mail catcher in the Compose profile; the message contains the activation URL, but the API and application logs do not. Production adapter/provider is deliberately not selected in this change. Persist a delivery-outbox record in the same transaction as invitation creation/resend, then dispatch with retry and bounded backoff. Do not place the raw token in an ordinary business outbox or logs; if delivery requires recoverable secret material, design a separate encrypted, short-lived secret hand-off and threat-model it before implementation. A hash-only token record cannot reconstruct the link after process restart.

## Important design issue — recoverable delivery secret
Because the raw token must be delivered after a durable invitation write and delivery may retry after process restart, storing only a hash is not sufficient to reconstruct the activation URL. Before implementation, choose and approve one of:
- encrypt the token using an application-managed key and store ciphertext in a short-lived delivery-secret record, deleting it after confirmed delivery/expiry; or
- send synchronously after commit and accept that a delivery outage requires a new resend/token rotation.

The preferred option is a dedicated encrypted, short-lived delivery-secret record with strict access and deletion, but it requires key management, rotation and threat-model review. Do not put plaintext token into the existing lifecycle outbox or generic logs. This design issue is an explicit implementation blocker until approved.

## Rate limiting and responses
Apply ASP.NET Core rate limiting to anonymous activation by IP and a privacy-safe token-derived key where feasible. Use a generic invalid-invitation response for invalid, expired, cancelled and used tokens. Do not reveal whether a matching email or employee exists. HR endpoints can return actionable conflict details because the caller is authenticated and authorized.

## Configuration and operations
No committed credentials or provider secrets. Local mail catcher is development-only. Production delivery provider, encryption key storage and rotation are deployment decisions and remain out of scope. No AWS resource is provisioned in this increment.

## Test strategy
- Inject `TimeProvider` for deterministic expiry and race tests.
- Verify token entropy/encoding and that persisted state contains only digest/ciphertext, never raw token.
- Test atomic first redemption, concurrent duplicate redemption, resend-vs-redemption and cancel-vs-redemption.
- Test duplicate normalized email, existing employee conflict and transaction rollback.
- Test delivery retry without leaking token and token invalidation after resend.
- Start the real Identity API host against fresh MySQL 8.4 Testcontainers and exercise production DI/migrations.
