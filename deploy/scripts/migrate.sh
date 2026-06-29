#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${SCRIPT_DIR}/../migrate.env"

if [[ ! -f "$ENV_FILE" ]]; then
    echo "ERROR: $ENV_FILE not found. Copy migrate.env.example to migrate.env and fill in values."
    exit 1
fi

source "$ENV_FILE"

DRY_RUN="${DRY_RUN:-false}"
BACKUP_DIR="${BACKUP_DIR:-/tmp/htkis-migration-backup}"
TIMESTAMP="$(date +%Y%m%d_%H%M%S)"

SSH_OLD="ssh -p ${OLD_SERVER_SSH_PORT:-22} ${OLD_SERVER_USER}@${OLD_SERVER_HOST}"
SSH_NEW="ssh -p ${NEW_SERVER_SSH_PORT:-22} ${NEW_SERVER_USER}@${NEW_SERVER_HOST}"
SCP_OLD="scp -P ${OLD_SERVER_SSH_PORT:-22}"
SCP_NEW="scp -P ${NEW_SERVER_SSH_PORT:-22}"

log() { echo "[$(date '+%H:%M:%S')] $*"; }
dry_log() { if [[ "$DRY_RUN" == "true" ]]; then log "[DRY-RUN] $*"; return 0; fi; }

run_old() {
    if [[ "$DRY_RUN" == "true" ]]; then
        log "[DRY-RUN] OLD: $*"
    else
        $SSH_OLD "$@"
    fi
}

run_new() {
    if [[ "$DRY_RUN" == "true" ]]; then
        log "[DRY-RUN] NEW: $*"
    else
        $SSH_NEW "$@"
    fi
}

pre_check() {
    log "=== Pre-flight checks ==="

    log "Checking SSH connectivity to old server ${OLD_SERVER_HOST}..."
    if ! $SSH_OLD "echo 'Old server reachable'" >/dev/null 2>&1; then
        log "ERROR: Cannot SSH to old server ${OLD_SERVER_HOST}"
        return 1
    fi
    log "OK: Old server reachable"

    log "Checking SSH connectivity to new server ${NEW_SERVER_HOST}..."
    if ! $SSH_NEW "echo 'New server reachable'" >/dev/null 2>&1; then
        log "ERROR: Cannot SSH to new server ${NEW_SERVER_HOST}"
        return 1
    fi
    log "OK: New server reachable"

    log "Checking Docker on new server..."
    run_new "docker --version && docker compose version"

    log "Checking frpc on new server..."
    run_new "/usr/local/bin/frpc --version"

    log "Checking Samba on new server..."
    run_new "/usr/sbin/smbd --version"

    log "Checking network from new server to frps..."
    run_new "nc -zv ${WAN_HOST} ${FRPS_PORT} 2>&1 || true"

    log "Pre-flight checks completed."
}

backup_old() {
    log "=== Backing up old server ${OLD_SERVER_HOST} ==="

    run_old "mkdir -p ${BACKUP_DIR}"

    log "Stopping services on old server..."
    if [[ "${STOP_OLD_SERVICES}" == "true" ]]; then
        run_old "sudo systemctl stop frpc || true"
        run_old "cd ~/Cloud/guacamole && sudo docker compose down || true"
        run_old "sudo systemctl stop smbd nmbd || true"
    fi

    log "Dumping PostgreSQL database..."
    run_old "cd ~/Cloud/guacamole && sudo docker exec guac_postgres pg_dump -U guacamole guacamole_db > ${BACKUP_DIR}/guacamole_db.sql"

    log "Backing up Guacamole config files..."
    run_old "cp ~/Cloud/guacamole/docker-compose.yml ${BACKUP_DIR}/docker-compose.yml"
    run_old "cp -r ~/Cloud/guacamole/guacamole-home ${BACKUP_DIR}/guacamole-home 2>/dev/null || true"
    run_old "cp -r ~/Cloud/guacamole/init ${BACKUP_DIR}/init 2>/dev/null || true"
    run_old "cp ~/Cloud/guacamole/patch-title.sh ${BACKUP_DIR}/patch-title.sh 2>/dev/null || true"

    log "Backing up frpc config..."
    run_old "sudo cp /etc/frp/frpc.toml ${BACKUP_DIR}/frpc.toml"
    run_old "sudo cp /etc/systemd/system/frpc.service ${BACKUP_DIR}/frpc.service 2>/dev/null || true"

    log "Backing up Samba config..."
    run_old "sudo cp /etc/samba/smb.conf ${BACKUP_DIR}/smb.conf"

    log "Packaging Samba shared files..."
    run_old "sudo tar czf ${BACKUP_DIR}/samba-shares.tar.gz -C /data/shares/public . 2>/dev/null || true"

    log "Computing checksums..."
    run_old "cd ${BACKUP_DIR} && md5sum guacamole_db.sql docker-compose.yml frpc.toml smb.conf > checksums.md5"

    log "Backup completed. Files at ${BACKUP_DIR} on old server."
}

