#!/bin/bash
# Build offline deployment package for HTKIS Cloud Office
# This script downloads all dependencies and creates a self-contained tarball
# that can be deployed without internet access.

set -euo pipefail

VERSION="${1:-1.0.0}"
FRP_VERSION="0.65.0"
PACKAGE_NAME="htkis-cloud-office-${VERSION}-offline"

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
BUILD_DIR="${SCRIPT_DIR}/${PACKAGE_NAME}"

echo "============================================"
echo " Building offline package: ${PACKAGE_NAME}"
echo "============================================"

rm -rf "$BUILD_DIR"
mkdir -p "$BUILD_DIR/packages"
mkdir -p "$BUILD_DIR/docker-images"
mkdir -p "$BUILD_DIR/deploy/templates"
mkdir -p "$BUILD_DIR/deploy/init"

echo "[1/5] Downloading frp ${FRP_VERSION}..."
FRP_URL="https://ghfast.top/https://github.com/fatedier/frp/releases/download/v${FRP_VERSION}/frp_${FRP_VERSION}_linux_amd64.tar.gz"
wget -q --timeout=120 "$FRP_URL" -O "${BUILD_DIR}/packages/frp_${FRP_VERSION}_linux_amd64.tar.gz" || {
    echo "ERROR: Failed to download frp. Try direct URL:"
    echo "  https://github.com/fatedier/frp/releases/download/v${FRP_VERSION}/frp_${FRP_VERSION}_linux_amd64.tar.gz"
    exit 1
}
echo "  Done."

echo "[2/5] Pulling Docker images..."
docker pull guacamole/guacd:latest
docker pull guacamole/guacamole:latest
docker pull postgres:15

echo "[3/5] Saving Docker images to tar..."
docker save guacamole/guacd:latest -o "${BUILD_DIR}/docker-images/guacd.tar"
docker save guacamole/guacamole:latest -o "${BUILD_DIR}/docker-images/guacamole.tar"
docker save postgres:15 -o "${BUILD_DIR}/docker-images/postgres15.tar"
echo "  Done."

echo "[4/5] Copying deployment files..."
cp "${SCRIPT_DIR}/config.env.example" "${BUILD_DIR}/deploy/"
cp "${SCRIPT_DIR}/install.sh" "${BUILD_DIR}/deploy/"
cp "${SCRIPT_DIR}/install-offline.sh" "${BUILD_DIR}/deploy/" 2>/dev/null || true
cp "${SCRIPT_DIR}/templates/"* "${BUILD_DIR}/deploy/templates/"

# Copy Guacamole SQL init script (download from official repo if not present)
if [ -f "${SCRIPT_DIR}/templates/init/01_initdb.sql" ]; then
    cp "${SCRIPT_DIR}/templates/init/"* "${BUILD_DIR}/deploy/init/"
else
    echo "  Downloading Guacamole PostgreSQL init script..."
    wget -q --timeout=30 \
        "https://raw.githubusercontent.com/apache/guacamole-client/master/extensions/guacamole-auth-jdbc/modules/guacamole-auth-jdbc-postgresql/schema/01-initdb.sql" \
        -O "${BUILD_DIR}/deploy/init/01_initdb.sql" || {
        echo "  WARNING: Could not download init SQL. You'll need to get it from the Guacamole container."
    }
fi
echo "  Done."

echo "[5/5] Creating tarball..."
cd "$SCRIPT_DIR"
tar czf "${PACKAGE_NAME}.tar.gz" "${PACKAGE_NAME}"
rm -rf "$BUILD_DIR"

SIZE=$(du -h "${PACKAGE_NAME}.tar.gz" | cut -f1)
echo ""
echo "============================================"
echo " Offline package created!"
echo " File: ${PACKAGE_NAME}.tar.gz (${SIZE})"
echo "============================================"
echo ""
echo "To deploy on a target server:"
echo "  1. Copy ${PACKAGE_NAME}.tar.gz to the target"
echo "  2. tar xzf ${PACKAGE_NAME}.tar.gz"
echo "  3. cd ${PACKAGE_NAME}/deploy"
echo "  4. cp config.env.example config.env && vi config.env"
echo "  5. sudo bash install-offline.sh"