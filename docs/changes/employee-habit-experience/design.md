# Employee Habit Experience Design

## Application
SvelteKit + Svelte 5 + TypeScript. The application is a server-rendered employee workspace.

## Authentication boundary
- Login action calls Identity server-to-server.
- Access token is stored in a secure-in-production, HTTP-only, SameSite=Lax cookie.
- The browser never reads the token.
- Authenticated page loads and actions forward the cookie token as a Bearer token to backend APIs.
- On a 401 response, clear the cookie and redirect to login.
- SvelteKit form actions preserve same-origin interaction and avoid exposing service URLs to the browser.

## API integration
Private server configuration:
- `IDENTITY_API_BASE_URL`, default `http://localhost:5101`
- `HABIT_API_BASE_URL`, default `http://localhost:5102`

Identity supplies login and `/api/identity/me`. Habit supplies list/create/update/archive. Habit owns validation and employee isolation; the frontend performs usability validation only.

## Screens
- Sign-in: email/password, generic API errors, HR-provisioned account notice.
- My habits: profile greeting, active/archived summaries, active habit cards, create/edit forms and archived history.
- Sign-out: clear the HTTP-only session cookie.

## Deferred
Activity completion, streaks, challenge linking, reminders, HR/manager views, production deployment and refresh-token/session renewal.
