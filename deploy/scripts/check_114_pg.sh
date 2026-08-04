#!/bin/bash
echo '=== wal_level ==='
docker exec erp-guac-postgres psql -U guacamole -d erp_guacamole_db -t -c "SHOW wal_level;"
echo '=== replicator user ==='
docker exec erp-guac-postgres psql -U guacamole -d erp_guacamole_db -t -c "SELECT rolname FROM pg_roles WHERE rolname='replicator';"
echo '=== pg_hba replication ==='
docker exec erp-guac-postgres cat /var/lib/postgresql/data/pg_hba.conf | grep replication
echo '=== port binding ==='
docker port erp-guac-postgres
echo '=== pg_is_in_recovery ==='
docker exec erp-guac-postgres psql -U guacamole -d erp_guacamole_db -t -c "SELECT pg_is_in_recovery();"