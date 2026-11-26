# Specification: Challenge Registration and HR Team Formation

## Challenge publication and invitations

- HR creates a challenge with a registration deadline that is not earlier than the current date and not later than the challenge start date.
- Challenge creation opens registration and triggers invitations to eligible active employees in the same organization.
- Invitation email contains challenge name, description, dates, registration deadline, and an authenticated application link. It does not contain a bearer token.
- Invitation delivery is queued in Challenge-owned persistence and processed by a background SMTP worker.
- SMTP delivery retries transient failures up to three attempts; final failure is recorded/logged and does not change employee response status.

## Employee acknowledgement

- Employee and HrAdmin may read their own challenge invitations and respond.
- Join records an explicit Joined response; Decline records Declined.
- A response is unique per challenge and employee. Repeating the same response is idempotent.
- Response changes are allowed while registration is open; after registration closes, Join and Decline return conflict.
- Employee identity and organization are derived from validated claims. Inactive employees are denied.

## HR registration management

- HR can list challenges and view response counts: invited, joined, declined, pending.
- HR can view joined participants for team formation.
- HR closes registration explicitly. The close operation is idempotent.
- After closure, pending invitees are not participants and cannot be assigned to teams.
- HR team formation is unavailable until registration is closed.

## Challenge-specific teams

- One challenge can contain multiple teams.
- HR creates and names teams and assigns joined employees.
- A joined employee may belong to exactly one team in a challenge.
- Team membership does not change permanent Identity team membership.
- HR can finalize teams only after at least one team exists and every joined participant is assigned exactly once.
- Finalization is idempotent and prevents subsequent team/member edits.
- Employees can read their assigned team after finalization.

## Existing data

- Existing participation rows are migrated to Joined responses.
- Existing challenges are closed for registration during migration to prevent unexpected late acknowledgements.
- Existing challenges are not automatically assigned teams.

## Out of scope

Challenge goal metrics, activity submission, progress calculation, team scoring, and leaderboard remain out of scope for this change.