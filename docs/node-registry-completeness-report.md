# 节点注册配置完整度评估报告

> 生成日期：2026-05-13
> 检查范围：
> - SecurityPlatform `src/frontend/packages/workflow/playground/src/node-registries`
> - SecurityPlatform `src/frontend/packages/workflow/playground/src/nodes-v2`
> - Coze Studio `frontend/packages/workflow/playground/src/node-registries`

---

## 检查标准

根据 [WorkflowNodeRegistry 接口定义](file:///D:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/workflow/base/src/types/registry.ts#L125-L184)，需要检查的钩子和属性包括：

| 钩子/属性 | 说明 |
|---|---|
| `onInit` | 节点初始化时执行，用于加载远程数据 |
| `checkError` | 检查节点是否有错误信息 |
| `onDispose` | 节点销毁时清理资源 |
| `getNodeInputParameters` | 获取节点输入参数 |
| `getNodeOutputs` | 获取节点输出 |
| `beforeNodeSubmit` | 提交前数据转换 |
| `onCreate` | 节点创建时执行（如创建子画布） |
| `getHeaderExtraOperation` | 节点头部额外操作 |
| `meta.nodeDTOType` | 节点 DTO 类型（必须） |
| `meta.size` | 节点尺寸 |
| `meta.defaultPorts` | 默认端口配置 |
| `meta.nodeMetaPath` | 元数据路径 |
| `meta.outputsPath` | 输出路径 |
| `meta.inputParametersPath` | 输入参数路径 |

---

## 一、SecurityPlatform `node-registries` 目录（34 个注册文件）

### 1. 实现了 onInit / checkError / onDispose 的节点

| 节点 | onInit | checkError | onDispose | 其他钩子 |
|---|---|---|---|---|
| **Plugin** | ✅ | ✅ | ✅ | `getHeaderExtraOperation` |
| **Sub-Workflow** | ✅ | ✅ | ✅ | `onCreate`, `getHeaderExtraOperation` |
| **Database-Create** | ✅ | ❌ | ✅ | - |

### 2. 实现了 onCreate 的节点

| 节点 | onCreate | 说明 |
|---|---|---|
| **Batch** | ✅ | 创建 BatchFunction 子画布节点 |
| **Loop** | ✅ | 创建 LoopFunction 子画布节点 |
| **Sub-Workflow** | ✅ | 初始化 CONVERSATION_NAME 参数回填 |

### 3. 实现了 getOutputPoints 的节点

| 节点 | getOutputPoints | 说明 |
|---|---|---|
| **Break** | ✅ | 返回空数组（无输出） |
| **Continue** | ✅ | 返回空数组（无输出） |

### 4. 缺失所有高级钩子的节点（仅基础 meta + formMeta）

| 节点 | nodeDTOType | size | defaultPorts | nodeMetaPath | outputsPath | inputParametersPath |
|---|---|---|---|---|---|---|
| **Batch** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Break** | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| **Code** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Continue** | ✅ | ✅ | ✅ | ✅ | ❌ | ❌ |
| **End** | ✅ | ✅ | ✅ | ✅ | ❌ | ✅ |
| **HTTP** | ✅ | ✅ | ❌ | ✅ | ✅ | ❌ |
| **If** | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Image-Canvas** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Image-Generate** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Image-Reference** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Input** | ✅ | ✅ | ❌ | ✅ | ✅ | ❌ |
| **Intent** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **Json-Stringify** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Loop** | ✅ | ✅ | ✅ | ✅ | ✅ | ✅ |
| **LTM** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Output** | ✅ | ✅ | ❌ | ✅ | ❌ | ✅ |
| **Question** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Set-Variable** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Start** | ✅ | ✅ | ✅ | ✅ | ✅ | ❌ |
| **Text-Process** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Trigger-Delete** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Trigger-Read** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Trigger-Upsert** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Variable** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Database-Base** | ✅ | ✅ | ❌ | ✅ | ❌ | ❌ |
| **Database-Delete** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Database-Query** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Database-Update** | ✅ | ❌ | ❌ | ❌ | ❌ | ❌ |
| **Dataset-Search** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |
| **Dataset-Write** | ✅ | ✅ | ❌ | ✅ | ✅ | ✅ |

### 5. 缺失最严重的节点（meta 不完整）

| 节点 | 缺失项 |
|---|---|
| **Image-Generate** | 缺少 `size`、`defaultPorts`、`nodeMetaPath`、`outputsPath`、`inputParametersPath` |
| **Image-Reference** | 仅实现 `nodeDTOType`，formMeta 直接抛错 |
| **Database-Delete** | 缺少 `size`、`defaultPorts`、`nodeMetaPath`、`outputsPath`、`inputParametersPath` |
| **Database-Query** | 缺少 `size`、`defaultPorts`、`nodeMetaPath`、`outputsPath`、`inputParametersPath` |
| **Database-Update** | 缺少 `size`、`defaultPorts`、`nodeMetaPath`、`outputsPath`、`inputParametersPath` |

---

## 二、SecurityPlatform `nodes-v2` 目录（14 个注册文件）

### 1. Chat 系列节点（10 个，使用 `createNodeRegistry` 工厂函数）

这些节点通过 [createNodeRegistry](file:///D:/Code/Web_SaaS_Backend/SecurityPlatform/src/frontend/packages/workflow/playground/src/nodes-v2/chat/create-node-registry.ts#L56-L131) 统一创建，共享以下特性：

| 钩子/属性 | 实现情况 |
|---|---|
| `beforeNodeSubmit` | ✅ 工厂函数统一实现 |
| `getInputVariableTag` | ✅ 工厂函数统一实现 |
| `onInit` | ❌ 全部缺失 |
| `checkError` | ❌ 全部缺失 |
| `onDispose` | ❌ 全部缺失 |
| `getNodeInputParameters` | ❌ 全部缺失 |
| `getNodeOutputs` | ❌ 全部缺失 |

**涉及节点：**
- ClearConversationHistory
- CreateConversation
- CreateMessage
- DeleteConversation
- DeleteMessage
- QueryConversationHistory
- QueryConversationList
- QueryMessageList
- UpdateConversation
- UpdateMessage

**meta 完整性：** 所有 chat 节点通过工厂函数统一配置了 `nodeDTOType`、`size`、`nodeMetaPath`、`inputParametersPath`、`helpLink`，但 **缺少 `defaultPorts`、`outputsPath`**。

### 2. nodes-v2 其他节点

| 节点 | onInit | checkError | onDispose | getNodeInputParameters | getNodeOutputs | size | defaultPorts | outputsPath |
|---|---|---|---|---|---|---|---|---|
| **LLM** | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |
| **Variable-Assign** | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |
| **Variable-Merge** | ❌ | ❌ | ❌ | ❌ | ❌ | ✅ | ❌ | ❌ |

---

## 三、与 Coze Studio 的差异对比

### 结论：**SecurityPlatform 的 `node-registries` 目录与 Coze Studio 完全一致**

通过逐行对比，两个项目的以下文件内容完全相同：
- `batch/node-registry.ts`
- `break/node-registry.ts`
- `code/node-registry.ts`
- `continue/node-registry.ts`
- `end/node-registry.ts`
- `http/node-registry.ts`
- `if/node-registry.ts`
- `plugin/node-registry.ts`
- `sub-workflow/node-registry.ts`
- `database/database-create/database-create-node-registry.tsx`
- 以及其他所有 node-registries 下的文件

**Coze Studio 本身也没有为大部分节点实现 onInit / checkError / onDispose 钩子**，只有 3 个节点实现了这些高级钩子：

| Coze Studio 节点 | onInit | checkError | onDispose |
|---|---|---|---|
| **Plugin** | ✅ | ✅ | ✅ |
| **Sub-Workflow** | ✅ | ✅ | ✅ |
| **Database-Create** | ✅ | ❌ | ✅ |

---

## 四、重点标注：缺失钩子的节点

### A. 需要 onInit / checkError / onDispose 但缺失的节点

这些节点涉及**远程数据加载**（如插件详情、子工作流详情、数据库元信息），建议补充：

| 节点 | 当前状态 | 建议 |
|---|---|---|
| **Database-Delete** | 无 onInit/onDispose | 应实现：加载数据库元信息、清除错误状态 |
| **Database-Query** | 无 onInit/onDispose | 应实现：加载数据库元信息、清除错误状态 |
| **Database-Update** | 无 onInit/onDispose | 应实现：加载数据库元信息、清除错误状态 |
| **Dataset-Search** | 无 onInit/onDispose | 应实现：加载知识库元信息、清除错误状态 |
| **Dataset-Write** | 无 onInit/onDispose | 应实现：加载知识库元信息、清除错误状态 |

### B. nodes-v2 全部 Chat 节点缺失生命周期钩子

所有 10 个 Chat 系列节点（通过 `createNodeRegistry` 工厂创建）**均未实现** `onInit`、`checkError`、`onDispose`。如果这些节点需要调用 API 获取会话/消息详情或进行错误状态管理，需要在工厂函数中补充这些钩子。

### C. meta 缺失最严重的节点

| 节点 | 缺失项 | 风险 |
|---|---|---|
| **Image-Generate** | `size`、`defaultPorts`、`nodeMetaPath`、`outputsPath`、`inputParametersPath` | 节点渲染可能异常，变量系统无法正确提取输入输出 |
| **Image-Reference** | 仅 `nodeDTOType`，formMeta 直接抛错 | 占位节点，不可用 |
| **Database-Delete/Query/Update** | `size`、`defaultPorts`、`nodeMetaPath`、`outputsPath`、`inputParametersPath` | 节点初始尺寸、端口、变量提取依赖默认值 |

---

## 五、总结

| 维度 | SecurityPlatform | Coze Studio | 差异 |
|---|---|---|---|
| `node-registries` 钩子实现 | 3/34 节点实现高级钩子 | 3/34 节点实现高级钩子 | **完全一致** |
| `nodes-v2` 钩子实现 | 0/14 节点实现生命周期钩子 | N/A（Coze 无此目录） | SecurityPlatform 独有 |
| `beforeNodeSubmit` | nodes-v2 chat 工厂实现 | 部分节点实现 | nodes-v2 已覆盖 |
| `onCreate` | 3 个节点（Batch/Loop/SubWorkflow） | 3 个节点 | **完全一致** |

**核心结论：** SecurityPlatform 的 `node-registries` 目录与 Coze Studio 完全同步，没有缺失。差异主要体现在 `nodes-v2` 目录是 SecurityPlatform 独有的新架构节点，该目录下所有节点均未实现 `onInit`/`checkError`/`onDispose` 生命周期钩子。
