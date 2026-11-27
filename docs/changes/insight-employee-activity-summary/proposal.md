# Proposal: Employee Activity Summary Insights

## Why
Employees can record personal habit activity and challenge activity, but Insight currently maintains employee lifecycle status. This increment adds a minimal employee-private read model so employees can understand recent consistency without exposing activity details to managers or HR.

## Approved first increment
- Consume schema-v1 `HabitActivityRecorded` and `ChallengeActivityRecorded` events from the existing RabbitMQ domain-event exchange using a dedicated Insight activity queue.
- Maintain daily counts of accepted activity submissions, separated into habit and challenge categories, per employee and activity date.
- Expose an authenticated, employee-self-only API for a bounded date range (default recent 7 days; maximum 30 days).
- Use Insight-owned RDS MySQL derived data; Event Tracking remains the source of truth and Challenge remains authoritative for challenge goals/progress.
- Keep the existing employee lifecycle consumer and its queue unchanged.
- Process each event using a SQL transaction that inserts the inbox event ID and increments the aggregate atomically.

## Explicit non-goals
No streaks, rewards, goal recommendations, manager/HR analytics, cross-employee/team aggregation, habit names/details, challenge scoring, data correction/replay UI, AWS provisioning, or EventBridge/SQS adapter.

## Acceptance
A duplicate event increments no count; each valid unique activity event increments exactly one employee/day/category count; employees can read only their own aggregate; malformed or unsupported events are dead-lettered; persistence and inbox commit atomically before ack; all existing Insight lifecycle behavior remains unchanged.

## Storage decision
Use MySQL because TeamPulse already uses MySQL for local development and relational reporting. Daily aggregate has composite primary key `(EmployeeId, ActivityDate)`; inbox has primary key `EventId`. The aggregate and inbox are committed in one database transaction.
