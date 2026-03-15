# 后端与前端命名整改清单（SEC-23）

> 版本：v1.0-draft  
> 产出日期：2026-03-16  
> 任务来源：Linear `SEC-23`（`[P0/SEC-9] 形成后端与前端命名整改清单`）  
> 输入基线：`docs/analysis/code-object-inventory.md`、`docs/analysis/unified-terminology-glossary-v1.md`、`docs/contracts.md`

## 1. 目标与范围

- 本清单用于给后续代码改造卡提供直接输入：后端、前端、接口契约三侧的命名整改项。
- 本清单只定义“改什么、何时改、风险在哪、先后顺序”，不在本卡直接执行大规模重命名。
- 分类统一为三类：
  - **立刻改**：当前冲突高、能显著降低语义歧义，且可控。
  - **兼容保留**：短期不能硬切，需要版本并行或别名兼容。
  - **最后清理**：依赖前两类稳定后再做清扫。

## 2. 后端整改清单

### 2.1 实体 / 聚合 / DTO 命名整改

| 对象（现状） | 目标命名 | 现状位置（示例） | 分类 | 风险等级 | 影响面 | 处理说明 |
|---|---|---|---|---|---|---|
| `LowCodeApp`（承载租户开通实例语义） | `TenantAppInstance`（或经评审确定的等价名） | Domain LowCode 实体与 DTO | 立刻改 | 高 | 实体、DTO、服务、映射、API 响应 | 与词汇表对齐，消除“LowCode=应用本体”误导；先新增目标 DTO，再迁移读路径。 |
| `AppManifest`（平台目录/发布元数据） | `ApplicationCatalog` + `ApplicationRelease`（对象职责拆分） | Domain Platform + Controller DTO | 兼容保留 | 高 | 平台应用管理接口、发布链路 | 先保留 `AppManifest` 兼容字段，v2 新增目标名；旧名标记 Deprecated。 |
| `TenantDataSource` 中 `AppId` 语义不清 | `TenantApplicationId`（关系显式） | Domain System 实体 | 立刻改 | 高 | 数据源绑定、解析链路、迁移脚本 | 优先改 DTO/接口字段名，再做实体字段兼容映射，避免一次性破坏存量数据。 |
| `Project`（组织域）与创作域“项目”同名 | 组织域保留 `Project`；创作域统一改 `ProjectAsset` | Domain Identity + 后续创作域模型 | 兼容保留 | 高 | 权限、菜单、数据范围、请求头 | 组织域不动；新建创作域模型时禁止再复用 `Project` 裸词。 |
| `RuntimeRoute` / 运行执行对象混用 `Runtime` | `RuntimeContext`（上下文）+ `RuntimeExecution`（执行实例） | Platform/Runtime 相关模型 | 兼容保留 | 高 | 运行 API、审计、监控 | 先在 DTO 层拆词，再回落到实体层，避免监控指标维度变化过大。 |

### 2.2 Controller / 路由命名整改

| 现状 Controller / 路由 | 目标方向 | 分类 | 风险等级 | 高风险说明 |
|---|---|---|---|---|
| `LowCodeAppsController` `/api/v1/lowcode-apps`（已 Obsolete） | 迁移至 `/api/v2/tenant-app-instances`（命名示例） | 立刻改 | 高 | 前端主链路仍在调用旧路由；改名将影响所有应用工作台页面。 |
| `AppManifestsController` `/api/v1/app-manifests` | 演进至 `/api/v2/application-catalogs` | 兼容保留 | 高 | 影响平台控制台、发布接口、OpenAPI 文档、HTTP 用例。 |
| `AppsController` `/api/v1/apps`（实际为 AppConfig） | 重命名为 `/api/v1/application-configs`（或拆分到租户应用上下文） | 立刻改 | 中 | “apps”与应用实例/目录冲突最严重，需优先止血。 |
| `PageRuntimeController` `/api/v1/runtime/*` | 细分为 `/api/v1/runtime-contexts/*` 与 `/api/v1/runtime-executions/*` | 兼容保留 | 高 | 影响运行页加载、运行提交、审计日志归类。 |
| `AiWorkspacesController` 与应用 workspace 子路由同名 | AI 侧固定 `ai-workspaces`，应用侧改 `studios` 或 `design-workspaces` | 立刻改 | 中 | 同名 workspace 导致运维与日志定位困难。 |

