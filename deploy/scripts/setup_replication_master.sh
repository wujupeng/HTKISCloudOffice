#!/bin/bash
# Setup PostgreSQL streaming replication on master

# 1. Create replication user
docker exec erp-guac-postgres psql -U guacamole -d erp_guacamole_db -c "CREATE ROLE replicator WITH REPLICATION LOGIN PASSWORD '****REDACTED****';" 2>/dev/null || echo "replicator user may already exist"

# 2. Configure postgresql.conf for replication
docker exec erp-guac-postgres bash -c "cat >> /var/lib/postgresql/data/postgresql.conf <<'EOF'

# Streaming Replication Configuration
wal_level = replica
max_wal_senders = 3
wal_keep_size = 64MB
hot_standby = on
EOF
"

# 3. Add replication entry to pg_hba.conf
docker exec erp-guac-postgres bash -c "echo 'host replication replicator 192.168.2.88/32 md5' >> /var/lib/postgresql/data/pg_hba.conf"

# 4. Restart PostgreSQL
docker restart erp-guac-postgres

echo "Master replication setup complete"