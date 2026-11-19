# TeamPulse Engineering Instructions

## SDD is mandatory
Before every feature, read this file, `docs/specs/`, ownership docs and contracts. Create `docs/changes/<id>/proposal.md`, `spec.md`, `design.md`, and `tasks.md` before coding. Specs are source of truth. Map acceptance criteria to tests, update contracts and `docs/project-status.md`, and record unresolved business rules in `docs/open-questions.md`.

## Architecture constraints
- Exactly six services: Identity, Habit, Challenge, Event Tracking, Insight, Notification. No separate Activity Tracking service.
- Each service owns its database; no cross-service database access or foreign keys.
- Challenge owns programmes, tasks, participation and authoritative challenge state.
- Event Tracking owns submitted activity records and audit history.
- Insight owns aggregated/derived read models only.
- Lambda is compute, not a seventh microservice or business-data owner.
- Validate JWT issuer, audience, signature and expiry. Take employee identity from validated `sub`, never client-supplied employeeId.
- Each service enforces tenant/resource authorization. Personal wellness data is private.

## Backend conventions
- ASP.NET Core .NET 8 baseline; Clean Architecture (API, Application, Domain, Infrastructure).
- EF Core + Npgsql, PostgreSQL transactional storage.
- Thin controllers; business logic in Domain/Application; persistence in Infrastructure.
- DTOs, validation, async I/O and CancellationToken. Do not expose EF entities.
- PasswordHasher; never log secrets/tokens. Deployed secrets from Secrets Manager/environment.

## Events
- Event envelope includes eventId, eventType, schemaVersion, occurredAtUtc, correlationId and payload.
- Minimal versioned payloads; no credentials, JWTs or password hashes.
- At-least-once delivery: consumers must be idempotent. Use transactional outbox, retries and DLQs. Never claim exactly-once.

## Tests and validation
- Unit, integration and contract tests; cover authorization, tenant isolation, invalid input, duplicate messages and failures.
- Do not claim build/test passed unless executed.
- Validate with `dotnet restore backend/TeamPulse.sln`, `dotnet build backend/TeamPulse.sln --no-restore`, `dotnet test backend/TeamPulse.sln --no-build`, `docker compose config`.
