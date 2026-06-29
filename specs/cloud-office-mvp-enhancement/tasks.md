# HTKIS Cloud Office MVP 增强 — 编码任务规划

| 字段 | 值 |
|------|-----|
| 文档版本 | v1.0 |
| 创建日期 | 2026-06-29 |
| 关联规格 | spec.md (cloud-office-mvp-enhancement) |
| 关联设计 | design.md (cloud-office-mvp-enhancement) |
| 文档状态 | 初稿 |

---

## 优先级说明

- **P1 服务器迁移**：阻塞性任务，所有后续功能依赖新服务器环境就绪
- **P2 邮箱验证登录**：安全合规核心功能，依赖 P1 完成
- **P3 截屏记录**：审计功能，依赖 P1 完成，可与 P2 并行开发

---

## 1. 服务器迁移 — 环境准备

- [ ] 1.1 在 192.168.2.3 上安装 Docker Engine 26.x+ 和 Docker Compose v2.26+
  - 依赖：无
  - 复杂度：中
  - 验收标准：`docker --version` 和 `docker compose version` 输出正确版本号

- [ ] 1.2 在 192.168.2.3 上安装 frp 0.65.0 (frpc)
  - 依赖：1.1
  - 复杂度：小
  - 验收标准：`frpc --version` 输出 0.65.0

- [ ] 1.3 在 192.168.2.3 上安装 Samba
  - 依赖：1.1
  - 复杂度：小
  - 验收标准：`smbd --version` 输出版本号

- [ ] 1.4 在 192.168.2.3 上创建目录结构
  - 创建 `~/Cloud/guacamole/`、`~/Cloud/services/`、`/data/shares/public`、`/data/screenshots`
  - 依赖：1.1
  - 复杂度：小
  - 验收标准：所有目录存在且权限正确（`/data/shares/public` 权限 2777，`/data/screenshots` 权限 770）

- [ ] 1.5 验证 192.168.2.3 到公网 frps 和 RDP 的网络连通性
  - `nc -zv x.x.x.214 7001` 和 `nc -zv 192.168.2.88 3389`
  - 依赖：1.2
  - 复杂度：小
  - 验收标准：两个端口均连通

## 2. 服务器迁移 — 数据备份与传输

- [ ] 2.1 编写迁移脚本 `deploy/scripts/migrate.sh`
  - 包含 `pre_check()`、`backup_old()`、`transfer_data()`、`deploy_new()`、`verify()`、`retire_old()` 六个函数
  - 依赖：1.5
  - 复杂度：大
  - 验收标准：脚本语法正确，支持 `--dry-run` 模式预检，各函数逻辑完整

- [ ] 2.2 编写迁移配置模板 `deploy/migrate.env.example`
  - 包含 OLD_SERVER_HOST、NEW_SERVER_HOST、GUAC_DB_PASSWORD、HTKIS_DB_PASSWORD 等变量
  - 依赖：无
  - 复杂度：小
  - 验收标准：模板包含所有迁移所需变量，带注释说明

- [ ] 2.3 停止旧服务器 192.168.2.102 上的所有服务并备份数据
  - 停止 frpc、Guacamole (docker compose down)、Samba
  - 导出 PostgreSQL 数据 (`pg_dump`)
  - 打包 Samba 共享文件 (`tar`)
  - 备份 frpc.toml、docker-compose.yml、guacamole-home
  - 依赖：2.1
  - 复杂度：中
  - 验收标准：备份文件完整，md5sum 校验通过

- [ ] 2.4 传输备份数据到新服务器 192.168.2.3
  - 通过 scp 传输 SQL 备份、Samba 压缩包、配置文件
  - 依赖：2.3
  - 复杂度：小
  - 验收标准：所有文件传输完成，新服务器上文件 md5sum 与源一致

## 3. 服务器迁移 — 新服务器部署

- [ ] 3.1 部署 Guacamole 容器到 192.168.2.3
  - 更新 docker-compose.yml：GUACD_HOSTNAME 改为 192.168.2.3，guacamole 端口改为 `127.0.0.1:8081:8080`
  - 导入 PostgreSQL 数据
  - 运行标题补丁
  - 依赖：2.4
  - 复杂度：中
  - 验收标准：`curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1:8081/guacamole/` 返回 200 或 302

