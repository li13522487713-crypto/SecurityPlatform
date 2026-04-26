# 系统初始化与迁移控制台 - 上生产 Checklist

> 适用版本：M1-M10（2026-Q2 生产硬化完成）。
> 与运维手册 [`docs/setup-console-runbook.md`](./setup-console-runbook.md) 配套使用：
> 运维手册讲"怎么操作"，本 checklist 讲"上生产前/中/后必须检查的项"。

## 1. 启动前必检（Pre-Flight）

### 1.1 配置安全

- [ ] `appsettings.json` 中 `Security.BootstrapAdmin.Password` 已替换为强密码（≥ 16 位 + 大小写 + 数字 + 特殊字符）
- [ ] `Security.BootstrapAdmin.TenantId` 与生产 TenantId 一致（默认 `00000000-0000-0000-0000-000000000001`）
- [ ] `Security.SetupConsole.MigrationProtectorKey`（M8/A2）已配置为独立密钥，**不与 BootstrapAdmin 密码相同**
- [ ] `Jwt.SigningKey` ≥ 32 字符，已在密钥库轮换（不要使用配置文件示例值）
- [ ] `Database.Encryption.Enabled = true`（如需 SQLite 静态加密）

### 1.2 数据库目录权限

- [ ] `atlas.db` 所在目录仅 AppHost 服务账号可读写（其它账号禁止）
- [ ] `backups/` 目录已创建，磁盘空间 ≥ 30 天 × 平均每日备份大小 × 2
- [ ] `appsettings.runtime.json`（M5 持久化首装数据库连接 / M9 切主后更新）目录可写

### 1.3 网络与防火墙

- [ ] AppHost 5002 仅运维内网可达（互联网不可直连）
- [ ] `/setup-console` 前端入口由公司 SSO / VPN / 跳板机保护，不暴露公网
- [ ] 控制台所在主机已启用 fail2ban 或同等 IP 限流方案（与 M10/D4 IP 限流叠加）

### 1.4 时区与依赖

- [ ] 服务器时区为 UTC（避免 ConsoleToken / RecoveryKey 过期判定漂移）
- [ ] .NET 10 SDK 已安装；SqlSugar 5.1.4.169 已 NuGet 还原成功
- [ ] 跨引擎迁移目标库（如 MySQL 8 / PostgreSQL 14）已就位且可连接

## 2. 首装期间必做（During First-Install）

### 2.1 控制台二次认证

- [ ] 首次访问 `/setup-console`，用 BootstrapAdmin 凭证登录（恢复密钥此时尚未生成）
- [ ] 控制台 Dashboard 总览正确显示"系统未初始化"

### 2.2 6 步顺序执行

按顺序执行以下 6 步，确认每步状态徽章变 succeeded 后再进入下一步：

- [ ] Step 1 Precheck：连接性预检查通过
- [ ] Step 2 Schema：真实建 290+ 张表（M9 通过 EnsureRuntimeSchema 全量初始化）
- [ ] Step 3 Seed：种子数据应用（roles / menus / dictionaries / model-configs 4 个 bundle，M8/B1）
- [ ] Step 4 BootstrapUser：填生产 admin 用户名/密码（**不要使用 P@ssw0rd!**）+ 勾"生成恢复密钥"
- [ ] Step 5 DefaultWorkspace：填生产工作空间名 + Owner 用户名
- [ ] Step 6 Complete：状态机切到 completed

### 2.3 恢复密钥保存（关键安全步骤）

- [ ] 恢复密钥（24 字符 base32，格式 `ATLS-XXXX-XXXX-XXXX-XXXX-XXXX`）已**完整复制**到运维密钥库（如 1Password / Vault / Bitwarden）
- [ ] 已二次确认密钥可用（通过控制台 logout → 重新用密钥登录验证）
- [ ] 恢复密钥**不与 BootstrapAdmin 密码同处存储**（等保 2.0 双因子隔离要求）
- [ ] 恢复密钥保管人 ≥ 2 人（避免单点丢失）

### 2.4 首次登录改密

- [ ] BootstrapAdmin 首次登录后**立即改密**（PasswordHistory.IsBootstrap 已写标志位，登录后会强制跳改密页）
- [ ] 新密码符合公司密码策略
- [ ] 旧密码（appsettings 中的 BootstrapAdmin.Password）保留作为应急 fallback

## 3. 跨库迁移前必检（Before Migration）

### 3.1 源库准备

- [ ] 源库已完整备份，备份文件 SHA256 已记录
- [ ] 源库已切到只读窗口（`AppDataRoutePolicy.ReadOnlyWindow=true`），停止业务写入
- [ ] 源库待迁移行数已统计（`SELECT COUNT(*) FROM 关键表`）

### 3.2 目标库准备

- [ ] 目标库连接串测试通过（`POST /api/v1/setup-console/migration/test-connection`）
- [ ] 目标库账户具备 `CREATE TABLE / INSERT / ALTER` 权限
- [ ] 目标库 charset / collation 与生产一致（MySQL `utf8mb4_unicode_ci` / PostgreSQL UTF-8）
- [ ] 目标库磁盘空间 ≥ 源库 × 1.5（含索引空间）

