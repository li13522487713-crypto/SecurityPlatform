# 代码主对象命名与用途盘点（SEC-30）

> 版本：v0.1  
> 产出日期：2026-03-16  
> 任务来源：Linear `SEC-30`  
> 目标边界：仅盘点后端与前端现状，不做目标模型裁决与重命名决策。

## 1. 代码对象清单

| 层 | 对象名 | 文件位置 | 当前职责一句话 | 上下文层级 |
|---|---|---|---|---|
| 后端 Domain | `Tenant` | `src/backend/Atlas.Domain/System/Entities/Tenant.cs` | 平台租户主实体，承载租户状态、管理员绑定与启停。 | 租户 |
| 后端 Domain | `TenantDataSource` | `src/backend/Atlas.Domain/System/Entities/TenantDataSource.cs` | 租户数据源配置实体，可选绑定 `AppId`，记录连接测试结果。 | 租户 / 应用 |
| 后端 Domain | `Project` | `src/backend/Atlas.Domain/Identity/Entities/Project.cs` | 租户下项目域隔离对象，承载项目编码、状态和排序。 | 应用 / 项目 |
| 后端 Domain | `LowCodeApp` | `src/backend/Atlas.Domain/LowCode/Entities/LowCodeApp.cs` | 低代码应用聚合根，承载 `AppKey`、数据源绑定、共享策略与发布状态。 | 应用 / 工作台 / 发布 |
| 后端 Domain | `LowCodeAppVersion` | `src/backend/Atlas.Domain/LowCode/Entities/LowCodeAppVersion.cs` | 低代码应用发布/回滚快照。 | 发布 |
| 后端 Domain | `AiWorkspace` | `src/backend/Atlas.Domain/AiPlatform/Entities/AiWorkspace.cs` | 用户级 AI 工作台偏好对象（主题、最近访问、收藏）。 | 工作台 |
| 后端 Domain | `AiApp` | `src/backend/Atlas.Domain/AiPlatform/Entities/AiApp.cs` | AI 应用实体，绑定 Agent/Workflow/Prompt 模板并支持发布版本递增。 | 应用 / 发布 |
| 后端 Domain | `AppManifest` | `src/backend/Atlas.Domain/Platform/Entities/ProductizationEntities.cs` | 平台产品化应用元数据根对象，承载版本、状态、配置与数据源绑定。 | 应用 / 发布 |
| 后端 Domain | `AppRelease` | `src/backend/Atlas.Domain/Platform/Entities/ProductizationEntities.cs` | `AppManifest` 的发布快照/回滚点对象。 | 发布 |
| 后端 Domain | `RuntimeRoute` | `src/backend/Atlas.Domain/Platform/Entities/ProductizationEntities.cs` | 运行态路由映射对象，维护 `appKey + pageKey` 与发布版本绑定。 | 运行 |
| 后端 WebApi | `TenantsController` (`/api/v1/tenants`) | `src/backend/Atlas.WebApi/Controllers/TenantsController.cs` | 租户 CRUD 与状态切换入口。 | 平台 / 租户 |
| 后端 WebApi | `ProjectsController` (`/api/v1/projects`) | `src/backend/Atlas.WebApi/Controllers/ProjectsController.cs` | 项目 CRUD、我的项目，以及用户/部门/职位分配接口。 | 租户 / 项目 |
| 后端 WebApi | `AppsController` (`/api/v1/apps`) | `src/backend/Atlas.WebApi/Controllers/AppsController.cs` | 应用配置对象（`AppConfig`）查询与更新入口。 | 应用 |
| 后端 WebApi | `LowCodeAppsController` (`/api/v1/lowcode-apps`) | `src/backend/Atlas.WebApi/Controllers/LowCodeAppsController.cs` | 低代码应用及页面/环境等管理入口（已标注 `Obsolete`）。 | 应用 / 工作台 / 运行 / 发布 |
| 后端 WebApi | `AppManifestsController` (`/api/v1/app-manifests`) | `src/backend/Atlas.WebApi/Controllers/AppManifestsController.cs` | 新的应用元数据与发布管理入口，并暴露 workspace 子资源。 | 应用 / 工作台 / 发布 |
| 后端 WebApi | `PageRuntimeController` (`/api/v1/runtime`) | `src/backend/Atlas.WebApi/Controllers/PageRuntimeController.cs` | 按 `appKey + pageKey` 提供运行态 schema 与运行写入。 | 运行 |
| 后端 WebApi | `AiAppsController` (`/api/v1/ai-apps`) | `src/backend/Atlas.WebApi/Controllers/AiAppsController.cs` | AI 应用 CRUD、发布、版本检查与资源复制任务入口。 | 应用 / 发布 |
| 后端 WebApi | `AiWorkspacesController` (`/api/v1/ai-workspaces`) | `src/backend/Atlas.WebApi/Controllers/AiWorkspacesController.cs` | 当前用户 AI 工作台配置与资源库检索入口。 | 工作台 |
| 前端 Route | `/apps/:appId/*`（命名含 `app-workspace-*`） | `src/frontend/Atlas.WebApp/src/router/index.ts` | 将“应用”与“工作台”组合为路由主语义。 | 应用 / 工作台 |
| 前端 Route | `/r/:appKey/:pageKey`（`runtime-delivery-page`） | `src/frontend/Atlas.WebApp/src/router/index.ts` | 运行交付路由，按 `appKey/pageKey` 渲染页面。 | 运行 |
| 前端 Route | `/ai/workspace` | `src/frontend/Atlas.WebApp/src/router/index.ts` | AI 工作台固定入口。 | 工作台 |
| 前端 Route | `/settings/projects` | `src/frontend/Atlas.WebApp/src/router/index.ts` | 项目管理页面入口。 | 项目 |
| 前端 View | `ConsolePage` | `src/frontend/Atlas.WebApp/src/pages/console/ConsolePage.vue` | 将 `App` 作为卡片化资产列表展示，并跳转 `/apps/{id}`。 | 平台 / 应用 |
| 前端 View | `AiWorkspacePage` | `src/frontend/Atlas.WebApp/src/pages/ai/AiWorkspacePage.vue` | 工作台偏好管理 + 常用资源列表。 | 工作台 |
| 前端 View | `ProjectsPage` | `src/frontend/Atlas.WebApp/src/pages/system/ProjectsPage.vue` | 项目对象的增删改查与成员分配页面。 | 项目 |
| 前端 View | `PageRuntimeRenderer` | `src/frontend/Atlas.WebApp/src/pages/runtime/PageRuntimeRenderer.vue` | 按应用与页面键加载运行态 schema，并将表单提交映射到 runtime API。 | 运行 |
| 前端 Type | `LowCodeApp*` / `LowCodePage*` / `LowCodeEnvironment*` | `src/frontend/Atlas.WebApp/src/types/lowcode.ts` | 低代码应用、页面、环境、版本等核心前端类型集合。 | 应用 / 工作台 / 运行 / 发布 |
| 前端 Service | `lowcode.ts` API 客户端 | `src/frontend/Atlas.WebApp/src/services/lowcode.ts` | 仍以 `/lowcode-apps` 为主接口，同时混入 `/runtime` 调用。 | 应用 / 运行 |
| 前端 Service | `api-ai-workspace.ts` API 客户端 | `src/frontend/Atlas.WebApp/src/services/api-ai-workspace.ts` | AI 工作台对象（current/library）请求封装。 | 工作台 |

