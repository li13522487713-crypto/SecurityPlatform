# LLM 节点前端 Form 逐行分析 + ChatSDK 完整 API 拆解

> 基于全量源码阅读，逐行级分析
> 日期：2026-04-09

---

# 第一部分：LLM 节点前端 Form 组件逐行分析

## 1. 文件结构总览

LLM 节点前端共 **59 个文件**，位于 `frontend/packages/workflow/playground/src/nodes-v2/llm/`

```
llm/
├── index.ts                    # 包入口，导出 LLM_NODE_REGISTRY
├── llm-node-registry.ts        # 节点注册配置 (47行)
├── llm-form-meta.tsx           # 🔥 核心表单定义 (536行)
├── types.ts                    # FormData 类型 (39行)
├── utils.ts                    # 工具函数
├── node-test.ts                # 测试运行配置
├── index.module.less           # 样式
├── hooks/
│   └── use-model-type.ts       # 模型类型 hook
├── system-prompt/
│   ├── index.tsx               # System Prompt 编辑器组件
│   └── use-skill-libraries.tsx # 技能库数据 hook
├── user-prompt/
│   └── index.tsx               # User Prompt 编辑器组件
├── skills/                     # 🔥 Function Calling 技能面板 (18个文件)
│   ├── index.tsx               # Skills 主组件
│   ├── types.ts                # BoundSkills 类型
│   ├── constants.ts            # 常量
│   ├── data-transformer.ts     # fcParam 前后端数据转换
│   ├── add-skill.tsx           # 添加技能
│   ├── skill-modal.tsx         # 技能选择弹窗
│   ├── bound-item-card.tsx     # 已绑定技能卡片
│   ├── plugin-setting.tsx      # 插件技能设置
│   ├── plugin-setting-form-modal.tsx  # 插件参数编辑弹窗
│   ├── knowledge-setting.tsx   # 知识库技能设置
│   ├── knowledge-setting-form-modal.tsx # 知识库参数编辑弹窗
│   ├── workflow-setting.tsx    # 工作流技能设置
│   ├── input-params-form.tsx   # 输入参数表单
│   ├── output-params-form.tsx  # 输出参数表单
│   ├── async-params-form.tsx   # 异步参数表单
│   ├── skill-knowledge-sider/  # 知识库侧边面板
│   └── 其他辅助文件
├── vision/                     # 视觉能力 (9个文件)
│   ├── index.ts
│   ├── constants.ts
│   ├── components/
│   │   ├── vision.tsx          # Vision 主开关组件
│   │   ├── vision-input-field.tsx
│   │   ├── vision-name-field.tsx
│   │   └── vision-value-field.tsx
│   ├── hooks/
│   │   └── use-model-enabled-types.ts
│   └── utils/
├── cot/                        # Chain-of-Thought 推理 (4个文件)
│   ├── index.ts
│   ├── constants.ts
│   ├── utils.ts
│   └── provide-reasoning-content.ts
└── validators/                 # 校验器 (3个文件)
    ├── index.ts
    ├── llm-output-tree-meta-validator.ts
    └── llm-input-name-validator.ts
```

---

## 2. 节点注册配置 — `llm-node-registry.ts` (47行) 【已确认】

```17:46:frontend/packages/workflow/playground/src/nodes-v2/llm/llm-node-registry.ts
import {
  DEFAULT_NODE_META_PATH,
  type WorkflowNodeRegistry,
} from '@coze-workflow/nodes';
import { StandardNodeType } from '@coze-workflow/base';
// ...
export const LLM_NODE_REGISTRY: WorkflowNodeRegistry<NodeTestMeta> = {
  type: StandardNodeType.LLM,
  meta: {
    nodeDTOType: StandardNodeType.LLM,
    style: { width: 360 },
    size: { width: 360, height: 130.7 },
    test,
    nodeMetaPath: DEFAULT_NODE_META_PATH,
    batchPath: '/batch',
    inputParametersPath: '/$$input_decorator$$/inputParameters',
    getLLMModelIdsByNodeJSON: nodeJSON =>
      nodeJSON.data.inputs.llmParam.find(p => p.name === 'modelType')?.input.value.content,
    helpLink: '/open/docs/guides/llm_node',
  },
  formMeta: LLM_FORM_META,
};
```

