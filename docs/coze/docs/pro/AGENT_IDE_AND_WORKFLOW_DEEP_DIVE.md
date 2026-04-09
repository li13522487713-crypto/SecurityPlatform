# Agent IDE 按钮级逻辑拆解 + 工作流节点表单配置详解

> 基于全量源码阅读，最细粒度拆解每个按钮/面板/节点
> 日期：2026-04-09

---

# 第一部分：Agent IDE 每个按钮逻辑

## 1. Header 区域（顶部栏）

### 1.1 Header 整体结构 【已确认】

```
BotHeader (layout/src/components/header/index.tsx, 145行)
├── [左侧] BackButton + BotInfoCard + ModeSelect
│   ├── BackButton: 返回开发列表
│   ├── BotInfoCard: Bot 头像/名称/描述 (可编辑) + 内嵌 DeployButton
│   └── ModeSelect: 模式切换下拉
└── [右侧] HeaderAddonAfter (layout-adapter/src/header/index.tsx, 79行)
    ├── OriginStatus: 草稿/协作状态
    ├── Divider
    ├── MoreMenuButton: ⋯ 更多菜单
    ├── DeployButton: 发布按钮 (editable 时)
    ├── DuplicateBot: 复制按钮 (!editable 时)
    └── #diff-task-button-container: Diff 任务挂载点
```

### 1.2 逐按钮拆解

---

#### 按钮 1：返回按钮 (BackButton)

| 项目 | 详情 |
|------|------|
| **位置** | Header 最左侧 |
| **组件** | `BackButton` from `@coze-foundation/layout` |
| **触发** | 点击 |
| **动作** | `navigate(\`/space/${spaceID}/develop\`)` — 返回工作空间开发列表 |
| **条件** | 始终显示 |
| **接口** | 无 |
| **状态变更** | 路由跳转，离开当前 Bot 编辑器 |

---

#### 按钮 2：Bot 信息卡片 (BotInfoCard) — 可编辑

| 项目 | 详情 |
|------|------|
| **位置** | Header 左侧，返回按钮右侧 |
| **组件** | `BotInfoCard` (layout/src/components/header/bot-info-card/) |
| **触发** | 点击卡片/编辑图标 |
| **动作** | `editBotInfoFn()` → 打开 `useUpdateAgent` 弹窗 |
| **弹窗内容** | 修改 Bot 名称、描述、头像 |
| **接口** | `DeveloperApi.UpdateDraftBotDisplayInfo` — 保存修改 |
| **成功回调** | `updateBotInfo()` 更新 chat 区 sender info |
| **条件** | `!isReadonly` 时可编辑 |

---

#### 按钮 3：模式切换 (ModeSelect)

| 项目 | 详情 |
|------|------|
| **位置** | Header 左侧，BotInfoCard 右侧 |
| **组件** | `ModeSelect` from `@coze-agent-ide/space-bot/component` |
| **选项** | `BotMode.SingleMode` (Single Agent LLM) / `BotMode.WorkflowMode` (工作流模式) |
| **触发** | 下拉选择 |
| **动作** | 切换 `useBotInfoStore.mode` → 页面重新渲染 SingleMode 或 WorkflowMode |
| **条件** | `diffTask` 存在时隐藏（Diff 模式下不可切换） |
| **接口** | 切换后自动保存 → `UpdateDraftBotDisplayInfo` |
| **getIsDisabled** | 两种模式均始终可用 (`() => false`) |

---

#### 按钮 4：草稿/协作状态 (OriginStatus)

| 项目 | 详情 |
|------|------|
| **位置** | Header 右侧区域开头 |
| **组件** | `OriginStatus` from `@coze-agent-ide/layout` |
| **触发** | 无（纯状态展示） |
| **内容** | 显示 "草稿" / "已发布有改动" / 协作者状态 |
| **条件** | `!isReadonly` 时显示 |

---

#### 按钮 5：更多菜单 (MoreMenuButton) — ⋯ 按钮

| 项目 | 详情 |
|------|------|
| **位置** | Header 右侧 |
| **组件** | `MoreMenuButton` (layout/src/components/header/more-menu-button/, 242行) |
| **触发** | 点击 ⋯ 图标 |
| **下拉菜单内容** | |
| **条件** | `!isEditLocked` (非预览模式) |

**菜单项明细**:

| 菜单项 | 条件 | 动作 |
|--------|------|------|
| **数据分析** | `showPublishManageMenu && FLAGS['bot.studio.publish_management'] && !IS_OPEN_SOURCE` | `navigate(\`/space/${spaceId}/publish/agent/${botId}?tab=analysis\`)` |
| **运行日志** | 同上 | `navigate(...?tab=logs)` |
| **触发器** | 同上 | `navigate(...?tab=triggers)` |
| **在...中打开** | `!hideOpenIn` (有发布渠道+有分享链接) | 展开子菜单 → 各 Connector 的 `share_link` |
| **商店** | `botMarketStatus === BotMarketStatus.Online` | 打开 `/store/agent/${botId}` |

