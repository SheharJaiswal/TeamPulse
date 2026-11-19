# Proposal: Identity Unit Test Baseline

## Problem
The Identity service currently has authentication and persistence code but no automated test project. This leaves password hashing and JWT construction without repeatable regression coverage.

## Scope
Add a focused .NET 8 xUnit test project covering the concrete security primitives already implemented: one-way password hashing and JWT claims/signature/issuer/audience/expiry. Keep tests deterministic and independent of a running MySQL instance.

## Out of scope
MySQL integration tests, controller/API behavior, inactive-account enforcement, HR provisioning, refresh tokens and changes to production behavior. Those require separate environment setup or approved business decisions.

## Success criteria
- A test project is included in backend/TeamPulse.sln.
- Tests verify password hashes are not plaintext and accept only the original password.
- Tests validate generated JWT signature, issuer, audience, expiry and required identity claims.
- No production behavior or service ownership changes.