**关键配置**:
| 字段 | 值 | 说明 |
|------|-----|------|
| `type` | `StandardNodeType.LLM` | 节点类型标识 |
| `size` | `360 × 130.7` | 画布上的默认尺寸 |
| `inputParametersPath` | `/$$input_decorator$$/inputParameters` | 输入参数在表单中的路径 |
| `batchPath` | `/batch` | 批量配置路径 |
| `getLLMModelIdsByNodeJSON` | 从 `llmParam` 中提取 `modelType` | 模型 ID 提取器 |
| `formMeta` | `LLM_FORM_META` | 表单渲染/校验/数据转换定义 |

---

## 3. FormData 类型 — `types.ts` (39行) 【已确认】

```typescript
export enum BatchMode {
  Single = 'single',
  Batch = 'batch',
}

export interface FormData {
  batchMode: BatchMode;                    // 批量/单条模式
  visionParam?: InputValueVO[];            // 视觉参数
  model?: IModelValue;                     // 模型配置
  $$input_decorator$$: {                   // 输入装饰器
    inputParameters?: InputValueVO[];      // 输入参数列表
    chatHistorySetting?: {                 // 对话历史设置
      enableChatHistory?: boolean;         // 是否启用
      chatHistoryRound?: number;           // 历史轮数
    };
  };
  batch: BatchVO;                          // 批量配置
}
```

**注意**: `FormData` 并未包含所有表单字段。`fcParam`、`$$prompt_decorator$$`、`outputs`、`settingOnError`、`nodeMeta`、`version` 等字段在 `Render` 中通过 `Field name={...}` 动态使用。

---

## 4. 核心表单渲染器 — `llm-form-meta.tsx` 逐行分析 (536行) 【已确认】

### 4.1 Render 函数 (第 97-333 行)

`Render` 是表单的 React 组件，使用 Flowgram 的 `Field` / `FieldArray` 来声明每个表单区块。

**渲染顺序（即用户从上到下看到的面板顺序）**:

| # | 行号 | Field name | 组件 | 用户看到的 UI | 说明 |
|---|------|-----------|------|-------------|------|
| 1 | 105-109 | `nodeMeta` (deps: outputs, batchMode) | `NodeMeta` | 节点标题/描述 | 节点元信息编辑 |
| 2 | 110-119 | `batchMode` | `BatchMode` | "批量模式" Switch | 切换 single/batch |
| 3 | 120-129 | `model` | `ModelSelect` (FormCard) | **模型选择** 下拉框 | 选择 LLM 模型 |
| 4 | 130 | `batch` (dep: batchMode) | `Batch` | 批量配置面板 | 仅 batch 模式显示 |
| 5 | 131-137 | `fcParam` | `Skills` | **技能面板** (Function Calling) | 绑定插件/工作流/知识库作为工具 |
| 6 | 138-275 | `$$input_decorator$$.inputParameters` | `FieldArray` | **输入参数** KV 列表 | 可增删的 name→value 映射 |
| 7 | 239-254 | (内嵌) `$$input_decorator$$.chatHistorySetting` | `ChatHistory` | 对话历史设置 | 仅 Chatflow 模式显示 |
| 8 | 276 | — | `Vision` | **视觉能力** Switch | 非抖音模式显示 |
| 9 | 277-294 | `$$prompt_decorator$$.systemPrompt` | `SystemPrompt` | **System Prompt** 编辑器 | 富文本，支持变量插入 |
| 10 | 295-305 | `$$prompt_decorator$$.prompt` | `UserPrompt` | **User Prompt** 编辑器 | 用户消息模板 |
| 11 | 306-328 | `outputs` (dep: batchMode) | `Outputs` | **输出定义** | 可编辑输出字段 + 响应格式 |
| 12 | 329 | `settingOnError` | `SettingOnError` | **错误处理** 配置 | 失败时行为 |

**关键设计点**:

