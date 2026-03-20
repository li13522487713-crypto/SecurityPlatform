# SEC-39 前端路由与入口页面盘点

## 1. 盘点范围与方法

- 盘点目标：梳理 `console`、`ai`、`lowcode`、`runtime` 与 legacy 入口的现状。
- 盘点依据：
  - 静态路由：`src/frontend/Atlas.WebApp/src/router/index.ts`
  - 挂载层级：`src/frontend/Atlas.WebApp/src/App.vue`
  - 各壳层导航：`src/frontend/Atlas.WebApp/src/layouts/*.vue`
  - 动态路由兜底映射：`src/frontend/Atlas.WebApp/src/utils/dynamic-router.ts`
- 说明：本文只记录“现状”，不做最终 IA 方案设计。

## 2. 路由入口清单（现状）

### 2.1 入口与挂载层级总览

| 路由前缀/入口 | 页面组件（主入口） | 父级容器/挂载层 | 当前归属层级判断 | legacy |
|---|---|---|---|---|
| `/console*` | `ConsolePage`、`ToolsAuthorizationPage`、`TenantDataSourcesPage`、`SystemConfigsPage` | `ConsoleLayout` | Platform（夹杂 Tenant/System 配置） | 否 |
| `/apps/:appId/*` | `AppDashboardPage`、`AppBuilderPage`、`AppSettingsPage`、`AppPagesPage`、`FormListPage`、`ApprovalFlowsPage`、`DynamicTablesPage`、`PermissionsPage`、`PageRuntimeRenderer` | `AppWorkspaceLayout` | App Workspace（夹杂 Runtime 入口） | 否 |
| `/r/:appKey/:pageKey` | `PageRuntimeRenderer` | `RuntimeLayout` | Runtime | 否 |
| `/ai/*` | `AiWorkspacePage`、`AiLibraryPage`、`AiVariablesPage`、`AiMarketplacePage` 等 | `MainLayout` | Platform + App 编辑/运营混层 | 否 |
| `/lowcode/*` | `AppListPage`、`AppBuilderPage`、`FormListPage`、`TemplateMarketPage`、`FormDesignerPage`、`PluginMarketPage` | `MainLayout` | App Workspace（混入 Platform 运维入口） | 否 |
| `/settings/*`、`/system/*`、`/monitor/*`、`/workflow/*`、`/approval/*` | 系统、流程、监控相关页面 | `MainLayout` | Platform / Tenant / Runtime 混层 | 部分是 |
| `/notifications`、`/alerts`、`/approval/tasks` 等 | redirect 到新路由 | `MainLayout` | 兼容入口 | 是 |

### 2.2 console 入口

| 路由路径 | 页面/组件 | 父级容器 | 当前归属层级判断 | legacy |
|---|---|---|---|---|
| `/console` | `ConsolePage` | `ConsoleLayout` | Platform | 否 |
| `/console/apps` | `ConsolePage` | `ConsoleLayout` | Platform（应用目录聚合） | 否 |
| `/console/resources` | `ConsolePage` | `ConsoleLayout` | Platform（资源聚合） | 否 |
| `/console/releases` | `ConsolePage` | `ConsoleLayout` | Platform（发布聚合） | 否 |
| `/console/tools` | `ToolsAuthorizationPage` | `ConsoleLayout` | Platform | 否 |
| `/console/datasources` | `TenantDataSourcesPage` | `ConsoleLayout` | Tenant 能力放在 Platform 入口 | 否 |
| `/console/settings/system/configs` | `SystemConfigsPage` | `ConsoleLayout` | Platform（系统配置） | 否 |

### 2.3 ai 入口（静态 + 动态映射可追溯）

