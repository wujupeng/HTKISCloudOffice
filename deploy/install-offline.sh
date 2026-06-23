#!/bin/bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
OFFLINE_DIR="$(dirname "$SCRIPT_DIR")"
CONFIG_FILE="${SCRIPT_DIR}/config.env"

if [ ! -f "$CONFIG_FILE" ]; then
    echo "ERROR: config.env not found. Copy config.env.example to config.env and fill in values."
    exit 1
fi

source "$CONFIG_FILE"

echo "============================================"
echo " HTKIS Cloud Office - OFFLINE Deployment"
echo " Customer: ${CUSTOMER_NAME}"
echo " Domain:   ${DOMAIN}"
echo "============================================"

confirm() {
    read -p "$1 [y/N] " -n 1 -r
    echo
    [[ $REPLY =~ ^[Yy]$ ]]
}

# ============================================================
# Step 1: Load Docker images from local tar files
# ============================================================
load_docker_images() {
    echo ""
    echo "====== Step 1: Loading Docker images ======"
    IMG_DIR="${OFFLINE_DIR}/docker-images"
    
    for img in guacd guacamole postgres15; do
        if [ -f "${IMG_DIR}/${img}.tar" ]; then
            echo "  Loading ${img}.tar..."
            sudo docker load -i "${IMG_DIR}/${img}.tar"
        else
            echo "  WARNING: ${img}.tar not found, skipping"
        fi
    done
    echo "  Done."
}

# ============================================================
# Step 2: Install frpc/frps from local archive
# ============================================================
install_frp() {
    echo ""
    echo "====== Step 2: Installing frp ${FRP_VERSION} ======"
    FRP_ARCHIVE="${OFFLINE_DIR}/packages/frp_${FRP_VERSION}_linux_amd64.tar.gz"
    
    if [ ! -f "$FRP_ARCHIVE" ]; then
        echo "ERROR: frp archive not found at $FRP_ARCHIVE"
        exit 1
    fi
    
    cd /tmp
    tar xzf "$FRP_ARCHIVE"
    sudo cp "/tmp/frp_${FRP_VERSION}_linux_amd64/frpc" /usr/local/bin/frpc
    sudo cp "/tmp/frp_${FRP_VERSION}_linux_amd64/frps" /usr/local/bin/frps
    frpc --version
    echo "  Done."
}

# ============================================================
# Step 3: Deploy LAN server components
# ============================================================
deploy_lan() {
    echo ""
    echo "====== Step 3: Deploying LAN Server ======"

    # Guacamole
    echo "[3.1] Setting up Guacamole..."
    mkdir -p ~/Cloud/guacamole/init ~/Cloud/guacamole/guacamole-home
    cp "${SCRIPT_DIR}/templates/docker-compose.yml" ~/Cloud/guacamole/docker-compose.yml
    cp "${SCRIPT_DIR}/init/"*.sql ~/Cloud/guacamole/init/ 2>/dev/null || true
    
    # Replace template variables in docker-compose.yml
    sed -i "s/{{LAN_HOST}}/${LAN_HOST}/g" ~/Cloud/guacamole/docker-compose.yml
    
    cd ~/Cloud/guacamole && docker compose up -d
    echo "  Waiting 30s for Guacamole to initialize..."
    sleep 30

    # frpc
    echo "[3.2] Configuring frpc..."
    sudo mkdir -p /etc/frp
    cat "${SCRIPT_DIR}/templates/frpc.toml.template" | \
        sed "s/{{WAN_HOST}}/${WAN_HOST}/g" | \
        sed "s/{{FRPS_PORT}}/${FRPS_PORT}/g" | \
        sed "s/{{FRP_TOKEN}}/${FRP_TOKEN}/g" | \
        sed "s/{{CUSTOMER_NAME}}/${CUSTOMER_NAME}/g" | \
        sed "s/{{GUAC_PORT}}/${GUAC_PORT}/g" | \
        sed "s/{{FRP_REMOTE_PORT}}/${FRP_REMOTE_PORT}/g" | \
        sudo tee /etc/frp/frpc.toml > /dev/null
    
    sudo cp "${SCRIPT_DIR}/templates/frpc.service" /etc/systemd/system/frpc.service
    sudo systemctl daemon-reload
    sudo systemctl enable frpc
    sudo systemctl start frpc
    echo "  Done."

    # Patch title
    echo "[3.3] Patching Guacamole title..."
    docker exec -u root guacamole bash -c "
        cd /opt/guacamole/webapp
        for lang in en zh; do
            mkdir -p translations
            jar xf guacamole.war translations/\${lang}.json
            perl -i -pe 's/Apache Guacamole/${APP_TITLE}/g' translations/\${lang}.json
            jar uf guacamole.war translations/\${lang}.json
            rm -rf translations
        done"
    docker restart guacamole
    echo "  Done."

    # Samba
    echo "[3.4] Configuring Samba..."
    cat "${SCRIPT_DIR}/templates/smb.conf.template" | \
        sed "s/{{SAMBA_WORKGROUP}}/${SAMBA_WORKGROUP}/g" | \
        sed "s|{{SAMBA_SHARE_PATH}}|${SAMBA_SHARE_PATH}|g" | \
        sudo tee /etc/samba/smb.conf > /dev/null
    sudo mkdir -p ${SAMBA_SHARE_PATH}
    sudo chmod 2777 ${SAMBA_SHARE_PATH}
    sudo systemctl restart smbd nmbd 2>/dev/null || true
    echo "  Done."
}