- [ ] 3.2 部署 frpc 到 192.168.2.3
  - 复制 frpc.toml 配置（serverAddr、auth.token 不变）
  - 创建 systemd 服务并启动
  - 依赖：2.4
  - 复杂度：小
  - 验收标准：`sudo systemctl status frpc` 显示 active，frps dashboard 确认代理在线

- [ ] 3.3 部署 Samba 并恢复共享文件到 192.168.2.3
  - 解压 Samba 备份到 `/data/shares/`
  - 配置 smb.conf
  - 依赖：2.4
  - 复杂度：小
  - 验收标准：从 Windows Server 可访问 `\\192.168.2.3\public`

- [ ] 3.4 更新 deploy/templates/ 模板文件
  - 更新 `docker-compose.yml` 模板（端口绑定、GUACD_HOSTNAME）
  - 更新 `frpc.toml.template`（确保与新架构一致）
  - 更新 `smb.conf.template`（新增 screenshots 共享段）
  - 依赖：3.1, 3.2, 3.3
  - 复杂度：中
  - 验收标准：模板文件与实际部署配置一致

## 4. 服务器迁移 — 验证与回滚

- [ ] 4.1 执行迁移验证
  - 本地验证 Guacamole (curl 127.0.0.1:8081)
  - 验证 frpc 隧道（frps dashboard 确认代理在线）
  - 验证公网访问（curl http/https ****.htkis.com）
  - 验证 RDP 连接（通过 Guacamole Web 界面）
  - 验证 Samba 文件共享
  - 依赖：3.1, 3.2, 3.3
  - 复杂度：中
  - 验收标准：所有验证项通过，全链路可用

- [ ] 4.2 编写回滚方案文档并验证
  - 停止新服务器 frpc → 重启旧服务器全部服务 → 验证旧服务器恢复
  - 依赖：4.1
  - 复杂度：中
  - 验收标准：回滚脚本可执行，旧服务器服务可恢复

- [ ] 4.3 退役旧服务器 192.168.2.102
  - 确认新服务器所有服务正常后，停止并禁用旧服务器上的 frpc、Guacamole、Samba
  - 依赖：4.1
  - 复杂度：小
  - 验收标准：旧服务器所有服务已停止且 disabled

---

## 5. 邮箱验证服务 — 数据库与基础框架

- [ ] 5.1 创建应用层数据库 Docker 容器配置
  - 编写 `deploy/templates/services-docker-compose.yml` 中 `app-postgres` 服务定义
  - 端口 5433，数据库 `htkis_cloud`，用户 `htkis`
  - 依赖：3.1（Guacamole 已迁移部署）
  - 复杂度：小
  - 验收标准：`docker compose up -d app-postgres` 启动成功，`pg_isready` 返回正常

- [ ] 5.2 编写数据库初始化 SQL 脚本
  - 创建 `user_email_binding` 表（含 username UNIQUE 约束、email CHECK 约束）
  - 创建 `email_verification_code` 表（含 verification_status ENUM、fail_count CHECK）
  - 创建 `auth_exempt_period` 表（含 username+device_id UNIQUE 约束）
  - 创建 `screenshot_record` 表（含 file_path UNIQUE 约束、retain_until 索引）
  - 创建所有索引（idx_user_email_binding_username、idx_ev_code_username_status、idx_auth_exempt_username_device、idx_screenshot_username_capture 等）
  - 依赖：5.1
  - 复杂度：中
  - 验收标准：SQL 脚本可在 htkis_cloud 数据库上无错误执行，所有表和索引创建成功

