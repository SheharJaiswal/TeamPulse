# Tasks: Habit Activity Submissions

## Policy

- [x] HAS-00 Owner approved submission cardinality, correction policy, date/backdating/time-zone semantics and quantity/idempotency rules (ADR-0003).

## Implemented in the first delivery slice

- [x] Add Event Tracking domain model and application abstraction for activity submission/history.
- [x] Add Habit API client call that forwards caller authorization and validates habit ownership/date range.
- [x] Add Event Tracking MySQL persistence and additive migration; retain startup migration behavior.
- [x] Add authenticated submit/list/detail endpoints with employee ownership and local inactive-status enforcement.
- [x] Add SvelteKit server actions and activity history UI without exposing the JWT to browser JavaScript.
- [x] Add basic domain validation tests for quantity, identity and UTC timestamp rules.

## Remaining before the vertical slice is complete

- [ ] Add a transactional Event Tracking outbox record in the same database transaction as each accepted activity submission.
- [ ] Add lease-safe local RabbitMQ publisher for the versioned `HabitActivityRecorded` event; EventBridge remains deferred until cloud infrastructure is provisioned.
- [ ] Add MySQL integration tests for persistence, employee-scoped idempotency, race behavior and history filtering/order.
- [ ] Add API integration tests for JWT authorization, habit owner isolation, inactive employee denial and Habit API outage handling.
- [ ] Improve the habit card action to preselect its habit on the activity page.
- [ ] Run complete backend and frontend CI and record the verified results in `docs/project-status.md`.
