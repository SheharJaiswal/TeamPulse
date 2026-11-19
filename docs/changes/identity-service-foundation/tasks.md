# Tasks: Identity Service Foundation

- [x] Establish SDD and service ownership documentation.
- [x] Create Identity Clean Architecture projects.
- [x] Add Employee entity and EF mapping.
- [x] Add password hashing and JWT service.
- [x] Add register, login and /me endpoints.
- [x] Replace PostgreSQL with MySQL 8.4 and Pomelo EF Core provider.
- [x] Configure startup migration execution with Database.MigrateAsync().
- [x] Add initial checked-in Identity schema migration.
- [x] Add unit tests for password hashing and JWT signature/claims.
- [x] Add controller unit tests for registration validation, duplicate normalized email, successful hashed registration, generic login failures and successful normalized login.
- [x] Add MySQL-backed integration-test harness and cases for migration/restart, persistence, database unique-email constraint, bearer authentication and /me.
- [x] Run restore/build/test with .NET 8 SDK and MySQL Testcontainers in GitHub Actions.
- [x] Record CI evidence: 21 passed, 0 failed, 0 skipped; build had 0 warnings and 0 errors.