**注意**: OSS 版本中 `IS_OPEN_SOURCE=true` + 无 `bot.studio.publish_management` flag → **此菜单可能完全不显示**

---

#### 按钮 6：发布按钮 (DeployButton)

| 项目 | 详情 |
|------|------|
| **位置** | Header 右侧 |
| **组件** | `DeployButton` (layout/src/components/header/deploy-button/, 111行) |
| **触发** | 点击 |
| **核心 Hook** | `useDeployService` (service.tsx, 66行) |
| **动作** | `sendTeaEvent('bot_publish_button_click')` → `navigate(\`/space/${spaceId}/bot/${botId}/publish\`)` |
| **条件** | `editable` 为 true 时显示（用户有编辑权限） |
| **变更提示** | `hasUnpublishChange` 为 true 时显示黄色圆点 + Tooltip "有未发布的改动" |
| **Disabled** | `getBotDetailIsReadonly()` 为 true 时禁用 |
| **Loading** | 可通过 props 传入 |

**注意**: `DeployButton` **不直接调用发布 API**，而是跳转到发布页面 `/publish`，发布 API 在发布页面中调用

---

#### 按钮 7：复制 Bot (DuplicateBot)

| 项目 | 详情 |
|------|------|
| **位置** | Header 右侧（仅 `!editable` 时显示） |
| **组件** | `DuplicateBot` from `@coze-studio/components` |
| **触发** | 点击 |
| **动作** | `DeveloperApi.DuplicateDraftBot({ bot_id })` → 复制 Bot → Toast 成功 |
| **条件** | `!editable && botInfo && botId` — 无编辑权限时替代发布按钮 |

---

## 2. 配置区域（左侧面板）

### 2.1 Single LLM 模式 — AgentConfigArea 【已确认】

```
AgentConfigArea (entry/src/modes/single-mode/section-area/agent-config-area/index.tsx, 71行)
├── [Header Strip] BotConfigArea (bot-config-area-adapter, 80行)
│   ├── ModelConfigView: 模型选择/配置
│   ├── MonetizeConfigButton: 付费配置 (仅海外版)
│   └── ToolMenu: 工具菜单 (技能可见性管理)
├── [Body - 上半] PromptView: System Prompt 编辑器
└── [Body - 下半] ToolArea: 技能/知识/记忆/对话 配置区
```

### 2.2 逐面板/按钮拆解

---

#### 面板 A：模型选择 (ModelConfigView)

| 项目 | 详情 |
|------|------|
| **位置** | Header Strip 左侧 |
| **组件** | `ModelConfigView` → `SingleAgentModelView` |
| **触发** | 点击展开模型列表 |
| **动作** | 选择模型 → `useModelStore.setConfig({ model: modelId })` → 自动保存 |
| **数据** | `GetModelList` API 返回可用模型列表 |
| **接口** | GET `/api/admin/config/model/list` |
| **Store** | `useModelStore` (Zustand) |

---

#### 面板 B：工具菜单 (ToolMenu)

| 项目 | 详情 |
|------|------|
| **位置** | Header Strip 右侧 |
| **组件** | `ToolMenu` from `@coze-agent-ide/tool` |
| **功能** | 控制技能面板中各 Tool Group 的显示/隐藏 |
| **触发** | 点击菜单图标 → 展开 checkbox 列表 |
| **条件** | `!isReadonly && (SingleMode || WorkflowMode)` |
| **新手引导** | `toolHiddenModeNewbieGuideIsRead` → 首次使用弹 Popover |

---

#### 面板 C：System Prompt 编辑器 (PromptView)

| 项目 | 详情 |
|------|------|
| **位置** | 配置区域 Body 上半部分 |
| **组件** | `PromptView` (prompt-adapter, 60行) → base `PromptView` (prompt 包) |
| **功能** | 编辑 Bot 的 System Prompt |
| **编辑器** | 富文本编辑器，支持 `{变量}` 插入 |
| **Action Bar** | `InsertInputSlotAction` — 插入变量占位符 |
| **Placeholder** | "输入 { 可以插入变量" |
| **Action Buttons** | |

| 按钮 | 条件 | 动作 |
|------|------|------|
| **导入到资源库** (ImportToLibrary) | `!isReadonly` | 将当前 Prompt 保存为资源库资源 |
| **从资源库导入** (PromptLibrary) | `!isReadonly` | 打开 Prompt 资源库选择器，导入已有 Prompt |

| **Store** | `usePersonaStore` — 存储 System Prompt 内容 |
| **自动保存** | 内容变更 → debounce → `UpdateDraftBotDisplayInfo` |

---

#### 面板 D：技能区 — ToolArea (entry/tool-area.tsx, 170行)

ToolArea 使用 `ToolView` + `GroupingContainer` 组织为 4 大分组：

##### D.1 技能组 (Skills)

