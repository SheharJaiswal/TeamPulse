


## Event Tracking consumer CI verification and API-host startup test

- CI #136 passed for consumer commit `057d5cb9c38f045e728d50ad3a34b10a1b49f1f8`: Release build, Compose validation, RabbitMQ startup/topology checks, 194 .NET tests and 14 Python topology tests passed. [CI #136](https://github.com/SheharJaiswal/TeamPulse/actions/runs/44879757388).
- Added a real EventTracking API-host integration test using a fresh MySQL 8.4 Testcontainer. It exercises `/health/ready`, verifies the startup migration is applied, resolves the production projection store/reader from DI, persists a lifecycle projection and reads it back.
- Upgraded MySqlConnector to 2.6.2. The CI warning identified GHSA-473q-m89c-ghf8; versions before 2.6.1 are affected, and 2.6.1+ contains the fix. [GitHub advisory](https://github.com/advisories/GHSA-473q-m89c-ghf8).
- These API-host test and dependency changes are authored but await CI verification. RabbitMQ Testcontainers behavior tests and actual-host consumer health isolation remain outstanding. Existing user RabbitMQ/MySQL volumes remain untouched.

Updated: 2026-11-21
