# Change Specification

Implements `docs/specs/identity-service.md`.

- [x] IA-01 Required fields/password validation
- [x] IA-02 Normalized email uniqueness
- [x] IA-03 One-way password hashing
- [x] IA-04 Generic login failure
- [x] IA-05 Signed JWT issuer/audience/expiry
- [x] IA-06 Anonymous /me denied
- [x] IA-07 /me uses validated token claims
- [ ] IA-08 Missing, blank or fewer-than-32-UTF-8-byte signing key fails startup (startup tests added; CI verification pending)
- [x] IA-DB-01 MySQL schema is created/updated by checked-in EF migrations at API startup
- [x] IA-DB-02 Repeated startup after migration is applied is safe and does not recreate tables
- [ ] IA-09 Inactive employee denied when account status support is implemented
