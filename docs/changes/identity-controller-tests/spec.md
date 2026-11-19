# Specification: Identity Controller Unit Tests

## Acceptance criteria
- ICT-01: Registration with any missing/blank required field returns HTTP 400 and does not add or save an employee.
- ICT-02: Registration with a password shorter than 12 characters returns HTTP 400 and does not add or save an employee.
- ICT-03: Registration trims the supplied email and uses uppercase invariant normalization for duplicate lookup.
- ICT-04: A pre-existing normalized email returns HTTP 409 and does not add or save an employee.
- ICT-05: Successful registration stores a password hash that is not the supplied password and verifies against the supplied password; the response contains a bearer token and employee summary.
- ICT-06: Login for an unknown email returns HTTP 401 with the generic message `Invalid email or password.`.
- ICT-07: Login with an incorrect password returns the same HTTP 401 status and generic message as an unknown email.
- ICT-08: Successful login trims/normalizes email, verifies the password and returns the bearer token response.

## Constraints
- Tests use the existing Identity API controller and current approved contract.
- Tests do not change controller behavior or invent account lifecycle rules.
- These unit tests do not replace MySQL-backed integration tests.
