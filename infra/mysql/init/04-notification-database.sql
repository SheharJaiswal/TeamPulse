CREATE DATABASE IF NOT EXISTS teampulse_notification
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;

GRANT ALL PRIVILEGES ON teampulse_notification.* TO 'teampulse_app'@'%';