### 2.3 后端“最后清理”项

| 清理项 | 前置条件 | 分类 |
|---|---|---|
| 删除 `LowCodeAppsController` 及其 DTO | 前端与 API 客户端 100% 切到新路由，旧路由 6 个月弃用期结束 | 最后清理 |
| 删除 `AppManifest` 旧 DTO 字段别名 | `contracts`、前端类型、第三方调用均完成新名切换 | 最后清理 |
| 清理旧版 `runtime` 泛化命名方法 | 新监控指标与审计报表已按 Context/Execution 分层 | 最后清理 |

## 3. 前端整改清单

### 3.1 页面与路由命名整改

| 现状页面/路由 | 目标命名 | 分类 | 风险等级 | 影响面 |
|---|---|---|---|---|
| `/apps/:appId/*` + `app-workspace-*` 路由名 | `/tenant-apps/:tenantAppId/*` + `tenant-app-studio-*` | 立刻改 | 高 | 菜单、面包屑、收藏、权限点、埋点键名 |
| `/ai/workspace` | `/ai/workspaces`（资源复数） | 立刻改 | 中 | 侧边导航、收藏链接、跳转守卫 |
| `/r/:appKey/:pageKey` | `/runtime/:tenantAppKey/pages/:pageKey`（示例） | 兼容保留 | 高 | 外部分享链接、运行入口、网关转发 |
| `ConsolePage` 中“应用”卡片语义混用 | 区分“应用目录项”与“租户应用实例” | 立刻改 | 中 | 首页入口文案、卡片标签、筛选项 |
| `ProjectsPage`（组织域） | 保留；创作域页面统一 `ProjectAssetsPage` | 兼容保留 | 中 | 菜单树、权限码、国际化词条 |

### 3.2 前端类型与 API 客户端整改

| 现状类型/客户端 | 目标命名 | 分类 | 风险等级 | 处理说明 |
|---|---|---|---|---|
| `types/lowcode.ts` (`LowCodeApp*`) | 新增 `types/tenant-app.ts` (`TenantAppInstance*`) | 立刻改 | 高 | 先并行类型文件，页面逐步迁移，最后删除 `lowcode` 命名。 |
| `services/lowcode.ts` | `services/api-tenant-apps.ts` + `services/api-application-catalogs.ts` | 立刻改 | 高 | 按资源边界拆客户端，避免单文件混合 runtime/manifest/lowcode。 |
| runtime 相关类型泛用 `Runtime*` | 拆为 `RuntimeContext*` 与 `RuntimeExecution*` | 兼容保留 | 中 | 与后端 v2 DTO 对齐后统一替换。 |
| workspace 相关类型混用 | AI 保留 `AiWorkspace*`，应用设计改 `StudioWorkspace*` | 立刻改 | 中 | 避免在 store 与路由 meta 中出现裸 `workspace`。 |

### 3.3 前端“最后清理”项

| 清理项 | 前置条件 | 分类 |
|---|---|---|
| 删除 `/apps/*` 旧路由与跳转别名 | 新路由稳定运行并完成外链迁移 | 最后清理 |
| 删除 `lowcode.ts` 旧服务与类型导出 | 所有页面不再引用旧接口命名 | 最后清理 |
| 清理菜单中“工作台/工作区/空间”混用文案 | UX 与 IA 词汇表发布并生效 | 最后清理 |

## 4. 接口契约（contracts）对齐清单

### 4.1 立刻改

| contracts 项 | 现状问题 | 目标动作 | 风险等级 |
|---|---|---|---|
| `LowCodeApp*` 契约块 | 与目标词汇 `TenantAppInstance` 不一致 | 新增 `TenantAppInstance*` 契约并标注 `LowCodeApp*` Deprecated | 高 |
| `AppManifest*` 与 `AppRelease*` | “应用目录”与“租户实例”边界不够清晰 | 增补 `ApplicationCatalog*` 定义及关系图 | 高 |
| `DataSourceId/AppId` 字段说明 | 归属层级不明确 | 改为 `tenantApplicationId` 语义描述并补绑定规则 | 高 |

