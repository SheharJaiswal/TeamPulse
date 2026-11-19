# ADR-0001: Backend and Persistence Baseline

- Status: Accepted.
- ASP.NET Core .NET 8 initial compatibility baseline.
- PostgreSQL via EF Core/Npgsql for transactional service databases.
- Database-per-service: separate logical databases and application credentials. A shared local PostgreSQL container is acceptable for development; production ownership remains isolated.
- DynamoDB for analytics projections.
- Upgrade to .NET 10 only after SDK and dependencies are validated in local and CI environments.