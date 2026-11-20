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

## Local development

Prerequisites: .NET 8 SDK and Docker Desktop.

Set a local JWT signing key in the process environment before starting the API. Do not commit a signing key to appsettings or source control.

PowerShell:
```powershell
$env:Jwt__SigningKey = [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Bash:
```bash
export Jwt__SigningKey="$(openssl rand -base64 32)"
```

Start MySQL and the local RabbitMQ broker:
```bash
docker compose up -d mysql rabbitmq
dotnet restore backend/TeamPulse.sln
dotnet build backend/TeamPulse.sln
```

For local cross-service event delivery, explicitly enable the outbox worker and select RabbitMQ. PowerShell:
```powershell
$env:OutboxDispatcher__Enabled = "true"
$env:OutboxDispatcher__Transport = "RabbitMq"
$env:RabbitMq__Uri = "amqp://teampulse:local_rabbitmq_password@localhost:5672/"
dotnet run --project backend/src/Services/Identity/Identity.API
```

Bash:
```bash
export OutboxDispatcher__Enabled=true
export OutboxDispatcher__Transport=RabbitMq
export RabbitMq__Uri="amqp://teampulse:local_rabbitmq_password@localhost:5672/"
dotnet run --project backend/src/Services/Identity/Identity.API
```

The local RabbitMQ management UI is available at http://localhost:15672 (local-only credentials: `teampulse` / `local_rabbitmq_password`). Compose imports one durable topic exchange and five service-owned local queues. RabbitMQ publishes the complete TeamPulse envelope as the message body and waits for publisher confirmation. A broker outage leaves the MySQL outbox retryable. Identity liveness/readiness remains independent of broker availability.

The dispatcher remains disabled by default. To use the production EventBridge adapter in a cloud deployment, explicitly set `OutboxDispatcher__Transport=EventBridge`, provide `EventBridge__BusName` and `AWS_REGION`, and use the workload's AWS SDK default credentials. EventBridge provisioning is deferred until cloud infrastructure setup; no AWS credentials are needed for local RabbitMQ development.

The Identity API applies all pending, checked-in EF Core migrations automatically during startup before accepting requests. For local development, the MySQL container creates the `teampulse_identity` database and `teampulse_app` user. The API uses `Server=localhost;Port=3306` when run on the host. If the API is containerized on the Compose network, use `Server=mysql;Port=3306`.

Swagger is enabled in Development. Configure orchestrator liveness against `/health/live` (process-only) and readiness against `/health/ready` (Identity MySQL connectivity). The original `/health` endpoint remains as a compatibility alias for readiness. Health responses expose aggregate status only. Local database credentials are development placeholders. The JWT signing key is intentionally absent from checked-in configuration and must be supplied through `Jwt__SigningKey`. Production secrets must come from AWS Secrets Manager or an equivalent secret store. The POST /api/identity/register bootstrap endpoint is enabled only when ASPNETCORE_ENVIRONMENT=Development; it returns 404 in Testing, Staging, Production and other non-Development environments. It must be replaced with HR provisioning before production.


### Habit employee-status projection foundation

Habit owns a separate logical MySQL database named `teampulse_habit`. The local MySQL initialization script creates that database and grants the local `teampulse_app` user access; production uses a separately provisioned Habit database credential. Habit applies its checked-in EF Core/Pomelo migrations at host startup before accepting requests.

Run the Habit API locally after starting MySQL:

PowerShell:
```powershell
$env:ConnectionStrings__HabitDatabase = "Server=localhost;Port=3306;Database=teampulse_habit;User=teampulse_app;Password=local_teampulse_password;"
dotnet run --project backend/src/Services/Habit/Habit.API
```

The Habit host exposes `/health/live` and `/health/ready`; readiness checks only the Habit database. The employee-status projection processor validates schema-v1 Identity lifecycle envelopes and atomically records the minimal event inbox marker with a strictly version-increasing projection. The RabbitMQ consumer adapter is the next implementation increment; this persistence foundation does not yet consume the queue.

### Automated tests

Run the full test suite with `dotnet test backend/TeamPulse.sln`. Unit tests do not require external services. Identity API integration tests use Testcontainers to start a disposable MySQL 8.4 container, so Docker Desktop or another compatible Docker daemon must be running. The `.NET 8 CI` GitHub Actions workflow runs restore, build and tests on a Docker-capable GitHub-hosted runner.

### Migration policy

Create and commit migrations when the model changes. Startup migration application is enabled for local development and a single app instance. For production with multiple replicas, run migrations as a coordinated deployment job before rolling out application instances.

### Habit RabbitMQ employee-lifecycle consumer

The Habit consumer is disabled by default. To run it locally, start the existing MySQL and RabbitMQ containers, then enable the Habit consumer explicitly. PowerShell:

```powershell
$env:HabitConsumer__Enabled = "true"
$env:HabitConsumer__Uri = "amqp://teampulse:local_rabbitmq_password@localhost:5672/"
$env:HabitConsumer__QueueName = "teampulse.habit.events"
dotnet run --project backend/src/Services/Habit/Habit.API
```

Bash:

```bash
export HabitConsumer__Enabled=true
export HabitConsumer__Uri="amqp://teampulse:local_rabbitmq_password@localhost:5672/"
export HabitConsumer__QueueName=teampulse.habit.events
dotnet run --project backend/src/Services/Habit/Habit.API
```

The Habit consumer uses manual acknowledgements and a prefetch of one. It acknowledges only after the projection processor has committed its MySQL inbox/projection transaction. Duplicate and stale events are successful outcomes; malformed events and exhausted processing retries are rejected to the Habit-owned quorum DLQ. Transient MySQL/network failures use bounded full-jitter retries. The host reconnects to RabbitMQ with a bounded delay if the broker is unavailable.

The Habit source queue is a quorum queue with a finite delivery limit and at-least-once dead-lettering to `teampulse.habit.events.dlq`. The DLX and DLQ are part of the local Compose definitions. Existing RabbitMQ volumes created before this topology change may retain the previous classic queue declaration; for a disposable local development environment only, stop Compose and recreate the RabbitMQ volume before starting the updated broker. Do not delete a volume containing messages or data you need to retain.

Habit database readiness remains independent of RabbitMQ. Use `/health/consumer` to inspect the separate consumer connection state; `/health/ready` continues to report only the Habit database. No AWS credentials or EventBridge resources are required for local event delivery.