- [ ] 5.3 搭建邮箱验证服务 FastAPI 项目骨架
  - 创建 `deploy/services/email-auth/` 目录结构
  - 编写 `Dockerfile`（基于 python:3.12-slim）
  - 编写 `requirements.txt`（fastapi、uvicorn、sqlalchemy、asyncpg、aiosmtplib、python-jose、passlib、httpx）
  - 编写 `main.py` 入口（FastAPI app、CORS、路由挂载）
  - 编写 `config.py` 配置管理（环境变量读取：DATABASE_URL、GUACAMOLE_URL、SMTP_*、JWT_SECRET、ADMIN_USERNAMES）
  - 编写 `database.py` 数据库连接（SQLAlchemy async engine + session）
  - 依赖：5.2
  - 复杂度：中
  - 验收标准：`docker compose up -d email-auth` 启动成功，`curl http://127.0.0.1:5001/docs` 返回 OpenAPI 文档

## 6. 邮箱验证服务 — 核心认证逻辑

- [ ] 6.1 实现 Guacamole REST API 密码验证模块
  - 调用 `POST http://127.0.0.1:8081/guacamole/api/tokens`，使用 Basic Auth 验证用户名密码
  - 成功获取 Guacamole token，失败返回 INVALID_CREDENTIALS
  - 依赖：5.3
  - 复杂度：中
  - 验收标准：正确密码返回 Guacamole token，错误密码返回 INVALID_CREDENTIALS 错误

- [ ] 6.2 实现验证码生成与存储逻辑
  - 生成 6 位随机数字验证码
  - 存储到 `email_verification_code` 表，设置 expire_at = sent_at + 5min
  - 同一用户新验证码生成后，将旧验证码状态更新为 INVALIDATED
  - 依赖：5.2
  - 复杂度：中
  - 验收标准：验证码为 6 位随机数字，数据库记录正确，旧验证码自动失效

- [ ] 6.3 实现验证码邮件发送模块
  - 使用 aiosmtplib 异步发送邮件
  - HTML 邮件模板：主题"【HTKIS 云办公】登录验证码"，正文包含验证码和有效期提示
  - 60 秒内仅允许发送一次（频率限制）
  - SMTP 连接失败时记录错误日志并返回友好提示
  - 依赖：6.2
  - 复杂度：中
  - 验收标准：验证码邮件成功发送到绑定邮箱，60 秒内重复发送返回 RATE_LIMITED 错误

- [ ] 6.4 实现验证码校验逻辑
  - 校验验证码正确性、有效期、失败次数
  - 正确 → 更新状态为 VERIFIED，记录免验证期
  - 错误 → fail_count +1，达 5 次后状态变为 INVALIDATED
  - 过期 → 返回 CODE_EXPIRED
  - 已失效 → 返回 CODE_INVALIDATED
  - 依赖：6.2
  - 复杂度：中
  - 验收标准：各种场景返回正确的错误码和剩余次数

## 7. 邮箱验证服务 — JWT Cookie 与认证流程

- [ ] 7.1 实现 JWT Token 生成与验证模块
  - Token payload：sub（用户名）、exp（过期时间）、iat（签发时间）、type（htkis_auth）、guac_token（Guacamole token）
  - 签名密钥通过 JWT_SECRET 环境变量配置
  - 有效期 30 天
  - 依赖：5.3
  - 复杂度：中
  - 验收标准：JWT 生成和验证正确，过期 Token 被拒绝

- [ ] 7.2 实现 JWT Cookie 设置与清除
  - Cookie 名称：`htkis_auth_token`
  - 属性：HttpOnly、Secure、SameSite=Lax、Max-Age=2592000（30天）、Path=/
  - 注销时清除 Cookie（Max-Age=0）
  - 依赖：7.1
  - 复杂度：小
  - 验收标准：登录成功后浏览器收到 Set-Cookie 头，注销后 Cookie 被清除

- [ ] 7.3 实现 Nginx auth_request 子请求处理端点 `GET /auth`
  - 从请求中读取 `htkis_auth_token` Cookie
  - 验证 JWT 有效性
  - 有效 → 返回 200
  - 无效/过期/不存在 → 返回 401
  - 依赖：7.1
  - 复杂度：小
  - 验收标准：Nginx auth_request 可正确调用，有效 JWT 返回 200，无效返回 401

- [ ] 7.4 实现免验证期管理逻辑
  - 验证码通过后，在 `auth_exempt_period` 表记录免验证期（起始时间 + 30天）
  - 登录时检查免验证期：在期内 → 跳过邮箱验证，直接设置 JWT cookie
  - 超过 30 天 → 要求邮箱验证
  - 依赖：5.2, 7.1
  - 复杂度：中
  - 验收标准：免验证期内用户可直接登录，过期后需重新验证

