# Proposal: Local RabbitMQ Transport for Identity Outbox

## Summary

Add a RabbitMQ transport adapter to the existing Identity transactional outbox so the six-service platform can exchange versioned domain events during local development without AWS credentials or a deployed EventBridge bus. Keep the existing EventBridge adapter as the production transport selected explicitly by deployment configuration.

## Motivation

The Identity outbox dispatcher and EventBridge fan-out template are implemented, but the owner will provision EventBridge only during the later cloud-infrastructure setup. Development of downstream services and event-driven workflows should continue now. A local RabbitMQ broker plus the official RabbitMQ .NET client provides real cross-process delivery between independently running .NET services.

## Scope

- Add explicit outbox transport selection while preserving the existing `IEventPublisher` and MySQL outbox state machine.
- Add a RabbitMQ.Client 7.x publisher using a durable topic exchange, persistent JSON messages, publisher confirms and mandatory routing.
- Seed one local exchange and five durable service-owned queues/bindings through RabbitMQ definitions loaded by Docker Compose.
- Add RabbitMQ to local Compose without making it a prerequisite for API startup when the dispatcher is disabled.
- Add Testcontainers.RabbitMq integration coverage for broker-confirmed fan-out to all five queues and unroutable-message handling.
- Document the local configuration, envelope mapping and transport boundary.

## Non-goals

- Do not remove, replace or redesign the EventBridge production adapter or its CloudFormation template.
- Do not deploy or configure AWS resources, credentials, EventBridge, SQS, IAM or CloudWatch.
- Do not create a seventh microservice or give Identity ownership of downstream service data.
- Do not implement the five downstream consumers or their business projections in this increment.
- Do not add a new database migration or alter the existing outbox schema.
- Do not claim exactly-once delivery.

## Safeguards

- The dispatcher remains disabled by default.
- RabbitMQ is a local-development and integration-test transport only; EventBridge remains the production transport.
- The transport provider is selected explicitly. A missing/invalid provider configuration fails fast when dispatch is enabled.
- A message is accepted only after RabbitMQ publisher confirmation and successful mandatory routing. Broker errors are systemic and release the lease for a delayed retry; they do not quarantine all pending events.
- The RabbitMQ body contains the existing complete TeamPulse envelope. Consumers deduplicate by `eventId` and apply only strictly newer `lifecycleVersion` values.
- Identity readiness remains based on its own MySQL database; a broker outage must not make the API liveness/readiness probes report a database failure.

## Success criteria

The Identity API can run locally with MySQL and RabbitMQ, publish an outbox event to all five independent service queues without AWS access, preserve the event envelope and stable identity, and safely retry if RabbitMQ is unavailable or cannot route the message. Existing EventBridge behavior remains available for later cloud deployment.