| 工具 | 组件 | 功能 | 触发 |
|------|------|------|------|
| **插件** | `PluginApisArea` (plugin-area-adapter) | 管理 Bot 绑定的插件 | 点击 → 展开插件列表 → 添加/移除 |
| **工作流** | `WorkflowCard` (workflow-card-adapter) | 管理 Bot 绑定的工作流 | 点击 → 展开工作流选择弹窗 |

**插件添加流程**: 展开 → 搜索/浏览插件列表 → Toggle 启用 → `useBotSkillStore.addPlugin(pluginId)` → 自动保存

**工作流添加流程**: 展开 → 选择工作流 → `useBotSkillStore.addWorkflow(workflowId)` → 自动保存

##### D.2 知识组 (Knowledge)

| 工具 | 组件 | 功能 |
|------|------|------|
| **文本知识库** | `DataSetArea` (formatType=Text) | 绑定文本类知识库 |
| **表格知识库** | `DataSetArea` (formatType=Table) | 绑定表格类知识库 |
| **图片知识库** | `DataSetArea` (formatType=Image) | 绑定图片类知识库 |
| **知识库设置** | `DataSetSetting` (actionNode) | Top-K / 检索策略配置 |

**绑定流程**: 展开 → 选择知识库 → Toggle 启用 → `useBotSkillStore.addDataset(datasetId)` → 自动保存

##### D.3 记忆组 (Memory)

| 工具 | 组件 | 功能 |
|------|------|------|
| **变量存储** | `DataMemory` (toolKey=VARIABLE) | 用户级变量管理 |

##### D.4 对话组 (Dialog)

| 工具 | 组件 | 功能 |
|------|------|------|
| **开场白** | `OnboardingMessage` | 配置 Bot 首次对话的欢迎语 |
| **建议回复** | `SuggestionBlock` | 开关自动建议回复功能 |
| **快捷指令** | `ShortcutToolConfig` + `SkillsModal` | 配置聊天快捷指令 |
| **聊天背景** | `ChatBackground` | 设置调试聊天的背景图 |

---

## 3. 聊天调试区域（右侧面板）

### 3.1 调试区结构 【已确认】

```
AgentChatArea (entry/single-mode/section-area/agent-chat-area.tsx, 98行)
└── BotDebugChatArea (chat-debug-area/src/index.tsx, 177行)
    ├── HeaderNode (标题栏, 含 DebugToolList)
    │   ├── SkillsPane (SingleMode): 技能调试入口
    │   └── MemoryToolPane: 变量查看入口
    ├── ChatArea (@coze-common/chat-area)
    │   ├── OnboardingMessagePop: 开场白展示
    │   ├── MessageList: 消息列表
    │   │   ├── WorkflowRender: 工作流执行结果渲染
    │   │   ├── ReceiveMessageBox: 接收消息气泡
    │   │   └── MessageBoxActionBarAdapter: 消息操作栏
    │   ├── ShortcutBarRender: 快捷指令栏
    │   └── ChatInput: 输入框
    │       ├── 文本输入
    │       ├── 多模态上传 (图片/文件, 上限 10 个)
    │       └── 发送按钮
    └── BotDebugPanel: 调试面板 (Ctrl+K)
```

### 3.2 聊天调试区逐按钮/交互拆解

---

#### 交互 1：发送消息

| 项目 | 详情 |
|------|------|
| **触发** | 输入框回车 或 点击发送按钮 |
| **前置检查** | `onBeforeSubmit()` → 若 `savingInfo.saving` 为 true，Toast.warning "正在自动保存，请稍后" 并阻止发送 |
| **核心** | `ChatSDK.sendMessage()` → `fetchStream()` |
| **接口** | POST `/api/conversation/chat` (SSE) |
| **流式处理** | `eventsource-parser` 解析 → `ChunkProcessor` → 消息追加到列表 |
| **成功** | 消息逐字流式显示在气泡中 |
| **失败** | 错误消息显示在聊天区 |
| **清空输入** | `submitClearInput: !interceptSend` — 未在保存时才清空 |

---

#### 交互 2：多模态上传

| 项目 | 详情 |
|------|------|
| **触发** | 点击上传按钮 或 拖拽文件到输入框 |
| **限制** | `fileLimit={10}` — 最多 10 个文件 |
| **模式切换** | `enableMultimodalUpload` / `enableLegacyUpload` — 快捷指令模板模式下禁用多模态 |
| **提示** | `UploadTooltipsContent` 组件显示上传说明 |

---

#### 交互 3：快捷指令栏 (ShortcutBar)

| 项目 | 详情 |
|------|------|
| **组件** | `ShortcutBarRender` (chat-debug-area/plugins/shortcut/, 117行) |
| **位置** | 输入框上方 |
| **条件** | Workflow 模式下隐藏 (`mode === WorkflowMode → return null`) |
| **数据** | `useBotSkillStore.shortcut.shortcut_list` |
| **交互** | 选中快捷指令 → `onActiveShortcutChange` |
| **副作用** | 模板类快捷指令 → 隐藏输入框 (`setChatInputSlotVisible(false)`) + 禁用多模态上传 |
| **多 Agent** | 快捷指令指定 `agent_id` → `manuallySwitchAgent(agent_id)` |

