# ADR-0001: Backend and Persistence Baseline

- Status: Accepted.
- ASP.NET Core .NET 8 initial compatibility baseline.
- MySQL 8.4 via EF Core 8 and Pomelo.EntityFrameworkCore.MySql for transactional service databases.
- Database-per-service: separate logical databases and application credentials. A shared local MySQL container is acceptable for development; production ownership remains isolated.
- EF Core migrations are committed to source control and applied automatically by the Identity API at startup for local development and single-instance deployments.
- Production multi-instance deployments should use a coordinated migration job/step before application rollout to avoid concurrent schema migration attempts.
- DynamoDB for analytics projections.
- Upgrade framework/provider versions together only after SDK and dependencies are validated in local and CI environments.
