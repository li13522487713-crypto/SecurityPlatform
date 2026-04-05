---
name: 第三组大域拆小 Case
overview: 将第三组"大域拆小"的 40 个骨架页面迁移任务，按迁移复杂度分层拆成可独立闭环的小 case（Case 014 ~ Case 055），每个 case 独立可构建、可验证，前后依赖关系清晰。
todos:
  - id: case-014
    content: "Case 014: platform-web AssetsPage 补齐（安全资产列表，只读分页）"
    status: completed
  - id: case-015
    content: "Case 015: platform-web AuditPage 补齐（审计日志列表，只读分页+筛选）"
    status: completed
  - id: case-016
    content: "Case 016: platform-web AlertPage 补齐（安全告警列表，只读分页+级别筛选）"
    status: completed
  - id: case-017
    content: "Case 017: platform-web MigrationGovernancePage 补齐（迁移治理只读看板）"
    status: completed
  - id: case-018
    content: "Case 018: platform-web AppDashboardPage 补齐（应用仪表盘导航壳）"
    status: completed
  - id: case-019
    content: "Case 019: platform-web AppPagesPage 补齐（应用页面简单列表）"
    status: completed
  - id: case-020
    content: "Case 020: platform-web PersonalAccessTokensPage 补齐（PAT CRUD）"
    status: completed
  - id: case-021
    content: "Case 021: platform-web WebhooksPage 补齐（订阅CRUD+投递记录）"
    status: completed
  - id: case-022
    content: "Case 022: platform-web PluginManagePage 补齐（插件管理CRUD）"
    status: completed
  - id: case-023
    content: "Case 023: platform-web SystemConfigsPage 补齐（分组配置表单）"
    status: completed
  - id: case-024
    content: "Case 024: platform-web ProjectsPage 补齐（项目管理CRUD+分配抽屉）"
    status: completed
  - id: case-025
    content: "Case 025: platform-web ResourceCenterPage 补齐（资源中心聚合展示）"
    status: completed
  - id: case-026
    content: "Case 026: platform-web RuntimeContextsPage 补齐（运行时上下文列表+Drawer）"
    status: completed
  - id: case-027
    content: "Case 027: platform-web MessageQueuePage 补齐（消息队列监控）"
    status: completed
  - id: case-028
    content: "Case 028: platform-web ScheduledJobsPage 补齐（定时任务管理）"
    status: completed
  - id: case-029
    content: "Case 029: platform-web ReleaseCenterPage 补齐（发布中心列表+回滚）"
    status: completed
  - id: case-030
    content: "Case 030: platform-web AppSettingsPage 补齐（应用设置多卡片）"
    status: completed
  - id: case-031
    content: "Case 031: platform-web DataSourcesPage 补齐（数据源管理+连带Drawer子组件）"
    status: completed
  - id: case-032
    content: "Case 032: platform-web RuntimeExecutionsPage 补齐（执行记录多API工作台）"
    status: completed
  - id: case-033
    content: "Case 033: platform-web DynamicTablesPage 补齐（动态数据工作台，不含设计器）"
    status: completed
  - id: case-034
    content: "Case 034: platform-web AgentEditorPage 补齐（智能体编辑器+MultiAgentRunPanel）"
    status: completed
  - id: case-035-038
    content: "Case 035-038: platform-web AI余下骨架页面逐个补齐（AiConfig/KnowledgeBases/AiMarketplace/AiPlugin等）"
    status: completed
  - id: case-039
    content: "Case 039: platform-web LogicFlowDesignerPage 补齐（逻辑流设计器+8个子组件打包迁移）"
    status: completed
  - id: case-040
    content: "Case 040: platform-web WorkflowEditorPage 补齐（Vue Flow画布+面板组件）"
    status: completed
  - id: case-041
    content: "Case 041: platform-web ApprovalDesignerPage 补齐（审批设计器+6个子组件）"
    status: completed
  - id: case-042
    content: "Case 042: platform-web ERDCanvasPage+DataDesignerPage 补齐（动态表设计器壳+4子模块）"
    status: completed
  - id: case-043
    content: "Case 043: platform-web AppBuilderPage 补齐（低代码设计器+AmisEditor依赖链）"
    status: completed
  - id: case-044
    content: "Case 044: app-web ApprovalInstanceDetailPage 补齐"
    status: completed
  - id: case-045
    content: "Case 045: app-web ReportsPage 补齐"
    status: completed
  - id: case-046
    content: "Case 046: app-web DashboardsPage 补齐"
    status: completed
  - id: case-047
    content: "Case 047: app-web VisualizationRuntimePage 补齐"
    status: completed
  - id: case-048
    content: "Case 048: app-web AiAssistantPage 补齐"
    status: completed