---

#### 交互 4：开场白 (OnboardingMessagePop)

| 项目 | 详情 |
|------|------|
| **组件** | `OnboardingMessagePop` |
| **条件** | 首次打开聊天、无历史消息时显示 |
| **内容** | 配置的欢迎语 + 可选的建议问题列表 |
| **交互** | 点击建议问题 → 自动填入输入框并发送 |

---

#### 交互 5：消息操作栏 (MessageBoxActionBar)

| 项目 | 详情 |
|------|------|
| **组件** | `MessageBoxActionBarAdapter` → `MessageBoxActionBarFooter` |
| **位置** | 每条消息气泡底部 |
| **操作** | 复制 / 重新生成 / 调试查看 |

---

#### 交互 6：调试面板 (BotDebugPanel)

| 项目 | 详情 |
|------|------|
| **组件** | `BotDebugPanel` (space-bot/component/bot-debug-panel/, 105行) |
| **触发** | `Ctrl+K` / `Cmd+K` 快捷键 |
| **内容** | 懒加载 `@coze-devops/debug-panel` — 调试日志/链路追踪 |
| **关闭** | `onClose` → 恢复页面布局 + 清除 `currentDebugQueryId` |
| **Tea 事件** | `open_debug_panel` |

---

#### 交互 7：初始化失败 (InitFail)

| 项目 | 详情 |
|------|------|
| **条件** | `initStatus === 'initFail'` |
| **UI** | 错误图标 + "加载失败" 文字 + [重试] 链接 |
| **动作** | 点击重试 → `refreshMessageList()` |
| **上报** | Slardar `BotDebugAreaInitFail` error |

---

## 4. 技能面板调试工具 (DebugToolList)

### SingleMode 调试工具

| 工具 | 组件 | 功能 |
|------|------|------|
| **技能调试** | `SkillsPane` (skills-pane-adapter, 130行) | 打开技能调试弹窗 (NavModal) |
| **变量查看** | `MemoryToolPane` (memory-tool-pane-adapter) | 查看/编辑运行时变量 |

**SkillsPane 弹窗菜单**:
| 菜单项 | 内容 |
|--------|------|
| 权限管理 | `PluginPermissionManageList` — 管理插件权限状态 |

### WorkflowMode 调试工具

| 工具 | 组件 | 功能 |
|------|------|------|
| **变量查看** | `MemoryToolPane` | 查看/编辑运行时变量 |

（WorkflowMode 不显示 SkillsPane）

---

# 第二部分：工作流每种节点的表单配置

## 1. 节点类型完整清单 【已确认】

### 1.1 节点分类总览

后端 `backend/domain/workflow/entity/node_meta.go` 定义了 **40+ 种节点类型**，前端 `node-registries/` + `nodes-v2/` 中各有对应的 UI 注册。

| 分类 | 节点类型 | 前端目录 | 后端实现 |
|------|---------|---------|---------|
| **流程控制** | Start (入口) | `node-registries/start/` | `nodes/entry/` |
| | End (结束) | `node-registries/end/` | `nodes/exit/` |
| | If (条件分支) | `node-registries/if/` | `nodes/selector/` |
| | Loop (循环) | `node-registries/loop/` | `nodes/loop/` |
| | Batch (批处理) | `node-registries/batch/` | `nodes/batch/` |
| | Break | `node-registries/break/` | `nodes/loop/break/` |
| | Continue | `node-registries/continue/` | `nodes/loop/continue/` |
| **AI 能力** | LLM (大模型) | `nodes-v2/llm/` | `nodes/llm/` |
| | Intent Detector (意图识别) | `node-registries/intent/` | `nodes/intentdetector/` |
| | Question Answer (问答) | `node-registries/question/` | `nodes/qa/` |
| **数据处理** | Code (代码) | `node-registries/code/` | `nodes/code/` |
| | Text Processor (文本处理) | `node-registries/text-process/` | `nodes/textprocessor/` |
| | JSON Stringify | `node-registries/json-stringify/` | `nodes/json/` |
| | Variable (变量聚合) | `node-registries/variable/` | `nodes/variableaggregator/` |
| | Set Variable (变量赋值) | `node-registries/set-variable/` | `nodes/variableassigner/` |
| **外部交互** | Plugin (插件调用) | `node-registries/plugin/` | `nodes/plugin/` |
| | HTTP Request | `node-registries/http/` | `nodes/httprequester/` |
| | Sub Workflow | `node-registries/sub-workflow/` | `nodes/subworkflow/` |
| **知识库** | Dataset Search (知识检索) | `node-registries/dataset/dataset-search/` | `nodes/knowledge/knowledge_retrieve.go` |
| | Dataset Write (知识写入) | `node-registries/dataset/dataset-write/` | `nodes/knowledge/knowledge_indexer.go` |
| | Knowledge Delete | — | `nodes/knowledge/knowledge_deleter.go` |
| | LTM (长期记忆) | `node-registries/ltm/` | — |
| **数据库** | Database Query | `node-registries/database/database-query/` | `nodes/database/query.go` |
| | Database Insert | `node-registries/database/database-create/` | `nodes/database/insert.go` |
| | Database Update | `node-registries/database/database-update/` | `nodes/database/update.go` |
| | Database Delete | `node-registries/database/database-delete/` | `nodes/database/delete.go` |
| | Custom SQL | `node-registries/database/database-base/` | `nodes/database/customsql.go` |
| **输出** | Output (消息输出) | `node-registries/output/` | `nodes/emitter/` |
| | Input (用户输入) | `node-registries/input/` | `nodes/receiver/` |
| **对话管理** | Create Conversation | `nodes-v2/chat/` | `nodes/conversation/` |
| | Conversation List | 同上 | 同上 |
| | Conversation Update | 同上 | 同上 |
| | Conversation Delete | 同上 | 同上 |
| | Conversation History | 同上 | 同上 |
| | Clear History | 同上 | 同上 |
| | Message List | 同上 | 同上 |
| | Create Message | 同上 | 同上 |
| | Edit Message | 同上 | 同上 |
| | Delete Message | 同上 | 同上 |
| **画布标注** | Comment (注释) | `node-registries/comment/` | 无后端执行 |

