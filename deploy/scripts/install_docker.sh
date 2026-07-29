#!/bin/bash
set -e
echo "$SUDO_PASSWORD" | sudo -S sed -i '/cdrom/d' /etc/apt/sources.list /etc/apt/sources.list.d/*.list 2>/dev/null || true
echo "$SUDO_PASSWORD" | sudo -S apt-get update -qq
echo "$SUDO_PASSWORD" | sudo -S apt-get install -y -qq docker.io docker-compose-v2 2>/dev/null || {
  echo "$SUDO_PASSWORD" | sudo -S apt-get install -y -qq docker docker-compose 2>/dev/null || {
    curl -fsSL https://mirrors.aliyun.com/docker-ce/linux/debian/gpg | echo "$SUDO_PASSWORD" | sudo -S tee /etc/apt/trusted.gpg.d/docker.asc > /dev/null
    echo "deb [arch=amd64] https://mirrors.aliyun.com/docker-ce/linux/debian trixie stable" | echo "$SUDO_PASSWORD" | sudo -S tee /etc/apt/sources.list.d/docker.list > /dev/null
    echo "$SUDO_PASSWORD" | sudo -S apt-get update -qq
    echo "$SUDO_PASSWORD" | sudo -S apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-compose-plugin
  }
}
echo "$SUDO_PASSWORD" | sudo -S usermod -aG docker debian
echo "Docker installation complete"
