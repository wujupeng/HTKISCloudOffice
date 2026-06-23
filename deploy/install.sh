#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_FILE="${SCRIPT_DIR}/config.env"

if [ ! -f "$CONFIG_FILE" ]; then
    echo "ERROR: config.env not found. Copy config.env.example to config.env and fill in values."
    echo "  cp config.env.example config.env"
    exit 1
fi

source "$CONFIG_FILE"

echo "============================================"
echo " HTKIS Cloud Office - Automated Deployment"
echo " Customer: ${CUSTOMER_NAME}"
echo " Domain:   ${DOMAIN}"
echo "============================================"

confirm() {
    read -p "$1 [y/N] " -n 1 -r
    echo
    [[ $REPLY =~ ^[Yy]$ ]]
}

# ============================================================
# Phase 1: Debian LAN Server Setup
# ============================================================
deploy_lan_server() {
    echo ""
    echo "====== Phase 1: Debian LAN Server (${LAN_HOST}) ======"
    echo ""

    SSH_CMD="ssh -o StrictHostKeyChecking=no -p ${LAN_SSH_PORT} ${LAN_USER}@${LAN_HOST}"

    echo "[1/7] Installing system dependencies..."
    $SSH_CMD "echo '${LAN_SUDO_PASSWORD}' | sudo -S apt-get update -qq && \
        echo '${LAN_SUDO_PASSWORD}' | sudo -S apt-get install -y -qq docker.io docker-compose-v2 samba autossh sshpass perl 2>/dev/null; \
        echo '${LAN_SUDO_PASSWORD}' | sudo -S systemctl enable docker && \
        echo '${LAN_SUDO_PASSWORD}' | sudo -S systemctl start docker"
    echo "  Done."

    echo "[2/7] Installing frpc ${FRP_VERSION}..."
    $SSH_CMD "if [ ! -f /usr/local/bin/frpc ] || [ \"\$(frpc --version 2>/dev/null)\" != \"${FRP_VERSION}\" ]; then \
        cd /tmp && \
        if [ -f /tmp/frp_${FRP_VERSION}_linux_amd64.tar.gz ]; then \
            echo 'Using cached frp archive'; \
        else \
            wget -q --timeout=60 https://ghfast.top/https://github.com/fatedier/frp/releases/download/v${FRP_VERSION}/frp_${FRP_VERSION}_linux_amd64.tar.gz -O /tmp/frp_${FRP_VERSION}_linux_amd64.tar.gz; \
        fi && \
        tar xzf /tmp/frp_${FRP_VERSION}_linux_amd64.tar.gz -C /tmp/ && \
        echo '${LAN_SUDO_PASSWORD}' | sudo -S cp /tmp/frp_${FRP_VERSION}_linux_amd64/frpc /usr/local/bin/frpc && \
        echo '${LAN_SUDO_PASSWORD}' | sudo -S cp /tmp/frp_${FRP_VERSION}_linux_amd64/frps /usr/local/bin/frps; \
    fi && frpc --version"
    echo "  Done."

    echo "[3/7] Deploying Guacamole (Docker Compose)..."
    $SSH_CMD "mkdir -p ~/Cloud/guacamole/init ~/Cloud/guacamole/guacamole-home"
    scp -o StrictHostKeyChecking=no -P ${LAN_SSH_PORT} \
        "${SCRIPT_DIR}/templates/docker-compose.yml" \
        "${LAN_USER}@${LAN_HOST}:~/Cloud/guacamole/docker-compose.yml"
    echo "  Done."

    echo "[4/7] Starting Guacamole containers..."
    $SSH_CMD "cd ~/Cloud/guacamole && docker compose up -d"
    echo "  Done. Waiting 30s for Guacamole to initialize..."
    sleep 30

    echo "[5/7] Configuring frpc..."
    $SSH_CMD "echo '${LAN_SUDO_PASSWORD}' | sudo -S mkdir -p /etc/frp"
    cat "${SCRIPT_DIR}/templates/frpc.toml.template" | \
        sed "s/{{WAN_HOST}}/${WAN_HOST}/g" | \
        sed "s/{{FRPS_PORT}}/${FRPS_PORT}/g" | \
        sed "s/{{FRP_TOKEN}}/${FRP_TOKEN}/g" | \
        sed "s/{{CUSTOMER_NAME}}/${CUSTOMER_NAME}/g" | \
        sed "s/{{GUAC_PORT}}/${GUAC_PORT}/g" | \
        sed "s/{{FRP_REMOTE_PORT}}/${FRP_REMOTE_PORT}/g" | \
        ssh -o StrictHostKeyChecking=no -p ${LAN_SSH_PORT} ${LAN_USER}@${LAN_HOST} \
        "cat | echo '${LAN_SUDO_PASSWORD}' | sudo -S tee /etc/frp/frpc.toml > /dev/null"

    scp -o StrictHostKeyChecking=no -P ${LAN_SSH_PORT} \
        "${SCRIPT_DIR}/templates/frpc.service" \
        "${LAN_USER}@${LAN_HOST}:/tmp/frpc.service"
    $SSH_CMD "echo '${LAN_SUDO_PASSWORD}' | sudo -S cp /tmp/frpc.service /etc/systemd/system/frpc.service && \
        echo '${LAN_SUDO_PASSWORD}' | sudo -S systemctl daemon-reload && \
        echo '${LAN_SUDO_PASSWORD}' | sudo -S systemctl enable frpc && \
        echo '${LAN_SUDO_PASSWORD}' | sudo -S systemctl start frpc"
    echo "  Done."

    echo "[6/7] Patching Guacamole title to '${APP_TITLE}'..."
    $SSH_CMD "cd ~/Cloud/guacamole && docker exec -u root guacamole bash -c ' \
        cd /opt/guacamole/webapp && \
        for lang in en zh; do \
            mkdir -p translations && \
            jar xf guacamole.war translations/\${lang}.json && \
            perl -i -pe \"s/Apache Guacamole/${APP_TITLE}/g\" translations/\${lang}.json && \
            jar uf guacamole.war translations/\${lang}.json && \
            rm -rf translations; \
        done' && \
        docker restart guacamole"
    echo "  Done. Waiting 20s for restart..."
    sleep 20

    echo "[7/7] Configuring Samba..."
    cat "${SCRIPT_DIR}/templates/smb.conf.template" | \
        sed "s/{{SAMBA_WORKGROUP}}/${SAMBA_WORKGROUP}/g" | \
        sed "s|{{SAMBA_SHARE_PATH}}|${SAMBA_SHARE_PATH}|g" | \
        ssh -o StrictHostKeyChecking=no -p ${LAN_SSH_PORT} ${LAN_USER}@${LAN_HOST} \
        "cat | echo '${LAN_SUDO_PASSWORD}' | sudo -S tee /etc/samba/smb.conf > /dev/null"
    $SSH_CMD "echo '${LAN_SUDO_PASSWORD}' | sudo -S mkdir -p ${SAMBA_SHARE_PATH} && \
        echo '${LAN_SUDO_PASSWORD}' | sudo -S chmod 2777 ${SAMBA_SHARE_PATH} && \
        echo '${LAN_SUDO_PASSWORD}' | sudo -S systemctl restart smbd nmbd"
    echo "  Done."

    echo ""
    echo "====== LAN Server deployment complete! ======"
}

