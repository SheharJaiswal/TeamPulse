# Insight RabbitMQ queue migration runbook

**Status:** Prepared for review. The repository definitions intentionally retain the existing durable classic source queue with no arguments and no Insight DLQ. Do not change definitions or execute against a persistent broker until the migration utility and maintenance window are separately reviewed.

## Scope and invariants

The currently committed RabbitMQ definitions must continue to declare the existing Insight queue as durable classic with no arguments, with its existing domain-exchange binding. The target topology below is a migration destination only; it must not be imported or activated before the migration is completed and verified. The topology contract tests intentionally assert the current classic queue until that point.

This procedure changes only `teampulse.insight.events` and adds `teampulse.insight.events.dlq`. It must not alter the Habit, Challenge, Event Tracking or Notification queues, bindings, exchanges or persistent volumes. The target Insight source queue is durable quorum with delivery limit 5, at-least-once dead-lettering to `teampulse.domain.events.dlx`, routing key `insight.employee.lifecycle.dead`, and `reject-publish` overflow. The DLQ is a durable quorum queue bound to that routing key.

Never use `docker compose down -v`, `rabbitmqctl purge_queue`, or any command that resets the shared RabbitMQ data volume.

## Preconditions

1. Schedule a maintenance window and notify operators that Identity lifecycle event publication and Insight consumption will pause.
2. Confirm the exact broker/vhost and record current queue declaration, binding, ready count and unacknowledged count for the Insight queue.
3. Confirm the migration utility is available and reviewed. It must preserve message bodies and supported AMQP basic properties/headers, deliberately force delivery mode to persistent (`2`), use mandatory routing and publisher confirms, and acknowledge each source message only after the destination confirms it and its digest is appended to the ledger.
4. Confirm a durable temporary quorum queue named `teampulse.insight.events.migration` can be created and has sufficient disk capacity.
5. Ensure no other tool or consumer is consuming the Insight source queue.

If any precondition is not met, stop. Do not attempt an in-place redeclaration.

## Migration procedure

1. Disable the Insight consumer (`InsightConsumer:Enabled=false`) and stop its host. Wait until all in-flight deliveries have settled and the source queue reports zero unacknowledged messages.
2. Pause the Identity local RabbitMQ outbox publisher. Confirm it is no longer publishing lifecycle events. Keep the Identity outbox records intact; do not delete or mark pending outbox messages as dispatched manually.
3. Capture source queue ready/unacknowledged counts and the current binding to `teampulse.domain.events` with routing key `identity.employee.lifecycle`.
4. Declare the temporary migration queue as durable quorum. Do not bind it to the domain exchange.
5. Run the reviewed copy utility from the source queue to the migration queue. For every message: consume with manual acknowledgement, republish the original body and supported properties/headers with delivery mode forced to persistent, require mandatory routing and publisher confirmation, append the payload digest, and only then acknowledge the source delivery. If the utility crashes between destination confirmation and source acknowledgement, duplicates are acceptable; the Insight inbox deduplicates by eventId.
6. Wait until the original source queue has zero ready and zero unacknowledged messages. Compare the migration queue count with the recorded source count and validate the utility's message count/checksum ledger. If counts or checksums differ, stop and reconcile; do not delete either queue.
7. Delete only `teampulse.insight.events`. Recreate it as durable quorum with `x-delivery-limit=5`, `x-dead-letter-exchange=teampulse.domain.events.dlx`, `x-dead-letter-routing-key=insight.employee.lifecycle.dead`, `x-dead-letter-strategy=at-least-once`, and `x-overflow=reject-publish`. Restore the original binding to `teampulse.domain.events` using `identity.employee.lifecycle`.
8. Declare `teampulse.insight.events.dlq` as durable quorum and bind it to `teampulse.domain.events.dlx` using `insight.employee.lifecycle.dead`.
9. Copy all messages from the migration queue back to the new Insight source queue using the same confirmed-copy/manual-ack procedure. Verify the migration queue is empty, the source count matches the ledger, and the source/DLQ declarations and bindings exactly match the target topology.
10. Keep the temporary queue until post-migration verification is complete. Then delete only `teampulse.insight.events.migration`.
11. Only after all migration checks pass, update the committed RabbitMQ definitions and topology contract tests to the target source/DLQ topology. Review and deploy that definitions change in the same controlled maintenance window. Resume Identity publication, then enable/start the Insight consumer. Monitor queue depth, unacknowledged count, consumer health, DLQ depth and duplicate outcomes.

## Rollback

If the target topology or restore verification fails, keep Identity publication and Insight consumption paused. Do not purge messages. Copy any messages currently in the target source queue back to the temporary migration queue with confirmed publication before acknowledging them. Delete only the new Insight source queue, recreate the original durable classic queue with no arguments and its original binding, then restore messages from the migration queue using confirmed publication/manual acknowledgement. Verify counts and checksums before resuming publishers. Retain the migration queue until rollback is verified.

## Operational evidence to retain

Record the maintenance window, operator, broker/vhost, before/after queue declarations, bindings, ready/unacknowledged counts, migration utility version, source/destination counts, checksum ledger, DLQ verification, rollback decision and resume timestamps. Do not include message bodies, optional HR notes, credentials or connection strings in the report.
