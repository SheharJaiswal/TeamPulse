# Design: Habit Activity Submissions

## Status

Draft — implementation is blocked on the product decisions listed in Open Question 20. No API contract or persistence model is considered approved yet.

## Service boundaries

```text
SvelteKit employee UI
    |
    | same-origin server action; forwards employee bearer token
    v
Event Tracking API
    |                         |
    | validates caller        | validates habit ownership/eligibility
    v                         v
Event Tracking MySQL       Habit API
(activity source of truth) (habit definition source of truth)
```

- The browser never calls Event Tracking or Habit directly and never receives the JWT.
- Event Tracking derives employee identity from the validated token.
- Event Tracking calls the Habit API with the caller's bearer token to retrieve/validate the requested habit. A not-found response must not reveal whether a habit belongs to another employee.
- No cross-service database access or cross-service foreign key is introduced.
- The Habit API remains authoritative for whether the habit is active and for its configured start/end dates.

## Candidate HTTP contract

These routes are design candidates only and must not be treated as implemented:

- `POST /api/habit-activities`: submit an activity for a habit.
- `GET /api/habit-activities`: list the caller's submissions; optional `habitId`, `fromDate` and `toDate` filters.
- `GET /api/habit-activities/{id}`: retrieve one caller-owned submission.

Candidate request fields: `habitId`, `activityDate`, `quantity`, `idempotencyKey`. Employee ID, submission ID and submitted-at timestamp are server-owned.

## Candidate persistence

A dedicated Event Tracking table would store the submission, with an employee/date index and a unique idempotency constraint scoped to the employee. Whether a second uniqueness constraint on habit/date is appropriate depends on Open Question 20. Persisting an accepted submission and its outbox event should be atomic. The outbox publisher/transport must follow the existing local RabbitMQ and deferred EventBridge architecture; do not introduce an AWS prerequisite.

## Security and privacy

- Require authenticated employee access and local employee-status enforcement.
- Validate habit ownership through the Habit API using the caller's token.
- Never accept an employee ID from the request.
- Return a non-disclosing not-found response for inaccessible habits/submissions.
- Do not expose raw submissions to managers or aggregate endpoints.
- Do not accept evidence or health/device/location data in this increment.
- Apply idempotency before mutating persistence.
- Keep the existing employee lifecycle inbox/projection behavior intact.

## Delivery sequence after policy approval

1. Finalize the API and event contract in SDD.
2. Add Event Tracking domain/application/persistence and MySQL migration.
3. Add authenticated submission/history API and Habit API client.
4. Add unit and MySQL integration tests for authorization, date/quantity validation, idempotency and history ordering.
5. Add SvelteKit server actions and employee activity history UI.
6. Verify with backend and frontend CI; update project status only after checks pass.