transfer_data() {
    log "=== Transferring data to new server ${NEW_SERVER_HOST} ==="

    run_new "mkdir -p ${BACKUP_DIR}"

    log "Transferring backup files..."
    $SCP_OLD "${OLD_SERVER_USER}@${OLD_SERVER_HOST}:${BACKUP_DIR}/*" "${NEW_SERVER_USER}@${NEW_SERVER_HOST}:${BACKUP_DIR}/"

    log "Verifying checksums on new server..."
    run_new "cd ${BACKUP_DIR} && md5sum -c checksums.md5"

    log "Data transfer completed."
}

deploy_new() {
    log "=== Deploying services on new server ${NEW_SERVER_HOST} ==="

    log "Setting up Guacamole..."
    run_new "mkdir -p ~/Cloud/guacamole/init ~/Cloud/guacamole/guacamole-home"
    run_new "cp ${BACKUP_DIR}/docker-compose.yml ~/Cloud/guacamole/docker-compose.yml"
    run_new "cp ${BACKUP_DIR}/init/* ~/Cloud/guacamole/init/ 2>/dev/null || true"
    run_new "cp -r ${BACKUP_DIR}/guacamole-home/* ~/Cloud/guacamole/guacamole-home/ 2>/dev/null || true"
    run_new "cp ${BACKUP_DIR}/patch-title.sh ~/Cloud/guacamole/patch-title.sh 2>/dev/null || true"

    log "Updating docker-compose.yml for new server..."
    run_new "sed -i 's/GUACD_HOSTNAME: .*/GUACD_HOSTNAME: ${NEW_SERVER_HOST}/' ~/Cloud/guacamole/docker-compose.yml"
    run_new "sed -i 's/ports:/ports:\n      - \"127.0.0.1:8081:8080\"/' ~/Cloud/guacamole/docker-compose.yml 2>/dev/null || true"
    run_new "sed -i 's/\"8080:8080\"/\"127.0.0.1:8081:8080\"/' ~/Cloud/guacamole/docker-compose.yml"

    log "Starting Guacamole containers..."
    run_new "cd ~/Cloud/guacamole && sudo docker compose up -d"

    log "Waiting for PostgreSQL to be ready..."
    run_new "sleep 10"

    log "Restoring database..."
    run_new "cd ~/Cloud/guacamole && sudo docker exec -i guac_postgres psql -U guacamole -d guacamole_db < ${BACKUP_DIR}/guacamole_db.sql 2>/dev/null || true"
    run_new "cd ~/Cloud/guacamole && sudo docker restart guacamole"

    log "Running title patch..."
    run_new "cd ~/Cloud/guacamole && bash patch-title.sh 2>/dev/null || true"

    log "Setting up frpc..."
    run_new "sudo mkdir -p /etc/frp"
    run_new "sudo cp ${BACKUP_DIR}/frpc.toml /etc/frp/frpc.toml"
    run_new "sudo cp ${BACKUP_DIR}/frpc.service /etc/systemd/system/frpc.service 2>/dev/null || true"
    run_new "sudo systemctl daemon-reload"
    run_new "sudo systemctl enable frpc"
    run_new "sudo systemctl start frpc"

    log "Setting up Samba..."
    run_new "sudo cp ${BACKUP_DIR}/smb.conf /etc/samba/smb.conf"
    run_new "sudo tar xzf ${BACKUP_DIR}/samba-shares.tar.gz -C /data/shares/public/ 2>/dev/null || true"
    run_new "sudo systemctl enable smbd nmbd"
    run_new "sudo systemctl start smbd nmbd"

    log "Deployment completed."
}

