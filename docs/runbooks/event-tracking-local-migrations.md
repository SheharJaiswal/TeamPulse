# Event Tracking local database migration

## New local MySQL volumes

The Compose MySQL service runs `infra/mysql/init/03-event-tracking-database.sql` during first data-directory initialization. This creates the `teampulse_event_tracking` database with the expected `utf8mb4` charset/collation and grants the application user access.

## Existing persistent MySQL volumes

MySQL initialization scripts are not rerun when the data directory already exists. Before starting Event Tracking against an existing local volume, create the database and grant explicitly:

```sh
docker compose exec mysql mysql -uroot -p -e "CREATE DATABASE IF NOT EXISTS teampulse_event_tracking CHARACTER SET utf8mb4 COLLATE utf8mb4_0900_ai_ci; GRANT ALL PRIVILEGES ON teampulse_event_tracking.* TO 'teampulse_app'@'%'; FLUSH PRIVILEGES;"
```

Enter the root password interactively when prompted. Do not put passwords in shell history, scripts, source control, or logs. This command does not reset or delete the existing MySQL volume.

## EF Core schema migration

On startup, EventTracking.API applies the checked-in EF Core migrations to `teampulse_event_tracking` before serving health endpoints. The API connection string must be supplied as `ConnectionStrings__EventTrackingDatabase` (or the equivalent `EventTrackingDatabase` configuration key).

For a new or existing database, verify the API's `/health/ready` endpoint after startup. Readiness checks MySQL connectivity; liveness remains independent of database readiness.

## RabbitMQ

The Event Tracking RabbitMQ consumer and safe migration of the existing persistent Event Tracking queue are not implemented by this database runbook. Do not redeclare the existing queue with different immutable arguments, delete it, or reset the shared RabbitMQ volume until the dedicated consumer/topology increment documents a pending-message preservation procedure.
