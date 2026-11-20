# Proposal: Identity EventBridge Fan-out Infrastructure

## Summary

Add a parameterized AWS CloudFormation stack for the existing Identity employee-lifecycle EventBridge producer. The stack creates a custom event bus, attaches least-privilege `events:PutEvents` permission to an existing Identity workload role, routes the two approved schema-v1 lifecycle events to the five existing service-owned SQS projection queues, configures one target-delivery DLQ per queue, and provisions AWS-managed delivery alarms.

## Motivation

The Identity outbox dispatcher is implemented and CI-verified, but no deployment infrastructure exists in the repository. Enabling the dispatcher without a bus, scoped IAM permission, target queues, DLQs and monitoring would leave downstream lifecycle projections undelivered or unobservable.

## Scope

- Parameterized CloudFormation template with no account IDs, role names, physical bus names, credentials or SNS topic ARNs embedded.
- Existing Identity workload-role inline policy scoped to the created bus ARN.
- One lifecycle event rule and five service-owned SQS target queues.
- Per-target EventBridge delivery DLQs and queue policies restricted by rule ARN and source account.
- CloudWatch alarms for target queue age, target DLQ depth and EventBridge DLQ-write failures.
- Optional existing SNS topic for alarm/OK actions.
- Static CloudFormation lint workflow and deployment/rollout documentation.
- SDD and architecture/event contract updates.

## Non-goals

- Deploying or enabling the stack in an AWS account during this increment.
- Creating the Identity compute role or choosing its trust relationship.
- Granting consumer read permissions or implementing any of the five queue consumers.
- Creating consumer-processing DLQs, cross-account routing, event archive/replay, global JWT revocation or an additional microservice.
- Creating alarms for producer outbox metrics until an exporter and CloudWatch metric contract are approved.

## Risks and safeguards

- A target-delivery DLQ is distinct from Identity's MySQL outbox quarantine and from a consumer's poison-message DLQ.
- Standard SQS queues are at-least-once and unordered; consumers must deduplicate by `eventId` and apply only strictly newer `lifecycleVersion` values.
- Queue resources are retained on stack deletion/replacement to avoid silent loss of undelivered messages.
- The dispatcher remains disabled until a non-production change-set review and end-to-end delivery verification succeed.
