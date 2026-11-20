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


## Identity lifecycle event fan-out infrastructure

- The parameterized CloudFormation stack is `infra/cloudformation/identity-eventbridge.yaml`; it creates one custom bus and one rule for schema-v1 `EmployeeDeactivated` / `EmployeeReactivated` events from `teampulse.identity`.
- The rule targets one standard SQS queue each for Habit, Challenge, Event Tracking, Insight and Notification. Each target has its own EventBridge target-delivery DLQ.
- EventBridge queue policies allow only `events.amazonaws.com` from the exact rule ARN and current AWS account. Identity receives only `events:PutEvents` on the custom bus. Consumer receive/delete permissions remain owned by each consumer's deployment.
- SQS target messages retain the full outer EventBridge event; the TeamPulse schema-v1 envelope is in its `detail` field. Consumers deduplicate by `eventId` and apply strictly newer `lifecycleVersion` values.
- EventBridge target DLQs, Identity MySQL outbox quarantine and consumer poison-message DLQs are distinct failure boundaries.
- The dispatcher remains disabled until non-production infrastructure and all five consumer projections are verified.


## Local development transport

- RabbitMQ is the local development and integration-test transport only; it is not a new production messaging architecture or a seventh service.
- The Identity outbox selects exactly one publisher through `OutboxDispatcher:Transport`. Local runs set `RabbitMq`; production cloud deployments select the existing `EventBridge` adapter explicitly.
- Local RabbitMQ uses the durable topic exchange `teampulse.domain.events`, routing key `identity.employee.lifecycle`, and one durable queue for each existing receiving service. The topology is preloaded by the local Docker Compose broker.
- RabbitMQ messages carry the complete TeamPulse envelope as the body with persistent delivery, message ID, event type, correlation ID and schema-version metadata. The publisher uses mandatory routing and publisher confirms before the Identity outbox is acknowledged.
- RabbitMQ consumers receive the envelope directly. EventBridge/SQS consumers extract the same envelope from the outer EventBridge event's `detail` field. This is a transport adapter difference only.
- RabbitMQ broker availability is not part of Identity database readiness. If the broker is unavailable, the outbox worker releases leases for delayed retry; it does not mark messages published.
- Local RabbitMQ credentials/topology are development placeholders. Production EventBridge provisioning is deferred until cloud infrastructure setup.

## Habit local lifecycle consumer

- Habit consumes the direct schema-v1 envelope from `teampulse.habit.events`; it does not expect an EventBridge outer wrapper on RabbitMQ.
- The consumer is disabled by default and is enabled explicitly through `HabitConsumer__Enabled` and `HabitConsumer__Uri` for local development.
- Manual acknowledgement follows the Habit MySQL inbox/projection transaction. Applied, Stale and Duplicate outcomes are acknowledged only after the processor returns from its committed store operation.
- Prefetch is fixed at one until per-channel acknowledgement concurrency is explicitly reviewed. The consumer uses bounded full-jitter retries for known transient MySQL/network errors; malformed events and exhausted retries are rejected without requeue to the Habit quorum DLQ.
- The Habit queue has a finite broker delivery limit and an at-least-once dead-letter route. The local topology is seeded by `infra/rabbitmq/definitions.json`; other service queues remain unchanged.
- RabbitMQ connectivity is exposed separately at Habit `/health/consumer`. Habit database readiness does not depend on the broker.
- The application projection processor remains transport-independent. A future SQS adapter can call the same processor when cloud infrastructure is provisioned; no AWS dependency is needed for local development.


## Challenge local lifecycle consumer

- Challenge consumes the direct schema-v1 envelope from `teampulse.challenge.events`; it does not expect an EventBridge outer wrapper on RabbitMQ.
- The consumer is disabled by default and enabled explicitly through `ChallengeConsumer__Enabled` and `ChallengeConsumer__Uri` for local development.
- Challenge owns its MySQL projection and processed-event inbox. Inbox insertion and the conditional strictly-newer-version projection upsert commit atomically.
- Manual acknowledgement follows the Challenge MySQL inbox/projection transaction. Applied, Stale and Duplicate outcomes are acknowledged only after the store commits.
- Prefetch is fixed at one. Known transient MySQL/network failures use bounded exponential full-jitter retries with configurable attempt/base/max-delay limits. Invalid events, unsupported schema versions, invalid producers and duplicate-event integrity violations are explicitly published to the shared Challenge lifecycle DLQ with persistent delivery and the `x-teampulse-failure-classification` header. Transient exhaustion uses `TransientRetryExhausted`; non-transient processing exceptions use `NonTransientProcessingFailure`. A dedicated publisher-confirm channel uses mandatory routing and tracked confirms; the original delivery is acknowledged only after positive broker confirmation. If publishing, routing or confirmation fails, the source channel is closed without acknowledgement so RabbitMQ redelivers.
- The Challenge queue has a finite broker delivery limit and an at-least-once dead-letter route to `teampulse.challenge.events.dlq`. Its queue, DLX routing key and DLQ are Challenge-owned; Habit topology remains unchanged.
- RabbitMQ connectivity is exposed separately at Challenge `/health/consumer`. Challenge database readiness does not depend on the broker.
- The application processor is transport-independent. A future SQS adapter can extract the envelope from EventBridge `detail` and call the same processor; AWS deployment remains deferred.
