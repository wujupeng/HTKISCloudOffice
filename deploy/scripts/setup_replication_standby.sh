#!/bin/bash
# Setup PostgreSQL streaming replication on standby
# This script runs on the standby server

set -e

# 1. Stop ERP PostgreSQL and Guacamole on standby
docker stop erp-guacamole 2>/dev/null || true
docker stop erp-guac-postgres 2>/dev/null || true
docker rm erp-guac-postgres 2>/dev/null || true

# 2. Remove old data volume
docker volume rm erp-guacamole_erp_pgdata 2>/dev/null || true

# 3. Create new volume
docker volume create erp-guacamole_erp_pgdata

# 4. Run pg_basebackup using a temporary postgres container
# The container uses host network to access master
# PGPASSWORD is set for the replicator user
docker run --rm \
  --name pg-basebackup \
  --network host \
  -e PGPASSWORD='${REPL_PASSWORD}' \
  -v erp-guacamole_erp_pgdata:/var/lib/postgresql/data \
  postgres:15 \
  bash -c "
    chown -R postgres:postgres /var/lib/postgresql/data
    rm -rf /var/lib/postgresql/data/*
    su postgres -c 'pg_basebackup -h ${MASTER_IP} -p 5435 -U replicator -D /var/lib/postgresql/data -Fp -Xs -P -R'
    chown -R postgres:postgres /var/lib/postgresql/data
  "

echo "Base backup complete"

# 5. Verify standby.signal file exists
docker run --rm \
  -v erp-guacamole_erp_pgdata:/var/lib/postgresql/data \
  postgres:15 \
  ls -la /var/lib/postgresql/data/standby.signal

echo "Standby replication setup complete - standby.signal confirmed"
