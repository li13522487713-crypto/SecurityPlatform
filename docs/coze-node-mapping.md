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

## 2. Atlas 暂不支持、需在节点目录中**过滤**的上游节点

> 这些 ID 出现在上游 `StandardNodeType`，但 Atlas 没有等效执行器。`/api/workflow_api/node_type` 与 `node_template_list` 不会返回这些类型，前端节点面板自然不会显示。

| Coze ID | Coze type | 缺失原因 / 替代方案 |
|---|---|---|
| 11 | Variable | 与 `VariableAggregator(32)` 与 `AssignVariable(40)` 行为部分重叠，Atlas 暂不单独引入读变量节点。 |
| 12 | Database | Atlas 提供 `DatabaseQuery/Insert/Update/Delete/CustomSql` 5 个细分节点替代。 |
| 14 | Imageflow | Atlas 不支持 image flow 节点；前端 `imageflow_basic_nodes` fallback 已返回空集。 |
| 16 | ImageGenerate | 同上。等保2.0 / 商用合规未评估，暂不开放。 |
| 17 | ImageReference | 同上。 |
| 23 | ImageCanvas | 同上。 |
| 24 | SceneVariable | Atlas 暂不实现 Coze 场景变量域。 |
| 25 | SceneChat | 同上。 |
| 26 | LTM | Atlas 用私有 `Ltm(62)` 替代，节点 ID 不与上游对齐；前端走 Atlas 自有节点目录。 |
<!-- M12 已落地 TriggerUpsert(34) / TriggerRead(35) / TriggerDelete(36)，从缺失表移除。 -->
<!-- M20 已落地 Variable(11) / ImageGenerate(14) / Imageflow(15) / ImageReference(16) / ImageCanvas(17,23) / SceneVariable(24) / SceneChat(25) / LtmUpstream(26)，节点 ID 与上游对齐；缺失表已清空。 -->
<!-- 节点执行器与 RuntimeTriggerService / 拆分 Memory(64/65/66) / 图像视频 N44-N49 详见 BuiltInWorkflowNodeDeclarations.cs M12+M20 节点区块；docs/lowcode-orchestration-spec.md §3 提供完整映射表。 -->

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
