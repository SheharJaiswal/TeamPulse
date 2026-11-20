# Proposal: Remove Obsolete PostgreSQL Bootstrap Artifact

## Problem
The repository has adopted MySQL 8.4 with EF Core 8 and Pomelo for transactional service databases. The active local Compose setup, Identity API configuration, provider dependencies and ADR all use MySQL, but an orphaned PostgreSQL initialization script remains under `infrastructure/docker/postgres/`. It creates a PostgreSQL-specific user and database with unrelated local credentials, which can mislead contributors and contradict the approved persistence baseline.

## Scope
Remove the unused PostgreSQL bootstrap SQL file and document the repository hygiene decision. Preserve the MySQL Compose service, Pomelo provider, checked-in EF migration and startup migration behavior unchanged.

## Out of scope
Changing database technology, modifying MySQL credentials/configuration, introducing production database provisioning, or changing service boundaries.

## Success criteria
- The PostgreSQL bootstrap SQL artifact is removed from the feature branch.
- No active source, runtime, deployment or developer documentation depends on that script.
- MySQL 8.4 remains the sole local relational database bootstrap.
- Identity persistence and migration behavior remain unchanged.
