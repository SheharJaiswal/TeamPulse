# Specification: Habit Activity Submissions

## Status

Draft / blocked by Open Question 20. The requirements below describe the intended capability; rules dependent on unresolved product decisions are deliberately not asserted.

## Actors and ownership

- An authenticated employee may submit and view only their own activity.
- Event Tracking owns submission records and history.
- Habit owns habit definitions and validates ownership, status and configured date range.
- Challenge owns challenge task completion and scoring; this change does not create or mutate challenge completion state.

## Functional requirements

- HAS-01: An authenticated employee can submit a self-reported activity against a habit they own.
- HAS-02: The employee identity is derived from the validated JWT `sub`; request data cannot select an employee.
- HAS-03: Event Tracking validates the referenced habit through the Habit API and never accesses the Habit database directly.
- HAS-04: A caller cannot submit activity against another employee's habit; inaccessible habits return a non-disclosing not-found response.
- HAS-05: Event Tracking persists activity submissions in its own database.
- HAS-06: An employee can retrieve their own activity history with optional habit and date filters.
- HAS-07: An employee cannot retrieve another employee's submission.
- HAS-08: Duplicate request handling is idempotent according to the approved idempotency policy.
- HAS-09: The service enforces the local inactive-employee projection at its authorization boundary.
- HAS-10: No manager or organization endpoint exposes individual submissions or personal habit details.
- HAS-11: Database migrations are applied by the Event Tracking API at startup.
- HAS-12: Persisting an accepted submission and its publishable domain event is atomic.

## Policy requirements pending owner decision

The exact semantics for duplicate activity dates, multiple submissions, correction/deletion, backdating/time zones, quantity aggregation and target progress must be specified from Open Question 20 before HAS-01 through HAS-08 are implemented.

## Non-functional requirements

- Keep activity persistence and query paths within Event Tracking.
- Use UTC timestamps for server-generated audit timestamps.
- Use calendar dates for the activity date only after the time-zone interpretation is approved.
- Preserve tenant/employee isolation and avoid leaking resource existence.
- Do not require AWS/EventBridge for local development or CI.
- Do not claim production retention compliance until the ADR-0002 legal/privacy review and deletion jobs are complete.
