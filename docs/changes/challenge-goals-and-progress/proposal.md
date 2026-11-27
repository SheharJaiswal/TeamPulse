# Proposal: Challenge goals and employee progress

## Problem
Employees can acknowledge and join a challenge, but the current challenge contract contains no measurable goal or action. They cannot tell what to do, record progress, or know when they have completed the challenge.

## Outcome
Add one measurable, individual, cumulative goal to each challenge. HR defines an action/instruction, a numeric target, and a unit when creating the challenge. Joined employees can submit self-reported quantities during the challenge window and see their total, remaining amount, and completion state.

## Scope
- Extend Challenge-owned challenge metadata with goal instructions, target quantity, and unit.
- Keep challenge activity submission records in Event Tracking, separate from Habit activity records.
- Validate employee challenge eligibility and challenge date/goal metadata through the Challenge API using the caller's bearer token.
- Provide employee progress and activity submission through server-side SvelteKit actions.
- Add tests and update service contracts/project status.

## Non-goals
- Habit linking, automatic device/health data ingestion, evidence uploads, manager review, team scoring, leaderboards, reward issuance, editing/deleting submitted activity, or AWS/EventBridge deployment.
- No claim that self-reported activity is independently verified.

## Acceptance
A joined employee can understand the goal, submit an eligible quantity, see cumulative progress, and see Complete only when the cumulative total reaches the configured target. Nonparticipants, inactive employees, wrong-tenant callers, invalid dates/quantities and duplicate idempotency keys cannot create duplicate or unauthorized activity.
