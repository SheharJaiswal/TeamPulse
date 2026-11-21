# Specification: HR-Authorized Employee Lifecycle Commands

**Status:** Implemented; acceptance criteria ILC-01–ILC-20 verified by GitHub Actions CI #241 (268/268 full-suite tests passed).  
**Authority:** ADR-0002, AGENTS.md, Identity service ownership and the domain event envelope.

## Authorization and field ownership

| Actor | Employee profile fields | Lifecycle status | Notes |
|---|---|---|---|
| `HrAdmin` | HR is the canonical authority for email, display name, organization ID, team ID and role. Profile/role mutation endpoints are not part of this change. | May deactivate and explicitly reactivate employees through the commands below. | Role is sourced from the validated JWT role claim. Actor ID is sourced from validated `sub`. |
| `OrganizationAdmin` | No direct write access to employee profile fields. | Cannot activate, deactivate or override an HR decision. | May request a change from HR; request intake and approval workflow are not yet specified. |
| Employee | No employee profile write API is introduced here. | Cannot change own lifecycle status. | |
| Trusted HRIS | May eventually act on HR's behalf. | Same HR-authorized transitions after integration. | HRIS authentication, source identity and conflict handling are deferred. |

HR-owned profile fields are canonical Identity data. This change intentionally exposes no profile mutation endpoint because role catalogue/elevation rules and the invitation/credential lifecycle are not approved. Password hashes, credentials, employee IDs and creation timestamps are never client-editable.

## Lifecycle commands

- `POST /api/identity/employees/{employeeId}/deactivate`
- `POST /api/identity/employees/{employeeId}/reactivate`

Both require:
- A valid bearer JWT issued by Identity.
- The JWT role claim to equal `HrAdmin`.
- A valid GUID `sub` claim; this is the audit actor and cannot be supplied in the body.
- An `Idempotency-Key` header containing 1–128 visible ASCII characters (whitespace-only and repeated header values are rejected).
- A JSON body with `reasonCode` (required, 1–100 characters) and optional `note` (maximum 1,000 characters).
- An optional UUID `X-Correlation-ID`; Identity generates a UUID when absent and rejects malformed or repeated values.

No controlled reason-code catalogue has been approved. The API stores the supplied reason code as an opaque, trimmed value; it must not invent or silently translate reason values.

## State behavior

- Deactivation changes active to inactive. After the transaction commits, Identity login rejects the employee using the existing generic 401 response.
- Reactivation changes inactive to active only on an explicit HR command. It does not restore prior sessions.
- A command for an employee already in the requested state returns HTTP 200 with an `alreadyInDesiredState` outcome and creates no audit or event because no transition occurred.
- A missing target employee returns 404.
- Invalid body/header values return 400.
- Missing/invalid authentication returns 401; an authenticated non-`HrAdmin` returns 403.
- A conflicting idempotency-key reuse or stale concurrent transition returns 409.
- Persistence failure returns a server error and leaves employee status, audit and outbox unchanged.

## Idempotency

The idempotency key is unique per actor employee ID. Identity stores a SHA-256 fingerprint of the canonical tuple `(employeeId, desiredStatus, normalizedReasonCode, normalizedNote)` with the audit record.

- Same actor + same key + same fingerprint: return the original transition result/event ID; do not create another audit row or outbox message.
- Same actor + same key + different fingerprint: return 409; do not mutate state.
- Correlation ID is excluded from the fingerprint so a network retry may carry a different trace identifier without duplicating the transition.
- A no-op command does not create an idempotency record because it creates no transition. Clients should use a stable key when retrying an actual state-changing command.

## Atomic persistence and concurrency

For a real transition, one Identity-owned MySQL transaction:
1. Resolves the idempotency key and target employee.
2. Reads and locks the employee row; a lifecycle version changed since the initial read is treated as a stale command (409), except a matching idempotent retry returns the committed original result.
3. Changes `Employee.IsActive` and increments `LifecycleVersion` by exactly one.
4. Inserts one immutable `EmployeeLifecycleAudit` row.
5. Inserts one `OutboxMessage` containing the matching event envelope.
6. Commits all writes together.

Any failure rolls back all writes. A unique index on (actor employee ID, idempotency key), a unique event ID, a unique per-employee lifecycle version and optimistic concurrency protect retries and concurrent commands.

Audit data includes event/transition ID, employee ID, actor employee ID, source (`hr-admin-api`), previous and new status, lifecycle version, UTC timestamp, reason code, optional note, correlation ID, idempotency key and request fingerprint. It contains no credential or token data.

## Event contract

Event types:
- `EmployeeDeactivated`
- `EmployeeReactivated`

Both use the shared envelope:
- `eventId`: UUID, equal to the lifecycle audit/transition ID.
- `eventType`: one of the two event names above.
- `schemaVersion`: integer `1`.
- `occurredAtUtc`: UTC transition timestamp.
- `correlationId`: UUID from the request or generated by Identity.
- `payload`: `employeeId`, `actorEmployeeId`, `source`, `previousStatus`, `newStatus`, `lifecycleVersion`, `reasonCode`, and optional `note`.

Payload status values are `active` and `inactive`. Events contain only the minimum identity/lifecycle information needed for access projections; never include email, display name, password hashes, credentials, JWTs or wellness/activity data.

