# Tasks: Habit Activity Submissions

## Blocked — owner policy decision required

- [ ] HAS-00 Approve Open Question 20: submission cardinality, correction policy, date/backdating/time-zone semantics, and quantity/target semantics.
- [ ] Finalize API request/response and event contract from the approved policy.

## Implementation after approval

- [ ] Add Event Tracking domain model and application abstractions for activity submission/history.
- [ ] Add Habit API client that forwards the validated caller context and validates habit ownership/status/date eligibility.
- [ ] Add Event Tracking MySQL persistence and migration; retain startup migration behavior.
- [ ] Add authenticated submit/list/detail endpoints with employee ownership and local inactive-status enforcement.
- [ ] Add atomic outbox publication using the existing local transport abstraction.
- [ ] Add unit tests for policy validation, ownership, idempotency and failure-without-mutation.
- [ ] Add MySQL integration tests for persistence, unique constraints and history queries.
- [ ] Add SvelteKit server actions and activity history UI without exposing the JWT to browser JavaScript.
- [ ] Run backend and frontend CI; update `docs/project-status.md` with verified results.