isProject: false
---

# 第三组：大域拆小 — 可闭环 Case 详细计划

## 分层策略

按迁移复杂度分为四层，由简到繁逐层推进：

- **L1 简单只读列表**（~100-200 行，无子组件，1-2 个 API）：直接复制改造
- **L2 中等 CRUD / 表单**（~200-500 行，可能有 1 个子组件，2-4 个 API）：需新建 service 文件
- **L3 复杂工作台 / 多 API**（~500-1000 行，多 API 联动）：需拆分阶段交付
- **L4 设计器 / 大型组件**（~1000+ 行，大量子组件依赖链）：需打包迁移整个子树

---

## L1 层：简单只读 / 展示页面（Case 014 ~ 019）

### Case 014：platform-web AssetsPage 补齐（安全资产列表）
- **类型：** 页面迁移（只读列表）
- **改动范围：** `apps/platform-web/src/pages/AssetsPage.vue` + `services/api-system.ts` 补 `getAssetsPaged`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/AssetsPage.vue`](src/frontend/Atlas.WebApp/src/pages/AssetsPage.vue)（129 行，依赖 `@/services/api`）
- **迁移要点：** 分页表格 + 关键字筛选，无写操作；需在 `api-system.ts` 中补充资产查询 API
- **i18n：** 补充 `zh-CN.ts` / `en-US.ts` 资产相关词条
- **验证：** `pnpm run build:platform-web` 通过
- **完成标准：** 资产列表可渲染、可分页、可搜索

### Case 015：platform-web AuditPage 补齐（审计日志列表）
- **类型：** 页面迁移（只读列表）
- **改动范围：** `apps/platform-web/src/pages/AuditPage.vue` + `services/api-system.ts` 补 `getAuditsPaged`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/AuditPage.vue`](src/frontend/Atlas.WebApp/src/pages/AuditPage.vue)（177 行，关键字 + 动作/结果筛选）
- **迁移要点：** 分页表格 + 多条件筛选（关键字、动作类型、结果），无写操作
- **验证：** `pnpm run build:platform-web` 通过

### Case 016：platform-web AlertPage 补齐（安全告警列表）
- **类型：** 页面迁移（只读列表）
- **改动范围：** `apps/platform-web/src/pages/AlertPage.vue` + `services/api-system.ts` 补 `getAlertsPaged`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/AlertPage.vue`](src/frontend/Atlas.WebApp/src/pages/AlertPage.vue)（170 行，关键字 + 严重级别筛选）
- **迁移要点：** 分页表格 + 级别筛选，无写操作
- **验证：** `pnpm run build:platform-web` 通过

### Case 017：platform-web MigrationGovernancePage 补齐（迁移治理看板）
- **类型：** 页面迁移（只读看板）
- **改动范围：** `apps/platform-web/src/pages/console/MigrationGovernancePage.vue` + 新建 `services/api-migration-governance.ts`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/console/MigrationGovernancePage.vue`](src/frontend/Atlas.WebApp/src/pages/console/MigrationGovernancePage.vue)（148 行）
- **迁移要点：** 治理概览指标卡片 + 说明，偏仪表盘，无复杂交互
- **验证：** `pnpm run build:platform-web` 通过

### Case 018：platform-web AppDashboardPage 补齐（应用仪表盘）
- **类型：** 页面迁移（只读导航壳）
- **改动范围：** `apps/platform-web/src/pages/apps/AppDashboardPage.vue` + 在 `api-console.ts` 补相关 API
- **Legacy 参考：** [`Atlas.WebApp/src/pages/apps/AppDashboardPage.vue`](src/frontend/Atlas.WebApp/src/pages/apps/AppDashboardPage.vue)（97 行，统计卡片 + 快捷入口）
- **迁移要点：** 应用详情统计 + 跳转链接，体量最小
- **验证：** `pnpm run build:platform-web` 通过

