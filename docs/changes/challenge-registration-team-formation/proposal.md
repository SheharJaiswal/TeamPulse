# Proposal: Challenge Registration and HR Team Formation

## Why

The current challenge flow lets employees join immediately after HR creates a challenge. It does not support an explicit registration window, decline acknowledgement, HR visibility into response status, or multiple challenge-specific teams.

## Scope

- HR publishes a challenge with a registration deadline and an invitation audience consisting of active employees in the organization.
- The system sends a challenge invitation email to each eligible employee using the configured email delivery mechanism.
- Employees explicitly join or decline while registration is open.
- HR can view invited, joined, declined, and pending counts and the joined employee list.
- HR closes registration. Pending employees are not participants after closure.
- HR creates multiple teams within the challenge from joined employees and assigns every joined employee to exactly one team.
- HR finalizes team formation. Team membership is challenge-specific and does not modify Identity company-team membership.
- Existing employee challenge pages show acknowledgement status and the assigned team after finalization.

## Out of scope

- Habit/step progress, goal metrics, scoring, and leaderboard calculation.
- Employee-created teams.
- Changes to permanent company team membership.
- AWS EventBridge provisioning or production mail provider selection.

## Compatibility

Existing challenge records must remain readable. Existing participation records should be treated as joined acknowledgements for migration purposes, but they must not be silently assigned to newly invented teams.