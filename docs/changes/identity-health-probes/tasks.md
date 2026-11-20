# Tasks: Identity Health Probe Separation

- [x] Reconcile live branch HEAD, recent commit history, recursive tree and continuation checkpoint.
- [x] Read AGENTS.md, README.md, open questions, Identity specification/contract, service ownership, communication conventions and ADR-0001.
- [x] Inspect existing health registration, endpoint mapping, Testcontainers fixture and startup migration tests.
- [x] Add tagged liveness and readiness checks with a bounded three-second database probe timeout.
- [x] Preserve `/health` as the readiness compatibility alias.
- [x] Add a MySQL integration test that stops the database after startup and distinguishes liveness from readiness.
- [x] Verify health responses do not expose connection strings or credentials.
- [x] Update Identity API contract and README.
- [x] Correct .NET 8 health-check API usage found by CI run #30 (HealthCheckOptions namespace and timeout registration).
- [x] Run CI #31 (44675962776): restore passed; Release build passed with 0 warnings and 0 errors; Docker Compose validation passed; 38 tests passed, 0 failed, 0 skipped.
- [x] Record artifact 14601438633 and implementation commit 3782940507dee022a611cbe34f92d3a689400bfb in docs/project-status.md.
