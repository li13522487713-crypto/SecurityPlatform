# Workflow Editor 校验矩阵（当前 Atlas 主编辑器）

## 说明

- 当前矩阵描述的是 Coze adapter 已接入后的主编辑器要求：
  - `app-web` 主路径编辑入口以 `@coze-workflow/playground-adapter` 为准
  - `workflow-editor-react` 仅作为迁移期遗留实现保留，不再是默认主入口
- 本矩阵用于前端工作流编辑器校验覆盖清单，单一事实源依赖后端：
  - `POST /api/app-web/workflow-sdk/node_type`（端口 + Schema）
  - `POST /api/app-web/workflow-sdk/node_template_list`（默认值）
  - `GET /api/v1/model-configs/enabled`（模型目录）
- 校验分层：
  - 第 1 层：Schema 字段校验
  - 第 2 层：端口与 inputMappings 校验
  - 第 3 层：业务跨字段规则（definitions.validate）

## 风险提示

- 当前矩阵默认要求同时覆盖：
  - Coze registry / form-meta / node-test 行为
  - Atlas adapter 与模型目录映射行为

## Coze 通用 Database(ID=12) 节点处理策略（P5-3 修正）

> **根因**：Coze 开源前端 `StandardNodeType.Database = "12"` 是一个通用父节点占位符，在 `get-enabled-node-types.ts` 中曾被错误启用。该 ID 在 Atlas `WorkflowNodeType` 枚举中没有对应成员，导致：
> - 画布中添加 Database(12) 节点后保存成功（raw JSON string）
> - 试运行时后端解析 `type=12` → `Enum.IsDefined = false` → 整个 canvas 解析失败 → 报"有未连接的节点"
>
> **修复措施**：
> 1. **前端**：`get-enabled-node-types.ts` 移除 `StandardNodeType.Database`，Atlas 已用 `DatabaseQuery/Insert/Update/Delete/CustomSql` 5 个细粒度节点替代，禁止用户添加通用 Database 父节点。
> 2. **后端 Bridge**：`WorkflowCanvasJsonBridge.TryConvertCanvas` 改为软失败——节点类型不可识别时 `continue` 跳过，同步过滤指向被跳过节点的孤儿连线，不中止整个画布解析。
> 3. **后端 Compiler**：`CozeWorkflowPlanCompiler.CozeNodeTypeAliases` 补充 `Database(12)` → `Comment` 降级映射，防止 Coze schema 编译路径同样失败。
>
> **前端同步恢复**：`StandardNodeType.JsonParser`（ID=59，对应 `JsonDeserialization`）已从注释中恢复，后端执行器完整支持。

## 全节点覆盖矩阵（49+，P5-2 修正：含 P0-2/P0-3 新增 20 节点）

> P0-2 + P0-3 修复后：M12 触发器 3 节点（TriggerUpsert/Read/Delete）+ M20 17 个新节点（Variable / ImageGenerate / Imageflow / ImageReference / ImageCanvas / SceneVariable / SceneChat / LtmUpstream / MemoryRead/Write/Delete / ImageGeneration / Canvas / ImagePlugin / VideoGeneration / VideoToAudio / VideoFrameExtraction）全部已注册执行器。校验语义同基础类节点（Schema 校验 / 端口校验 / 业务规则校验三层）；详见 `docs/coze-node-mapping.md` §1。
>
> 媒体节点（ImageGeneration / VideoGeneration 等）增加 1 项业务规则：当租户未配置 `IChatClientFactory` 时执行期返回 `MODEL_PROVIDER_NOT_CONFIGURED`，编辑期保留校验通过（避免在租户配置前阻断画布保存）。



