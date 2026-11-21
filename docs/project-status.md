## Habit Management API and employee web experience

- Habit Management API is implemented: authenticated personal habit create/list/detail/update/archive endpoints, Habit-owned MySQL migration, domain rules, JWT validation, local inactive-status enforcement and owner-isolation integration coverage.
- Employee web experience is implemented in `frontend/` using SvelteKit, Svelte 5 and TypeScript. It provides Identity login, HTTP-only cookie sessions, employee profile display, personal habit list/create/update/archive, and active/archived views.
- Browser code never reads the JWT or calls service APIs directly. SvelteKit server actions/load functions call Identity and Habit with the cookie token; service URLs are private environment configuration.
- Local ports: Identity `5101`, Habit `5102`, SvelteKit `5173`. See `frontend/README.md`.
- Backend CI #247 passed: Release build, all 275 .NET tests (including 30 Habit tests), and all 19 RabbitMQ topology contract tests.
- SvelteKit frontend CI #3 passed after adding edit support: Svelte/TypeScript checks and production build succeeded.
- Activity completion, streaks, challenge participation, employee invitation and production hosting remain outside this increment. No AWS resources or persistent RabbitMQ data were changed.

## Next product increment: habit activity submissions

- The next planned vertical slice is employee self-reported habit activity and personal progress history.
- Service ownership is fixed: Event Tracking owns activity submissions/history; Habit remains the source of truth for habit definitions. Event Tracking must validate habit ownership through the Habit API and must not access the Habit database.
- The SDD change is drafted under `docs/changes/habit-activity-submissions/`. Implementation is intentionally blocked pending owner decisions in Open Question 20 covering duplicate/multiple submissions, correction/deletion, backdating/time-zone semantics and quantity/target behavior.
- This is a draft design, not an implemented API or UI capability.

Updated: 2026-11-21
