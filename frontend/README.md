# TeamPulse Web

SvelteKit employee workspace for the TeamPulse MVP.

## Current scope

- Sign in through the Identity API.
- Store the access token in an HTTP-only, same-site session cookie.
- Load the authenticated employee profile and personal habits.
- Create daily or weekly habits.
- Archive personal habits.
- Display active and archived habits.

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
4. Start Habit on port 5102 in a second terminal, using the exact same JWT signing key:
   ```bash
   dotnet run --project backend/src/Services/Habit/Habit.API --urls http://localhost:5102
   ```
5. Copy `.env.example` to `.env` in this directory if you need to override the API URLs.
6. Install and start the web application:
   ```bash
   cd frontend
   npm install
   npm run dev
   ```
7. Open http://localhost:5173.

The Identity registration endpoint is Development-only and is not exposed as public self-registration in the employee UI. For a disposable local environment, an HR/engineering operator can bootstrap a test employee directly through the Identity Development-only endpoint. Production onboarding remains HR-controlled.

## Validation

```bash
npm run check
npm run build
```