---

## 2. 核心节点表单配置详解

### 2.1 LLM 节点 — 大模型调用

**前端**: `nodes-v2/llm/llm-form-meta.tsx` (v2 form engine)

| 表单字段 | 对应后端 Config | UI 控件 | 说明 |
|----------|----------------|---------|------|
| **模型** | `LLMParams.model` | 模型选择器 | 选择使用的 LLM 模型 |
| **System Prompt** | `SystemPrompt` / `LLMParams.system_prompt` | 富文本编辑器 | 支持 {{变量}} 插入 |
| **User Prompt** | `UserPrompt` / `LLMParams.user_prompt` | 富文本编辑器 | 用户消息模板 |
| **输入参数** | `Inputs.inputParameters` | 参数树 (KV) | 节点输入映射 |
| **技能 (Function Calling)** | `FCParam` | 技能面板 | 选择插件/工作流/知识库作为工具 |
| **对话历史** | `ChatHistorySetting` | Switch + 数字输入 | 是否携带历史 + 轮数 |
| **批量模式** | `batchMode` / `batch` | Switch + 配置 | 批处理设置 |
| **视觉** | `Vision` | Switch | 开启视觉能力 |
| **输出** | `outputs` | 输出定义 | 节点输出字段 |
| **响应格式** | `OutputFormat` | 下拉 (Text/Markdown/JSON) | 指定输出格式 |
| **错误处理** | `settingOnError` | 错误处理配置 | 失败时行为 |
| **备用模型** | `BackupLLMParams` | 模型选择器 | 主模型失败时切换 |

**后端 Config**:
```go
type Config struct {
    SystemPrompt      string
    UserPrompt        string
    OutputFormat      OutputFormatType
    LLMParams         vo.LLMParams
    FCParam           vo.FCParam
    BackupLLMParams   *vo.LLMParams
    ChatHistorySetting *vo.ChatHistorySetting
    AssociateStartNodeUserInputFields []string
}
```

---

### 2.2 Code 节点 — 代码执行

**前端**: `node-registries/code/form.tsx`

| 表单字段 | 后端 Config | UI 控件 | 说明 |
|----------|------------|---------|------|
| **输入参数** | Canvas Inputs | 参数树 `InputsParametersField` | 定义代码可用的输入变量 |
| **代码** | `Code` | 代码编辑器 `CodeField` | Python/JavaScript 代码 |
| **语言** | `Language` | 下拉选择 | Python / JavaScript |
| **输出** | Canvas Outputs | 输出定义 | 可编辑输出字段 |

**后端 Config**:
```go
type Config struct {
    Code     string
    Language coderunner.Language
    Runner   coderunner.CodeRunner
}
```

---

### 2.3 Plugin 节点 — 插件调用

**前端**: `node-registries/plugin/form.tsx`

| 表单字段 | 后端 Config | UI 控件 | 说明 |
|----------|------------|---------|------|
| **插件选择** | `PluginID` | 插件选择器 | 从空间插件列表选择 |
| **API 选择** | `ToolID` | API 选择器 | 选择插件内的具体 API |
| **输入参数** | Canvas Inputs (动态) | 动态表单 | 根据 API Schema 自动生成 |
| **批量模式** | `batchMode` / `batch` | Switch + 配置 | 批处理设置 |
| **输出** | Canvas Outputs (只读) | 只读展示 | 由 API 定义决定 |

**后端 Config**:
```go
type Config struct {
    PluginID      string
    ToolID        string
    PluginVersion string
    PluginFrom    bot_common.PluginFrom
}
```

---

### 2.4 If 节点 — 条件分支

**前端**: `node-registries/if/form.tsx`

