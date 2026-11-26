# Design: Challenge Registration and HR Team Formation

## Lifecycle

A challenge is created in RegistrationOpen state with a registration deadline. HR may close registration explicitly. Closing registration atomically changes the challenge to RegistrationClosed and prevents further join/decline responses. Pending invitees remain non-participants.

After registration is closed, HR creates challenge-specific teams and assigns only employees whose response is Joined. A joined employee may be assigned to one team at most. HR finalizes teams only when every joined participant is assigned exactly once and at least one team exists. Finalization is one-way for this change.

## Data ownership

Challenge Service owns challenge registration, response status, challenge teams, team members, and finalization. Identity remains the source of employee identity and organization membership; the Challenge service must use its existing employee-status projection and trusted employee directory integration rather than changing permanent team membership.

## API shape

- HR creates a challenge with a registration deadline.
- Employee acknowledgement endpoints support Join and Decline, scoped by validated JWT organization and employee claims.
- HR registration endpoints return response counts and joined participant details.
- HR closes registration.
- HR creates/updates challenge teams and their member assignments while registration is closed and teams are not finalized.
- HR finalizes team formation.
- Employee reads return their challenge response and team assignment.

## Email

Challenge invitation is sent to eligible active employees after publish. Email sending must use configured SMTP and be resilient to transient failures. Do not put bearer tokens in email links; links route to the authenticated TeamPulse challenge page. Email delivery failure must be visible/logged and must not fabricate an employee response.

## Persistence

Use Challenge-owned tables and EF migrations. Keep cross-service employee IDs as scalar identifiers; do not add cross-service foreign keys. Enforce unique employee response per challenge and unique membership per employee per challenge at database level. Persist team finalization and UTC timestamps.

## Safety and authorization

All HR operations require HrAdmin and organization scoping. Employee response actions use employee/organization claims, not request-supplied employee IDs. Employees may only acknowledge their own invitation. Only Joined employees may be assigned. Registration closure is the server-side gate for team creation. Pending employees are excluded from team formation.

## Compatibility

Existing participation rows migrate to Joined responses. Existing challenges are marked RegistrationClosed with team formation not finalized to avoid reopening historic challenges or fabricating team membership.