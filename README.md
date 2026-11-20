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

Start MySQL and the API:
```bash
docker compose up -d mysql
dotnet restore backend/TeamPulse.sln
dotnet build backend/TeamPulse.sln
dotnet run --project backend/src/Services/Identity/Identity.API
```

The Identity API applies all pending, checked-in EF Core migrations automatically during startup before accepting requests. For local development, the MySQL container creates the `teampulse_identity` database and `teampulse_app` user. The API uses `Server=localhost;Port=3306` when run on the host. If the API is containerized on the Compose network, use `Server=mysql;Port=3306`.

Swagger is enabled in Development. Configure orchestrator liveness against `/health/live` (process-only) and readiness against `/health/ready` (Identity MySQL connectivity). The original `/health` endpoint remains as a compatibility alias for readiness. Health responses expose aggregate status only. Local database credentials are development placeholders. The JWT signing key is intentionally absent from checked-in configuration and must be supplied through `Jwt__SigningKey`. Production secrets must come from AWS Secrets Manager or an equivalent secret store. The POST /api/identity/register bootstrap endpoint is enabled only when ASPNETCORE_ENVIRONMENT=Development; it returns 404 in Testing, Staging, Production and other non-Development environments. It must be replaced with HR provisioning before production.

### Automated tests

Run the full test suite with `dotnet test backend/TeamPulse.sln`. Unit tests do not require external services. Identity API integration tests use Testcontainers to start a disposable MySQL 8.4 container, so Docker Desktop or another compatible Docker daemon must be running. The `.NET 8 CI` GitHub Actions workflow runs restore, build and tests on a Docker-capable GitHub-hosted runner.

### Migration policy

Create and commit migrations when the model changes. Startup migration application is enabled for local development and a single app instance. For production with multiple replicas, run migrations as a coordinated deployment job before rolling out application instances.
