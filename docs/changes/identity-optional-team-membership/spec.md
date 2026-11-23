# Specification: Optional Employee Team Membership

## Business rules
1. Every employee must belong to exactly one Identity-owned organization as represented by the existing OrganizationId field.
2. Team membership is optional. A missing, null, empty or whitespace-only TeamId is normalized to null.
3. A supplied TeamId is trimmed and must not exceed 100 characters.
4. An employee without a team can complete HR invitation activation and receives the fixed Employee role.
5. Existing employees and pending invitations retain their current TeamId values through migration.
6. Team assignment is not added or changed by this increment. A later assignment workflow must be separately authorized and specified.
7. Team membership does not gate employee-owned personal capabilities.
8. When TeamId is null, Identity omits the team_id JWT claim; /me and employee/invitation summaries represent the team as null.
9. OrganizationId remains required. HR-only invitation authorization, token handling, expiry, activation, delivery and lifecycle behavior remain unchanged.

## API and UI
- POST /api/identity/invitations accepts an omitted, null or blank teamId.
- A supplied teamId longer than 100 characters is rejected before persistence.
- Invitation summaries include nullable teamId.
- The HR invitation form labels Team ID as optional, allows it to be blank and shows "Not assigned" in invitation details.
- Development-only POST /api/identity/register accepts an omitted teamId for consistency; it remains unavailable outside Development.

## Acceptance criteria
- OTM-01: HR can create an invitation with no teamId.
- OTM-02: The persisted invitation stores TeamId as SQL NULL when omitted or blank.
- OTM-03: Successful activation creates an active Employee with the required OrganizationId, Employee role and TeamId SQL NULL.
- OTM-04: The activated employee can authenticate; the resulting JWT has no team_id claim.
- OTM-05: Existing non-null employee and invitation TeamId values are preserved by the migration.
- OTM-06: Existing invitation behavior, authorization, token safety, expiry, resend/cancel and delivery semantics are unchanged.
- OTM-07: Supplied TeamId values over 100 characters are rejected; organizationId remains required.
- OTM-08: HR UI permits a blank Team ID and clearly displays "Not assigned".
- OTM-09: Release build, full .NET tests, frontend checks/build and Compose validation pass.

## Explicit exclusions
No new team-management workflow, team assignment mutation, personal skills feature, employee role change or cross-service event/schema change is introduced.
