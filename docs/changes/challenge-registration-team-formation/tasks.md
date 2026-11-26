# Tasks: Challenge Registration and HR Team Formation

- [x] Add OpenSpec proposal, design, and requirements.
- [x] Add challenge registration lifecycle and employee response domain model.
- [x] Add challenge team and member domain models with uniqueness constraints.
- [x] Add EF Core migration preserving existing challenges and participation rows.
- [x] Add an HR-scoped Identity employee directory endpoint.
- [x] Implement queued SMTP invitation dispatch with delivery retry and status tracking.
- [x] Implement employee Join/Decline and registration status endpoints.
- [x] Implement HR registration summary and close-registration endpoints.
- [x] Implement HR team create/assign/finalize endpoints with tenant and role checks; create-and-assign is atomic.
- [x] Update HR Svelte page for response summary, registration closure, team creation and finalization.
- [x] Update employee Svelte page for Join/Decline acknowledgement and team assignment display.
- [x] Add domain and API integration tests.
- [x] Frontend SvelteKit tests, type checks, and production build passed.
- [ ] Run backend tests and fix any CI regressions.
- [ ] Confirm latest backend CI passes and fix any remaining regressions.
- [x] Open draft PR #4 against feature/backend-foundation for review.