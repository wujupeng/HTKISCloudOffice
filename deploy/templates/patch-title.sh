#!/bin/bash
# Patch Guacamole title in WAR file
# Usage: ./patch-title.sh "Your-Title"
# Note: Must re-run after container recreate (docker compose up --force-recreate)

TITLE="${1:-Htkis-Cloud}"
echo "Patching Guacamole title to: ${TITLE}"

sleep 10

docker exec -u root guacamole bash -c "
cd /opt/guacamole/webapp
for lang in en zh; do
    mkdir -p translations
    jar xf guacamole.war translations/\${lang}.json
    perl -i -pe 's/Apache Guacamole/${TITLE}/g' translations/\${lang}.json
    jar uf guacamole.war translations/\${lang}.json
    rm -rf translations
done
"

docker restart guacamole
echo "Title patched to '${TITLE}' (container restarted)"