# Habit Management Design

## Layers
- Domain: `HabitDefinition`, `HabitFrequency`, `HabitStatus`; invariants and lifecycle transitions.
- Application: `IHabitService`, request/response contracts and service orchestration.
- Infrastructure: EF Core repository and Habit-owned MySQL table.
- API: authenticated `HabitsController`, JWT validation and HTTP mapping.

## Persistence
Add `habits` table to `teampulse_habit` with UUID primary key, owner UUID index, bounded strings, frequency/status values and date/timestamp columns. No foreign key to Identity; owner is a stable external identifier.

## Authorization
JWT validation uses Identity's configured issuer/audience and `Jwt__SigningKey`. The validated `sub` claim is parsed as a GUID. Ownership is enforced in repository queries and again by service operations. The local employee-status projection is consulted; an explicit inactive status denies access. Until a complete active-employee snapshot is available, absent projections remain unknown, not inactive.

## Failure behavior
- Invalid body/domain data: 400.
- Missing/invalid authentication: 401.
- Unknown or other-owner resource: 404.
- Update of archived habit: 409.
- Unexpected persistence errors are not converted into success.

## Boundaries
Event Tracking owns activity submissions/completion records. Challenge owns challenge snapshots and participation. Habit does not publish completion events or read other service databases.
