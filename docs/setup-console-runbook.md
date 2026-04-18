# 系统初始化与迁移控制台 - 运维手册

> 适用版本：M1-M7（2026-Q2 落地）。
> 控制台路由：`/setup-console`（永久免登录，恢复密钥保护）。
> 后端契约：见 [`docs/contracts.md`](./contracts.md) 第 12 章。

## 1. 总览

`/setup-console` 是 Atlas 系统的"运维总控制台"，承担三类职责：

- **首装（Initial Install）**：裸机部署后第一次完成 6 步系统初始化（precheck → schema → seed → bootstrap-user → default-workspace → complete）；
- **补初始化 / 重新初始化（Repair / Re-init）**：已部署系统出现部分缺失（如版本升级、缺关键表、种子数据 v1 → v2 升级），通过控制台增量补齐；
- **跨库迁移（Cross-Database Migration）**：旧 SQLite → 新 SQLite / 跨引擎 SQLite → MySQL / PostgreSQL / SQL Server 的 ORM 优先迁移，含校验报告与切主。

控制台与平台 JWT 完全解耦，由"恢复密钥（24 字符 base32，首装 BootstrapUser 一次性下发）"或"BootstrapAdmin 凭证"双因子任一通过认证。

## 2. 准备工作

### 2.1 系统依赖

- **后端**：.NET 10 SDK；嵌入式 SQLite（`atlas.db`）；可选 MySQL 8.x / PostgreSQL 14+ / SQL Server 2019+ 用于跨引擎迁移；
- **前端**：Node 22 + pnpm 10；
- **配置文件**：`appsettings.json` + `appsettings.runtime.json`（首装完成后自动持久化数据库连接串）。

### 2.2 端口

| 服务 | 端口 | 备注 |
|---|---|---|
| PlatformHost | 5001 | 控制台 API 入口 |
| AppHost | 5002 | 应用运行时 |
| AppWeb | 5181 | 控制台前端入口（`http://localhost:5181/setup-console`） |

### 2.3 关键配置（`appsettings.json`）

```json
{
  "Security": {
    "BootstrapAdmin": {
      "Enabled": true,
      "TenantId": "00000000-0000-0000-0000-000000000001",
      "Username": "admin",
      "Password": "<replace-in-prod>",
      "Roles": "SuperAdmin,Admin",
      "IsPlatformAdmin": true
    }
  },
  "Database": {
    "ConnectionString": "Data Source=atlas.db",
    "DbType": "Sqlite"
  },
  "DatabaseInitializer": {
    "SkipSchemaInit": false,
    "SkipSeedData": false,
    "SkipSchemaMigrations": false
  }
}
```

`BootstrapAdmin.Password` 必须在生产环境提前替换；它同时作为控制台二次认证的 fallback（恢复密钥丢失时使用）。

## 3. 首装流程（Initial Install）

### 3.1 启动两个服务

```powershell
dotnet run --project src/backend/Atlas.PlatformHost
dotnet run --project src/backend/Atlas.AppHost  # 可选
cd src/frontend
pnpm run dev:app-web
```

`PlatformHost` 启动时检测到 setup 未完成 → `SetupModeMiddleware` 只放行 `/api/v1/setup/*` 与 `/api/v1/setup-console/auth/*`。

### 3.2 浏览器进入控制台

- 访问 `http://localhost:5181/setup-console`
- 用 BootstrapAdmin 凭证登录（恢复密钥首装时尚未生成）
- 进入"Dashboard 总览"看 4 卡（System / Workspace / Migration / Catalog）

### 3.3 顺序执行 6 步

| Step | 操作 | 实际副作用 |
|---|---|---|
| 1. Precheck | 点 Run | 写 `setup_step_record` 一条 succeeded 记录，状态机 → precheck_passed |
| 2. Schema | 点 Run | 真实调用 `AtlasOrmSchemaCatalog.EnsureRuntimeSchema(db)`，建与 `AllRuntimeEntityTypes` 一致的表（当前 211 个实体） |
| 3. Seed | 点 Run | 承认现有 `DatabaseInitializerHostedService` 启动时已写入的种子数据；可选填 bundleVersion 升级 |
| 4. Bootstrap User | 填 admin 用户名/密码/租户 ID + 勾"生成恢复密钥" | 一次性返回 `ATLS-XXXX-XXXX-XXXX-XXXX-XXXX`，**必须立即保存** |
| 5. Default Workspace | 填工作空间名 + Owner 用户名 | 写 `setup_workspace_state` `default` 行 → completed |
| 6. Complete | 点 Run | 系统状态机 → completed；后续所有正常路径开放 |

### 3.4 保存恢复密钥（关键安全步骤）

- 恢复密钥**只显示一次**，明文不落任何日志或 UI 持久化；
- 必须复制到运维密钥库（如 1Password / Vault / Bitwarden）；
- 丢失后的应急路径：`appsettings.json` 中的 BootstrapAdmin 凭证仍可登录控制台，并可通过"重新生成恢复密钥"步骤重新派发；
- 等保 2.0 要求：恢复密钥必须由专人保管，不与 BootstrapAdmin 凭证同处存储。

