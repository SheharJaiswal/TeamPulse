# TeamPulse Web

SvelteKit employee workspace for the TeamPulse MVP.

## Current scope

- Sign in through the Identity API and keep the token in an HTTP-only, same-site session cookie.
- Load the authenticated employee profile and personal habits.
- Create, edit and archive daily or weekly habits.
- Record self-reported habit activity and view a private activity history.
- Store activity submissions in Event Tracking; Habit remains the source of truth for habit definitions.

The browser never receives or stores the JWT. SvelteKit server load functions and form actions call the backend APIs with the HTTP-only cookie. This keeps API calls same-origin from the browser and avoids exposing backend service URLs or requiring browser CORS access.

## Local development

1. Start MySQL: `docker compose up -d mysql`.
2. In each backend terminal, set the same JWT signing key (at least 32 UTF-8 bytes) and use the Development environment:

   PowerShell:
   ```powershell
   $env:ASPNETCORE_ENVIRONMENT = "Development"
   $env:Jwt__SigningKey = "replace-with-a-local-random-secret-at-least-32-bytes"
   ```

   Bash:
   ```bash
   export ASPNETCORE_ENVIRONMENT=Development
   export Jwt__SigningKey="replace-with-a-local-random-secret-at-least-32-bytes"
   ```

3. Start Identity on port 5101:
   ```bash
   dotnet run --project backend/src/Services/Identity/Identity.API --urls http://localhost:5101
   ```
4. Start Habit on port 5102:
   ```bash
   dotnet run --project backend/src/Services/Habit/Habit.API --urls http://localhost:5102
   ```
5. Start Event Tracking on port 5103 (it validates habit ownership by calling Habit):
   ```bash
   dotnet run --project backend/src/Services/EventTracking/EventTracking.API --urls http://localhost:5103
   ```
6. Copy `.env.example` to `.env` in this directory if you need to override the API URLs.
7. Install and start the web application:
   ```bash
   cd frontend
   npm install
   npm run dev
   ```
8. Open http://localhost:5173.

The Identity registration endpoint is Development-only and is not exposed as public self-registration in the employee UI. Production onboarding remains HR-controlled.

## Validation

```bash
npm run check
npm run build
```