### 3.3 变更窗口

- [ ] 已通告业务方，确认变更窗口
- [ ] 监控告警已暂停
- [ ] 回滚预案已准备（保留源库只读 ≥ 7 天）

## 4. 迁移进行中必看（During Migration）

### 4.1 进度监控

- [ ] Dashboard 总览的"活跃迁移任务"卡片实时更新进度
- [ ] 当前实体名 / 批次号 / 已复制行数没有长时间停滞
- [ ] `setup_data_migration_log` 表无 error 级日志（如有立即停止 + 排查）

### 4.2 关键日志检查

PowerShell 监控命令：

```powershell
# 检查最近 1 小时的 error 日志
Get-Content (Get-ChildItem -Path 'logs' -Filter '*.log' | Sort-Object LastWriteTime -Descending | Select-Object -First 1).FullName | Select-String -Pattern "OrmDataMigration.*error" -Context 2,2
```

- [ ] 无 SqlSugar.SqlSugarException
- [ ] 无 Microsoft.Data.Sqlite.SqliteException（非源库正常 read）
- [ ] 无 OrmDataMigrationService.Copy 错误

## 5. 切主与上线（Cutover）

### 5.1 验证阶段

- [ ] 校验报告（`POST /api/v1/setup-console/migration/jobs/{id}/validate`）：
  - [ ] `overallPassed = true`
  - [ ] `passedEntities = totalEntities`
  - [ ] M9/C3 抽样字段哈希校验全部通过（`samplingDiff` 中 `mismatched = 0`）
- [ ] 关键业务表行数差为 0（手动 SQL 复核 `Tenant / UserAccount / Workspace` 等核心表）

### 5.2 切主操作

- [ ] 执行 `POST /api/v1/setup-console/migration/jobs/{id}/cutover`，`keepSourceReadonlyForDays=7`
- [ ] `appsettings.runtime.json`（M9/C5 RuntimeConfigPersistor）已自动更新为新连接串
- [ ] 文件已 git commit / 备份（防止重启时丢失）
- [ ] 重启 AppHost：`Restart-Service Atlas.AppHost` 或对应 systemd / docker
- [ ] 业务页面登录冒烟：登录 / 工作空间 / 智能体列表 / 工作流列表 各点 1 次

### 5.3 切主后 24 小时观察期

- [ ] 监控告警已恢复
- [ ] 错误日志无新增异常
- [ ] 慢查询监控：MySQL `slow_query_log` / PostgreSQL `pg_stat_statements` 对比迁移前
- [ ] 业务 KPI（QPS / 响应时间 / 错误率）回到迁移前基线 ±10%

## 6. 上生产后必做（Post-Production）

### 6.1 审计与监控

- [ ] `setup_console_audit` 审计日志（写入 AuditRecord）已开启滚动（默认 180 天）
- [ ] 控制台所有写操作（schema/seed/bootstrap-user/default-workspace/complete/retry/migration/*）均能查到 actor=`setup-console:anonymous` + IP/UA 记录（M10/D5）
- [ ] `RECOVERY_KEY_RATE_LIMITED` 错误码（M10/D4）触发时已配置告警

### 6.2 备份策略

- [ ] 主库自动备份已开启（`Database.Backup.Enabled=true`，`IntervalHours=24`，`RetentionDays=30`）
- [ ] 备份文件已自动同步到异地（S3 / OSS / NFS 异地复制）
- [ ] 已演练过一次"从备份恢复"流程（运维手册第 6 节）

### 6.3 文档归档

- [ ] 本次首装 / 切主的 `appsettings.runtime.json` 已 commit 到运维仓库
- [ ] 本次操作的恢复密钥已存入密钥库
- [ ] 控制台访问凭证（BootstrapAdmin 密码 + 恢复密钥）已记录到密钥库，并通知运维负责人
- [ ] 本次首装 / 切主的时间、操作人、目标库版本已写运维变更日志

## 7. 灾难恢复演练（每季度一次）

参考运维手册第 6 节。本 checklist 仅记录演练完成项：

- [ ] 已演练 SQLite 数据库损坏恢复（自动 + 手动 fallback）
- [ ] 已演练恢复密钥丢失场景（用 BootstrapAdmin 凭证重派发）
- [ ] 已演练 BootstrapAdmin 密码遗忘场景（修改 `appsettings.json` + 重启）
- [ ] 已演练跨库迁移失败回滚（rollback 任务 + 反向迁移）

## 8. 变更管理

每次执行控制台操作均需在变更日志记录：

- 时间（UTC + 本地时区）
- 操作人
- 操作类型（首装 / 补初始化 / 跨库迁移 / 恢复 / 重置）
- 关联 jobId / stepRecord
- 影响范围（仅控制面 / 仅工作空间 / 全平台）
- 回滚预案
- 验证结论

本 checklist 与 [`docs/setup-console-runbook.md`](./setup-console-runbook.md) 共同构成系统初始化控制台的运维基线，新人上岗前必读。
