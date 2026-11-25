# Design: Employee Challenge Participation

## Backend

Add the Challenge-owned `ChallengeParticipation` domain entity and EF mapping. The table has a unique composite index on challenge and employee IDs plus an employee lookup index. No FK crosses service boundaries.

Use controller-level JWT authentication with action-level role authorization: HR-only for management; Employee/HrAdmin for employee-facing actions. A shared scope helper reads tenant and employee claims. A helper checks the Challenge-owned lifecycle projection before employee operations.

The join operation checks tenant ownership and end date, then inserts a participation row. The unique index is the concurrency authority. If insert reports a database update conflict, use a fresh context to check whether the exact participation exists; return idempotent success only when it does, otherwise rethrow.

## Frontend

Create SvelteKit `/challenges` route. Server load verifies the current Identity profile and calls Challenge API available/my endpoints with the session bearer token. The Join form posts to a server action which validates the challenge GUID and calls the join API. SvelteKit action enhancement refreshes loaded data after joining.

Add a Challenges navigation item for all employee roles and a separate Manage challenges item for HR. Keep existing HR create/list route intact.

## Verification

- Unit-test participation identifier and UTC timestamp invariants.
- Run backend restore/build and Challenge unit/integration tests.
- Run frontend tests, `npm run check` and production build.
- Verify API authorization, tenant isolation, inactive employee denial, duplicate join and ended challenge behavior with integration tests.
