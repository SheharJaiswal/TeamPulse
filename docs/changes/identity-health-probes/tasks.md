# Tasks: Identity Health Probe Separation

- [x] Reconcile live branch HEAD, recent commit history, recursive tree and continuation checkpoint.
- [x] Read AGENTS.md, README.md, open questions, Identity specification/contract, service ownership, communication conventions and ADR-0001.
- [x] Inspect existing health registration, endpoint mapping, Testcontainers fixture and startup migration tests.
- [x] Add tagged liveness and readiness checks with a bounded three-second database probe timeout.
- [x] Preserve `/health` as the readiness compatibility alias.
- [x] Add an integration test that stops MySQL after startup and distinguishes liveness from readiness.
- [x] Update Identity API contract and README.
- [ ] Run restore, Release build, Compose validation and the full MySQL-backed test suite in CI.
- [ ] Record exact CI evidence and resulting commit SHA.
