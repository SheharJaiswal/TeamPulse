## Habit Management API — implementation added; CI verification pending

- Added the first employee-facing Habit business capability to the existing Habit service: authenticated personal habit create/list/detail/update/archive endpoints.
- Added the Habit domain aggregate, application service/contracts, Habit-owned MySQL persistence and migration. Employee identity is sourced only from validated JWT `sub`; repository queries enforce owner isolation.
- Habit validates Identity's issuer/audience/signing key configuration and denies access when its local employee-status projection explicitly marks the caller inactive. Missing projections remain unknown; this increment does not claim immediate global revocation.
- Habit definitions contain daily/weekly frequency, target count, date range, optional description/category and active/archived lifecycle. Completion/activity records remain owned by Event Tracking; Challenge owns challenge-specific snapshots.
- Added domain invariant tests. Svelte frontend integration remains the next UI increment.
- CI #243 passed on the preceding head (`e90ee53`): all existing build/test/topology checks succeeded. New Habit Management changes are not yet CI-verified.
- No AWS resources, existing persistent RabbitMQ topology or broker data were changed.

Updated: 2026-11-21
