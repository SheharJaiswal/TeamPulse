# Employee Habit Experience Specification

## Authentication
- EHE-01: Employee can sign in through `POST /api/identity/login`.
- EHE-02: JWT is stored only in an HTTP-only, same-site session cookie; browser JavaScript cannot read it.
- EHE-03: Expired/invalid sessions are cleared and redirected to sign-in when an API returns 401.
- EHE-04: No public registration route is presented in the employee UI.
- EHE-05: Sign-out clears the session cookie.

## Personal habits
- EHE-06: Authenticated employee sees only their habits from `GET /api/habits`.
- EHE-07: Employee sees their Identity profile from `GET /api/identity/me`.
- EHE-08: Employee can create a habit with name, optional description/category, daily/weekly frequency, target count and date range.
- EHE-09: Client validates basic form constraints; server/API remains authoritative.
- EHE-10: Employee can archive a habit; archived habits remain visible in a separate historical section.
- EHE-11: No employee ID is accepted from browser form data; backend derives ownership from JWT `sub`.
- EHE-12: Employee can update an active habit using the Habit API; archived habits are not offered for editing and the API remains authoritative for archived-state conflicts.

## Runtime and security
- EHE-13: Browser-to-SvelteKit interactions are same-origin. SvelteKit server actions proxy to Identity and Habit; backend base URLs remain private server configuration.
- EHE-14: Backend API URLs are configurable through `IDENTITY_API_BASE_URL` and `HABIT_API_BASE_URL`.
- EHE-15: The UI remains usable on desktop and mobile.
- EHE-16: Svelte check and production build run in GitHub Actions.
