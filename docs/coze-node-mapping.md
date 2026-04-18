# Atlas WorkflowNodeType ↔ Coze StandardNodeType 对照表（M4）

> 上游基准：`e:/codeding/coze-studio/frontend/packages/workflow/base/src/types/node-type.ts` 的 `StandardNodeType`（字符串数字 ID `"1"`~`"59"`）。
> Atlas 基准：`Atlas.Domain.AiPlatform.Enums.WorkflowNodeType`（C# 枚举，整数值与上游 `NodeType` 对齐）。
> 兼容层路由 `/api/workflow_api/node_type` 会通过 `ToCozeNodeTypeCode` 把 Atlas 枚举转成 Coze 字符串数字 ID 给前端节点面板。
>
> 本表是 M4 的唯一权威节点对照，节点新增/废弃必须同步更新本文件、`BuiltInWorkflowNodeDeclarations`、`NodeExecutorRegistry._executorTypes`、`AiRuntimeServiceRegistration` 中的 DI 行。

## 1. 已对齐节点（节点目录会真实暴露给前端）

| Coze ID | Coze type | Atlas 枚举 | Atlas Key | 执行器 | DI 注册 |
|---|---|---|---|---|---|
| 1 | Start | `Entry` | `Entry` | `EntryNodeExecutor` | ✅ |
| 2 | End | `Exit` | `Exit` | `ExitNodeExecutor` | ✅ |
| 3 | LLM | `Llm` | `Llm` | `LlmNodeExecutor` | ✅ |
| 4 | Api | `Plugin` | `Plugin` | `PluginNodeExecutor` | ✅ |
| 5 | Code | `CodeRunner` | `CodeRunner` | `CodeRunnerNodeExecutor` | ✅ |
| 6 | Dataset | `KnowledgeRetriever` | `KnowledgeRetriever` | `KnowledgeRetrieverNodeExecutor` | ✅ |
| 8 | If | `Selector` | `If` | `SelectorNodeExecutor` | ✅ |
| 9 | SubWorkflow | `SubWorkflow` | `SubWorkflow` | `SubWorkflowNodeExecutor` | ✅ |
| 13 | Output | `OutputEmitter` | `OutputEmitter` | `OutputEmitterNodeExecutor` | ✅ |
| 15 | Text | `TextProcessor` | `TextProcessor` | `TextProcessorNodeExecutor` | ✅ |
| 18 | Question | `QuestionAnswer` | `QuestionAnswer` | `QuestionAnswerNodeExecutor` | ✅ |
| 19 | Break | `Break` | `Break` | `BreakNodeExecutor` | ✅ |
| 20 | SetVariable | `VariableAssignerWithinLoop` | `VariableAssignerWithinLoop` | `VariableAssignerWithinLoopNodeExecutor` | ✅（M4 新增 DI） |
| 21 | Loop | `Loop` | `Loop` | `LoopNodeExecutor` | ✅ |
| 22 | Intent | `IntentDetector` | `IntentDetector` | `IntentDetectorNodeExecutor` | ✅ |
| 27 | DatasetWrite | `KnowledgeIndexer` | `KnowledgeIndexer` | `KnowledgeIndexerNodeExecutor` | ✅ |
| 28 | Batch | `Batch` | `Batch` | `BatchNodeExecutor` | ✅ |
| 29 | Continue | `Continue` | `Continue` | `ContinueNodeExecutor` | ✅ |
| 30 | Input | `InputReceiver` | `InputReceiver` | `InputReceiverNodeExecutor` | ✅ |
| 31 | Comment | `Comment` | `Comment` | —（前端纯展示） | n/a |
| 32 | VariableMerge | `VariableAggregator` | `VariableAggregator` | `VariableAggregatorNodeExecutor` | ✅ |
| 37 | QueryMessageList | `MessageList` | `MessageList` | `MessageListNodeExecutor` | ✅ |
| 38 | ClearContext | `ClearConversationHistory` | `ClearConversationHistory` | `ClearConversationHistoryNodeExecutor` | ✅ |
| 39 | CreateConversation | `CreateConversation` | `CreateConversation` | `CreateConversationNodeExecutor` | ✅ |
| 40 | VariableAssign | `AssignVariable` | `AssignVariable` | `AssignVariableNodeExecutor` | ✅ |
| 41 | -（自定义 SQL） | `DatabaseCustomSql` | `DatabaseCustomSql` | `DatabaseCustomSqlNodeExecutor` | ✅ |
| 42 | DatabaseUpdate | `DatabaseUpdate` | `DatabaseUpdate` | `DatabaseUpdateNodeExecutor` | ✅ |
| 43 | DatabaseQuery | `DatabaseQuery` | `DatabaseQuery` | `DatabaseQueryNodeExecutor` | ✅ |
| 44 | DatabaseDelete | `DatabaseDelete` | `DatabaseDelete` | `DatabaseDeleteNodeExecutor` | ✅ |
| 45 | Http | `HttpRequester` | `HttpRequester` | `HttpRequesterNodeExecutor` | ✅ |
| 46 | DatabaseCreate | `DatabaseInsert` | `DatabaseInsert` | `DatabaseInsertNodeExecutor` | ✅ |
| 51 | UpdateConversation | `ConversationUpdate` | `ConversationUpdate` | `ConversationUpdateNodeExecutor` | ✅ |
| 52 | DeleteConversation | `ConversationDelete` | `ConversationDelete` | `ConversationDeleteNodeExecutor` | ✅ |
| 53 | QueryConversationList | `ConversationList` | `ConversationList` | `ConversationListNodeExecutor` | ✅ |
| 54 | QueryConversationHistory | `ConversationHistory` | `ConversationHistory` | `ConversationHistoryNodeExecutor` | ✅ |
| 55 | CreateMessage | `CreateMessage` | `CreateMessage` | `CreateMessageNodeExecutor` | ✅ |
| 56 | UpdateMessage | `EditMessage` | `EditMessage` | `EditMessageNodeExecutor` | ✅ |
| 57 | DeleteMessage | `DeleteMessage` | `DeleteMessage` | `DeleteMessageNodeExecutor` | ✅ |
| 58 | JsonStringify | `JsonSerialization` | `JsonSerialization` | `JsonSerializationNodeExecutor` | ✅ |
| 59 | JsonParser | `JsonDeserialization` | `JsonDeserialization` | `JsonDeserializationNodeExecutor` | ✅ |
| 60（Atlas 私有） | - | `Agent` | `Agent` | `AgentNodeExecutor` | ✅ |
| 61（Atlas 私有） | - | `KnowledgeDeleter` | `KnowledgeDeleter` | `KnowledgeDeleterNodeExecutor` | ✅（M4 新增 DI） |
| 62（Atlas 私有） | - | `Ltm` | `Ltm` | `LtmNodeExecutor` | ✅ |
| 11 | Variable | `Variable` | `Variable` | `VariableNodeExecutor` | ✅（P0-3 M20） |
| 14 | ImageGenerate | `ImageGenerate` | `ImageGenerate` | `ImageGenerateUpstreamNodeExecutor` | ✅（P0-3 M20） |
| 15 → 67（私有） | Imageflow | `Imageflow` | `Imageflow` | `ImageflowNodeExecutor` | ✅（P0-3 M20） |
| 16 | ImageReference | `ImageReference` | `ImageReference` | `ImageReferenceNodeExecutor` | ✅（P0-3 M20） |
| 17 / 23 | ImageCanvas | `ImageCanvas` | `ImageCanvasUpstream` | `ImageCanvasUpstreamNodeExecutor` | ✅（P0-3 M20） |
| 24 | SceneVariable | `SceneVariable` | `SceneVariable` | `SceneVariableNodeExecutor` | ✅（P0-3 M20） |
| 25 | SceneChat | `SceneChat` | `SceneChat` | `SceneChatNodeExecutor` | ✅（P0-3 M20） |
| 26 | LTM | `LtmUpstream` | `LtmUpstream` | `LtmUpstreamNodeExecutor` | ✅（P0-3 M20，与 Atlas Ltm(62) 联动） |
| 34 | Trigger（Coze 计划中） | `TriggerUpsert` | `TriggerUpsert` | `TriggerUpsertNodeExecutor` | ✅（P0-2 M12） |
| 35 | Trigger（Coze 计划中） | `TriggerRead` | `TriggerRead` | `TriggerReadNodeExecutor` | ✅（P0-2 M12） |
| 36 | Trigger（Coze 计划中） | `TriggerDelete` | `TriggerDelete` | `TriggerDeleteNodeExecutor` | ✅（P0-2 M12） |
| 64（Atlas 私有，拆分自 Ltm） | - | `MemoryRead` | `MemoryRead` | `MemoryReadNodeExecutor` | ✅（P0-3 M20） |
| 65（Atlas 私有，拆分自 Ltm） | - | `MemoryWrite` | `MemoryWrite` | `MemoryWriteNodeExecutor` | ✅（P0-3 M20） |
| 66（Atlas 私有，拆分自 Ltm） | - | `MemoryDelete` | `MemoryDelete` | `MemoryDeleteNodeExecutor` | ✅（P0-3 M20） |
| 47（Atlas 私有 N47） | - | `VideoGeneration` | `VideoGeneration` | `VideoGenerationNodeExecutor` | ✅（P0-3 M20） |
| 48（Atlas 私有 N48） | - | `VideoToAudio` | `VideoToAudio` | `VideoToAudioNodeExecutor` | ✅（P0-3 M20） |
| 49（Atlas 私有 N49） | - | `VideoFrameExtraction` | `VideoFrameExtraction` | `VideoFrameExtractionNodeExecutor` | ✅（P0-3 M20） |
| 68（Atlas 私有 N44） | - | `ImageGeneration` | `ImageGeneration` | `ImageGenerationNodeExecutor` | ✅（P0-3 M20） |
| 69（Atlas 私有 N45） | - | `Canvas` | `Canvas` | `CanvasNodeExecutor` | ✅（P0-3 M20） |
| 70（Atlas 私有 N46） | - | `ImagePlugin` | `ImagePlugin` | `ImagePluginNodeExecutor` | ✅（P0-3 M20） |

