#!/bin/bash
# Configure 114 as replication primary for 192.168.2.3

# 1. Change port binding to allow external access
sed -i 's/127.0.0.1:5435:5432/5435:5432/' /home/debian/Cloud/erp-guacamole/docker-compose.yml

# 2. Restart postgres with new port binding
cd /home/debian/Cloud/erp-guacamole && docker-compose up -d postgres

# 3. Add UFW rule for 192.168.2.3
echo 9090 | sudo -S ufw allow from 192.168.2.3 to any port 5435 proto tcp 2>&1

# 4. Verify
docker port erp-guac-postgres
echo "Done configuring 114 as primary"