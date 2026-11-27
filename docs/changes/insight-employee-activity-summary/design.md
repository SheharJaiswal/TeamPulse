# Design: Employee Activity Summary Insights

**Status:** Proposed; requires owner approval.

## Components
- Application: transport-independent activity-event processor and summary query abstraction.
- Domain: daily employee activity summary value/entity with validation.
- Infrastructure: DynamoDB transactional store, RabbitMQ activity consumer, dedicated DLQ publisher.
- API: authenticated employee-self-only summary endpoint.

## DynamoDB key design
Reuse the existing Insight-owned table:
- Daily summary: PK `EMPLOYEE#<employeeId>`, SK `ACTIVITY#<yyyy-MM-dd>`; attributes employeeId, activityDate, habitActivityCount, challengeActivityCount.
- Activity inbox: PK `EVENT#<eventId>`, SK `ACTIVITY`; attributes eventId, eventType, schemaVersion, employeeId, activityDate, processedAtUtc.

The transaction inserts the inbox item with `attribute_not_exists(PK)` and atomically ADDs one to the daily summary category. A conditional failure is followed by a strongly consistent inbox read to distinguish duplicate from an actual persistence conflict.

## Query
Query the employee partition with a bounded sort-key range for requested dates. The API derives employeeId from JWT sub and cannot query another employee's partition.

## RabbitMQ topology
Add a distinct durable queue and DLQ for Insight activity events. Bind only the two activity routing keys. Use quorum queue arguments and the repository's existing at-least-once dead-letter conventions. Do not repurpose the lifecycle queue or alter existing queue arguments.

## Verification
Unit tests cover event validation and category mapping. DynamoDB Local tests cover transaction, duplicate, concurrency and date queries. API integration tests cover auth/sub/range. RabbitMQ Testcontainers tests cover ack, retry, DLQ and redelivery. Python topology contract tests verify exact new queue/bindings while asserting existing lifecycle bindings remain unchanged.
