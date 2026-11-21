# Habit Management

## Problem
The Habit service currently contains only the employee-status projection and messaging foundation. Employees cannot create or manage personal habits.

## Scope
Add an authenticated, employee-owned habit management API to the existing Habit service. Support listing, reading, creating, updating and archiving personal habits. Keep activity completion records and challenge participation outside this service.

## Non-goals
- Activity submission/completion history (Event Tracking owns it).
- Challenge-specific targets or participation (Challenge owns them).
- Manager access to individual habits.
- Cross-employee sharing, templates or public habit catalogue.
- Svelte UI in this backend increment; the frontend will consume this API in a subsequent vertical slice.

## Architecture
Use the existing Habit Clean Architecture projects and Habit-owned MySQL database. Employee identity comes only from the validated JWT `sub` claim. The API validates the same issuer, audience and signing key configuration as Identity. The service checks its local employee-status projection and rejects employees explicitly projected as inactive. No cross-service database access is introduced.

## Acceptance
See `spec.md`. The feature is implemented only within the existing Habit service.
