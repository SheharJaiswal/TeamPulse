# Specification: Local RabbitMQ Outbox Transport

**Status:** Approved for implementation from the project owner's direction to defer EventBridge provisioning and continue with a .NET-runnable local transport.

## Runtime selection

1. The existing `OutboxDispatcher:Enabled` setting remains `false` by default. API startup, migrations and health endpoints must work without RabbitMQ or AWS when dispatch is disabled.
2. Add `OutboxDispatcher:Transport` with supported values `RabbitMq` and `EventBridge`. The code default remains `EventBridge` to avoid silently changing an existing deployment that enables the dispatcher; local instructions explicitly set `RabbitMq`.
3. When `RabbitMq` is selected and dispatch is enabled, validate an absolute `amqp://` or `amqps://` URI and bounded exchange/routing-key values before the host starts.
4. When `EventBridge` is selected, preserve existing bus-name, AWS-region and SDK-default-credential validation. No AWS configuration is required for the RabbitMQ provider.
5. Unsupported transport values fail fast. Do not fall back from one transport to another silently.

## RabbitMQ topology

| Item | Contract |
|---|---|
| Exchange | `teampulse.domain.events` |
| Exchange type | Durable topic |
| Routing key | `identity.employee.lifecycle` |
| Message body | Complete existing schema-v1 TeamPulse event envelope JSON, without a transport wrapper |
| Content type | `application/json` |
| Delivery mode | Persistent |
| Message ID | Envelope `eventId` |
| Type property | Envelope `eventType` |
| Correlation ID | Envelope `correlationId` |
| Schema header | `schemaVersion` |
| Confirmation | Publisher-confirm tracking enabled; await broker confirmation |
| Routing | Mandatory publish; an unroutable message is not accepted |

Local development topology contains one durable queue per receiving service, all bound to the lifecycle routing key:

- `teampulse.habit.events`
- `teampulse.challenge.events`
- `teampulse.event-tracking.events`
- `teampulse.insight.events`
- `teampulse.notification.events`

The queues are local infrastructure fixtures. Each production consumer still owns its queue, access policy, retry and poison-message behavior in its own deployment.

## Dispatch semantics

- The existing MySQL outbox remains authoritative and is claimed with the existing lease-token fencing.
- RabbitMQ publishing is serialized per publisher channel. The connection is long-lived and uses the .NET client's automatic recovery.
- The dispatcher acknowledges an outbox row only after the corresponding publish operation completes with publisher confirmation and mandatory routing.
- If a publish fails, the failed and not-yet-attempted messages receive a sanitized systemic result. The worker releases their leases with its existing systemic-failure pause; it must not mark them published or quarantine them as malformed events.
- If RabbitMQ confirms a message and the process stops before the MySQL acknowledgement, the outbox may publish the same event again after lease expiry. This is expected at-least-once delivery.
- A batch is bounded by the existing dispatcher maximum of 10. Results preserve the original event IDs.
- RabbitMQ availability is not part of Identity database readiness. The worker retries when the broker is unavailable.

## Compatibility with EventBridge

The production EventBridge mapping remains unchanged: source `teampulse.identity`, detail type equal to `eventType`, event time equal to `occurredAtUtc`, and the full envelope as `Detail`. EventBridge/SQS consumers extract the envelope from the outer EventBridge `detail`; RabbitMQ consumers receive the envelope directly as the message body. The domain envelope itself is identical across transports.

## Security

- No AWS access keys or RabbitMQ production credentials are committed.
- The checked-in RabbitMQ URI/password is a local-only development placeholder, overridden with environment configuration where required.
- Never log broker URIs, credentials, message payloads or HR notes.
- The RabbitMQ broker is bound through Docker Compose ports for local development only. Do not treat the local credentials or topology as production-ready.

## Acceptance criteria and test mapping

| ID | Acceptance criterion | Verification |
|---|---|---|
| LRM-01 | Dispatcher disabled by default; API startup does not connect to RabbitMQ or AWS. | Existing startup/configuration tests. |
| LRM-02 | Explicit transport selection validates RabbitMQ and EventBridge settings independently. | Options/configuration unit tests. |
| LRM-03 | RabbitMQ publisher declares/uses the durable topic exchange and preserves envelope metadata. | RabbitMQ Testcontainers integration test. |
| LRM-04 | Confirmed, mandatory publication fans out to all five local service queues. | RabbitMQ Testcontainers integration test. |
| LRM-05 | Unroutable or failed broker publish is not acknowledged as accepted. | RabbitMQ Testcontainers integration test. |
| LRM-06 | A broker outage yields a systemic result and leaves outbox rows retryable. | Publisher/worker unit test and existing outbox persistence tests. |
| LRM-07 | Existing EventBridge provider mapping and configuration validation remain available. | Existing EventBridge publisher/configuration tests. |
| LRM-08 | Compose starts a local RabbitMQ service with the checked-in topology; no AWS dependency is introduced. | Compose validation and topology contract tests. |
| LRM-09 | No new microservice, database or EF migration is added. | Architecture/repository review. |