# ============================================================
# Phase 2: Ubuntu WAN Server Setup
# ============================================================
deploy_wan_server() {
    echo ""
    echo "====== Phase 2: Ubuntu WAN Server (${WAN_HOST}) ======"
    echo ""
    echo "NOTE: Ubuntu SSH may not be open. You may need to run commands manually."
    echo "      Commands will be saved to /tmp/wan_commands.sh for manual execution."
    echo ""

    WAN_CMD_FILE="/tmp/wan_commands_${CUSTOMER_NAME}.sh"

    cat > "$WAN_CMD_FILE" << WANEOF
#!/bin/bash
set -e

echo "[1/4] Installing frps ${FRP_VERSION}..."
if [ ! -f /usr/local/bin/frps ] || [ "\$(frps --version 2>/dev/null)" != "${FRP_VERSION}" ]; then
    cd /tmp
    if [ -f /tmp/frp_${FRP_VERSION}_linux_amd64.tar.gz ]; then
        echo 'Using cached frp archive'
    else
        wget -q --timeout=60 https://ghfast.top/https://github.com/fatedier/frp/releases/download/v${FRP_VERSION}/frp_${FRP_VERSION}_linux_amd64.tar.gz -O /tmp/frp_${FRP_VERSION}_linux_amd64.tar.gz
    fi
    tar xzf /tmp/frp_${FRP_VERSION}_linux_amd64.tar.gz -C /tmp/
    cp /tmp/frp_${FRP_VERSION}_linux_amd64/frps /usr/local/bin/frps
fi
frps --version

echo "[2/4] Configuring frps-htkis..."
mkdir -p /etc/frp
python3 -c "open('/etc/frp/frps-htkis.toml','w').write('bindPort = ${FRPS_PORT}\\n\\nauth.method = \"token\"\\nauth.token = \"${FRP_TOKEN}\"\\n\\nwebServer.addr = \"0.0.0.0\"\\nwebServer.port = ${FRPS_DASHBOARD_PORT}\\nwebServer.user = \"${FRPS_DASHBOARD_USER}\"\\nwebServer.password = \"${FRPS_DASHBOARD_PASSWORD}\"\\n')"

python3 -c "open('/etc/systemd/system/frps-htkis.service','w').write('[Unit]\\nDescription=frps server for HTKIS\\nAfter=network.target\\n\\n[Service]\\nType=simple\\nExecStart=/usr/local/bin/frps -c /etc/frp/frps-htkis.toml\\nRestart=always\\nRestartSec=5\\n\\n[Install]\\nWantedBy=multi-user.target\\n')"

systemctl daemon-reload
systemctl enable frps-htkis
systemctl start frps-htkis
sleep 2
systemctl status frps-htkis --no-pager

echo "[3/4] Configuring nginx for ${DOMAIN}..."
mkdir -p /etc/nginx/sites-available
python3 -c "open('/etc/nginx/sites-available/${DOMAIN}','w').write('server {\\n    listen 80;\\n    server_name ${DOMAIN};\\n    client_max_body_size 100M;\\n\\n    location = / {\\n        return 302 /guacamole/;\\n    }\\n\\n    location / {\\n        proxy_pass http://127.0.0.1:${FRP_REMOTE_PORT};\\n        proxy_set_header Host \$host;\\n        proxy_set_header X-Real-IP \$remote_addr;\\n        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;\\n        proxy_set_header X-Forwarded-Proto \$scheme;\\n        proxy_http_version 1.1;\\n        proxy_set_header Upgrade \$http_upgrade;\\n        proxy_set_header Connection \"upgrade\";\\n        proxy_read_timeout 3600s;\\n        proxy_send_timeout 3600s;\\n    }\\n}\\n')"

ln -sf /etc/nginx/sites-available/${DOMAIN} /etc/nginx/sites-enabled/${DOMAIN}
nginx -t && systemctl reload nginx

echo "[4/4] Verifying..."
sleep 2
curl -s -o /dev/null -w 'HTTP %{http_code}' http://127.0.0.1:${FRP_REMOTE_PORT}/guacamole/ 2>/dev/null || echo 'frps tunnel not ready yet (frpc needs to connect first)'
echo ""
echo "WAN Server deployment complete!"
WANEOF

    chmod +x "$WAN_CMD_FILE"
    echo "WAN server commands saved to: $WAN_CMD_FILE"
    echo ""
    echo "To execute on Ubuntu, copy and run:"
    echo "  cat $WAN_CMD_FILE | ssh root@${WAN_HOST} 'bash -s'"
    echo ""
    echo "Or if SSH is not available, copy the file content and paste in Ubuntu terminal."
}

