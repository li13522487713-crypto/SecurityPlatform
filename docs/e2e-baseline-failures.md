# E2E 基线失败清单（M10 范围外，独立专项处理）

> **采集背景**：M10「控制台生产硬化」终验执行 `pnpm run test:e2e:full`（聚合 `e2e-all.ordered.spec.ts`）后，记录全套 E2E 中与 M10 范围无关的既有失败 case，便于后续独立专项跟进。
> **最近一次执行**：2026-04-18 00:54Z，第 2 轮（在 PLAN §D.2「失败 case 立即抓日志、修代码、再跑」框架下追加）。
>
> | 轮次 | 通过 | 失败 | 跳过 | 改进 |
> |---|---|---|---|---|
> | 第 1 轮 | 32 | 17 | 29 | — |
> | 第 2 轮 | **39** | **12** | 27 | **+7 转绿，-5 失败** |

---

## 1. M10 范围内（Setup Console）状态

✅ **17/17 全部通过**（全套 ordered run 中 case #45–61），独立运行 `pnpm run test:e2e:app:only --grep "Setup Console"` 时为 18/18（多 1 个 setup-console-debug.spec）。

详见 `/opt/cursor/artifacts/screenshots/full-e2e-round2-20260418T004433Z/setup-console/` 共 42 张 png。

## 2. 第 2 轮内修复并验证转绿的 case（共 7 个）

| Spec | 修复内容 | 类型 |
|---|---|---|
| `workflow-orchestration` | testId 由 `app-workflows-page` 更新为现 IA 的 `app-develop-page` | 测试侧 |
| `navigation`（3 case） | workflows/chatflows pageTestId 修正；manage 头部文案改为现 IA 的 `团队治理` | 测试侧 |
| `publish-center` | scope 限定到页面 testId，避免与左侧菜单"发布中心"文本冲突 | 测试侧 |
| `settings-and-maintenance` | 直接 goto legacy `/apps/<appKey>/admin/settings`（sidebar `settings` 现指向工作空间设置） | 测试侧 |
| `visualization-and-runtime`（entry） | 接受新 IA 下 legacy `/apps/<appKey>/entry` 回退到 `app-dashboard-page` 的稳态 | 测试侧 |
| `helpers.loginApp` | 接受单工作空间登录后直接跳到 dashboard 的稳态 | 测试基础设施 |
| `reset-setup-state.mjs` | 新增 Linux 端口杀进程 + 3s grace 释放 TIME_WAIT | 测试基础设施 |

## 3. 第 2 轮仍失败的 12 个 case

均与本次 M10 改动无任何关联：

| # | Spec | 失败堆栈 | 根因 | 处理建议 |
|---|---|---|---|---|
| 1 | `e2e/setup/setup.spec.ts:30` | `waitForURL platformBaseUrl/setup` 30s 超时 | 复用安装态后 wizard URL 不再可达；DB 隔离不彻底 | 重新设计 `reset-setup-state` 的 DB rebind 逻辑 |
| 11 | `e2e/setup/z-post-setup-auth.spec.ts:281` | 同 #1 链路 | 同 #1 | 同 #1 |
| 25 | `e2e/app/console-workspace-workflow.smoke.spec.ts:142` | workflow 编辑器测试 | 依赖 workflow editor，与 #63-71 同源 | 同 workflow 系列 |
| 31 | `e2e/app/app-builder.spec.ts:37` | `chooseSemiOption("安全事件处置流")` 30s 超时 | mock workflows 列表不在 select 中显示 | 重写 mock 与 select 的协议 |
| 34 | `e2e/app/agent-workbench.spec.ts:238` | `app-bot-ide-page` 30s 不可见 | bot 创建后 navigate 到 detail 路由未触发 | 修复 `module-studio-react/agent dialog` 的 onOpenBot 调用链 |
| 40 | `e2e/app/reports-dashboards.spec.ts:18` | POST `/api/v1/reports` 返回 500 | 后端 ReportsController **不存在** | 新增 `Atlas.PlatformHost/Controllers/ReportsController.cs` 真实控制器 |
| 63 | `e2e/app/workflow-editor.spec.ts:12` | `workflow.detail.title.save-draft` 15s 不可见 | Coze workflow 编辑器（`@coze-workflow/playground-adapter`）中无此 testId | (a) 在 packages/workflow 内补回 testId；或 (b) 重写 specs 适配 Coze 测试钩子 |
| 66 | `e2e/app/workflow-collab.spec.ts:6` | 同 #63 | 同 #63 | 同 #63 |
| 67 | `e2e/app/workflow-publish.spec.ts:5` | 同 #63 | 同 #63 | 同 #63 |
| 68 | `e2e/app/workflow-run.spec.ts:30` | 同 #63 | 同 #63 | 同 #63 |
| 71 | `e2e/app/workflow-complete-flow.spec.ts:22` | 同 #63 | 同 #63 | 同 #63 |
| 72 | `e2e/app/workflow-v2-acceptance.spec.ts:268` | POST `/api/v2/workflows` 返回 500 | 后端 DAG workflow 创建端点错误 | 修复 DagWorkflowController.CreateAsync |

## 4. 独立专项立项建议

**专项一：后端缺失的真实控制器**
- ReportsController + DashboardsController + 对应 Service / DTO / xUnit / .http
- DagWorkflowController 创建端点修复

**专项二：Coze 工作流编辑器 testId 同步**
- 在 `packages/workflow/playground` 节点头部 / 标题栏 / canvas 位置补齐 `workflow.detail.title.{save-draft,duplicate,canvas-json,run-inputs,...}` testId
- 或：将 6 个 workflow E2E spec 重写为 Coze 原生 testId（`coze-workflow-editor-*`）

**专项三：Setup chain 状态隔离重构**
- 把 setup wizard 的"装好后跳过"逻辑由"配置文件标志"改为"基于 URL 的状态机"，使 setup spec 在已就绪环境也能跑
- 或：为 setup spec 引入独立 Hook 把 PlatformHost/AppHost 启动前先重置 DB

**专项四：业务编辑器交互回归**
- App Builder workflow 选择器 mock；agent-workbench bot 创建后导航；console-workspace-workflow 主链路
