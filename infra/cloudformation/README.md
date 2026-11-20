# Identity EventBridge fan-out stack

This stack provisions the AWS transport boundary for Identity employee lifecycle events. It is infrastructure for the existing Identity producer and five existing consumer services; it does not create a seventh microservice.

## Resources

- A parameter-named custom EventBridge bus.
- An inline IAM policy attached to an **existing** Identity workload role. Its only action is `events:PutEvents`, scoped to the bus ARN. The compute deployment remains responsible for the role trust policy.
- One rule matching source `teampulse.identity`, detail types `EmployeeDeactivated` / `EmployeeReactivated`, and schema version 1.
- Five standard, SSE-SQS encrypted projection queues: Habit, Challenge, Event Tracking, Insight and Notification.
- One EventBridge target-delivery DLQ for each projection queue, with resource policies restricted to the exact rule ARN and source account.
- CloudWatch alarms for each DLQ depth, each projection queue's oldest-message age, and EventBridge failures to write to a target DLQ.
- Queue and DLQ resources use `DeletionPolicy: Retain` and `UpdateReplacePolicy: Retain` to avoid silently deleting undelivered employee lifecycle messages. Stack removal therefore requires deliberate queue cleanup.

The stack does **not** grant consumer receive/delete permissions. Each service's own workload stack must grant only its own role the required SQS receive permissions. It also does not create consumer-processing DLQs; those are separate from EventBridge target-delivery DLQs and remain owned by each consumer service.

## Required parameters

| Parameter | Purpose |
|---|---|
| `EnvironmentName` | Short environment identifier. |
| `EventBusName` | Explicit custom bus name; no account-specific name is hard-coded. |
| `IdentityWorkloadRoleName` | Existing same-account Identity ECS task or instance role. |
| `TargetQueueAgeThresholdSeconds` | Operator-approved SQS oldest-message age threshold; intentionally has no default because no queue SLO is approved. |

Other parameters have documented defaults: `ResourceNamePrefix=teampulse`, EventBridge target retry age 86,400 seconds and attempts 185, target queue retention 345,600 seconds, and target DLQ retention 1,209,600 seconds. The EventBridge retry defaults mirror the service's documented defaults; tune them through deployment parameters when an approved delivery SLO exists.

`AlarmNotificationTopicArn` is optional. When supplied, it must be an existing same-account, same-region SNS topic ARN and is attached to alarm and OK actions. If omitted, alarms are created without notification actions; production deployment should supply the approved operations topic.

## Static validation

The repository workflow `.github/workflows/cloudformation.yml` installs `cfn-lint` and validates the template on changes to `infra/cloudformation/**`.

Run locally when Python is available:

```bash
python -m pip install cfn-lint
cfn-lint --non-zero-exit-code warning infra/cloudformation/identity-eventbridge.yaml
```

This is static template validation. It does not replace AWS CloudFormation change-set review, deployment, IAM simulation or a non-production delivery test.

## Deploy to a non-production account

Use an AWS profile/role with permission to create the listed resources and attach an inline policy to the existing Identity workload role. Deploy in the same account and region as that role. Set `AWS_REGION` and the parameter values from the target environment; do not commit credentials or account-specific ARNs.

Bash example (the variables must be supplied by the operator):

```bash
: "${AWS_REGION:?Set AWS_REGION}"
: "${ENVIRONMENT_NAME:?Set ENVIRONMENT_NAME}"
: "${EVENT_BUS_NAME:?Set EVENT_BUS_NAME}"
: "${IDENTITY_WORKLOAD_ROLE_NAME:?Set IDENTITY_WORKLOAD_ROLE_NAME}"
: "${TARGET_QUEUE_AGE_THRESHOLD_SECONDS:?Set the approved queue-age SLO}"

PARAMETERS=(
  "EnvironmentName=${ENVIRONMENT_NAME}"
  "EventBusName=${EVENT_BUS_NAME}"
  "IdentityWorkloadRoleName=${IDENTITY_WORKLOAD_ROLE_NAME}"
  "TargetQueueAgeThresholdSeconds=${TARGET_QUEUE_AGE_THRESHOLD_SECONDS}"
)

if [[ -n "${ALARM_NOTIFICATION_TOPIC_ARN:-}" ]]; then
  PARAMETERS+=("AlarmNotificationTopicArn=${ALARM_NOTIFICATION_TOPIC_ARN}")
fi

aws cloudformation deploy \
  --region "${AWS_REGION}" \
  --stack-name "teampulse-identity-events-${ENVIRONMENT_NAME}" \
  --template-file infra/cloudformation/identity-eventbridge.yaml \
  --capabilities CAPABILITY_IAM \
  --parameter-overrides "${PARAMETERS[@]}"
```

Review the CloudFormation change set before execution in controlled environments. Capture stack outputs `EventBusName`, `EventBusArn`, queue URLs/ARNs and rule ARN in protected deployment configuration.

## Application configuration and rollout gate

The Identity API resolves the bus from `EventBridge__BusName` and the region from `AWS_REGION` (or `AWS:Region`). The AWS SDK default credential chain uses the workload role. Do not configure long-lived access keys.

Keep `OutboxDispatcher__Enabled=false` during infrastructure deployment and review. Before enabling it even in non-production:

1. Verify the stack and IAM policy are in the intended account and region.
2. Verify the Identity workload can call `PutEvents` on only this bus.
3. Verify each queue and its EventBridge target-delivery DLQ policy names the exact rule ARN and account.
4. Send a non-sensitive schema-v1 lifecycle test event and verify delivery to all five queues.
5. Verify forced target-delivery failure reaches only the corresponding target DLQ and the DLQ alarm fires.
6. Confirm each receiving service has its queue consumer, idempotent `eventId` handling, strictly increasing `lifecycleVersion` handling and service-owned SQS permissions.

Do not enable the production dispatcher until all five consumer projections and operational ownership are deployed. SQS standard queues and EventBridge provide at-least-once delivery; consumers must deduplicate and tolerate out-of-order messages.

EventBridge sends its standard outer event object to each SQS target. The complete TeamPulse schema-v1 envelope is in the outer event's `detail` field. No input transformer is configured.

## Observability limitation

This stack alarms on AWS-managed target-delivery signals: non-empty target DLQs, projection queue age, and EventBridge inability to write to a target DLQ. The Identity worker currently emits .NET `System.Diagnostics.Metrics` instruments, but the API has no CloudWatch/OpenTelemetry exporter. Therefore this stack does not yet create alarms for producer outbox oldest-pending age, quarantined rows or systemic `PutEvents` failures. Those require a separately approved telemetry export path, metric namespace and operational thresholds; do not create alarms against metrics that are not exported.
