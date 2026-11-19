# Change Specification

Implements `docs/specs/identity-service.md`.

- [ ] IA-01 Required fields/password validation
- [ ] IA-02 Normalized email uniqueness
- [ ] IA-03 One-way password hashing
- [ ] IA-04 Generic login failure
- [ ] IA-05 Signed JWT issuer/audience/expiry
- [ ] IA-06 Anonymous /me denied
- [ ] IA-07 /me uses validated token claims
- [ ] IA-08 Weak/missing signing key fails startup
- [ ] IA-DB-01 MySQL schema is created/updated by checked-in EF migrations at API startup
- [ ] IA-DB-02 Repeated startup after migration is applied is safe and does not recreate tables
- [ ] IA-09 Inactive employee denied when account status support is implemented
