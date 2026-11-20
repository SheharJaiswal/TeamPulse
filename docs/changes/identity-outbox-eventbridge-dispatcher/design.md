# Design: Lease-Safe Identity Outbox Dispatcher

## Service boundary and hosting

Keep the dispatcher inside the existing Identity service. Do not add a seventh service or let any other service query the Identity database. The Identity API hosts a .NET BackgroundService when `OutboxDispatcher:Enabled` is true. The worker shares the existing IdentityDbContext factory/scope pattern but creates a fresh DbContext for each claim/acknowledgement operation.

The worker is disabled by default. Local API startup and the existing migration flow must remain functional without AWS SDK credentials or an EventBridge bus. When enabled, options are validated at startup before the host accepts traffic.

## Components

- **Application:** define an outbox dispatch contract and an EventBridge publisher abstraction. The application layer owns orchestration, event-result classification and retry policy; it does not reference AWS SDK types.
- **Infrastructure persistence:** implement claim, acknowledge, reschedule and quarantine operations using IdentityDbContext and MySQL.
- **Infrastructure transport:** implement the publisher with the AWS SDK for .NET EventBridge client. Translate application event data into PutEvents entries and translate each response entry into a transport-neutral result.
- **API hosting:** register the BackgroundService, options validation, AWS client and metrics only when enabled. Do not execute dispatch inside lifecycle controllers.
- **Domain:** extend OutboxMessage with lease, retry and quarantine state transitions that enforce valid state changes.

## Database claim algorithm

Use MySQL 8.4/InnoDB row locking. In one Read Committed transaction:

```sql
SELECT EventId
FROM outbox_messages
WHERE PublishedAtUtc IS NULL
  AND DeadLetteredAtUtc IS NULL
  AND (NextAttemptAtUtc IS NULL OR NextAttemptAtUtc <= @nowUtc)
  AND (LeaseExpiresAtUtc IS NULL OR LeaseExpiresAtUtc <= @nowUtc)
ORDER BY CreatedAtUtc, EventId
LIMIT @batchSize
FOR UPDATE SKIP LOCKED;
```

For each selected row, assign a fresh UUID lease token, set lease expiry and last-attempt time, and increment AttemptCount. Load the immutable event fields/payload while the rows are locked, save and commit. No transaction or row lock remains open while calling AWS.

An index should begin with the state/due-time columns used by the pending predicate and include CreatedAtUtc for ordered selection. Confirm the final index with the generated migration and MySQL integration tests rather than relying on an in-memory provider.

## Publish and acknowledge flow

1. Map each row to one PutEvents entry: configured bus, fixed source `teampulse.identity`, exact event type as detail type, occurrence time as EventBridge Time, and the complete stored envelope as Detail.
2. Send up to 10 entries. Use the AWS SDK's standard retry behavior for an individual API request; application retry state handles a final unsuccessful entry/request.
3. Match response entries to request entries by array position. A response entry with EventId and no ErrorCode is accepted. Handle a non-zero FailedEntryCount and individual errors even when the HTTP call returned normally.
4. For each accepted row, conditionally set PublishedAtUtc and clear lease/retry/error fields only if its lease token still matches.
5. For each failed row, conditionally persist a retry time or quarantine state using the same lease token.
6. If the lease token no longer matches, do not overwrite the newer owner's state. The other worker will complete or the lease will expire.

The database acknowledgement is intentionally after EventBridge acceptance. If the process terminates between steps 3 and 4, the same envelope/eventId is sent again after lease expiry. That duplicate is not a correctness failure; consumer idempotency is mandatory.

## Retry policy

Use an injectable clock and jitter source so retry calculations are deterministic in tests. For transient failures, compute a full-jitter delay in the range `[0, min(baseDelay * 2^(attempt-1), maxDelay)]`. Initial defaults are base 1 second, cap 5 minutes and maximum 10 attempts.

Permanent event-specific errors are quarantined immediately. A systemic bus/region/IAM failure pauses the worker and emits an operational failure signal; it must not quarantine every pending event. Store only a sanitized error code, not raw exception text or request payload.

The existing `AttemptCount` is incremented on claim. It therefore counts dispatch claims, including a worker crash before the network call. This bounded behavior prevents repeated crashes from leasing the same poison row forever. Keep this meaning documented and tested.

## Outbox state transitions

- **Pending -> Leased:** eligible row claimed; token/expiry assigned and attempt count incremented.
- **Leased -> Published:** EventBridge entry accepted and matching lease token acknowledged; PublishedAtUtc written.
- **Leased -> Pending:** transient failure scheduled for retry; lease cleared and NextAttemptAtUtc set.
- **Leased -> Quarantined:** permanent event-specific failure or retry limit reached; DeadLetteredAtUtc set and lease cleared.
- **Leased -> Pending by expiry:** worker dies or lease is abandoned; another claim may acquire it.
- **Systemic transport failure:** do not quarantine; pause polling and surface an alert until configuration/permission is corrected.

A quarantine row retains its immutable original payload and can be inspected for controlled operator recovery. No public redrive API is introduced.

## EventBridge and SQS responsibilities

EventBridge is the producer-facing acceptance boundary. `PublishedAtUtc` means the event bus accepted the entry; it does not mean a rule target or consumer processed it.

The communication architecture's SQS/DLQ convention applies to retryable target delivery after EventBridge acceptance. Each eventual EventBridge target should define its own SQS queue/DLQ and least-privilege queue policy in infrastructure. Producer-side PutEvents failures remain in the Identity outbox retry/quarantine state machine.

## Configuration and IAM

- `OutboxDispatcher__Enabled=false` unless deployment infrastructure has been provisioned.
- `EventBridge__BusName` is supplied by deployment configuration, not source code.
- `AWS_REGION` selects the regional EventBridge endpoint.
- AWS SDK default credentials are used; deployed Identity tasks receive a task/instance role.
- IAM policy permits `events:PutEvents` on only the configured bus ARN.
- Static keys and account-specific ARNs are never committed.

The repository has no deployment/IaC tree, so bus creation, role attachment, target rules, SQS DLQs and alarms cannot be verified in this change. Production enablement remains blocked until those resources exist.

## Observability and failure safety

Use structured logs and System.Diagnostics.Metrics (or the existing telemetry provider if one is introduced) for pending count/age, claims, accepted events, retry/quarantine counts, expired leases, PutEvents latency and failure categories. Include eventId, eventType, correlationId, attempt, bus, AWS request ID and sanitized error code. Exclude payload and HR note.

The existing API liveness endpoint must remain process-only. Database readiness remains based on Identity MySQL. Dispatcher health/metrics should be separately observable so a transient EventBridge outage does not cause a restart loop or incorrectly report the Identity database unhealthy.

## Test strategy

- Pure unit tests for option validation, event mapping, per-entry result mapping, retry/backoff and error classification.
- Fake publisher tests for success, partial failure, thrown transport exception and cancellation.
- MySQL 8.4 Testcontainers tests for disjoint concurrent claims, lease expiry/reclaim, stale-token acknowledgement rejection, success timestamp ordering, retry scheduling, quarantine and migration from the current schema.
- A crash/replay test simulates EventBridge acceptance followed by failure before database acknowledgement; the next worker republishes the identical eventId.
- Do not require live AWS credentials in CI. The EventBridge SDK client is mocked/faked. Live bus/IAM/target/DLQ verification belongs to deployment validation after infrastructure is provisioned.
