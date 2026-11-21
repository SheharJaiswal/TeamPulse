CREATE DATABASE IF NOT EXISTS teampulse_event_tracking
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;

GRANT ALL PRIVILEGES ON teampulse_event_tracking.* TO 'teampulse_app'@'%';
