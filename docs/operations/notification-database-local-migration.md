# Notification local MySQL database setup

The Notification service owns the logical database `teampulse_notification`. The checked-in EF Core migration creates only the service-owned projection and inbox tables.

## Fresh local MySQL volume

The Docker Compose configuration mounts `infra/mysql/init/04-notification-database.sql` into the MySQL entrypoint initialization directory. A newly initialized volume creates the database and grants the existing local `teampulse_app` user access.

## Existing persistent MySQL volume

MySQL entrypoint scripts run only when the data directory is initialized. Do not delete/reset `teampulse-mysql-data` to apply this addition.

1. Keep the existing MySQL container and volume.
2. Connect as an administrative MySQL user.
3. Execute the following statements once:

```sql
CREATE DATABASE IF NOT EXISTS teampulse_notification
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;
GRANT ALL PRIVILEGES ON teampulse_notification.* TO 'teampulse_app'@'%';
```

4. Confirm the application user can connect to `teampulse_notification`.
5. Configure the Notification API connection string to use this database. The API applies the checked-in EF migration at startup.

Do not alter any other service database, and do not place cross-service foreign keys in this schema.