## 2. 代码侧冲突表

| 对象名 | 冲突类型 | 涉及文件 | 问题说明 | 后续归类 |
|---|---|---|---|---|
| `App` | 同名异义 | `AppsController`、`LowCodeApp`、`AppManifest`、前端 `/apps/:appId/*` | 代码里至少存在 `AppConfig`（配置对象）、`LowCodeApp`（低代码资产）、`AiApp`（AI 资产）、`AppManifest`（产品化元数据）四套主对象，前端路由统一叫 `apps`，语义已重叠。 | 必须收敛 |
| `Workspace` | 同名异义 | `AiWorkspace`、`AppManifestsController` 的 `workspace/*` 路径、前端 `app-workspace-*` 路由命名 | `Workspace` 同时表示 AI 个人工作台、应用设计工作区、manifest 的子资源视图，不是同一个生命周期对象。 | 必须收敛 |
| `Runtime` | 语义过载 | `RuntimeRoute`、`PageRuntimeController`、前端 `runtime-delivery-page` 与 `PageRuntimeRenderer` | `Runtime` 既被用作路由发布映射对象，也被用作页面运行时渲染与写入 API 名称，执行态与映射态未做命名区分。 | 必须收敛 |
| `Release/Version` | 重复建模 | `LowCodeAppVersion`、`AppRelease`、`AiApp.PublishVersion` | 发布/版本在低代码、平台产品化、AI 三条线各自建模，字段命名（`Version`/`PublishVersion`）和生命周期动作（publish/rollback）分散。 | 保留观察（短期） |
| `Project` | 生命周期不清 | `Project` 实体、`ProjectsController`、前端 `ProjectsPage`、`api-core` `X-Project-Id` | `Project` 在身份域中是租户内隔离对象，但请求上下文中又作为应用级 scope header，跨层含义接近“访问上下文”而非纯业务实体。 | 必须收敛 |
| `DataSource` | 语义过载 | `TenantDataSource`、`LowCodeApp.DataSourceId`、`LowCodeAppsController` datasource 子路由 | 数据源既是租户基础设施对象，又被 `LowCodeApp` 直接引用为应用绑定字段，缺少“租户开通关系对象”承接。 | 必须收敛 |
| `lowcode-apps` vs `app-manifests` | 重复建模 | `LowCodeAppsController`（已弃用）与 `AppManifestsController`（新）并行，前端仍走 `lowcode.ts` | 后端新旧资源并行，且前端主链路仍访问已标记弃用资源，命名和语义迁移未完成。 | 必须收敛 |

