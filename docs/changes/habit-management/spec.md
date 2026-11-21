# Habit Management Specification

## Domain
A personal habit is owned by exactly one employee. It is private to that employee and cannot be read or modified by another employee or manager.

A habit contains:
- ID (server-generated UUID)
- Owner employee ID (from validated JWT `sub`)
- Name (required, trimmed, 1–100 characters)
- Description (optional, trimmed, maximum 500 characters)
- Category (optional, trimmed, maximum 50 characters)
- Frequency (`Daily` or `Weekly`)
- Target count (integer 1–1000 per frequency period)
- Start date (required)
- Optional end date (must not precede start date)
- Status (`Active` or `Archived`)
- Created and updated UTC timestamps

## Business rules
- Only authenticated employees may access the habit API.
- Employee ID is never accepted from a request body or query parameter.
- A caller may list/read/update/archive only habits whose OwnerEmployeeId equals the validated `sub`.
- A missing or malformed `sub` is unauthorized.
- An employee with a local employee-status projection of inactive is denied. A missing projection is treated as unknown rather than inactive; the service does not claim global immediate revocation until initial status synchronization is complete.
- New habits are Active.
- Archived habits remain readable in the owner's list/detail and cannot be updated; archive is idempotent.
- Create/update validation occurs before persistence.
- Habit service owns habit definitions only. It does not store completion records or mutate Challenge snapshots.
- Dates are calendar dates; timestamps are UTC.

## HTTP API
- `GET /api/habits`: list caller's habits, optionally filter by status.
- `GET /api/habits/{id}`: retrieve one caller-owned habit.
- `POST /api/habits`: create a habit.
- `PUT /api/habits/{id}`: update an active caller-owned habit.
- `POST /api/habits/{id}/archive`: archive a caller-owned habit.

## Acceptance criteria
- HM-01: Anonymous requests are rejected.
- HM-02: Caller identity is taken from validated JWT `sub`; request payloads cannot select an owner.
- HM-03: A caller can create a valid daily or weekly habit.
- HM-04: Invalid names, descriptions, categories, target counts, frequencies and date ranges return HTTP 400 without persistence.
- HM-05: List returns only the caller's habits.
- HM-06: Detail/update/archive for another employee's habit return HTTP 404 without revealing its existence.
- HM-07: An archived habit cannot be updated.
- HM-08: Repeated archive requests succeed without changing the original archive timestamp.
- HM-09: An employee explicitly marked inactive in the local projection receives HTTP 401.
- HM-10: Habit completion and challenge data are not written by this service.
- HM-11: Database migration is applied automatically by the Habit API at startup.