1. **条件渲染**: `!isBindDouyin` 控制 Skills 和 Vision 是否显示（抖音版不显示）
2. **Chatflow 专属**: `isChatflow` 控制 ChatHistory 设置是否显示
3. **只读模式**: `readonly` 通过 `useReadonly()` 获取，控制所有编辑控件的禁用
4. **Vision 输入过滤**: 输入参数列表中的 Vision 类型输入会被 `isVisionInput` 过滤掉，不在普通输入列表中显示（由独立的 Vision 组件处理）

### 4.2 输入参数区详解 (第 138-275 行)

```
FieldArray name="$$input_decorator$$.inputParameters"
├── defaultValue: [{name: 'input', input: {type: REF}}]
├── 表头: [变量名 | 值]
├── 每行:
│   ├── NodeInputName (变量名输入框, flex:2)
│   │   └── 校验: llmInputNameValidator
│   ├── ValueExpressionInput (值表达式, flex:3)
│   │   └── 校验: createValueExpressionInputValidate({required:true})
│   └── [-] 删除按钮 (非只读时)
├── [ChatHistory] (仅Chatflow)
│   ├── enableChatHistory: Switch
│   └── chatHistoryRound: 数字输入 (默认3)
└── [+] 添加按钮 (非只读时)
    └── append({name:'', input:{type: REF}})
```

### 4.3 校验配置 (第 342-367 行)

| 校验路径 | 校验器 | 说明 |
|----------|--------|------|
| `nodeMeta` | `nodeMetaValidate` | 节点标题非空 |
| `outputs` | `llmOutputTreeMetaValidator` | 输出定义合法性 |
| `$$input_decorator$$.inputParameters.*.name` | `llmInputNameValidator` | 输入变量名唯一性/合法性 |
| `$$input_decorator$$.inputParameters.*.input` | `createValueExpressionInputValidate({required: true})` | 值表达式必填 |
| `batch.inputLists.*.name` | `createNodeInputNameValidate` | 批量输入名唯一 (仅 batch 模式) |
| `$$prompt_decorator$$.prompt` | 自定义函数 | **User Prompt 条件必填**: 当模型 `is_up_required=true` 时必填 |

### 4.4 数据转换 — formatOnInit (第 379-468 行)

**后端 → 表单** 数据转换，在表单初始化时执行:

```
后端 Canvas JSON (node.data)
  │
  ▼ formatOnInit
  │
  ├── inputs.llmParam[] → model: {modelType, temperature, ...}
  │   ├── 遍历 llmParam 数组，调用 reviseLLMParamPair 转 KV
  │   ├── 提取 prompt → $$prompt_decorator$$.prompt
  │   ├── 提取 systemPrompt → $$prompt_decorator$$.systemPrompt
  │   └── 提取 enableChatHistory → $$input_decorator$$.chatHistorySetting
  │
  ├── inputs.inputParameters → $$input_decorator$$.inputParameters
  │   └── 默认: [{name:'input', input:{type:REF}}]
  │
  ├── outputs → outputs
  │   └── 空时默认: [{name:'output', type:String}]
  │   └── 调用 formatReasoningContentOnInit (CoT 推理输出处理)
  │
  ├── inputs.batch → batchMode + batch
  │   └── batchEnable → 'batch' / 'single'
  │
  ├── inputs.fcParam → fcParam
  │   └── formatFcParamOnInit (技能参数转换)
  │
  └── version → version (默认 '3')
```

### 4.5 数据转换 — formatOnSubmit (第 470-534 行)

**表单 → 后端 Canvas JSON** 数据转换，在保存时执行:

```
表单 FormData
  │
  ▼ formatOnSubmit
  │
  ├── model → llmParam[]
  │   ├── modelItemToBlockInput(model, modelMeta) → BlockInput 数组
  │   ├── + BlockInput.createString('prompt', $$prompt_decorator$$.prompt)
  │   ├── + BlockInput.createBoolean('enableChatHistory', ...)
  │   │   └── 非 Chatflow 模式强制 false
  │   ├── + BlockInput.createInteger('chatHistoryRound', ...)
  │   └── + BlockInput.createString('systemPrompt', ...)
  │
  ├── $$input_decorator$$.inputParameters → inputs.inputParameters
  │
  ├── fcParam → inputs.fcParam
  │   └── formatFcParamOnSubmit
  │
  ├── batchMode + batch → inputs.batch
  │   └── batch 模式: {batchEnable:true, ...batchDTO}
  │   └── single 模式: undefined
  │
  ├── outputs → outputs
  │   └── formatReasoningContentOnSubmit (CoT 推理输出处理)
  │
  └── version → '3' (固定)
```

