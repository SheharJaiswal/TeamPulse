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

## Technology
.NET 8 / ASP.NET Core, EF Core / Npgsql, PostgreSQL, JWT Bearer, Docker Compose. Target AWS: API Gateway, EventBridge, Lambda, ECS, RDS, DynamoDB and CloudWatch. .NET 8 is the initial compatible baseline; upgrade SDK, target frameworks and dependencies together after environment validation.

## Local development
Prerequisites: .NET 8 SDK and Docker Desktop.

```bash
docker compose up -d postgres
dotnet restore backend/TeamPulse.sln
dotnet build backend/TeamPulse.sln
```

See `docs/changes/identity-service-foundation/tasks.md` for EF migration commands. Run Identity API with:

```bash
dotnet run --project backend/src/Services/Identity/Identity.API
```

Swagger is enabled in Development; health endpoint is `/health`. Local database credentials and JWT key are development placeholders. Production secrets must come from AWS Secrets Manager or an equivalent secret store. Public registration is only a local bootstrap mechanism and must be replaced with HR provisioning before production.