## 8. 邮箱验证服务 — API 端点实现

- [ ] 8.1 实现 `POST /api/email-auth/login` 端点
  - 接收 username + password
  - 调用 Guacamole API 验证密码
  - 检查邮箱绑定状态（未绑定 → EMAIL_NOT_BOUND）
  - 检查免验证期（在期内 → 直接设置 JWT cookie，返回 require_email_code=false）
  - 需邮箱验证 → 生成验证码并发送，返回 require_email_code=true + email_mask
  - 依赖：6.1, 6.3, 7.4
  - 复杂度：大
  - 验收标准：三种场景（免验证/需验证/未绑定邮箱）均返回正确响应

- [ ] 8.2 实现 `POST /api/email-auth/send-code` 端点
  - 接收 username
  - 60 秒频率限制
  - 生成新验证码，旧验证码失效
  - 发送邮件
  - 依赖：6.2, 6.3
  - 复杂度：中
  - 验收标准：成功发送返回 resend_after=60，频率限制返回 RATE_LIMITED

- [ ] 8.3 实现 `POST /api/email-auth/verify-code` 端点
  - 接收 username + code
  - 校验验证码（正确/错误/过期/已失效）
  - 正确 → 记录免验证期，设置 JWT cookie
  - 依赖：6.4, 7.2, 7.4
  - 复杂度：中
  - 验收标准：各场景返回正确响应，验证成功后 JWT cookie 正确设置

- [ ] 8.4 实现 `POST /api/email-auth/logout` 端点
  - 清除 JWT cookie
  - 返回重定向到登录页
  - 依赖：7.2
  - 复杂度：小
  - 验收标准：注销后 Cookie 被清除，重定向到登录页

- [ ] 8.5 实现 `POST /api/email-auth/admin/bind-email` 端点
  - 管理员为用户绑定/修改邮箱
  - 验证邮箱格式合法性（RFC 5322）
  - 验证管理员身份（JWT cookie 中的用户名在 ADMIN_USERNAMES 列表中）
  - 依赖：7.1
  - 复杂度：中
  - 验收标准：合法邮箱绑定成功，非法格式返回 INVALID_EMAIL，非管理员返回 403

- [ ] 8.6 实现 `GET /api/email-auth/admin/emails` 端点
  - 查询所有用户的邮箱绑定情况
  - 返回 username、email、bound_at、updated_at
  - 依赖：5.2
  - 复杂度：小
  - 验收标准：返回所有用户邮箱绑定列表，未绑定用户 email 为 null

- [ ] 8.7 实现 `DELETE /api/email-auth/admin/bind-email` 端点
  - 管理员解除用户邮箱绑定
  - 验证管理员身份
  - 依赖：7.1
  - 复杂度：小
  - 验收标准：绑定解除成功，非管理员返回 403

## 9. 邮箱验证服务 — 登录页面与管理界面

- [ ] 9.1 实现登录页面（`GET /api/email-auth/login-page`）
  - 用户名 + 密码表单
  - 验证码输入表单（AJAX 提交）
  - 重新发送验证码按钮（60 秒倒计时）
  - 错误提示展示
  - 移动端适配（安卓平板 Chrome）
  - 依赖：8.1, 8.2, 8.3
  - 复杂度：中
  - 验收标准：页面在安卓平板 Chrome 上正常显示，登录流程完整可用

- [ ] 9.2 实现邮箱管理界面（`GET /api/email-auth/admin/emails-page`）
  - 用户列表展示（username、email、bound_at）
  - 绑定/修改邮箱弹窗
  - 解除邮箱绑定确认
  - 管理员身份验证
  - 依赖：8.5, 8.6, 8.7
  - 复杂度：中
  - 验收标准：管理员可查看、绑定、修改、解除用户邮箱

## 10. 邮箱验证服务 — Nginx 集成与部署

