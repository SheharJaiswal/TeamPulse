# Open Questions — Resolved MVP Baseline

The initial nine product questions are resolved using the project owner's instruction to apply the recommended answers. The accepted defaults and rationale are recorded in ADR-0002, docs/decisions/0002-product-policy-baseline.md. These decisions are binding for MVP design unless superseded by a new approved ADR.

1. **Employee onboarding — RESOLVED:** HR-controlled provisioning/invitation only in production. Self-registration remains disabled outside Development; the current Development bootstrap route is temporary.
2. **Organization/team authority — RESOLVED:** Identity owns canonical TeamPulse employee, organization, team and role records. HRIS is the authoritative upstream employment/membership source when integrated; authorized HR administrators maintain records until then.
3. **Challenge scoring — RESOLVED:** Default to normalized completion percentage (valid completed eligible tasks / eligible assigned tasks × 100). Alternative formulas must be explicitly selected and versioned before participation starts.
4. **Insight cohort privacy — RESOLVED:** Suppress team/organization aggregates below 10 distinct employees. Prevent filter differencing and never expose individual habit/activity details in manager insight views.
5. **Activity evidence — RESOLVED:** Employee self-report by default. Require evidence only for tasks that explicitly declare verification before participation. No passive health/location/device monitoring by default.
6. **Habit reuse — RESOLVED:** A personal habit may link to multiple programmes. Challenge snapshots its own target/rules; challenge targets never mutate the employee-owned habit.
7. **Manager scope — RESOLVED:** Direct reports by default. Broader team scope requires explicit auditable delegation. Managers see qualifying aggregates, not individual habits, raw submissions, evidence or health details.
8. **Retention/deletion — RESOLVED FOR MVP, LEGAL REVIEW REQUIRED:** Raw evidence 90 days after challenge close; identifiable activity/participation 12 months after close; security/admin audit 24 months; irreversibly anonymized aggregates up to 5 years. Legal holds and applicable law override. Obtain jurisdiction-specific privacy/legal approval before production.
9. **Employee lifecycle — RESOLVED:** HR controls activation/deactivation; trusted HRIS feed may act on HR's behalf. Organization Admins may request changes but cannot override HR or reactivate employees. Record actor/source, employee, old/new status, UTC time, reason code, optional note and correlation ID. Publish versioned lifecycle events through the transactional outbox; receiving services maintain local status projections. Until downstream revocation is implemented, already-issued stateless JWTs may remain usable by receiving services until expiry. Login must deny an employee whose Identity record is already inactive.

## Remaining implementation work (not unresolved product questions)
- HR provisioning/HRIS integration and authorized lifecycle commands.
- Lifecycle audit persistence and transactional outbox publication.
- Downstream employee-status projections and access revocation.
- Retention/deletion jobs and privacy/legal sign-off.
- Insight cohort suppression and anti-differencing controls.
- Challenge scoring/versioned rules and evidence verification.
