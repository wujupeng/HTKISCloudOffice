#!/bin/bash
# Patch Guacamole title in WAR file (persistent across restarts, lost on container recreate)
sleep 10
docker exec -u root guacamole bash -c '
cd /opt/guacamole/webapp
for lang in en zh; do
    mkdir -p translations
    jar xf guacamole.war translations/${lang}.json
    perl -i -pe "s/Apache Guacamole/Htkis-Cloud/g" translations/${lang}.json
    jar uf guacamole.war translations/${lang}.json
    rm -rf translations
done
'
# Restart to reload patched WAR
docker restart guacamole
echo "Title patched to Htkis-Cloud (container restarted)"