### 4.6 副作用 (effect) 配置 (第 368-377 行)

| Field 路径 | Effect | 说明 |
|-----------|--------|------|
| `nodeMeta` | `fireNodeTitleChange` | 标题变更 → 更新画布上的节点名称 |
| `batchMode` | `createProvideNodeBatchVariables` | 批量模式变更 → 更新变量上下文 |
| `batch.inputLists` | 同上 | 批量输入变更 → 更新变量上下文 |
| `outputs` | `provideNodeOutputVariablesEffect` | 输出变更 → 更新下游节点可用变量 |
| `model` | `provideReasoningContentEffect` | 模型变更 → 更新 CoT 推理输出可用性 |

---

## 5. 复刻 LLM 节点表单的最小实现清单

### 必做表单区块 (简化版)

| # | 区块 | 最小实现 |
|---|------|---------|
| 1 | 模型选择 | 下拉框，从 `/api/admin/config/model/list` 加载 |
| 2 | System Prompt | 文本区域（支持 `{{变量}}` 高亮即可） |
| 3 | User Prompt | 文本区域 |
| 4 | 输入参数 | 可增删的 name-value KV 列表 |
| 5 | 输出定义 | 可增删的 name-type 列表 |

### 可后加

| 区块 | 说明 |
|------|------|
| Function Calling (Skills) | 插件/工作流/知识库工具绑定 — 需要完整技能系统 |
| Vision | 图像输入 — 需要多模态模型支持 |
| Batch Mode | 批量处理 — 需要画布批量子流程 |
| Chat History | 对话历史 — 仅 Chatflow 模式 |
| CoT Reasoning | Chain-of-Thought — 需要特定模型支持 |
| 错误处理 | 失败重试/备用模型 |

### 数据格式要点

**后端期望的 `inputs.llmParam` 格式**:
```json
[
  {"name": "modelType", "input": {"value": {"content": 1}}},
  {"name": "modelId", "input": {"value": {"content": "gpt-4"}}},
  {"name": "temperature", "input": {"value": {"content": 0.7}}},
  {"name": "systemPrompt", "input": {"value": {"content": "You are..."}}},
  {"name": "prompt", "input": {"value": {"content": "{{input}}"}}},
  {"name": "enableChatHistory", "input": {"value": {"content": false}}},
  {"name": "chatHistoryRound", "input": {"value": {"content": 3}}}
]
```

每个参数是 `BlockInput` 格式: `{name, input: {type, value: {content}}}` — `formatOnSubmit` 用 `BlockInput.createString/Boolean/Integer` 构造。

---

# 第二部分：ChatSDK 完整 API 拆解

## 1. 类概览 【已确认】

`ChatSDK` 是 `@coze-common/chat-core` 的核心类，位于 `chat-core/src/chat-sdk/index.ts` (533行)。

**设计模式**: 单例 Map（同一 `bot_id` / `preset_bot` 只创建一个实例）

```
ChatSDK
├── 静态方法
│   ├── create(props) → ChatSDK     # 单例创建
│   ├── getUniqueKey(props)         # 获取实例唯一键
│   └── convertMessageList(data)    # 静态消息格式转换
├── 公开属性
│   ├── biz, bot_id, space_id, preset_bot, user
│   ├── env, deployVersion, bot_version, draft_mode
│   ├── conversation_id, enableDebug, logLevel
│   └── EVENTS (静态 = SdkEventsEnum)
├── 消息创建 API (5个)
│   ├── createTextMessage()
│   ├── createImageMessage()
│   ├── createFileMessage()
│   ├── createTextAndFileMixMessage()
│   └── createNormalizedPayloadMessage()
├── 消息发送 API (2个)
│   ├── sendMessage()
│   └── resumeMessage()
├── 消息管理 API (6个)
│   ├── getHistoryMessage()
│   ├── clearMessageContext()
│   ├── clearHistory()
│   ├── deleteMessage()
│   ├── reportMessage()
│   └── breakMessage()
├── 语音 API (1个)
│   └── chatASR()
├── 插件 API (3个)
│   ├── registerPlugin()
│   ├── checkPluginIsRegistered()
│   └── getRegisteredPlugin()
├── 生命周期 (2个)
│   ├── updateConversationId()
│   └── destroy()
└── 事件 API (2个)
    ├── on(event, fn) → unsubscribe
    └── off(event, fn)
```