# ============================================================
# Phase 3: Post-deployment verification
# ============================================================
verify_deployment() {
    echo ""
    echo "====== Phase 3: Verification ======"
    echo ""

    echo "[1/4] Checking Guacamole on LAN..."
    LAN_SSH_CMD="ssh -o StrictHostKeyChecking=no -p ${LAN_SSH_PORT} ${LAN_USER}@${LAN_HOST}"
    GUAC_STATUS=$($LAN_SSH_CMD "curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:${GUAC_PORT}/guacamole/" 2>/dev/null || echo "FAIL")
    echo "  Guacamole local: $GUAC_STATUS"

    echo "[2/4] Checking frpc connection..."
    FRPC_STATUS=$($LAN_SSH_CMD "sudo systemctl is-active frpc" 2>/dev/null || echo "unknown")
    echo "  frpc status: $FRPC_STATUS"

    echo "[3/4] Checking public access..."
    PUB_STATUS=$(curl -s -o /dev/null -w '%{http_code}' "http://${DOMAIN}/guacamole/" 2>/dev/null || echo "FAIL")
    echo "  http://${DOMAIN}: $PUB_STATUS"

    PUBS_STATUS=$(curl -sk -o /dev/null -w '%{http_code}' "https://${DOMAIN}/guacamole/" 2>/dev/null || echo "FAIL")
    echo "  https://${DOMAIN}: $PUBS_STATUS"

    echo "[4/4] Checking title..."
    TITLE=$(curl -s "http://${DOMAIN}/guacamole/translations/en.json" 2>/dev/null | grep -o '"Htkis-Cloud"\|"Apache Guacamole"' | head -1 || echo "unknown")
    echo "  Title: $TITLE"

    echo ""
    echo "============================================"
    if [ "$PUB_STATUS" = "200" ] || [ "$PUBS_STATUS" = "200" ]; then
        echo " DEPLOYMENT SUCCESSFUL!"
    else
        echo " DEPLOYMENT INCOMPLETE - check verification results above"
    fi
    echo "============================================"
}

# ============================================================
# Main
# ============================================================
echo ""
echo "This script will deploy HTKIS Cloud Office to:"
echo "  LAN Server: ${LAN_USER}@${LAN_HOST}:${LAN_SSH_PORT}"
echo "  WAN Server: root@${WAN_HOST}:${WAN_SSH_PORT}"
echo "  Domain:     ${DOMAIN}"
echo ""

if ! confirm "Continue with deployment?"; then
    echo "Aborted."
    exit 0
fi

deploy_lan_server
deploy_wan_server

echo ""
echo "LAN server is deployed. Now run the WAN server commands on Ubuntu."
echo "After WAN server is ready, come back and press Enter to verify."
read -p "Press Enter when WAN server is configured..."

verify_deployment