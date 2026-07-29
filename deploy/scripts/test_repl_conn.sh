#!/bin/bash
# Test replication connectivity from standby to master
# Run on 192.168.2.88

echo "Testing connection to master PostgreSQL as replicator..."
docker run --rm --network host \
  -e PGPASSWORD='****REDACTED****' \
  postgres:15 \
  psql -h 192.168.2.3 -p 5435 -U replicator -d erp_guacamole_db -c "SELECT 1 AS connected;"

echo "Test complete"