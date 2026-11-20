# Specification: Identity Password Hash Upgrade

## Acceptance criteria
- PH-01: When VerifyHashedPassword returns SuccessRehashNeeded, login generates a replacement hash using the configured IPasswordHasher<Employee>.
- PH-02: The replacement hash is applied to the Employee and persisted through the Identity repository before CreateAccessToken is called.
- PH-03: If persistence fails, the login action does not issue an access token or return a successful response.
- PH-04: When verification returns Success, the existing hash is retained and no persistence write is performed.
- PH-05: When verification returns Failed, the existing generic HTTP 401 response remains unchanged and no hash write occurs.
- PH-06: A MySQL-backed integration test proves an IdentityV2 legacy hash is upgraded to a hash accepted as Success by the current configured hasher after login.
- PH-07: No database schema migration or public API response change is introduced.

## Compatibility
The login request and successful AuthResponse remain unchanged. Password rehash is transparent to clients.