## 3. 文档与代码差异表

| 对象名 | 文档侧定义摘要 | 代码侧实际职责 | 差异说明 | 影响范围 |
|---|---|---|---|---|
| `Tenant-App / TenantApplication` | 文档定义为“租户开通应用后的订阅关系对象”，承载状态与数据源。 | 代码未见独立 `TenantApplication` 实体/控制器；当前由 `LowCodeApp + TenantDataSource(+AppId)` 组合承接。 | 文档是显式对象，代码是隐式组合关系。 | 租户开通、状态流转、数据源归属 |
| `DataSource` | 文档强调 Tenant-App 维度绑定与可用性校验。 | `TenantDataSource` 允许 `AppId` 为空（平台级）或有值（应用级），并由 lowcode app 直接引用 `DataSourceId`。 | 文档中数据源绑定主体是 Tenant-App；代码中是 Tenant 或 LowCodeApp。 | 数据隔离、连接解析、故障定位 |
| `AppManifest` / `LowCodeApp` | 文档（contracts）已给出 `AppManifest/AppRelease/RuntimeRoute` 主线，同时仍保留 `LowCodeApp` 大量接口。 | 后端存在两套路由；`LowCodeAppsController` 明确 `Obsolete`，但前端服务仍主要调用 `/lowcode-apps`。 | 文档与后端迁移方向一致，但前端实现仍偏旧模型。 | API 契约一致性、前后端演进节奏 |
| `Workspace` | 文档 `contracts` 将 Workspace 明确到 AI 工作台接口。 | 代码中除 `AiWorkspace` 外，`app-manifests/{id}/workspace/*` 与前端 `app-workspace-*` 也在使用 workspace 命名。 | 文档定义偏 AI 工作台；代码已扩展到应用设计上下文。 | 路由命名、导航信息架构 |
| `Runtime` | 文档把 `RuntimeRoute` 定义为发布态映射。 | 代码里 runtime 同时覆盖：发布映射实体、运行态 schema 读取、记录写入接口与前端运行页。 | 文档定义较“静态映射”，代码实现是“映射 + 执行入口”混合。 | 运行态 API 边界、审计与监控归类 |
| `Project` | 文档强调项目模式用于应用内数据范围隔离（header: `X-Project-Id`）。 | 代码有独立 `Project` 管理域与权限分配，同时 API core 也注入 `X-Project-Id` 上下文。 | 文档更偏“应用上下文开关”，代码同时实现“组织对象 + 上下文参数”双角色。 | 权限模型、上下文传播、菜单语义 |

## 4. 一页结论摘要

- 当前代码侧最重的问题是 `App` 多模型并行：`AppConfig`、`LowCodeApp`、`AiApp`、`AppManifest` 共存，且前端路由都挂在 `apps` 主语义下，已经产生同名异义。  
- 第二个高风险问题是 `workspace` 术语横跨 AI 工作台、应用工作区、manifest 子资源，生命周期与边界不一致。  
- 第三个高风险问题是 `runtime` 术语同时表示“发布映射对象”和“运行执行入口”，后续做监控、审计与权限时容易出现归属漂移。  
- 第四个问题是发布对象重复建模：`LowCodeAppVersion`、`AppRelease`、`AiApp.PublishVersion` 各自演化，短期可观察但中期需统一词汇映射。  
- 第五个问题是文档中的 `Tenant-App` 在代码缺失独立对象，导致数据源和应用归属关系依赖隐式约定，不利于后续 `SEC-33` 直接生成稳定映射表。

## 5. 与 SEC-29 对接建议（仅盘点建议）

- 可将本文件的“冲突类型 + 影响范围”直接并入 `SEC-29` 的冲突矩阵，作为代码侧证据列。  
- 下一步在 `SEC-33` 生成映射表时，建议按“对象名 -> 代码实体/接口/页面/类型”四列固化，避免仅凭术语讨论。
