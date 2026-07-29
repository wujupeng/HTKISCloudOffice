# ERP 云办公平台 — 运维文档

| 项目 | 内容 |
|------|------|
| 文档版本 | v2.0 |
| 编写日期 | 2026-07-29 |
| 状态 | 已发布 |
| 访问地址 | https://erp.oascii.com:8443 |

---

## 1. 日常运维入口

| 操作 | 命令/地址 |
|------|----------|
| SSH 登录主库 | `ssh debian@<MASTER_IP>` (需配置密钥) |
| SSH 登录备机 | `ssh debian@<STANDBY_IP>` (通过跳板) |
| ERP 登录页 | https://erp.oascii.com:8443 |
| ERP Guacamole 管理 | 用 guacadmin 登录后 → 右上角 → Settings |
| 邮箱管理页 | https://erp.oascii.com:8443/api/erp-auth/admin/emails-page (需管理员登录) |

---

## 2. 服务管理

### 2.1 查看服务状态

```bash
# 查看所有 ERP 相关容器
docker ps --filter "name=erp" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"

# 查看 Nginx 容器
docker ps --filter "name=htkis-nginx" --format "table {{.Names}}\t{{.Status}}"

# 查看 guacd
docker ps --filter "name=guacd" --format "table {{.Names}}\t{{.Status}}"

# 查看所有容器（主库或备机）
docker ps
```

### 2.2 启停服务

```bash
# 启动 ERP Guacamole
cd /home/debian/Cloud/erp-guacamole && docker-compose up -d

# 停止 ERP Guacamole
cd /home/debian/Cloud/erp-guacamole && docker-compose down

# 启动 ERP 邮箱验证服务
cd /home/debian/Cloud/services && docker-compose up -d erp-email-auth

# 重启 ERP 邮箱验证服务
cd /home/debian/Cloud/services && docker-compose restart erp-email-auth

# 重建 ERP 邮箱验证服务（代码更新后）
cd /home/debian/Cloud/services && docker-compose build erp-email-auth && docker-compose up -d erp-email-auth

# 重启 Nginx
docker exec htkis-nginx nginx -s reload

# 重启 guacd
docker restart guacd
```

> **注意：备机 192.168.2.88 使用 `docker-compose` 命令（不是 `docker compose`），且需要 `-f` 参数指定文件路径。**

### 2.3 查看日志

```bash
# ERP Guacamole 日志
docker logs erp-guacamole --tail 50 -f

# ERP PostgreSQL 日志
docker logs erp-guac-postgres --tail 50 -f

# ERP 邮箱验证服务日志
docker logs erp-email-auth --tail 50 -f

# Nginx 日志（容器内）
docker exec htkis-nginx tail -50 /var/log/nginx/error.log
docker exec htkis-nginx tail -50 /var/log/nginx/access.log

# guacd 日志
docker logs guacd --tail 50 -f
```

---

## 3. 双机热备运维

### 3.1 查看 Keepalived 状态

```bash
# 查看 Keepalived 服务状态
sudo systemctl status keepalived

# 查看 Keepalived 日志
sudo journalctl -u keepalived --since "10 minutes ago" --no-pager

# 查看 VIP 是否在本机
ip addr show | grep 192.168.2.200
```

### 3.2 手动切换主备

```bash
# 在主库上停止 Keepalived，触发 VIP 迁移到备机
sudo systemctl stop keepalived

# 恢复主库（VIP 会自动回切，因为 priority 更高）
sudo systemctl start keepalived
```

### 3.3 查看流复制状态

```bash
# 在主库上查看复制状态
docker exec erp-guac-postgres psql -U guacamole -d erp_guacamole_db -c "SELECT * FROM pg_stat_replication;"

# 在备库上确认处于恢复模式
docker exec erp-guac-postgres psql -U guacamole -d erp_guacamole_db -c "SELECT pg_is_in_recovery();"
# 应返回 t (true)
```

### 3.4 重建备库流复制

如果备库数据损坏需要重建：