| 路由路径 | 页面/组件 | 父级容器 | 当前归属层级判断 | legacy |
|---|---|---|---|---|
| `/settings/ai/model-configs` | `ModelConfigsPage` | `MainLayout` | Platform | 否 |
| `/ai/workspace` | `AiWorkspacePage` | `MainLayout` | Platform（产品工作台） | 否 |
| `/ai/library` | `AiLibraryPage` | `MainLayout` | Platform（资源域） | 否 |
| `/ai/variables` | `AiVariablesPage` | `MainLayout` | Platform/应用配置混层 | 否 |
| `/ai/open-platform` | `AiOpenPlatformPage` | `MainLayout` | Platform（开放能力） | 否 |
| `/ai/devops/test-sets` | `AiTestSetsPage` | `MainLayout` | Runtime/测试治理能力挂主导航 | 否 |
| `/ai/devops/mock-sets` | `AiMockSetsPage` | `MainLayout` | Runtime/测试治理能力挂主导航 | 否 |
| `/ai/shortcuts` | `AiShortcutsPage` | `MainLayout` | Platform | 否 |
| `/ai/search` | `AiSearchResultsPage` | `MainLayout` | Platform（全局搜索） | 否 |
| `/ai/marketplace` | `AiMarketplacePage` | `MainLayout` | Platform（市场） | 否 |
| `/ai/agents/:id/edit` | `AgentEditorPage` | `MainLayout` | App 编辑页直接暴露主导航域 | 否 |
| `/ai/agents` | `AgentListPage`（动态映射） | `MainLayout` | App Workspace/运营混层 | 否 |
| `/ai/agents/:agentId/chat` | `AgentChatPage`（动态映射） | `MainLayout` | Runtime 会话能力混入主导航域 | 否 |
| `/ai/knowledge-bases` | `KnowledgeBaseListPage`（动态映射） | `MainLayout` | App Workspace/数据资产混层 | 否 |
| `/ai/knowledge-bases/:id` | `KnowledgeBaseDetailPage`（动态映射） | `MainLayout` | App 编辑详情暴露在主导航域 | 否 |
| `/ai/workflows` | `AiWorkflowListPage`（动态映射） | `MainLayout` | App Workspace | 否 |
| `/ai/workflows/:id/edit` | `AiWorkflowEditorPage`（动态映射） | `MainLayout` | App 编辑页直接暴露主导航域 | 否 |

### 2.4 lowcode 入口

| 路由路径 | 页面/组件 | 父级容器 | 当前归属层级判断 | legacy |
|---|---|---|---|---|
| `/lowcode/apps` | `AppListPage` | `MainLayout` | App Workspace 入口（租户视角） | 否 |
| `/lowcode/apps/:id/builder` | `AppBuilderPage` | `MainLayout` | App 编辑页直接暴露主导航域 | 否 |
| `/lowcode/forms` | `FormListPage` | `MainLayout` | App Workspace（资源管理） | 否 |
| `/lowcode/templates` | `TemplateMarketPage` | `MainLayout` | Platform/市场 | 否 |
| `/lowcode/forms/:id/designer` | `FormDesignerPage` | `MainLayout` | App 编辑页直接暴露主导航域 | 否 |
| `/lowcode/plugin-market` | `PluginMarketPage` | `MainLayout` | Platform/市场 | 否 |

### 2.5 runtime 入口

| 路由路径 | 页面/组件 | 父级容器 | 当前归属层级判断 | legacy |
|---|---|---|---|---|
| `/r/:appKey/:pageKey` | `PageRuntimeRenderer` | `RuntimeLayout` | Runtime（标准运行态） | 否 |
| `/apps/:appId/run/:pageKey` | `PageRuntimeRenderer` | `AppWorkspaceLayout` | Runtime 入口挂在 App Workspace（跨层） | 否 |
| `/process/instances/:id` | `ApprovalInstanceDetailPage` | `MainLayout` | Runtime（流程实例） | 否 |
| `/approval/workspace` | `ApprovalWorkspacePage` | `MainLayout` | Runtime（个人工作台） | 否 |
| `/approval/instances` | redirect `/approval/workspace?tab=requests` | `MainLayout` | Runtime 旧入口 | 是 |
| `/approval/inbox` | redirect `/approval/workspace?tab=pending` | `MainLayout` | Runtime 旧入口 | 是 |
| `/approval/tasks` | redirect `/approval/workspace?tab=pending` | `MainLayout` | Runtime 旧入口 | 是 |
| `/approval/done` | redirect `/approval/workspace?tab=done` | `MainLayout` | Runtime 旧入口 | 是 |
| `/approval/cc` | redirect `/approval/workspace?tab=cc` | `MainLayout` | Runtime 旧入口 | 是 |