- [ ] 10.1 编写 Nginx 反代配置模板 `deploy/templates/nginx-proxy.conf.template`
  - `location = /auth`（internal，proxy_pass 到 :5001/auth）
  - `location /guacamole/`（auth_request + error_page 401 + proxy_pass 到 :8081）
  - `location /api/email-auth/`（proxy_pass 到 :5001）
  - `location /screenshot/`（proxy_pass 到 :5002）
  - `location = /`（重定向到 /guacamole/）
  - WebSocket 支持（Upgrade/Connection 头、proxy_read_timeout 3600s）
  - 依赖：7.3
  - 复杂度：中
  - 验收标准：Nginx 配置语法正确，auth_request 机制正常工作

- [ ] 10.2 更新 Guacamole docker-compose.yml 端口绑定
  - guacamole 端口从 `8080:8080` 改为 `127.0.0.1:8081:8080`
  - 依赖：3.1
  - 复杂度：小
  - 验收标准：Guacamole 仅监听本地 8081 端口，外部无法直接访问

- [ ] 10.3 编写邮箱验证服务 Docker 镜像构建与部署配置
  - 完善 `deploy/services/email-auth/Dockerfile`
  - 在 `deploy/templates/services-docker-compose.yml` 中添加 email-auth 服务定义
  - 环境变量：DATABASE_URL、GUACAMOLE_URL、SMTP_*、JWT_SECRET、JWT_EXPIRE_DAYS、ADMIN_USERNAMES
  - network_mode: host
  - 依赖：5.3
  - 复杂度：中
  - 验收标准：`docker compose up -d email-auth` 启动成功，服务健康检查通过

- [ ] 10.4 更新 `deploy/config.env.example` 新增邮箱验证服务配置项
  - SMTP_HOST、SMTP_PORT、SMTP_USER、SMTP_PASSWORD、SMTP_FROM
  - JWT_SECRET、ADMIN_USERNAMES
  - HTKIS_DB_PASSWORD
  - 依赖：10.3
  - 复杂度：小
  - 验收标准：配置模板包含所有新增环境变量，带注释说明

- [ ] 10.5 端到端集成测试：邮箱验证登录全流程
  - 无 JWT cookie 访问 /guacamole/ → 重定向到登录页
  - 输入正确用户名密码 → 发送验证码
  - 输入正确验证码 → 登录成功，JWT cookie 设置
  - 再次访问 /guacamole/ → 免验证直接进入
  - JWT 过期后 → 重新触发邮箱验证
  - 未绑定邮箱用户 → 提示联系管理员
  - 依赖：10.1, 10.2, 10.3
  - 复杂度：大
  - 验收标准：所有场景均按设计文档预期工作

---

## 11. 截屏记录 — Windows Server 截屏脚本

- [ ] 11.1 编写 PowerShell 截屏脚本 `deploy/scripts/capture-screen.ps1`
  - 获取当前登录用户名 (`$env:USERNAME`)
  - 构建输出路径：`\\192.168.2.3\screenshots\{username}\{date}\{time}.png`
  - 自动创建目录（如不存在）
  - 使用 .NET System.Drawing 截取主屏幕画面
  - 保存为 PNG 格式
  - 静默执行，不弹窗、不显示任何提示
  - 依赖：3.3（Samba 共享已部署）
  - 复杂度：中
  - 验收标准：脚本执行后截屏文件写入 Samba 共享目录，文件名格式正确

- [ ] 11.2 编写 Windows 计划任务安装脚本 `deploy/scripts/setup-screenshot-task.ps1`
  - 创建计划任务 `HTKIS-Screenshot`：每 10 秒执行截屏脚本，仅在有用户会话时运行
  - 创建计划任务 `HTKIS-Screenshot-Cleanup`：每天 01:00 执行清理脚本
  - 配置：DisallowStartIfOnBatteries=false、StopIfGoingOnBatteries=false、RunOnlyIfNetworkAvailable=true
  - 依赖：11.1
  - 复杂度：中
  - 验收标准：`Get-ScheduledTask -TaskName "HTKIS-Screenshot"` 显示任务已创建且启用

