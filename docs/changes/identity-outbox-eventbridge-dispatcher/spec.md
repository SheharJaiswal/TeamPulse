# Specification: Lease-Safe Identity Outbox Dispatcher

**Status:** Ready for implementation; production activation requires external infrastructure.  
**Authority:** AGENTS.md, ADR-0001, ADR-0002, `docs/architecture/communication.md`, Identity service ownership and the shared domain-event envelope.

## Runtime configuration

| Setting | Required behavior |
|---|---|
| `OutboxDispatcher:Enabled` | Defaults to `false`. No EventBridge client or worker polling is required while disabled. |
| `EventBridge:BusName` | Bound from `EventBridge__BusName`; required and non-whitespace when the worker is enabled. No physical bus name is committed. |
| AWS region | Resolved from `AWS_REGION` / standard AWS SDK region configuration; required when enabled. |
| `EventBridge:Source` | Fixed producer identity `teampulse.identity`; not caller-controlled. |
| AWS credentials | AWS SDK default credential chain. Use the deployed workload role; do not configure long-lived access keys. |

The implementation must validate enabled configuration at startup. A disabled local development instance must continue to run without AWS credentials or an EventBridge bus.

## EventBridge entry contract

For every outbox row, create one EventBridge entry:

| EventBridge field | Value |
|---|---|
| `EventBusName` | Configured bus name |
| `Source` | `teampulse.identity` |
| `DetailType` | Exact outbox `EventType` (`EmployeeDeactivated` or `EmployeeReactivated`) |
| `Time` | Outbox `OccurredAtUtc` |
| `Detail` | The complete serialized schema-v1 envelope already stored in `Payload` |

Do not wrap the envelope a second time, rename its fields, or create a transport-specific event ID. The envelope `eventId` remains the stable deduplication key. The event payload must not be logged.

EventBridge `PutEvents` accepts at most 10 entries per request. The dispatcher may batch up to 10 claimed rows, but must inspect each response entry independently and preserve request/response ordering. A successful sibling entry must not be retried merely because another entry in the same batch failed.

An entry is accepted only when the corresponding response entry has a non-empty EventBridge `EventId` and no `ErrorCode`. Treat entry-level errors and the `FailedEntryCount` as authoritative even when the HTTP request itself succeeded.

## Outbox persistence contract

Extend the Identity-owned `outbox_messages` table with nullable dispatch metadata:

- `LeaseToken` (UUID/char(36)).
- `LeaseExpiresAtUtc` (UTC datetime).
- `NextAttemptAtUtc` (UTC datetime).
- `LastAttemptAtUtc` (UTC datetime).
- `DeadLetteredAtUtc` (UTC datetime).
- `LastErrorCode` (bounded, sanitized code; never a raw exception or event payload).

Retain the existing `AttemptCount` and `PublishedAtUtc`. Add a composite dispatch-queue index that supports pending/unquarantined rows ordered by `CreatedAtUtc`, with due retry and expired-lease predicates. Use a checked-in EF Core migration and update the model snapshot.

A row is dispatchable only when:
- `PublishedAtUtc IS NULL`;
- `DeadLetteredAtUtc IS NULL`;
- `NextAttemptAtUtc IS NULL OR NextAttemptAtUtc <= now`; and
- `LeaseExpiresAtUtc IS NULL OR LeaseExpiresAtUtc <= now`.

## Claiming and concurrency

1. Open a short MySQL InnoDB transaction at Read Committed isolation.
2. Select due rows ordered by `CreatedAtUtc` and `EventId`, bounded by the configured batch size (maximum 10), using `FOR UPDATE SKIP LOCKED`.
3. Assign a fresh lease token per row, set `LeaseExpiresAtUtc`, update `LastAttemptAtUtc`, and increment `AttemptCount`.
4. Commit the transaction before any AWS call.
5. Publish outside the database transaction.
6. Apply each outcome with a conditional update requiring the same `EventId` and `LeaseToken`. A worker that lost its lease must not update the row.

Recommended initial defaults: poll interval 2 seconds, batch size 10, lease duration 60 seconds, maximum attempts 10, base retry delay 1 second and maximum retry delay 5 minutes. Validate all values and keep them configurable; batch size must never exceed the EventBridge API limit. The lease must exceed the configured AWS request timeout plus local acknowledgement margin.

Expired leases become eligible for another worker. This can result in duplicate publication if the prior worker reached EventBridge but failed before recording acknowledgement; this is an accepted at-least-once outcome.

## Success acknowledgement

After an individual EventBridge entry is accepted, set `PublishedAtUtc` to the current UTC time and clear its lease/retry/error metadata, using a conditional update on the lease token.

