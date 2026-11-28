# Tasks: Insight Employee Status Projection Consumer

- [x] Keep lifecycle envelope validation and transport-independent processor.
- [x] Replace lifecycle projection persistence with Insight MySQL tables and EF Core migration.
- [x] Persist projection and processed-event inbox atomically.
- [x] Preserve duplicate, stale/equal, newer-version and concurrent-delivery semantics.
- [x] Remove DynamoDB SDK, client configuration, health check, table initializer and local service.
- [x] Replace DynamoDB Local integration tests with MySQL Testcontainers coverage.
- [x] Keep RabbitMQ manual acknowledgement, retry, DLQ and reconnect behavior.
- [ ] Verify Release build, complete .NET suite, Compose validation, RabbitMQ topology and frontend CI in pull request.
