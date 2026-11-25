# Proposal: Employee Challenge Participation

## Context

HR can create organization challenges, but employees currently have no employee-facing way to discover or join them. Challenge participation belongs to the existing Challenge service and employee SvelteKit workspace.

## Scope

- Show organization challenges that have not ended and that the signed-in employee has not joined.
- Allow an active employee to join an organization challenge.
- Show the employee's joined challenges, including past challenges.
- Add a unique database constraint so one employee can join a challenge only once.
- Expose the employee Challenges route to Employee and HrAdmin roles; keep HR challenge management HR-only.
- Enforce organization scope and the Challenge-owned employee lifecycle projection in the API.

## Out of scope

Challenge goal definitions, activity submissions, progress calculation, scoring, leaderboards, evidence, notifications and employee withdrawal are not defined by the current challenge creation contract and are not added here. The current HR-created challenge has dates and descriptive text only; no progress metric is assumed.

## Architecture

Keep the existing six-service architecture. Challenge owns participation and its MySQL schema. The SvelteKit server route forwards authenticated requests to Challenge API using the server-side session token. No cross-service database access or new service is introduced.