## 4. 跨库迁移流程（旧库 → 新库）

### 4.1 适用场景

- 同引擎换实例：`atlas.db` → 新主机的 `atlas.db`；
- 跨引擎升级：开发态 SQLite → 生产态 MySQL / PostgreSQL / SQL Server；
- 灾难恢复：从备份库 → 新建库。

### 4.2 准备目标库

跨引擎升级前，确保目标库满足：

| 引擎 | 要求 |
|---|---|
| MySQL 8.x | `utf8mb4` charset；`utf8mb4_unicode_ci` collation；账户具备 `CREATE TABLE / INSERT / ALTER` 权限 |
| PostgreSQL 14+ | UTF-8 编码；账户具备 `CREATE / INSERT` |
| SQL Server 2019+ | 单库账户具备 `db_owner` 或 `db_ddladmin + db_datawriter` |

### 4.3 控制台操作

- 进 `/setup-console/migration`
- **计划（Plan）**：
  - 配置源连接串（默认旧 SQLite `Data Source=atlas.db`）→ 点"测试连接"
  - 配置目标连接串（如 `Server=mysql.prod;Port=3306;Database=atlas;Uid=migrator;Pwd=...`）→ 点"测试连接"
  - 选迁移模式：`structure-plus-data`（最常用）/ `structure-only` / `validate-only`
  - 是否勾"允许重复执行"：仅当源/目标指纹相同的"已 cutover-completed"任务存在时才需要
- 点"新建迁移任务" → 控制台展示 jobId
- **执行（Execute）**：依次点 Precheck → Start → Validate → Cutover
- **进度（Progress）**：点"刷新"轮询；当前实体名 / 批次号 / 已复制行数实时刷新
- **报告（Report）**：cutover 后点"查看校验报告"，确认所有实体行数差为 0

### 4.4 切主与回滚

- **切主**：cutover 成功后，写 `appsettings.runtime.json`：

```json
{
  "Database": {
    "ConnectionString": "Server=mysql.prod;Port=3306;Database=atlas;Uid=app;Pwd=...",
    "DbType": "MySql"
  }
}
```

重启 PlatformHost / AppHost 即可生效。

- **保留源库只读 N 天**：cutover 时 `keepSourceReadonlyForDays` 默认 7；该期间源库不允许写入但可读，便于回滚或对比。

- **回滚**：cutover 之前可点"回滚"（rolled-back）；cutover 之后回滚需手动反向执行一次 `target → source` 的迁移任务（创建新 job + 反向连接）。

### 4.5 防重复机制

| 层级 | 实现 |
|---|---|
| UI | session 内同一 jobId 不重复触发 |
| 任务 | `(SourceFingerprint, TargetFingerprint)` 对相同 + 已 cutover-completed → `MIGRATION_FINGERPRINT_DUPLICATED` |
| 批次 | `(JobId, EntityName)` 唯一，断点续跑从最后 batch + 1 |
| 版本 | `seedBundleVersion v1 / v2` 增量补种；同 version 不重复执行 |

## 5. 工作空间初始化

### 5.1 适用场景

- 首装时控制台 Step 5 已建好 `default` 工作空间；
- 后续新增工作空间时（M8 提供"新建工作空间"入口），需要单独跑 init + seed-bundle + complete 三步；
- 旧工作空间升级 seed bundle（v1 → v2），通过"应用 v1"按钮触发。

### 5.2 控制台操作

- 进 `/setup-console/workspace-init`
- 找到对应工作空间行 → 点"初始化"（如未完成）/ "v1"（应用 seed bundle）/ "完成"
- 状态徽章实时更新到 `workspace_init_completed`

## 6. 灾难恢复

### 6.1 SQLite 数据库损坏

PlatformHost 启动时会自动检测 SQLite 损坏并触发 `AppMigrationService` 的"应急容灾"路径，在 `backups/disaster-recovery/` 下保存损坏 db 的副本，并尝试复用最近备份。

如果自动恢复失败：

1. 关闭 PlatformHost；
2. 从 `backups/atlas.db.<date>.bak` 恢复到工程根的 `atlas.db`；
3. 启动 PlatformHost；
4. 进 `/setup-console`，用 BootstrapAdmin 凭证登录；
5. Dashboard 总览检查"缺失关键表"徽章；如有则点"系统初始化 → Step 2 (Schema)"补建。

### 6.2 恢复密钥丢失

- 用 BootstrapAdmin 凭证登录控制台；
- 进 `/setup-console/system-init`；
- 点 Step 4 "默认管理员" → 勾"同时生成恢复密钥" → 点 Run；
- 一次性下发新恢复密钥，旧密钥失效。

### 6.3 BootstrapAdmin 密码遗忘

- 直接修改 `appsettings.json` 的 `Security.BootstrapAdmin.Password` → 重启 PlatformHost；
- 用新密码登录控制台 → 重新生成恢复密钥；
- 强烈建议：定期轮换 BootstrapAdmin 密码（等保 2.0 要求 90 天）。

## 7. 审计与合规（等保 2.0）

控制台所有写操作均通过 `SetupConsoleAuditWriter` 写入 `AuditRecord`（M7 落地，M10/D5 加固 IP/UA 透传）：