### 2.6 legacy/兼容入口

| 路由路径 | 当前去向 | 问题特征 |
|---|---|---|
| `/settings/:pathMatch(.*)*` | 重定向到 `/console/settings/*` | settings 与 console 双入口并存，语义重复 |
| `/system/dict-types` | `/settings/system/dict-types` | system 与 settings 并存 |
| `/system/configs` | `/settings/system/configs` | system 与 settings 并存 |
| `/notifications` | `/system/notifications` | 旧入口别名 |
| `/alerts` | `/alert` | 单复数与命名不一致 |
| `/approval/instances`、`/approval/inbox`、`/approval/tasks`、`/approval/done`、`/approval/cc` | `/approval/workspace?tab=*` | 流程入口重复，迁移痕迹明显 |

## 3. 入口问题表

| 路由/页面 | 问题类型 | 问题说明 | 影响用户路径 |
|---|---|---|---|
| `/ai/agents/:id/edit`、`/ai/workflows/:id/edit`、`/lowcode/forms/:id/designer`、`/lowcode/apps/:id/builder` | 混层 | 应用内编辑页直接暴露在平台主导航域，设计态入口没有收敛到统一 App Workspace。 | 平台导航 -> 深层编辑页，用户难区分“平台管理”与“应用开发”。 |
| `/apps/:appId/run/:pageKey` 与 `/r/:appKey/:pageKey` | 重复入口 | 同属运行态页面，分别挂在 AppWorkspace 与 Runtime 两套壳层。 | 应用内调试与运行发布路径来回跳转，认知负担高。 |
| `/console/datasources` + `/settings/system/datasources` | 重复入口 | 数据源管理被 console 与 settings 双挂载。 | 管理员难判断唯一入口，培训成本高。 |
| `/settings/system/configs` + `/console/settings/system/configs` + `/system/configs` | 重复入口 + legacy | 系统参数配置存在三条路径（含兼容路由）。 | 收藏链接、文档链接易失效或混乱。 |
| `/alerts` -> `/alert` | 命名不清 | 复数/单数并存，不符合统一命名规范。 | 搜索与记忆成本上升。 |
| `/settings/:pathMatch(.*)*` | 入口过浅 | 通配符重定向扩大了“隐式兼容入口”范围，难以治理真实使用路径。 | 新旧 IA 并行期难以统计入口收敛进度。 |
| `/approval/*`（workspace 与管理并列） | 混层 | 个人审批工作台与管理后台入口并列。 | 普通审批人易误入管理页面，路径决策成本高。 |

## 4. 重点迁移候选表

| 页面/入口 | 建议目标层级 | 优先级 | 备注 |
|---|---|---|---|
| `/ai/agents/:id/edit`、`/ai/workflows/:id/edit`、`/lowcode/forms/:id/designer`、`/lowcode/apps/:id/builder` | App Workspace | 高 | 典型“应用内编辑页暴露在平台主导航”的高风险入口。 |
| `/apps/:appId/run/:pageKey` 与 `/r/:appKey/:pageKey` | Runtime | 高 | 需统一一条主路径，另一条保留短期兼容。 |
| `/console/datasources`、`/settings/system/datasources` | Tenant/Platform（需明确唯一归属） | 高 | 与系统配置能力边界耦合，建议优先确定归属并收敛。 |
| `/settings/system/configs`、`/console/settings/system/configs`、`/system/configs` | Platform | 高 | 多入口重复最明显，影响后续 IA 收敛验收。 |
| `/approval/workspace` 与 `/approval/instances/manage`、`/approval/flows/manage` | Runtime + Platform（拆分） | 中 | 先分离“个人工作台”与“流程治理后台”。 |
| `/settings/:pathMatch(.*)*` | legacy 兼容层 | 中 | 需配合埋点评估后逐步下线。 |
| `/alerts` | Platform（规范化命名） | 低 | 可在主路径稳定后统一别名策略。 |