```bash
# 1. 在备机停止 ERP 容器
docker stop erp-guacamole erp-guac-postgres
docker rm erp-guac-postgres
docker volume rm erp-guacamole_erp_pgdata

# 2. 用 pg_basebackup 重新同步
docker run --rm --network host \
  -e PGPASSWORD='****REDACTED****' \
  -v erp-guacamole_erp_pgdata:/var/lib/postgresql/data \
  postgres:15 \
  bash -c "rm -rf /var/lib/postgresql/data/* && \
    pg_basebackup -h 192.168.2.3 -p 5435 -U replicator -D /var/lib/postgresql/data -Fp -Xs -P -R && \
    chown -R 999:999 /var/lib/postgresql/data"

# 3. 重新启动 ERP 容器
cd /home/debian/Cloud/erp-guacamole && docker-compose up -d
```

---

## 4. 用户管理

### 4.1 创建新用户

1. 以 guacadmin 登录 https://erp.oascii.com:8443
2. 右上角用户名 → Settings → Users → New User
3. 填写用户名和密码，保存
4. 新用户首次登录时会自动绑定邮箱并授权 ERP Server 连接

### 4.2 通过 API 创建用户

```bash
# 获取管理员 token
TOKEN=$(curl -s -X POST http://127.0.0.1:8082/guacamole/api/tokens \
  -d "username=guacadmin&password=****" | python3 -c "import sys,json;print(json.load(sys.stdin)['authToken'])")

# 创建用户
curl -X POST http://127.0.0.1:8082/guacamole/api/session/data/postgresql/users \
  -H "Content-Type: application/json" \
  -H "Guacamole-Token: $TOKEN" \
  -d '{"username":"newuser","password":"newpass123","attributes":{}}'
```

### 4.3 管理邮箱绑定

- 访问 https://erp.oascii.com:8443/api/erp-auth/admin/emails-page
- 可查看、修改、删除用户邮箱绑定

### 4.4 Guacamole 管理员

| 用户名 | 密码 | 角色 |
|--------|------|------|
| guacadmin | ****REDACTED**** | 系统管理员 |

---

## 5. SSL 证书管理

### 5.1 查看证书信息

```bash
sudo certbot certificates
```

### 5.2 手动续期

```bash
# DNS-01 方式续期（需要手动添加 TXT 记录）
sudo certbot renew --manual --preferred-challenges dns

# 续期后重启 Nginx
docker exec htkis-nginx nginx -s reload

# 续期后同步证书到备机
scp -r /etc/letsencrypt/live/erp.oascii.com/ debian@192.168.2.88:/etc/letsencrypt/live/erp.oascii.com/
```

### 5.3 自动续期

certbot timer 已启用。**注意：续期后需同步证书到备机并 reload 备机 Nginx。**

---

## 6. 数据库备份与恢复

### 6.1 备份

```bash
# 备份 ERP Guacamole 数据库（在主库执行）
docker exec erp-guac-postgres pg_dump -U guacamole erp_guacamole_db > erp_guacamole_backup_$(date +%Y%m%d).sql
```

### 6.2 恢复

```bash
# 恢复数据库
cat erp_guacamole_backup_YYYYMMDD.sql | docker exec -i erp-guac-postgres psql -U guacamole erp_guacamole_db
```

---

## 7. 防火墙管理

### 7.1 查看规则

```bash
sudo ufw status numbered
```

### 7.2 ERP 相关规则

| 规则 | 端口 | 来源 | 用途 |
|------|------|------|------|
| 8443/tcp | 8443 | Anywhere | ERP HTTPS 公网入口 |
| 4822/tcp | 4822 | 172.16.0.0/12 | guacd (Docker 容器访问) |
| 5435/tcp | 5435 | 192.168.2.88 | PostgreSQL 流复制（主库） |

---

## 8. 故障排查

### 8.1 常见问题

