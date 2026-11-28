# Proposal: Insight Employee Status Projection Consumer

## Context
Identity is the source of truth for employee lifecycle state. Insight consumes lifecycle events to maintain its own minimal employee-status projection. The employee activity summary and lifecycle projection are both owned by Insight and persisted in its existing MySQL database.

## Scope
- Keep the lifecycle event contract and transport-independent application processor.
- Persist the employee status projection and event inbox in Insight MySQL.
- Commit inbox deduplication and projection changes atomically.
- Preserve manual acknowledgement, retry, dead-letter, health and reconnect behavior.
- Remove the DynamoDB dependency and DynamoDB Local setup from Insight.

## Non-goals
- EventBridge/SQS deployment, AWS credentials or cloud provisioning.
- Changes to lifecycle event semantics or other services' projections.
- Employee profile, email, credentials, activity details or wellness data in the lifecycle projection.

## Acceptance
Duplicate events are idempotent; stale/equal lifecycle versions cannot regress state; concurrent deliveries preserve the maximum version; the event inbox and projection are transactionally consistent; Insight can run locally with MySQL and RabbitMQ only.
