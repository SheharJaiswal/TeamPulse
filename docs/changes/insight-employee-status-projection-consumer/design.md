# Design: Insight Employee Status Projection Consumer

**Status:** Implemented with MySQL persistence.

## Architecture
Identity transactional outbox -> local RabbitMQ publisher -> domain event exchange -> Insight lifecycle consumer -> application processor -> MySQL employee-status projection and processed-event inbox.

The projection is owned by Insight and remains transport-independent. EventBridge/SQS production transport is deferred until cloud infrastructure is provisioned.

## Persistence
Insight uses its existing MySQL database and EF Core context for both activity summaries and lifecycle state. The lifecycle projection is keyed by employee ID and stores only active state, lifecycle version and UTC update time. The processed-event inbox is keyed by event ID and stores employee, event type, schema version, lifecycle version and processing time.

Projection update and inbox insert are committed in one MySQL transaction. The store uses serializable isolation, row locking and bounded retries for duplicate-key/deadlock/lock-timeout races. Only strictly newer lifecycle versions update the projection; stale/equal events are recorded without regression. Duplicate event IDs are acknowledged without changing state.

## RabbitMQ
The lifecycle consumer retains manual acknowledgement, bounded transient retries, confirmed dead-letter publishing, poison-event classification, reconnect behavior and independent consumer health. The source delivery is acknowledged only after the database transaction commits or a durable dead-letter publish is confirmed.

## Local development
MySQL is the only persistence dependency for Insight. No AWS SDK, cloud credentials, local DynamoDB service or external table bootstrap is required. Insight migrations create the activity and lifecycle tables.

## Verification
MySQL Testcontainers integration tests cover atomic projection/inbox persistence, duplicate event IDs, stale/equal/newer versions and concurrent version ordering. Existing RabbitMQ integration and topology tests continue to verify delivery, acknowledgement, retries and DLQ behavior.
