#!/bin/bash
# Promote 114 to primary and configure for replication to 3

echo '=== Step 1: Promote standby to primary ==='
docker exec erp-guac-postgres pg_promote
echo 'Promoted!'

echo '=== Step 2: Verify no longer in recovery ==='
docker exec erp-guac-postgres psql -U guacamole -d erp_guacamole_db -t -c "SELECT pg_is_in_recovery();"

echo '=== Step 3: Update pg_hba.conf to allow <BACKUP_IP> ==='
docker exec erp-guac-postgres bash -c "echo 'host replication replicator <BACKUP_IP>/32 md5' >> /var/lib/postgresql/data/pg_hba.conf"

echo '=== Step 4: Reload PostgreSQL ==='
docker exec erp-guac-postgres psql -U guacamole -d erp_guacamole_db -c "SELECT pg_reload_conf();"

echo '=== Done ==='