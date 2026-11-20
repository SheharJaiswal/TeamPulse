# Tasks: Remove Obsolete PostgreSQL Bootstrap Artifact

- [x] Fetch the live feature branch HEAD, recent commits and recursive repository tree.
- [x] Read AGENTS.md, README.md, project status, open questions, service ownership, communication conventions, Identity spec, API contract and ADR-0001.
- [x] Inspect existing Identity unit/integration tests and the successful Docker-capable .NET 8 CI run.
- [x] Confirm the PostgreSQL directory contains only one orphaned SQL bootstrap file.
- [x] Review active local runtime/deployment configuration and developer documentation; no dependency on the PostgreSQL script was found.
- [x] Delete the obsolete PostgreSQL SQL artifact without changing MySQL configuration.
- [x] Verify the resulting repository tree and relevant content references.
- [x] Run and record the .NET 8 GitHub Actions validation: run #24 completed successfully for the deletion commit.
- [x] Update docs/project-status.md with the resulting commit, checks and the single next task.