### Case 019：platform-web AppPagesPage 补齐（应用页面列表）
- **类型：** 页面迁移（简单列表）
- **改动范围：** `apps/platform-web/src/pages/apps/AppPagesPage.vue` + 新建 `services/api-lowcode.ts`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/apps/AppPagesPage.vue`](src/frontend/Atlas.WebApp/src/pages/apps/AppPagesPage.vue)（119 行，依赖 `@/services/lowcode`）
- **迁移要点：** 页面列表 + 发布状态 + 跳转设计器 / 预览链接
- **验证：** `pnpm run build:platform-web` 通过

---

## L2 层：中等 CRUD / 表单页面（Case 020 ~ 030）

### Case 020：platform-web PersonalAccessTokensPage 补齐
- **类型：** 页面迁移（CRUD 列表）
- **改动范围：** `apps/platform-web/src/pages/settings/PersonalAccessTokensPage.vue` + 新建 `services/api-pat.ts`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/settings/PersonalAccessTokensPage.vue`](src/frontend/Atlas.WebApp/src/pages/settings/PersonalAccessTokensPage.vue)（274 行，依赖 `api-pat`）
- **迁移要点：** 列表 + 创建/编辑 Modal + 吊销；无子组件依赖
- **验证：** `pnpm run build:platform-web` 通过

### Case 021：platform-web WebhooksPage 补齐
- **类型：** 页面迁移（CRUD + Drawer）
- **改动范围：** `apps/platform-web/src/pages/settings/WebhooksPage.vue`（需确认目录，Legacy 在 `system/`）+ 新建 `services/api-webhook.ts`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/system/WebhooksPage.vue`](src/frontend/Atlas.WebApp/src/pages/system/WebhooksPage.vue)（271 行，订阅 CRUD + 投递记录 Drawer + 测试发送）
- **迁移要点：** 无子组件；Drawer 为内联 template
- **验证：** `pnpm run build:platform-web` 通过

### Case 022：platform-web PluginManagePage 补齐
- **类型：** 页面迁移（CRUD + 操作按钮）
- **改动范围：** `apps/platform-web/src/pages/settings/PluginManagePage.vue` + 新建 `services/api-plugin.ts`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/system/PluginManagePage.vue`](src/frontend/Atlas.WebApp/src/pages/system/PluginManagePage.vue)（224 行，使用 `CrudPageLayout`）
- **迁移要点：** 上传包 + 重载 + 启用/禁用/卸载 + JSON 配置 Drawer；platform-web 已有 `CrudPageLayout`（`@atlas/shared-ui`）
- **验证：** `pnpm run build:platform-web` 通过

