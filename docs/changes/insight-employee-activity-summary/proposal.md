# Proposal: Employee Activity Summary Insights

## Why
Employees can record personal habit activity and challenge activity, but the Insight service currently only maintains employee lifecycle status. The activity events are already published by Event Tracking; this change proposes a minimal employee-private read model so employees can understand their recent consistency without exposing individual activity to managers or HR.

## Proposed first increment
- Consume schema-v1 `HabitActivityRecorded` and `ChallengeActivityRecorded` events from the existing RabbitMQ domain-event exchange using a dedicated Insight activity queue.
- Maintain daily counts of accepted activity submissions, separated into habit and challenge categories, per employee and activity date.
- Expose an authenticated, employee-self-only API for a bounded date range (default recent 7 days; maximum 30 days).
- Use Insight-owned DynamoDB derived data only; Event Tracking remains the source of truth and Challenge remains authoritative for challenge goals/progress.
- Keep the existing employee lifecycle consumer and its queue unchanged.
- Add isolated tests for validation, event idempotency, aggregation concurrency, date-range authorization and RabbitMQ acknowledgement/DLQ behavior.

## Explicit non-goals
No streaks, rewards, goal recommendations, manager/HR analytics, cross-employee/team aggregation, habit names/details, challenge scoring, data correction/replay UI, AWS provisioning, or EventBridge/SQS adapter.

## Proposed acceptance
A duplicate event increments no count; each valid unique activity event increments exactly one employee/day/category count; employees can read only their own aggregate; malformed or unsupported events are dead-lettered; persistence and inbox commit atomically before ack; all existing Insight lifecycle behavior remains unchanged.

## Approval needed
This is a new business/read-model slice beyond the approved lifecycle projection spec. Please approve or revise the proposed metrics and self-only API before implementation.