| 表单字段 | 后端 Config | UI 控件 | 说明 |
|----------|------------|---------|------|
| **条件组** | `Clauses` | `ConditionField` | 多个条件分支 |
| **每个条件** | `OneClauseSchema` | 条件编辑器 | 左值 + 操作符 + 右值 |
| **操作符** | `Operator` | 下拉 | `=`, `!=`, `contain`, `not_contain`, `>`, `<`, `>=`, `<=`, `is_empty`, `is_not_empty`, `length_=`, ... |
| **条件关系** | `relation` | AND/OR 切换 | 多条件间的逻辑关系 |
| **Else 分支** | `else` | `ElseField` | 默认分支 |
| **输出** | 只读 | 各分支输出 | 每个条件分支一个输出端口 |

**后端 Config**:
```go
type Config struct {
    Clauses []OneClauseSchema
}
type OneClauseSchema struct {
    Single *SingleClauseSchema   // 单条件: Left Op Right
    Multi  *MultiClauseSchema    // 多条件: Relation + []Operators
}
```

---

### 2.5 Knowledge 检索节点

**前端**: `node-registries/dataset/dataset-search/`

| 表单字段 | 后端 Config | UI 控件 | 说明 |
|----------|------------|---------|------|
| **知识库** | `KnowledgeIDs` | 知识库多选 | 选择一个或多个知识库 |
| **查询内容** | Canvas Input (`query`) | 文本输入/变量引用 | 检索的查询文本 |
| **Top-K** | `RetrievalStrategy.topK` | 数字输入 | 返回前 K 条结果 |
| **最低分数** | `RetrievalStrategy.minScore` | 数字输入 | 过滤低分结果 |
| **使用重排** | `RetrievalStrategy.useRerank` | Switch | 是否启用重排 |
| **使用重写** | `RetrievalStrategy.useRewrite` | Switch | 是否启用查询重写 |
| **检索策略** | `RetrievalStrategy.strategy` | 下拉 | 向量/全文/混合 |
| **对话历史** | `ChatHistorySetting` | Switch + 数字 | 检索时是否携带历史 |

---

### 2.6 Start 节点

**前端**: `node-registries/start/form.tsx`

| 表单字段 | 后端 Config | UI 控件 | 说明 |
|----------|------------|---------|------|
| **输出参数** | `DefaultValues` | 参数定义列表 | 定义工作流的输入变量 |
| **自动保存历史** | `auto_save_history` | Switch | 是否自动保存对话历史 |
| **触发器** (Tab) | Trigger config | 定时触发表单 | Cron/时区/固定参数 |

---

### 2.7 End 节点

**前端**: `node-registries/end/form.tsx`

| 表单字段 | 后端 Config | UI 控件 | 说明 |
|----------|------------|---------|------|
| **终止方式** | `TerminatePlan` | 单选 | 返回变量 vs 使用回答内容 |
| **输入参数** | Canvas Inputs | 参数树 | 最终返回的变量映射 |
| **回答内容** | `Template` | 文本编辑器 | 终止时的输出文本 |
| **流式输出** | Streaming switch | Switch | 是否流式返回 |

---

### 2.8 Loop 节点

**前端**: `node-registries/loop/form.tsx` → `LoopFormRender`

| 表单字段 | 后端 Config | UI 控件 | 说明 |
|----------|------------|---------|------|
| **循环类型** | `LoopType` | 下拉 | 计数循环 / 数组遍历 |
| **循环次数** | `loopCount` | 数字输入 | 计数循环时的次数 |
| **输入参数** | `InputArrays` | 参数树 | 数组类型输入 |
| **中间变量** | `IntermediateVars` | 变量定义列表 | 循环体内的临时变量 |
| **输出** | Canvas Outputs | 输出定义 | 循环结果聚合 |

**特殊**: Loop 节点有 `subCanvas` — 循环体是一个嵌套画布

---

### 2.9 Batch 节点

**前端**: `node-registries/batch/form.tsx` → `BatchFormRender`

| 表单字段 | 后端 Config | UI 控件 | 说明 |
|----------|------------|---------|------|
| **并发数** | `ConcurrentSize` | 数字输入 | 并行执行数量 |
| **批大小** | `BatchSize` | 数字输入 | 每批处理数量 |
| **输入参数** | Canvas Inputs | 参数树 | 数组类型输入 |
| **输出** | Canvas Outputs | 输出定义 | 批处理结果 |

**特殊**: Batch 节点有 `subCanvas` — 批处理函数是嵌套画布

---

### 2.10 HTTP Request 节点

**前端**: `node-registries/http/`

