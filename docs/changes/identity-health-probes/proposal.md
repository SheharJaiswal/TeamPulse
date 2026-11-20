# Proposal: Identity Health Probe Separation

## Problem
The Identity API currently exposes one `/health` endpoint backed by the database connectivity check. Orchestrators need to distinguish process liveness from dependency readiness: if MySQL becomes unavailable after startup, a liveness probe coupled to MySQL can cause unnecessary application restarts.

## Scope
Add a dependency-independent liveness endpoint and a MySQL-backed readiness endpoint. Preserve `/health` as a compatibility alias for readiness. Keep the existing startup migration behavior unchanged.

## Out of scope
Changing startup migration policy, adding new infrastructure, changing database ownership, changing authentication behavior, or defining business/account lifecycle rules.

## Success criteria
- `/health/live` reports process liveness without checking MySQL.
- `/health/ready` checks Identity MySQL connectivity and reports unavailable dependencies as HTTP 503.
- `/health` retains the current readiness behavior for existing callers.
- Integration tests prove liveness remains healthy while MySQL is unavailable and readiness fails closed.
