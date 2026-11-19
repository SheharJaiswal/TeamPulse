# Design: Identity Service Foundation

- Domain: Employee entity.
- Application: repository and token abstractions, auth DTOs.
- Infrastructure: EF Core DbContext/repository, MySQL provider and JWT token service.
- API: HTTP endpoints, JWT validation, authorization, health check and startup migration execution.
- Identity DB owns employees; other services retain EmployeeId references only.
- PasswordHasher<Employee> stores one-way hashes.
- JWT validation enforces issuer, audience, signature and lifetime.
- Local signing key is a placeholder; deployed environments inject a secret.
- Registration is development-only. Production uses HR provisioning.
- Checked-in EF migration creates the employees table and unique normalized-email index. API runs Database.MigrateAsync() before serving requests.
- Startup migration is suitable for local development/single instance. Production multi-replica rollout should use a coordinated migration job.
- Follow-up: tests, inactive-user check, key rotation, refresh tokens and audit events.
