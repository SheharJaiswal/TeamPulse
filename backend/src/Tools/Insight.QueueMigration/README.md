# Insight RabbitMQ queue migration utility

This is a deliberately narrow copy tool. It transfers messages from one already-existing queue to another already-existing queue. It does not declare, delete, purge, bind or reconfigure queues.

## Safety model

- Default invocation is read-only preflight. It passively inspects both queues and reports the source ready count and consumer count.
- Execution requires both `--execute` and `--confirm-quiesced`. The operator must first stop the Identity RabbitMQ publisher and every consumer of the source queue.
- The utility refuses to run if either source or destination queue reports active consumers.
- It uses manual source acknowledgement, a mandatory publish to the destination queue through the default exchange, and publisher confirms.
- It forces the AMQP delivery mode to persistent (`2`) for the migration copy; body and other supported basic properties/headers are preserved. This deliberate durability upgrade is the only basic-property change.
- Mandatory returns are detected; a returned publication is treated as failure and the source delivery remains unacknowledged.
- Source acknowledgement occurs only after the destination publish is confirmed and the payload digest has been appended to the local JSONL ledger.
- The JSONL ledger contains one payload-digest record per acknowledged message and a per-invocation summary record with source, destination, acknowledged count and completion timestamp.
- A crash after destination confirmation but before source acknowledgement can produce a duplicate. This is intentional at-least-once behavior; do not assume exactly-once transfer.
- Payloads, headers, credentials and message identifiers are never written to the ledger or console. The ledger contains only SHA-256 payload digests, one per acknowledged copy.
- The utility never deletes either queue. Queue deletion/recreation and topology changes remain a separate operator-controlled maintenance step.

## Build

```powershell
dotnet build backend/src/Tools/Insight.QueueMigration/Insight.QueueMigration.csproj -c Release
```

## Preflight

Set `RABBITMQ_URI` using a protected environment variable. Do not place credentials in command-line arguments or source control.

```powershell
dotnet run --project backend/src/Tools/Insight.QueueMigration -- --source teampulse.insight.events --destination teampulse.insight.events.migration
```

Preflight does not consume messages. It requires both queues to exist already.

## Copy to staging

Only during an approved maintenance window, after stopping the Identity publisher and all Insight consumers:

```powershell
dotnet run --project backend/src/Tools/Insight.QueueMigration -- --source teampulse.insight.events --destination teampulse.insight.events.migration --execute --confirm-quiesced --ledger .\artifacts\insight-stage-ledger.jsonl
```

Verify the source queue has zero ready and zero unacknowledged messages and reconcile the ledger before proceeding. The tool can be rerun after interruption; already-confirmed messages may be copied again if the process stopped before acknowledging their source delivery.

After the new source queue is recreated and its bindings are verified, run the same utility in the reverse direction, using the migration queue as source and the new Insight source queue as destination, with a separate ledger file.

## Important limitations

- The utility does not create the temporary queue, recreate the source queue, create the DLQ or change bindings.
- It does not prove that producers are paused. The explicit `--confirm-quiesced` flag is an operator attestation, not a broker-enforced lock.
- A ledger digest is a per-message payload fingerprint, not a cryptographic signature or a globally unique message identifier. Duplicate deliveries can create repeated digest lines.
- Preserve the original source and staging queues until count, digest and topology verification is complete.
- Follow `docs/changes/insight-employee-status-projection-consumer/rabbitmq-migration-runbook.md`. Never purge a queue or reset the shared RabbitMQ volume.
