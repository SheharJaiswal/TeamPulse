# Proposal: Insight Employee Status Projection Consumer

**Status:** Approved by project owner on 2026-11-21.

## Context
Identity owns authoritative employee lifecycle state and publishes schema-v1 EmployeeDeactivated and EmployeeReactivated events. The shared Employee Status Projection Contract v1 requires Insight to maintain its own version-aware projection; it must not query another service's database.

Insight is one of the six defined services and owns aggregated/derived read models only. The repository currently has the durable local RabbitMQ queue teampulse.insight.events, but no Insight service projects or implementation. The architecture reserves DynamoDB for Insight read models. Other downstream consumers provide validation, idempotency, acknowledgement, retry, health and DLQ patterns, but Insight must use DynamoDB rather than copy their MySQL persistence.

## Goal
Create the Insight service foundation needed to consume Identity lifecycle events locally and maintain a minimal employee-status projection plus processed-event inbox in DynamoDB. Keep the application processor transport-independent so a future SQS adapter can reuse it when owner-managed AWS infrastructure is provisioned.

## Scope
- Add Insight.API, Insight.Application, Insight.Domain, Insight.Infrastructure and Insight.UnitTests to the existing solution.
- Use AWS SDK for .NET DynamoDB APIs behind Insight-owned persistence abstractions; do not use EF Core/MySQL for Insight lifecycle state.
- Define one Insight-owned DynamoDB table for projection and inbox items, with conditional transactional writes to atomically record event processing and apply strictly newer lifecycle versions.
- Add DynamoDB Local for local execution and integration tests, without an AWS account, AWS credentials or cloud resources.
- Validate schema-v1 lifecycle envelopes and persist only approved minimal projection/inbox fields.
- Add an opt-in RabbitMQ consumer for the existing Insight queue, manual acknowledgements, bounded retries/reconnect, separate consumer health and an Insight-owned DLQ.
- Upgrade only Insight's local RabbitMQ topology after a pending-message-preserving migration plan is approved.
- Add unit, DynamoDB Local, RabbitMQ Testcontainers and topology tests, plus local run and broker migration documentation.

## Non-goals
- Dashboards, cohort metrics, analytics queries, aggregation pipelines or employee/manager read APIs.
- Recomputing, deleting or changing derived metrics on employee deactivation/reactivation.
- Employee profile, tenant/team/role, credential or source-service replication.
- Cross-service database access; MySQL/EF Core persistence in Insight.
- SQS/EventBridge consumer deployment, AWS credentials, IAM, CloudFormation changes or cloud provisioning.
- Inbox expiry/cleanup, retention policy or global JWT revocation.
- Changes to any other service's queue, database or behavior.

## Proposed technical decisions for approval
1. Use one DynamoDB table per Insight deployment with composite string PK/SK. Projection items use PK=EMPLOYEE#<uuid>, SK=STATUS; inbox items use PK=EVENT#<uuid>, SK=INBOX.
2. Use DynamoDB TransactWriteItems to atomically put the inbox item and either update a newer projection or condition-check an already-newer/equal projection for a stale event. Strongly consistent reads and optimistic version conditions handle concurrent projection races.
3. Use DynamoDB Local for local execution and tests. Production endpoint, table provisioning, encryption, IAM and backup settings remain deployment-owned and deferred.
4. Keep the consumer disabled by default. Local RabbitMQ is development/test only; EventBridge/SQS remains the intended production fan-out path.
5. Keep inbox records without TTL/automatic deletion pending the existing privacy/legal retention review.

## Approval boundary
The event/projection contracts and Insight's DynamoDB ownership are established. This SDD defines only the technical projection foundation and introduces no analytics/product behavior. Owner approval of proposal.md, spec.md, design.md and tasks.md is required before implementation.