## 2. Atlas 暂不支持、需在节点目录中过滤的上游节点（P5-2 修正：仅剩 Coze ID 12）

> M12 + M20 落地后，原"暂不支持"列表已大量清空；Variable / ImageGenerate / Imageflow / ImageReference / ImageCanvas / SceneVariable / SceneChat / LtmUpstream / TriggerUpsert/Read/Delete 均已注册执行器（P0-2 + P0-3）。

| Coze ID | Coze type | 缺失原因 / 替代方案 |
|---|---|---|
| 12 | Database | Atlas 提供 `DatabaseQuery/Insert/Update/Delete/CustomSql` 5 个细分节点替代；不引入"通用 Database"父节点。 |

> 旧的"图像 / 场景 / 长期记忆 / 触发器"缺失项已全部清空，详见 P0-2 + P0-3 修复（`docs/plan-coze-lowcode-gap-fix-P0.md`）。
> 节点执行器与 RuntimeTriggerService / 拆分 Memory(64/65/66) / 图像视频 N44-N49 详见 `BuiltInWorkflowNodeDeclarations.cs` M12+M20 节点区块。

## 3. M4 节点执行器 DI 同步

- 新增 DI 注册：`VariableAssignerWithinLoopNodeExecutor`、`KnowledgeDeleterNodeExecutor`（参见 `Atlas.Infrastructure/DependencyInjection/AiRuntimeServiceRegistration.cs`）。
- `Comment` 节点为前端注释专用，无后端执行器；`BuiltInWorkflowNodeDeclarations` 中保留声明，DAG 执行时按"无操作"跳过。
- `_executorTypes` 与 DI 注册保持同步：节点目录 + 执行器之间的"声明-注册-执行"三者一致性由编译期控制（任何执行器新增都必须三处同时落地，否则 `NodeExecutorRegistry.GetExecutor` 返回 null 即可被运行时检测到）。

## 4. ConfigSchemaJson 与上游 formMeta 对齐建议

- 重点节点（LLM / Code / Http / Knowledge / SubWorkflow）的 `BuiltInWorkflowNodeDeclarations.GetDefaultConfig` 已包含上游表单期望的核心字段（`provider/model/prompt/temperature/maxTokens` 等）。
- 当上游 `formMeta` 中新增字段时，需在以下三处同步：
  1. `BuiltInWorkflowNodeDeclarations.GetDefaultConfig`（默认值）。
  2. 节点对应执行器中读取 `Config` 的位置（如 `LlmNodeExecutor` 通过 `VariableResolver.GetConfigString(config, "provider", ...)`）。
  3. 必要时为该 key 增加 `ConfigSchemaJson` 描述（表单驱动场景）。
