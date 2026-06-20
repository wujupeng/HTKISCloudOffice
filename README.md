# HTKIS Cloud Office

企业轻终端云办公平台 — 将安卓平板变成超轻终端，所有业务在云端运行。

## 架构概览

```
┌─────────────┐     ┌──────────────────────────────────────────────┐
│  安卓平板    │     │           Debian 13 服务器 (192.168.2.102)   │
│  Chrome浏览器│────▶│  ┌─────────┐  ┌───────────┐  ┌───────────┐ │
│             │     │  │ Guacamole│  │   guacd   │  │  Samba    │ │
│  Guacamole  │     │  │  :8080   │─▶│  :4822    │  │  :445     │ │
│  Web UI     │     │  └─────────┘  └─────┬─────┘  └─────┬─────┘ │
└─────────────┘     └─────────────────────┼───────────────┼───────┘
                                           │ RDP           │ SMB
                                           ▼               ▼
                                    ┌──────────────────────────────┐
                                    │  Windows Server (192.168.2.88)│
                                    │  RDSH + WPS Office            │
                                    │  域: cii  用户: 10000         │
                                    └──────────────────────────────┘
```

## 核心链路 (MVP)

> 安卓平板 Chrome → Guacamole 登录 → RDP 连接 → Win Server 桌面 → WPS Office → 打开/编辑/保存文件

## 快速部署

### 前置条件

| 组件 | 要求 |
|------|------|
| 服务器 | Debian 13 (Trixie), IP: 192.168.2.102 |
| Windows Server | 2019/2022, 已配置 RDSH, IP: 192.168.2.88 |
| Docker | 已安装并运行 |
| 安卓平板 | Chrome 浏览器 |

### 1. 部署 Guacamole

```bash
# 上传部署文件到服务器
scp -r deploy/guacamole/ debian@192.168.2.102:/home/debian/Cloud/guacamole/

# SSH 到服务器
ssh debian@192.168.2.102

# 启动 Guacamole
cd /home/debian/Cloud/guacamole
docker compose up -d

# 等待容器启动
sleep 10

# 修改标题为 Htkis-Cloud
bash patch-title.sh
```

### 2. 配置连接参数

编辑 `user-mapping.xml`，修改以下参数：

```xml
<param name="hostname">你的Windows服务器IP</param>
<param name="username">域\用户名</param>
<param name="password">用户密码</param>
```

### 3. 配置 Samba 文件共享

```bash
# 创建共享目录
sudo mkdir -p /data/shares/public
sudo chmod 2777 /data/shares/public

# 安装 Samba
sudo apt-get install -y samba

# 配置 /etc/samba/smb.conf
# 参见 deploy/samba/smb.conf.template

# 设置 Samba 用户密码
sudo smbpasswd -a debian

# 重启 Samba
sudo systemctl restart smbd nmbd
```

### 4. Windows Server 配置

1. **启用远程桌面**：系统属性 → 远程 → 允许远程连接
2. **禁用 NLA**（Guacamole 不支持）：
   - 组策略：`gpedit.msc` → 计算机配置 → 管理模板 → Windows 组件 → 远程桌面服务 → 远程桌面会话主机 → 安全
   - 禁用"要求使用网络级别的身份验证"
   - 禁用"要求安全 RPC 连接"
3. **安装 WPS Office**：通过远程桌面在服务器上安装
4. **映射共享驱动器**（可选）：
   ```
   net use Z: \\192.168.2.102\public /user:debian 9090 /persistent:yes
   ```

### 5. 验证

1. 安卓平板 Chrome 打开 `http://192.168.2.102:8080/guacamole`
2. 用户名: `tablet`, 密码: `Tablet@2026`
3. 点击 **Htkis-Cloud** 连接
4. 验证远程桌面、文件传输功能

## 文件结构

```
deploy/
├── guacamole/
│   ├── docker-compose.yml      # Docker Compose 配置
│   ├── user-mapping.xml        # 用户和连接配置
│   ├── patch-title.sh          # 标题补丁脚本
│   └── translations/           # 翻译覆盖
├── samba/
│   └── smb.conf.template       # Samba 配置模板
├── nginx/
│   └── portal.htkis.local.conf # Nginx 反向代理配置
├── systemd/
│   └── htkis-portal.service    # systemd 服务文件
└── appsettings.production.json # .NET 应用生产配置
src/                            # .NET 8 源代码 (Alpha 阶段)
```

## 用户账户

| 用途 | 用户名 | 密码 |
|------|--------|------|
| Guacamole 平板登录 | tablet | Tablet@2026 |
| Guacamole 管理员 | guacadmin | guacadmin |
| RDP 远程桌面 | cii\10000 | 123456 |
| Samba 文件共享 | debian | 9090 |

## 关键配置说明

### Guacamole docker-compose.yml

- **guacd**: 使用 `network_mode: host`，确保能访问 Windows Server
- **guacamole**: 通过 `192.168.2.102:4822` 连接 guacd
- **SFTP**: 启用文件传输，连接 Debian SSH，根目录 `/data/shares/public`

### user-mapping.xml

- 使用文件认证（非 PostgreSQL 后端），确保连接参数正确传递给 guacd
- `security-mode: rdp` — 使用 RDP 安全层（非 TLS/NLA）
- `ignore-cert: true` — 忽略自签名证书
- `enable-sftp: true` — 启用 Guacamole 文件传输

### 已知问题

1. **PostgreSQL 后端不传递参数**：Guacamole PostgreSQL 扩展不把连接参数传给 guacd，改用 user-mapping.xml 文件认证
2. **NLA 不兼容**：Guacamole 不支持 NLA，必须在 Windows 上禁用
3. **标题补丁**：容器重启后需运行 `patch-title.sh` 恢复标题
4. **SecurityLayer**：Windows RDP SecurityLayer 必须设为 0（纯 RDP），否则 TLS 证书验证失败

## 许可证

内部项目，未公开授权。