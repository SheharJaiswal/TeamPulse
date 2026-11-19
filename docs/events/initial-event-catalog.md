# Initial Event Catalog

| Event | Owner | Consumers |
|---|---|---|
| EmployeeCreated | Identity | Habit, Challenge, Notification and Insight projections |
| EmployeeUpdated | Identity | Services maintaining employee/team projections |
| ChallengeCreated | Challenge | Notification and Insight |
| EmployeeJoinedChallenge | Challenge | Event Tracking projection, Insight and Notification |
| ChallengeActivityRecorded | Event Tracking | Challenge progress processor, Insight and Notification |
| HabitActivityRecorded | Event Tracking | Habit progress processor, Insight and Notification |

Events contain minimum required information; never credentials, JWTs or password hashes.