# Communication Conventions

- Frontend requests use HTTPS REST through the API entry point.
- Immediate authoritative validation uses a private owner API.
- State-change fan-out uses versioned EventBridge domain events.
- Retryable work uses SQS queues and DLQs where appropriate.
- Event envelope: eventId, eventType, schemaVersion, occurredAtUtc, correlationId, payload.
- Consumers are idempotent; assume at-least-once delivery.
- Use transactional outbox for state-change publication.
- Never claim exactly-once delivery.

## Identity lifecycle event transport

- Identity publishes its existing complete schema-v1 envelope through EventBridge `PutEvents`.
- EventBridge `Source` is `teampulse.identity`; `DetailType` is the exact envelope `eventType`; `Time` is `occurredAtUtc`; `Detail` is the serialized envelope without an additional wrapper.
- The event bus name is supplied by deployment configuration (`EventBridge__BusName`); account-specific resource names and ARNs are not hard-coded.
- Identity's publisher uses the AWS SDK default credential chain and a workload role limited to `events:PutEvents` on the configured bus.
- An outbox row is marked published only after its individual EventBridge entry is acknowledged. This means accepted by the bus, not delivered to a rule target or consumed.
- Producer-side PutEvents failures remain in the Identity outbox retry/quarantine workflow. SQS DLQs are configured on EventBridge rule targets for downstream target-delivery failures; the two failure boundaries are distinct.
- Event consumers deduplicate by `eventId` and, for employee lifecycle state, apply only strictly newer `lifecycleVersion` values.