# ============================================================
# Step 4: Print WAN server commands
# ============================================================
print_wan_commands() {
    echo ""
    echo "====== Step 4: WAN Server Commands ======"
    echo ""
    echo "Run the following on Ubuntu (${WAN_HOST}):"
    echo ""
    echo "--- CUT HERE ---"
    echo "# Install frps"
    echo "cd /tmp && tar xzf frp_${FRP_VERSION}_linux_amd64.tar.gz && cp frp_${FRP_VERSION}_linux_amd64/frps /usr/local/bin/"
    echo ""
    echo "# Configure frps"
    echo "sudo mkdir -p /etc/frp"
    echo "sudo python3 -c \"open('/etc/frp/frps-htkis.toml','w').write('bindPort = ${FRPS_PORT}\\n\\nauth.method = \\\"token\\\"\\nauth.token = \\\"${FRP_TOKEN}\\\"\\n\\nwebServer.addr = \\\"0.0.0.0\\\"\\nwebServer.port = ${FRPS_DASHBOARD_PORT}\\nwebServer.user = \\\"${FRPS_DASHBOARD_USER}\\\"\\nwebServer.password = \\\"${FRPS_DASHBOARD_PASSWORD}\\\"\\n')\""
    echo ""
    echo "# Create frps service"
    echo "sudo python3 -c \"open('/etc/systemd/system/frps-htkis.service','w').write('[Unit]\\nDescription=frps server for HTKIS\\nAfter=network.target\\n\\n[Service]\\nType=simple\\nExecStart=/usr/local/bin/frps -c /etc/frp/frps-htkis.toml\\nRestart=always\\nRestartSec=5\\n\\n[Install]\\nWantedBy=multi-user.target\\n')\""
    echo ""
    echo "# Start frps"
    echo "sudo systemctl daemon-reload && sudo systemctl enable frps-htkis && sudo systemctl start frps-htkis"
    echo ""
    echo "# Configure nginx"
    echo "sudo python3 -c \"open('/etc/nginx/sites-available/${DOMAIN}','w').write('server {\\n    listen 80;\\n    server_name ${DOMAIN};\\n    client_max_body_size 100M;\\n\\n    location = / {\\n        return 302 /guacamole/;\\n    }\\n\\n    location / {\\n        proxy_pass http://127.0.0.1:${FRP_REMOTE_PORT};\\n        proxy_set_header Host \$host;\\n        proxy_set_header X-Real-IP \$remote_addr;\\n        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;\\n        proxy_set_header X-Forwarded-Proto \$scheme;\\n        proxy_http_version 1.1;\\n        proxy_set_header Upgrade \$http_upgrade;\\n        proxy_set_header Connection \\\"upgrade\\\";\\n        proxy_read_timeout 3600s;\\n        proxy_send_timeout 3600s;\\n    }\\n}\\n')\""
    echo "sudo ln -sf /etc/nginx/sites-available/${DOMAIN} /etc/nginx/sites-enabled/${DOMAIN}"
    echo "sudo nginx -t && sudo systemctl reload nginx"
    echo "--- CUT HERE ---"
    echo ""
    echo "Also ensure Aliyun security group allows: ${FRPS_PORT}/TCP, ${FRP_REMOTE_PORT}/TCP"
    echo "And DNS A record: ${DOMAIN} -> ${WAN_HOST}"
}

# ============================================================
# Main
# ============================================================
if ! confirm "Start offline deployment?"; then
    echo "Aborted."
    exit 0
fi

load_docker_images
install_frp
deploy_lan
print_wan_commands

echo ""
echo "============================================"
echo " LAN deployment complete!"
echo " Configure WAN server using commands above."
echo "============================================"