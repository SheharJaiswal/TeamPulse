## Habit activity submissions — implementation in progress

- Owner approved the Open Question 20 defaults on 2026-11-21; the resolved policy is recorded in ADR-0003.
- Event Tracking now has an append-only `habit_activity_submissions` entity/table and an additive MySQL migration. The employee ID is derived from JWT `sub`; employee status projection denies explicitly inactive employees.
- The authenticated Event Tracking API validates habit ownership by calling the Habit API with the caller's bearer token, checks the configured habit date range, persists quantities from 1–1,000, enforces employee-scoped GUID idempotency and provides owner-scoped chronological history.
- The SvelteKit workspace now includes an Activity History page with a check-in form and personal timeline. JWT remains in the HTTP-only server cookie.
- Frontend CI #5 passed Svelte/TypeScript checks and production build. Backend CI #251 Release build passed; the full .NET test step was still running at the time of this update.
- Remaining before this increment is complete: transactional outbox persistence and local RabbitMQ publisher for `HabitActivityRecorded`, Event Tracking API integration tests for auth/ownership/idempotency/history, frontend polish for selecting the specific habit from the habit card, and final end-to-end CI verification.
- This feature is not yet considered complete or production-ready. No Event Tracking activity event is published yet; no manager-facing individual activity data is exposed.

Updated: 2026-11-21