## 5. 一页结论摘要

### 5.1 当前入口结构最严重的 5 个问题

1. **应用编辑页外溢到平台导航**：AI 与 Lowcode 的多个编辑页直接挂在 `/ai/*`、`/lowcode/*`，与平台治理入口混杂。
2. **运行态双入口并行**：`/apps/:appId/run/:pageKey` 与 `/r/:appKey/:pageKey` 同时存在，壳层语义不一致。
3. **系统配置类入口重复**：datasource/config 等能力在 `console`、`settings`、`system` 多路径并存。
4. **审批域“个人工作台”和“管理后台”混放**：运行态与治理态入口并列，角色切换成本高。
5. **legacy 通配兼容范围过大**：`/settings/:pathMatch(.*)*` 使新旧路径边界不清，影响收敛治理。

### 5.2 最影响四段式 IA（Platform / Tenant / App Workspace / Runtime）的入口

- **Platform 受影响最大**：`/ai/*`、`/lowcode/*` 中的编辑器页侵入平台导航语义。
- **App Workspace 边界不稳**：`/apps/:appId/*` 已具雏形，但 runtime 仍与 `/r/*` 并行。
- **Runtime 路径不统一**：流程运行态（`/approval/workspace`）与应用运行态（`/r/*`）尚未在 IA 结构上统一表达。
- **Tenant/Platform 交界不清**：数据源、系统参数等能力在 console/settings/system 多入口重复，需作为 IA 收敛优先区。

## 6. 收口执行记录（2026-03-20）

> 本节记录第一批“信息架构收口”已落地项，作为联调期唯一入口基线。

### 6.1 唯一主路径表（第一批）

| 领域能力 | 主路径（Canonical） | 兼容路径（Legacy） |
|---|---|---|
| 租户数据源管理 | `/settings/system/datasources` | `/console/datasources` |
| 系统参数配置 | `/settings/system/configs` | `/console/settings/system/configs`、`/system/configs` |
| 应用运行态（主） | `/r/:appKey/:pageKey` | `/runtime/:appKey/:pageKey`、`/apps/:appId/run/:pageKey`（工作台辅助预览） |

### 6.2 Legacy 下线清单（阶段一）

| Legacy 路径 | 当前状态 | 下线前提 |
|---|---|---|
| `/console/datasources` | 已改为 redirect 到主路径，并弹出 Deprecated 提示 | 入口埋点确认低流量 + 文档/收藏链接迁移完成 |
| `/console/settings/system/configs` | 已改为 redirect 到主路径，并弹出 Deprecated 提示 | 同上 |
| `/console/settings/:pathMatch(.*)*` | 已限制为 console 前缀内兼容，不再使用 `/settings/:pathMatch(.*)*` 全局通配 | 逐条替换历史入口后可删除该兼容规则 |

### 6.3 动态路由 fallback 映射补齐

- 已补齐 Runtime 主入口映射：`/r/:appKey/:pageKey -> PageRuntimeRenderer.vue`
- 已保留主路径 fallback：
  - `/settings/system/datasources`
  - `/settings/system/configs`
- legacy fallback 继续保留窗口期，避免动态菜单历史 path 直接 404。

### 6.4 菜单归属层级统一（本次变更）

- `ConsoleLayout` 顶部菜单中的“数据源管理/系统设置”已统一指向主路径：
  - `/settings/system/datasources`
  - `/settings/system/configs`
- Console 首页快捷入口、应用设置页“前往数据源管理”链接已同步到主路径。
