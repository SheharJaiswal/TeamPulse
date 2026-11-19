# Change: Identity Service Foundation

## Problem
TeamPulse requires an authoritative identity boundary before downstream services can authenticate users and scope employee-owned data.

## Outcome
Establish an Identity API with employee persistence, password hashing, login and signed JWT issuance.

## Out of scope
Full HR provisioning, SSO, refresh-token rotation, team administration UI, production secret integration and identity lifecycle events.

## Risks
Development registration is not production onboarding. Local credentials must not be deployed. Target SDK/runtime needs verification.