## Next product increment: habit activity submissions

- The next planned vertical slice is employee self-reported habit activity and personal progress history.
- Service ownership is fixed: Event Tracking owns activity submissions/history; Habit remains the source of truth for habit definitions. Event Tracking must validate habit ownership through the Habit API and must not access the Habit database.
- The SDD change is drafted under `docs/changes/habit-activity-submissions/`. Implementation is intentionally blocked pending owner decisions in Open Question 20 covering duplicate/multiple submissions, correction/deletion, backdating/time-zone semantics and quantity/target behavior.
- This is a draft design, not an implemented API or UI capability.

Updated: 2026-11-21