### Case 023：platform-web SystemConfigsPage 补齐（系统配置表单）
- **类型：** 页面迁移（分组表单）
- **改动范围：** `apps/platform-web/src/pages/settings/SystemConfigsPage.vue` + 新建 `services/api-system-config.ts`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/system/SystemConfigsPage.vue`](src/frontend/Atlas.WebApp/src/pages/system/SystemConfigsPage.vue)（358 行，依赖 `@/services/system-config`）
- **迁移要点：** 按分组 Tab 展示、多类型编辑（布尔/数字/Secret/JSON）、创建弹窗；无子组件
- **验证：** `pnpm run build:platform-web` 通过

### Case 024：platform-web ProjectsPage 补齐（项目管理列表）
- **类型：** 页面迁移（CRUD + 分配抽屉）
- **改动范围：** `apps/platform-web/src/pages/settings/ProjectsPage.vue` + 在 `api-system.ts` 补项目 API
- **Legacy 参考：** [`Atlas.WebApp/src/pages/system/ProjectsPage.vue`](src/frontend/Atlas.WebApp/src/pages/system/ProjectsPage.vue)（448 行，使用 `CrudPageLayout` + `TableViewToolbar` + 间接 `useTableView`）
- **迁移要点：** CRUD 主表 + 分配用户/部门/职位抽屉 + 远程多选；platform-web 已有 `CrudPageLayout` 和 `useTableView`
- **验证：** `pnpm run build:platform-web` 通过

### Case 025：platform-web ResourceCenterPage 补齐（资源中心）
- **类型：** 页面迁移（聚合展示）
- **改动范围：** `apps/platform-web/src/pages/console/ResourceCenterPage.vue` + 在 `api-console.ts` 补 API
- **Legacy 参考：** [`Atlas.WebApp/src/pages/console/ResourceCenterPage.vue`](src/frontend/Atlas.WebApp/src/pages/console/ResourceCenterPage.vue)（194 行，统计卡片 + 分组资源表 + 跳转）
- **验证：** `pnpm run build:platform-web` 通过

### Case 026：platform-web RuntimeContextsPage 补齐（运行时上下文）
- **类型：** 页面迁移（列表 + 详情 Drawer）
- **改动范围：** `apps/platform-web/src/pages/console/RuntimeContextsPage.vue` + 新建 `services/api-runtime-contexts.ts`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/console/RuntimeContextsPage.vue`](src/frontend/Atlas.WebApp/src/pages/console/RuntimeContextsPage.vue)（227 行）
- **迁移要点：** 筛选 + 分页 + 详情 Drawer + 路由 query 联动；无子组件
- **验证：** `pnpm run build:platform-web` 通过

### Case 027：platform-web MessageQueuePage 补齐（消息队列监控）
- **类型：** 页面迁移（列表 + 操作）
- **改动范围：** `apps/platform-web/src/pages/monitor/MessageQueuePage.vue` + 在 service 补 API
- **Legacy 参考：** [`Atlas.WebApp/src/pages/monitor/MessageQueuePage.vue`](src/frontend/Atlas.WebApp/src/pages/monitor/MessageQueuePage.vue)（298 行，队列统计、死信重试/清理、消息 Drawer）
- **验证：** `pnpm run build:platform-web` 通过

### Case 028：platform-web ScheduledJobsPage 补齐（定时任务）
- **类型：** 页面迁移（列表 + 操作 + Drawer）
- **改动范围：** `apps/platform-web/src/pages/monitor/ScheduledJobsPage.vue` + 在 service 补 API
- **Legacy 参考：** [`Atlas.WebApp/src/pages/monitor/ScheduledJobsPage.vue`](src/frontend/Atlas.WebApp/src/pages/monitor/ScheduledJobsPage.vue)（301 行，任务启停、立即触发、执行历史 Drawer）
- **验证：** `pnpm run build:platform-web` 通过

### Case 029：platform-web ReleaseCenterPage 补齐（发布中心）
- **类型：** 页面迁移（列表 + 大详情 Modal）
- **改动范围：** `apps/platform-web/src/pages/console/ReleaseCenterPage.vue` + 新建 `services/api-release-center.ts`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/console/ReleaseCenterPage.vue`](src/frontend/Atlas.WebApp/src/pages/console/ReleaseCenterPage.vue)（383 行，依赖 `api-coze-runtime`）
- **迁移要点：** 发布列表 + 详情/追踪 Modal + 回滚操作
- **验证：** `pnpm run build:platform-web` 通过

### Case 030：platform-web AppSettingsPage 补齐（应用设置）
- **类型：** 页面迁移（多卡片设置）
- **改动范围：** `apps/platform-web/src/pages/apps/AppSettingsPage.vue`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/apps/AppSettingsPage.vue`](src/frontend/Atlas.WebApp/src/pages/apps/AppSettingsPage.vue)（475 行，数据源绑定 Modal + 文件存储等多卡片）
- **迁移要点：** 无子组件但交互多（数据源切换绑定等）
- **验证：** `pnpm run build:platform-web` 通过

---

## L3 层：复杂工作台 / 多 API 页面（Case 031 ~ 038）