---

## 2. 创建参数 — CreateProps 【已确认】

```typescript
type CreateProps = BotUnique & {
  space_id?: string;           // 空间 ID
  biz: Biz;                    // 业务方: 'coze_home' | 'bot_editor' | 'third_part'
  bot_version?: string;        // Bot 版本号
  draft_mode?: boolean;        // 草稿模式 vs 在线模式
  conversation_id: string;     // 会话 ID
  user?: string;               // 用户唯一标识
  scene?: Scene;               // 场景: Default(0)/Explore(1)/BotStore(2)/CozeHome(3)/Playground(4)/...
  env: ENV;                    // 环境: 测试/线上
  deployVersion: DeployVersion; // 部署版本
  enableDebug?: boolean;       // 调试模式 (开启后消息含 debug_messages)
  logLevel?: LogLevel;         // 日志级别: 'disable' | 'info' | 'error'
  requestManagerOptions?: RequestManagerOptions; // 请求拦截器配置
  tokenManager?: TokenManager; // Token 管理 (API Key 认证)
};
```

**BotUnique**: `{bot_id: string}` 或 `{preset_bot: PresetBot}` 二选一

---

## 3. 公开 API 逐一拆解

### 3.1 消息创建 API

| 方法 | 参数 | 返回 | 说明 |
|------|------|------|------|
| `createTextMessage(text, options?)` | 文本内容 | `Message<Text>` | 创建纯文本消息对象 |
| `createImageMessage(props, options?)` | `ImageMessageProps` (含文件/URL) | `Message<Image>` | 创建图片消息（含上传） |
| `createFileMessage(props, options?)` | `FileMessageProps` (含文件) | `Message<File>` | 创建文件消息（含上传） |
| `createTextAndFileMixMessage(...)` | 文本+文件混合 | `Message` | 创建混合消息 |
| `createNormalizedPayloadMessage(props, options?)` | 标准化 payload | `Message<T>` | 通用创建器 |

**所有创建方法**: 委托给 `CreateMessageService` → `PreSendLocalMessageFactory` → 生成带 `local_message_id` 的本地消息对象

### 3.2 消息发送 API

#### `sendMessage(message, options?) → Promise<Message>`

这是最核心的方法，完整调用链:

```
sendMessage(message, options)
  │
  ▼ SendMessageService.sendMessage()
  │
  ├── 合并默认选项: {sendTimeout: 120s, betweenChunkTimeout: 60s, stream: true}
  ├── 按 content_type 分发:
  │   ├── Image → sendImageMessage → 等待上传完成 → sendChannelMessage
  │   ├── File  → sendFileMessage  → 等待上传完成 → sendChannelMessage
  │   └── Text  → sendTextMessage  → sendChannelMessage
  │
  ▼ sendChannelMessage(message, options) → Promise
  │
  ├── 启动超时计时器 (sendTimeout)
  ├── httpChunk.sendMessage(message, {betweenChunkTimeout, headers, requestScene})
  │   └── 内部: fetch → SSE stream → 逐 chunk 解析
  ├── 监听 MESSAGE_SEND_SUCCESS → resolve
  ├── 监听 MESSAGE_SEND_FAIL → reject
  └── 超时 → reject(ChatCoreError '消息发送超时')
```

**SendMessageOptions**:
```typescript
interface SendMessageOptions {
  sendTimeout?: number;         // 发送超时(ms), 默认 120000
  betweenChunkTimeout?: number; // chunk 间超时(ms), 默认 60000
  stream?: boolean;             // 是否流式, 默认 true
  chatHistory?: Message[];      // 自定义历史
  isRegenMessage?: boolean;     // 是否重新生成
  headers?: Record<string, string>; // 额外请求头
}
```

