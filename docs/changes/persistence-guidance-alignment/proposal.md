# Proposal: Align persistence guidance with the accepted MySQL baseline

## Context
The repository's accepted backend decision, README and Identity implementation use MySQL 8.4 with EF Core 8 and Pomelo. The mandatory AGENTS.md backend conventions still prescribe PostgreSQL/Npgsql, creating contradictory instructions for future contributors and automated development runs.

## Goal
Make the repository's mandatory engineering guidance consistent with the accepted MySQL decision without changing runtime behavior.

## Scope
- Update backend conventions in AGENTS.md to specify MySQL 8.4, EF Core 8 and Pomelo.
- Preserve the database-per-service rule and the accepted DynamoDB analytics read-model choice.
- Update project status with a durable continuation checkpoint and record that runtime verification remains outstanding.

## Out of scope
- Changing providers, package versions, migrations or application code.
- Claiming build, migration or integration verification without executing it.
