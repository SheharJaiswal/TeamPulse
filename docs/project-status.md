## Habit activity submissions — implementation in progress

- Owner approved submission cardinality, correction policy, activity-date/time-zone semantics, quantity and idempotency rules on 2026-11-21; recorded in ADR-0003.
- Event Tracking owns an append-only `habit_activity_submissions` source of truth and validates habit ownership through the Habit API using the caller's bearer token.
- Each accepted submission and its `HabitActivityRecorded` version 1 outbox event are persisted in the same MySQL transaction. The event includes employee and habit identifiers, activity date, quantity, UTC occurrence time and correlation ID.
- The Event Tracking outbox uses lease tokens, bounded batch claiming with `FOR UPDATE SKIP LOCKED`, retry scheduling and quarantine after the configured attempt limit. RabbitMQ publishing uses persistent messages and publisher confirms on the existing `teampulse.domain.events` topic exchange with routing key `habit.activity.recorded`.
- The dispatcher and publisher are configurable and disabled by default; enabling `EventTrackingOutbox:Enabled` starts local RabbitMQ delivery. EventBridge remains deferred until cloud infrastructure is provisioned.
- SvelteKit includes the employee Activity History page, server-side activity submission, habit/date history filters and direct habit-card check-in links. Submission idempotency keys are retained across failed/ambiguous responses and rotated after acceptance. Frontend CI #5 passed Svelte/TypeScript checks and production build. The backend Release build passed in CI #251; its test step was still running at the last observed poll.
- Remaining: full CI confirmation after the outbox increment, MySQL atomicity/idempotency/history tests, API auth/ownership integration tests and isolated RabbitMQ publisher integration coverage. Do not mark the vertical slice complete until those pass.
- No manager-facing individual activity data or progress/streak calculations are exposed.

Updated: 2026-11-21
