# Proposal: Enforce Development-Only Identity Registration

## Problem
The Identity specification, API contract and README describe public registration as a local bootstrap mechanism. The current controller action is anonymous and mapped in every host environment, so a deployed API can still expose employee self-registration.

## Scope
Guard `POST /api/identity/register` using the host environment. Permit it only in Development. Return HTTP 404 before validation or persistence in every other environment. Add controller unit tests and real-host MySQL-backed integration tests for Development, Testing and Production behavior.

## Out of scope
HR provisioning, employee lifecycle/deactivation, login, `/me`, JWT behavior, persistence schema and service ownership changes.

## Success criteria
- Registration succeeds in Development, preserving the current bootstrap contract.
- Registration returns 404 and does not mutate storage in Testing, Staging or Production.
- Existing login, `/me`, MySQL migrations and other Identity behavior remain unchanged.
