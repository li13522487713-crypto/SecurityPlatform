# AI 数据库（Coze 数据库模块复刻）

## 目标

在资源库中提供与 Coze Studio 对齐的「数据库」资源：两步创建向导、详情页三 Tab（表结构 / 测试数据 / 线上数据）、右上角渠道读写配置 + 渠道隔离 + 权限模式；后端为 SQLite 动态物理表 `draft` / `online` 双表 + JSON 行存储，并与工作流 DB 节点、智能体 Table Memory 绑定共用同一套 API。

## 权威引用

- 契约：`docs/contracts.md`（「AI 数据库补充契约」及 `api/v1/ai-databases` 各端点）
- 后端：`AiDatabasesController`、`AiDatabaseService`、`AiDatabasePhysicalTableService`、`AiDatabaseAccessPolicy`
- 前端：`apps/app-web` 资源库与 `library-create-modal`；`module-studio-react` 的 `database-detail-page.tsx` 与 `copy.ts` 中 `databaseDetail` 词条
- 渠道目录：`Atlas.Infrastructure.Channels.ChannelCatalog`（与 `AiDatabaseChannelCatalog` 同步）

## 里程碑状态（实施摘要）

| 阶段 | 内容 |
|------|------|
| M1 | 实体 `AiDatabase` / `AiDatabaseField` / `AiDatabaseChannelConfig`，`AtlasOrmSchemaCatalog` 注册，物理表服务 draft+online |
| M2 | REST：`fields` 经详情与 PUT 更新、`records` CRUD、bulk、`mode`、`channel-config` |
| M3–M5 | 详情页 Semi UI + i18n（`getStudioCopy`）；渠道配置弹窗链接式开关；隔离/权限 Dropdown |
| M4 | 资源库创建：`SingleUser` + `ChannelIsolated` 默认 |
| M6 | `DatabaseQuery/Insert/Update/Delete` 经 `IAiDatabaseService` + `IsDebug` → `DatabaseEnvironment` |
| M7 | `AiAssistantsController` `database-bindings` + Agent 工作台配置 Tab |
| M8–M9 | `Atlas.Infrastructure.Channels` 项目、微信 NuGet 对齐版本；飞书沿用 `Atlas.Connectors.Feishu` |
| M10 | 本文档 + `AGENTS.md` 专题 + `.http` 示例维护 |

## 配置

- 微信开放平台（可选）：`Atlas:Channels:Weixin` 节，见 `WeixinOpenChannelOptions`。
- 渠道隔离 / 单用户模式依赖请求头 `X-App-Channel` 与当前用户上下文；详见 `AiDatabaseAccessPolicy`。

## 验证

- `dotnet build src/backend/Atlas.AppHost`
- `pnpm run build:app-web` / `pnpm run i18n:check`
- `Bosch.http/AiDatabases.http` 手测创建、改 mode、改 channel-config、读写 records
