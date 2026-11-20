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
- HR provisioning/invitation and HRIS integration.
- HR-authorized lifecycle commands, immutable audit persistence and transactional outbox record creation are implemented in Identity. The lease-safe dispatcher and EventBridge production adapter are implemented; local RabbitMQ development transport is implemented and CI-verified (run #93). EventBridge deployment remains deferred until the owner sets up cloud infrastructure.
- Downstream employee-status projections and access revocation.
- Retention/deletion jobs and privacy/legal sign-off.
- Insight cohort suppression and anti-differencing controls.
- Challenge scoring/versioned rules and evidence verification.

## Additional implementation decisions identified by the HR lifecycle SDD

These are not unresolved MVP product-baseline questions; they are implementation policies that must be decided before the corresponding deferred capability is built.

10. **Employee invitation and credential activation — OPEN / BLOCKS PROVISIONING ENDPOINTS:** Decide invitation delivery provider, token entropy/storage/expiry, resend and cancellation behavior, initial password setup, account activation and replay handling. The one-time HrAdmin bootstrap is not a general employee invitation mechanism.
11. **Role catalogue and elevated-role assignment — OPEN / BLOCKS ROLE MUTATION:** Define supported role values, who may grant/revoke OrganizationAdmin and HrAdmin, approval/step-up controls and how role changes affect already-issued JWTs. Until approved, no profile/role mutation endpoint is exposed.
12. **Organization Admin request intake — OPEN / BLOCKS REQUEST WORKFLOW:** Define request fields, permitted request types, approval/denial workflow, notification, status visibility and audit. Organization Admins have no direct employee field or lifecycle write access meanwhile.
13. **HRIS authentication and reconciliation — OPEN / BLOCKS HRIS FEED:** Define service authentication, source identifiers, mapping to TeamPulse organization/team IDs, conflict precedence, replay/idempotency and termination/reactivation ordering.
14. **Lifecycle reason-code catalogue — OPEN / NON-BLOCKING FOR INITIAL API:** Decide whether reason codes require a controlled vocabulary, localization or policy-specific validation. The initial lifecycle API stores a required bounded opaque reason code without inventing allowed values.
15. **EventBridge deployment and outbox dispatcher — TEMPLATE COMMITTED; LIVE DEPLOYMENT DEFERRED BY OWNER:** The dispatcher and parameterized CloudFormation template are implemented and CI-verified. The owner will configure EventBridge during the later cloud-infrastructure setup. Do not attempt AWS deployment, request credentials or make EventBridge a local-development prerequisite. Until cloud setup, use the local RabbitMQ transport defined by `docs/changes/identity-local-rabbitmq-transport/`; EventBridge remains the intended production transport. Keep the dispatcher disabled by default and explicitly select `RabbitMq` for local runs.

16. **Downstream projection rollout and token revocation — OPEN / BLOCKS GLOBAL REVOCATION CLAIM:** Implement and operate version-aware status projections in Habit, Challenge, Event Tracking, Insight and Notification. Decide whether any stronger-than-token-expiry revocation mechanism is required. ADR-0002 remains authoritative: do not claim immediate global revocation before consumers are deployed.
17. **Retention/legal sign-off — OPEN / PRODUCTION BLOCKER:** Obtain jurisdiction-specific privacy/legal approval for the ADR-0002 retention defaults and employee offboarding/deletion policy.


18. **Identity outbox CloudWatch telemetry — DEFERRED WITH CLOUD INFRASTRUCTURE:** The dispatcher emits `System.Diagnostics.Metrics` instruments but no CloudWatch/OpenTelemetry exporter is configured. The exporter, CloudWatch namespace/dimensions, least-privilege `cloudwatch:PutMetricData` permission and producer-side alarms will be addressed during/after cloud infrastructure setup. This is not a blocker for local RabbitMQ development; do not add AWS credentials or deploy CloudWatch resources now.