- [ ] 11.3 编写 Windows 端截屏清理脚本 `deploy/scripts/cleanup-screenshots.ps1`
  - 删除 Samba 共享中超过 90 天的截屏文件
  - 清理空目录
  - 依赖：11.1
  - 复杂度：小
  - 验收标准：超过 90 天的截屏文件被删除，空目录被清理

## 12. 截屏记录 — Samba 共享配置

- [ ] 12.1 配置 Samba screenshots 共享
  - 在 `/etc/samba/smb.conf` 新增 `[screenshots]` 段
  - path=/data/screenshots、browseable=no、read only=no、guest ok=yes、force user=debian
  - create mask=0660、directory mask=0770
  - 依赖：3.3
  - 复杂度：小
  - 验收标准：从 Windows Server 可写入 `\\192.168.2.3\screenshots`

- [ ] 12.2 更新 `deploy/templates/smb.conf.template` 新增 screenshots 共享段
  - 依赖：12.1
  - 复杂度：小
  - 验收标准：模板包含 screenshots 共享配置

## 13. 截屏记录 — 截屏管理服务

- [ ] 13.1 搭建截屏管理服务 FastAPI 项目骨架
  - 创建 `deploy/services/screenshot/` 目录结构
  - 编写 `Dockerfile`（基于 python:3.12-slim）
  - 编写 `requirements.txt`（fastapi、uvicorn、sqlalchemy、asyncpg、python-jose、jinja2、aiofiles）
  - 编写 `main.py` 入口
  - 编写 `config.py` 配置管理（DATABASE_URL、SCREENSHOT_DIR、JWT_SECRET、ADMIN_USERNAMES）
  - 依赖：5.2
  - 复杂度：中
  - 验收标准：`docker compose up -d screenshot-service` 启动成功

- [ ] 13.2 实现截屏记录扫描与元数据入库
  - 扫描 `/data/screenshots/` 目录，解析文件路径为 username、date、time
  - 将元数据写入 `screenshot_record` 表（file_path、username、capture_time、session_id、file_size、retain_until）
  - 支持增量扫描（仅处理新增文件）
  - 依赖：13.1
  - 复杂度：中
  - 验收标准：截屏文件元数据正确入库，重复扫描不会产生重复记录

- [ ] 13.3 实现 `GET /screenshot/api/list` 端点
  - 查询参数：username（必填）、date_from（必填）、date_to（必填）、page、page_size
  - 返回截屏记录列表（分页）
  - JWT cookie 管理员认证
  - 依赖：13.2
  - 复杂度：中
  - 验收标准：按用户和日期范围正确返回截屏列表，分页正常

- [ ] 13.4 实现 `GET /screenshot/api/image` 端点
  - 查询参数：path（截屏相对路径）
  - 返回图片文件流（Content-Type: image/png）
  - 路径安全校验（防止目录遍历攻击）
  - JWT cookie 管理员认证
  - 依赖：13.1
  - 复杂度：中
  - 验收标准：管理员可查看截屏图片，非法路径请求被拒绝

- [ ] 13.5 实现 `GET /screenshot/api/stats` 端点
  - 返回截屏统计：total_files、total_size_mb、oldest_date、newest_date、users 列表、disk_usage_percent
  - JWT cookie 管理员认证
  - 依赖：13.2
  - 复杂度：小
  - 验收标准：统计数据准确，磁盘使用率正确计算

- [ ] 13.6 实现截屏管理界面 `GET /screenshot/`
  - Jinja2 渲染 HTML 页面
  - 用户选择下拉框
  - 日期范围选择器
  - 截屏列表展示（缩略图 + 时间）
  - 点击查看大图
  - 管理员身份验证
  - 依赖：13.3, 13.4, 13.5
  - 复杂度：大
  - 验收标准：管理员可通过界面查看任意用户的历史截屏

## 14. 截屏记录 — 清理策略与部署

- [ ] 14.1 编写 Debian 端截屏清理 cron 脚本 `deploy/scripts/cleanup-screenshots.sh`
  - 每天 02:00 执行 `find /data/screenshots -type f -mtime +90 -delete`
  - 清理空目录
  - 记录清理日志
  - 依赖：12.1
  - 复杂度：小
  - 验收标准：超过 90 天的截屏文件被自动删除

