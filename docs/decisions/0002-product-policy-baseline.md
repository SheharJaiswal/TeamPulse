# ADR-0002: Recommended Product Policy Baseline

- Status: Accepted by the project owner.
- Date: 2026-11-20.
- Scope: Resolve the initial product open questions with privacy-preserving MVP defaults.
- Review note: Retention periods and employee-data handling require jurisdiction-specific privacy/legal review before production launch. This ADR is a product baseline, not legal advice.

## Decision 1 — Employee onboarding
Production onboarding is HR-controlled provisioning/invitation only. Public self-registration is disabled outside Development; the existing Development bootstrap endpoint is not a production onboarding mechanism. Identity creates and owns the canonical TeamPulse employee record after an authorized HR provisioning request or trusted HRIS feed.

## Decision 2 — Organization and team membership
Identity owns canonical TeamPulse employee, organization, team and role records and stable identifiers. Every employee belongs to an organization; team membership is optional and may be assigned later. Employees can use personal employee capabilities without joining a team. An organization's HRIS is the authoritative upstream source for employment status and organizational assignment when integrated. Until an HRIS integration exists, authorized HR administrators provision and maintain these records through Identity-owned workflows. Other services store stable IDs only and resolve authoritative details through Identity APIs/events; no cross-service database access.

## Decision 3 — Challenge scoring
The MVP default score is normalized completion percentage: valid completed eligible tasks divided by eligible tasks assigned to the participant, multiplied by 100, bounded from 0 to 100. Challenge definitions snapshot eligible tasks and the scoring denominator for the challenge period. Any alternative scoring formula must be explicitly selected and versioned before participants start.

## Decision 4 — Minimum cohort size for insights
A team or organizational wellness aggregate is displayed only when its cohort contains at least 10 distinct employees. Smaller cohorts are suppressed, not displayed as zero and not automatically rolled into a smaller subgroup. Filters and drill-downs must prevent differencing attacks. Individual habit/activity details are never exposed through manager or organization insight views.

## Decision 5 — Activity evidence and verification
Employee self-report is the default. Evidence is required only when a challenge task explicitly declares an evidence/verification requirement before participation begins. No continuous passive health, location or device monitoring is enabled by default. Event Tracking stores submission/evidence metadata and access-controlled evidence references; it does not own challenge definitions or credentials. Evidence access is limited to the minimum authorized verification role and is audited.

## Decision 6 — Reuse of personal habits across challenges
An employee may link one personal habit to multiple challenge programmes. Habit remains employee-owned; Challenge owns its task, target, eligibility, period and completion state. At enrolment, Challenge snapshots the relevant target/rules for that programme. A challenge-specific target mismatch does not mutate the employee's personal habit. Completion in one challenge does not automatically complete another challenge unless its own task rules are satisfied.

## Decision 7 — Manager access scope
Managers default to direct-report scope for authorized operational employee information. Broader team scope requires explicit, auditable delegation by an Organization Admin. Managers receive only aggregate wellness/challenge insight when the minimum cohort threshold is met; they do not receive individual personal habit definitions, raw activity submissions, evidence or health details. Organization Admins manage organization configuration and access to qualifying aggregates, not unrestricted individual wellness data. Every service enforces its own resource/tenant authorization; a role claim alone is insufficient.

## Decision 8 — Data retention and deletion
MVP defaults, subject to privacy/legal review before production:
- Raw evidence files: delete 90 days after the related challenge closes, unless a documented investigation/legal hold applies.
- Identifiable activity submissions and challenge participation records: retain 12 months after challenge close, then delete or irreversibly anonymize.
- Security and administrative audit records: retain 24 months, with access restricted to authorized reviewers.
- Aggregates may be retained up to 5 years only after irreversible anonymization and continued compliance with cohort thresholds and anti-differencing controls.
- On employee offboarding, disable access immediately; erase or anonymize profile data after the organization's approved contractual retention period, subject to legal holds and applicable law.
- Deletion jobs must be idempotent, auditable and scoped to the owning service.

## Decision 9 — Employee account lifecycle
HR is the authority for employee activation and deactivation. A trusted HRIS employment-status feed may perform the same transition on HR's behalf. Organization Admins may submit a request to HR but cannot independently reactivate an employee or override an HR deactivation. Deactivation blocks new login immediately and marks the employee inactive in Identity. Reactivation is an explicit HR action and does not restore previous sessions. Every transition records actor/source, target employee ID, previous and new status, UTC timestamp, reason code, optional explanatory note and correlation ID.

Identity publishes versioned EmployeeDeactivated / EmployeeReactivated events through the transactional outbox. Receiving services maintain an employee-status projection and enforce access locally; no service reads Identity's database. Until downstream event propagation and revocation handling are implemented, an already-issued stateless access token may remain usable by a receiving service until expiry. Do not claim immediate global token revocation before that capability is implemented and verified. The existing IsActive flag is enforced defensively during login; this login guard does not implement lifecycle commands or audit workflows.

## Consequences
The nine initial open questions have recommended MVP decisions and no longer block domain design. Each decision must be reflected in the owning service specification and contracts before implementation. Retention settings require legal/privacy sign-off before production. HRIS integration, HR provisioning workflows, lifecycle audit storage, transactional outbox and downstream status projections remain implementation work; this ADR does not claim those capabilities already exist.
