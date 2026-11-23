# TeamPulse

Corporate Wellness and Team Engagement Platform.

## Development workflow: Specification-Driven Development

The approved specifications are the source of truth. Before implementing any feature, read `AGENTS.md`, the relevant domain specification, service ownership and API/event contracts. Create `docs/changes/<change-id>/proposal.md`, `spec.md`, `design.md` and `tasks.md`; map acceptance criteria to tests; implement only the approved scope; update contracts and `docs/project-status.md`. Record unresolved requirements in `docs/open-questions.md` rather than inventing business rules.

## Services

| Service | Responsibility | Data ownership |
|---|---|---|
| Identity | Employees, organizations, teams, roles and authentication | Identity DB |
| Habit | Employee-owned personal habits and schedules | Habit DB |
| Challenge | HR programmes, tasks, eligibility, participation and authoritative challenge state | Challenge DB |
| Event Tracking | Activity submissions, challenge task completions and activity audit | Event Tracking DB |
| Insight | Aggregated team/organization metrics and read models | DynamoDB |
| Notification | Preferences, notification records and delivery history | Notification DB |

Do not create a separate Activity Tracking service. Lambda is compute, not a seventh microservice or independent business-data owner.

## Product policy baseline

The accepted MVP product defaults are recorded in [ADR-0002](docs/decisions/0002-product-policy-baseline.md): HR-controlled onboarding, Identity-owned employee records, normalized challenge completion scoring, minimum 10-person insight cohorts, self-reported activity by default, reusable employee-owned habits, privacy-limited manager access, documented retention defaults and HR-controlled account lifecycle. Retention defaults require jurisdiction-specific privacy/legal review before production. ADR-0002 records policy decisions; it does not imply that HRIS integration, lifecycle audit/outbox, retention jobs or downstream revocation are already implemented.

## Technology

.NET 8 / ASP.NET Core, EF Core 8 / Pomelo MySQL provider, MySQL 8.4, JWT Bearer and Docker Compose. Target AWS: API Gateway, EventBridge, Lambda, ECS, RDS for MySQL and DynamoDB. .NET 8 is the initial compatibility baseline.

## AWS infrastructure

The Identity employee-lifecycle EventBridge fan-out template and deployment gates are documented in [infra/cloudformation/README.md](infra/cloudformation/README.md). The stack provisions the bus, scoped Identity publisher permission, five service-owned SQS targets, target-delivery DLQs and AWS-managed delivery alarms. It does not deploy consumers or enable the dispatcher.

## Local development (without Docker for application services)

Run every TeamPulse API and the SvelteKit frontend directly on your machine. Docker is not required for the application processes. Use a locally installed MySQL 8.4 server for persistence. RabbitMQ is the only dependency in the standard local workflow that is started with Docker; it can also be installed and run natively if preferred.

### Prerequisites

- .NET 8 SDK
- Node.js 20+ and npm
- MySQL Server 8.4 running locally on port 3306
- Docker Desktop (only if you want to run RabbitMQ in Docker)
- Git

Check the installations:

```powershell
dotnet --version
node --version
npm --version
mysql --version
```

### 1. Create the local MySQL databases and user

Connect to your local MySQL server as an administrator:

```sql
CREATE DATABASE IF NOT EXISTS teampulse_identity CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci;
CREATE USER IF NOT EXISTS 'teampulse_app'@'%' IDENTIFIED BY 'local_teampulse_password';
GRANT ALL PRIVILEGES ON teampulse_identity.* TO 'teampulse_app'@'%';
```

From the repository root, execute the checked-in database setup scripts. They create the Habit, Challenge, Event Tracking and Notification databases and grant the application user access:

PowerShell:
```powershell
Get-Content .\\infra\\mysql\\init\\01-habit-database.sql | mysql -u root -p
Get-Content .\\infra\\mysql\\init\\02-challenge-database.sql | mysql -u root -p
Get-Content .\\infra\\mysql\\init\\03-event-tracking-database.sql | mysql -u root -p
Get-Content .\\infra\\mysql\\init\\04-notification-database.sql | mysql -u root -p
```

Bash:
```bash
mysql -u root -p < infra/mysql/init/01-habit-database.sql
mysql -u root -p < infra/mysql/init/02-challenge-database.sql
mysql -u root -p < infra/mysql/init/03-event-tracking-database.sql
mysql -u root -p < infra/mysql/init/04-notification-database.sql
```

The local connection strings in each API's Development configuration use `localhost:3306`, database-specific names, and the local-only `teampulse_app` credentials. If you choose different MySQL credentials, override the corresponding `ConnectionStrings__<Name>` environment variable rather than editing committed configuration.

### 2. Start RabbitMQ (optional, for cross-service event delivery)

The APIs can run without RabbitMQ when their consumers/outbox dispatchers are disabled (the checked-in defaults). To test local event delivery, start only the RabbitMQ dependency:

