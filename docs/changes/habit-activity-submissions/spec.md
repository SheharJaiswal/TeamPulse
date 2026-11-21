# Specification: Habit Activity Submissions

## Status

Approved by the project owner on 2026-11-21. See ADR-0003. The first implementation is in progress; remaining verification tasks are tracked in `tasks.md`.

## Actors and ownership

- An authenticated employee may submit and view only their own activity.
- Event Tracking owns immutable activity submissions, history and the corresponding domain event outbox.
- Habit owns habit definitions and validates ownership, status and configured date range.
- Challenge owns challenge task completion and scoring; this change does not create or mutate challenge completion state.

## Functional requirements

- HAS-01: An authenticated employee can submit a self-reported activity against an active habit they own.
- HAS-02: Employee identity is derived from validated JWT `sub`; request data cannot select an employee.
- HAS-03: Event Tracking validates the referenced habit through the Habit API using the caller's bearer token and never accesses the Habit database directly.
- HAS-04: Inaccessible habits return a non-disclosing not-found response.
- HAS-05: Event Tracking persists append-only submissions in its own database.
- HAS-06: An employee can retrieve their own history with optional habit and date filters.
- HAS-07: An employee cannot retrieve another employee's submission.
- HAS-08: Idempotency keys are GUIDs unique per employee. An identical retry returns the original submission; reuse with different habit/date/quantity returns conflict.
- HAS-09: The service enforces the local inactive-employee projection at its authorization boundary.
- HAS-10: No manager or organization endpoint exposes individual submissions or personal habit details.
- HAS-11: Database migrations are applied by the Event Tracking API at startup.
- HAS-12: Submission and `HabitActivityRecorded` outbox event are persisted atomically.
- HAS-13: The outbox dispatcher publishes versioned events at least once through configurable local RabbitMQ transport; consumers deduplicate by event ID.

## Approved submission policy (ADR-0003)

- Multiple submissions per habit and calendar date are allowed.
- Accepted submissions are immutable; no edit or delete endpoint exists.
- Employee-selected activity date must be within the habit's configured start/end date range.
- Activity date is a calendar date, not a UTC instant. Submission/occurrence timestamps are server-generated UTC values.
- Quantity is an integer from 1 through 1,000.
- Reuse of an employee-scoped idempotency key with different submission values returns HTTP 409.
- Streak and target-progress calculations are explicitly out of scope.

## Non-functional requirements

- Keep activity persistence and queries within Event Tracking.
- Preserve employee isolation and avoid leaking resource existence.
- Do not require AWS/EventBridge for local development or CI.
- Use a transactional outbox, publisher confirms, persistent RabbitMQ messages, bounded retries and lease-safe acknowledgement.
- Do not claim production retention compliance until ADR-0002 legal/privacy review and deletion jobs are complete.
