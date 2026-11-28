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
| Insight | Employee-private daily activity summaries and lifecycle read models | MySQL (activity summary), DynamoDB (lifecycle projection) |
| Notification | Preferences, notification records and delivery history | Notification DB |

Do not create a separate Activity Tracking service. Lambda is compute, not a seventh microservice or independent business-data owner.

## Product policy baseline

The accepted MVP product defaults are recorded in [ADR-0002](docs/decisions/0002-product-policy-baseline.md): HR-controlled onboarding, Identity-owned employee records, normalized challenge completion scoring, minimum 10-person insight cohorts, self-reported activity by default, reusable employee-owned habits, privacy-limited manager access, documented retention defaults and HR-controlled account lifecycle. Retention defaults require jurisdiction-specific privacy/legal review before production. ADR-0002 records policy decisions; it does not imply that HRIS integration, lifecycle audit/outbox, retention jobs or downstream revocation are already implemented.

## Technology

.NET 8 / ASP.NET Core, EF Core 8 / Pomelo MySQL provider, MySQL 8.4, JWT Bearer and SvelteKit. Docker Compose is reserved for optional local infrastructure dependencies (for example RabbitMQ); application services and MySQL run natively on the developer machine. Target AWS: API Gateway, EventBridge, Lambda, ECS, RDS for MySQL and DynamoDB. .NET 8 is the initial compatibility baseline.

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

From the repository root, execute the checked-in database setup scripts. They create the Habit, Challenge, Event Tracking, Notification and Insight databases and grant the application user access:

PowerShell:
```powershell
Get-Content .\\infra\\mysql\\init\\01-habit-database.sql | mysql -u root -p
Get-Content .\\infra\\mysql\\init\\02-challenge-database.sql | mysql -u root -p
Get-Content .\\infra\\mysql\\init\\03-event-tracking-database.sql | mysql -u root -p
Get-Content .\\infra\\mysql\\init\\04-notification-database.sql | mysql -u root -p
Get-Content .\\infra\\mysql\\init\\05-insight-database.sql | mysql -u root -p
```

Bash:
```bash
mysql -u root -p < infra/mysql/init/01-habit-database.sql
mysql -u root -p < infra/mysql/init/02-challenge-database.sql
mysql -u root -p < infra/mysql/init/03-event-tracking-database.sql
mysql -u root -p < infra/mysql/init/04-notification-database.sql
mysql -u root -p < infra/mysql/init/05-insight-database.sql
```

The local connection strings in each API's Development configuration use `localhost:3306`, database-specific names, and the local-only `teampulse_app` credentials. If you choose different MySQL credentials, override the corresponding `ConnectionStrings__<Name>` environment variable rather than editing committed configuration.

### 2. Start and configure RabbitMQ (optional, for cross-service event delivery)

RabbitMQ is the only application dependency in the standard local workflow that this guide runs in Docker. The Compose service uses the official RabbitMQ 4.1 management image and mounts the repository's broker configuration and definitions:

- `infra/rabbitmq/rabbitmq.conf` loads the definitions file at broker startup.
- `infra/rabbitmq/definitions.json` declares the durable topic exchange, service queues, routing-key bindings, dead-letter exchange and dead-letter queues.
- The named queues and bindings are provisioned automatically when the container starts. You do not need to create them manually in the management UI.

Start only RabbitMQ from the repository root:

```bash
docker compose up -d rabbitmq
docker compose ps rabbitmq
docker compose logs -f rabbitmq
```

Local connection details:

| Setting | Value |
|---|---|
| AMQP host/port | `localhost:5672` |
| Management UI | http://localhost:15672 |
| Username | `teampulse` |
| Password | `local_rabbitmq_password` |
| Virtual host | `/` |
| Topic exchange | `teampulse.domain.events` |
| Dead-letter exchange | `teampulse.domain.events.dlx` |

These credentials are for local development only. Do not reuse them outside your machine. The RabbitMQ data is persisted in the `teampulse-rabbitmq-data` Docker volume. To stop RabbitMQ without deleting its data, run `docker compose stop rabbitmq`. To remove the container while retaining the volume, run `docker compose rm -f rabbitmq`. Do not remove the volume unless you intentionally want to reset local broker data.

#### Existing exchange and queue topology

The definitions file creates the following topology:

