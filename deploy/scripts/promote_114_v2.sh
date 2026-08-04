#!/bin/bash
# Promote 114 to primary using psql
docker exec erp-guac-postgres psql -U guacamole -d erp_guacamole_db -c "SELECT pg_promote();"
echo '=== Verify ==='
docker exec erp-guac-postgres psql -U guacamole -d erp_guacamole_db -t -c "SELECT pg_is_in_recovery();"