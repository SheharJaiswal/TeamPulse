# Identity Employee Invitation Delivery Runbook

## Scope and current verification status

This runbook covers the local-development invitation mail path: Identity API, its durable MySQL delivery queue, SMTP adapter and Mailpit. Mailpit is a development mail catcher, not a production email provider.

**Important:** The hosted worker's end-to-end SMTP delivery is currently unverified. The direct SMTP probe to Mailpit has succeeded in CI, but the worker's invitation delivery has failed with `SmtpDeliveryFailed`. Do not treat this path as production-ready until the latest backend CI identifies and verifies the fix. Never work around a failure by extracting tokens or activation URLs from the database or logs.

## Local prerequisites and startup

1. Use the repository's documented .NET 8 and Docker Desktop prerequisites.
2. From the repository root, start the local Identity database and Mailpit:

   ```bash
   docker compose up -d mysql mailpit
   ```

3. Confirm MySQL is healthy and Mailpit is running. Mailpit exposes:
   - SMTP: `localhost:1025`
   - Web UI/API: `http://localhost:8025`
4. Start the Identity API with the Development environment and the existing local JWT/database configuration. Its Development settings enable invitation email and point to `localhost:1025`.
5. Start the SvelteKit frontend using the repository's frontend instructions. The activation page is `/activate`; the configured activation base URL is `http://localhost:5173/activate`.
6. Authenticate as the seeded/bootstrap `HrAdmin` and create an invitation from the HR invitations page at `/hr/invitations`.

The local SMTP settings are configurable under `InvitationEmail` (environment-variable prefix `InvitationEmail__`): `Enabled`, `Host`, `Port`, `EnableSsl`, `FromAddress`, `FromName`, `ActivationBaseUrl`, `PollIntervalSeconds`, `BatchSize`, `LeaseDurationSeconds` and `MaxRetryDelaySeconds`. Do not put SMTP credentials or JWT signing keys in source control.

## Inspecting a delivery

- Open [Mailpit](http://localhost:8025) and search for the invited recipient.
- A delivered message contains the activation link. Treat the message and link as credentials: do not paste them into issue trackers, logs or chat.
- The invitation HR page exposes non-secret status and expiry information. The API intentionally has no invitation-list endpoint.
- The worker polls the MySQL-backed queue (Development default: every 3 seconds, batches of 10). It checks the invitation's pending state, expiry and delivery generation before sending.
- Delivery leases default to 60 seconds. Transient failures are rescheduled with bounded exponential backoff; the default maximum delay is 900 seconds. SMTP acceptance deletes the live protected-secret row only when the generation and lease still match.

SMTP acceptance means the SMTP server accepted the message; it does not prove inbox delivery or that the recipient opened it. A crash after SMTP acceptance but before the queue acknowledgement can cause a duplicate email. This is expected at-least-once behavior.

## SMTP outage and recovery

1. Confirm Mailpit is running and the configured host, port and SSL setting match the process network. When Identity runs on the host, the default is `localhost:1025`; when running in a container network, use the Mailpit service hostname and ensure the API container can reach it.
2. Restore SMTP connectivity and allow the worker's scheduled retries to run. Do not reset queue rows or manually clear leases in SQL.
3. If the original invitation is still pending and unexpired, an HrAdmin can use **Resend**. Resend atomically rotates the token and expiry; the old link is invalidated. If it has expired, create a new invitation.
4. If the Data Protection key ring was deleted, replaced or is unavailable, outstanding protected secrets may be unrecoverable. Restore the correct persistent local key ring where possible; otherwise, after the key issue is resolved, use the HR resend/new-invitation flow to issue a fresh token. Never disable protection or attempt to reconstruct a token.
5. Verify the new message appears in Mailpit and the invitee can activate once and sign in with the `Employee` role.

## Data Protection key-ring handling

Identity persists its local Data Protection key ring under the API content root's `.keys` directory by default. The directory is Git-ignored and receives restrictive Linux directory permissions. Keep it persistent across local API restarts. Deleting it can make outstanding protected delivery secrets unreadable. Do not commit, copy into logs or share key files.

Production key encryption, shared multi-instance key-ring storage, backup/restore, rotation operations, provider selection and deployment identity are intentionally deferred until cloud infrastructure setup. No AWS/KMS configuration is implied by this local runbook.

## Security and operational guardrails

- Never log or expose raw invitation tokens, activation URLs, passwords, password hashes, protected ciphertext or Data Protection key material.
- Do not return activation credentials through HR APIs or add them to ordinary outbox/event payloads.
- Do not manually mutate invitation/secret/lease rows to force delivery.
- Invalid, expired, cancelled and used activation tokens return the same generic activation failure; anonymous activation is limited to five attempts per minute per client IP.
- The worker's safe diagnostics may report exception type, SMTP status and configured endpoint. Do not expand diagnostics to include exception messages or message content.
- Do not enable the production workflow or claim delivery readiness until backend CI passes the real Mailpit end-to-end test and the restart/outage recovery scenarios are verified.
