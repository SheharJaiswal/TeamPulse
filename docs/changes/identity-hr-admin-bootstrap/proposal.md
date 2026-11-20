# Proposal: Controlled Initial HR Administrator Bootstrap

## Problem
Production self-registration is disabled, while ADR-0002 assigns employee provisioning and lifecycle authority to HR. Identity currently has no safe mechanism to establish the first HR administrator. Enabling an unauthenticated registration route or relying on manual password-hash SQL would undermine the production onboarding boundary.

## Proposed change
Add a one-time, operator-invoked bootstrap utility that creates the initial Identity HR administrator using validated configuration and ASP.NET Core password hashing. The utility is not an HTTP endpoint and must refuse to run once any HR administrator record exists. It will be the prerequisite for later HR-authorized provisioning and lifecycle APIs.

## Scope
- Add a small .NET console entry point referencing the existing Identity domain and infrastructure projects.
- Reuse Identity's EF Core model, MySQL connection configuration and password hasher.
- Read the initial HR administrator identity and password from protected environment/secret-store configuration; never accept the password as a command-line argument.
- Enforce one-time behavior and fail closed on an existing HR administrator.
- Add automated tests for validation, duplicate/one-time behavior, hashing and persistence.
- Document the operator runbook and secret handling.

## Out of scope
- HTTP employee provisioning or activation/deactivation endpoints.
- HRIS integration, invitation email delivery or password-reset/change workflows.
- Lifecycle audit/outbox publication and downstream status projections.
- Organization Admin permissions or manager scopes.

## Acceptance
A deployment operator can securely establish the first HR administrator without enabling public self-registration, exposing a bootstrap HTTP endpoint, storing plaintext credentials, or changing an existing account's role.
