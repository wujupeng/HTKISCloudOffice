#!/bin/bash
# Patch Guacamole title to Htkis-Cloud after container start
sleep 10
EN_JSON=$(docker exec guacamole find /tmp -name 'en.json' -path '*/translations/*' 2>/dev/null | head -1)
if [ -n "$EN_JSON" ]; then
    docker exec guacamole sed -i 's/Apache Guacamole/Htkis-Cloud/g' "$EN_JSON"
    for f in $(docker exec guacamole find /tmp -name '*.json' -path '*/translations/*' 2>/dev/null); do
        docker exec guacamole sed -i 's/Apache Guacamole/Htkis-Cloud/g' "$f"
    done
    echo "Title patched to Htkis-Cloud"
else
    echo "Translation file not found"
fi