| 现象 | 可能原因 | 排查步骤 |
|------|---------|---------|
| 502 Bad Gateway | ERP Guacamole 容器未运行 | `docker ps --filter name=erp-guacamole` |
| 登录后空白页 | Header 认证未配置 | 检查 guacamole.properties 中 `http-auth-header: Remote-User` |
| RDP 连接超时 | guacd 未运行或 UFW 阻止 | `docker ps --filter name=guacd`; `sudo ufw status \| grep 4822` |
| 验证码发送失败 | SMTP 配置错误 | `docker logs erp-email-auth --tail 20` |
| SSL 证书错误 | 证书过期 | `sudo certbot certificates` |
| 400 Bad Request | HTTP 请求发送到 HTTPS 端口 | 确保使用 https:// 访问 |
| 断开连接不返回登录页 | sub_filter JS 注入失败 | 检查 Nginx 配置中 `proxy_set_header Accept-Encoding ""` |
| VIP 不可达 | Keepalived 未运行 | `sudo systemctl status keepalived`; `ip addr show \| grep 192.168.2.200` |
| 流复制中断 | 备库 PostgreSQL 未运行 | 主库: `SELECT * FROM pg_stat_replication;` |

### 8.2 端到端连通性测试+测试

```bash
# 测试 ERP 邮箱验证服务
curl -s http://127.0.0.1:5003/api/erp-auth/login-page | head -3

# 测试 ERP Guacamole
curl -s -o /dev/null -w "%{http_code}" http://127.0.0.1:8082/guacamole/

# 测试 Nginx HTTPS
curl -sk https://127.0.0.1:8443/api/erp-auth/login-page | head -3

# 测试 VIP 上的 ERP 服务
curl -sk -o /dev/null -w "%{http_code}" https://192.168.2.200:8443/
```

---

## 9. 宝山路由器端口转发

| 外网端口 | 内网 IP | 内网端口 | 协议 | 用途 |
|---------|---------|---------|------|------|
| 8443 | 192.168.2.200 (VIP) | 8443 | TCP | ERP HTTPS |

**⚠️ 路由器管理界面需要现场访问或通过内网登录。**

---

## 10. DNS 记录

| 域名 | 类型 | 值 | 用途 |
|------|------|-----|------|
| erp.oascii.com | A | 210.22.123.254 | 指向宝山路由器 |
| _acme-challenge.erp.oascii.com | TXT | (证书续期时设置) | SSL 证书 DNS 验证 |

DNS 服务商：阿里云 (dns9.hichina.com / dns10.hichina.com)

---

## 11. 配置变更流程

1. 本地修改代码/配置（`C:\Users\DELL\Documents\dev\HTKISCloudOffice\deploy\`）
2. `scp` 上传到 192.168.2.3（主库）
3. 同步到备机 192.168.2.88
4. 重建容器或 reload Nginx（主/备都需操作）
5. 验证功能正常

```bash
# 示例：更新 ERP 邮箱验证服务（主+备）
scp -r deploy/services/erp-email-auth debian@192.168.2.3:/home/debian/Cloud/services/
ssh debian@192.168.2.3 "cd /home/debian/Cloud/services && docker-compose build erp-email-auth && docker-compose up -d erp-email-auth"
# 同步到备机
ssh debian@192.168.2.3 "****REDACTED**** scp -r -o StrictHostKeyChecking=no /home/debian/Cloud/services/erp-email-auth debian@192.168.2.88:/home/debian/Cloud/services/"
ssh debian@192.168.2.3 "****REDACTED**** ssh -o StrictHostKeyChecking=no debian@192.168.2.88 'cd /home/debian/Cloud/services && docker-compose build erp-email-auth && docker-compose up -d erp-email-auth'"

# 示例：更新 Nginx 配置（主+备）
scp deploy/services/nginx/nginx.conf debian@192.168.2.3:/home/debian/Cloud/services/nginx/nginx.conf
ssh debian@192.168.2.3 "docker exec htkis-nginx nginx -t && docker exec htkis-nginx nginx -s reload"
```

---

## 12. 安全注意事项

- `erp_auth_token` Cookie 设置了 `secure=True; httponly=True; samesite=lax`
- 验证码 5 分钟有效，5 次错误后失效
- 验证码 60 秒内不可重发
- 登录速率限制：10次/60秒/IP
- Guacamole 管理员密码已修改为 `guacadmin/****REDACTED****`
- Swagger /docs 端点已禁用
- SSL 证书有效期 90 天，需定期续期并同步到备机
- 宝山路由器仅开放 8443 端口，不暴露内网
- PostgreSQL 5435 端口仅允许备机 IP 访问流复制
- guacd 4822 端口仅允许 Docker 子网访问
