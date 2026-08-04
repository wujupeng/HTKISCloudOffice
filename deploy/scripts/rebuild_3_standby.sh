#!/bin/bash
# Rebuild 192.168.2.3 as standby from new primary 192.168.2.114

set -e

# 1. Stop ERP containers
docker stop erp-guacamole erp-guac-postgres 2>/dev/null || true
docker rm erp-guac-postgres 2>/dev/null || true

# 2. Remove old data
docker volume rm erp-guacamole_erp_pgdata 2>/dev/null || true
docker volume create erp-guacamole_erp_pgdata

# 3. pg_basebackup from 114
echo "Starting pg_basebackup from 192.168.2.114:5435..."
docker run --rm \
  --network host \
  -e PGPASSWORD='****REDACTED****' \
  -v erp-guacamole_erp_pgdata:/var/lib/postgresql/data \
  postgres:15 \
  bash -c "rm -rf /var/lib/postgresql/data/* && pg_basebackup -h 192.168.2.114 -p 5435 -U replicator -D /var/lib/postgresql/data -Fp -Xs -P -R && chown -R 999:999 /var/lib/postgresql/data"

echo "Base backup complete!"

# 4. Verify standby.signal
docker run --rm -v erp-guacamole_erp_pgdata:/var/lib/postgresql/data postgres:15 ls -la /var/lib/postgresql/data/standby.signal

# 5. Restart ERP containers
cd /home/debian/Cloud/erp-guacamole && docker-compose up -d

echo "Standby rebuild complete!"