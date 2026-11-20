# Proposal: HR-Authorized Employee Lifecycle Commands

## Problem

The one-time bootstrap utility establishes the first `HrAdmin`, and Identity already rejects login for employees whose `IsActive` flag is false. There is still no authenticated workflow for HR to provisionally deactivate or explicitly reactivate an employee, no durable transition audit, and no transactional event record for the other services. A direct database update would bypass actor attribution and leave downstream services unaware.

## Approved policy boundary

ADR-0002 is authoritative:

- HR controls employee activation and deactivation. A trusted HRIS feed may act on HR's behalf after a separately approved integration.
- Identity owns canonical employee, organization, team and role records.
- Organization Admins may manage organization configuration and qualifying aggregate access, but may not directly override HR employee lifecycle decisions. They may request HR changes; the request-intake workflow is not defined or implemented.
- Deactivation blocks new Identity login. Reactivation is an explicit HR action and does not restore previous sessions.
- Each transition records actor/source, target employee, old/new status, UTC time, reason code, optional note and correlation ID.
- Identity publishes versioned lifecycle events through a transactional outbox. Receiving services maintain local status projections.

For profile ownership, HR is the authority for canonical employee fields (email, display name, organization ID, team ID and role). Organization Admins have no direct write permission to those employee fields. The role catalogue and elevated-role assignment policy are not defined, so this change does not add profile or role mutation endpoints. Password hashes and credentials are never editable through profile/lifecycle commands.

## Proposed change

Add an HR-only lifecycle command boundary to Identity:

- `POST /api/identity/employees/{employeeId}/deactivate`
- `POST /api/identity/employees/{employeeId}/reactivate`

The API requires a validated JWT with the `HrAdmin` role. The actor employee ID is taken only from the validated `sub` claim. Commands require a reason code and an `Idempotency-Key`; an optional UUID `X-Correlation-ID` is accepted, otherwise Identity generates one.

A successful state transition updates the employee, writes an immutable lifecycle audit row and writes the corresponding versioned event to the Identity outbox in one MySQL transaction. A monotonically increasing employee lifecycle version allows consumers to ignore duplicate and out-of-order events. Same-state commands are no-ops. A repeated idempotency key with the same canonical command returns the original result; reusing it for a different command is rejected.

## Focused implementation scope

This increment implements the HR lifecycle command, its authorization, lifecycle version, audit persistence and transactional outbox write in Identity. It defines the event and downstream projection contracts. It does not implement a transport dispatcher or consumers because those require a deployment-ready EventBridge bus, retry/DLQ configuration and the other service codebases.

Employee provisioning/invitation is deliberately a separate follow-up: the repository has no approved invitation delivery, invitation expiry, initial credential setup or password activation flow. The existing bootstrap utility creates only the first HR administrator and is not an employee invitation mechanism.

## Out of scope

- Employee invitation/provisioning endpoints, invitation delivery, password setup/reset and HRIS integration.
- Organization Admin request intake, approval workflow or direct employee writes.
- Role catalogue definition and privileged role assignment.
- EventBridge dispatcher, retry/backoff, DLQ and operational alerting.
- Consumer projection implementations in Habit, Challenge, Event Tracking, Insight and Notification.
- Immediate revocation of already-issued stateless JWTs.

## Acceptance

The scoped lifecycle API is usable only by an authenticated `HrAdmin`; every state transition and its audit/outbox records commit atomically; retries cannot create duplicate transitions/events; stale concurrent transitions fail safely; and the event contract enables receiving services to maintain idempotent, version-aware employee-status projections without cross-service database access.
