# Design: Local RabbitMQ Outbox Transport

## Service boundary

Keep the outbox worker and publisher in Identity. Keep `IEventPublisher` in the Application layer. RabbitMQ.Client is an Infrastructure dependency; AWS SDK EventBridge remains a separate Infrastructure adapter. No downstream service reads the Identity database.

## Provider selection

`Program.cs` reads `OutboxDispatcher:Transport` only when the dispatcher is enabled. It registers exactly one `IEventPublisher`:
- `RabbitMq`: bind `RabbitMqOptions`, validate the AMQP URI, register the RabbitMQ publisher.
- `EventBridge`: validate existing bus/region settings, register the AWS client and existing EventBridge publisher.

The common outbox store, retry worker and metrics are shared. Do not register both providers or silently switch providers on failure.

## RabbitMQ publisher

Use the official `RabbitMQ.Client` asynchronous API. Keep one long-lived connection and one publisher-confirm channel per Identity API process. A `SemaphoreSlim` serializes access because an AMQP channel must not be used by concurrent publishing operations. Enable automatic connection/topology recovery and recreate a closed channel after recovery.

For each message:
1. Build persistent AMQP properties from the existing immutable envelope metadata.
2. Publish the exact stored envelope bytes to the configured durable topic exchange and fixed lifecycle routing key with `mandatory=true`.
3. Await the publisher-confirm-tracked `BasicPublishAsync` operation.
4. Return `Accepted` only after the operation completes. A nack or mandatory return is a systemic transport failure.
5. If the broker cannot be reached before publication, return a systemic result for the entire batch. If a later entry fails after earlier entries were confirmed, preserve earlier accepted results and return systemic results for the failed and remaining entries.

Do not include exception messages, URI values or payloads in error codes/logs. Use stable sanitized codes such as `RabbitMqUnavailable` and `RabbitMqPublishFailed`.

## Local topology

Docker Compose runs a pinned RabbitMQ management image with a persistent data volume and health check. A read-only mounted RabbitMQ definitions file imports the topic exchange, five durable service queues and their lifecycle bindings. The local definitions file is only a development convenience; production queue declarations, access policies and consumer failure handling remain owned by each service.

The local RabbitMQ body is the complete TeamPulse envelope. EventBridge retains its standard outer event wrapper, where the same envelope is in `detail`. This is a transport-adapter difference, not a domain-contract change.

## Configuration

- Keep `OutboxDispatcher:Enabled=false` in checked-in appsettings.
- Keep `OutboxDispatcher:Transport=EventBridge` as the code fallback for backward-compatible deployment behavior.
- Local run instructions set `OutboxDispatcher__Enabled=true` and `OutboxDispatcher__Transport=RabbitMq`.
- `RabbitMq:Uri` is configurable and contains only local placeholder credentials in checked-in Development settings.
- Production EventBridge settings remain `EventBridge:BusName` and `AWS_REGION`; they are not needed locally.

## Testing

- Unit-test URI, scheme, exchange and routing-key validation.
- Use Testcontainers.RabbitMq to start the same RabbitMQ major/minor image used by Compose.
- Declare the five consumer queues/bindings in the test fixture, publish a single schema-v1 event, and assert each queue receives the same envelope and persistent AMQP metadata.
- Verify an exchange with no matching queue produces a systemic failure rather than an accepted result.
- Keep existing MySQL Testcontainers tests for outbox lease/retry/acknowledgement semantics; no database schema change is required.