#### `resumeMessage(message, options?)`

用于恢复/继续被中断的消息流，直接调用 `httpChunk.sendMessage` 不等待 Promise resolve。

### 3.3 消息管理 API

| 方法 | HTTP | 说明 |
|------|------|------|
| `getHistoryMessage(params)` | POST `/api/conversation/message/list` | 获取历史消息 |
| `clearMessageContext(params)` | POST | 清除对话上下文 |
| `clearHistory()` | POST | 清除全部历史 |
| `deleteMessage(params)` | POST | 删除指定消息 |
| `reportMessage(params)` | POST | 点赞/点踩消息 |
| `breakMessage(params)` | POST | **中断消息流** → `httpChunk.abort(local_message_id)` + 通知后端 |
| `chatASR(params)` | POST | 语音转文字 |

### 3.4 插件 API

| 方法 | 说明 |
|------|------|
| `registerPlugin(key, plugin, options?)` | 注册插件 (目前仅 `'upload-plugin'`) |
| `checkPluginIsRegistered(key)` | 检查插件是否已注册 |
| `getRegisteredPlugin(key)` | 获取已注册的插件实例 |

### 3.5 生命周期 API

| 方法 | 说明 |
|------|------|
| `updateConversationId(id)` | 动态更新会话 ID |
| `destroy()` | **销毁实例**: 中断流、清除事件、清除缓存、从 instances Map 移除 |

---

## 4. 事件系统 【已确认】

### 4.1 SDK 事件枚举

```typescript
enum SdkEventsEnum {
  MESSAGE_RECEIVED_AND_UPDATE = 'message_received_and_update',
  MESSAGE_PULLING_STATUS = 'message_pulling_status',
  ERROR = 'error',
}
```

### 4.2 事件详解

#### 事件 1: `MESSAGE_RECEIVED_AND_UPDATE`

**触发时机**: 每收到一个非 ACK 的 chunk，经 `ChunkProcessor` 处理后触发

```typescript
// 回调签名
(event: {
  name: SdkEventsEnum;
  data: Message<ContentType>[];  // 当前累积的完整消息
}) => void
```

**调用链**:
```
HttpChunk → SSE chunk 到达
  → HttpChunkService.handleHttpChunkMessageReceived
    → chunkProcessor.addChunkAndProcess(chunk)
    → chunkProcessor.getProcessedMessageByChunk(chunk)
    → chatSdkEventEmit(MESSAGE_RECEIVED_AND_UPDATE, {data: [processedMessage]})
```

#### 事件 2: `MESSAGE_PULLING_STATUS`

**触发时机**: 拉流状态变化

```typescript
// 回调签名
(event: {
  name: SdkEventsEnum;
  data: {
    pullingStatus: PullingStatus; // 'start' | 'pulling' | 'answerEnd' | 'success' | 'error' | 'timeout'
    local_message_id: string;
    reply_id: string;
  };
  error?: ChatCoreError;
  abort?: () => void;  // timeout 时可调用中止
}) => void
```

**状态流转**:
```
start → pulling → pulling → ... → answerEnd → success
                                             → error
                                             → timeout (可 abort)
```

| 状态 | 触发点 | 说明 |
|------|--------|------|
| `start` | `READ_STREAM_START` | 开始读取 SSE 流 |
| `pulling` | `MESSAGE_RECEIVED` (非 answer end) | 收到中间 chunk |
| `answerEnd` | `MESSAGE_RECEIVED` (answer end) | 收到最终回答 |
| `success` | `ALL_SUCCESS` | 整个流完成 |
| `error` | `READ_STREAM_ERROR` (已收到 ACK) | 读流阶段异常 |
| `timeout` | `BETWEEN_CHUNK_TIMEOUT` | chunk 间超时 |

#### 事件 3: `ERROR`

**触发时机**: 全局错误

```typescript
(event: {
  name: SdkEventsEnum;
  data: { error: Error };
}) => void
```

---

## 5. 内部模块架构 【已确认】

