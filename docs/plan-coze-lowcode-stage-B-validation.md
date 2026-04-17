# 阶段 B 验证报告（M04-M08 设计器与运行时）

## 范围
- M04 lowcode-editor-canvas：dnd + 三 LayoutEngine + 对齐线 + 多选 + 剪贴板 + 缩放 + 快捷键 ≥ 40 + 历史栈
- M05 outline + inspector + property-forms：结构树 + 三 Tab 检查器 + 5 值源 + 6 类内容参数 + Monaco LSP 桥
- M06 component-registry + components-web：47 件组件 + 元数据驱动校验 + 6 维矩阵 + AI 原生 4 件
- M07 lowcode-studio-web：5183 + Rsbuild + 三栏壳 + 5 Tab + 应用列表 + 多页面 / 变量 后端
- M08 lowcode-runtime-web + lowcode-preview-web：runtime-web 完整 + 5184 PreviewShell + RuntimeSchemaController + LowCodePreviewHub

## 验证结果

### 后端
- `dotnet build Atlas.SecurityPlatform.slnx` → **0 警告 0 错误**（每个里程碑均通过；最终 ~21s）。
- 新增端点：
  - `/api/v1/lowcode/apps/{appId}/draft-lock/{acquire,renew,release,takeover,status}`（M04）
  - `/api/v1/lowcode/components/{registry,overrides}`（M06）
  - `/api/v1/lowcode/apps/{appId}/pages/*`、`/api/v1/lowcode/apps/{appId}/variables/*`（M07）
  - `/api/runtime/apps/{appId}/schema` + `/api/runtime/apps/{appId}/versions/{versionId}/schema`（M08）
- 新增 SignalR Hub：`/hubs/lowcode-preview`（M08）。
- 写接口全部经 IAuditWriter 审计。
- Schema catalog 加入 9 张低代码表（`AppDefinition` / `PageDefinition` / `AppVariable` / `AppContentParam` / `AppVersionArchive` / `AppPublishArtifact` / `AppResourceReference` / `AppDraftLock` / `AppComponentOverride`）。

### 前端
- `pnpm run i18n:check` → **0 缺失**。
- 各包 `pnpm test`：
  - lowcode-editor-canvas → **22**（dnd + layout + guides + select + clipboard + zoom + history + keymap ≥ 40）
  - lowcode-editor-outline → **5**（buildOutline + setVisibility + setLocked + rename + delete + searchOutline 含祖先链）
  - lowcode-editor-inspector → **4**（INSPECTOR_TABS + appendActionToEvent + reorder + setActionResilience）
  - lowcode-property-forms → **9**（5 sourceType + 6 contentParam + dependsOn + validateFields）
  - lowcode-component-registry → **5**（register/get + 元数据驱动校验：fetch/workflow_api 拒绝）
  - lowcode-components-web → **5**（47 件 / 6 维矩阵零空缺 / AI 4 件均含 contentParams）
  - lowcode-studio-web → **1**（i18n 53 词条 zh-CN ↔ en-US 100% 对齐）
  - lowcode-runtime-web → **6**（store / dispatch / events / renderer）
  - lowcode-preview-web → **1**（端口 5184 强约束）
- 阶段 B 累计单测：**58**（与阶段 A 75 合计 **133**）。

### 文档
- `docs/lowcode-shortcut-spec.md`（M04 落地完整 ≥ 40 项）
- `docs/lowcode-component-spec.md`（M06 6 维矩阵 + AI 4 件特征 + 元数据驱动正反例）
- `docs/lowcode-content-params-spec.md`（M05 6 类内容参数；stub 已具备完整章节占位）
- `docs/contracts.md` 已含「低代码应用 UI Builder」总目录章节

## 关键决策与对齐
- **API 双前缀强约束**：M04 / M06 / M07 全部走 PlatformHost `/api/v1/lowcode/*`；M08 严格切到 AppHost `/api/runtime/*`。
- **dispatch 唯一桥梁**：lowcode-runtime-web 的 dispatch-client 走 `/api/runtime/events/dispatch`（M13 后端落地后联动）；context.workflow / chatflow 默认实现委托 dispatch，禁止任何包内直 fetch。
- **元数据驱动**：lowcode-components-web 注册时全部声明 implementationDescriptor（仅引 react + semi-ui）；CI 守门测试已强校验。
- **作用域隔离**：M03 + M02 双层守门贯穿全阶段（property-forms / inspector / runtime store 全部使用同一 ScopeViolationError）。
- **Yjs 协同接口预留**：M04 history 提供 IHistoryProvider 接口，M16 attach。

## 进入阶段 C
- M09 lowcode-workflow-adapter + 后端 RuntimeWorkflowsController（同步/异步/批量 + Polly 弹性 + 模式 A/B 黄金样本）开始。
