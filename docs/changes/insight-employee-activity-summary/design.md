# Design: Employee Activity Summary Insights

**Status:** Approved for implementation.

## Components
- Application: transport-independent activity-event processor and summary query abstraction.
- Domain: daily employee activity summary and event inbox contracts.
- Infrastructure: MySQL transaction store, dedicated RabbitMQ activity consumer and DLQ publisher.
- API: authenticated employee-self-only summary endpoint.

## MySQL schema
`InsightDailyActivity`
- `EmployeeId CHAR(36) NOT NULL`
- `ActivityDate DATE NOT NULL`
- `HabitActivityCount INT NOT NULL DEFAULT 0`
- `ChallengeActivityCount INT NOT NULL DEFAULT 0`
- Primary key: `(EmployeeId, ActivityDate)`

`InsightProcessedEvent`
- `EventId CHAR(36) NOT NULL`
- `ProcessedAtUtc DATETIME(6) NOT NULL`
- Primary key: `EventId`

## Processing transaction
1. Begin transaction.
2. Insert EventId into `InsightProcessedEvent`.
3. Insert the daily aggregate row or atomically increment the matching category with `ON DUPLICATE KEY UPDATE`.
4. Commit both writes together.
5. If EventId already exists, roll back and return Duplicate without incrementing.
6. Ack RabbitMQ only after commit or confirmed duplicate.

The inbox insert is attempted first so a duplicate-key error occurs before aggregate mutation. Any other failure rolls back the entire transaction.

## Query
Select daily rows by authenticated employee ID and bounded date range. The API derives employeeId from JWT `sub`; clients cannot query another employee's partition.

## RabbitMQ topology
Add a distinct durable Insight activity queue and DLQ. Bind only `habit.activity.recorded` and `challenge.activity.recorded`. Use quorum queue arguments and the repository's existing at-least-once dead-letter conventions. Do not repurpose lifecycle queue or alter existing queue arguments.

## Verification
Unit tests cover event validation and category mapping. MySQL integration tests cover transaction, duplicate, concurrent increments and date queries. API integration tests cover authentication, subject and range. RabbitMQ tests cover ack, retry, DLQ and redelivery. Topology contract tests assert exact new queue/bindings and unchanged lifecycle bindings.
