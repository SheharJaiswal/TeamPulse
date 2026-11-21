# Design: Shared SvelteKit Accessibility Baseline

## Approach
Implement the baseline in `frontend/src/routes/app.css`, which is imported by the root layout and therefore applies across the existing SvelteKit routes.

## Keyboard focus
Use `:focus-visible` on interactive anchors, buttons and form controls. Apply a clearly visible outline with sufficient offset so focus is not conveyed by color alone. Retain the existing input focus border and box-shadow rules.

## Reduced motion
Use `@media (prefers-reduced-motion: reduce)` to turn off smooth scrolling and reduce transition/animation duration and iteration. Do not change the default motion behavior for users who have not requested reduced motion.

## Risk and compatibility
This is a CSS-only change. It does not alter markup, server actions, API contracts, authentication or data handling. The existing SvelteKit CI workflow is the verification gate.
