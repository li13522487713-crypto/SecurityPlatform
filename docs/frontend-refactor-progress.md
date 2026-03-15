# 前端重构进度追踪报告

> **分析时间**：2026-03-14
> **信息来源**：`docs/` 下所有计划文档 + [frontend-code-review-report.md](file:///e:/codeding/SecurityPlatform/docs/frontend-code-review-report.md) + 代码实际状态扫描
> **说明**：本报告仅针对**前端**部分，综合文档追踪状态与代码实际状态

---

## 一、全局进度总览

项目存在 **三条并行的重构/改进线**，需要分别追踪：

| 重构线 | 来源文档 | 总体进度 | 前端相关度 |
|---|---|---|---|
| **A. 代码审查整改** | [frontend-code-review-report.md](file:///e:/codeding/SecurityPlatform/docs/frontend-code-review-report.md) | 🟡 部分完成 | 🔴 核心 |
| **B. 功能补齐 12 Case** | `plan-功能补齐总览.md` | 🔴 前端全部未勾选 | 🔴 核心 |
| **C. 产品化重构 12 Sprint** | `plan-产品化重构-12-sprint.md` | 🔴 Sprint 全部未勾选 | 🟡 中等 |

---

## 二、线 A：前端代码审查整改（来自 [frontend-code-review-report.md](file:///e:/codeding/SecurityPlatform/docs/frontend-code-review-report.md) 2026-03-04）

### P0（本周——原定 2026-03-04 当周）

| # | 整改项 | 文档状态 | 代码实际状态 | 结论 |
|---|---|---|---|---|
| P0-1 | 清理/替换 `innerHTML` 注入点，使用 DOMPurify 或安全 DOM API | ❌ 待改 | 🟡 **部分残留** — [AmisEditor.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/components/amis/AmisEditor.vue) (2处) + [amis-renderer.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/components/amis/amis-renderer.vue) (1处) 仍在用 `innerHTML = ""`（清空容器）；[MarkdownRenderer.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/components/ai/MarkdownRenderer.vue) 使用 `v-html` | 🟡 **未完成** |
| P0-2 | 直连 [fetch](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/amis/amis-env.ts#70-129) 路径统一接入 [requestApi](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/api-core.ts#106-269) | ❌ 待改 | ✅ **已完成** — [lowcode.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/types/lowcode.ts)、[login-log.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/login-log.ts)、[useExcelExport.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/composables/useExcelExport.ts) 已清理，当前仅 [api-core.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/api-core.ts)（核心封装自身）和 [api-conversation.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/api-conversation.ts)（AI 流式 SSE 场景）使用原生 fetch，属于合理场景 | ✅ **已完成** |
| P0-3 | 手写业务代码中限制新增 `any`（先从 Workflow/Approval 开始） | ❌ 待改 | 🟡 **未系统治理** — 排除 [api-generated.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/types/api-generated.ts) 后，手写代码中 `any` 仍大量存在（原报告统计约 827 处含生成代码） | 🟡 **未完成** |

### P1（两周内——原定 2026-03-18 前）

| # | 整改项 | 文档状态 | 代码实际状态 | 结论 |
|---|---|---|---|---|
| P1-1 | 拆分 [WorkflowDesignerPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/WorkflowDesignerPage.vue) 与 `ApprovalPropertiesPanel.vue` | ❌ 待改 | 🔴 **未拆分** — [ApprovalDesignerPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/ApprovalDesignerPage.vue) (30.9KB)、[WorkflowEditorPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/ai/AiWorkflowEditorPage.vue) (16.2KB) 等巨型页面仍未拆分 | 🔴 **未完成** |
| P1-2 | 修复列表 `key=idx/i`，改为稳定业务标识 | ❌ 待改 | 🟡 **部分残留** — 发现 3 处：[DesignerToolbar.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/components/approval/designer/DesignerToolbar.vue) (`:key="i"`)、[TestRunPanel.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/components/workflow/panels/TestRunPanel.vue) (`:key="idx"`)、[PropertiesPanel.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/components/workflow/panels/PropertiesPanel.vue) (`:key="idx"`) | 🟡 **未完成** |
| P1-3 | 提炼审批/流程相关常量枚举，减少魔法值 | ❌ 待改 | 🟡 **部分完成** — 已有 [constants/approval.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/constants/approval.ts) 和 [constants/workflow.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/constants/workflow.ts)，但大小偏小（867B / 147B），覆盖有限 | 🟡 **部分完成** |

### P2（迭代内）

| # | 整改项 | 文档状态 | 代码实际状态 | 结论 |
|---|---|---|---|---|
| P2-1 | 建立前端质量闸门：`lint + build + security scan + type check` | ❌ 待改 | 🟡 **部分就绪** — `npm run check` 命令已定义（`vue-tsc --noEmit && eslint && vite build`），但未见 CI/CD 集成 | 🟡 **部分完成** |
| P2-2 | 增加前端单测与关键路径 E2E（登录/审批提交/流程设计保存） | ❌ 待改 | 🟡 **骨架已有** — Vitest + Playwright 已配置，有少量 spec 文件（[api-core.spec.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/api-core.spec.ts)、[useTimeNormalize.spec.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/composables/useTimeNormalize.spec.ts)、[workflow-node-guards.spec.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/utils/workflow-node-guards.spec.ts)、[useWorkflowSerializer.spec.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/composables/useWorkflowSerializer.spec.ts)），但覆盖极少 | 🟡 **部分完成** |

---

## 三、线 B：功能补齐 12 Case（前端列）

> 来源：[plan-功能补齐总览.md](file:///e:/codeding/SecurityPlatform/docs/plan-功能补齐总览.md) 底部"12个Case执行追踪"

| Case | 功能 | 规格文档 | 后端 | **前端** | 验收 | 前端现状判断 |
|---|---|---|---|---|---|---|
| Case 01 | 认证安全基线 | ✅ | ✅ | ❌ | ❌ | 🟡 [api-core.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/api-core.ts) 已有完整 CSRF/幂等/Cookie 基础，[LoginPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/LoginPage.vue) 已存在（25KB），但 `innerHTML` XSS 点未清理 |
| Case 02 | 动态表 CRUD | ✅ | ❌ | ❌ | ❌ | 🟡 页面已有 [DynamicTablesPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/dynamic/DynamicTablesPage.vue) + [DynamicTableCrudPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/dynamic/DynamicTableCrudPage.vue)，但后端未联调 |
| Case 03 | 审批流设计运行 | ✅ | ❌ | ❌ | ❌ | 🟡 审批模块页面最丰富（17 个 Approval*.vue），设计器、实例列表、任务详情等均已实现，待后端联调 |
| Case 04 | 定时任务管理 | ✅ | ❌ | ❌ | ❌ | 🟡 [ScheduledJobsPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/monitor/ScheduledJobsPage.vue) (10KB) 已存在 |
| Case 05 | 服务监控健康 | ✅ | ❌ | ❌ | ❌ | 🟡 [ServerInfoPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/monitor/ServerInfoPage.vue) (10.7KB) 已存在 |
| Case 06 | 项目域权限隔离 | ✅ | ❌ | ❌ | ❌ | 🟡 [ProjectsPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/system/ProjectsPage.vue) (13.6KB) + [ProjectSwitcher.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/components/ProjectSwitcher.vue) (5.3KB) 已存在，项目作用域头已在 [api-core.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/api-core.ts) 实现 |
| Case 07 | 多数据源接入 | ✅ | ❌ | ❌ | ❌ | 🟡 [TenantDataSourcesPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/system/TenantDataSourcesPage.vue) (8.6KB) 已存在 |
| Case 08 | 自定义表格视图 | ✅ | ❌ | ❌ | ❌ | 🟡 [useTableView.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/composables/useTableView.ts) (18KB) 已实现核心逻辑，类型定义完整 |
| Case 09 | Excel 导入导出 | ✅ | ❌ | ❌ | ❌ | 🟡 [useExcelExport.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/composables/useExcelExport.ts) (2.7KB) + [requestApiBlob()](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/api-core.ts#270-385) 已存在 |
| Case 10 | 数据权限 DataScope | ✅ | ❌ | ❌ | ❌ | 🔴 前端未见 DataScope 配置 UI |
| Case 11 | 通知公告闭环 | ✅ | ❌ | ❌ | ❌ | 🟡 [NotificationsPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/system/NotificationsPage.vue) (6.8KB) + [notification.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/notification.ts) 服务已存在 |
| Case 12 | 生产级安全加固 | ✅ | ❌ | ❌ | ❌ | 🟡 httpOnly Cookie、CSRF、安全头均已实现，但 XSS 清理未完成 |

> [!IMPORTANT]
> **12 个 Case 的前端列全部标记为 ❌（未完成）**。但代码实际上大部分页面已经存在且可运行。
> **差距主要在**：(1) 未与重构后的后端新 API 联调；(2) 部分安全整改未收尾；(3) 文档追踪未及时更新。

---

## 四、线 C：产品化重构 12 Sprint（前端相关部分）

> 来源：[plan-产品化重构-12-sprint.md](file:///e:/codeding/SecurityPlatform/docs/plan-产品化重构-12-sprint.md)

| Sprint | 核心目标 | 文档状态 | 前端实际状态 |
|---|---|---|---|
| Sprint 1 | 基线冻结、弃用清单 | ❌ | 🟡 弃用清单 [deprecation-list.md](file:///e:/codeding/SecurityPlatform/docs/deprecation-list.md) 已发布（路由映射 + API 映射）；路由中已实现兼容重定向（`/settings/*` → `/console/settings/*`） |
| Sprint 2 | 后端骨架与迁移 | ❌ | ⚪ 主要是后端任务 |
| Sprint 3 | 平台控制面 V1 | ❌ | 🟡 [ConsoleLayout.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/layouts/ConsoleLayout.vue) + [ConsolePage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/console/ConsolePage.vue) + [ToolsAuthorizationPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/console/ToolsAuthorizationPage.vue) 已存在 |
| Sprint 4 | 发布中心与审计主链路 | ❌ | 🟡 [AuditPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/AuditPage.vue) 已存在 |
| Sprint 5 | 应用工作台 V1 | ❌ | 🟡 [AppWorkspaceLayout.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/layouts/AppWorkspaceLayout.vue) + [AppDashboardPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/apps/AppDashboardPage.vue) + [AppSettingsPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/apps/AppSettingsPage.vue) + [AppPagesPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/apps/AppPagesPage.vue) 已存在 |
| Sprint 6 | 统一设计器 V1 | ❌ | 🟡 [FormDesignerPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/lowcode/FormDesignerPage.vue) + [AppBuilderPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/lowcode/AppBuilderPage.vue) (33.7KB) 已存在 |
| Sprint 7 | 运行态 V1 | ❌ | 🟡 [RuntimeLayout.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/layouts/RuntimeLayout.vue) + [PageRuntimeRenderer.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/runtime/PageRuntimeRenderer.vue) 已存在；`/r/:appKey/:pageKey` 路由已配置 |
| Sprint 8-12 | 流程闭环/导入导出/License/Tools/硬化 | ❌ | 🟡 [LicensePage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/LicensePage.vue)、[WorkflowEditorPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/ai/AiWorkflowEditorPage.vue)、[PluginMarketPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/lowcode/PluginMarketPage.vue) 等均已存在 |

> [!NOTE]
> **Gate-R1 已于 2026-03-08 通过**（GUI 手测 + API 补证），说明控制台→应用→运行→License 的基本链路已可走通。但 Sprint 状态列均未勾选，存在文档追踪滞后。

---

## 五、历史完成的前端改进（已确认完成）

根据 [implementation-summary.md](file:///e:/codeding/SecurityPlatform/docs/implementation-summary.md) 和 [week3-4-implementation-summary.md](file:///e:/codeding/SecurityPlatform/docs/week3-4-implementation-summary.md)：

| 时间 | 完成项 | 确认度 |
|---|---|---|
| Week 1-2 (2026-02-12) | [api-core.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/api-core.ts) 添加 `credentials: "include"`（httpOnly Cookie 支持） | ✅ 代码已确认 |
| Week 1-2 | 前端兼容 localStorage + Cookie 双模式 | ✅ 代码已确认 |
| Week 3-4 (2026-02-12) | 表单设计器新手引导（[useOnboarding.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/composables/useOnboarding.ts) + driver.js 集成） | ✅ 代码已确认 |
| Week 3-4 | i18n 翻译补充（7 个命名空间：validation/error/success/confirm/approval/organization/onboarding） | ✅ 代码已确认 |
| 持续 | API 服务层重构：从单体 [api.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/types/api.ts) (1879行) 拆分为 30+ 个领域子模块 | ✅ 代码已确认 |
| 持续 | [requestApi](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/api-core.ts#106-269) 统一封装（CSRF + 幂等 + Token 刷新 + 写请求去重 + 错误去重） | ✅ 代码已确认 |
| 持续 | 四种 Layout 体系（Main/Console/AppWorkspace/Runtime） | ✅ 代码已确认 |
| 持续 | 直连 fetch 清理（[lowcode.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/types/lowcode.ts)、[login-log.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/login-log.ts)、[useExcelExport.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/composables/useExcelExport.ts) 已接入 [requestApi](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/api-core.ts#106-269)） | ✅ 代码已确认 |
| 持续 | 弃用路由兼容重定向（`/settings/*` → `/console/settings/*` 等） | ✅ 代码已确认 |
| 持续 | NSwag 类型生成流程配置（`npm run generate-types`） | ✅ 代码已确认 |
| 持续 | Design Tokens（CSS 变量体系） | ✅ 代码已确认 |
| 2026-03-08 | Gate-R1 GUI 手测通过 | ✅ 报告已确认 |

---

## 六、前端待完成事项清单（按优先级排序）

### 🔴 P0 — 必须优先处理

| # | 待办事项 | 来源 | 涉及文件 | 预估工作量 |
|---|---|---|---|---|
| 1 | **清理 innerHTML / v-html XSS 风险** | 代码审查 P0-1 | [AmisEditor.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/components/amis/AmisEditor.vue)(2处)、[amis-renderer.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/components/amis/amis-renderer.vue)(1处)、[MarkdownRenderer.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/components/ai/MarkdownRenderer.vue)(v-html) | 2-3h |
| 2 | **`any` 类型治理**（至少 Workflow/Approval 主链路） | 代码审查 P0-3 | 需逐文件扫描治理 | 8-12h |
| 3 | **12 Case 前端勾选状态同步** — 将已实际完成的前端页面在追踪表中打勾 | 文档追踪滞后 | `plan-功能补齐总览.md` | 1h |
| 4 | **12 Sprint 状态同步** — Gate-R1 已过但 Sprint 状态未更新 | 文档追踪滞后 | `plan-产品化重构-12-sprint.md` | 1h |

### 🟡 P1 — 本迭代内完成

| # | 待办事项 | 来源 | 涉及文件 | 预估工作量 |
|---|---|---|---|---|
| 5 | **拆分巨型页面**：[ApprovalDesignerPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/ApprovalDesignerPage.vue)(30.9KB)、[AppBuilderPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/lowcode/AppBuilderPage.vue)(33.7KB)、[LoginPage.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/pages/LoginPage.vue)(25KB) 等 | 代码审查 P1-1 | 3-5 个大页面 | 8-16h |
| 6 | **修复列表 key=idx/i** 为稳定业务标识 | 代码审查 P1-2 | [DesignerToolbar.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/components/approval/designer/DesignerToolbar.vue)、[TestRunPanel.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/components/workflow/panels/TestRunPanel.vue)、[PropertiesPanel.vue](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/components/workflow/panels/PropertiesPanel.vue) | 1-2h |
| 7 | **充实常量枚举文件** — [constants/approval.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/constants/approval.ts)(867B) 和 [constants/workflow.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/constants/workflow.ts)(147B) 过小 | 代码审查 P1-3 | 2 个文件 | 2-3h |
| 8 | **审批流程设计器右键菜单增强** | Week 3-4 待完成任务 #11 | ApprovalDesigner 相关组件 | 2-3h |
| 9 | **Data Scope 前端配置 UI** | Case 10 前端缺失 | 需新建页面/组件 | 4-6h |

### 🟢 P2 — 迭代内/下迭代

| # | 待办事项 | 来源 | 涉及文件 | 预估工作量 |
|---|---|---|---|---|
| 10 | **前端单测补充**（关键 composable 和 service 的测试覆盖） | 代码审查 P2-2 | composables、services | 8-12h |
| 11 | **E2E 测试补充**（登录/审批提交/流程设计保存） | 代码审查 P2-2 | `e2e/specs/` | 6-8h |
| 12 | **CI/CD 集成质量闸门**（`npm run check` 接 pipeline） | 代码审查 P2-1 | CI 配置 | 2-4h |
| 13 | **i18n 实际接入率提升** — 翻译键已定义但大量页面仍硬编码中文 | Week 3-4 后续 | 各 pages 组件 | 持续 |
| 14 | **废弃 localStorage token 读取路径** — 设定淘汰窗口 | 代码审查安全项 | [utils/auth.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/utils/auth.ts) + [api-core.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/api-core.ts) | 2h |

---

## 七、进度可视化

```
代码审查整改    ████████░░░░░░░░░░░░  ~40%
  P0 (3项)    ██████░░░░░░░░░░░░░░  1/3 完成
  P1 (3项)    ████░░░░░░░░░░░░░░░░  0.5/3 完成
  P2 (2项)    ██████░░░░░░░░░░░░░░  0.5/2 完成

12 Case 前端   ████████████░░░░░░░░  ~60% (页面存在但未勾选/未联调)
  页面已存在   11/12 Case
  联调完成     0/12 Case
  追踪已勾选   0/12 Case

12 Sprint     ████████░░░░░░░░░░░░  ~40% (框架已搭,Sprint 未勾选)
  Gate-R1     ████████████████████  已通过
  Sprint 勾选  0/12 Sprint
```

---

## 八、建议的下一步行动顺序

1. **先花 2 小时同步文档追踪状态**（P0 #3 + #4）— 把已实际完成的前端页面和 Gate-R1 结果反映到追踪表
2. **再花 2-3 小时清理 XSS 残留** (P0 #1) — 最高安全风险
3. **修复 key=idx 问题** (P1 #6) — 1-2 小时即可完成
4. **按 Case 优先级逐个标记前端完成** — 与后端联调配合进行
5. **`any` 治理和巨型页面拆分** — 作为持续改进，每次迭代覆盖一批
