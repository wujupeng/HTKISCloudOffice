#!/bin/bash
# Rebuild <BACKUP_IP> as standby from new primary <MASTER_IP>

set -e

# 1. Stop ERP containers
docker stop erp-guacamole erp-guac-postgres 2>/dev/null || true
docker rm erp-guac-postgres 2>/dev/null || true

# 2. Remove old data
docker volume rm erp-guacamole_erp_pgdata 2>/dev/null || true
docker volume create erp-guacamole_erp_pgdata

# 3. pg_basebackup from 114
echo "Starting pg_basebackup from <MASTER_IP>:5435..."
docker run --rm \
  --network host \
  -e PGPASSWORD='${REPL_PASSWORD}' \
  -v erp-guacamole_erp_pgdata:/var/lib/postgresql/data \
  postgres:15 \
  bash -c "rm -rf /var/lib/postgresql/data/* && pg_basebackup -h <MASTER_IP> -p 5435 -U replicator -D /var/lib/postgresql/data -Fp -Xs -P -R && chown -R 999:999 /var/lib/postgresql/data"

echo "Base backup complete!"

# 4. Verify standby.signal
docker run --rm -v erp-guacamole_erp_pgdata:/var/lib/postgresql/data postgres:15 ls -la /var/lib/postgresql/data/standby.signal

# 5. Restart ERP containers
cd /home/debian/Cloud/erp-guacamole && docker-compose up -d

echo "Standby rebuild complete!"