verify() {
    log "=== Verifying migration ==="

    log "Checking Guacamole on new server..."
    HTTP_CODE=$(run_new "curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8081/guacamole/ 2>/dev/null || echo '000'")
    if [[ "$HTTP_CODE" == "200" || "$HTTP_CODE" == "302" ]]; then
        log "OK: Guacamole responding (HTTP $HTTP_CODE)"
    else
        log "WARN: Guacamole returned HTTP $HTTP_CODE"
    fi

    log "Checking frpc tunnel..."
    run_new "sudo systemctl status frpc --no-pager -l | head -5"

    log "Checking Samba..."
    run_new "sudo systemctl status smbd --no-pager -l | head -5"

    log "Checking public access via frps..."
    PUBLIC_CODE=$(curl -s -o /dev/null -w '%{http_code}' "http://${WAN_HOST}:${FRP_REMOTE_PORT}/guacamole/" 2>/dev/null || echo '000')
    if [[ "$PUBLIC_CODE" == "200" || "$PUBLIC_CODE" == "302" ]]; then
        log "OK: Public access working (HTTP $PUBLIC_CODE)"
    else
        log "WARN: Public access returned HTTP $PUBLIC_CODE (may need DNS update)"
    fi

    log "Verification completed."
}

retire_old() {
    log "=== Retiring old server ${OLD_SERVER_HOST} ==="

    log "Stopping and disabling all services on old server..."
    run_old "sudo systemctl stop frpc || true"
    run_old "sudo systemctl disable frpc || true"
    run_old "cd ~/Cloud/guacamole && sudo docker compose down || true"
    run_old "sudo systemctl stop smbd nmbd || true"
    run_old "sudo systemctl disable smbd nmbd || true"

    log "Old server services retired."
}

usage() {
    echo "Usage: $0 [--dry-run] [--pre-check] [--backup] [--transfer] [--deploy] [--verify] [--retire] [--all]"
    echo ""
    echo "  --dry-run     Show what would be done without executing"
    echo "  --pre-check   Run pre-flight checks only"
    echo "  --backup      Backup old server only"
    echo "  --transfer    Transfer data to new server only"
    echo "  --deploy      Deploy services on new server only"
    echo "  --verify      Verify migration only"
    echo "  --retire      Retire old server only"
    echo "  --all         Run all steps in sequence (default)"
    echo ""
    echo "Environment: Copy migrate.env.example to migrate.env and fill in values."
}

STEP="all"

while [[ $# -gt 0 ]]; do
    case "$1" in
        --dry-run) DRY_RUN="true"; shift ;;
        --pre-check) STEP="pre_check"; shift ;;
        --backup) STEP="backup"; shift ;;
        --transfer) STEP="transfer"; shift ;;
        --deploy) STEP="deploy"; shift ;;
        --verify) STEP="verify"; shift ;;
        --retire) STEP="retire"; shift ;;
        --all) STEP="all"; shift ;;
        -h|--help) usage; exit 0 ;;
        *) echo "Unknown option: $1"; usage; exit 1 ;;
    esac
done

log "HTKIS Cloud Office Migration Tool"
log "Old server: ${OLD_SERVER_HOST}"
log "New server: ${NEW_SERVER_HOST}"
log "Dry run: ${DRY_RUN}"
log "Step: ${STEP}"
log ""

case "$STEP" in
    pre_check) pre_check ;;
    backup) backup_old ;;
    transfer) transfer_data ;;
    deploy) deploy_new ;;
    verify) verify ;;
    retire) retire_old ;;
    all)
        pre_check
        backup_old
        transfer_data
        deploy_new
        verify
        log ""
        log "Migration completed. To retire the old server, run:"
        log "  $0 --retire"
        ;;
esac