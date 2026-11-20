# Design: Identity Password Hash Upgrade

## Domain behavior
Add Employee.UpdatePasswordHash(string) to encapsulate replacement of the stored one-way hash and reject blank hash values.

## Application flow
The IdentityController login action continues to normalize email and verify the submitted password. Failed returns the existing generic 401. SuccessRehashNeeded causes the configured IPasswordHasher<Employee> to create a new hash, updates the tracked Employee, and awaits IEmployeeRepository.SaveChangesAsync before CreateResponse is invoked. Unknown enum values fail closed with the same generic 401. The EF repository query currently returns a tracked entity, so SaveChanges persists the aggregate mutation.

## Tests
- Controller unit tests use a deterministic IPasswordHasher<Employee> stub to force SuccessRehashNeeded and Success outcomes.
- A recording token service verifies token generation happens after SaveChanges.
- A simulated SaveChanges failure proves token generation is not reached.
- The existing wrong-password test continues to assert the generic 401 and no persistence write.
- A MySQL Testcontainers integration test seeds an IdentityV2 hash, verifies that the current hasher reports SuccessRehashNeeded, logs in through the real API, and confirms the persisted replacement verifies as Success.

## Persistence and operations
No schema change is required. The existing MySQL 8.4 / EF Core 8 / Pomelo provider, checked-in migration and startup migration behavior remain unchanged.