### Case 031：platform-web DataSourcesPage 补齐（数据源管理）
- **类型：** 页面迁移（大表单 + 需连带子组件）
- **改动范围：** `apps/platform-web/src/pages/settings/DataSourcesPage.vue` + 新建 `services/api-datasource.ts` + 迁移 `AdvancedDataPreviewDrawer.vue`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/system/TenantDataSourcesPage.vue`](src/frontend/Atlas.WebApp/src/pages/system/TenantDataSourcesPage.vue)（524 行）+ [`AdvancedDataPreviewDrawer.vue`](src/frontend/Atlas.WebApp/src/pages/system/AdvancedDataPreviewDrawer.vue)
- **迁移要点：** 大表单（可视化/连接串切换）+ 驱动定义 + 测试连接 + **需连带 Drawer 子组件**；注意收拢硬编码中文到 i18n
- **验证：** `pnpm run build:platform-web` 通过

### Case 032：platform-web RuntimeExecutionsPage 补齐（运行时执行记录）
- **类型：** 页面迁移（复杂多 API 工作台）
- **改动范围：** `apps/platform-web/src/pages/console/RuntimeExecutionsPage.vue` + 新建 `services/api-runtime-executions.ts`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/console/RuntimeExecutionsPage.vue`](src/frontend/Atlas.WebApp/src/pages/console/RuntimeExecutionsPage.vue)（968 行，依赖 4 个 API 模块）
- **迁移要点：** 应用/状态/时间范围筛选 + 取消/重试/恢复 + 详情 Drawer + 跨页跳转；无子组件但逻辑密集
- **验证：** `pnpm run build:platform-web` 通过

### Case 033：platform-web DynamicTablesPage 补齐（动态数据工作台）
- **类型：** 页面迁移（大型工作台，不含设计器内核）
- **改动范围：** `apps/platform-web/src/pages/dynamic/DynamicTablesPage.vue` + 在 `api-dynamic-tables.ts` 补齐缺失 API
- **Legacy 参考：** [`Atlas.WebApp/src/pages/dynamic/DynamicTablesPage.vue`](src/frontend/Atlas.WebApp/src/pages/dynamic/DynamicTablesPage.vue)（1169 行）
- **迁移要点：** 多 Tab + 侧栏表目录 + 建表流程；不含 ERD/DataDesigner 设计器（那些是独立 case）
- **验证：** `pnpm run build:platform-web` 通过

### Case 034：platform-web AgentEditorPage 补齐（智能体编辑器）
- **类型：** 页面迁移（复杂编辑器）
- **改动范围：** `apps/platform-web/src/pages/ai/AgentEditorPage.vue` + 迁移 `MultiAgentRunPanel.vue` + 在 service 层补齐 `api-agent-publication` / `api-ai-plugin` / `api-multi-agent` 等
- **Legacy 参考：** [`Atlas.WebApp/src/pages/ai/AgentEditorPage.vue`](src/frontend/Atlas.WebApp/src/pages/ai/AgentEditorPage.vue)（1112 行，依赖 5 个 API + `agent-workspace` + `MultiAgentRunPanel`）
- **迁移要点：** 编辑器主体 + **MultiAgentRunPanel 子组件必须连带迁移**；建议先迁 service 层再迁页面
- **前置依赖：** 需先有 `api-ai.ts` 中对应接口（platform-web 已有该文件）
- **验证：** `pnpm run build:platform-web` 通过

### Case 035-038：platform-web 其余 AI 骨架页面逐个补齐

#### Case 035：platform-web AiConfigPage 补齐（AI 全局配置）
- **改动范围：** `pages/ai/AiConfigPage.vue` + service
- **Legacy 参考：** 如果 legacy 有对应实现则迁移，否则按 API 契约新建
- **验证：** `pnpm run build:platform-web` 通过

#### Case 036：platform-web KnowledgeBasesPage 补齐（知识库管理）
- **改动范围：** `pages/ai/KnowledgeBasesPage.vue` + 新建 `services/api-knowledge.ts`
- **验证：** `pnpm run build:platform-web` 通过

#### Case 037：platform-web AiMarketplacePage 补齐（AI 市场）
- **改动范围：** `pages/ai/AiMarketplacePage.vue` + service 补齐
- **验证：** `pnpm run build:platform-web` 通过

