# Specification: Development-Only Identity Registration

## Acceptance criteria
- REG-01: When the Identity host environment is Development, `POST /api/identity/register` follows the existing validation and registration contract.
- REG-02: When the host environment is not Development, `POST /api/identity/register` returns HTTP 404 before request validation or repository access.
- REG-03: A non-Development registration attempt does not add or persist an employee.
- REG-04: The guard is enforced by the API host environment, not by a client-supplied field or header.
- REG-05: Login and authenticated `/me` behavior are not changed.
- REG-06: MySQL 8.4 / EF Core 8 / Pomelo persistence and checked-in startup migrations are unchanged.

## Environment matrix
| ASPNETCORE_ENVIRONMENT | Registration |
|---|---|
| Development | Enabled for local bootstrap |
| Testing | HTTP 404 |
| Staging | HTTP 404 |
| Production | HTTP 404 |

## Compatibility
The existing request and success response shape is unchanged in Development. Non-Development callers receive 404, matching the endpoint's documented development-only nature.
