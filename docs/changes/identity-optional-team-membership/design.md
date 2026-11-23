# Design: Optional Employee Team Membership

## Domain and contracts
- Change Employee.TeamId and EmployeeInvitation.TeamId to nullable strings.
- Keep OrganizationId required and bounded to 100 characters.
- Change RegisterRequest, EmployeeSummary, CreateEmployeeInvitationRequest and EmployeeInvitationSummary to expose nullable TeamId. The optional request parameter defaults to null for source compatibility.
- Normalize omitted/empty/whitespace TeamId to null at the Identity boundary/domain constructor.
- During activation, copy the nullable invitation TeamId to the Employee unchanged.

## Persistence
- Update EF Core mappings for employees.TeamId and employee_invitations.TeamId to nullable varchar(100).
- Add an additive migration that alters both columns to nullable without changing existing values.
- Update the EF model snapshot to match. No table, index, key, or cross-service database change is required.
- Migration rollback maps null values to an empty string before restoring NOT NULL, because the prior schema cannot represent an unassigned employee.

## Authentication
- Build the existing JWT claims as before, except add team_id only when Employee.TeamId is non-null.
- The /me endpoint naturally returns null for teamId when the claim is absent.
- No event envelope or lifecycle event payload changes; employee status projections remain independent of team assignment.

## Frontend
- HR invitation server action treats teamId as optional, rejects values longer than 100 characters, and sends null when blank.
- Invitation result renders "Not assigned" when teamId is null.
- No new team selector or team-management capability is introduced.

## Verification
- Add API integration coverage for omitted teamId and persistence as null.
- Add activation integration coverage proving null team assignment survives activation and the employee can log in.
- Add a domain test for invitation construction without a team.
- Existing tests continue covering assigned-team invitations and activation, preserving the previous path.