#### Case 038：platform-web AiPluginListPage 补齐（AI 插件列表）
- **改动范围：** `pages/ai/AiPluginListPage.vue` + service 补齐
- **验证：** `pnpm run build:platform-web` 通过

> 其余 AI 骨架页面（AiWorkflowListPage / AiWorkflowEditorPage / MultiAgentListPage / AiLibraryPage / AiWorkspacePage）按同模式逐个补齐，每页一个 case，此处不逐一展开。

---

## L4 层：设计器 / 大型组件树（Case 039 ~ 047）

这些 case 涉及大量子组件依赖链，建议最后批次实施。

### Case 039：platform-web LogicFlowDesignerPage 补齐（逻辑流设计器）
- **类型：** 页面迁移（设计器 + 8 个子组件）
- **改动范围：** 页面 + **整个 `designer/` 子目录**（FlowCanvas / FlowPropertyPanel / FlowNodePanel / FlowObjectPanel / FlowDesignerToolbar / FlowDebugPanel / FlowStructureTree / FlowDiffView）+ 新建 `services/api-logic-flow.ts`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/logic-flow/LogicFlowDesignerPage.vue`](src/frontend/Atlas.WebApp/src/pages/logic-flow/LogicFlowDesignerPage.vue)（455 行 + 8 子组件）
- **迁移要点：** 需打包迁移整个 `logic-flow/designer/` 子树

### Case 040：platform-web WorkflowEditorPage 补齐（工作流编辑器）
- **类型：** 页面迁移（Vue Flow 画布 + 面板组件）
- **改动范围：** 页面 + `@/components/workflow/panels/`（NodePanel / PropertiesPanel / TestRunPanel）+ WorkflowNodeRenderer + 新建 `services/api-workflow-v2.ts`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/workflow/WorkflowEditorPage.vue`](src/frontend/Atlas.WebApp/src/pages/workflow/WorkflowEditorPage.vue)（612 行）
- **迁移要点：** 依赖 `@vue-flow/*` 库，需确认 platform-web 的 `package.json` 已安装
- **前置依赖：** platform-web 已有 `WorkflowListPage`（已实现），本 case 补编辑器

### Case 041：platform-web ApprovalDesignerPage 补齐（审批设计器）
- **类型：** 页面迁移（三栏设计器 + 6 个子组件）
- **改动范围：** 页面 + `@/components/approval/`（X6PreviewCanvas / DesignerToolbar / DesignerBasicInfo / DesignerFormSchema / DesignerFlowProcess / ValidationErrorPanel）
- **Legacy 参考：** [`Atlas.WebApp/src/pages/ApprovalDesignerPage.vue`](src/frontend/Atlas.WebApp/src/pages/ApprovalDesignerPage.vue)（843 行 + 6 子组件）
- **迁移要点：** 审批 composable + 双服务模块；`DesignerFlowProcess` 可能有下层依赖需再深入

### Case 042：platform-web ERDCanvasPage + DataDesignerPage 补齐（动态表设计器壳）
- **类型：** 页面迁移（设计器入口 + 4 个子模块）
- **改动范围：** `ERDCanvasPage.vue`（32 行壳）+ `DataDesignerPage.vue`（48 行壳）+ 需迁移 `RelationDesigner` / `ViewDesignerCanvas` / `TransformDesignerCanvas` / `DynamicTableDesignPage`
- **Legacy 参考：** 壳页本身极薄，**真实复杂度在 4 个设计器组件**
- **迁移要点：** 建议与 Case 033（DynamicTablesPage）一起规划但分开交付

### Case 043：platform-web AppBuilderPage 补齐（低代码设计器）
- **类型：** 页面迁移（低代码核心）
- **改动范围：** 页面 + `AmisEditor.vue`（异步加载）+ `@/stores/schemaHistory` + `@/services/lowcode` + `@/services/templates`
- **Legacy 参考：** [`Atlas.WebApp/src/pages/lowcode/AppBuilderPage.vue`](src/frontend/Atlas.WebApp/src/pages/lowcode/AppBuilderPage.vue)（1218 行）
- **迁移要点：** 迁移成本最高；Amis 编辑器依赖链需完整评估；建议此 case 作为最后实施

---

