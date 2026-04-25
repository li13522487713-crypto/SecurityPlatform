# SQLite AI 数据库管理中心浏览器 E2E 测试报告

测试时间：2026-04-25 21:33-21:50（Asia/Shanghai）

## 测试环境

- 仓库：`D:\Code\Web_SaaS_Backend\SecurityPlatform`
- 后端启动命令：`dotnet run --project src/backend/Atlas.AppHost --urls http://localhost:5002`
- 前端启动命令：`cd src/frontend && pnpm run dev:app-web`
- Codex 内置浏览器 URL：`http://localhost:5181/space/1496769226340306944/library?tab=database`
- API Base URL：`http://localhost:5002/api/v1`
- 登录账号：`admin`
- 登录密码：`***`
- Tenant：`00000000-0000-0000-0000-000000000001`
- SQLite Root：`data/ai-db/e2e`
- 托管配置名称：`E2E_SQLITE_PROFILE_20260425_214514`
- AI 数据库名称：`E2E_SQLITE_DB_20260425_214514`
- Draft SQLite 文件路径：未创建，Blocked
- Online SQLite 文件路径：未创建，Blocked

## 前置验证

- 后端 build：第一次因已有 `Atlas.AppHost` 进程锁定 DLL 失败；停止 PID `29500` 后重跑通过，0 warning / 0 error。
- 前端 build：`pnpm run build:app-web` 通过。
- 前端 lint：`pnpm run lint` 通过。
- i18n：`pnpm run i18n:check` 通过，zh/en missing=0。
- 后端启动：`5002` 正常监听，`/swagger` 可访问；`/api/v1/health*` 当前返回 404，不是有效健康检查入口。
- 前端启动：`5181` 正常监听。

## 浏览器执行结果

已使用 Codex 内置浏览器完成：

- 打开前端页面。
- 通过 UI 退出并重新登录。
- 通过左侧菜单进入资源库。
- 点击数据库 Tab 进入数据库管理中心。
- 打开托管配置管理 SideSheet。
- 在 UI 中填写 SQLite 托管配置并点击保存。

在“创建 SQLite 托管配置”处出现阻断失败：

1. 浏览器 UI 点击保存后返回 `One or more validation errors occurred.`
2. 后端响应 400：`$.provisionMode` 无法从字符串 `"SQLiteFile"` 绑定到枚举。
3. 使用 API 辅助 workaround 改为数字枚举 `0` 后仍失败。
4. 后端 500：`AiDatabaseHostProfile.Port` 和 `LastTestAt` 被当前 SQLite 元库错误建为 NOT NULL，导致插入失败。

因此，后续依赖 `profileId` / `databaseId` / `sourceId` 的流程全部标记为 Blocked，包括：

- 测试 SQLite 托管配置。
- 创建 SQLite AI 数据库。
- Draft / Online 实例验证。
- 数据源列表选择。
- Schema 树。
- ER 图。
- 可视化建表。
- SQL 建表。
- SQL 格式化。
- DDL / 数据预览。
- 创建视图。
- 危险 SQL 拒绝。
- 删除视图/表二次确认。

## 截图证据

截图目录：`docs/test-reports/sqlite-ai-database-e2e-screenshots/`

- `01-open-home.png`
- `02-login-success.png`
- `03-database-center.png`
- `04-host-profile-list.png`
- `05-create-sqlite-profile.png`
- `05-create-sqlite-profile-failed.png`

说明：Codex 内置浏览器的 `Page.captureScreenshot` 和 CUA visible screenshot 均出现超时；浏览器 DOM 和交互由 Codex 内置浏览器完成，截图文件使用系统可见屏幕捕获作为 workaround。

## 控制台与网络错误

控制台错误：

- `CustomError: space id error`（历史错误，当前页面仍可渲染）
- `Warning: Invalid prop total of type string supplied to Pagination`

网络/接口错误：

- `POST /api/v1/ai-database-host-profiles` 400：`$.provisionMode` 无法绑定 enum。
- `POST /api/v1/ai-database-host-profiles` 500：`NOT NULL constraint failed: AiDatabaseHostProfile.Port`
- `POST /api/v1/ai-database-host-profiles` 500：`NOT NULL constraint failed: AiDatabaseHostProfile.LastTestAt`

## 数据库文件检查

未创建 AI 数据库，因此：

- Draft SQLite 文件：不存在，Blocked。
- Online SQLite 文件：不存在，Blocked。
- 未执行表/视图创建。
- 未执行删除。

## ID Number 化检查

扫描范围：

- `src/frontend/apps/app-web/src/app/pages/database-center`
- `src/frontend/apps/app-web/src/services/api-database-center.ts`
- `src/frontend/apps/app-web/src/services/api-ai-database-host-profiles.ts`
- `src/frontend/apps/app-web/src/services/api-database-structure.ts`
- `src/frontend/apps/app-web/src/services/api-ai-database.ts`

结论：

- 未发现 `databaseId/profileId/instanceId/sourceId/resourceId` 直接 `Number()` / `parseInt()` / `+id`。
- 命中项为旧记录 `recordId/fileId/taskId`、对象数量 `count` 和 SQL limit，不属于本次要求的资源 ID。

## 缺陷清单

1. `[Blocker]` UI 创建 SQLite 托管配置失败：前端发送字符串 `provisionMode`，后端 enum 绑定失败。
2. `[Blocker]` `AiDatabaseHostProfile` 当前 SQLite 表结构漂移，nullable 字段被建为 NOT NULL，API workaround 也无法创建 profile。
3. `[Major]` Codex 内置浏览器截图接口超时，只能用系统可见屏幕捕获 workaround。
4. `[Major]` 数据库中心关键控件缺少稳定 `data-testid`，浏览器 E2E 定位脆弱。
5. `[Minor]` 控制台存在历史 `space id error` 和 Pagination total string warning。

## 清理结果

未成功创建任何本轮 `E2E_SQLITE_*` profile、AI 数据库、表或视图。

清理动作：未执行删除，避免误删非 E2E 数据。

## 最终结论

- 总步骤数：46
- Pass：10
- Fail：2
- Blocked：34
- 主要缺陷：SQLite 托管配置创建链路不可用，且 HostProfile SQLite 元表结构漂移。
- 是否建议上线：否
- 需要修复的问题：修复前端 `provisionMode` enum 序列化或后端支持字符串 enum；修复 `AiDatabaseHostProfile` SQLite schema 自愈/迁移；补充稳定 `data-testid`；修复控制台 warning。
- 本次是否完成清理：是。没有创建成功的测试数据，无需删除。
