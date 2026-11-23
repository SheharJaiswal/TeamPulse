# Tasks: Optional Employee Team Membership

**Status:** Implementation committed; automated CI verification pending.

## SDD and policy
- [x] Record owner-approved rule: organization required, team optional.
- [x] Update ADR-0002, open questions, Identity service specification and invitation specification.
- [x] Define acceptance criteria and backward-compatible migration.

## Implementation
- [x] Make Employee and EmployeeInvitation TeamId nullable.
- [x] Make invitation and employee response contracts accept/return nullable TeamId.
- [x] Allow omitted/blank TeamId in HR invitation API and Development-only bootstrap registration.
- [x] Make JWT team_id claim conditional on team assignment.
- [x] Add additive MySQL migration and update EF model snapshot.
- [x] Update SvelteKit HR invitation form/server action and unassigned display.
- [x] Update Identity API contract and project status.

## Tests and verification
- [x] Add invitation API test for omitted TeamId and SQL NULL persistence.
- [x] Add domain test for invitation without TeamId.
- [x] Add activation integration test for unassigned employee and successful login.
- [ ] Verify Release build and complete .NET test suite in CI.
- [ ] Verify frontend checks and production build in CI.
- [ ] Verify Docker Compose configuration.
