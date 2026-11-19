# Domain Event Envelope

Fields: `eventId` (UUID), `eventType`, `schemaVersion`, `occurredAtUtc` (UTC), `correlationId` (UUID), `payload`.

Payloads are event-specific and minimal. Never include access tokens, passwords, hashes or unnecessary private wellness data.