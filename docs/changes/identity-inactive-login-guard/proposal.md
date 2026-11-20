# Proposal: Deny Authentication for Inactive Employees

## Problem
The Employee entity already contains an IsActive flag, but the Identity login action currently verifies credentials and may issue a JWT without checking that flag. An employee record marked inactive must not authenticate.

## Decision context
ADR-0002 establishes HR as the authority for employee activation/deactivation. This change implements only the defensive authentication rule for an existing inactive record. It does not add lifecycle APIs, HR workflows, audit persistence or status-change events.

## Scope
- Reject login for an employee whose IsActive flag is false.
- Return the same generic 401 response used for unknown email and incorrect password.
- Do not invoke password verification/rehashing or token issuance for an inactive employee.
- Add controller unit tests and MySQL-backed API integration tests.
- Update Identity specification, API contract and project checkpoint.

## Out of scope
Activation/deactivation commands, HR authorization, audit records, HRIS integration, refresh tokens, token revocation and downstream EmployeeDeactivated/EmployeeReactivated event consumers.

## Success criteria
- Inactive employees receive the same generic unauthorized response.
- No password hash is changed and no access token is issued.
- Active employee login behavior remains unchanged.
- Full .NET 8 CI passes.
