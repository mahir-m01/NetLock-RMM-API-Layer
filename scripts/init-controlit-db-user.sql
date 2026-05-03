-- ControlIT API — Dedicated Database User Setup
-- Run this script against the NetLock MySQL instance as root to create
-- a least-privilege user for the ControlIT API.
--
-- Usage:
--   mysql -u root -p < scripts/init-controlit-db-user.sql
--
-- Replace '__CONTROLIT_DB_PASSWORD__' with CONTROLIT_DB_PASSWORD from .env.
-- Replace `iphbmh` if MYSQL_DATABASE uses a different database name.
-- Do not run the API with root. Use root/migration credentials only for EF
-- migrations, then run ControlIT with this least-privilege user.

CREATE USER IF NOT EXISTS 'controlit_api'@'%' IDENTIFIED BY '__CONTROLIT_DB_PASSWORD__';

-- Read-only on all NetLock tables (device inventory, tenants, etc.)
GRANT SELECT ON `iphbmh`.* TO 'controlit_api'@'%';

-- Runtime DML on ControlIT-owned tables. No CREATE/ALTER/INDEX at runtime.
GRANT SELECT, INSERT, UPDATE, DELETE
    ON `iphbmh`.`controlit_%`
    TO 'controlit_api'@'%';

FLUSH PRIVILEGES;
