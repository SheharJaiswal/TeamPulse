# Specification: Shared SvelteKit Accessibility Baseline

## Acceptance criteria
- FAB-01: Keyboard users can identify focused links and buttons through a consistent, high-visibility focus indicator.
- FAB-02: Keyboard users can identify focused form controls; the shared indicator does not remove or weaken the existing input focus styling.
- FAB-03: When the operating system requests reduced motion, the application disables smooth scrolling and minimizes CSS transition/animation durations.
- FAB-04: Default pointer interaction and existing page layouts remain unchanged.
- FAB-05: Frontend unit tests, Svelte/TypeScript checks and production build pass in the existing frontend CI workflow.

## Verification
- Review the shared stylesheet selectors and media query.
- Run the existing frontend CI workflow, which executes `npm test`, `npm run check` and `npm run build`.
