---
name: Coze v6 Frontend &amp; Backend Replace
overview: 在已完成 M01-M20 的 Atlas 复刻基础上，前端用 @coze-agent-ide/* + @coze-foundation/* + @coze-common/chat-uikit 整体替换 Atlas 自研 module-studio-react 与自研 Semi 聊天（选项 C），后端补齐 Coze v1 全量 OpenAPI 面（workflow/chatflow/conversations/messages/files/audio/knowledge/datasets/bots/variables/triggers，选项 B），让 Coze 官方 Python/Node/Go SDK 能无修改指向 Atlas 后端。
todos:
  - id: fe1_audit
    content: M-FE1 Coze 前端替换盘点与桥接策略：笾 module-studio-react 12 页、agent-ide/common/foundation 可用度、foundation-bridge 升级方案，产出 docs/plan-coze-frontend-lib-replacement.md
    status: pending
  - id: fe2_foundation_shell
    content: M-FE2 Foundation & Shell 对齐：atlas-foundation-bridge 升级为 foundation-sdk 真实兼容 + 挂载 workspace-adapter + coze-shell-react 改 thin wrapper + 登录页替换 + 主题硬编码去除
    status: pending
  - id: fe3_agent_layout
    content: M-FE3 Agent IDE（一）layout/entry/navigate/context/bot-config-area/prompt/model-manager/commons 替换为 @coze-agent-ide/*，删除 module-studio-react layout 自研实现
    status: pending
  - id: fe4_agent_chat_skills
    content: M-FE4 Agent IDE（二）chat-area-provider + chat-uikit 替换自研聊天 + debug-area + skills/tools/plugins/workflow-modal/memory/onboarding/publish/audit 全量挂载
    status: pending
  - id: fe5_chatflow
    content: M-FE5 Chatflow 特殊交互：mode 透传 + chat-workflow-render + 会话历史侧边 + 中断恢复 UI + SSE 5 类事件展示 + additional_messages / conversation_id 显式切换
    status: pending
  - id: fe6_workflow_hardening
    content: M-FE6 Workflow Designer 硬化：boundary user 字段 + workspace 上下文 + test-run-next 接入 + testset 去 mock + VITE_LIBRARY_MOCK 默认关 + 节点目录 7 大类对齐 + Selector/If 名称统一
    status: pending
  - id: be1_protocol_harden
    content: M-BE1 Coze v6 协议硬化：workflow_version / connector_id / debug_url / event_id / stream_resume body / additional_messages / conversation_id / SSE 5 类事件 / NodeType 字符串 / Selector-If 在同一条 PR 链路上逐帳落地
    status: pending
  - id: be2_workflow_openapi
    content: M-BE2 Workflow Open API 真流：StreamRun 改真流 + stream_resume + async + async-jobs + batch + response envelope 对齐 + scope 扩展 + 独立 OpenChatflowController
    status: pending
  - id: be3_conversations_messages
    content: M-BE3 Conversations + Messages Open API：新建 OpenConversationsController / OpenMessagesController，完整 CRUD、清空、历史拉取、chat_history 映射、Coze Python SDK 端到端 demo
    status: pending
  - id: be4_files_hardening
    content: M-BE4 Files Open API 加固：upload/multipart/chunked/retrieve/delete 全套，MIME 尺寸杀毒 policy 统一，等保 2.0 合规
    status: pending
  - id: be5_audio_openapi
    content: M-BE5 Audio Open API：transcriptions / speech / translations / voices 四个端点 + IAudioProviderAdapter 抽象 + not_configured 默认 + 长音频 Hangfire 异步
    status: pending
  - id: be6_datasets_knowledge
    content: M-BE6 Datasets Open API：OpenDatasetsController + documents + query + images + tables + ParsingStrategy 全字段，兼容旧 OpenKnowledgeController
    status: pending
  - id: be7_bots_agents
    content: M-BE7 Bots / Agents Open API：create/publish/get_online_info/get_draft_info/list/update/invoke，对齐 AgentChannelAdapters 多渠道
    status: pending
  - id: be8_variables_triggers
    content: M-BE8 Variables / Triggers / PAT / UserInfo Open API：variables CRUD + triggers CRUD + webhook + pat 管理 + user_info
    status: pending
  - id: be9_sdk_interop
    content: M-BE9 SDK 双向互通：ICozeOpenApiClient + CozeOpenApiClient + CozeInvokeWorkflow 节点 + 租户级凭据 + SSE 映射 + 端到端 demo
    status: pending
  - id: be10_observability_regression
    content: M-BE10 观测 + 任务中心 + 回归：OTel 扩展、runtime/tasks 聚合四类任务、审计全覆盖、SSRF/QPS 安全回归、.http 总清单与契约文档同步
    status: pending
isProject: false
---

# Coze v6 前端 Coze-Lib 大替换 + 后端全量 OpenAPI 复刻计划

## 0. 范围与强约束

- 前端选项 C（Agent IDE 整体替换）：保留 `@coze-workflow/playground-adapter`（已落地），**新增替换** `@atlas/module-studio-react`、`@atlas/coze-shell-react` 自研骨架、自研 Semi 聊天，改走 `@coze-agent-ide/*`、`@coze-foundation/*`、`@coze-common/chat-area*`、`@coze-common/chat-uikit`、`@coze-studio/workspace-adapter`。
- 后端选项 B（Coze v1 OpenAPI 全量面）：以 `/api/v1/open/*` 为 Coze 官方 SDK 入口，保留 `/api/v2/workflows/*`（Atlas 原生）与 `/api/workflow_api/*`（Coze 兼容旧面）不动；所有写接口走 `IAuditWriter`，SSE 对齐 Coze v6 事件名。
- 强约束（全过程）：
  - 后端 `dotnet build` 0 警告 0 错误；新增/修改 API 必须同步 `.http` + `docs/contracts.md`。
  - 前端 `pnpm run build` + `pnpm run i18n:check` 0 缺失；零 `any`。
  - 每个 case ≤ 1 人日，独立 PR，独立冒烟；失败必须立即停止，不得伪造完成。
  - 禁止一次改 3 层以上；严格按依赖顺序（Foundation → Shell → Agent IDE → Chat/Debug → Publish）。
  - 等保 2.0：OpenAPI 所有写接口必须 PAT scope + 审计；SSRF / MIME / 尺寸校验兜底。

## 1. 当前仓库与 v6 报告的关键差距（本计划要修的清单）

先把探查到的 MVP / 简易实现问题集中列出，下面的 case 都 map 到这些点：

- 后端协议：
  - `DagWorkflowRunRequest` 无 `workflow_version` / `connector_id` / `debug_url`；`CozeWorkflowTestRunRequest` 响应无 `connector_id` / `debug_url`（`src/backend/Atlas.Presentation.Shared/Controllers/Ai/CozeWorkflowCompatControllerBase.cs` ~663）。
  - `CozeWorkflowTestResumeRequest.event_id` 未使用；`StreamResumeAsync` 不接 body（`src/backend/Atlas.Infrastructure/Services/WorkflowEngine/DagWorkflowExecutionService.cs`）。
  - `RuntimeChatflowInvokeRequest` 仅 `(ChatflowId, SessionId, Input, Context)`，无 `additional_messages` / `conversation_id` 一等字段（`src/backend/Atlas.Application/AiPlatform/Models/RuntimeChatflowDto.cs`）。
  - Chatflow SSE 仅 4 类 `tool_call|message|error|final`，未对齐 Coze v6 的 `Message/Error/Done/Interrupt/PING`（`src/backend/Atlas.Infrastructure/Services/LowCode/RuntimeSessionAndChatflowServices.cs` 的 `MapEngineEventToChunk`）。
  - `OpenWorkflowsController.StreamRun` 不是真流（一帧 JSON + `[DONE]`）（`src/backend/Atlas.AppHost/Controllers/Open/OpenWorkflowsController.cs`）。
  - `WorkflowNodeType` 序列化为数字；`Selector` 与 `If` 命名不一致。
- 后端 OpenAPI 覆盖：只有 `Open/OpenWorkflowsController`、`OpenChatController`、`OpenFilesController`、`OpenKnowledgeController`、`OpenBotsController`、`OpenScopeHelper`，缺 `conversations / messages / audio / datasets / variables / triggers / pat / user_info` 八条主链。
- 前端集成：
  - `@coze-studio/workspace-adapter` 已在 `package.json` 但无 `import`；`@atlas/foundation-bridge` 只是 shim，未真实对齐 `@coze-foundation/foundation-sdk`。
  - `@coze-agent-ide/*`（48 个包）与 `@coze-common/chat-area*`、`@coze-common/chat-uikit` 全在 workspace 但 `app-web` 无路由挂载。
  - `WorkflowRuntimeBoundary` theme 硬编码 `light`，user 字段缺 avatar/email（`src/frontend/apps/app-web/src/app/workflow-runtime-boundary.tsx`）。
  - `mode`（workflow vs chatflow）在 `WorkflowPlayground` props 上声明但未透传（`src/frontend/packages/workflow/adapter/playground/src/page.tsx`）。
  - `services/mock/testset`、`VITE_LIBRARY_MOCK` 仍在前端挂着；`OpenBotsController` 等部分接口为 stub。

---

## 2. 里程碑总览

- 前端（FE1-FE6，约 12 周）：FE1 盘点 → FE2 Foundation/Shell → FE3 Agent IDE（一）骨架 → FE4 Agent IDE（二）chat/skills/publish → FE5 Chatflow 特殊交互 → FE6 Workflow Designer 硬化与去 mock。
- 后端（BE1-BE10，约 10 周）：BE1 协议硬化 → BE2 Workflow Open API 真流 → BE3 Conversations/Messages → BE4 Files 加固 → BE5 Audio → BE6 Datasets/Knowledge → BE7 Bots/Agents → BE8 Variables/Triggers/Misc → BE9 SDK 双向互通 → BE10 观测/任务中心/回归。
- 并行策略：FE1、BE1 首启；FE2 与 BE2-BE3 并行；FE3-FE4 期间 BE4-BE8 并行；FE5 与 BE3 共同收口 chatflow；FE6 与 BE10 共同收口观测。

下面给出每个里程碑的 case 列表。每个 case 都包含：改动文件、预期产出、接受验收。每个 case 视为一个独立的 todo（见 todos 块）。

---

## 3. 前端详细 case（选项 C）

### M-FE1 盘点与桥接策略（1 周）

- FE1-C1 列出 `module-studio-react` 的 12 张页面 / 路由入口与被它依赖的组件：
  - 文件：`src/frontend/packages/module-studio-react/src/**`
  - 产出：`docs/plan-coze-frontend-lib-replacement.md` 的「待替换 page → 目标 Coze 包」映射表。
  - 验收：markdown 成文；12 页面逐条给出 `@coze-agent-ide/*` 目标包名、路由、上游 props。
- FE1-C2 盘点 `packages/agent-ide/*`（48 包）、`packages/common/chat-area/*`（14 包）、`packages/foundation/*`（16 包）目前可用度（exports、peerDeps 冲突、lint 状态）。
  - 产出：`docs/plan-coze-frontend-lib-replacement.md` 的「可直接消费 / 需要补 adapter / 需要补 exports」三类清单。
  - 验收：对每包给出「直用 / 包一层 adapter / 删除」结论。
- FE1-C3 设计 `@atlas/foundation-bridge`（位于 `packages/atlas-foundation-bridge/`）的升级方案：从当前 shim 升级为完整的 `@coze-foundation/foundation-sdk` 兼容实现。
  - 产出：API diff 清单（当前 bridge 导出 vs foundation-sdk 实际导出）+ 三阶段 rollout（alias-only → 双通 → 切断 bridge）。
  - 验收：docs 成文，有迁移步骤。
- FE1-C4 挑选 1 个最小子路径做「探路 spike」：`/org/:orgId/workspaces/:workspaceId/develop/chat` 下的 Agent 调试页，梳理需要哪些 `@coze-agent-ide/*` 包。
  - 产出：spike 文档（不动代码）。
- FE1-C5 产出 `docs/plan-coze-frontend-lib-replacement.md` 总文档（三部分：Foundation 升级、Shell 替换、Agent IDE 替换），各附里程碑列表。

### M-FE2 Foundation & Shell 对齐（2 周）

- FE2-C1 `@atlas/foundation-bridge` 升级（一）：补齐 `@coze-foundation/foundation-sdk` 的 account / space / global 三个 store 的真实 getter。
  - 文件：`src/frontend/packages/atlas-foundation-bridge/src/index.ts`
  - 验收：`pnpm run build` 通过；单测覆盖 3 个 store 的读写对齐。
- FE2-C2 `@atlas/foundation-bridge` 升级（二）：把 account-ui-base / space-ui-base 的基础 hook（`useAccountInfo`、`useCurrentSpace`、`useGlobalConfig`）落地。
  - 验收：全部 hook 返回 Atlas 真实数据，不再 mock。
- FE2-C3 在 `src/frontend/apps/app-web/src/app/app.tsx` 挂载 `@coze-studio/workspace-adapter`：把 `/org/:orgId/workspaces/:workspaceId` 下的 layout 换成 workspace-adapter 提供的 layout。
  - 验收：登录后进入 workspace，顶栏 / 侧栏来自 Coze workspace-adapter，无样式错乱。
- FE2-C4 `@atlas/coze-shell-react` 改 thin wrapper：把 `packages/coze-shell-react/src/**` 精简为包一层 `@coze-foundation/layout` 与 `@coze-foundation/browser-upgrade-banner` 的 React 组件。
  - 验收：老路由仍打得开；顶栏换成 foundation-layout 的实现。
- FE2-C5 登录 / 登出页对齐：`/sign` 替换为 `@coze-foundation/account-ui-base` + `@coze-foundation/account-adapter`。
  - 文件：`src/frontend/apps/app-web/src/app/pages/sign/*`
  - 验收：登录链路端到端可用，JWT + `X-Tenant-Id` 注入不丢。
- FE2-C6 去除 `WorkflowRuntimeBoundary` 硬编码 theme：从 `@coze-foundation/foundation-sdk` 的 `useGlobalConfig().theme` 读。
  - 文件：`src/frontend/apps/app-web/src/app/workflow-runtime-boundary.tsx`
  - 验收：切换主题时 workflow 画布随之切换。
- FE2-C7 E2E 冒烟：`pnpm run test:e2e:app` 跑通「登录 → 进入 workspace → 打开 develop/chat 空壳」路径。

### M-FE3 Agent IDE 替换（一）：layout + context + prompt + model（3 周）

- FE3-C1 挂载 `@coze-agent-ide/layout` + `@coze-agent-ide/layout-adapter` 到 `/agent/:agentId/editor`。
  - 文件：`src/frontend/apps/app-web/src/app/pages/editor-routes.tsx` + `src/app/app.tsx` 的 `AgentEditorRoute`
  - 验收：进入 Agent 编辑器后，左中右三栏骨架来自 Coze，而不是 `AgentWorkbench` 自研。
- FE3-C2 挂载 `@coze-agent-ide/entry` + `@coze-agent-ide/entry-adapter`：左侧能力入口列表。
  - 验收：左侧入口树可点击切换。
- FE3-C3 挂载 `@coze-agent-ide/navigate`：右上 navigate bar（发布、调试、版本）。
  - 验收：navigate 按钮 click 有行为钩子，但不强求全部生效。
- FE3-C4 挂载 `@coze-agent-ide/bot-editor-context-store` + `@coze-agent-ide/context`：Agent 全局状态 store。
  - 验收：旧 `module-studio-react` 的 store 入口迁到 bot-editor-context-store。
- FE3-C5 挂载 `@coze-agent-ide/bot-config-area` + `@coze-agent-ide/bot-config-area-adapter`：中心区 bot 配置面板骨架。
  - 验收：bot 配置区渲染（先空实现允许）；和 store 相通。
- FE3-C6 挂载 `@coze-agent-ide/prompt` + `@coze-agent-ide/prompt-adapter`：提示词编辑器。
  - 验收：提示词编辑 → 本地 store 更新 → 保存草稿调用 Atlas `/api/v1/ai-assistants/{id}`。
- FE3-C7 挂载 `@coze-agent-ide/model-manager`：模型选择下拉。
  - 验收：下拉的模型列表读自 Atlas `/api/v1/ai-models/*`。
- FE3-C8 挂载 `@coze-agent-ide/commons`：共享工具函数层。
  - 验收：其他 agent-ide 包能解析 commons 依赖。
- FE3-C9 删除 `module-studio-react` 里 layout/prompt/model 相关的 12 个自研 page（只删 layout 部分，chat/tool 还保留，后续里程碑再换）。
- FE3-C10 M-FE3 收尾冒烟：`/agent/:agentId/editor` 打开后无控制台报错，提示词 + 模型切换生效。

### M-FE4 Agent IDE 替换（二）：chat + debug + skills + publish（3 周）

- FE4-C1 挂载 `@coze-agent-ide/chat-area-provider` + `@coze-agent-ide/chat-area-provider-adapter`。
- FE4-C2 挂载 `@coze-common/chat-area` + `@coze-common/chat-uikit`：替换 `module-studio-react` 的 `AgentChatPage` Semi 自研聊天。
  - 文件：删掉 `src/frontend/packages/module-studio-react/src/pages/agent-chat-page/**`
  - 验收：聊天 UI 来自 Coze chat-uikit；消息列表、输入框、发送按钮来自 Coze。
- FE4-C3 挂载 `@coze-agent-ide/chat-debug-area` + `@coze-agent-ide/chat-answer-action-adapter`：调试区 + 单条消息的 action。
- FE4-C4 挂载 `@coze-agent-ide/debug-tool-list`：调试期工具列表。
- FE4-C5 挂载 `@coze-agent-ide/chat-background` + `@coze-agent-ide/chat-background-config-content`：背景图。
- FE4-C6 挂载 `@coze-agent-ide/chat-components-adapter`：聊天里引用的业务组件（工作流卡片、插件调用等）。
- FE4-C7 挂载 `@coze-agent-ide/skills-pane-adapter`：右侧技能装配面板。
- FE4-C8 挂载 `@coze-agent-ide/tool` + `@coze-agent-ide/tool-config`：插件/HTTP 工具配置。
- FE4-C9 挂载 `@coze-agent-ide/plugin-area-adapter` + `@coze-agent-ide/plugin-modal-adapter` + `@coze-agent-ide/plugin-content-adapter` + `@coze-agent-ide/plugin-setting-adapter`：插件挂接全链路。
- FE4-C10 挂载 `@coze-agent-ide/workflow-modal` + `@coze-agent-ide/workflow-item` + `@coze-agent-ide/workflow-card-adapter` + `@coze-agent-ide/workflow-as-agent-adapter`：Agent 引用工作流。
- FE4-C11 挂载 `@coze-agent-ide/memory-tool-pane-adapter`：记忆工具。
- FE4-C12 挂载 `@coze-agent-ide/onboarding` + `@coze-agent-ide/onboarding-message-adapter`：引导消息。
- FE4-C13 挂载 `@coze-agent-ide/publish-to-base` + `@coze-agent-ide/agent-publish`：发布到渠道面板。
- FE4-C14 挂载 `@coze-agent-ide/bot-input-length-limit` + `@coze-agent-ide/bot-audit-base` + `@coze-agent-ide/bot-audit-adapter`：输入长度与审计。
- FE4-C15 挂载 `@coze-agent-ide/space-bot`：空间 Bot 管理集成。
- FE4-C16 全量删除已不再引用的 `module-studio-react` 自研 page，只保留 Atlas 特有（等保审计视图、外部连接器视图）。
- FE4-C17 M-FE4 收尾冒烟：Agent IDE 能跑通「改提示词 → 挂工作流 → 调试对话 → 发布飞书」完整链路；发布调用 Atlas 的 `AgentChannelAdapters` 对应渠道。

### M-FE5 Chatflow 特殊交互对齐（2 周）

- FE5-C1 `mode` prop 透传：在 `packages/workflow/adapter/playground/src/page.tsx` 把 `mode` 注入 `WorkflowPlayground`。
  - 验收：chatflow 画布顶栏出现 Coze 原生的 chatflow-only 控件。
- FE5-C2 引入 `@coze-common/chat-workflow-render`：chatflow 运行面板里的消息卡片。
- FE5-C3 引入 `@coze-common/chat-core` + `@coze-common/chat-area-plugin-reasoning`：推理链展示。
- FE5-C4 Chatflow 会话历史面板：读 `/api/v1/open/conversations` + `/messages`（后端 BE3 提供）。
  - 文件：新建 `src/frontend/packages/coze-shell-react/src/chatflow-conversation-sidebar.tsx`
  - 验收：历史 session 列表可点，消息回放正确。
- FE5-C5 Chatflow 中断恢复 UI：在 `WorkflowPlayground` 运行面板里识别 `Interrupt` 事件（来自 BE1-C5），渲染「追问输入框」。
  - 验收：`QuestionAnswer` 节点触发时聊天里出现追问，输入 → 调 `/api/v1/open/workflows/executions/{id}/stream_resume`（BE2-C2 提供）。
- FE5-C6 SSE 5 类事件展示：`Message` 追加 / `Error` 提示 / `Done` 收尾 / `Interrupt` 打断 / `PING` 心跳。
  - 文件：`src/frontend/packages/lowcode-chatflow-adapter/src/sse-event-mapper.ts`
  - 验收：各事件都有 test case，UI 展示正确。
- FE5-C7 Chatflow 运行面板里的 `additional_messages` 注入入口（高级 → 预置消息数组）。
- FE5-C8 `conversation_id` 显式切换：session / conversation 解耦显示。

### M-FE6 Workflow Designer 硬化 + 去 mock（1 周）

- FE6-C1 `WorkflowRuntimeBoundary` user 字段完整化：从 foundation-sdk 读 avatar/email。
- FE6-C2 `workspace-adapter` 注入 `workspaceId / appId` 上下文；`WorkflowPlayground` 能拿到 workspace 信息。
- FE6-C3 Single-node debug 面板对齐 `@coze-workflow/test-run-next/main` + `/form` + `/trace`：单节点试运行 + trace 组件。
- FE6-C4 testset-drawer 去 mock：把 `src/frontend/apps/app-web/src/app/pages/workflow/testset-drawer.tsx` 的 `services/mock` 替换为真实 `/api/v2/workflows/{id}/testsets` 后端。
- FE6-C5 `VITE_LIBRARY_MOCK` 默认关闭；mock 仅在 `import.meta.env.DEV` 生效。
- FE6-C6 节点目录/模板 API 前端面板分组对齐 Coze 7 大类（基础/业务逻辑/输入输出/数据库/知识数据/图像音视频/会话与消息/触发器）。
  - 文件：`src/frontend/apps/app-web/src/app/pages/workflow/node-catalog.tsx`
  - 验收：节点面板的分组与 `BuiltInWorkflowNodeDeclarations` 7 类对齐。
- FE6-C7 `workflow-editor-validation-matrix.md` 统一 `Selector`→`If` 命名（与后端 BE1-C7 同步）。

---

## 4. 后端详细 case（选项 B）

### M-BE1 Coze v6 协议硬化（2 周）

- BE1-C1 `DagWorkflowRunRequest` 新增 `WorkflowVersion` / `ConnectorId` 字段并透传：`PrepareExecutionAsync` 按 version id 加载 canvas。
  - 文件：`src/backend/Atlas.Application/AiPlatform/Models/DagWorkflowModels.cs`、`src/backend/Atlas.Infrastructure/Services/WorkflowEngine/DagWorkflowExecutionService.cs`
  - 验收：xUnit 覆盖 3 个 case：workflow_version 指向已发布版本 / 不存在 / source=draft。
- BE1-C2 `DagWorkflowRunResult` / `CozeWorkflowTestRunResponse` 增加 `DebugUrl` / `ConnectorId` / `LogId`。
  - 文件：`DagWorkflowModels.cs`、`src/backend/Atlas.Presentation.Shared/Controllers/Ai/CozeWorkflowCompatControllerBase.cs`
  - 验收：`.http` 示例更新；`docs/contracts.md` Workflow 段落更新。
- BE1-C3 `RuntimeWorkflowInvokeRequest.VersionId` 真正透传到 `DoInvokeAsync`。
  - 文件：`src/backend/Atlas.Infrastructure/Services/LowCode/RuntimeWorkflowExecutor.cs`
  - 验收：单测覆盖低代码 runtime 指定 version 运行。
- BE1-C4 `CozeWorkflowTestResumeRequest.event_id` 真实消费：`ResumeCoreAsync` 按 event_id 匹配最近 interrupt 记录。
  - 文件：`DagWorkflowExecutionService.cs` + `CozeWorkflowCompatControllerBase.cs`
  - 验收：单测覆盖「resume 未指定 event_id / 指定正确 / 指定错误」三场景。
- BE1-C5 `StreamResumeAsync` 接 body：新增 `POST /api/v2/workflows/executions/{execId}/stream-resume` 的 body 参数（inputsJson、data、variableOverrides）。
  - 文件：`DagWorkflowController.cs` + `DagWorkflowExecutionService.cs`
  - 验收：集成测试 SSE 流式 resume 正确产出 `Message`/`Done`。
- BE1-C6 `RuntimeChatflowInvokeRequest` 增加 `AdditionalMessages[]` + `ConversationId` 一等字段。
  - 文件：`src/backend/Atlas.Application/AiPlatform/Models/RuntimeChatflowDto.cs`
  - 验收：字段到达 `IRuntimeChatflowService`，在 session 恢复时被正确使用。
- BE1-C7 Chatflow SSE 事件从 4 类升 5 类：`Message / Error / Done / Interrupt / PING`。
  - 文件：`src/backend/Atlas.Infrastructure/Services/LowCode/RuntimeSessionAndChatflowServices.cs` 的 `MapEngineEventToChunk`
  - 验收：单测覆盖 `execution_interrupted` → `Interrupt`，心跳每 15s 发 `PING`，完成发 `Done`。
- BE1-C8 `WorkflowNodeType` JSON 输出字符串：统一配置 `JsonStringEnumConverter`。
  - 文件：`src/backend/Atlas.AppHost/Program.cs` JsonSerializerOptions
  - 验收：回归现有 `.http` 用例；前端节点匹配逻辑兼容。
- BE1-C9 `Selector` / `If` 命名统一：保留 `If` 为 API Key，同时补 `Selector` alias（向后兼容）。
  - 文件：`BuiltInWorkflowNodeDeclarations.cs` + `docs/workflow-editor-validation-matrix.md`
  - 验收：前端 node-catalog 两种写法都能解析。

### M-BE2 Workflow Open API 真流 + 异步 + 批量（2 周）

- BE2-C1 `OpenWorkflowsController.StreamRun` 真流改造：桥接 `IDagWorkflowExecutionService.StreamRunAsync`。
  - 文件：`src/backend/Atlas.AppHost/Controllers/Open/OpenWorkflowsController.cs`
  - 验收：SSE 帧与 `/api/v2/workflows/{id}/stream` 一致；`.http` + curl 冒烟通过。
- BE2-C2 新增 `POST /api/v1/open/workflows/executions/{id}/stream_resume`：桥接 `StreamResumeAsync`（含 BE1-C5 的 body）。
- BE2-C3 新增 `POST /api/v1/open/workflows/{id}/async`：桥接 `RuntimeWorkflowExecutor.SubmitAsyncAsync`。
- BE2-C4 新增 `GET /api/v1/open/workflows/async-jobs/{jobId}` + `POST :cancel`。
- BE2-C5 新增 `POST /api/v1/open/workflows/{id}/batch`：桥接 `InvokeBatchAsync`（含 CSV 模板读取）。
- BE2-C6 响应 envelope 对齐 Coze v6：每个 run/stream 返回都带 `debug_url / connector_id / logid / workflow_version`。
- BE2-C7 `OpenScopeHelper` 扩展 scope：`open:workflow:run / :stream / :resume / :async / :batch / :read`。
  - 文件：`src/backend/Atlas.AppHost/Controllers/Open/OpenScopeHelper.cs` + `Atlas.PlatformHost/Controllers/PatsController.cs`（PAT scope 白名单）
  - 验收：PAT 缺 scope 返回 403。
- BE2-C8 新增独立 `OpenChatflowController`（`/api/v1/open/chatflow/stream`）：Coze 官方 SDK 的 chatflow 入口。
  - 文件：`src/backend/Atlas.AppHost/Controllers/Open/OpenChatflowController.cs`（新建）
  - 验收：SSE 5 类事件返回，可被 Coze Python SDK 消费。

### M-BE3 Conversations + Messages Open API（1.5 周）

- BE3-C1 `OpenConversationsController`（新建）
  - `POST /api/v1/open/conversations` 创建
  - `GET /api/v1/open/conversations/{id}` 详情
  - `GET /api/v1/open/conversations` 列表（分页、bot_id/agent_id 过滤）
  - `DELETE /api/v1/open/conversations/{id}`
  - `POST /api/v1/open/conversations/{id}/clear`
  - 桥接 `IRuntimeSessionService`（已存在）。
- BE3-C2 `OpenMessagesController`（新建）
  - `POST /api/v1/open/messages` 创建
  - `GET /api/v1/open/messages?conversation_id=` 列表
  - `GET /api/v1/open/messages/{id}`
  - `PUT /api/v1/open/messages/{id}`
  - `DELETE /api/v1/open/messages/{id}`
  - 桥接消息节点 `CreateMessage / EditMessage / DeleteMessage / MessageList` 共享底层。
- BE3-C3 Coze 兼容字段映射：`chat_history[]` ↔ `messages[]`、`meta_data`、`content_type` 等。
- BE3-C4 `.http`：`src/backend/Atlas.AppHost/Bosch.http/OpenConversations.http`、`OpenMessages.http`。
- BE3-C5 集成测试：用 Coze 官方 Python SDK 一个 demo 脚本完整跑通「创建会话 → 发消息 → 调 chatflow stream → 拉历史」。

### M-BE4 Files Open API 加固（1 周）

- BE4-C1 现有 `OpenFilesController` 盘点：列出实现覆盖率。
- BE4-C2 补齐 `POST /api/v1/open/files/upload`：multipart/form-data → 返回 `file_id`（Coze 官方格式）。
- BE4-C3 补齐 chunked upload：`POST /api/v1/open/files/prepare-upload` + `POST /api/v1/open/files/complete-upload`（对齐 `IRuntimeFileService`）。
- BE4-C4 `GET /api/v1/open/files/retrieve?file_id=` 返回元信息。
- BE4-C5 `DELETE /api/v1/open/files/{id}`。
- BE4-C6 等保 2.0：MIME / 尺寸 / 病毒扫描 policy 统一（复用 `IRuntimeFileService.ValidateUpload`）；每个写接口审计。

### M-BE5 Audio Open API（1 周）

- BE5-C1 `OpenAudioController`（新建）
  - `POST /api/v1/open/audio/transcriptions`（STT）
  - `POST /api/v1/open/audio/speech`（TTS）
  - `POST /api/v1/open/audio/translations`（语音翻译）
  - `POST /api/v1/open/audio/voices`（获取可用音色）
- BE5-C2 `IAudioProviderAdapter`（`Atlas.Application.AiPlatform.Abstractions`）：provider = `doubao_sami / volc_asr / openai_whisper / not_configured`。
- BE5-C3 默认 adapter 返回 `AUDIO_PROVIDER_NOT_CONFIGURED`（对齐 `AgentChannelAdapters` 的未配置范式）。
- BE5-C4 异步化：长音频自动走 Hangfire → 返回 `job_id`，复用 `RuntimeWorkflowBackgroundJob` 基础设施。
- BE5-C5 与 `VideoToAudio` / `VideoFrameExtraction` 节点共享 adapter 层。
- BE5-C6 `.http` + 集成测试。

### M-BE6 Datasets / Knowledge Open API（1.5 周）

- BE6-C1 `OpenDatasetsController`（新建，与现有 `OpenKnowledgeController` 对齐 Coze `datasets` 名）
  - `POST /api/v1/open/datasets`
  - `GET /api/v1/open/datasets`
  - `GET /api/v1/open/datasets/{id}`
  - `PUT /api/v1/open/datasets/{id}`
  - `DELETE /api/v1/open/datasets/{id}`
- BE6-C2 Documents：`POST /api/v1/open/datasets/{id}/documents` + `DELETE` + `GET list` + `batch` 批量。
- BE6-C3 Retrieval 预览：`POST /api/v1/open/datasets/{id}/query` 对齐 `IKnowledgeRetriever`。
- BE6-C4 Images：`GET /api/v1/open/datasets/{id}/images` + `DELETE /images/{imageId}`。
- BE6-C5 Tables：`GET /api/v1/open/datasets/{id}/tables` + `GET /rows` + `POST /rows:query`。
- BE6-C6 `ParsingStrategy` 字段对齐 issue #847：`parsing_type / extract_image / extract_table / image_ocr / sheet_id / header_line / data_start_line / rows_count / caption_type`。
- BE6-C7 Coze 兼容：`OpenKnowledgeController` 继续保留（向后兼容），内部共享 Service 不再双实现。
- BE6-C8 `.http` + 集成测试。

### M-BE7 Bots / Agents Open API（1 周）

- BE7-C1 `OpenBotsController` 盘点。
- BE7-C2 `POST /api/v1/open/bot/create`（桥接 Atlas `AgentCommandService`）。
- BE7-C3 `POST /api/v1/open/bot/publish`（对齐 `AgentChannelAdapters`，channel=`feishu/wechat/douyin/doubao/api/sdk/web`）。
- BE7-C4 `GET /api/v1/open/bot/get_online_info` + `/get_draft_info` + `/list`。
- BE7-C5 `PUT /api/v1/open/bot/update`。
- BE7-C6 `POST /api/v1/open/bot/invoke`（对齐 `/api/v1/open/chat/completions`）。
- BE7-C7 Multi-agent（team）旁路：`/api/v1/open/team-agents/*` 最小 stub，不进入 B MVP 必交付。

### M-BE8 Variables / Triggers / PAT / User Open API（1 周）

- BE8-C1 `OpenVariablesController`：system / user / application 三类变量 CRUD（桥接 Atlas variable services）。
- BE8-C2 `OpenTriggersController`：`POST / GET / DELETE / :pause / :resume`（对齐 `IRuntimeTriggerService`）。
- BE8-C3 Webhook 入口对齐：`POST /api/v1/open/triggers/{id}/webhook`（已在 runtime，补 open 层匿名入口 + `X-Atlas-Webhook-Secret`）。
- BE8-C4 `OpenUserInfoController`：`GET /api/v1/open/user_info`（当前 PAT 对应的 tenant/user）。
- BE8-C5 `OpenPatController`：`GET /api/v1/open/pat/list` + `POST /revoke`（仅允许本人 PAT）。
- BE8-C6 `.http` + 集成测试。

### M-BE9 SDK 双向互通（1 周）

- BE9-C1 `ICozeOpenApiClient`（新建 `src/backend/Atlas.Application/Integration/ICozeOpenApiClient.cs`）。
- BE9-C2 `CozeOpenApiClient`（`Atlas.Infrastructure.Services.Integration.CozeOpenApiClient`）：HttpClient 封装，支持 bearer + tenant 隔离。
- BE9-C3 新节点 `CozeInvokeWorkflow`：在 `BuiltInWorkflowNodeDeclarations.cs` 新增；节点运行时调 Coze 官方 `/v1/workflow/run`。
- BE9-C4 凭据存储：租户级 `CozeOpenApiCredential`（protected by `LowCodeCredentialProtector`）。
- BE9-C5 流式响应兼容：Coze 返回的 SSE → Atlas Chatflow 5 类事件映射。
- BE9-C6 .http 示例 + 一个端到端 demo：Atlas 工作流里嵌一个 Coze 官方 workflow，输出回填到 Atlas 节点。

### M-BE10 观测 + 任务中心 + 回归（1 周）

- BE10-C1 OTel / Meter 扩展：`LowCodeOtelInstrumentation` 追加 Coze Open API 维度（scope、provider、conversation_id）。
- BE10-C2 `/api/v1/runtime/tasks`（`RuntimeTasksController` 已有）扩展：展示 async job / batch job / chatflow resume / audio job 四类任务。
- BE10-C3 审计覆盖：回归所有 Open* 写接口通过 `IAuditWriter`。
- BE10-C4 安全回归：PAT scope 验证、SSRF（Audio / HTTP 节点）、速率限制（per-tenant QPS）。
- BE10-C5 `.http` 总清单 + 集成测试 matrix；`docs/contracts.md` Open API 段落完整更新。

---

## 5. 关键文件与参考代码锚点

- 前端：
  - `src/frontend/apps/app-web/src/app/app.tsx`（appRoutes）
  - `src/frontend/apps/app-web/src/app/pages/editor-routes.tsx`
  - `src/frontend/apps/app-web/src/app/workflow-runtime-boundary.tsx`
  - `src/frontend/packages/atlas-foundation-bridge/src/index.ts`
  - `src/frontend/packages/coze-shell-react/src/**`
  - `src/frontend/packages/workflow/adapter/playground/src/page.tsx`
  - `src/frontend/packages/module-studio-react/src/**`（本次主要被替换）
- 后端：
  - `src/backend/Atlas.AppHost/Controllers/Open/*.cs`（所有新增 Open 控制器落这里）
  - `src/backend/Atlas.AppHost/Controllers/DagWorkflowController.cs`
  - `src/backend/Atlas.AppHost/Controllers/RuntimeChatflowsController.cs`
  - `src/backend/Atlas.Infrastructure/Services/WorkflowEngine/DagWorkflowExecutionService.cs`
  - `src/backend/Atlas.Infrastructure/Services/LowCode/RuntimeSessionAndChatflowServices.cs`
  - `src/backend/Atlas.Infrastructure/Services/LowCode/RuntimeWorkflowExecutor.cs`
  - `src/backend/Atlas.Application/AiPlatform/Models/DagWorkflowModels.cs`
  - `src/backend/Atlas.Application/AiPlatform/Models/RuntimeChatflowDto.cs`
  - `src/backend/Atlas.Presentation.Shared/Controllers/Ai/CozeWorkflowCompatControllerBase.cs`
  - `src/backend/Atlas.Infrastructure/Services/WorkflowEngine/BuiltInWorkflowNodeDeclarations.cs`

## 6. 验收与交付标准（每个 case 必须满足）

- 后端：`dotnet build` 0 警告 0 错误；新增/修改 Open API 同步落 `.http`；必要时补 xUnit；`docs/contracts.md` 对齐。
- 前端：`pnpm run lint`、`pnpm run build`、`pnpm run i18n:check`；E2E 至少一条冒烟路径。
- 文档：PR 说明包含「属于哪个 case、已完成验证、风险与下一步」。
- 回归：每个 case 结束后跑一次 AGENTS.md 指定的最小验证集。

## 7. 风险与回滚

- FE3-FE4 替换期间 `module-studio-react` 与 `@coze-agent-ide/*` 并存窗口较长：保留 feature flag `VITE_USE_COZE_AGENT_IDE`（默认 true，旧壳回退 false）。
- `@atlas/foundation-bridge` 两头跑期间的 store 同步风险：FE2 阶段双写，FE6 阶段一次性切断。
- 后端 Open API 口径变更（如返回体增加字段）默认向后兼容；Coze 兼容层字段若与 v6 冲突，走配置 `CozeV6Compat:Enabled` 开关（默认 true）。
- 任一里程碑验证失败立即停止推进，回退到上一个绿色 commit；不得压力式合并。