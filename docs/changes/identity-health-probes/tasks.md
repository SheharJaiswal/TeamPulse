# Tasks: Identity Health Probe Separation

- [x] Reconcile live branch HEAD, recent commit history, recursive tree and continuation checkpoint.
- [x] Read AGENTS.md, README.md, open questions, Identity specification/contract, service ownership, communication conventions and ADR-0001.
- [x] Inspect existing health registration, endpoint mapping, Testcontainers fixture and startup migration tests.
- [ ] Add tagged liveness and readiness checks with a bounded database probe timeout.
- [ ] Preserve `/health` as the readiness compatibility alias.
- [ ] Add an integration test that stops MySQL after startup and distinguishes liveness from readiness.
- [ ] Update Identity API contract, README and project checkpoint.
- [ ] Run restore, Release build, Compose validation and the full MySQL-backed test suite in CI.
- [ ] Record exact CI evidence and resulting commit SHA.