Identity writes the event to the outbox in the same transaction as the employee and audit changes. This increment does not claim the event has been delivered to EventBridge. The dispatcher must later provide at-least-once delivery, retries and a DLQ; consumers must be idempotent.

## Downstream employee-status projection contract

Each receiving service owns a local projection keyed by `employeeId`, containing only `isActive`, `lifecycleVersion` and `updatedAtUtc`. Consumers:
- Apply an event only when its `lifecycleVersion` is greater than the locally stored version.
- Ignore duplicate events and older/out-of-order versions.
- Persist the projection update atomically with the consumer's event-deduplication marker.
- Enforce the local employee status at the service boundary; do not read Identity's database.
- On deactivation, deny new access to employee-owned resources in that service.
- On reactivation, update status only; do not restore prior sessions or bypass the service's normal authorization checks.

The projection is a status signal, not a replacement for each service's resource/tenant authorization. Until dispatch and all consumers are deployed, global revocation is not immediate; existing stateless JWTs may remain usable by a receiving service until expiry, as ADR-0002 states.

## Acceptance criteria and test mapping

| ID | Acceptance criterion | Verification |
|---|---|---|
| ILC-01 | Only a validated `HrAdmin` can execute either lifecycle command. | API integration tests: anonymous, Employee and OrganizationAdmin denied; HrAdmin accepted. |
| ILC-02 | Actor identity comes from JWT `sub`; a client cannot impersonate another actor. | Integration test asserts audit actor equals token subject while a spoofed body field is ignored. |
| ILC-03 | Deactivation/reactivation changes status and increments lifecycle version exactly once. | MySQL integration tests for both directions and domain unit tests. |
| ILC-04 | Employee update, audit and outbox commit atomically. | MySQL integration test injects a post-save failure and verifies all writes roll back. |
| ILC-05 | Same-key/same-command retry returns the original result without duplicate rows. | Concurrent MySQL integration test. |
| ILC-06 | Same-key/different-command reuse returns 409 without mutation. | MySQL integration test. |
| ILC-07 | Same-state command is a 200 no-op without audit/outbox writes. | MySQL integration test. |
| ILC-08 | Concurrent stale transitions cannot create duplicate or orphaned records. | MySQL row-lock/version and idempotency integration coverage. |
| ILC-09 | Events use schema version 1 and include required envelope/payload fields without credentials or unnecessary PII. | Outbox JSON integration assertion. |
| ILC-10 | API contract documents 400/401/403/404/409 outcomes and correlation behavior. | Controller integration tests and API contract. |
| ILC-11 | Existing login guard continues to reject inactive employees. | Existing login integration test remains green. |
| ILC-12 | Checked-in EF migration applies at startup and can be applied to a clean MySQL 8.4 database. | GitHub Actions MySQL Testcontainers suite, run #72. |
| ILC-13 | Blank/overlength reason codes and overlength notes return 400 without mutating employee, audit or outbox; maximum accepted lengths succeed and the reason code is trimmed. | MySQL API-host integration tests. |
| ILC-14 | Whitespace/overlength/repeated idempotency headers and repeated correlation headers return 400 before mutation. | MySQL API-host integration test verifies employee, audit and correlation-scoped outbox state remain unchanged. |
| ILC-15 | One-character and 128-character visible-ASCII idempotency keys are accepted; control, DEL and non-ASCII characters return 400 before mutation. | MySQL API-host integration test verifies accepted boundary transitions and no mutation for rejected character classes. |

| ILC-16 | Missing or non-GUID JWT `sub` claims on otherwise correctly signed HrAdmin tokens return 401 before any lifecycle mutation. | MySQL API-host integration test verifies employee status/version, lifecycle audit and correlation-scoped outbox remain unchanged. |
| ILC-17 | A supplied X-Correlation-ID UUID is returned unchanged and persisted identically in the lifecycle audit, outbox row and event envelope. | MySQL API-host integration test compares the supplied UUID across all four representations. |
| ILC-18 | When X-Correlation-ID is omitted, Identity generates one UUID and returns/persists that same value in the lifecycle audit, outbox row and event envelope. | MySQL API-host integration test verifies the generated UUID is non-empty and identical across all four representations. |
| ILC-19 | A same-actor retry with the same idempotency key and command but a different X-Correlation-ID replays the original transition, event ID and original correlation ID; it creates no additional audit or outbox row. | Sequential MySQL API-host integration test verifies both responses, persisted audit/outbox/envelope correlation IDs, and single-row counts. |
| ILC-20 | A present X-Correlation-ID header with an empty/whitespace or non-UUID value is invalid and returns 400 before any lifecycle mutation; only an absent header causes Identity to generate a UUID. Header presence is detected independently of its parsed value count. | MySQL API-host integration test sends a whitespace-only header and verifies HTTP 400, unchanged employee status/version, no lifecycle audit and no outbox insertion; controller-level test also verifies a present header with zero parsed values is rejected without invoking the lifecycle service. |

## Deferred work (not acceptance criteria for this increment)

Employee invitations/provisioning, profile/role mutation, Organization Admin request intake, HRIS integration, EventBridge dispatch/retries/DLQ, and downstream consumer implementations each require their own SDD change and tests.

