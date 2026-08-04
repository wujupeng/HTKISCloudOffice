#!/bin/bash
echo "=== 114 (Primary) replication status ==="
docker exec erp-guac-postgres psql -U guacamole -d erp_guacamole_db -c "SELECT client_addr, state, sent_lsn, replay_lsn FROM pg_stat_replication;"
echo "=== 3 (Standby) recovery status ==="