| 表单字段 | 后端 Config | UI 控件 | 说明 |
|----------|------------|---------|------|
| **HTTP 方法** | `Method` | 下拉 (GET/POST/PUT/DELETE/PATCH) | 请求方法 |
| **URL** | `URLConfig.Tpl` | 文本输入(支持变量) | 请求地址模板 |
| **认证** | `AuthConfig` | 认证配置 | Bearer / Custom Header |
| **Headers** | `MD5FieldMapping` | KV 列表 | 请求头 |
| **Query 参数** | `MD5FieldMapping` | KV 列表 | URL 参数 |
| **Body 类型** | `BodyConfig.BodyType` | 下拉 (JSON/RAW/FORM_DATA/EMPTY) | 请求体格式 |
| **Body 内容** | `BodyConfig.Tpl` | 编辑器(按类型) | 请求体内容 |
| **超时** | `Timeout` | 数字输入 | 超时时间(秒) |
| **重试次数** | `RetryTimes` | 数字输入 | 失败重试次数 |

---

### 2.11 Question Answer 节点

**前端**: `node-registries/question/form.tsx`

| 表单字段 | 后端 Config | UI 控件 | 说明 |
|----------|------------|---------|------|
| **模型** | `LLMParams` | 模型选择器 | 用于理解用户回答的模型 |
| **输入参数** | Canvas Inputs | 参数树 | 输入变量 |
| **问题文本** | `QuestionTpl` | 文本编辑器 | 向用户提出的问题 |
| **回答类型** | `AnswerType` | 下拉 | 自由文本 / 选择题 |
| **选项** | `FixedChoices` | 选项列表编辑 | 选择题的选项 |
| **最大回答次数** | `MaxAnswerCount` | 数字输入 | 允许用户回答的次数 |
| **输出** | Dynamic outputs | 动态输出端口 | 根据回答类型生成 |

---

### 2.12 Intent Detector 节点

**前端**: `node-registries/intent/`

| 表单字段 | 后端 Config | UI 控件 | 说明 |
|----------|------------|---------|------|
| **意图列表** | `Intents` | 标签编辑器 | 定义可识别的意图 |
| **模式** | `IsFastMode` | 切换 (普通/极速) | 极速模式牺牲精度提升速度 |
| **模型** | `LLMParams` | 模型选择器 | 用于意图分类的模型 |
| **System Prompt** | `SystemPrompt` | 文本编辑器 | 额外的系统提示 |
| **对话历史** | `ChatHistorySetting` | Switch + 数字 | 是否携带历史上下文 |

---

### 2.13 Database 节点族

#### Database Query

| 表单字段 | 后端 Config | 说明 |
|----------|------------|------|
| **数据库** | `DatabaseInfoID` | 选择数据库连接 |
| **查询字段** | `QueryFields` | 选择要返回的列 |
| **筛选条件** | `ClauseGroup` | 条件组(WHERE) |
| **排序** | `OrderClauses` | ORDER BY 配置 |
| **限制** | `Limit` | LIMIT 数值 |

#### Database Insert

| 表单字段 | 后端 Config | 说明 |
|----------|------------|------|
| **数据库** | `DatabaseInfoID` | 选择数据库连接 |
| **列值** | Canvas Inputs | 动态表单(按表结构) |

#### Database Update

| 表单字段 | 后端 Config | 说明 |
|----------|------------|------|
| **数据库** | `DatabaseInfoID` | 选择数据库连接 |
| **筛选条件** | `ClauseGroup` | WHERE 条件 |
| **更新值** | Canvas Inputs | 要更新的字段 |

#### Database Delete

| 表单字段 | 后端 Config | 说明 |
|----------|------------|------|
| **数据库** | `DatabaseInfoID` | 选择数据库连接 |
| **筛选条件** | `ClauseGroup` | WHERE 条件 |

#### Custom SQL

| 表单字段 | 后端 Config | 说明 |
|----------|------------|------|
| **数据库** | `DatabaseInfoID` | 选择数据库连接 |
| **SQL 模板** | `SQLTemplate` | SQL 编辑器，支持 `{{var}}` 变量 |

---

### 2.14 其他节点

| 节点 | 表单字段 | 说明 |
|------|---------|------|
| **Output (消息输出)** | `Template` (文本模板) | 中间输出文本给用户 |
| **Input (用户输入)** | `OutputSchema` (JSON Schema) | 等待用户结构化输入 |
| **Sub Workflow** | `WorkflowID`, `WorkflowVersion` | 选择子工作流 + I/O 映射 |
| **Text Processor** | `Type` (concat/split), `Tpl`/`ConcatChar`/`Separators` | 文本拼接或分割 |
| **JSON Stringify/Parse** | 无额外字段 | I/O 在画布上定义 |
| **Variable Aggregator** | `MergeStrategy`, `GroupLen`, `GroupOrder` | 多分支变量合并策略 |
| **Set Variable** | `Pairs[]` ({Left, Right}) | 变量赋值映射 |
| **Comment** | 无 | 画布注释，不执行 |
| **Break/Continue** | 无 | 循环控制，无额外配置 |

---

## 3. 节点注册机制 【已确认】

### 前端注册流程

```
node-registries/xxx/node-registry.ts
  → export XXX_NODE_REGISTRY: WorkflowNodeRegistry = {
      type: StandardNodeType.Xxx,
      meta: { nodeDTOType, size, style, helpLink, ... },
      formMeta: XXX_FORM_META,  // 表单渲染器
    }

node-registries/index.ts → barrel re-export

nodes-v2/constants.ts → NODES_V2 数组
  → import 所有 *_NODE_REGISTRY
  → WorkflowNodesV2Contribution.registerFlowNodes(NODES_V2)
```

