# Event Tracking RabbitMQ queue migration

## Scope and safety

This runbook applies only to the existing `teampulse.event-tracking.events` queue. Its current durable classic queue has no arguments. The new topology changes it to a durable quorum queue with delivery limit 5, at-least-once dead-lettering and reject-publish overflow, and adds `teampulse.event-tracking.events.dlq`.

RabbitMQ queue type and declaration arguments are immutable. Importing the new definitions over the old queue will fail with a precondition error. **Do not delete the existing queue, reset the shared RabbitMQ volume, or discard pending messages as a shortcut.** This migration requires an approved maintenance window and an operator who can verify message preservation.

## Preconditions

1. Schedule a maintenance window and notify the owners of Identity publishing and Event Tracking consumption.
2. Stop the EventTracking.API consumer and pause the Identity lifecycle publisher so no new lifecycle messages are routed during the migration.
3. Record the source queue's ready and unacknowledged counts and confirm no consumer remains attached:
   ```sh
   docker compose exec rabbitmq rabbitmqctl list_queues name type messages_ready messages_unacknowledged consumers
   ```
4. If the queue has any pending or unacknowledged messages, use RabbitMQ Shovel (or an approved equivalent durable broker-to-broker transfer) to move them to a temporary durable migration-buffer queue. Do not use a destructive get/ack operation. Verify the source queue is empty and the buffer count equals the recorded pending count before proceeding.
5. Keep the migration buffer and its messages until the new source queue is created, rebound, and the messages have been transferred back and verified.

## Queue replacement

1. With publishers and consumers still stopped and the source queue verified empty, remove only `teampulse.event-tracking.events`. Do not purge or delete any other queue or volume.
2. Declare the source queue using the exact arguments in `infra/rabbitmq/definitions.json`: quorum, delivery limit 5, DLX `teampulse.domain.events.dlx`, routing key `event-tracking.employee.lifecycle.dead`, dead-letter strategy `at-least-once`, overflow `reject-publish`.
3. Declare durable quorum queue `teampulse.event-tracking.events.dlq` and bind it to the existing direct DLX with routing key `event-tracking.employee.lifecycle.dead`.
4. Restore/verify the source binding from `teampulse.domain.events` with routing key `identity.employee.lifecycle`.
5. Use Shovel (or the approved durable transfer mechanism) to move all buffered messages back to the new source queue. Verify the buffer is empty and the source ready count matches the pre-migration count.
6. Retain the migration buffer until the restored queue and application consumer have been verified. Then remove the empty buffer queue.
7. Start the EventTracking.API consumer, verify `/health/consumer`, and only then resume Identity publishing.

## Verification and rollback

- Confirm the source queue is quorum and has all six expected arguments; confirm the DLQ is quorum and its exact DLX binding exists.
- Confirm the lifecycle source binding remains intact and the pre-migration pending-message count is accounted for (processed, ready, or explicitly quarantined with an operator-approved reason).
- Verify consumer health and inspect only safe message identifiers/classifications; do not log raw bodies, optional notes, broker credentials or broker URIs.
- If queue recreation or message transfer fails, keep publishers/consumers stopped and preserve the migration buffer. Do not resume publishing into a partially migrated topology. Restore the original classic queue and binding only under an operator-approved rollback plan; preserve every message in the buffer.
- Fresh CI/Testcontainers brokers import the new definitions directly and do not require this persistent-volume migration.
