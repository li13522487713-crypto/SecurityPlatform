# 现状对象到目标对象映射主表（SEC-33）

> 版本：v1.0-draft  
> 产出日期：2026-03-16  
> 任务来源：Linear `SEC-33` / 父任务 `SEC-21`  
> 输入基线：`docs/analysis/object-definition-conflicts.md`、`docs/analysis/code-object-inventory.md`、`docs/analysis/unified-terminology-glossary-v1.md`

## 1. 目标与范围

- 输出“现状对象 -> 目标对象”的唯一主归属，用于后续命名整改与实施卡拆分。
- 映射结果只定义对象去向与处理方式，不在本卡执行代码重命名或数据库结构变更。
- 处理方式统一为：保留 / 改名 / 降级 / 废弃。

## 2. 对象映射主表

| 现状对象名 | 来源 | 当前职责摘要 | 目标对象名 | 目标职责摘要 | 处理方式 |
|---|---|---|---|---|---|
| Tenant | 文档 + 后端 + 前端 | 多租户隔离主体，承载组织、用户、权限、审计边界。 | Tenant | 保持租户根对象，不做语义迁移。 | 保留 |
| Application（裸用） | 文档 + 后端 + 前端 | 既表示平台目录项，也表示租户应用实例或入口。 | ApplicationCatalog / TenantApplication / TenantAppInstance | 按“目录定义 / 开通关系 / 运行实例”三段拆分。 | 改名 |
| Tenant-App | 文档 | 租户开通应用后的订阅关系，承载状态与可用性。 | TenantApplication | 明确为租户与应用目录项的关系对象。 | 改名 |
| LowCodeApp | 后端 + 前端 | 低代码应用资产与配置载体，含页面、环境与发布态。 | TenantAppInstance | 收敛为租户应用运行实例；低代码仅保留为能力标签。 | 降级 |
| AppManifest | 后端 + 文档 | 平台产品化应用元数据与发布主线。 | ApplicationCatalog | 收敛为平台应用目录定义，不直接承担租户运行态。 | 改名 |
| AiApp | 后端 + 前端 | AI 应用实体，绑定 agent/workflow/prompt 并支持发布。 | TenantAppInstance（子类型：Ai） | 统一到租户应用实例模型，通过类型区分 AI/LowCode。 | 降级 |
| AppsController（/api/v1/apps） | 后端 | 应用配置对象（AppConfig）查询与更新。 | TenantApplicationSettings（契约名） | 不再使用裸词 apps，改为“租户应用配置”资源。 | 改名 |
| LowCodeAppsController（/api/v1/lowcode-apps） | 后端 | 历史低代码应用聚合 API（已标注 Obsolete）。 | TenantApplicationsController + WorkspacesController | 路由拆分到租户应用与工作空间资源。 | 废弃 |
| AppManifestsController（/api/v1/app-manifests） | 后端 | 应用元数据、发布与 workspace 子资源管理。 | ApplicationCatalogsController + ReleasesController | 目录定义与发布资源分离，保留兼容窗口。 | 改名 |
| Project | 后端 + 前端 | 既是租户项目域对象，又被当作应用上下文 header。 | ProjectAsset（创作资产） / ProjectScope（访问上下文） | 拆分“资产对象”与“访问上下文”两种语义。 | 改名 |
| AiWorkspace | 后端 + 前端 | 用户级 AI 工作台偏好、最近访问与收藏。 | Workspace | 收敛为工作空间对象，AI 仅作能力域标记。 | 改名 |
| app-workspace-*（前端路由命名） | 前端 | 应用和工作台复合语义路由。 | workspace-*（工作空间） + tenant-application-*（应用） | 解除“应用=工作台”的混合命名。 | 改名 |
| RuntimeRoute | 后端 | 按 appKey + pageKey 映射发布版本路由。 | RuntimeContextRoute | 明确为运行上下文路由映射，不等同执行实例。 | 改名 |
| PageRuntimeController（/api/v1/runtime） | 后端 + 前端 | 运行态 schema 读取与运行写入。 | RuntimeExecutionsController | 统一为运行执行资源，区分定义态与执行态。 | 改名 |
| Workflow（裸用） | 文档 + 后端 + 前端 | 定义态流程与运行态执行常被混称。 | WorkflowDefinition / RuntimeExecution | 严格分离定义对象与执行对象。 | 改名 |
| DataSource（裸用） | 文档 + 后端 + 前端 | 租户共享资源与应用绑定资源语义混合。 | TenantDataSource + TenantApplicationDataSourceBinding | 数据源主对象与绑定关系拆分。 | 改名 |
| Marketplace / 探索广场 | 文档 | 模板、插件、资产分发场域。 | Marketplace | 作为平台层对象保留，不并入应用对象。 | 保留 |
| Knowledge / KnowledgeBase | 文档 + 后端 | 检索增强知识资源，语义边界未固定。 | KnowledgeBase | 统一为知识库对象，支持 Agent/Workflow 绑定。 | 改名 |
| Agent | 文档 + 后端 | AI 交互对象，独立草稿、调试、发布与运行。 | Agent | 保持对象名称，后续明确其与 TenantAppInstance 关系。 | 保留 |

