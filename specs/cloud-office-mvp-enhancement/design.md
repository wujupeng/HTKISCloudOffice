# HTKIS Cloud Office MVP 增强 — 实现方案设计文档

| 字段 | 值 |
|------|-----|
| 文档版本 | v1.0 |
| 创建日期 | 2026-06-29 |
| 关联规格 | spec.md (cloud-office-mvp-enhancement) |
| 文档状态 | 初稿 |

---

# 1. 实现模型

## 1.1 上下文视图

迁移后的系统上下文视图如下，新增邮箱验证服务和截屏管理服务两个独立组件，与现有 Guacamole + frp 架构松耦合集成：

```
┌─────────────────────────────────────────────────────────────────────┐
│                          公网 (Internet)                            │
│                                                                     │
│   ┌──────────────┐         ┌──────────────────────────┐            │
│   │  安卓平板     │────────▶│  ****.htkis.com      │            │
│   │  Chrome浏览器 │ HTTP/S  │  DNS → x.x.x.214     │            │
│   └──────────────┘         └──────────┬───────────────┘            │
│                                       │                             │
└───────────────────────────────────────┼─────────────────────────────┘
                                        │ 80/TCP + 443/TCP
                                        ▼
┌─────────────────────────────────────────────────────────────────────┐
│         Ubuntu 服务器 (x.x.x.214) — 阿里云香港                     │
│                                                                     │
│   ┌─────────────────────────────────────────────────────────┐      │
│   │  nginx                                                   │      │
│   │  ├─ ****.htkis.com → :443 ssl → proxy_pass :18080      │      │
│   │  └─ ****.htkis.com → :80 → proxy_pass :18080           │      │
│   └────────────────────────┬────────────────────────────────┘      │
│                            │ 18080/TCP (frp 隧道)                  │
│                            ▼                                       │
│   ┌─────────────────────────────────────────────────────────┐      │
│   │  frps-htkis (0.65.0)  :7001 / :7501                    │      │
│   └─────────────────────────────────────────────────────────┘      │
└─────────────────────────────────────────────────────────────────────┘
            │
            │ frp 隧道 (x.x.x.214:7001 ←→ 192.168.2.3:frpc)
            │
┌───────────┼─────────────────────────────────────────────────────────┐
│           ▼        Debian 服务器 (192.168.2.3) — 上海宝山 [新]      │
│                                                                     │
│   ┌─────────────────────────────────────────────────────────┐      │
│   │  frpc (0.65.0)                                          │      │
│   │  连接: x.x.x.214:7001 (直连)                           │      │
│   │  代理: htkis-guacamole, localIP=127.0.0.1:8080          │      │
│   └────────────────────────┬────────────────────────────────┘      │
│                            │ 127.0.0.1:8080                        │
│                            ▼                                       │
│   ┌─────────────────────────────────────────────────────────┐      │
│   │  Nginx (本地反代) [新增]                                 │      │
│   │  :8080 → auth_request → :5001/auth                       │      │
│   │         → proxy_pass → :8081 (Guacamole)                │      │
│   │         → /api/email-auth/* → :5001 (邮箱验证服务)       │      │
│   │         → /screenshot/* → :5002 (截屏管理服务)           │      │
│   └──────────┬──────────────────────┬───────────────────────┘      │
│              │                      │                               │
│   ┌──────────▼──────────┐  ┌───────▼────────────────────────┐     │
│   │  Docker: Guacamole   │  │  Docker: 邮箱验证服务 [新增]    │     │
│   │  ├─ guacd (host)     │  │  FastAPI + SMTP + JWT          │     │
│   │  ├─ guac_postgres    │  │  :5001                         │     │
│   │  └─ guacamole :8081  │  └────────────────────────────────┘     │
│   └──────────┬──────────┘                                          │
│              │ RDP                                                  │
│              ▼                                                      │
│   ┌─────────────────────────────────────────────────────────┐      │
│   │  Docker: 截屏管理服务 [新增]                              │      │
│   │  FastAPI + 静态文件服务                                   │      │
│   │  :5002                                                    │      │
│   └─────────────────────────────────────────────────────────┘      │
│                                                                     │
│   其他服务:                                                         │
│   ├─ Samba: /data/shares/public (权限2777)                        │
│   ├─ 截屏存储: /data/screenshots/{用户}/{日期}/                   │
│   └─ PostgreSQL(应用层): :5433 (邮箱验证/截屏元数据)              │
└─────────────────────────────────────────────────────────────────────┘
            │
            │ RDP 3389/TCP
            ▼
┌─────────────────────────────────────────────────────────────────────┐
│              Windows Server (192.168.2.88) — 上海宝山                │
│                                                                     │
│   RDP 服务: 3389/TCP (NLA模式)                                     │
│   截屏脚本 [新增]: PowerShell 计划任务 (每10秒)                     │
│   截屏输出: \\192.168.2.3\screenshots\{用户}\{日期}\               │
└─────────────────────────────────────────────────────────────────────┘
```

## 1.2 服务/组件总体架构

### 1.2.1 架构总览

系统由以下核心组件构成，所有新增组件以 Docker 容器独立部署，与 Guacamole 松耦合：

| 组件 | 类型 | 端口 | 技术栈 | 状态 |
|------|------|------|--------|------|
| Nginx (本地反代) | Docker | 8080 | nginx:alpine | 新增 |
| Guacamole (guacd + postgres + web) | Docker | 8081/4822/5432 | guacamole/guacd + guacamole/guacamole + postgres:15 | 迁移 |
| frpc | systemd | - | frp 0.65.0 | 迁移 |
| Samba | systemd | 445 | samba | 迁移 |
| 邮箱验证服务 | Docker | 5001 | FastAPI + SQLAlchemy + aiosmtplib | 新增 |
| 截屏管理服务 | Docker | 5002 | FastAPI + Jinja2 | 新增 |
| 应用层数据库 | Docker | 5433 | postgres:15 | 新增 |
| 截屏脚本 | 计划任务 | - | PowerShell | 新增 (Win Server) |

