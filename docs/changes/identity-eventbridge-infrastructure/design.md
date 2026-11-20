# Design: Identity EventBridge Fan-out Infrastructure

## Resource graph

The stack creates one custom EventBridge bus and one enabled rule. The rule has exactly five SQS targets, one for each existing receiving service. Each target has a separate EventBridge target-delivery DLQ. The rule is created only after all five queue policies exist.

Queue policies compute the exact custom-bus rule ARN from the same required name parameters rather than referencing the rule resource. This avoids a CloudFormation dependency cycle and lets the rule depend on the queue policies, preventing an enabled rule from becoming active before its resource policies are installed.

## Identity publisher permission

The stack accepts the name of an existing Identity workload role and attaches one inline policy. The policy contains only `events:PutEvents` and the exact EventBridge bus ARN. The compute platform owns the role's trust policy and task/instance attachment. No IAM user, access key, wildcard resource or cross-account access is introduced.

## Event routing

The rule matches source `teampulse.identity` and uses two `$or` branches so `detail-type=EmployeeDeactivated` is paired only with `detail.eventType=EmployeeDeactivated`, and the corresponding Reactivated fields are paired in the other branch. Both require numeric `detail.schemaVersion=1`.

Each SQS target receives the default full EventBridge event JSON. The TeamPulse envelope is preserved in `detail`. No `Input`, `InputPath` or `InputTransformer` is used, so event ID, correlation ID, timestamp and payload remain available to consumers.

## Queues and failure boundaries

All ten queues are standard SQS queues with SSE-SQS. Target queues are owned by their named receiving services. Their receive/delete IAM policies are deliberately excluded and must be created by the corresponding service deployment.

Each EventBridge target has a separate SQS DLQ. EventBridge retry age/attempt limits and queue retention are configurable. The EventBridge DLQ captures delivery failure after an event has been accepted by the bus; it does not replace Identity's producer-side MySQL outbox retry/quarantine or a consumer's poison-message DLQ.

Queue/DLQ policies grant `sqs:SendMessage` to `events.amazonaws.com` only for the exact rule ARN and current AWS account. Queue/DLQ resources are retained on stack deletion/replacement so undelivered messages are not silently removed.

## Alarms

The stack creates one DLQ visible-message alarm per target, one oldest-message-age alarm per target queue using a required operator-supplied threshold, and one EventBridge `InvocationsFailedToBeSentToDlq` alarm scoped to the custom bus and lifecycle rule.

An optional existing SNS topic attaches alarm and OK actions. Without the topic, CloudWatch alarms still exist but have no notification action. Production deployment must supply the approved operations topic.

The Identity worker's current `System.Diagnostics.Metrics` instruments are not exported to CloudWatch. Therefore pending outbox age, quarantine count and systemic publisher failure alarms are deferred until a telemetry exporter and metric contract are approved. The stack does not create misleading alarms for metrics that are not emitted.

## Deployment order

1. Review parameters, IAM attachment and queue retention/deletion behavior.
2. Deploy the stack in the same account/region as the existing Identity role.
3. Verify the role can call `PutEvents` on only the created bus.
4. Deploy each consumer's queue receive policy and idempotent status projection.
5. In non-production, publish a non-sensitive schema-v1 event and verify all five target queues and a forced target DLQ failure.
6. Verify queue/DLQ/EventBridge alarms and alarm routing.
7. Only then enable the dispatcher in non-production. Production enablement requires a separate release review and all consumer projections operational.

## Validation

The repository workflow runs `cfn-lint` and Python contract tests for resource counts, exact event-type pairing, least-privilege policies and alarm coverage. Static validation does not prove the account's existing role name/trust, permissions, region availability, CloudFormation change-set behavior or live target delivery. Those require an authorized non-production AWS deployment.
