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
- [x] Add IA-08 startup validation for absent/blank/weak JWT signing keys and UTF-8 byte-boundary tests.
- [x] Remove the committed JWT signing-key fallback from appsettings and document environment/secret-store configuration.
- [x] Run restore/build/test with .NET 8 SDK and MySQL Testcontainers in GitHub Actions.
- [x] Record CI evidence: run #27 passed 28 tests, 0 failed, 0 skipped; Release build had 0 warnings and 0 errors; Docker Compose validation passed.
- [x] Enforce development-only access to the bootstrap registration endpoint; non-Development environments return 404 without mutation, covered by unit and MySQL-backed integration tests.
- [ ] Resolve account lifecycle requirements before implementing IA-09 inactive-employee enforcement.
- [x] Upgrade password hashes on successful login when ASP.NET Core returns SuccessRehashNeeded; persist the replacement before issuing the JWT, with unit and MySQL-backed legacy-hash coverage.
