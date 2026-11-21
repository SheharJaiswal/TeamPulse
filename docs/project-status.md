## Habit activity submissions — implementation in progress

- Owner approved submission cardinality, correction policy, activity-date/time-zone semantics, quantity and idempotency rules on 2026-11-21; recorded in ADR-0003.
- Event Tracking owns an append-only `habit_activity_submissions` source of truth and validates habit ownership through the Habit API using the caller's bearer token.
- Each accepted submission and its `HabitActivityRecorded` version 1 outbox event are persisted in the same MySQL transaction. The event includes employee and habit identifiers, activity date, quantity, UTC occurrence time and correlation ID.
- The Event Tracking outbox uses lease tokens, bounded batch claiming with `FOR UPDATE SKIP LOCKED`, retry scheduling and quarantine after the configured attempt limit. RabbitMQ publishing uses persistent messages and publisher confirms on the existing `teampulse.domain.events` topic exchange with routing key `habit.activity.recorded`.
- The dispatcher and publisher are configurable and disabled by default; enabling `EventTrackingOutbox:Enabled` starts local RabbitMQ delivery. EventBridge remains deferred until cloud infrastructure is provisioned.
- SvelteKit includes the employee Activity History page, server-side activity submission, habit/date history filters and direct habit-card check-in links. Submission idempotency keys are retained across failed/ambiguous responses and rotated after acceptance. Frontend CI #5 passed Svelte/TypeScript checks and production build. The backend CI #257 passed on 2026-11-21: Release build succeeded and all 284 tests passed across the seven test assemblies. This includes MySQL atomicity/idempotency/history integration coverage and isolated RabbitMQ publisher Testcontainers coverage.
- Remaining: Event Tracking API integration tests for activity submission authorization, habit ownership isolation and Habit API outage handling. The dedicated backend CI run passed; frontend CI #7 passed Svelte/TypeScript checks and the production build. Do not mark the vertical slice complete until those pass.
- No manager-facing individual activity data or progress/streak calculations are exposed.

Updated: 2026-11-21
