# Specification: Identity EventBridge Fan-out Infrastructure

**Status:** CloudFormation template and static lint workflow added; AWS deployment and live delivery verification remain blocked on environment access and consumer readiness.  
**Authority:** AGENTS.md, ADR-0001, ADR-0002, service ownership, communication conventions, Identity lifecycle event envelope and employee-status projection contract.

## Resource contract

| Resource | Required behavior |
|---|---|
| Custom event bus | Name is a required deployment parameter; no physical bus name or account ARN is hard-coded. |
| Identity IAM attachment | Attach an inline policy to an existing same-account workload role. Permit only `events:PutEvents` on the created bus ARN. Do not create or alter the role trust policy. |
| Lifecycle rule | Match source `teampulse.identity` and use EventBridge `$or` branches so outer `detail-type` and inner `detail.eventType` must be the same approved event type, with numeric `detail.schemaVersion=1`. |
| Projection queues | Exactly five standard SQS queues, owned operationally by Habit, Challenge, Event Tracking, Insight and Notification. |
| Target-delivery DLQs | One standard SQS DLQ per EventBridge target. These capture target delivery failures after bus acceptance only. |
| Queue resource policies | Permit `events.amazonaws.com:sqs:SendMessage` only when `aws:SourceArn` is the exact lifecycle rule ARN and `aws:SourceAccount` is the current account. |
| Retry | EventBridge target maximum age and attempts are parameters; defaults mirror AWS EventBridge defaults (86,400 seconds and 185 attempts). |
| Encryption | SQS-managed server-side encryption on every target and DLQ queue. |
| Retention | Target and DLQ retention are parameters. Queue/DLQ resources are retained on stack deletion/replacement. |
| Alarms | Alarm on any visible message in each target DLQ, target queue oldest-message age above a required operator-supplied threshold, and EventBridge failures to write to a target DLQ. |
| Alarm actions | Optional existing same-account/same-region SNS topic. Without it, alarms exist without notification actions. |
| Consumer access | This stack does not grant receive/delete permissions. Each service's own deployment owns its consumer role policy. |

## Message and delivery contract

EventBridge targets receive the default outer EventBridge event object in the SQS message body. The full TeamPulse schema-v1 envelope is in the outer event's `detail`; no input transformer or field projection is configured. Consumers must parse that envelope, deduplicate by `eventId`, and apply employee status only when `lifecycleVersion` is strictly newer than the local projection.

SQS standard queues and EventBridge target delivery are at-least-once. Do not assume ordering or exactly-once processing.

## Security and ownership

- The stack is deployed in the same account/region as the Identity workload role and target queues.
- Identity retains ownership of the producer and its MySQL outbox.
- Each target queue is assigned to exactly one existing consumer service. No service reads another service's database.
- Queue policies are resource-based and restricted to the exact rule ARN/account.
- Identity's inline IAM policy is restricted to the custom bus ARN; no wildcard actions/resources or long-lived access keys.
- No consumer receive permission, cross-account principal, role trust policy or service database is modified by this stack.

## Observability boundary

The template provisions AWS-managed delivery alarms for queue age, target DLQ depth and EventBridge inability to write to a DLQ. The Identity worker currently exposes .NET `System.Diagnostics.Metrics` instruments only; no CloudWatch exporter or log metric contract is present. Do not create producer alarms for oldest outbox age, quarantined rows or systemic publisher failures until the telemetry export path, namespace, dimensions and thresholds are approved and implemented.

## Acceptance criteria and verification

| ID | Acceptance criterion | Verification |
|---|---|---|
| IEF-01 | Bus name, environment, existing role and queue-age SLO are parameterized. | cfn-lint and template review. |
| IEF-02 | Identity permission is only `events:PutEvents` on the bus ARN. | cfn-lint and IAM policy inspection. |
| IEF-03 | Rule matches only paired outer/inner approved Identity lifecycle event types and schema version 1. | Python template contract test, EventBridge event-pattern review and non-production event test. |
| IEF-04 | Exactly five service-owned target queues and five distinct target-delivery DLQs exist. | cfn-lint and CloudFormation resource inspection. |
| IEF-05 | Each queue/DLQ policy is limited to the rule ARN and source account. | Template/IAM policy inspection and non-production delivery test. |
| IEF-06 | Target retry settings are bounded and parameterized; queues use SSE-SQS. | cfn-lint and stack resource inspection. |
| IEF-07 | Target queue age, DLQ depth and DLQ-write failure alarms exist. | cfn-lint and CloudWatch alarm inspection. |
| IEF-08 | Alarm actions are attached only when an existing SNS topic parameter is supplied. | cfn-lint and stack change-set review. |
| IEF-09 | Consumer read permissions remain service-owned; no role trust or database ownership changes. | Template review. |
| IEF-10 | Static template lint and resource/security contract tests run for infrastructure changes. | GitHub Actions cfn-lint and Python unittest workflow. |
| IEF-11 | Dispatcher remains disabled until non-production deployment and consumer readiness checks complete. | Deployment configuration/release checklist. |
| IEF-12 | No producer outbox alarms are created against unexported .NET Meter instruments. | Telemetry/observability review. |

## Deferred

Live CloudFormation deployment, IAM simulation, end-to-end event delivery, consumer role policies and consumer projection implementations require separate service/environment work. Producer-side outbox alarms require a telemetry exporter change.
