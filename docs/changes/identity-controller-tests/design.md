# Design: Identity Controller Unit Tests

## Test structure
Add `IdentityControllerTests` to the existing `Identity.UnitTests` project. Reference the Identity.API project so the tests instantiate the actual controller. Use the real `PasswordHasher<Employee>`, a fake `IEmployeeRepository` backed by an in-memory list, and a fake `ITokenService` returning a deterministic test token.

## Assertions
Assert concrete MVC result types and HTTP status behavior. Verify no repository add/save calls occur on validation or duplicate-email failures. On successful registration, verify normalized input, trimmed display/organization/team values, persisted hash and password verification. Compare the exact generic login message for both unknown and incorrect credentials.

## Isolation
No test uses application configuration, the local placeholder signing key, MySQL, network access or external credentials. The repository fake models only the methods used by this controller.

## Deferred coverage
EF unique-index race behavior, database transactions, migration execution, authentication middleware and authenticated `/me` behavior require the separate MySQL/API integration-test increment.