| Queue | Bound routing key | Purpose |
|---|---|---|
| `teampulse.habit.events` | `identity.employee.lifecycle` | Habit employee lifecycle projection |
| `teampulse.challenge.events` | `identity.employee.lifecycle` | Challenge employee lifecycle projection |
| `teampulse.event-tracking.events` | `identity.employee.lifecycle` | Event Tracking employee lifecycle projection |
| `teampulse.insight.events` | `identity.employee.lifecycle` | Insight employee lifecycle projection |
| `teampulse.notification.events` | `identity.employee.lifecycle` | Notification employee lifecycle projection |
| `teampulse.activity.events` | `habit.activity.recorded` | Activity event consumers |

The Habit, Challenge, Event Tracking and Notification lifecycle queues have corresponding dead-letter queues. The activity queue also has `teampulse.activity.events.dlq`. The configured quorum queues use a delivery limit of five and dead-letter routing. These definitions are loaded from source control; update the definitions file through an approved architecture change if the topology needs to change.

#### Enable local publishers and consumers

RabbitMQ being healthy does not automatically turn on message publishing or consumption. The checked-in service configuration keeps these components disabled by default so the APIs can be developed and run without a broker. Set the following environment variables in the terminal for each service you want to exercise. Keep the AMQP URI consistent with the local broker credentials above.

| Service | Environment variables to enable |
|---|---|
| Identity | `RabbitMq__Uri=amqp://teampulse:local_rabbitmq_password@localhost:5672/`; `OutboxDispatcher__Enabled=true` |
| Habit | `HabitConsumer__Enabled=true` |
| Challenge | `ChallengeConsumer__Enabled=true` |
| Event Tracking | `EventTrackingConsumer__Enabled=true`; `EventTrackingOutbox__Enabled=true`; `EventTrackingPublisher__Uri=amqp://teampulse:local_rabbitmq_password@localhost:5672/` |
| Insight | `InsightConsumer__Enabled=true`; `InsightConsumer__Uri=amqp://teampulse:local_rabbitmq_password@localhost:5672/` |
| Notification | `NotificationConsumer__Enabled=true`; `NotificationConsumer__Uri=amqp://teampulse:local_rabbitmq_password@localhost:5672/` |

For example, in the PowerShell terminal used to run Identity:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:Jwt__SigningKey = "<the-shared-local-base64-signing-key-generated-in-step-3>"
$env:RabbitMq__Uri = "amqp://teampulse:local_rabbitmq_password@localhost:5672/"
$env:OutboxDispatcher__Enabled = "true"
dotnet run --project backend/src/Services/Identity/Identity.API --urls http://localhost:5101
```

For Habit, Challenge, Event Tracking, Insight or Notification, set that service's variables from the table in its own terminal before running its `dotnet run` command. Environment variables use the .NET double-underscore convention to override nested `appsettings.json` values. The default exchange, queue names and routing keys are already in the checked-in service configuration and broker definitions; do not create alternate names in local settings.

Only enable the consumers and outbox dispatchers you are actively testing. For a full local lifecycle-event flow, start RabbitMQ first, then enable the Identity publisher and the relevant downstream consumers. For activity-event publishing, enable the Event Tracking outbox and its publisher configuration as well. A service that is not enabled will not consume messages merely because its queue exists.

Do not start the full Compose stack for normal development. MySQL is expected to run as a native local service. DynamoDB Local and Mailpit are separate optional dependencies and are not started by the RabbitMQ command above.

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

The SvelteKit UI calls Identity, Habit, Event Tracking and Challenge through the URLs in `frontend/.env.example`. HR administrators can open `/hr/challenges` to create and list organization challenges; the Challenge API validates the HR role and scopes records to the caller's `organization_id` claim. From the repository root:

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


## Insight activity summary (development)

Insight keeps the existing employee-lifecycle projection in DynamoDB. The new employee-private activity summary is stored separately in MySQL database `teampulse_insight`.

- On a fresh MySQL volume, `infra/mysql/init/05-insight-database.sql` creates the Insight database and grants the app user access. For an existing volume, create the database/grant manually or rerun that SQL as a privileged MySQL user.
- Insight API applies its EF Core migration on startup.
- Configure `Jwt__SigningKey` as an environment variable or user secret. Use the same signing key as Identity and at least 32 UTF-8 bytes; do not commit the key.
- In Development, the activity RabbitMQ consumer is enabled and reads `teampulse.insight.activity.events`. Ensure the RabbitMQ definitions are loaded so the dedicated queue and DLQ bindings exist.
- The self-only endpoint is `GET /api/insights/activity/daily`. The activity consumer has its own `/health/activity-consumer` health endpoint, separate from the existing lifecycle consumer health. It defaults to the latest 7 UTC calendar days; optional `fromDate` and `toDate` must be supplied together and may cover at most 30 inclusive days.

The summary counts activity submissions by category; it does not sum activity quantity or replace Challenge Service goal/progress.
