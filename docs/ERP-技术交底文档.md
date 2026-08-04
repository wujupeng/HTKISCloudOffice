# ERP 云办公平台 — 技术交底文档

| 项目 | 内容 |
|------|------|
| 文档版本 | v3.0 |
| 编写日期 | 2026-08-04 |
| 状态 | 已发布 |
| 访问地址 | https://erp.oascii.com:8443 |

---

## 1. 系统概述

ERP 云办公平台基于 Apache Guacamole 实现，将 ERP Windows Server 的 RDP 桌面通过浏览器远程交付给用户。用户通过邮箱验证码二次认证后，在 Chrome 浏览器中即可操作 ERP 系统，无需安装任何客户端。

核心链路：

**安卓平板 Chrome → erp.oascii.com:8443 → 宝山路由器(NAT) → <VIP_IP>:8443(VIP) → Nginx SSL → 邮箱验证(auth_request) → Guacamole Header认证 → guacd → RDP → <WIN_SERVER_IP>:3389**

> VIP 192.168.x.200 由 Keepalived 管理，自动在主库(<MASTER_IP>)和备机(<BACKUP_IP>)之间切换。

---

## 2. 网络拓扑

```
                        ┌─────────────────┐
                        │   安卓平板/PC    │
                        │  Chrome 浏览器   │
                        └────────┬────────┘
                                 │ HTTPS :8443
                                 ▼
                    ┌────────────────────────┐
                    │   宝山工厂路由器        │
                    │   x.x.x.x       │
                    │   端口转发:            │
                     │   8443→<VIP_IP>:8443│
                    └────────────┬───────────┘
                                 │ HTTPS :8443
                                 ▼
                    ┌────────────────────────┐
                     │   VIP <VIP_IP>     │
                    │   (Keepalived 管理)    │
                    └────┬──────────────┬───┘
                         │              │
              ┌──────────▼──┐    ┌─────▼──────────┐
               │  主库 MASTER │    │  备机 BACKUP    │
               │  <MASTER_IP>│    │  <BACKUP_IP>    │
              │  priority=100│    │  priority=90    │
              └──────┬───────┘    └──────┬──────────┘
                     │                   │
        ┌────────────▼───────────────────▼────────────┐
        │         Debian 服务器 (主/备同构部署)         │
        │                                              │
        │  ┌──────────────────────────────┐            │
        │  │  htkis-nginx (Docker)        │            │
        │  │  :8080 HTTP (HTKIS)          │            │
        │  │  :8443 HTTPS SSL (ERP)       │            │
        │  │  auth_request → :5003/auth   │            │
        │  │  proxy_pass → :8082/guacamole│            │
        │  └──────┬───────────┬───────────┘            │
        │         │           │                        │
        │  ┌──────▼──────┐ ┌──▼────────────┐          │
        │  │ erp-email-  │ │ erp-guacamole  │          │
        │  │ auth :5003  │ │ :8082          │          │
        │  │ (FastAPI)   │ │ (Tomcat)       │          │
        │  └──────┬──────┘ └───────┬────────┘          │
        │         │                │                    │
        │  ┌──────▼──────┐ ┌───────▼────────┐          │
        │  │ erp-guac-   │ │ guacd :4822    │          │
        │  │ postgres    │ │ (host网络,     │          │
        │  │ :5435       │ │  HTKIS/ERP共用)│          │
        │  │  ┌────────┐ │ └───────┬────────┘          │
        │  │  │流复制  │ │         │                    │
        │  │  │主→备   │ │         │                    │
        │  │  └────────┘ │         │                    │
        │  └─────────────┘         │                    │
        └──────────────────────────┼────────────────────┘
                                   │ RDP :3389
                                   ▼
                        ┌─────────────────────┐
                         │  ERP Windows Server  │
                         │  <WIN_SERVER_IP>      │
                        └─────────────────────┘
```

---

## 3. 部署环境

### 3.1 服务器清单

