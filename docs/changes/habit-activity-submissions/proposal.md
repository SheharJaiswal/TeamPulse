# Proposal: Habit Activity Submissions

## Problem

Employees can create, edit and archive personal habit definitions, but they cannot record activity against those habits or review a progress history. This is the next employee-facing capability needed to make the habit workspace useful beyond habit setup.

## Ownership

- Habit remains the source of truth for personal habit definitions, schedules, date ranges and owner isolation.
- Event Tracking owns immutable activity submissions and their history.
- The employee identity is always derived from the validated JWT `sub`; clients never choose an employee ID.
- Event Tracking must validate the referenced habit through the Habit service using the caller's authorization context. It must not read Habit's database.
- Activity events may later feed Habit progress, Challenge progress, Insight and Notification through the approved event contracts. This change does not implement Challenge employee APIs or expose individual activity data to managers.

## Proposed MVP capability

1. An authenticated employee submits a self-reported activity against one of their own habits.
2. The submission records a server-generated ID, habit ID, employee ID, activity date, quantity/count, submitted-at UTC timestamp and an idempotency key.
3. The employee can list their own submissions, filtered by habit and date range, in a stable chronological order.
4. Submissions are append-only; corrections, deletion and replacement are not part of this MVP unless explicitly approved.
5. Event Tracking persists submissions in its own MySQL database and applies migrations at startup.
6. A submission is accepted only when the Habit service confirms that the habit belongs to the caller and is eligible for activity on the supplied activity date.
7. No evidence attachment is required for personal habits. Challenge evidence remains governed by the challenge task's predeclared verification requirement.
8. No individual submission or personal habit details are exposed through manager or organization insight views.

## Decisions required before implementation

See Open Question 20 in `docs/open-questions.md`. The proposed defaults are recommendations, not approved policy. In particular, the product owner must decide:
- Whether an employee can submit multiple activity records for the same habit and activity date, or only one.
- Whether a submission can be edited/deleted or must be immutable.
- Which activity dates are accepted (today only, bounded backdating, or any date inside the habit's active date range), and what calendar/time-zone interpretation applies.
- Whether quantity is required and how it contributes to daily/weekly targets.

## Out of scope

- Challenge participation, challenge completion or challenge scoring.
- Habit definition changes or storage of activity records in Habit.
- Manager access to raw activity submissions.
- Evidence files, passive monitoring, device/location/health integrations.
- Streaks and derived progress calculations until their rules are approved.
- Retention/deletion jobs; production retention still requires privacy/legal sign-off under ADR-0002.
- EventBridge deployment; local development remains independent of AWS infrastructure.