```
ChatSDK
├── ReportLog                    # 日志系统 (Slardar 上报)
├── RequestManager               # HTTP 请求管理 (axios 封装)
├── TokenManager                 # API Key 认证 (Bearer header 注入)
├── PreSendLocalMessageFactory   # 消息工厂: 创建本地待发送消息
├── ChunkProcessor               # Chunk 处理器: SSE chunk → 完整消息
│   └── StreamBuffer             # 流缓冲区: 按 reply_id 缓存 chunk
├── HttpChunk                    # HTTP 流通道: fetch → ReadableStream → chunk 解析
│   └── HttpChunkEvents          # 通道事件: FETCH_START/MESSAGE_RECEIVED/ALL_SUCCESS/...
├── MessageManager               # 消息管理: 历史/删除/清除/中断
├── PreSendLocalMessageEventsManager  # 本地消息事件: 上传完成/发送成功/发送失败
├── 5 个 Service 层:
│   ├── CreateMessageService     # 消息创建: text/image/file/mix/normalized
│   ├── SendMessageService       # 消息发送: 上传等待→通道发送→超时处理
│   ├── HttpChunkService         # 流事件处理: chunk→消息→SDK事件分发
│   ├── MessageManagerService    # 消息管理: 历史/删除/中断(含 httpChunk.abort)
│   └── PluginsService           # 插件管理: 注册/查询
└── EventBus (EventEmitter3)     # SDK 事件总线
```

---

## 6. 消息发送完整时序图

```
用户在输入框输入 → 回车发送
  │
  ▼ BotDebugChatArea.onBeforeSubmit()
  │  └── savingInfo.saving ? Toast.warning → 阻止 : 放行
  │
  ▼ ChatArea → ChatSDK.createTextMessage(text)
  │  └── PreSendLocalMessageFactory.create → Message<Text> (含 local_message_id)
  │
  ▼ ChatSDK.sendMessage(message, {stream: true})
  │  └── SendMessageService.sendMessage()
  │     └── sendTextMessage → sendChannelMessage(exposedMessage)
  │        ├── setTimeout(sendTimeout=120s) → 超时 reject
  │        ├── httpChunk.sendMessage(msg, {betweenChunkTimeout, requestScene})
  │        │   └── fetch('POST /api/conversation/chat', {body: JSON, signal})
  │        │       └── Response: SSE event stream
  │        ├── 监听 MESSAGE_SEND_SUCCESS → resolve
  │        └── 监听 MESSAGE_SEND_FAIL → reject
  │
  ▼ HttpChunk 内部
  │  ├── emit(FETCH_START)
  │  ├── 读 ReadableStream
  │  │   ├── emit(READ_STREAM_START)
  │  │   └── 每个 SSE event:
  │  │       └── eventsource-parser 解析 → emit(MESSAGE_RECEIVED, chunk)
  │  ├── 全部读完 → emit(ALL_SUCCESS)
  │  └── 异常:
  │      ├── fetch 失败 → emit(FETCH_ERROR)
  │      ├── 读流失败 → emit(READ_STREAM_ERROR)
  │      └── chunk 间超时 → emit(BETWEEN_CHUNK_TIMEOUT)
  │
  ▼ HttpChunkService (事件处理)
  │  ├── FETCH_START → reportEventsTracer.start
  │  ├── MESSAGE_RECEIVED:
  │  │   ├── chunkProcessor.addChunkAndProcess(chunk)
  │  │   ├── 是 ACK → emit(MESSAGE_SEND_SUCCESS) → sendMessage resolve
  │  │   ├── 非 ACK → chatSdkEventEmit(MESSAGE_RECEIVED_AND_UPDATE, [msg])
  │  │   │            └── 前端 Zustand store 追加消息 → React re-render → 流式文字显示
  │  │   └── 是 answerEnd → pullingStatus='answerEnd'
  │  ├── ALL_SUCCESS → pullingStatus='success', 清除 buffer
  │  ├── FETCH_ERROR → emit(MESSAGE_SEND_FAIL) → sendMessage reject
  │  ├── READ_STREAM_ERROR:
  │  │   ├── 未收到 ACK → emit(MESSAGE_SEND_FAIL) → sendMessage reject
  │  │   └── 已收到 ACK → pullingStatus='error'
  │  └── BETWEEN_CHUNK_TIMEOUT → pullingStatus='timeout' + abort 回调
```