### 1.2.2 组件交互架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        用户浏览器                                 │
│                                                                   │
│  ① GET /guacamole/ ──→ Nginx :8080                              │
│     ├─ auth_request → 邮箱验证服务 :5001/auth                    │
│     │   ├─ 无 cookie / cookie 过期 → 302 → /guacamole/ 登录页   │
│     │   └─ cookie 有效 → 200 → proxy_pass → Guacamole :8081    │
│     └─ Guacamole 原生登录表单提交                                │
│                                                                   │
│  ② POST /api/email-auth/login (邮箱验证服务处理)                 │
│     ├─ 验证 Guacamole 用户名+密码 (REST API)                    │
│     ├─ 检查免验证期 (JWT cookie)                                 │
│     ├─ 发送/验证邮箱验证码                                        │
│     └─ 设置 JWT cookie → 重定向到 /guacamole/                   │
│                                                                   │
│  ③ GET /screenshot/ → 截屏管理服务 :5002                        │
│     └─ 管理员查看截屏记录 (需管理员 JWT 认证)                    │
└─────────────────────────────────────────────────────────────────┘
```

### 1.2.3 邮箱验证登录 — 方案选型

| 方案 | 描述 | 优点 | 缺点 | 结论 |
|------|------|------|------|------|
| A: Nginx auth_request + 独立验证服务 | Nginx 通过 `auth_request` 子请求调用验证服务，验证通过才放行到 Guacamole | 不修改 Guacamole 源码；完全解耦；可独立升级 | 需要本地 Nginx 反代层；JWT cookie 管理 | **✅ 采用** |
| B: Guacamole 扩展 (guacamole-auth-email) | 开发 Guacamole 认证扩展 JAR | 原生集成 | 需深入理解 Guacamole 扩展 API；升级兼容性风险；开发复杂度高 | ❌ |
| C: SSO 集成 (OAuth2/OIDC) | Guacamole OIDC 扩展 + 独立 IdP | 标准化 | 架构过重；需部署 Keycloak 等 IdP；MVP 阶段过度设计 | ❌ |

**方案 A 详细设计**：

在 Debian 服务器本地部署 Nginx 反代，监听 8080 端口（替代 Guacamole 直接暴露 8080），Guacamole 改为监听 8081（仅本地可访问）。Nginx 对所有 `/guacamole/` 请求执行 `auth_request` 子请求到邮箱验证服务，验证通过后放行。

```
用户 → frpc:18080 → Nginx:8080 ──auth_request──→ 邮箱验证服务:5001/auth
                        │                              │
                        │         ← 200 OK ←──────────┘ (JWT 有效)
                        │         ← 401 ←─────────────┘ (JWT 无效/过期)
                        │
                        ├──→ /guacamole/* → proxy_pass → Guacamole:8081
                        ├──→ /api/email-auth/* → proxy_pass → 邮箱验证服务:5001
                        └──→ /screenshot/* → proxy_pass → 截屏管理服务:5002
```

### 1.2.4 截屏记录 — 方案选型

| 方案 | 描述 | 优点 | 缺点 | 结论 |
|------|------|------|------|------|
| A: Guacamole WebSocket 录屏 | guacamole-recording 扩展 | 服务端采集 | 需修改 Guacamole；录制的是 Guacamole 协议流，非标准图片 | ❌ |
| B: Windows Server 端 PowerShell 截屏 | Win Server 计划任务 + PowerShell 脚本 | 简单可靠；不依赖 Guacamole；截取真实桌面 | 需 Win Server 配置；需 Samba 传输 | **✅ 采用** |
| C: guacd 层面截屏 | 修改 guacd 或使用 guacamole-session-recording | 协议层采集 | 需修改 guacd 源码；复杂度高 | ❌ |
| D: 独立截屏服务 RDP 协议获取 | 独立 RDP 客户端定时截屏 | 不依赖 Guacamole | 需额外 RDP 连接占用 CAL 许可；实现复杂 | ❌ |

**方案 B 详细设计**：

在 Windows Server 上部署 PowerShell 截屏脚本，通过 Windows 计划任务每 10 秒执行一次。截屏文件通过 Samba 共享直接写入 Debian 服务器的 `/data/screenshots/` 目录。截屏管理服务读取该目录提供 Web 查看界面。

```
Windows Server (192.168.2.88)
  │
  │ PowerShell 截屏脚本 (每10秒, 计划任务)
  │ 输出: \\192.168.2.3\screenshots\{username}\{date}\{time}.png
  │
  ▼
Debian Server (192.168.2.3)
  /data/screenshots/  (Samba 共享)
  │
  │ 截屏管理服务 :5002 读取文件 + 元数据入库
  │
  ▼
管理员浏览器 → /screenshot/ 查看界面
```

## 1.3 实现设计文档

### 1.3.1 服务器迁移设计

#### 迁移架构

```
迁移前:                              迁移后:
192.168.2.102                        192.168.2.3
├─ Docker: Guacamole (3容器)         ├─ Docker: Guacamole (3容器, 配置更新)
├─ frpc → x.x.x.214:7001            ├─ frpc → x.x.x.214:7001 (配置不变)
├─ Samba /data/shares/public         ├─ Samba /data/shares/public (数据迁移)
├─ autossh-frps (可选)               ├─ [新增] Nginx 反代 :8080
                                     ├─ [新增] 邮箱验证服务 :5001
                                     ├─ [新增] 截屏管理服务 :5002
                                     ├─ [新增] 应用层数据库 :5433
                                     └─ [新增] 截屏 Samba 共享 /data/screenshots
```

#### 迁移步骤

**Phase 0: 准备阶段**

1. 在 192.168.2.3 上安装基础环境：
   - Docker Engine 26.x+、Docker Compose v2.26+
   - frp 0.65.0 (frpc)
   - Samba
   - Nginx (Docker 容器)
   - 创建目录结构：`~/Cloud/guacamole/`、`/data/shares/public`、`/data/screenshots`

2. 验证 192.168.2.3 到 x.x.x.214:7001 网络连通性：
   ```bash
   nc -zv x.x.x.214 7001
   ```

3. 验证 192.168.2.3 到 192.168.2.88:3389 RDP 连通性：
   ```bash
   nc -zv 192.168.2.88 3389
   ```

**Phase 1: 旧服务器数据备份**

1. 停止 192.168.2.102 上的所有服务：
   ```bash
   # 停止 frpc
   sudo systemctl stop frpc
   # 停止 Guacamole
   cd ~/guacamole && docker compose down
   # 停止 Samba
   sudo systemctl stop smbd nmbd
   ```

2. 导出 PostgreSQL 数据：
   ```bash
   # 启动仅 postgres 容器
   cd ~/guacamole && docker compose up -d postgres
   sleep 5
   # 导出全量数据
   docker exec guac_postgres pg_dump -U guacamole guacamole_db > /tmp/guacamole_db_backup.sql
   # 验证导出文件
   wc -l /tmp/guacamole_db_backup.sql
   md5sum /tmp/guacamole_db_backup.sql
   ```

3. 打包 Samba 共享文件：
   ```bash
   tar czf /tmp/samba_public_backup.tar.gz -C /data/shares public/
   md5sum /tmp/samba_public_backup.tar.gz
   ```

4. 备份配置文件：
   ```bash
   cp /etc/frp/frpc.toml /tmp/frpc.toml.backup
   cp ~/guacamole/docker-compose.yml /tmp/docker-compose.yml.backup
   cp -r ~/guacamole/guacamole-home /tmp/guacamole-home.backup
   ```

**Phase 2: 新服务器部署**

1. 传输数据到新服务器：
   ```bash
   # 从 192.168.2.102 传输到 192.168.2.3
   scp /tmp/guacamole_db_backup.sql debian@192.168.2.3:/tmp/
   scp /tmp/samba_public_backup.tar.gz debian@192.168.2.3:/tmp/
   scp /tmp/frpc.toml.backup debian@192.168.2.3:/tmp/frpc.toml
   scp /tmp/docker-compose.yml.backup debian@192.168.2.3:/tmp/docker-compose.yml
   scp -r /tmp/guacamole-home.backup debian@192.168.2.3:/tmp/guacamole-home
   ```

2. 部署 Guacamole（docker-compose.yml 需更新 GUACD_HOSTNAME 为 192.168.2.3）：
   ```bash
   mkdir -p ~/Cloud/guacamole/init ~/Cloud/guacamole/guacamole-home
   # 复制 docker-compose.yml 并更新配置
   cp /tmp/docker-compose.yml ~/Cloud/guacamole/docker-compose.yml
   # 更新 GUACD_HOSTNAME 为新服务器 IP
   sed -i 's/GUACD_HOSTNAME: .*/GUACD_HOSTNAME: 192.168.2.3/' ~/Cloud/guacamole/docker-compose.yml
   # 复制 guacamole-home 配置
   cp -r /tmp/guacamole-home/* ~/Cloud/guacamole/guacamole-home/
   # 复制初始化 SQL
   cp ~/Cloud/guacamole/init/01_initdb.sql ~/Cloud/guacamole/init/ 2>/dev/null || true
   # 启动容器
   cd ~/Cloud/guacamole && docker compose up -d
   sleep 30
   ```

3. 导入 PostgreSQL 数据：
   ```bash
   # 等待 postgres 就绪
   docker exec guac_postgres pg_isready -U guacamole
   # 导入数据（先删除初始化创建的默认数据）
   docker exec -i guac_postgres psql -U guacamole -d guacamole_db -c "DROP SCHEMA public CASCADE; CREATE SCHEMA public;"
   docker exec -i guac_postgres psql -U guacamole -d guacamole_db < /tmp/guacamole_db_backup.sql
   # 重启 Guacamole 使数据生效
   cd ~/Cloud/guacamole && docker compose restart guacamole
   ```

4. 部署 frpc：
   ```bash
   sudo mkdir -p /etc/frp
   # frpc.toml 配置无需修改（serverAddr、auth.token 不变）
   # localIP=127.0.0.1 仍指向本地 Nginx :8080
   sudo cp /tmp/frpc.toml /etc/frp/frpc.toml
   # 创建 systemd 服务
   sudo cp frpc.service /etc/systemd/system/frpc.service
   sudo systemctl daemon-reload
   sudo systemctl enable frpc
   sudo systemctl start frpc
   ```

5. 部署 Samba 并恢复数据：
   ```bash
   sudo mkdir -p /data/shares/public
   sudo tar xzf /tmp/samba_public_backup.tar.gz -C /data/shares/
   sudo chmod 2777 /data/shares/public
   # 配置 smb.conf（新增 screenshots 共享）
   sudo cp smb.conf.template /etc/samba/smb.conf
   sudo systemctl restart smbd nmbd
   ```

6. 运行标题补丁：
   ```bash
   cd ~/Cloud/guacamole && bash patch-title.sh "Htkis-Cloud"
   ```

**Phase 3: 验证**

1. 本地验证 Guacamole：
   ```bash
   curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/guacamole/
   # 期望: 200 或 302
   ```

2. 验证 frpc 隧道：
   ```bash
   sudo systemctl status frpc
   # 检查 frps dashboard 确认代理在线
   ```

3. 验证公网访问：
   ```bash
   curl -s -o /dev/null -w '%{http_code}' http://****.htkis.com/guacamole/
   curl -sk -o /dev/null -w '%{http_code}' https://****.htkis.com/guacamole/
   ```

4. 验证 RDP 连接：
   - 通过 Guacamole Web 界面登录并连接 RDP

5. 验证 Samba：
   ```bash
   # 从 Windows Server 测试
   dir \\192.168.2.3\public
   ```

**Phase 4: 旧服务器退役**

1. 确认新服务器所有服务正常后，停止 192.168.2.102 上的服务：
   ```bash
   sudo systemctl stop frpc
   sudo systemctl disable frpc
   cd ~/guacamole && docker compose down
   sudo systemctl stop smbd nmbd
   sudo systemctl disable smbd nmbd
   ```

#### 回滚方案

若迁移验证失败，执行以下回滚步骤：

1. 停止 192.168.2.3 上的 frpc（避免端口冲突）
2. 在 192.168.2.102 上重新启动所有服务：
   ```bash
   cd ~/guacamole && docker compose up -d
   sudo systemctl start frpc
   sudo systemctl start smbd nmbd
   ```
3. 验证旧服务器服务恢复正常

#### 迁移脚本设计

提供 `migrate.sh` 一键迁移脚本，位于 `deploy/migrate.sh`，流程如下：

```
migrate.sh
├── pre_check()          # 检查新服务器环境、网络连通性
├── backup_old()         # 在旧服务器上备份数据
├── transfer_data()      # 传输数据到新服务器
├── deploy_new()         # 在新服务器上部署所有服务
├── verify()             # 验证迁移结果
└── retire_old()         # 退役旧服务器（需确认）
```

### 1.3.2 邮箱验证登录设计

#### 组件架构

```
┌──────────────────────────────────────────────────────────────────┐
│  Nginx :8080 (本地反代)                                          │
│                                                                    │
│  location /guacamole/ {                                            │
│      auth_request /auth;                                           │
│      auth_request_set $auth_status $upstream_status;               │
│      error_page 401 = /api/email-auth/login-page;                 │
│      proxy_pass http://127.0.0.1:8081;                            │
│  }                                                                 │
│                                                                    │
│  location = /auth {                                                │
│      internal;                                                     │
│      proxy_pass http://127.0.0.1:5001/auth;                       │
│  }                                                                 │
│                                                                    │
│  location /api/email-auth/ {                                       │
│      proxy_pass http://127.0.0.1:5001/;                           │
│  }                                                                 │
└──────────────────────────────────────────────────────────────────┘
         │                                │
         ▼                                ▼
┌─────────────────────┐    ┌─────────────────────────────────────┐
│  Guacamole :8081    │    │  邮箱验证服务 :5001 (FastAPI)        │
│  (仅本地可访问)      │    │                                       │
│                     │    │  /auth          → JWT 验证            │
│  原生用户名+密码认证 │    │  /login         → 密码验证            │
│                     │    │  /send-code     → 发送验证码          │
│                     │    │  /verify-code   → 校验验证码          │
│                     │    │  /bind-email    → 绑定邮箱            │
│                     │    │  /admin/emails  → 邮箱管理            │
│                     │    │                                       │
│                     │    │  依赖:                               │
│                     │    │  ├─ PostgreSQL (验证码/邮箱/免验证期) │
│                     │    │  ├─ SMTP (发送验证码邮件)             │
│                     │    │  └─ Guacamole REST API (验证密码)    │
│                     │    │                                       │
│                     │    │  JWT Cookie:                         │
│                     │    │  ├─ htkis_auth_token                │
│                     │    │  ├─ HttpOnly, Secure, SameSite=Lax  │
│                     │    │  └─ 有效期 30 天                      │
└─────────────────────┘    └─────────────────────────────────────┘
```

#### 认证流程详细设计

**场景 1: 用户首次登录（无 JWT cookie）**

```
1. 用户访问 /guacamole/
2. Nginx auth_request → 邮箱验证服务 /auth
3. /auth 检测无 cookie → 返回 401
4. Nginx error_page 401 → 重定向到 /api/email-auth/login-page
5. 邮箱验证服务返回登录页面（用户名+密码表单）
6. 用户提交用户名+密码
7. 邮箱验证服务调用 Guacamole REST API 验证密码
   POST http://127.0.0.1:8081/guacamole/api/tokens
   Basic Auth: username:password
8. 验证通过 → 检查用户是否绑定邮箱
   a. 未绑定 → 返回错误 "该用户未绑定邮箱，请联系管理员"
   b. 已绑定 → 检查免验证期
      i.  免验证期内 → 直接设置 JWT cookie，重定向到 /guacamole/
      ii. 需邮箱验证 → 生成验证码，发送邮件，返回验证码输入页面
9. 用户输入验证码
10. 邮箱验证服务校验验证码
    a. 正确 → 记录免验证期，设置 JWT cookie，重定向到 /guacamole/
    b. 错误 → 返回错误提示
    c. 过期 → 返回过期提示
```

**场景 2: 用户免验证期内再次登录（有有效 JWT cookie）**

```
1. 用户访问 /guacamole/
2. Nginx auth_request → 邮箱验证服务 /auth
3. /auth 验证 JWT cookie 有效 → 返回 200
4. Nginx proxy_pass → Guacamole :8081
5. 用户直接进入 Guacamole 界面
```

**场景 3: JWT cookie 过期**

```
1. 用户访问 /guacamole/
2. Nginx auth_request → 邮箱验证服务 /auth
3. /auth 检测 JWT 过期 → 返回 401
4. 重复场景 1 流程
```

#### JWT Token 设计

```json
{
  "sub": "tablet",
  "exp": 1720000000,
  "iat": 1717408000,
  "type": "htkis_auth",
  "guac_token": "ABC123..."
}
```

| 字段 | 类型 | 说明 |
|------|------|------|
| sub | string | Guacamole 用户名 |
| exp | integer | 过期时间（Unix 时间戳，iat + 30天） |
| iat | integer | 签发时间 |
| type | string | 固定值 "htkis_auth" |
| guac_token | string | Guacamole REST API token（用于代理请求） |

JWT 签名密钥通过环境变量 `JWT_SECRET` 配置，部署时生成随机密钥。

#### 邮件模板设计

验证码邮件使用 HTML 模板：

```
主题: 【HTKIS 云办公】登录验证码

正文:
尊敬的用户 {username}：

您正在登录 HTKIS 云办公平台，验证码为：

    {code}

验证码 5 分钟内有效，请勿泄露给他人。

如非本人操作，请忽略此邮件。

— HTKIS 云办公平台
```

#### 邮箱绑定管理

管理员通过 API 为用户绑定/修改邮箱，不提供自助绑定（MVP 阶段简化）：

```
POST /api/email-auth/admin/bind-email
Authorization: Bearer <admin_jwt>
Body: { "username": "tablet", "email": "user@example.com" }

Response: { "success": true, "message": "邮箱绑定成功" }
```

### 1.3.3 截屏记录设计

#### 组件架构

```
┌─────────────────────────────────────────────────────────────────┐
│  Windows Server (192.168.2.88)                                   │
│                                                                   │
│  截屏脚本: C:\Scripts\capture-screen.ps1                         │
│  计划任务: HTKIS-Screenshot (每10秒, 仅在用户会话活跃时运行)      │
│                                                                   │
│  输出路径: \\192.168.2.3\screenshots\{username}\{date}\{time}.png│
│  清理脚本: C:\Scripts\cleanup-screenshots.ps1 (每天, 删除>90天)  │
└───────────────────────────┬──────────────────────────────────────┘
                            │ Samba 写入
                            ▼
┌─────────────────────────────────────────────────────────────────┐
│  Debian Server (192.168.2.3)                                     │
│                                                                   │
│  /data/screenshots/  (Samba 共享 "screenshots")                  │
│  ├── tablet/                                                      │
│  │   ├── 2026-06-29/                                             │
│  │   │   ├── 090000.png                                          │
│  │   │   ├── 090010.png                                          │
│  │   │   └── ...                                                 │
│  │   └── 2026-06-30/                                             │
│  └── cii/                                                         │
│                                                                   │
│  截屏管理服务 :5002 (FastAPI)                                     │
│  ├── GET /screenshot/           → 管理界面首页                    │
│  ├── GET /screenshot/api/list   → 截屏记录列表 API               │
│  ├── GET /screenshot/api/image  → 截屏图片访问 API               │
│  └── GET /screenshot/api/stats  → 截屏统计信息 API               │
│                                                                   │
│  截屏清理定时任务 (cron, 每天 02:00)                              │
│  删除 /data/screenshots/ 下超过 90 天的文件                       │
└─────────────────────────────────────────────────────────────────┘
```

#### Windows Server 截屏脚本设计

**capture-screen.ps1**：

```powershell
# 核心逻辑伪代码
param(
    [string]$SambaPath = "\\192.168.2.3\screenshots",
    [int]$Quality = 75  # JPEG 质量 (1-100)
)

# 1. 获取当前登录用户名
$username = $env:USERNAME

# 2. 构建输出路径
$date = Get-Date -Format "yyyy-MM-dd"
$time = Get-Date -Format "HHmmss"
$outputDir = Join-Path $SambaPath "$username\$date"
$outputFile = Join-Path $outputDir "$time.png"

# 3. 确保目录存在
if (-not (Test-Path $outputDir)) {
    New-Item -ItemType Directory -Path $outputDir -Force | Out-Null
}

# 4. 截屏 (使用 .NET System.Drawing)
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$screen = [System.Windows.Forms.Screen]::PrimaryScreen
$bounds = $screen.Bounds
$bitmap = New-Object System.Drawing.Bitmap($bounds.Width, $bounds.Height)
$graphics = [System.Drawing.Graphics]::FromImage($bitmap)
$graphics.CopyFromScreen($bounds.Location, [System.Drawing.Point]::Empty, $bounds.Size)

# 5. 保存为 PNG
$bitmap.Save($outputFile, [System.Drawing.Imaging.ImageFormat]::Png)
$graphics.Dispose()
$bitmap.Dispose()
```

**计划任务配置**：

```xml
<!-- 每10秒执行，仅在有用户登录会话时运行 -->
<TaskTrigger>
  <Repetition>
    <Interval>PT10S</Interval>
    <StopAtDurationEnd>false</StopAtDurationEnd>
  </Repetition>
  <Enabled>true</Enabled>
  <StartBoundary>2026-01-01T00:00:00</StartBoundary>
</TaskTrigger>
<Settings>
  <DisallowStartIfOnBatteries>false</DisallowStartIfOnBatteries>
  <StopIfGoingOnBatteries>false</StopIfGoingOnBatteries>
  <AllowStartOnDemand>true</AllowStartOnDemand>
  <RunOnlyIfNetworkAvailable>true</RunOnlyIfNetworkAvailable>
</Settings>
```

#### 截屏存储估算

| 参数 | 值 |
|------|-----|
| 单张截屏大小 | ~200KB (1920x1080 PNG 压缩后) |
| 每小时截屏数 | 360 张 (每10秒1张) |
| 每用户每天截屏 | ~2,880 张 (8小时工作日) |
| 每用户每天存储 | ~560MB |
| 每用户90天存储 | ~50GB |
| 5用户90天总存储 | ~250GB |

> **优化建议**：如存储压力大，可将 PNG 改为 JPEG (quality=75)，单张约 50KB，90天总存储降至 ~60GB。

#### 截屏清理策略

双重清理机制：

1. **Debian 端 cron 定时清理**（主要）：每天 02:00 执行 `find /data/screenshots -type f -mtime +90 -delete`，同时清理空目录
2. **Windows 端 PowerShell 清理脚本**（辅助）：每天 01:00 执行，删除 Samba 共享中超过 90 天的文件

---

# 2. 接口设计

## 2.1 总体设计

新增接口分为三组：

1. **邮箱验证服务接口** (`/api/email-auth/*`)：处理登录认证、验证码、邮箱绑定
2. **截屏管理服务接口** (`/screenshot/api/*`)：提供截屏记录查询和图片访问
3. **Nginx auth 子请求接口** (`/auth`)：供 Nginx `auth_request` 调用

所有接口遵循 RESTful 风格，响应格式为 JSON。认证接口使用 JWT cookie，管理接口使用 Bearer token。

## 2.2 接口清单

### 2.2.1 邮箱验证服务接口

#### POST /api/email-auth/login

用户名+密码登录，验证通过后触发邮箱验证流程。

| 项目 | 值 |
|------|-----|
| 路径 | /api/email-auth/login |
| 方法 | POST |
| Content-Type | application/json |
| 认证 | 无 |

**请求体**：

```json
{
  "username": "tablet",
  "password": "user_password"
}
```

**响应体 (需邮箱验证)**：

```json
{
  "success": true,
  "require_email_code": true,
  "email_mask": "u***@example.com",
  "message": "验证码已发送到绑定邮箱"
}
```

**响应体 (免验证期内)**：

```json
{
  "success": true,
  "require_email_code": false,
  "redirect": "/guacamole/"
}
```

> 同时设置 `Set-Cookie: htkis_auth_token=<jwt>; HttpOnly; Secure; SameSite=Lax; Max-Age=2592000; Path=/`

**响应体 (未绑定邮箱)**：

```json
{
  "success": false,
  "error_code": "EMAIL_NOT_BOUND",
  "message": "该用户未绑定邮箱，请联系管理员"
}
```

**响应体 (密码错误)**：

```json
{
  "success": false,
  "error_code": "INVALID_CREDENTIALS",
  "message": "用户名或密码错误"
}
```

---

#### POST /api/email-auth/send-code

发送或重发邮箱验证码。

| 项目 | 值 |
|------|-----|
| 路径 | /api/email-auth/send-code |
| 方法 | POST |
| Content-Type | application/json |
| 认证 | 无 (需先调用 /login 成功) |

**请求体**：

```json
{
  "username": "tablet"
}
```

**响应体 (成功)**：

```json
{
  "success": true,
  "message": "验证码已发送",
  "resend_after": 60
}
```

**响应体 (频率限制)**：

```json
{
  "success": false,
  "error_code": "RATE_LIMITED",
  "message": "发送过于频繁，请60秒后重试",
  "resend_after": 45
}
```

---

#### POST /api/email-auth/verify-code

校验邮箱验证码。

| 项目 | 值 |
|------|-----|
| 路径 | /api/email-auth/verify-code |
| 方法 | POST |
| Content-Type | application/json |
| 认证 | 无 |

**请求体**：

```json
{
  "username": "tablet",
  "code": "123456"
}
```

**响应体 (成功)**：

```json
{
  "success": true,
  "redirect": "/guacamole/"
}
```

> 同时设置 `Set-Cookie: htkis_auth_token=<jwt>; HttpOnly; Secure; SameSite=Lax; Max-Age=2592000; Path=/`

**响应体 (验证码错误)**：

```json
{
  "success": false,
  "error_code": "INVALID_CODE",
  "message": "验证码错误",
  "attempts_remaining": 4
}
```

**响应体 (验证码过期)**：

```json
{
  "success": false,
  "error_code": "CODE_EXPIRED",
  "message": "验证码已过期，请重新获取"
}
```

**响应体 (验证码已失效)**：

```json
{
  "success": false,
  "error_code": "CODE_INVALIDATED",
  "message": "验证码已失效，请重新获取"
}
```

---

#### GET /api/email-auth/auth

Nginx `auth_request` 子请求入口，验证 JWT cookie。

| 项目 | 值 |
|------|-----|
| 路径 | /api/email-auth/auth |
| 方法 | GET |
| 认证 | JWT cookie (htkis_auth_token) |

**响应**：

| 状态码 | 条件 | 说明 |
|--------|------|------|
| 200 | JWT 有效 | Nginx 放行请求到 Guacamole |
| 401 | 无 cookie / JWT 无效 / JWT 过期 | Nginx 重定向到登录页 |

---

#### POST /api/email-auth/logout

注销登录，清除 JWT cookie。

| 项目 | 值 |
|------|-----|
| 路径 | /api/email-auth/logout |
| 方法 | POST |
| 认证 | JWT cookie |

**响应**：

```json
{
  "success": true,
  "redirect": "/api/email-auth/login-page"
}
```

> 同时清除 `Set-Cookie: htkis_auth_token=; Max-Age=0; Path=/`

---

#### POST /api/email-auth/admin/bind-email

管理员为用户绑定或修改邮箱。

| 项目 | 值 |
|------|-----|
| 路径 | /api/email-auth/admin/bind-email |
| 方法 | POST |
| Content-Type | application/json |
| 认证 | JWT cookie (管理员) |

**请求体**：

```json
{
  "username": "tablet",
  "email": "user@example.com"
}
```

**响应体 (成功)**：

```json
{
  "success": true,
  "message": "邮箱绑定成功"
}
```

**响应体 (邮箱格式错误)**：

```json
{
  "success": false,
  "error_code": "INVALID_EMAIL",
  "message": "邮箱格式不合法"
}
```

---

#### GET /api/email-auth/admin/emails

查询所有用户的邮箱绑定情况。

| 项目 | 值 |
|------|-----|
| 路径 | /api/email-auth/admin/emails |
| 方法 | GET |
| 认证 | JWT cookie (管理员) |

**响应体**：

```json
{
  "success": true,
  "data": [
    {
      "username": "tablet",
      "email": "user@example.com",
      "bound_at": "2026-06-29T10:00:00Z",
      "updated_at": "2026-06-29T10:00:00Z"
    },
    {
      "username": "cii",
      "email": null,
      "bound_at": null,
      "updated_at": null
    }
  ]
}
```

---

#### DELETE /api/email-auth/admin/bind-email

管理员解除用户邮箱绑定。

| 项目 | 值 |
|------|-----|
| 路径 | /api/email-auth/admin/bind-email |
| 方法 | DELETE |
| Content-Type | application/json |
| 认证 | JWT cookie (管理员) |

**请求体**：

```json
{
  "username": "tablet"
}
```

**响应体**：

```json
{
  "success": true,
  "message": "邮箱绑定已解除"
}
```

---

### 2.2.2 截屏管理服务接口

#### GET /screenshot/

截屏管理界面首页（HTML 页面）。

| 项目 | 值 |
|------|-----|
| 路径 | /screenshot/ |
| 方法 | GET |
| 认证 | JWT cookie (管理员) |

**响应**：HTML 页面，包含用户选择、日期范围筛选、截屏列表展示。

---

#### GET /screenshot/api/list

查询截屏记录列表。

| 项目 | 值 |
|------|-----|
| 路径 | /screenshot/api/list |
| 方法 | GET |
| 认证 | JWT cookie (管理员) |

**查询参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| username | string | 是 | 用户名 |
| date_from | string | 是 | 起始日期 (YYYY-MM-DD) |
| date_to | string | 是 | 结束日期 (YYYY-MM-DD) |
| page | integer | 否 | 页码，默认 1 |
| page_size | integer | 否 | 每页条数，默认 100 |

**响应体**：

```json
{
  "success": true,
  "data": {
    "total": 2880,
    "page": 1,
    "page_size": 100,
    "records": [
      {
        "file_path": "tablet/2026-06-29/090000.png",
        "username": "tablet",
        "capture_time": "2026-06-29T09:00:00Z",
        "file_size": 204800,
        "session_id": "rdp-session-001"
      }
    ]
  }
}
```

---

#### GET /screenshot/api/image

获取截屏图片。

| 项目 | 值 |
|------|-----|
| 路径 | /screenshot/api/image |
| 方法 | GET |
| 认证 | JWT cookie (管理员) |

**查询参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| path | string | 是 | 截屏相对路径 (如 tablet/2026-06-29/090000.png) |

**响应**：图片文件流 (image/png)，带 `Content-Disposition: inline` 头。

**错误响应**：

```json
{
  "success": false,
  "error_code": "FILE_NOT_FOUND",
  "message": "截屏文件不存在"
}
```

---

#### GET /screenshot/api/stats

获取截屏统计信息。

| 项目 | 值 |
|------|-----|
| 路径 | /screenshot/api/stats |
| 方法 | GET |
| 认证 | JWT cookie (管理员) |

**响应体**：

```json
{
  "success": true,
  "data": {
    "total_files": 28800,
    "total_size_mb": 5600,
    "oldest_date": "2026-04-01",
    "newest_date": "2026-06-29",
    "users": [
      {
        "username": "tablet",
        "file_count": 14400,
        "size_mb": 2800
      }
    ],
    "disk_usage_percent": 35.2
  }
}
```

---

# 4. 数据模型

## 4.1 设计目标

1. 与 Guacamole 现有数据库 (`guacamole_db`) 解耦，新增数据存储在独立数据库 (`htkis_cloud`)
2. 用户名与 Guacamole `guacamole_entity.name` 保持逻辑关联，不使用外键约束（跨库）
3. 验证码数据高频读写，需合理索引
4. 截屏元数据支持按用户、日期范围高效查询

## 4.2 模型实现

### 4.2.1 数据库架构

```
PostgreSQL 实例
├── guacamole_db (Guacamole 原有，端口 5432)
│   └── guacamole_user, guacamole_entity, ... (不变)
│
└── htkis_cloud (新增，端口 5433)
    ├── user_email_binding     (用户邮箱绑定)
    ├── email_verification_code (邮箱验证码)
    ├── auth_exempt_period     (免验证期记录)
    └── screenshot_record      (截屏记录元数据)
```

### 4.2.2 user_email_binding (用户邮箱绑定)

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | serial | PK, AUTO | 主键 |
| username | varchar(128) | NOT NULL, UNIQUE | Guacamole 用户名，与 guacamole_entity.name 逻辑关联 |
| email | varchar(256) | NOT NULL | 邮箱地址 (RFC 5322 格式) |
| bound_at | timestamptz | NOT NULL, DEFAULT now() | 绑定时间 |
| updated_at | timestamptz | NOT NULL, DEFAULT now() | 最后修改时间 |

**索引**：

```sql
CREATE UNIQUE INDEX idx_user_email_binding_username ON user_email_binding(username);
CREATE INDEX idx_user_email_binding_email ON user_email_binding(email);
```

**DDL**：

```sql
CREATE TABLE user_email_binding (
    id          serial       NOT NULL,
    username    varchar(128) NOT NULL,
    email       varchar(256) NOT NULL,
    bound_at    timestamptz  NOT NULL DEFAULT now(),
    updated_at  timestamptz  NOT NULL DEFAULT now(),

    PRIMARY KEY (id),

    CONSTRAINT uq_user_email_binding_username UNIQUE (username),
    CONSTRAINT ck_user_email_binding_email CHECK (email ~* '^[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}$')
);
```

### 4.2.3 email_verification_code (邮箱验证码)

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | serial | PK, AUTO | 主键 |
| username | varchar(128) | NOT NULL | 关联用户名 |
| code | varchar(6) | NOT NULL | 6 位数字验证码 |
| status | varchar(20) | NOT NULL, DEFAULT 'PENDING' | 状态: PENDING / VERIFIED / EXPIRED / INVALIDATED |
| sent_at | timestamptz | NOT NULL, DEFAULT now() | 发送时间 |
| expire_at | timestamptz | NOT NULL | 过期时间 (sent_at + 5min) |
| verified_at | timestamptz | NULL | 验证通过时间 |
| fail_count | integer | NOT NULL, DEFAULT 0 | 验证失败次数 |
| created_at | timestamptz | NOT NULL, DEFAULT now() | 创建时间 |

**索引**：

```sql
CREATE INDEX idx_ev_code_username_status ON email_verification_code(username, status);
CREATE INDEX idx_ev_code_expire_at ON email_verification_code(expire_at);
```

**DDL**：

```sql
CREATE TYPE verification_status AS ENUM ('PENDING', 'VERIFIED', 'EXPIRED', 'INVALIDATED');

CREATE TABLE email_verification_code (
    id          serial              NOT NULL,
    username    varchar(128)        NOT NULL,
    code        varchar(6)          NOT NULL,
    status      verification_status NOT NULL DEFAULT 'PENDING',
    sent_at     timestamptz         NOT NULL DEFAULT now(),
    expire_at   timestamptz         NOT NULL,
    verified_at timestamptz,
    fail_count  integer             NOT NULL DEFAULT 0,
    created_at  timestamptz         NOT NULL DEFAULT now(),

    PRIMARY KEY (id),

    CONSTRAINT ck_ev_code_format CHECK (code ~ '^\d{6}$'),
    CONSTRAINT ck_ev_code_fail_count CHECK (fail_count >= 0 AND fail_count <= 5)
);
```

### 4.2.4 auth_exempt_period (免验证期记录)

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | serial | PK, AUTO | 主键 |
| username | varchar(128) | NOT NULL, UNIQUE | Guacamole 用户名 |
| device_id | varchar(128) | NOT NULL, DEFAULT 'default' | 设备/浏览器标识 |
| exempt_start | timestamptz | NOT NULL | 免验证起始时间 |
| exempt_end | timestamptz | NOT NULL | 免验证截止时间 (start + 30天) |
| created_at | timestamptz | NOT NULL, DEFAULT now() | 创建时间 |
| updated_at | timestamptz | NOT NULL, DEFAULT now() | 更新时间 |

**索引**：

```sql
CREATE UNIQUE INDEX idx_auth_exempt_username_device ON auth_exempt_period(username, device_id);
CREATE INDEX idx_auth_exempt_end ON auth_exempt_period(exempt_end);
```

**DDL**：

```sql
CREATE TABLE auth_exempt_period (
    id           serial       NOT NULL,
    username     varchar(128) NOT NULL,
    device_id    varchar(128) NOT NULL DEFAULT 'default',
    exempt_start timestamptz  NOT NULL,
    exempt_end   timestamptz  NOT NULL,
    created_at   timestamptz  NOT NULL DEFAULT now(),
    updated_at   timestamptz  NOT NULL DEFAULT now(),

    PRIMARY KEY (id),

    CONSTRAINT uq_auth_exempt_username_device UNIQUE (username, device_id)
);
```

### 4.2.5 screenshot_record (截屏记录)

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | serial | PK, AUTO | 主键 |
| file_path | varchar(512) | NOT NULL, UNIQUE | 截屏相对路径 ({username}/{date}/{time}.png) |
| username | varchar(128) | NOT NULL | 关联用户名 |
| capture_time | timestamptz | NOT NULL | 截屏采集时间 |
| session_id | varchar(128) | NOT NULL | RDP 会话标识 |
| file_size | bigint | NOT NULL | 文件大小 (字节) |
| retain_until | date | NOT NULL | 保留截止日期 (capture_time + 90天) |
| created_at | timestamptz | NOT NULL, DEFAULT now() | 记录创建时间 |

**索引**：

```sql
CREATE INDEX idx_screenshot_username_capture ON screenshot_record(username, capture_time);
CREATE INDEX idx_screenshot_retain_until ON screenshot_record(retain_until);
CREATE INDEX idx_screenshot_session_id ON screenshot_record(session_id);
```

**DDL**：

```sql
CREATE TABLE screenshot_record (
    id           serial       NOT NULL,
    file_path    varchar(512) NOT NULL,
    username     varchar(128) NOT NULL,
    capture_time timestamptz  NOT NULL,
    session_id   varchar(128) NOT NULL,
    file_size    bigint       NOT NULL,
    retain_until date        NOT NULL,
    created_at   timestamptz  NOT NULL DEFAULT now(),

    PRIMARY KEY (id),

    CONSTRAINT uq_screenshot_file_path UNIQUE (file_path)
);
```

---

# 5. 部署方案

## 5.1 Docker 化部署架构

### 5.1.1 新增 docker-compose 文件

新增 `docker-compose.services.yml` 与现有 Guacamole docker-compose.yml 分离，独立管理新增服务：

```yaml
# ~/Cloud/services/docker-compose.yml
services:
  nginx-proxy:
    image: nginx:alpine
    container_name: htkis-nginx
    restart: always
    network_mode: host
    volumes:
      - ./nginx/nginx.conf:/etc/nginx/nginx.conf:ro
      - ./nginx/conf.d:/etc/nginx/conf.d:ro

  email-auth:
    image: htkis/email-auth:latest
    container_name: htkis-email-auth
    restart: always
    build:
      context: ../../services/email-auth
      dockerfile: Dockerfile
    environment:
      - DATABASE_URL=postgresql://htkis:${HTKIS_DB_PASSWORD}@127.0.0.1:5433/htkis_cloud
      - GUACAMOLE_URL=http://127.0.0.1:8081/guacamole
      - SMTP_HOST=${SMTP_HOST}
      - SMTP_PORT=${SMTP_PORT}
      - SMTP_USER=${SMTP_USER}
      - SMTP_PASSWORD=${SMTP_PASSWORD}
      - SMTP_FROM=${SMTP_FROM}
      - JWT_SECRET=${JWT_SECRET}
      - JWT_EXPIRE_DAYS=30
      - ADMIN_USERNAMES=guacadmin
    network_mode: host

  screenshot-service:
    image: htkis/screenshot-service:latest
    container_name: htkis-screenshot
    restart: always
    build:
      context: ../../services/screenshot
      dockerfile: Dockerfile
    environment:
      - DATABASE_URL=postgresql://htkis:${HTKIS_DB_PASSWORD}@127.0.0.1:5433/htkis_cloud
      - SCREENSHOT_DIR=/data/screenshots
      - JWT_SECRET=${JWT_SECRET}
      - ADMIN_USERNAMES=guacadmin
    volumes:
      - /data/screenshots:/data/screenshots:ro
    network_mode: host

  app-postgres:
    image: postgres:15
    container_name: htkis_postgres
    restart: always
    environment:
      POSTGRES_DB: htkis_cloud
      POSTGRES_USER: htkis
      POSTGRES_PASSWORD: ${HTKIS_DB_PASSWORD}
    volumes:
      - htkis_pgdata:/var/lib/postgresql/data
      - ./init:/docker-entrypoint-initdb.d
    ports:
      - "5433:5432"

volumes:
  htkis_pgdata:
```

### 5.1.2 Nginx 反代配置

```nginx
# ~/Cloud/services/nginx/conf.d/default.conf

# auth 子请求 (internal only)
location = /auth {
    internal;
    proxy_pass http://127.0.0.1:5001/auth;
    proxy_pass_request_body off;
    proxy_set_header Content-Length "";
    proxy_set_header X-Original-URI $request_uri;
    proxy_set_header X-Original-Method $request_method;
    proxy_set_header Cookie $http_cookie;
}

# Guacamole 主入口 (需认证)
location /guacamole/ {
    auth_request /auth;
    auth_request_set $auth_status $upstream_status;

    # 认证失败重定向到登录页
    error_page 401 = /api/email-auth/login-page;

    proxy_pass http://127.0.0.1:8081;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header X-Forwarded-Proto $scheme;
    proxy_http_version 1.1;
    proxy_set_header Upgrade $http_upgrade;
    proxy_set_header Connection "upgrade";
    proxy_read_timeout 3600s;
    proxy_send_timeout 3600s;
}

# 邮箱验证服务 (无需 auth_request)
location /api/email-auth/ {
    proxy_pass http://127.0.0.1:5001/;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
}

# 截屏管理服务 (内部认证)
location /screenshot/ {
    proxy_pass http://127.0.0.1:5002/;
    proxy_set_header Host $host;
    proxy_set_header X-Real-IP $remote_addr;
    proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
    proxy_set_header Cookie $http_cookie;
}

# 根路径重定向
location = / {
    return 302 /guacamole/;
}
```

### 5.1.3 Guacamole docker-compose.yml 变更

```yaml
# ~/Cloud/guacamole/docker-compose.yml (变更点标注)
services:
  guacd:
    image: guacamole/guacd:latest
    container_name: guacd
    restart: always
    network_mode: host
    environment:
      GUACD_LOG_LEVEL: debug
    volumes:
      - /data/shares/public:/drive

  postgres:
    image: postgres:15
    container_name: guac_postgres
    restart: always
    environment:
      POSTGRES_DB: guacamole_db
      POSTGRES_USER: guacamole
      POSTGRES_PASSWORD: ${GUAC_DB_PASSWORD}
    volumes:
      - guac_pgdata:/var/lib/postgresql/data
      - ./init:/docker-entrypoint-initdb.d

  guacamole:
    image: guacamole/guacamole:latest
    container_name: guacamole
    restart: always
    ports:
      - "127.0.0.1:8081:8080"  # 变更: 仅绑定本地 8081
    environment:
      GUACD_HOSTNAME: 192.168.2.3  # 变更: 更新为新服务器 IP
      GUACD_PORT: 4822
      POSTGRESQL_HOSTNAME: postgres
      POSTGRESQL_PORT: 5432
      POSTGRESQL_DATABASE: guacamole_db
      POSTGRESQL_USER: guacamole
      POSTGRESQL_PASSWORD: ${GUAC_DB_PASSWORD}
      GUACAMOLE_HOME: /etc/guacamole
    volumes:
      - ./guacamole-home:/etc/guacamole
    depends_on:
      - guacd
      - postgres

volumes:
  guac_pgdata:
```

**关键变更**：
1. `guacamole` 端口从 `8080:8080` 改为 `127.0.0.1:8081:8080`，仅本地可访问
2. `GUACD_HOSTNAME` 更新为 `192.168.2.3`
3. frpc 的 `localPort` 保持 `8080`（指向 Nginx 反代）

## 5.2 Samba 配置变更

新增 `screenshots` 共享：

```ini
# /etc/samba/smb.conf 新增段

[screenshots]
   path = /data/screenshots
   browseable = no
   read only = no
   guest ok = yes
   force user = debian
   create mask = 0660
   directory mask = 0770
```

## 5.3 frpc 配置

frpc 配置无需变更，`localPort=8080` 指向本地 Nginx 反代：

```toml
serverAddr = "x.x.x.214"
serverPort = 7001

auth.method = "token"
auth.token = "****"

[[proxies]]
name = "htkis-guacamole"
type = "tcp"
localIP = "127.0.0.1"
localPort = 8080
remotePort = 18080
```

## 5.4 部署模板更新

### 5.4.1 新增模板文件

| 文件 | 说明 |
|------|------|
| `deploy/templates/services-docker-compose.yml` | 新增服务 docker-compose 模板 |
| `deploy/templates/nginx-proxy.conf.template` | Nginx 反代配置模板 |
| `deploy/templates/smb-screenshots.conf` | Samba screenshots 共享配置片段 |
| `deploy/services/email-auth/` | 邮箱验证服务源码 + Dockerfile |
| `deploy/services/screenshot/` | 截屏管理服务源码 + Dockerfile |
| `deploy/scripts/capture-screen.ps1` | Windows Server 截屏脚本 |
| `deploy/scripts/setup-screenshot-task.ps1` | Windows Server 计划任务安装脚本 |
| `deploy/scripts/cleanup-screenshots.sh` | Debian 端截屏清理 cron 脚本 |
| `deploy/scripts/migrate.sh` | 服务器迁移脚本 |
| `deploy/migrate.env.example` | 迁移配置模板 |

### 5.4.2 config.env.example 新增项

```bash
# ====== 邮箱验证服务 ======
SMTP_HOST="smtp.example.com"
SMTP_PORT="465"
SMTP_USER="noreply@example.com"
SMTP_PASSWORD="****"
SMTP_FROM="HTKIS Cloud Office <noreply@example.com>"
JWT_SECRET=""  # 部署时自动生成
ADMIN_USERNAMES="guacadmin"

# ====== 应用层数据库 ======
HTKIS_DB_PASSWORD="****"

# ====== 迁移配置 ======
OLD_SERVER_HOST="192.168.2.102"
NEW_SERVER_HOST="192.168.2.3"
```

## 5.5 启动顺序

```bash
# 1. 启动应用层数据库
cd ~/Cloud/services && docker compose up -d app-postgres
sleep 10

# 2. 启动 Guacamole
cd ~/Cloud/guacamole && docker compose up -d
sleep 30

# 3. 启动邮箱验证服务
cd ~/Cloud/services && docker compose up -d email-auth
sleep 5

# 4. 启动截屏管理服务
cd ~/Cloud/services && docker compose up -d screenshot-service
sleep 5

# 5. 启动 Nginx 反代 (最后启动，依赖上述服务)
cd ~/Cloud/services && docker compose up -d nginx-proxy

# 6. 启动 frpc
sudo systemctl start frpc

# 7. 运行标题补丁
cd ~/Cloud/guacamole && bash patch-title.sh "Htkis-Cloud"
```

## 5.6 Windows Server 截屏脚本部署

在 Windows Server (192.168.2.88) 上执行：

```powershell
# 1. 复制截屏脚本
Copy-Item capture-screen.ps1 C:\Scripts\capture-screen.ps1

# 2. 创建计划任务
.\setup-screenshot-task.ps1

# 3. 验证计划任务
Get-ScheduledTask -TaskName "HTKIS-Screenshot"
```

`setup-screenshot-task.ps1` 自动创建：
- 计划任务 `HTKIS-Screenshot`：每 10 秒执行截屏脚本
- 计划任务 `HTKIS-Screenshot-Cleanup`：每天 01:00 执行清理脚本