| 节点类型 | Schema 校验 | 端口校验 | 业务规则校验 |
|---|---|---|---|
| Entry | 是 | 是 | 是 |
| Exit | 是 | 是 | 是 |
| Llm | 是 | 是 | 是 |
| Plugin | 是 | 是 | 是 |
| Agent | 是 | 是 | 是 |
| IntentDetector | 是 | 是 | 是 |
| QuestionAnswer | 是 | 是 | 是 |
| Selector | 是 | 是 | 是 |
| SubWorkflow | 是 | 是 | 是 |
| TextProcessor | 是 | 是 | 是 |
| Loop | 是 | 是 | 是 |
| Batch | 是 | 是 | 是 |
| Break | 是 | 是 | 是 |
| Continue | 是 | 是 | 是 |
| InputReceiver | 是 | 是 | 是 |
| OutputEmitter | 是 | 是 | 是 |
| AssignVariable | 是 | 是 | 是 |
| VariableAssignerWithinLoop | 是 | 是 | 是 |
| VariableAggregator | 是 | 是 | 是 |
| KnowledgeRetriever | 是 | 是 | 是（v5 §38 / 计划 G6：retrievalProfile（topK/minScore/enableRerank/rerankModel/enableHybrid/weights/enableQueryRewrite）+ filters（key-value map）+ callerContextOverride（合并默认 CallerContext，含 preset）+ debug；输出 traceId / finalContext / candidates / rewrittenQuery / latencyMs / embeddingModel / vectorStore（snake_case 别名保留一个版本） |
| KnowledgeIndexer | 是 | 是 | 是（v5 §35 / 计划 G6：parsingStrategy（quick/precise + extractImage/Table + imageOcr + sheetId/headerLine/dataStartLine + filterPages + captionType）+ chunkingProfile（mode=fixed/semantic/table-row/image-item + size/overlap/separators/indexColumns）+ mode（append/overwrite，overwrite 模式先 GC 旧 chunks）；通过 IKnowledgeIndexJobService 走 Hangfire 调度 |
| KnowledgeDeleter | 是 | 是 | 是 |
| Ltm | 是 | 是 | 是 |
| DatabaseQuery | 是 | 是 | 是 |
| DatabaseInsert | 是 | 是 | 是 |
| DatabaseUpdate | 是 | 是 | 是 |
| DatabaseDelete | 是 | 是 | 是 |
| DatabaseCustomSql | 是 | 是 | 是 |
| CreateConversation | 是 | 是 | 是 |
| ConversationList | 是 | 是 | 是 |
| ConversationUpdate | 是 | 是 | 是 |
| ConversationDelete | 是 | 是 | 是 |
| ConversationHistory | 是 | 是 | 是 |
| ClearConversationHistory | 是 | 是 | 是 |
| MessageList | 是 | 是 | 是 |
| CreateMessage | 是 | 是 | 是 |
| EditMessage | 是 | 是 | 是 |
| DeleteMessage | 是 | 是 | 是 |
| HttpRequester | 是 | 是 | 是 |
| CodeRunner | 是 | 是 | 是 |
| JsonSerialization | 是 | 是 | 是 |
| JsonDeserialization | 是 | 是 | 是 |
| Comment | 是 | 是 | 是 |

## 外部协同 Connector 节点矩阵（v4 §27-31 + N6 钉钉/飞书三方扩展）

> 节点目录：`Atlas.Sdk.ConnectorPlugins/Resources/NodeCatalog.json`，DI 注册：`AddConnectorPluginNodes()`。所有节点 `Category = "external_collaboration"`。
> 校验语义：第 1 层 Schema 校验对外暴露字段（providerId / userIds / messageType / fieldsJson 等），第 2 层端口校验由统一 `IConnectorPluginNode.ExecuteAsync` 输入/输出 schema 提供，第 3 层业务规则由各 Provider 在执行期返回 `ConnectorErrorCodes.*`。

