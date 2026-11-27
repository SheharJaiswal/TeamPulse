# Challenge goals and activity API contract

## Challenge API

### Create challenge
`POST /api/challenges` (HrAdmin, JWT)

Request adds:
- `goalInstructions`: required string, 1–500 characters.
- `goalTarget`: required positive decimal, maximum 1,000,000 and two decimal places.
- `goalUnit`: required string, 1–30 characters.

Challenge list, available challenge, my challenge, registration summary and create responses include the goal instructions, target and unit.

### Employee goal eligibility
`GET /api/challenges/{challengeId}/activity-goal` (Employee/HrAdmin, JWT)

Returns goal instructions, target, unit and challenge start/end dates only when the authenticated active employee has Joined the challenge in their organization. Employee ID and organization are resolved from JWT claims.

## Event Tracking API

### Submit challenge activity
`POST /api/challenge-activities` (Employee/HrAdmin, JWT)

Request:
```json
{
  "challengeId": "GUID",
  "activityDate": "YYYY-MM-DD",
  "quantity": 1.25,
  "idempotencyKey": "GUID"
}
```

Quantity must be greater than zero, at most 1,000 per entry, and have at most two decimal places. Activity date must fall within the challenge dates, cannot be in the future, and submissions are accepted only during the challenge window. The employee must be active and joined. Employee ID is derived from JWT `sub`.

A repeated employee-scoped idempotency key with identical challenge/date/quantity returns the existing submission; conflicting reuse returns 409. Activity is append-only and self-reported. Accepted submissions and the `ChallengeActivityRecorded` outbox event are committed atomically.

### Read own progress
`GET /api/challenge-activities/mine?challengeId={GUID}` (Employee/HrAdmin, JWT)

Returns only the caller's entries and cumulative quantity for the specified joined challenge. Completion is `totalQuantity >= goalTarget`. The employee workspace displays the total, remaining quantity and completion state. No individual activity history is exposed to HR/manager views.
