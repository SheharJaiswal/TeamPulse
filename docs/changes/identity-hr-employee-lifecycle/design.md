# Design: HR-Authorized Employee Lifecycle Commands

## Service boundary

Identity remains the only owner of employee profile and lifecycle state. No new microservice is created. The API layer authenticates and validates the request; an Application abstraction owns the lifecycle command contract; Infrastructure performs the transactional MySQL work through the existing `IdentityDbContext`.

## API and authorization

Add an `EmployeeLifecycleController` under `/api/identity/employees/{employeeId}` with separate deactivate/reactivate actions. Decorate the controller with `[Authorize(Roles = "HrAdmin")]`. The existing JWT bearer configuration already maps the `role` claim to ASP.NET Core roles. Do not trust actor IDs, roles, source values or status values from the request body.

Use a typed request containing only `reasonCode` and optional `note`. Read the actor GUID from the validated `sub` claim, the idempotency key from `Idempotency-Key`, and the optional correlation GUID from `X-Correlation-ID`. The server supplies source `hr-admin-api`.

## Domain and persistence model

- Extend `Employee` with `LifecycleVersion` (long, initial value 0) and a domain method that changes active state and increments the version only when the state changes.
- Configure `LifecycleVersion` as an EF Core concurrency token. A stale update raises `DbUpdateConcurrencyException`.
- Add `EmployeeLifecycleAudit`: immutable transition metadata, idempotency key and request fingerprint. Unique index: (ActorEmployeeId, IdempotencyKey).
- Add `OutboxMessage`: event ID, type, schema version, occurrence time, correlation ID, serialized minimal envelope, creation time and nullable publication time/attempt metadata reserved for the future dispatcher. Unique event ID is the primary key.
- Keep audit/outbox rows in the Identity database. No foreign keys cross service boundaries.

Create a checked-in EF Core migration and update the model snapshot. Existing employees start at lifecycle version 0.

## Application flow

1. Validate request and required headers before opening the transaction.
2. Begin a ReadCommitted transaction.
3. Look up (actor ID, idempotency key). If found, compare the stored fingerprint and return the prior result or a conflict.
4. Load the target employee. Return not found if absent.
5. If already in the desired state, commit/close without writing audit or outbox and return the no-op result.
6. Capture the previous status, invoke the domain transition (incrementing lifecycle version), generate event/correlation IDs and a single UTC timestamp.
7. Construct the immutable audit row and schema-v1 event envelope. The event ID equals the audit ID.
8. Add audit and outbox entities and call SaveChanges once inside the transaction.
9. Commit. If a concurrency or uniqueness race occurs, roll back and map it to a conflict after checking whether the same idempotency key now has a completed matching result.
10. Return the resulting status, lifecycle version, event ID and correlation ID. Never return the event payload or any credential data.

The request fingerprint is SHA-256 over a stable canonical representation of target employee ID, desired status, trimmed reason code and trimmed note. Correlation ID is deliberately excluded. Avoid logging request notes or full event payloads.

## Transactional outbox and delivery boundary

The transaction guarantees that an employee transition cannot commit without its audit and outbox record, and an audit/outbox record cannot commit without the transition. The outbox is the durable hand-off point.

The communication ADR selects EventBridge for state-change fan-out and SQS/DLQ for retryable work. This repository does not yet contain the bus name, AWS deployment role, dispatcher configuration or receiving service projects. Therefore the current implementation must persist the outbox record but must not log or report it as published. A later SDD change will add a lease-safe dispatcher with at-least-once delivery, bounded retry/backoff, DLQ handling and operational monitoring. It must mark an outbox record published only after the transport acknowledges acceptance.

## Consumer projection

The new `docs/contracts/employee-status-projection.md` is the contract for future consumers. Each service stores its own employee status/version projection and applies only strictly newer lifecycle versions. This prevents duplicate or out-of-order events from reactivating an employee after a newer deactivation. No consumer may query the Identity database.

## Failure and concurrency handling

- Any exception before commit rolls back the employee, audit and outbox changes.
- Optimistic concurrency on `LifecycleVersion` prevents two commands based on the same state from both committing.
- The idempotency unique index is the final guard for concurrent retries.
- A duplicate key with the same fingerprint returns the committed original transition; a different fingerprint is a conflict.
- Same-state commands are no-ops and produce no audit/event because there was no state transition.
- Do not catch and suppress cancellation, database unavailability or unexpected exceptions. Return a safe error without exposing SQL, connection strings or internal exception details.

## Test strategy

Use the existing MySQL 8.4 Testcontainers fixture for transaction, migration, unique-index and concurrency behavior. Use API integration tests with signed test JWTs to verify role enforcement and actor attribution. Add focused unit/contract tests for request normalization, fingerprint determinism, event serialization and lifecycle version increment. Existing registration/login/health/bootstrap tests remain regression coverage.
