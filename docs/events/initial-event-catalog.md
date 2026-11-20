# Initial Event Catalog

| Event | Owner | Consumers |
|---|---|---|
| EmployeeCreated | Identity | Habit, Challenge, Notification and Insight projections |
| EmployeeUpdated | Identity | Services maintaining employee/team projections |
| EmployeeDeactivated | Identity | Habit, Challenge, Event Tracking, Insight and Notification status projections |
| EmployeeReactivated | Identity | Habit, Challenge, Event Tracking, Insight and Notification status projections |
| ChallengeCreated | Challenge | Notification and Insight |
| EmployeeJoinedChallenge | Challenge | Event Tracking projection, Insight and Notification |
| ChallengeActivityRecorded | Event Tracking | Challenge progress processor, Insight and Notification |
| HabitActivityRecorded | Event Tracking | Habit progress processor, Insight and Notification |

Employee lifecycle events use schema version 1 and carry a per-employee monotonic lifecycle version so consumers can ignore duplicate and out-of-order deliveries. All consumers are idempotent and assume at-least-once delivery. Events contain minimum required information; never credentials, JWTs, password hashes or unnecessary private wellness data.
