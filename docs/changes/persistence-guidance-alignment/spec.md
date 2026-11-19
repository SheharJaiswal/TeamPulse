# Specification: Persistence guidance alignment

## Requirement PG-01
Given a contributor reads the mandatory backend conventions, when they choose a persistence provider for transactional service databases, then the guidance must specify MySQL 8.4 with EF Core 8 and Pomelo.

## Requirement PG-02
The guidance must retain database-per-service ownership and must not imply that services may share databases in production.

## Requirement PG-03
The guidance must remain consistent with ADR-0001 and README.md.

## Acceptance
- AGENTS.md no longer instructs contributors to use PostgreSQL or Npgsql.
- Existing architecture constraints, security guidance and event conventions remain unchanged.
- Project status clearly identifies completed work, outstanding verification, blockers and the next task.
