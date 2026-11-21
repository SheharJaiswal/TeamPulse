# Notification RabbitMQ queue migration

## Scope and safety

This runbook applies only to the existing `teampulse.notification.events` queue. Its current durable classic queue has no arguments. The target topology is a durable quorum source queue with delivery limit 5, at-least-once dead-lettering, reject-publish overflow, and a durable quorum DLQ named `teampulse.notification.events.dlq`.

RabbitMQ queue type and declaration arguments are immutable. Importing updated definitions over the existing classic queue before replacing it can fail with an inequivalent-argument precondition error. **Do not delete the queue while it contains messages, reset the shared RabbitMQ volume, or discard pending lifecycle events.**

## Preconditions

1. Schedule an approved maintenance window with Identity publisher and Notification consumer owners.
2. Stop the Notification API consumer and pause the Identity lifecycle publisher.
3. Record queue ready/unacknowledged counts and confirm no consumer remains:
   ```sh
   docker compose exec rabbitmq rabbitmqctl list_queues name type messages_ready messages_unacknowledged consumers
   ```
4. If any messages are pending or unacknowledged, use RabbitMQ Shovel or an approved durable broker-to-broker transfer to move them to a temporary durable migration-buffer queue. Do not use destructive get/ack operations. Verify the source is empty and the buffer count matches the recorded count.
5. Keep the buffer and its messages until the target queue is declared, rebound, restored and verified.

## Queue replacement

1. With publisher and consumer stopped and the source verified empty, remove only `teampulse.notification.events`.
2. Import/apply the updated `infra/rabbitmq/definitions.json` or declare the queue using these exact arguments:
   - `x-queue-type=quorum`
   - `x-delivery-limit=5`
   - `x-dead-letter-exchange=teampulse.domain.events.dlx`
   - `x-dead-letter-routing-key=notification.employee.lifecycle.dead`
   - `x-dead-letter-strategy=at-least-once`
   - `x-overflow=reject-publish`
3. Declare durable quorum queue `teampulse.notification.events.dlq` and bind it to the existing direct DLX using `notification.employee.lifecycle.dead`.
4. Verify the source binding from `teampulse.domain.events` with routing key `identity.employee.lifecycle`.
5. Transfer every buffered message back to the new source queue. Verify the buffer is empty and the restored source count accounts for the original pending messages.
6. Retain the empty buffer until application verification is complete; then remove the empty buffer queue.
7. Start Notification API with the consumer explicitly enabled. Verify `/health/consumer` and delivery/replay behavior before resuming Identity publishing.

## Verification and rollback

- Confirm only Notification's source queue changed; Habit, Challenge, Event Tracking and Insight queues/arguments/bindings remain unchanged.
- Confirm source and DLQ are durable quorum queues with exact arguments and binding.
- Account for every pre-migration message as pending, processed or explicitly quarantined under an operator-approved reason.
- Do not inspect/log raw event bodies, optional notes, credentials or broker URIs.
- If transfer or queue creation fails, keep publishers and consumers stopped and preserve the buffer. Restore the prior classic queue only under an approved rollback plan; never purge the buffer to recover service.
- Fresh CI/Testcontainers brokers load the new definitions directly and do not need this persistent-volume migration.
