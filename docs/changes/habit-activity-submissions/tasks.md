# Tasks: Habit Activity Submissions

## Policy
- [x] Owner approved submission cardinality, correction policy, activity-date semantics, quantity and idempotency rules (ADR-0003).

## Implementation
- [x] Event Tracking domain model, employee-scoped persistence and history query.
- [x] Validate habit ownership through Habit API using the caller's bearer token.
- [x] Persist activity submission and versioned HabitActivityRecorded outbox message atomically.
- [x] Add additive MySQL outbox migration and startup migration support.
- [x] Add lease-safe outbox claiming, retry, quarantine and publisher-confirmed RabbitMQ publishing.
- [x] Add authenticated personal activity submit/list/detail endpoints and SvelteKit history UI.
- [x] Add frontend and backend CI validation.

## Remaining
- [x] Add MySQL integration tests proving activity and outbox commit/rollback atomically, employee-scoped idempotency and filtered history.
- [x] Add API integration tests for missing-bearer authorization, non-disclosing 404 on inaccessible habits, caller bearer-token forwarding, Habit API network outage/timeout (503), identical idempotent retries, conflicting key reuse (409), and no-persistence guarantees; latest CI execution remains pending.
- [x] Verify local RabbitMQ publisher against a dedicated Testcontainers broker without changing shared persistent topology/data.
- [x] Add activity history habit/date filters and a preselected habit action from habit cards.
- [x] Preserve the same idempotency key when a submission response is ambiguous or retryable; rotate it only after acceptance.
- [x] Verify concurrent submissions with the same employee-scoped idempotency key commit one submission and one outbox event.
- [ ] Decide downstream event consumer and progress projection scope separately; do not infer streak/target semantics.
