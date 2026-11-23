# Proposal: Optional Employee Team Membership

## Problem
The HR employee invitation workflow currently requires a Team ID before an employee can be created. This prevents onboarding an employee who belongs to an organization but has not joined a team yet, even though personal employee capabilities do not require team membership.

## Decision
An employee must belong to an organization, but team membership is optional. HR can create and activate an employee without a team assignment. A team can be assigned later through a separately authorized workflow when that capability is defined; this change does not add a team-assignment endpoint or workflow.

## Scope
- Make Team ID optional in HR invitation request/summary contracts, Identity persistence and employee activation.
- Allow the HR invitation page to submit an empty Team ID and display an explicit "Not assigned" state.
- Preserve existing team assignments without rewriting existing data.
- Make the Identity JWT team_id claim conditional on an employee having a team assignment.
- Keep organization membership required and keep the Employee role fixed on invitation activation.
- Update Identity API/domain specifications, ADR-0002 and project status.
- Add API, domain and activation tests for employees without a team.

## Out of scope
- Team creation, team assignment/removal endpoints, team membership history or multiple-team membership.
- Changes to organization ownership, HR authority, role assignment or employee lifecycle events.
- New personal skills or other employee features; the policy only ensures those personal capabilities are not gated by team membership.