- `actor`: `setup-console:anonymous`（M7 阶段；M8 起接 ConsoleSession 真实身份）
- `action`: `setup-console.{step}` 或 `setup-console.{operation}`
- `target`: `system:{state}` 或 `workspace:{id}` 或 `migration:{jobId}`
- `ipAddress` / `userAgent`：M10/D5 起由 `SetupConsoleAuditEnricherMiddleware` 在 `/api/v1/setup-console/*` 路径上自动注入到 `SetupConsoleAuditContext` Scoped 容器，`SetupConsoleAuthController.Recover` 显式传入；其他控制器调用 `SetupConsoleAuditWriter.WriteAsync` 时若未显式传入，自动从上下文 fallback。
- `result`: `Success` 或 `Failed: {message}`

审计保留期默认 180 天（与 `appsettings.json` 中 `Security.AuditRetentionDays` 一致）。

## 8. 故障排查

| 现象 | 可能原因 | 排查 |
|---|---|---|
| `/setup-console` 401 | 控制台 ConsoleToken 过期（30 分钟） | 重新输入恢复密钥或 BootstrapAdmin 凭证 |
| `MIGRATION_FINGERPRINT_DUPLICATED` | 源/目标连接串与已完成任务一致 | 显式勾"允许重复执行" |
| `cannot start migration from state {state}` | 状态机不允许该转换 | 看 docs/contracts.md 12.2 状态转移矩阵；可能需要先 Precheck 或 Retry |
| 跨引擎建表失败（如 MySQL `LONGTEXT` 不支持索引） | 个别 Entity 写死了 SQLite 类型 | 检查 `[SugarColumn(ColumnDataType=...)]`，去掉硬编码或按目标库重写 |
| Dashboard 总览空白 | 后端未启动或 ConsoleToken 失效 | F12 开发者工具看 `/api/v1/setup-console/overview` 响应 |

## 9. 升级路径

- M1-M4：纯前端骨架（mock）
- M5：后端实体 + Service + Controller + 真接口
- M6：ORM 跨库迁移引擎
- M7：审计 + 文档
- M8：A1 中间件放行 + A2 连接串加密 + A3 密码哈希与 ConsoleToken 持久化；B1 拆种子幂等方法 + B2/B3/B4 真实 upsert 用户/工作空间/实体目录 + B5 首登强制改密
- M9：C1 拓扑排序 + C2 断点续跑 + C3 抽样哈希校验 + C4 跨引擎类型适配 + C5 切主写 appsettings.runtime.json + C6 SQLite→SQLite Integration Test
- M10：D1 Playwright 4 spec 全过 + 截图留痕；D2 SetupConsoleService 6 步 lifecycle 集成测试；D3 token 过期边界；D4 IP 限流（5 次/15 分钟）；D5 审计 IP/UA 自动注入（`SetupConsoleAuditContext` + middleware）；D6 mock/real 切换；D7 平台菜单入口；D8 Repair Tab；D9 实体目录下钻；D10 上生产 checklist 文档
- M11+（计划中）：Hangfire 后台执行长时间迁移；ConsoleSession 真实身份接入审计 actor；多租户并发隔离；跨引擎 SQLite→MySQL Docker Integration Test

## 10. 相关代码

- 前端：[`src/frontend/apps/app-web/src/app/pages/setup-console/`](../src/frontend/apps/app-web/src/app/pages/setup-console/)
- 状态机：[`src/frontend/apps/app-web/src/app/setup-console-state-machine.ts`](../src/frontend/apps/app-web/src/app/setup-console-state-machine.ts)
- 真接口 client：[`src/frontend/apps/app-web/src/services/api-setup-console.ts`](../src/frontend/apps/app-web/src/services/api-setup-console.ts)
- mock 实现：[`src/frontend/apps/app-web/src/services/mock/api-*.mock.ts`](../src/frontend/apps/app-web/src/services/mock/)
- 后端实体：[`src/backend/Atlas.Domain/Setup/Entities/SetupConsoleEntities.cs`](../src/backend/Atlas.Domain/Setup/Entities/SetupConsoleEntities.cs)
- 后端服务：[`src/backend/Atlas.Infrastructure/Services/SetupConsole/`](../src/backend/Atlas.Infrastructure/Services/SetupConsole/)
- 控制器：[`src/backend/Atlas.PlatformHost/Controllers/SetupConsole*.cs`](../src/backend/Atlas.PlatformHost/Controllers/) + [`DataMigrationController.cs`](../src/backend/Atlas.PlatformHost/Controllers/DataMigrationController.cs)
- HTTP 测试：[`src/backend/Atlas.PlatformHost/Bosch.http/SetupConsole.http`](../src/backend/Atlas.PlatformHost/Bosch.http/SetupConsole.http)
- xUnit 测试：[`tests/Atlas.SecurityPlatform.Tests/SetupConsole/`](../tests/Atlas.SecurityPlatform.Tests/SetupConsole/)
- E2E 测试：[`src/frontend/e2e/app/setup-console-*.spec.ts`](../src/frontend/e2e/app/)
