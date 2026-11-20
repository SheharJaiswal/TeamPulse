# Design: Remove Obsolete PostgreSQL Bootstrap Artifact

## Repository assessment
The branch tree contains one file under `infrastructure/docker/postgres/`: `init/001-create-identity-db.sql`. Its content uses PostgreSQL-specific `CREATE USER` and `CREATE DATABASE ... OWNER` syntax and credentials unrelated to the active application configuration.

The active local runtime uses the `mysql:8.4` service in root `docker-compose.yml`; Identity uses `Pomelo.EntityFrameworkCore.MySql`, and ADR-0001 explicitly approves MySQL 8.4 / EF Core 8 / Pomelo. README, AGENTS, API Dockerfile, CI workflow, Identity Program/configuration, service ownership, communication conventions, Identity spec and API contract do not depend on the PostgreSQL script.

## Implementation
Delete only the orphaned SQL file. Git automatically removes the now-empty directories from the tracked tree. Do not change MySQL or EF migration files.

## Validation
Inspect the updated Git tree and search active configuration/documentation references. Run the existing .NET 8 GitHub Actions workflow, which restores, builds, validates Docker Compose and runs unit plus MySQL Testcontainers integration tests.
