## Habit activity submissions — implementation complete

- Owner approved submission cardinality, correction policy, activity-date/time-zone semantics, quantity and idempotency rules on 2026-11-21; recorded in ADR-0003.
- Event Tracking owns an append-only `habit_activity_submissions` source of truth and validates habit ownership through the Habit API using the caller's bearer token.
- Each accepted submission and its `HabitActivityRecorded` version 1 outbox event are persisted in the same MySQL transaction. The event includes employee and habit identifiers, activity date, quantity, UTC occurrence time and correlation ID.
- The Event Tracking outbox uses lease tokens, bounded batch claiming with `FOR UPDATE SKIP LOCKED`, retry scheduling and quarantine after the configured attempt limit. RabbitMQ publishing uses persistent messages and publisher confirms on the existing `teampulse.domain.events` topic exchange with routing key `habit.activity.recorded`.
- The dispatcher and publisher are configurable and disabled by default; enabling `EventTrackingOutbox:Enabled` starts local RabbitMQ delivery. EventBridge remains deferred until cloud infrastructure is provisioned.
- SvelteKit includes the employee Activity History page, server-side activity submission, habit/date history filters and direct habit-card check-in links. Submission idempotency keys are retained across failed/ambiguous responses and rotated after acceptance. Frontend CI run #44915274529 passed on commit `c8e512a`: all 9 Node unit tests passed, Svelte/TypeScript checks reported 0 errors and 0 warnings, and the production build succeeded. Backend CI #284 passed on 2026-11-21: Release build, Docker Compose validation, RabbitMQ topology checks, and all 298 tests passed across the seven test assemblies. This includes MySQL atomicity/idempotency/history integration coverage and isolated RabbitMQ publisher Testcontainers coverage. The outbox occurrence timestamp is normalized to MySQL `datetime(6)` precision before event serialization and persistence.
- Event Tracking API integration tests now cover missing-bearer authorization, inaccessible-habit non-disclosure (404), caller bearer-token forwarding and the expected Habit API route, Habit API network outage and timeout handling (503), no-persistence guarantees, and conflicting idempotency-key reuse (409). MySQL store integration coverage also verifies concurrent requests sharing an employee-scoped key produce one committed submission and outbox event. These backend API and store integration tests passed in backend CI #284. Frontend unit tests and SvelteKit production checks passed in frontend CI # run 44915274529 on commit `c8e512a`; the activity-submission vertical slice is complete. The separate downstream consumer/progress projection decision remains intentionally open.
- No manager-facing individual activity data or progress/streak calculations are exposed.

Updated: 2026-11-21

## Habit management and employee workspace — verified

- Habit management backend and SvelteKit employee experience are implemented: employees can view, create, update and archive their own habits through server-side authenticated actions; ownership remains enforced by Habit API.
- Habit management tasks HM-T01–HM-T09 are complete. Backend CI #284 passed the Release build and all 298 tests across seven assemblies. Frontend CI #44915274529 passed all 9 unit tests, Svelte/TypeScript checks (0 errors, 0 warnings) and the production build.
- The employee Activity History and submission vertical slice is also complete and CI-verified. No activity progress/streak projection or downstream activity consumer has been introduced without a separately approved business definition.
- Next product increment identified: HR-controlled employee invitations. Its SDD and security threat model are drafted, but implementation is blocked pending owner decisions on delivery-secret handling, key protection/backup, expiry/resend/cancel semantics, activation behavior and rate limiting. No invitation implementation has been started.

## Employee Activity History workspace refinement — verified