---

## 7. 复刻 ChatSDK 的最小实现清单

### 必做

| 模块 | 最小实现 | 说明 |
|------|---------|------|
| **消息创建** | `createTextMessage(text)` → `{content_type:'text', content, local_message_id}` | 生成唯一 ID 即可 |
| **消息发送** | `sendMessage(msg)` → `fetchStream('/api/conversation/chat', body)` | 用 `@coze-arch/fetch-stream` 或手写 `eventsource-parser` |
| **流处理** | SSE chunk → 消息拼接 → 事件发射 | 核心: `onMessage` 回调追加文本 |
| **事件系统** | `on('message_received')` + `on('pulling_status')` | EventEmitter3 |
| **生命周期** | `create()` 单例 + `destroy()` 清理 | 防止重复创建 |
| **中断** | `breakMessage()` → `AbortController.abort()` | 用户点停止时调用 |

### 可后加

| 模块 | 说明 |
|------|------|
| 图片/文件消息 | 需要上传插件系统 |
| Token 管理 | API Key 认证场景 |
| 日志上报 (Slardar) | 生产监控 |
| 超时管理 | 精细的 sendTimeout / betweenChunkTimeout |
| resumeMessage | 中断恢复 |

### 简化版实现参考

```typescript
class SimpleChatSDK {
  private eventBus = new EventEmitter();
  private abortController?: AbortController;

  constructor(private config: { botId: string; conversationId: string }) {}

  createTextMessage(text: string) {
    return {
      content_type: 'text',
      content: text,
      local_message_id: crypto.randomUUID(),
    };
  }

  async sendMessage(message: any) {
    this.abortController = new AbortController();
    const response = await fetch('/api/conversation/chat', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        bot_id: this.config.botId,
        conversation_id: this.config.conversationId,
        content: message.content,
        content_type: message.content_type,
        stream: true,
      }),
      signal: this.abortController.signal,
    });

    const reader = response.body!.getReader();
    const decoder = new TextDecoder();
    let buffer = '';

    while (true) {
      const { done, value } = await reader.read();
      if (done) break;
      buffer += decoder.decode(value, { stream: true });
      // 解析 SSE events
      const lines = buffer.split('\n');
      buffer = lines.pop() || '';
      for (const line of lines) {
        if (line.startsWith('data:')) {
          const data = JSON.parse(line.slice(5));
          this.eventBus.emit('message', data);
        }
      }
    }
    this.eventBus.emit('complete');
  }

  breakMessage() {
    this.abortController?.abort();
  }

  on(event: string, fn: Function) {
    this.eventBus.on(event, fn);
  }

  destroy() {
    this.abortController?.abort();
    this.eventBus.removeAllListeners();
  }
}
```

---

## 8. 已读文件清单

### LLM 节点
- `llm-form-meta.tsx` (536行) ✅ 完整逐行
- `llm-node-registry.ts` (47行) ✅ 完整
- `types.ts` (39行) ✅ 完整
- `index.ts` (18行) ✅ 完整
- `skills/` 目录结构 (18文件) ✅ 列表
- `vision/` 目录结构 (9文件) ✅ 列表
- `cot/` 目录结构 (4文件) ✅ 列表
- `validators/` 目录结构 (3文件) ✅ 列表

### ChatSDK
- `chat-sdk/index.ts` (533行) ✅ **完整逐行**
- `chat-sdk/types/interface.ts` (219行) ✅ **完整**
- `chat-sdk/events/sdk-events.ts` (25行) ✅ **完整**
- `chat-sdk/services/send-message-service.ts` (333行) ✅ **完整**
- `chat-sdk/services/http-chunk-service.ts` (353行) ✅ **完整**
- `chat-sdk/services/message-manager-service.ts` (182行) ✅ **完整**
- `chat-sdk/services/create-message-service.ts` ✅ 列表
- `chat-sdk/services/plugins-service.ts` ✅ 列表
- `chat-sdk/events/slardar-events.ts` ✅ 列表
