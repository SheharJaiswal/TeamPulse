# Specification: Challenge Goals and Employee Progress

## Challenge goal
- Each challenge has exactly one individual cumulative goal: action instructions, target quantity greater than zero, and a short unit label.
- HR must supply all goal fields when creating a challenge. Instructions are 1–500 characters, unit is 1–30 characters, and target is positive and at most 1,000,000.
- The goal is immutable after challenge creation for this increment.
- Challenge owns and returns goal metadata. Invitation email includes the action instructions, target and unit. Existing challenges receive a safe legacy goal during migration: instructions "Complete the challenge activity and record your progress.", target 1, unit "completion"; HR should use newly created challenges for explicit goals.

## Activity submission
- Event Tracking owns append-only challenge activity submissions, distinct from habit activity submissions.
- Employee identity comes from validated JWT sub. Organization scope and participation are checked by Challenge API; request body cannot supply employee or organization IDs.
- Submission quantity is greater than zero and at most 1,000,000 per entry; activity date must be within the challenge start/end dates and not in the future.
- A joined, active employee may submit activity only for the challenge's configured activity window. Declined, pending, inactive, nonparticipant, and cross-organization requests are rejected.
- Each submission has an employee-scoped idempotency key. Replaying the same key and same challenge/date/quantity returns the existing submission; conflicting reuse returns 409.
- Submissions are self-reported and append-only. No editing/deleting or verification workflow is included.

## Progress and completion
- Progress is the sum of the employee's accepted challenge activity quantities for that challenge.
- Completion is derived as progress >= goal target. Progress may exceed target; it is displayed capped at target for the progress bar while the total remains visible.
- A completed challenge cannot accept additional submissions.
- Employee can view their own activity entries and total only. HR and managers do not receive individual activity history through this feature.

## UI
- HR create-challenge form requires action instructions, target and unit.
- Employee available and joined challenge cards show goal/action and target.
- Joined challenges provide an activity log form and progress state: not started, in progress, complete.
- Use server-side SvelteKit actions; bearer tokens are never exposed to browser JavaScript.

## Security and ownership
- Challenge owns challenge goal metadata and participant eligibility.
- Event Tracking owns challenge activity source records and idempotency.
- No cross-service database access or foreign keys. Event Tracking calls Challenge API with the user's bearer token.
- JWT issuer, audience, signature, expiry, active employee and tenant/resource authorization remain enforced.
