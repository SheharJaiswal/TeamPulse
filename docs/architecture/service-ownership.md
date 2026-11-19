# Service Ownership

| Service | Owns | Must not own |
|---|---|---|
| Identity | Employees, organizations, teams, roles, credentials | Habits, challenges, activity submissions |
| Habit | Personal habit definitions, schedules and preferences | Credentials or challenge definitions |
| Challenge | Programmes, tasks, eligibility, participation and authoritative challenge state | Raw activity submissions or private habit definitions |
| Event Tracking | Activity submissions, evidence metadata and audit history | Challenge definitions or credentials |
| Insight | Aggregated metrics and read models | Source-of-truth employee/challenge/activity records |
| Notification | Preferences, notification records and delivery history | Habit or challenge state |

Cross-service references use stable IDs only; no cross-database foreign keys. Obtain authoritative information through owner APIs or owner-published events. Identity JWT `sub` identifies the caller; every service still authorizes its own resources.