### 4.2 兼容保留

| contracts 项 | 兼容策略 | 风险等级 |
|---|---|---|
| 旧 API 路径 `/api/v1/lowcode-apps` | 在 contracts 标注 Deprecated + 弃用窗口（>= 6 个月） | 高 |
| `/api/v1/runtime/*` | 增加 v2 草案路径并给出映射表 | 高 |
| `workspace` 术语 | 文档中加“AI Workspace / Studio Workspace”术语限定 | 中 |

### 4.3 最后清理

| contracts 清理项 | 前置条件 |
|---|---|
| 删除 `LowCodeApp*` 旧模型定义 | 前后端不再产出或消费相关字段 |
| 删除 v1 弃用途径示例 | 弃用窗口结束且发布公告完成 |
| 删除裸词 `Application/DataSource/Runtime/Workflow` | 全文完成限定名替换 |

## 5. 高风险项总表（影响 contracts、API、菜单、路由）

| 风险项 | 影响维度 | 风险描述 | 缓解措施 |
|---|---|---|---|
| `/lowcode-apps` → `/tenant-app-instances` 路由迁移 | API / 前端路由 / 菜单 / contracts | 改动面最广，易产生 404 与权限码错配 | 先双写路由与客户端适配层，再分批切页面。 |
| `AppManifest` 语义拆分 | API / contracts / 文档 | 目录定义与租户实例混淆会导致错误建模持续扩散 | 先在 contracts 固化关系图，再落 API v2。 |
| `runtime` 拆分为 context/execution | API / 监控 / 审计 / 前端运行页 | 指标、日志字段可能断裂 | 做字段映射表与指标回填，保留兼容查询。 |
| `workspace` 术语拆分 | 菜单 / 路由 / 页面文案 / 埋点 | 同名不同义导致导航理解和权限申请混乱 | 文案层先加限定词，再改路由名。 |
| `Project` 双语义 | contracts / 权限 / 数据范围 | 组织项目与创作项目混淆，易误授权 | 短期命名加前缀（OrgProject/ProjectAsset）。 |

## 6. 建议改造顺序（可直接拆研发卡）

1. **Step 1（文档先行）**：更新 `docs/contracts.md` 的术语限定、Deprecated 标记、v1/v2 映射表。  
2. **Step 2（后端先做兼容层）**：新增 v2 DTO 与路由（不删 v1），完成 `LowCodeApp -> TenantAppInstance`、`runtime -> context/execution` 映射。  
3. **Step 3（前端切主链路）**：先改 API 客户端与类型，再改页面路由与菜单文案，确保主流程全走新命名。  
4. **Step 4（联调与验收）**：补齐 `.http` 示例、前端回归清单、网关与权限码检查。  
5. **Step 5（弃用窗口管理）**：发布 Deprecated 公告并跟踪调用量。  
6. **Step 6（最后清理）**：移除旧路由、旧 DTO、旧类型导出与旧文档段落。

## 7. 后续子任务建议（对应 SEC-37 / SEC-38）

### 7.1 SEC-37（后端实体与接口）建议拆分

- `SEC-37-A`：`LowCodeApp` 命名与 DTO 兼容迁移。
- `SEC-37-B`：`AppManifest` → `ApplicationCatalog` 契约与 API v2。
- `SEC-37-C`：runtime 命名分层改造（Context/Execution）。
- `SEC-37-D`：workspace 术语去歧义（AI vs Studio）。

### 7.2 SEC-38（前端路由类型与 contracts）建议拆分

- `SEC-38-A`：`services/lowcode.ts` 客户端拆分与类型并行迁移。
- `SEC-38-B`：`/apps/*` 路由重命名与菜单映射。
- `SEC-38-C`：`contracts` 术语限定与 Deprecated 清单落盘。
- `SEC-38-D`：弃用窗口结束后的前端清理卡。
