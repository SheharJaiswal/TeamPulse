# Specification: Employee Challenge Participation

## Authorization and tenant scope

- Challenge API requires a validated JWT.
- HR challenge listing and creation remain restricted to `HrAdmin`.
- Employee discovery, joined-challenge reads and join operations are available to `Employee` and `HrAdmin`.
- Resolve organization and employee IDs only from validated `organization_id` and `sub` claims.
- Participation is scoped through the challenge's organization; do not query Identity or Habit databases.
- Employee challenge operations require a locally projected active employee status. Missing or inactive projection denies the operation.
- Never accept employeeId or organizationId from request body for employee operations.

## Available challenges

`GET /api/challenges/available` returns challenges belonging to the caller's organization whose end date is today or later and for which the caller has no participation. Challenges may be joined before their start date. Results are ordered by start date and name.

## Join

`POST /api/challenges/{challengeId}/join`:

- Return 400 for an empty/invalid challenge identifier.
- Return 404 if the challenge does not exist in the caller's organization.
- Return 409 if the challenge has ended.
- Create participation with a server-generated ID and UTC join timestamp.
- The unique key `(challenge_id, employee_id)` prevents duplicate participation at database level.
- A repeated or concurrent join for the same employee and challenge is idempotent and returns the existing join timestamp with `alreadyJoined=true`.
- Return the created join timestamp with `alreadyJoined=false` for a new participation.

## My challenges

`GET /api/challenges/my` returns the caller's participations for challenges in their organization, newest join first. This includes challenges that have ended.

## Persistence

Challenge owns `challenge_participations` with only participation ID, challenge ID, employee ID and joined UTC timestamp. Challenge and employee IDs are external identifiers; no cross-service foreign keys are added. Apply the schema using a checked-in EF Core migration.

## UI

- Employee route: `/challenges`.
- Display Available challenges and My challenges as separate sections.
- Available cards show name, description, start/end dates and Join challenge action.
- Joined cards show name, description, dates and join date.
- Join posts through a SvelteKit server action; the bearer token is never exposed to client-side JavaScript.
- Provide empty states, validation/service errors and success feedback.
- HR can use the same employee participation page and retain a separate HR management route at `/hr/challenges`.

## Progress and scoring

Participation is recorded only. No progress or completion state is fabricated because the current challenge model does not define a measurable target or activity mapping.