| 节点 type | 节点能力 | Schema 校验 | 端口校验 | 业务规则校验 |
|---|---|---|---|---|
| external_identity_bind | 把外部用户身份绑定到本地 LocalUserId（4 档策略） | 是 | 是 | 是（直绑/手机号/邮箱 + ConnectorErrorCodes.IdentityNotFound/IdentityAmbiguous） |
| external_directory_sync_trigger | 触发指定 provider 一次性全量同步 | 是 | 是 | 是（不可见范围降级 60011/60020） |
| external_sync_department | 增量同步部门变更 | 是 | 是 | 是 |
| external_sync_member | 增量同步成员变更 | 是 | 是 | 是 |
| wecom_send_message | 发送企微消息（文本/template_card） | 是 | 是 | 是（响应写 ExternalMessageDispatch 表 + ResponseCode 链路） |
| feishu_send_message | 发送飞书消息（文本/interactive） | 是 | 是 | 是 |
| dingtalk_send_message | 发送钉钉工作通知（asyncsend_v2） | 是 | 是 | 是（需要 AgentId；缺失返回 MessagingFailed） |
| wecom_create_approval | 创建企微审批实例 | 是 | 是 | 是（IntegrationMode != LocalLed 才推外部） |
| feishu_create_approval | 创建飞书审批实例 | 是 | 是 | 是 |
| dingtalk_create_approval | 创建钉钉工作流实例 | 是 | 是 | 是 |
| feishu_sync_third_party_approval | 模式 B：把本地状态推到飞书审批中心（external_instances+check） | 是 | 是 | 是（飞书 90001-90099 → ApprovalSubmitFailed） |
| external_query_approval_status | 跨 Provider 查询并对账外部审批状态 | 是 | 是 | 是（dingtalk processInstanceId / feishu instance_code / wecom sp_no 三套适配） |
| external_process_callback | 把死信回调显式重投到 ConnectorCallbackInboxService | 是 | 是 | 是（历史 PlatformHost 内部用，退役后由 AppHost/Connector 回调链路承载） |

## 连线规则矩阵

| 规则项 | 约束 |
|---|---|
| 起点方向 | 仅 Output |
| 终点方向 | 仅 Input |
| 自环 | 默认禁止 |
| 重复边 | 拦截 |
| 上限 | fromPort / toPort 均受 maxConnections 限制 |
| 类型兼容 | 同类型或白名单兼容 |
| 历史缺失端口 | 加载迁移到默认端口；无法修复则保存前阻断 |

## 结果呈现

- 字段级错误：属性面板字段就地提示
- 节点级错误：属性面板顶部汇总
- 画布级错误：保存/发布前统一阻断与定位
- 模型节点：属性面板应直接显示模型中心选择器，不再暴露手填 provider / model 作为默认交互

## 附录：M19 父级工程能力验证矩阵

> 历史说明：本附录记录的是早期 M19 `v2` 工程能力设计稿。
> 当前仓库已移除 `DagWorkflowEngineeringController` 与 `DagWorkflowAsyncController`，`/api/v2/workflows/*` 不再是有效工作流主链路。
> 运行时保留的可执行入口为 `api/runtime/workflows/{id}:invoke`、`api/runtime/workflows/{id}:invoke-async`、`api/runtime/workflows/{id}:invoke-batch`。

| # | 端点 | 校验点 |
| - | --- | --- |
| 1 | `POST /api/v2/workflows/generate (auto)` | mode 仅允许 auto/assisted；prompt 非空；产出包含 entry/llm/exit 三节点 + 2 边的 canvas JSON |
| 2 | `POST /api/v2/workflows/generate (assisted)` | 把 prompt 切词为节点骨架（含 entry / 关键字推断 type / exit）|
| 3 | `POST /api/v2/workflows/{id}/batch (csv)` | 首行 header；rows 等于 N-1；onFailure 控制 abort/continue |
| 4 | `POST /api/v2/workflows/{id}/batch (json)` | 数组 + 对象元素；非数组拒绝 |
| 5 | `POST /api/v2/workflows/{id}/batch (database)` | 接 IRuntimeDataSourceConnector（M19 简化为 stub）|
| 6 | `POST /api/v2/workflows/{id}/compose` | selectedNodeKeys ≥ 1；产出 inferred input/output（M19 简化为 input/output 单字段）|
| 7 | `POST /api/v2/workflows/{id}/decompose` | subWorkflowNodeKey 必填；写审计 |
| 8 | `GET /api/v2/workflows/quota` | 返回默认配额（200/100/10/100k）|

校验通过准则：每个端点均经 `IAuditWriter` 写入审计；JSON 输入服务端二次校验；超出配额走 docs/lowcode-resilience-spec.md §4 降级策略。