| 角色 | IP | 操作系统 | 用途 |
|------|-----|---------|------|
| Debian 主服务器 (MASTER) | <MASTER_IP> | Debian 13 | Nginx + Guacamole + 邮箱验证 + guacd |
| Debian 备服务器 (BACKUP) | <BACKUP_IP> | Debian 13 | 同主服务器，热备 |
| VIP (Keepalived) | <VIP_IP> | — | 浮动 IP，自动跟随 MASTER |
| 宝山工厂路由器 | x.x.x.x | — | 公网 NAT 端口转发 |
| ERP Windows Server | <WIN_SERVER_IP> | Windows Server | RDP 目标机，运行 ERP 软件 |

### 3.2 Docker 容器（主/备机同构部署）

| 容器名 | 镜像 | 端口 | 网络模式 | 用途 |
|--------|------|------|---------|------|
| erp-guacamole | guacamole/guacamole:latest | 127.0.0.1:8082→8080 | bridge | Guacamole Web 应用 |
| erp-guac-postgres | postgres:15 | 5435→5432 | bridge | Guacamole 数据库（主库读写，备库流复制只读） |
| erp-email-auth | 自建 (python:3.12-slim) | 127.0.0.1:5003 | host | 邮箱验证码认证服务 |
| htkis-nginx | nginx:alpine | 8080, 8443 | host | 反向代理 + SSL 终结 |
| guacd | guacamole/guacd:latest | 4822 | host | Guacamole 远程桌面代理（HTKIS/ERP 共用） |

### 3.3 端口映射

| 外部端口 | 内部端口 | 协议 | 用途 |
|---------|---------|------|------|
| 8443 (宝山路由器) | <VIP_IP>:8443 (VIP) | HTTPS | ERP 公网入口 |
| 8443 (Nginx) | — | HTTPS/SSL | ERP SSL 终结 |
| 8080 (Nginx) | — | HTTP | HTKIS 入口 + ERP HTTP→HTTPS 重定向 |
| 8082 | — | HTTP | ERP Guacamole 内部 |
| 5003 | — | HTTP | ERP 邮箱验证服务 |
| 5435 | — | PostgreSQL | ERP Guacamole 数据库（主库接受备机流复制连接） |
| 4822 | — | guacd | 远程桌面代理（共用） |

---

## 4. 双机热备架构

### 4.1 Keepalived 配置

| 项目 | 主库 <MASTER_IP> | 备机 <BACKUP_IP> |
|------|------------------|-------------------|
| 角色 | MASTER | BACKUP |
| priority | 100 | 90 |
| 网络接口 | eth0 | enp3s0 |
| virtual_router_id | 51 | 51 |
| VIP | <VIP_IP>/24 | <VIP_IP>/24 |
| 认证密码 | **** (PASS) | **** (PASS) |

### 4.2 健康检查

Keepalived 通过 `/etc/keepalived/check_container.sh` 检测容器状态：
- `check_nginx`：检测 htkis-nginx 容器是否运行（interval=5s, fall=3, rise=2）
- `check_erp_guac`：检测 erp-guacamole 容器是否运行（interval=5s, fall=3, rise=2）

任一检查失败 3 次后，MASTER 降级，VIP 迁移到备机。

### 4.3 PostgreSQL 流复制

| 项目 | 内容 |
|------|------|
| 复制用户 | replicator |
| 复制模式 | 异步流复制 (async) |
| wal_level | replica |
| max_wal_senders | 3 |
| wal_keep_size | 64MB |
| hot_standby | on |
| pg_hba.conf | host replication replicator <BACKUP_IP>/32 md5 |

### 4.4 主备切换流程

1. 主库故障（Nginx/Guacamole 容器停止或服务器宕机）
2. Keepalived 健康检查失败 3 次（约 15 秒）
3. MASTER 降级，VIP 迁移到备机
4. 备机接管所有 ERP 流量
5. 主库恢复后，VIP 自动回切到主库（priority 更高）

---

## 5. 认证流程