## 3. 高风险对象表

| 对象名 | 高风险原因 | 影响面 | 是否需要兼容期 | 备注 |
|---|---|---|---|---|
| Application（裸用） | 现状同时承担目录定义、开通关系、运行实例三类语义。 | 文档 / API / 前端 / 数据模型 / 运行态 | 是 | 必须先落地三段模型词汇后再改接口命名。 |
| LowCodeApp | 前端主链路仍依赖 `/lowcode-apps`，直接删除会中断核心功能。 | API / 前端 / 运行态 | 是 | 建议“新接口先行 + 旧接口转发 + 分批切流”。 |
| Project | 业务实体与访问上下文混用，影响权限和数据隔离判定。 | 文档 / API / 前端 / 数据模型 | 是 | 需要先定义 ProjectAsset 与 ProjectScope 边界。 |
| Runtime | “路由映射态”和“执行实例态”未分离，审计归属不稳定。 | API / 运行态 / 文档 | 是 | 先重命名契约，再迁移监控和审计字段。 |
| DataSource | 数据源归属层级与绑定对象不一致，易造成权限泄漏。 | API / 数据模型 / 运行态 / 文档 | 是 | 必须先补绑定关系对象，再清理直连字段。 |
| Workspace | AI 工作台、应用工作区、路由命名共享同词，生命周期冲突。 | 文档 / 前端 / API | 是 | 需统一工作空间定义后再做路由重构。 |

## 4. 未决问题表

| 对象名 | 未决点 | 为什么暂时不能定 | 建议收口卡 |
|---|---|---|---|
| TenantAppInstance | 是否需要独立数据库实体，还是先以视图模型承接。 | 依赖 `SEC-32` 最终模型深度与数据库改造窗口。 | `SEC-38` |
| ProjectAsset | 与现有 `Project` 的字段复用比例和迁移脚本策略。 | 需结合前端信息架构与权限模型联动确认。 | `SEC-37` |
| RuntimeExecution | 与现有运行日志、任务调度模型的主键关联方式。 | 需对齐运行监控与审计链路现状后定稿。 | `SEC-38` |
| ApplicationCatalog | 与 `AppManifest` 的版本对象是否完全同构。 | 需评估发布、回滚、灰度字段最小保留集。 | `SEC-35` |

## 5. 一页结论摘要

- 当前必须优先处理的 5 个对象：`Application`、`LowCodeApp`、`Project`、`Runtime`、`DataSource`。
- 最阻塞命名整改与 App 三段模型落地的核心问题是：`Application` 裸词复用与 `LowCodeApp` 历史路由依赖。
- 实施上应先完成“术语裁决 + 契约命名基线”，再进入 API/前端迁移；否则会在兼容期产生双向漂移。
- 本映射表可直接提供给 `SEC-34` 输出兼容期规则，并作为 `SEC-37`（命名整改）和 `SEC-38`（模型改造）的输入主表。
