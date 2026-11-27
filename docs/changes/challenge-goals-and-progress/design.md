# Design: Challenge Goals and Employee Progress

## Data ownership
Challenge adds action instructions, target quantity and unit to its existing challenge row. Event Tracking adds a separate challenge activity submission table with submission ID, employee ID, challenge ID, activity date, decimal quantity, idempotency key and UTC submission timestamp. No cross-service FK is introduced.

## Request flow
1. HR creates a challenge with goal metadata through Challenge API.
2. Employee workspace obtains challenge goal and their joined status from Challenge API.
3. Employee submits challenge ID, activity date, quantity and idempotency key to Event Tracking.
4. Event Tracking validates the active employee projection and forwards the caller's bearer token to Challenge API's employee-scoped goal/eligibility endpoint.
5. Event Tracking stores the submission and outbox event atomically, with unique employee/idempotency-key constraint.
6. Employee progress endpoint returns only the caller's records and cumulative total. SvelteKit displays completion by comparing total with Challenge-owned target.

## Boundaries and failure behavior
- Event Tracking does not query Challenge's database.
- Challenge API outage/timeout yields 503; no activity is persisted.
- A duplicate request is idempotent; conflicting idempotency-key reuse returns 409.
- Activity submission is self-report, not verified evidence.
- Progress is read from Event Tracking and the goal from Challenge; UI computes the display status without duplicating challenge data ownership.
- EventBridge remains deferred. The existing configurable local Event Tracking outbox is used for event publication.
