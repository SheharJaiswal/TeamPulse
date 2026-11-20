# Specification: Remove Obsolete PostgreSQL Bootstrap Artifact

## Acceptance criteria
- PG-01: `infrastructure/docker/postgres/init/001-create-identity-db.sql` is absent from the repository tree.
- PG-02: The tracked repository contains no remaining PostgreSQL bootstrap artifacts.
- PG-03: Docker Compose continues to define the MySQL 8.4 local database service.
- PG-04: Identity continues to use EF Core 8 with Pomelo and applies checked-in migrations at startup.
- PG-05: No active source, deployment or developer documentation references the removed script or its PostgreSQL-only credentials.

## Constraints
- Do not alter MySQL configuration, EF mappings, migrations, API behavior or the six-service architecture.
- Do not infer production provisioning requirements from the obsolete local SQL script.
- Validate through repository tree/content inspection and the existing Docker-capable .NET 8 CI workflow.