Never set `PublishedAtUtc`:
- when a row is merely claimed;
- before or during the AWS request;
- because the HTTP response was successful when that entry contains an error; or
- because another entry in the same batch was accepted.

`PublishedAtUtc` records EventBridge bus acceptance only. It does not assert that EventBridge rules matched, targets received the event, or consumers applied their projections.

## Retry, quarantine and failure classification

- Transient failures (throttling, internal service failures, network errors, timeouts and retryable 5xx responses) are rescheduled with exponential backoff and full jitter, starting at 1 second and capped at 5 minutes.
- After 10 dispatch attempts, a transiently failing row is quarantined by setting `DeadLetteredAtUtc`, clearing its lease and retaining a sanitized `LastErrorCode`. Attempts and delays are configurable.
- Permanent event-specific failures (for example malformed/oversized detail rejected for that entry) are quarantined immediately. Preserve the original envelope for investigation; do not delete or mutate it.
- Systemic failures such as missing bus, missing `events:PutEvents` permission or invalid runtime region must pause the worker and raise an operational error. Do not mark every pending event dead-lettered because of a deployment-wide configuration problem.
- Cancellation and process termination must not mark a row published. The lease expires and another worker may retry.
- The initial implementation does not provide an HTTP redrive endpoint. Quarantined records remain in the Identity database for controlled operator recovery; any automated redrive tool requires its own authorization, audit and SDD.

## DLQ boundary

The producer-side outbox quarantine handles a message that cannot be accepted by EventBridge. It is not an SQS queue.

When downstream EventBridge rules and targets are provisioned, each retryable target delivery must use an SQS DLQ as appropriate for that target. EventBridge target retries/DLQ capture failures after bus acceptance; they do not capture failures from the Identity `PutEvents` call. Downstream queues, target policies and DLQs are not implemented by this change.

## IAM and security

The deployed Identity workload role receives `events:PutEvents` on the exact configured event-bus ARN only. No `events:*`, wildcard resource, IAM user access key or secret embedded in appsettings is permitted. The publisher must not expose raw AWS exception messages, credentials, event payloads or HR notes in logs.

## Observability

Emit structured logs and metrics for:
- pending outbox count and oldest pending age;
- claimed, accepted, retried and quarantined event counts;
- expired leases and lease conflicts;
- PutEvents latency and failed entry count;
- systemic publisher/configuration failures.

Log event ID, event type, correlation ID, attempt count, bus name, AWS request ID and sanitized error code. Never log `Detail`, the payload, reason note, credentials or tokens. Provision CloudWatch alarms for sustained pending age, any quarantined event and systemic EventBridge/IAM failures in the deployment layer. Thresholds must be selected with the deployment SLO; this repository has no IaC or monitoring baseline yet.

## Acceptance criteria and test mapping

| ID | Acceptance criterion | Verification |
|---|---|---|
| OBD-01 | Dispatcher is disabled by default and the API starts without AWS credentials. | API configuration/startup tests. |
| OBD-02 | Enabling the worker requires a bus name and AWS region; invalid enabled configuration fails fast. | Options validation tests. |
| OBD-03 | EventBridge mapping preserves the complete envelope and uses the agreed source, detail type and event time. | Publisher unit tests with a fake/mock AWS client. |
| OBD-04 | Batches never exceed 10 and partial response failures affect only their matching outbox rows. | Publisher/dispatcher unit tests. |
| OBD-05 | Concurrent workers claim disjoint rows using MySQL `SKIP LOCKED`. | MySQL 8.4 Testcontainers concurrency test. |
| OBD-06 | A stale or expired lease holder cannot acknowledge a row claimed by another worker. | MySQL Testcontainers lease-token test. |
| OBD-07 | Only an individually accepted EventBridge entry sets `PublishedAtUtc`. | Success, partial-failure and exception tests. |
| OBD-08 | Transient failures use bounded jittered backoff; permanent/exhausted event failures are quarantined. | Deterministic retry-policy and MySQL persistence tests. |
| OBD-09 | Systemic bus/IAM failures pause dispatch and do not quarantine all pending rows. | Failure-classification tests. |
| OBD-10 | Crash after bus acceptance but before DB acknowledgement can replay the same event ID and is documented as at-least-once. | Dispatcher recovery test and consumer contract. |
| OBD-11 | Logs/metrics do not include payload, note, token or credential data. | Structured logging tests/review. |
| OBD-12 | EF migration is checked in and startup migration behavior remains consistent with ADR-0001. | MySQL clean-database integration test and CI. |

## Deferred work

EventBridge bus/rules, workload IAM, SQS target queues and DLQs, CloudWatch alarms, consumer projections, global revocation, operator redrive tooling and live AWS deployment verification remain separate infrastructure or service increments.
