# Employee Habit Experience

## Problem
The Habit API now supports employee-owned habit definitions, but employees do not yet have a usable web interface.

## Outcome
Provide a SvelteKit employee workspace that authenticates through Identity and lets an employee view, create and archive personal habits.

## Scope
- Sign in using the existing Identity login endpoint.
- Show the authenticated employee identity and personal habits.
- Create daily and weekly habits with target, dates, optional description and category.
- Archive habits and retain archived items in the personal list.
- Keep JWTs in HTTP-only cookies and make backend requests from SvelteKit server code.

## Non-goals
- Public registration or employee invitation workflow.
- Activity completion, streaks, reminders or challenge participation.
- Manager/HR dashboards or access to another employee's habits.
- Production hosting, AWS deployment or token refresh.

## Technology
SvelteKit with Svelte 5 and TypeScript. Do not introduce Angular or React. Backend APIs remain independently deployed .NET services.