**`WorkflowNodeRegistry`** 类型 (来自 `@coze-workflow/base`):
- `type`: 节点类型标识
- `meta`: 尺寸/样式/测试配置/路径映射
- `formMeta`: 表单渲染定义 (包含 `render` 函数或组件)
- 可选 hooks: `onInit`, `useDynamicPort`, `subCanvas`

### 后端适配流程

```
entity/node_meta.go → NodeTypeMetas map[NodeType]*NodeTypeMeta
  → 注册每种 NodeType 的 Factory/Name/Description

compose/node_builder.go → 根据 NodeType 选择 Builder
  → Builder.Adapt(vo.Node) → Config struct
  → Builder.Build(Config) → compose.Runnable
```

---

## 4. 复刻工作流编辑器的建议

### 必做 (MVP 5-10 种节点)

| 节点 | 理由 | 表单复杂度 |
|------|------|----------|
| Start | 所有工作流必须 | 低 — 只有输出变量定义 |
| End | 所有工作流必须 | 低 — 终止方式 + 输出映射 |
| LLM | 核心 AI 能力 | **高** — 模型+Prompt+FC+历史 |
| Code | 灵活性基础 | 中 — 代码编辑器 |
| If | 流程控制基础 | 中 — 条件编辑器 |
| Plugin | 外部能力集成 | 中 — 动态表单 |
| HTTP Request | 通用 API 调用 | 中 — URL/Header/Body |
| Output | 中间结果展示 | 低 — 文本模板 |

### 可后加

| 节点 | 理由 |
|------|------|
| Loop / Batch | 复杂度高(嵌套画布) |
| Knowledge | 需要 RAG 基础设施 |
| Database | 需要数据库管理系统 |
| Question / Intent | 需要对话交互系统 |
| Sub Workflow | 需要工作流版本管理 |
| Conversation 系列 | 需要对话管理系统 |

### 画布实现建议

| 原系统 | 替代方案 | 理由 |
|--------|---------|------|
| Fabric.js + flowgram | **React Flow** | 成熟的 React 节点编辑器，社区活跃 |
| Inversify DI | **React Context** | 简化架构，MVP 无需 DI |
| 300+ 节点注册文件 | **手写 8-10 个** | 每个节点一个 form 组件 |
| Zustand Entity | **useReducer / Context** | 简化状态管理 |

---

## 5. 已读文件清单（本次新增）

### Agent IDE
- `layout-adapter/src/header/index.tsx` ✅ (79行)
- `layout-adapter/src/header/mode-list.tsx` ✅ (41行)
- `layout-adapter/src/layout/base.tsx` ✅ (68行)
- `layout/src/components/header/index.tsx` (BotHeader) ✅ (145行)
- `layout/src/components/header/deploy-button/index.tsx` ✅ (111行)
- `layout/src/components/header/deploy-button/hooks/service.tsx` ✅ (66行)
- `layout/src/components/header/more-menu-button/index.tsx` ✅ (242行)
- `entry/src/modes/single-mode/section-area/agent-config-area/index.tsx` ✅ (71行)
- `entry/src/modes/single-mode/section-area/agent-config-area/tool-area.tsx` ✅ (170行)
- `entry/src/modes/single-mode/section-area/agent-chat-area.tsx` ✅ (98行)
- `bot-config-area-adapter/src/bot-config-area.tsx` ✅ (80行)
- `prompt-adapter/src/index.tsx` ✅ (60行)
- `prompt/src/index.ts` ✅ (24行)
- `chat-debug-area/src/index.tsx` ✅ (177行)
- `chat-debug-area/src/plugins/shortcut/index.tsx` ✅ (117行)
- `chat-debug-area/src/context/index.tsx` ✅ (30行)
- `skills-pane-adapter/src/components/skills-pane/index.tsx` ✅ (130行)
- `entry-adapter/src/components/single-mode-tool-pane-list/index.tsx` ✅ (48行)
- `entry-adapter/src/components/workflow-mode-tool-pane-list/index.tsx` ✅ (43行)
- `space-bot/src/component/bot-debug-panel/index.tsx` ✅ (105行)
- `common/chat-area/chat-core/src/chat-sdk/index.ts` (前200行) ✅

### 工作流节点
- `backend/domain/workflow/entity/node_meta.go` ✅ 完整
- `backend/domain/workflow/internal/nodes/` 全部 22 个目录 ✅
- 14 个核心节点实现的 Config 结构 ✅ 深读
- `frontend/packages/workflow/playground/src/node-registries/` 全部目录列表 ✅
- 10 种核心节点的 form-meta / form.tsx ✅ 结构级阅读
- `nodes-v2/llm/llm-form-meta.tsx` ✅
- `nodes-v2/constants.ts` (注册机制) ✅
