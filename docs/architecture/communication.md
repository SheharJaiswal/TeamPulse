# Communication Conventions

- Frontend requests use HTTPS REST through the API entry point.
- Immediate authoritative validation uses a private owner API.
- State-change fan-out uses versioned EventBridge domain events.
- Retryable work uses SQS queues and DLQs where appropriate.
- Event envelope: eventId, eventType, schemaVersion, occurredAtUtc, correlationId, payload.
- Consumers are idempotent; assume at-least-once delivery.
- Use transactional outbox for state-change publication.
- Never claim exactly-once delivery.