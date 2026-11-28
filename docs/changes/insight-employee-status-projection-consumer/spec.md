# Specification: Insight Employee Status Projection Consumer

## Ownership and data
Identity remains the lifecycle source of truth. Insight owns a local projection with employee ID, active state, monotonic lifecycle version and UTC update time. The processed-event inbox stores event ID, employee ID, event type, schema version, lifecycle version and processing time only.

## Processing
1. Validate the shared event envelope and supported lifecycle event type/schema.
2. Deduplicate by event ID.
3. Apply only when the incoming lifecycle version is greater than the stored version.
4. Commit projection update and inbox insert in one MySQL transaction.
5. Return Duplicate for an already processed event; record stale/equal events without projection regression.
6. Acknowledge successful outcomes only after commit.
7. Retry transient database/network failures within configured bounds; dead-letter invalid, unsupported or exhausted messages using the existing confirmed DLQ flow.

## Persistence and concurrency
Use Insight's MySQL database, EF Core context and migrations. The projection has a primary key on employee ID; inbox has a primary key on event ID. Transactions use serializable isolation and lock the employee projection row. Bounded retry handles MySQL deadlocks, lock timeouts and unique-key races. The projection and inbox must never commit independently.

## Health and local setup
Readiness checks the Insight MySQL database. Lifecycle consumer health remains independent. Local Insight requires MySQL and RabbitMQ; no AWS account, cloud credentials, local DynamoDB service or external table initialization is required.

## Acceptance criteria
- Atomic projection/inbox persistence.
- Duplicate event ID does not mutate projection.
- Stale/equal lifecycle version does not regress state.
- Newer lifecycle version is applied.
- Concurrent deliveries preserve the maximum lifecycle version.
- Manual acknowledgement follows commit; poison messages use confirmed dead-letter publishing.
- Release build, MySQL integration tests, RabbitMQ integration tests and topology tests pass.
