# Proposal: Lease-Safe Identity Outbox Dispatcher

**Change ID:** identity-outbox-eventbridge-dispatcher  
**Status:** Core implementation CI-verified; acceptance-test hardening remains and production activation is blocked on deployment infrastructure.  
**Date:** 2026-11-20

## Problem

Identity atomically persists employee lifecycle changes, immutable audit rows and schema-v1 outbox messages in MySQL. The outbox is durable, but no process currently publishes those messages. Consequently, the five receiving services cannot yet maintain their employee-status projections from Identity events.

The accepted communication architecture selects EventBridge for state-change fan-out, requires transactional outbox publication and assumes at-least-once delivery. The dispatcher must tolerate multiple Identity API replicas, worker crashes, EventBridge partial failures and duplicate delivery without losing events or falsely marking them as published.

## Goal

Add a bounded, lease-safe dispatcher hosted by the existing Identity API. It claims pending outbox rows from the Identity database, publishes the existing event envelope to a configured EventBridge event bus, and records publication only after EventBridge acknowledges the individual entry.

The implementation must remain safe when multiple API instances run concurrently. It must be disabled by default until deployment configuration and IAM are supplied.

## Architectural decisions

1. **No seventh service.** The dispatcher is a hosted worker inside Identity API. Identity remains the sole owner of the outbox and employee lifecycle state.
2. **Transport.** Use AWS EventBridge `PutEvents` through the AWS SDK for .NET. Do not add a second business-data store or publish directly from the HTTP lifecycle request.
3. **Event mapping.** Set `Source` to the constant `teampulse.identity`, `DetailType` to the existing `eventType`, `Time` to `occurredAtUtc`, and `Detail` to the complete existing schema-v1 envelope JSON. Set `EventBusName` from `EventBridge__BusName`; never hard-code an account-specific bus name.
4. **Region and credentials.** Resolve region from the standard `AWS_REGION` environment variable / AWS SDK region configuration. Use the AWS SDK default credential chain: ECS task role or EC2 instance profile in deployed environments, and a local developer profile for local use. Do not accept static access keys in application configuration.
5. **Least privilege.** The Identity runtime role needs only `events:PutEvents` on the exact configured event-bus ARN. No wildcard resource permission is required.
6. **Safe activation.** `OutboxDispatcher__Enabled` defaults to `false`. When enabled, startup validation requires a non-empty `EventBridge__BusName` and a resolvable AWS region. Missing configuration must fail fast rather than silently dropping events.
7. **Lease-based claiming.** Use short MySQL transactions and `SELECT ... FOR UPDATE SKIP LOCKED` to claim a bounded batch. Persist a unique lease token and expiry, commit the claim, and only then call EventBridge. Never hold database row locks during network I/O.
8. **At-least-once acknowledgement.** Process each `PutEvents` result entry independently. An entry is accepted only when its response has a non-empty EventBridge event ID and no entry error code. A successful HTTP response or a zero/non-zero batch-level count alone is not enough to mark every row published.
9. **Publication timestamp.** Set `PublishedAtUtc` only after the corresponding entry is accepted by EventBridge and only when the row still has the dispatcher's lease token. This timestamp means accepted by the bus, not delivered to every consumer.
10. **Retries and quarantine.** Retry transient failures with bounded exponential backoff and full jitter. Quarantine permanent event-specific failures and exhausted transient attempts in the Identity outbox; preserve the original event for investigation. Systemic bus/IAM configuration failures pause dispatch and alert rather than dead-lettering every pending event.
11. **DLQ boundary.** An Identity outbox quarantine is for producer-side failures before EventBridge acceptance. SQS dead-letter queues belong on EventBridge rule targets to capture downstream target-delivery failures. These are different failure boundaries and must not be conflated.
12. **Idempotency.** A crash after EventBridge acceptance but before the MySQL acknowledgement can cause a duplicate publish after lease expiry. This is expected. Consumers deduplicate using the stable envelope `eventId` and apply per-employee `lifecycleVersion` ordering.

## Scope

- Dispatcher configuration and startup validation.
- Application publisher/dispatcher abstractions.
- MySQL outbox lease, retry, quarantine and acknowledgement persistence.
- EventBridge publisher and partial-result handling.
- Hosted worker with bounded polling and cancellation.
- Unit and MySQL Testcontainers coverage for concurrency, leases, retry, partial failure and crash/replay semantics.
- Documentation of runtime configuration, IAM and operational metrics.

## Out of scope

- Provisioning an EventBridge bus, IAM role, EventBridge rules, SQS queues, SQS DLQs, CloudWatch alarms or deployment pipelines. No deployment/IaC tree exists in the current repository.
- Implementing consumers or employee-status projections in Habit, Challenge, Event Tracking, Insight or Notification.
- Global JWT revocation or a claim of immediate cross-service access revocation.
- Employee invitations, HRIS integration or other Identity profile workflows.
- Exactly-once delivery.

## Deployment prerequisite

The repository currently contains no deployment or infrastructure-as-code directory, no EventBridge bus definition, no runtime IAM role, and no EventBridge rule/target or SQS DLQ definitions. The dispatcher can be implemented and tested with a mocked EventBridge client while disabled by default, but it must not be enabled in a deployed environment until those resources, the exact bus ARN, target rules and alarms are provisioned and reviewed.

## Success criteria

The worker is safe under concurrent replicas; every accepted event is eventually marked published; no unaccepted event is marked published; transient and permanent failures are observable and recoverable; duplicate delivery is documented and safe; and no new microservice or cross-service database dependency is introduced.
