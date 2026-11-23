# TeamPulse Web

SvelteKit employee workspace for the TeamPulse MVP.

## Current scope

- Sign in through the Identity API and keep the token in an HTTP-only, same-site session cookie.
- Load the authenticated employee profile and personal habits.
- Create, edit and archive daily or weekly habits.
- Record self-reported habit activity and view a private activity history.
- Store activity submissions in Event Tracking; Habit remains the source of truth for habit definitions.

The browser never receives or stores the JWT. SvelteKit server load functions and form actions call the backend APIs with the HTTP-only cookie. This keeps API calls same-origin from the browser and avoids exposing backend service URLs or requiring browser CORS access.

## Local development (no Docker for application services)

Run the SvelteKit app and all .NET APIs directly on your machine. Install and start MySQL Server 8.4 locally; do not use the MySQL Compose container for this workflow. Docker is optional and is used only when you choose to run a supporting dependency such as RabbitMQ in a container.

### Prerequisites

- .NET 8 SDK
- Node.js 20+ and npm
- MySQL Server 8.4 running on `localhost:3306`
- Docker Desktop only if you want RabbitMQ in Docker

For the RabbitMQ Docker command, management UI, local credentials, existing exchange/queue topology and per-service environment variables, follow [the root README RabbitMQ setup](../README.md#2-start-and-configure-rabbitmq-optional-for-cross-service-event-delivery).

Create the local MySQL databases and application user by following [the root README database setup](../README.md#1-create-the-local-mysql-databases-and-user).

### Start the backend APIs

Open separate terminals from the repository root. Set the same JWT signing key in each backend terminal. Keep `ASPNETCORE_ENVIRONMENT=Development`.

PowerShell:
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Jwt__SigningKey = "<the-same-local-base64-signing-key-for-all-apis>"
dotnet run --project backend/src/Services/Identity/Identity.API --urls http://localhost:5101
```

Start Habit in another terminal:
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Jwt__SigningKey = "<the-same-local-base64-signing-key-for-all-apis>"
dotnet run --project backend/src/Services/Habit/Habit.API --urls http://localhost:5102
```

Start Event Tracking in another terminal:
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Jwt__SigningKey = "<the-same-local-base64-signing-key-for-all-apis>"
dotnet run --project backend/src/Services/EventTracking/EventTracking.API --urls http://localhost:5103
```

The JWT key must be identical across Identity and token-validating APIs. Generate it once (for example, with `[Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))`) and reuse that value in each terminal. Do not commit the key.

### Start the frontend

From the repository root:

PowerShell:
```powershell
Copy-Item frontend/.env.example frontend/.env
Set-Location frontend
npm install
npm run dev
```

Bash:
```bash
cp frontend/.env.example frontend/.env
cd frontend
npm install
npm run dev
```

Open http://localhost:5173.

The local API URLs are configured in `.env.example`:

- Identity: http://localhost:5101
- Habit: http://localhost:5102
- Event Tracking: http://localhost:5103

The Identity registration endpoint is Development-only and is not exposed as public self-registration in the employee UI. Production onboarding remains HR-controlled.

## Validation

From the `frontend` directory:

```bash
npm run check
npm run build
```