## app-web 页面补齐（Case 044 ~ 048）

### Case 044：app-web ApprovalInstanceDetailPage 补齐
- **类型：** 页面迁移
- **改动范围：** `apps/app-web/src/pages/approval/ApprovalInstanceDetailPage.vue` + 在 service 补审批详情 API
- **Legacy 参考：** 需确认 legacy app-web 对应实现
- **验证：** `pnpm run build:app-web` 通过

### Case 045：app-web ReportsPage 补齐
- **改动范围：** `apps/app-web/src/pages/reports/ReportsPage.vue` + 新建 `services/api-reports.ts`
- **验证：** `pnpm run build:app-web` 通过

### Case 046：app-web DashboardsPage 补齐
- **改动范围：** `apps/app-web/src/pages/reports/DashboardsPage.vue`
- **验证：** `pnpm run build:app-web` 通过

### Case 047：app-web VisualizationRuntimePage 补齐
- **改动范围：** `apps/app-web/src/pages/visualization/VisualizationRuntimePage.vue`
- **验证：** `pnpm run build:app-web` 通过

### Case 048：app-web AiAssistantPage 补齐
- **改动范围：** `apps/app-web/src/pages/ai/AiAssistantPage.vue`
- **迁移要点：** 同目录 `AgentChatPage.vue`（725 行）已完整实现，AiAssistant 可参考其 API 和交互模式
- **验证：** `pnpm run build:app-web` 通过

---

## 依赖关系图

```mermaid
graph TD
    subgraph L1_simple [L1 简单只读]
        C014[Case014 AssetsPage]
        C015[Case015 AuditPage]
        C016[Case016 AlertPage]
        C017[Case017 MigrationGovernance]
        C018[Case018 AppDashboard]
        C019[Case019 AppPages]
    end

    subgraph L2_crud [L2 中等CRUD]
        C020[Case020 PAT]
        C021[Case021 Webhooks]
        C022[Case022 Plugin]
        C023[Case023 SystemConfigs]
        C024[Case024 Projects]
        C025[Case025 ResourceCenter]
        C026[Case026 RuntimeContexts]
        C027[Case027 MessageQueue]
        C028[Case028 ScheduledJobs]
        C029[Case029 ReleaseCenter]
        C030[Case030 AppSettings]
    end

    subgraph L3_complex [L3 复杂工作台]
        C031[Case031 DataSources]
        C032[Case032 RuntimeExecutions]
        C033[Case033 DynamicTables]
        C034[Case034 AgentEditor]
        C035_038[Case035-038 AI余页]
    end

    subgraph L4_designer [L4 设计器]
        C039[Case039 LogicFlowDesigner]
        C040[Case040 WorkflowEditor]
        C041[Case041 ApprovalDesigner]
        C042[Case042 ERD+DataDesigner]
        C043[Case043 AppBuilder]
    end

    subgraph AppWeb [app-web]
        C044[Case044 ApprovalDetail]
        C045[Case045 Reports]
        C046[Case046 Dashboards]
        C047[Case047 Visualization]
        C048[Case048 AiAssistant]
    end

    L1_simple --> L2_crud
    L2_crud --> L3_complex
    C026 --> C032
    C033 --> C042
    L3_complex --> L4_designer
end
```

## 实施顺序建议

- **第一批（L1，可并行）**：Case 014 ~ 019，每个半天可闭环
- **第二批（L2，可并行）**：Case 020 ~ 030，每个 0.5~1 天可闭环
- **第三批（L3）**：Case 031 ~ 038，每个 1~2 天
- **第四批（L4，串行为主）**：Case 039 ~ 043，每个 2~3 天
- **app-web 批次**：Case 044 ~ 048 可与 platform-web 并行推进

## 每个 Case 通用闭环标准

1. 页面 `.vue` 文件从骨架变为完整实现（参考 Legacy 同名页面）
2. 对应 `services/api-*.ts` 补齐所需 API 函数
3. `i18n/zh-CN.ts` 和 `en-US.ts` 补齐该页面词条
4. `pnpm run build:platform-web`（或 `build:app-web`）0 错误通过
5. 无硬编码中文面向用户文案（全走 i18n）