- [ ] 14.2 编写截屏管理服务 Docker 镜像构建与部署配置
  - 完善 `deploy/services/screenshot/Dockerfile`
  - 在 `deploy/templates/services-docker-compose.yml` 中添加 screenshot-service 服务定义
  - 环境变量：DATABASE_URL、SCREENSHOT_DIR、JWT_SECRET、ADMIN_USERNAMES
  - 挂载 `/data/screenshots:/data/screenshots:ro`
  - network_mode: host
  - 依赖：13.1
  - 复杂度：中
  - 验收标准：`docker compose up -d screenshot-service` 启动成功

- [ ] 14.3 端到端集成测试：截屏记录全流程
  - Windows Server 计划任务执行截屏 → 文件写入 Samba 共享
  - 截屏管理服务扫描入库 → 管理界面可查看
  - 清理脚本删除超期文件 → 管理界面不再显示
  - 存储空间不足告警 → 提前触发清理
  - 依赖：11.2, 12.1, 13.6, 14.1, 14.2
  - 复杂度：大
  - 验收标准：截屏采集、查看、清理全流程正常

---

## 15. 整体部署与文档更新

- [ ] 15.1 编写完整部署启动脚本
  - 按正确顺序启动所有服务：app-postgres → Guacamole → email-auth → screenshot-service → nginx-proxy → frpc → patch-title
  - 依赖：10.3, 14.2
  - 复杂度：中
  - 验收标准：一键启动所有服务，全链路可用

- [ ] 15.2 更新 `deploy/config.env.example` 新增所有配置项
  - 邮箱验证服务配置（SMTP_*、JWT_SECRET、ADMIN_USERNAMES）
  - 应用层数据库配置（HTKIS_DB_PASSWORD）
  - 迁移配置（OLD_SERVER_HOST、NEW_SERVER_HOST）
  - 依赖：10.4
  - 复杂度：小
  - 验收标准：配置模板完整，新部署实例只需填写 config.env 即可

- [ ] 15.3 更新运维技术文档 `docs/运维技术文档.md`
  - 更新网络拓扑图（新增 Nginx 反代、邮箱验证服务、截屏管理服务）
  - 更新服务清单（新增服务的端口、配置文件、管理命令）
  - 新增邮箱验证服务运维操作（重启、查看日志、SMTP 故障排查）
  - 新增截屏服务运维操作（重启、清理、存储监控）
  - 更新故障排查章节
  - 依赖：10.5, 14.3
  - 复杂度：中
  - 验收标准：文档与实际部署架构一致，运维操作可执行

- [ ] 15.4 全链路冒烟测试
  - 公网访问 ****.htkis.com → 邮箱验证登录 → Guacamole RDP 连接 → 截屏采集 → 管理员查看截屏
  - 验证所有服务日志输出正常（JSON 格式、时间戳、级别、模块名）
  - 验证 frpc 隧道稳定
  - 验证 Samba 文件共享正常
  - 依赖：15.1
  - 复杂度：大
  - 验收标准：全链路端到端功能正常，所有服务运行稳定

---

## 任务统计

| 优先级 | 任务组数 | 子任务数 |
|--------|---------|---------|
| P1 服务器迁移 | 4 | 13 |
| P2 邮箱验证登录 | 6 | 22 |
| P3 截屏记录 | 4 | 12 |
| 整体部署 | 1 | 4 |
| **合计** | **15** | **51** |

## 需求覆盖映射

| 需求编号 | 需求名称 | 覆盖任务 |
|---------|---------|---------|
| 5.1 | 服务器迁移 | 1.1-1.5, 2.1-2.4, 3.1-3.4, 4.1-4.3 |
| 5.2 | 邮箱验证登录 | 5.1-5.3, 6.1-6.4, 7.1-7.4, 8.1-8.7, 9.1-9.2, 10.1-10.5 |
| 5.3 | 截屏记录 | 11.1-11.3, 12.1-12.2, 13.1-13.6, 14.1-14.3 |
| DFX | 性能/可靠性/安全性/可维护性/兼容性 | 贯穿所有任务 |