- Activity date input now formats the browser-local calendar date rather than deriving the selected date from a UTC ISO timestamp. This prevents the default date from shifting to the previous day for users east of UTC near midnight.
- Activity History now loads the authenticated employee profile from Identity's `/api/identity/me` endpoint and displays the employee name, role and avatar consistently with the Habit workspace. All three API requests remain server-side and use the validated session bearer token; unauthorized responses clear the session and redirect to sign-in.
- Frontend CI #31 passed on 2026-11-21 for commit `c2001e098bbc4086dfe0b3e9bc836126fc7804d1`: 10/10 Node unit tests, Svelte/TypeScript checks and production build succeeded.
- The failed intermediate CI attempts exposed a TypeScript definite-assignment issue in the new three-request load; the response tuple was corrected and the final CI run passed. No backend contract or business behavior changed.

Updated: 2026-11-21

## Activity submission form resilience — verified

- Activity submission failures now return the attempted habit, activity date and quantity to the SvelteKit page along with the existing idempotency key.
- The form restores those values after client-side validation failures, API rejections and ambiguous service errors, so employees can correct or retry without re-entering their submission.
- The idempotency key continues to be retained on failure and is rotated only after Event Tracking confirms acceptance.
- Frontend CI #33 passed on 2026-11-21 for commit `ee333bf413b9d4e1670804f4eaf449f59fdb5133`: frontend unit tests, Svelte/TypeScript checks and production build succeeded.

Updated: 2026-11-21


## HR-controlled employee invitations — implementation in progress

- Owner approved the HR invitation feature and selected Option A on 2026-11-21: asynchronous delivery using a dedicated, short-lived Data Protection-protected secret record, separate from ordinary outbox payloads. Initial invitation entities, EF Core persistence, migration, cryptographic token service and persistent local Data Protection key-ring setup are committed.
- Owner approved deleting the live delivery secret after provider acceptance (with expiry cleanup as a backstop) and at-least-once delivery semantics; duplicate emails are possible.
- Local development must use a persistent, Git-ignored Data Protection key ring with restrictive filesystem permissions. Production key protection/storage and backup/restore implementation are deferred until cloud infrastructure setup; no AWS/KMS choice is implied.
- Owner approved the remaining policy defaults on 2026-11-21: 24-hour expiry, resend only while pending/unexpired with atomic token rotation, terminal cancellation, direct activation to active Employee, generic activation failures and rate limiting. Mailpit is available in root Docker Compose on SMTP 1025 / web UI 8025. The transactional invitation application service and HrAdmin-protected create/get/resend/cancel API endpoints are implemented, with authorization and MySQL API integration coverage added. Anonymous activation is implemented with generic failure responses, a five-attempt-per-minute IP-based rate limit, password hashing and atomic Employee creation. A durable MySQL-backed delivery worker now leases protected-secret rows, checks invitation status/expiry/generation, unprotects the token only at the delivery boundary, sends through SMTP, retries transient failures with bounded exponential backoff, and deletes the secret after SMTP acceptance only if its generation/lease still matches. A SvelteKit `/activate` page collects and confirms the initial password. Local Development configuration enables Mailpit at `localhost:1025` and links to `http://localhost:5173/activate`; API integration tests explicitly disable the SMTP worker. MySQL integration tests cover retry scheduling, resend generation rotation, stale acknowledgement rejection and expiry cleanup. Backend CI #44920032059 passed on commit `5826083`: Release build, Docker Compose validation, RabbitMQ topology checks and the complete suite succeeded before the Mailpit end-to-end test was introduced. The first Mailpit end-to-end run #44920566280 failed because no invitation message was discovered within the test timeout; 130 of 131 Identity tests passed. The Mailpit test has been hardened to verify that the worker is enabled, parse structured recipient addresses and report queue attempt/error/lease diagnostics without exposing message contents or tokens. Backend CI #44921197892 is validating that diagnostic update. A separate MySQL integration test now simulates worker termination after lease acquisition and verifies a fresh queue instance reclaims the expired durable lease while the old lease cannot acknowledge it; backend CI #44921375328 was queued for this change. Frontend CI #44920805088 passed after the activation page added no-store caching and no-referrer headers: unit tests, Svelte/TypeScript checks and production build all succeeded. Production email-provider selection and production key protection/storage remain deferred.

Updated: 2026-11-21
