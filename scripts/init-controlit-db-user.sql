-- ControlIT API — Dedicated Database User Reference
-- Reference only. Use scripts/apply-controlit-db-user.sh for install/runtime
-- setup because it reads .env, validates identifiers, discovers concrete
-- controlit_* tables, and grants DML on those exact tables.
--
-- Example read-only bootstrap:
--   mysql -u root -p < scripts/init-controlit-db-user.sql
--
-- Replace '__CONTROLIT_DB_PASSWORD__' with CONTROLIT_DB_PASSWORD from .env.
-- Replace `iphbmh` if MYSQL_DATABASE uses a different database name.
-- Do not run the API with root. Use root/migration credentials only for EF
-- migrations, then run ControlIT with this least-privilege user.
--
-- MySQL GRANT does not treat `controlit_%` as a table wildcard. Do not grant
-- ON `database`.`controlit_%`; that targets one literal table name. Runtime
-- DML grants belong in scripts/apply-controlit-db-user.sh, which grants each
-- existing ControlIT-owned table explicitly.

CREATE USER IF NOT EXISTS 'controlit_api'@'%' IDENTIFIED BY '__CONTROLIT_DB_PASSWORD__';

-- Read-only on all NetLock tables (device inventory, tenants, etc.)
GRANT SELECT ON `iphbmh`.* TO 'controlit_api'@'%';

FLUSH PRIVILEGES;
