# Design: Identity Service Foundation

- Domain: Employee entity.
- Application: repository and token abstractions, auth DTOs.
- Infrastructure: EF Core DbContext/repository and JWT token service.
- API: HTTP endpoints, JWT validation, authorization and health check.
- Identity DB owns employees; other services retain EmployeeId references only.
- PasswordHasher<Employee> stores one-way hashes.
- JWT validation enforces issuer, audience, signature and lifetime.
- Local signing key is a placeholder; deployed environments inject a secret.
- Registration is development-only. Production uses HR provisioning.
- Follow-up: migration, tests, inactive-user check, key rotation, refresh tokens and audit events.