```bash
docker compose up -d rabbitmq
```

Management UI: http://localhost:15672  
Local-only credentials: `teampulse` / `local_rabbitmq_password`

Do not start the full Compose stack for normal development. MySQL is expected to run as a native local service. RabbitMQ consumers and dispatchers are disabled by default; enable only the components you are actively testing. For example, Identity's dispatcher can be enabled with `OutboxDispatcher__Enabled=true` and `OutboxDispatcher__Transport=RabbitMq`. Consumer configuration is service-specific and is shown in the relevant service section below.

### 3. Configure the shared JWT signing key

Use the same signing key for Identity and every API that validates Identity-issued tokens. Generate a local key; do not commit it.

PowerShell (run in each backend terminal):
```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Jwt__SigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Bash (run in each backend terminal):
```bash
export ASPNETCORE_ENVIRONMENT=Development
export Jwt__SigningKey="$(openssl rand -base64 32)"
```

For multiple terminal windows, generate the key once and set the same value in each terminal. Do not generate a different key per service.

### 4. Restore and build the backend

From the repository root:

```bash
dotnet restore backend/TeamPulse.sln
dotnet build backend/TeamPulse.sln
```

### 5. Run the APIs directly on your machine

Open a separate terminal for each API. Set `ASPNETCORE_ENVIRONMENT=Development` and the shared `Jwt__SigningKey` in each terminal before running the command.

| Service | Local URL | Command (from repository root) |
|---|---|---|
| Identity API | http://localhost:5101 | `dotnet run --project backend/src/Services/Identity/Identity.API --urls http://localhost:5101` |
| Habit API | http://localhost:5102 | `dotnet run --project backend/src/Services/Habit/Habit.API --urls http://localhost:5102` |
| Event Tracking API | http://localhost:5103 | `dotnet run --project backend/src/Services/EventTracking/EventTracking.API --urls http://localhost:5103` |
| Challenge API | http://localhost:5104 | `dotnet run --project backend/src/Services/Challenge/Challenge.API --urls http://localhost:5104` |
| Notification API | http://localhost:5105 | `dotnet run --project backend/src/Services/Notification/Notification.API --urls http://localhost:5105` |
| Insight API | http://localhost:5106 | `dotnet run --project backend/src/Services/Insight/Insight.API --urls http://localhost:5106` |

The first five APIs use the local MySQL databases listed above. Their checked-in EF Core migrations are applied by their hosts where configured. The Insight service uses DynamoDB for its read model; configure its DynamoDB endpoint/credentials and table before exercising Insight functionality. Cloud AWS resources are not provisioned by this local setup.

Swagger is enabled in Development for APIs that expose Swagger. Health endpoints are service-specific; Identity, Habit and Challenge expose `/health/live` and `/health/ready`.

### 6. Run the SvelteKit frontend

The employee UI calls Identity, Habit and Event Tracking through the URLs in `frontend/.env.example`. From the repository root:

PowerShell:
```powershell
Copy-Item frontend/.env.example frontend/.env
cd frontend
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

### 7. Run tests

Unit tests do not require Docker. Identity API integration tests use Testcontainers and therefore require a working Docker daemon. Run:

```bash
dotnet test backend/TeamPulse.sln
```

Frontend validation:

```bash
cd frontend
npm run check
npm run build
```

### 8. Local configuration and production notes

- Keep secrets, local environment files, generated keys and user-specific IDE files out of source control. The root `.gitignore` excludes common .NET, Node/Svelte, IDE, test and local-secret artifacts.
- The dispatcher remains disabled by default. For local RabbitMQ event delivery, explicitly configure the relevant publisher/consumer and dispatcher settings. EventBridge provisioning is deferred until cloud infrastructure setup; no AWS credentials are needed for the Identity/Habit/Challenge MySQL APIs.
- The Identity API applies its checked-in EF Core migrations at startup before accepting requests. Startup migration application is intended for local development and a single app instance; production multi-replica deployments should run migrations as a coordinated deployment job.
- The POST `/api/identity/register` bootstrap endpoint is enabled only when `ASPNETCORE_ENVIRONMENT=Development`; it returns 404 in Testing, Staging, Production and other non-Development environments. It is not public self-registration; production onboarding remains HR-controlled.
- Production secrets must come from AWS Secrets Manager or an equivalent secret store. Local credentials shown here are development-only placeholders.

## Employee web application

The employee-facing SvelteKit application lives in [frontend](frontend/README.md). It provides sign-in and the personal habit workspace. For local startup, run MySQL and the Identity/Habit APIs, then:

```bash
cd frontend
cp .env.example .env
npm install
npm run dev
```

Open http://localhost:5173. See [frontend/README.md](frontend/README.md) for shared JWT signing-key configuration and backend port details.
