# Security review: invitation delivery-secret handling

**Status:** Option A approved by owner on 2026-11-21, including provider-acceptance secret deletion and at-least-once delivery with possible duplicate messages. Production key protection/storage and backup/restore implementation remain deferred to cloud infrastructure setup. This document does not authorize implementation.

## Decision under review

The invitation flow needs a recoverable activation token for asynchronous delivery retries. The preferred candidate is a dedicated, short-lived delivery-secret record containing an ASP.NET Core Data Protection-protected token, separate from the ordinary lifecycle/business outbox. The invitation table continues to store only the SHA-256 token digest used for redemption lookup.

The alternative is synchronous delivery after the invitation transaction commits. That avoids recoverable token storage but makes delivery outages require an explicit resend/token rotation and leaves a commit-to-send failure window. The owner selected Option A on 2026-11-21. Production key protection/storage and backup/restore implementation remain deployment decisions deferred until cloud setup. The owner approved the full invitation policy and Mailpit local development adapter on 2026-11-21.

## Candidate Option A — protected short-lived secret

1. Generate 32 cryptographically random bytes. Derive the URL-safe activation token and SHA-256 lookup digest from the same bytes.
2. Persist the digest and invitation state in the Identity transaction. Protect the raw token with a dedicated, purpose-scoped `IDataProtector`; persist only the protected ciphertext in a separate delivery-secret row associated with the invitation/outbox item.
3. Keep the secret row inaccessible to ordinary outbox readers and business-event publishers. Only the narrowly scoped Identity delivery worker may retrieve and unprotect it.
4. On resend, rotate the digest and replace the protected secret in the same transaction. The previous token becomes invalid immediately; stale delivery jobs must verify the current invitation generation before sending.
5. Retry delivery with bounded attempts/backoff. Delete the protected secret after the delivery provider accepts the message, or when the invitation expires/cancels. Provider acceptance is not proof that the recipient read or received the message.
6. Never log token, activation URL, ciphertext, plaintext password, Data Protection key material or full delivery payload. Logs may include invitation ID, attempt number, outcome classification and correlation ID only, subject to normal access/retention controls.
7. If unprotect fails, the key ring is unavailable, or the secret is missing, fail closed: do not send a guessed/reconstructed link and do not expose cryptographic exception details. Require an HR-initiated resend/token rotation after the operational issue is repaired.

### Key-ring requirements

- Data Protection keys must be persisted outside ephemeral container filesystems and protected at rest by deployment-managed key encryption/access controls.
- All Identity instances and delivery workers that may process the same record must use the same protected key ring and compatible application discriminator/purpose configuration.
- Key creation, rotation, revocation, backup, restore and retention must be operationally documented. Retain old keys long enough to decrypt unexpired delivery records and recover from the agreed backup/restore window; do not delete a key merely because a newer key exists.
- Loss or corruption of the key ring makes outstanding ciphertext unrecoverable. The safe recovery is to invalidate affected pending tokens and issue fresh invitations, not weaken protection or expose stored values.
- Local development may use a persistent, ignored local key-ring directory with restrictive filesystem permissions. Do not commit keys. Production key storage is a deployment decision and is not selected here; AWS/KMS integration remains deferred.

## Threat analysis

| Threat / failure | Required control | Residual consideration |
|---|---|---|
| Database read or backup disclosure | Store token digest for redemption and protected ciphertext separately; no plaintext token | Ciphertext remains sensitive; database backups may retain it after live-row deletion |
| Ordinary outbox consumer obtains token | Separate table/record and restricted repository/API boundary; outbox contains only opaque invitation reference and generation | Application-wide database credentials may weaken table-level isolation; deployment permissions need review |
| Database write/tampering | Data Protection authenticated encryption; bind purpose to TeamPulse Identity invitation delivery and version | A database attacker may delete/replace records and cause denial of service |
| Stolen ciphertext | Protected by Data Protection key material kept outside the database and access-controlled | Compromise of both ciphertext and usable key material exposes tokens |
| Compromised delivery worker | Least privilege, narrow unprotect path, no token logging, restricted network/runtime identity | A worker with unprotect permission can access currently pending tokens |
| Resend races with stale delivery | Transactional generation/version check immediately before send; old digest invalidated atomically | A send already in progress at rotation may deliver a link that is now invalid; user must request/retry the latest invitation |
| Retry duplicates | Idempotent delivery attempt state where provider supports it; bounded retries | Email delivery is at-least-once; duplicate emails may occur |
| Key rotation/key loss | Shared persistent key ring, backup/restore and retention policy; fail closed | Lost keys require token rotation/resend |
| Expiry/cancellation | Check current invitation state and expiry before each send; cleanup job removes expired secret rows | Deletion from primary DB does not erase historical backups immediately |
| Logs/traces/metrics leak | Structured allow-list logging; redaction tests; no raw message/token in exceptions | Third-party provider telemetry and support access need provider-specific review |

## Required invariants and verification

- Raw token exists only in memory during generation/message construction and in the protected delivery-secret handling path.
- SHA-256 digest is the only token representation used for lookup; protected ciphertext is never accepted as an activation credential.
- No raw token, activation URL, protected ciphertext or Data Protection key appears in API responses, ordinary outbox rows, logs, traces, metrics or test failure output.
- The delivery worker verifies invitation ID, current generation, Pending status and unexpired state immediately before attempting delivery.
- A resend atomically invalidates the prior digest and replaces the protected secret; stale jobs cannot send an old generation after the current-generation check.
- Deleting a live secret row is not represented as cryptographic erasure from backups. Backup retention and expiry cleanup must be documented.
- Tests cover protect/unprotect round-trip, wrong purpose, tampered ciphertext, missing/rotated key, expired secret, stale generation, resend races, retry after process restart, log redaction and failure-closed behavior.
- Production key storage, key protection, worker identity and backup/restore must be reviewed before production use.

## Owner decisions still required

1. Choose Option A (dedicated protected short-lived secret) or Option B (synchronous post-commit delivery with resend on failure).
2. If Option A is selected, approve the Data Protection key-ring operating requirements and the production key-protection boundary; do not infer AWS/KMS selection.
3. Approve whether provider acceptance is the deletion point for the live secret, with expiry cleanup as a backstop.
4. Approve the stated at-least-once email behavior and possibility of duplicate messages.

No code, schema, key material, provider configuration or infrastructure is created by this review.
