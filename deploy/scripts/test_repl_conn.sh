#!/bin/bash
# Test replication connectivity from standby to master
# Run on standby server

echo "Testing connection to master PostgreSQL as replicator..."
docker run --rm --network host \
  -e PGPASSWORD='****' \
  postgres:15 \
  psql -h **** -p 5435 -U replicator -d erp_guacamole_db -c "SELECT 1 AS connected;"

echo "Test complete"