# Domain Event Envelope

Fields:
- `eventId` (UUID)
- `eventType`
- `schemaVersion` (integer)
- `occurredAtUtc` (UTC)
- `correlationId` (UUID)
- `payload` (event-specific, minimal data)

Events are versioned contracts. A breaking payload change requires a new schema version; consumers must reject or safely quarantine unsupported versions. Never include access tokens, passwords, password hashes, credentials or unnecessary private wellness data.

## Employee lifecycle event schema v1

Identity emits `EmployeeDeactivated` and `EmployeeReactivated` using this envelope. The payload contains:
- `employeeId` (UUID)
- `actorEmployeeId` (UUID from the validated HR administrator subject)
- `source` (for this API: `hr-admin-api`)
- `previousStatus` and `newStatus` (`active` or `inactive`)
- `lifecycleVersion` (monotonically increasing 64-bit integer per employee)
- `reasonCode` (opaque bounded code supplied by the HR actor)
- `note` (optional explanatory note)

The `eventId` is also the immutable lifecycle audit ID. The outbox row, audit row and employee state transition are committed in one Identity database transaction. The outbox record is not evidence of EventBridge delivery; dispatch is a separate at-least-once operation.
