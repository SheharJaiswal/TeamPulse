# Proposal: Identity Controller Unit Tests

## Problem
The Identity API controller implements registration and login behavior, but the current test project covers only password hashing and JWT token construction. Request validation, normalized-email duplicate detection, persistence calls and generic authentication failures are not protected by automated tests.

## Scope
Add isolated controller unit tests using a small in-memory fake employee repository, the real ASP.NET Core PasswordHasher, and a deterministic token-service fake. Test the existing approved Identity API behavior without changing business rules or requiring MySQL.

## Out of scope
Database uniqueness races, EF Core migrations, HTTP middleware/JWT bearer validation, /me authorization, inactive-account enforcement, HR provisioning and production secret management.

## Success criteria
- Missing registration fields and passwords shorter than 12 characters return 400 without persistence.
- Email is trimmed and normalized before duplicate lookup; duplicates return 409 without persistence.
- Valid registration persists a one-way password hash and returns the documented token response.
- Unknown-email and wrong-password login attempts return the same generic 401 response.
- Valid login normalizes email and returns a token response.