```
用户访问 https://erp.oascii.com:8443
         │
         ▼
    Nginx (8443 SSL)
         │
         ├─ /api/erp-auth/* → erp-email-auth:5003
         │
         ├─ /guacamole/* → auth_request /erp-auth
         │       │
         │       ▼
         │   erp-email-auth /auth
         │   检查 erp_auth_token Cookie
         │       │
         │       ├─ 401 → 重定向到 /api/erp-auth/login-page
         │       └─ 200 → 返回 X-Auth-User header
         │                │
         │                ▼
         │   Nginx 设置 Remote-User header
         │   proxy_pass → erp-guacamole:8082
         │                │
         │                ▼
         │   Guacamole Header 认证扩展
         │   根据 Remote-User 自动登录
         │                │
         │                ▼
         │   guacd:4822 → RDP → <WIN_SERVER_IP>:3389
         │
         └─ 断开连接检测 (sub_filter JS 注入)
             检测到"您的连接已断开"→ 自动登出 → 返回登录页
```

### 5.1 登录步骤

1. 用户访问 `https://erp.oascii.com:8443`
2. 显示 ERP 登录页，输入 Guacamole 用户名和密码
3. 首次登录需绑定邮箱，输入邮箱后发送验证码
4. 输入验证码验证通过，设置 `erp_auth_token` Cookie
5. 自动跳转到 Guacamole 远程桌面

### 5.2 动态用户授权

用户首次验证通过时，`erp-email-auth` 服务自动调用 Guacamole REST API：
- 如果用户不存在，自动创建
- 自动授权 ERP Server 连接（connection ID=1）的 READ 权限

---

## 6. SSL 证书

| 项目 | 内容 |
|------|------|
| 域名 | erp.oascii.com |
| 证书颁发机构 | Let's Encrypt (YE1) |
| 验证方式 | DNS-01 (TXT 记录) |
| 证书路径 | /etc/letsencrypt/live/erp.oascii.com/fullchain.pem |
| 私钥路径 | /etc/letsencrypt/live/erp.oascii.com/privkey.pem |
| 有效期 | 2026-07-08 ~ 2026-10-06 |
| 自动续期 | certbot timer 已启用 |

---

## 7. 数据库

| 项目 | 内容 |
|------|------|
| 数据库 | erp_guacamole_db |
| 用户 | guacamole |
| 端口 | 5435 (容器内 5432) |
| 数据卷 | erp_pgdata |
| 流复制 | 主库(<MASTER_IP>) → 备库(<BACKUP_IP>)，异步模式 |

### 邮箱验证服务表

| 表名 | 用途 |
|------|------|
| user_email_binding | 用户-邮箱绑定关系 |
| email_verification_code | 验证码记录（状态：PENDING/VERIFIED/EXPIRED/INVALIDATED） |

---

## 8. 关键配置文件

| 文件 | 路径 (主/备机相同) | 说明 |
|------|---------------------|------|
| Nginx 配置 | /home/debian/Cloud/services/nginx/nginx.conf | 双 server block，基于域名路由 |
| ERP Guacamole docker-compose | /home/debian/Cloud/erp-guacamole/docker-compose.yml | Guacamole + PostgreSQL |
| ERP Guacamole properties | /home/debian/Cloud/erp-guacamole/guacamole-home/guacamole.properties | http-auth-header: Remote-User |
| ERP 邮箱验证服务 | /home/debian/Cloud/services/erp-email-auth/ | FastAPI 应用 |
| Services docker-compose | /home/debian/Cloud/services/docker-compose.yml | 所有服务编排 |
| Services .env | /home/debian/Cloud/services/.env | 环境变量 |
| SSL 证书 | /etc/letsencrypt/live/erp.oascii.com/ | Let's Encrypt 证书 |
| Keepalived 配置 | /etc/keepalived/keepalived.conf | VRRP 实例 + 健康检查 |
| Keepalived 健康检查脚本 | /etc/keepalived/check_container.sh | Docker 容器状态检测 |

---

## 9. SMTP 配置

| 项目 | 内容 |
|------|------|
| 主机 | smtp.exmail.qq.com |
| 端口 | 465 (TLS) |
| 账号 | admin@cii.sh.cn |
| 发件人 | HTKIS Cloud Office <admin@cii.sh.cn> |
