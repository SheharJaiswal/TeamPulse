# Employee Status Projection Contract v1

## Purpose and ownership

Identity is the source of truth for employee lifecycle state. Habit, Challenge, Event Tracking, Insight and Notification each maintain their own local projection. No receiving service reads Identity's database or stores a cross-service foreign key.

## Projection shape

A receiving service stores only the fields required for lifecycle authorization:

| Field | Type | Meaning |
|---|---|---|
| `employeeId` | UUID | Stable Identity employee identifier; projection key. |
| `isActive` | boolean | Latest known lifecycle state. |
| `lifecycleVersion` | integer (64-bit) | Monotonic Identity version for the employee. |
| `updatedAtUtc` | UTC timestamp | Time the projection was applied. |

Do not copy email, display name, password data, credentials, JWTs, habits, activity submissions, evidence or wellness data into this projection.

## Source events

- `EmployeeDeactivated`, schema version 1.
- `EmployeeReactivated`, schema version 1.

Both use the shared domain event envelope. Their payload includes `employeeId`, `actorEmployeeId`, `source`, `previousStatus`, `newStatus`, `lifecycleVersion`, `reasonCode` and optional `note`.

## Consumer rules

1. Validate the envelope, event type and supported schema version.
2. Deduplicate by `eventId`; assume at-least-once delivery.
3. Apply the event only when `payload.lifecycleVersion` is strictly greater than the stored projection version. Ignore duplicates and lower/equal versions.
4. Update the projection and consumer deduplication marker atomically.
5. Enforce inactive status at the receiving service boundary for that service's employee-owned resources. Keep normal tenant/resource authorization in force.
6. A reactivation updates the status projection; it does not restore a previous session or bypass normal authorization.
7. Retry transient failures; route poison/unsupported messages to the configured DLQ and alert operators. Exact transport/retry settings are deployment configuration and are not defined by this contract.

## Delivery semantics and limitation

EventBridge is the selected domain-event fan-out mechanism. The Identity transactional outbox is the durable source for dispatch. Delivery is at least once, not exactly once. Until the dispatcher and every receiving projection are deployed, an already-issued stateless JWT may remain usable by a receiving service until expiry. Do not claim global immediate revocation.
