CREATE DATABASE IF NOT EXISTS teampulse_insight
    CHARACTER SET utf8mb4
    COLLATE utf8mb4_0900_ai_ci;

GRANT ALL PRIVILEGES ON teampulse_insight.* TO 'teampulse_app'@'%';
