# Workflow Editor 校验矩阵（当前 Atlas 主编辑器）

## 说明

- 当前矩阵描述的是 Coze adapter 已接入后的主编辑器要求：
  - `app-web` 主路径编辑入口以 `@coze-workflow/playground-adapter` 为准
  - `workflow-editor-react` 仅作为迁移期遗留实现保留，不再是默认主入口
- 本矩阵用于前端工作流编辑器校验覆盖清单，单一事实源依赖后端：
  - `GET /api/v2/workflows/node-types`（端口 + Schema）
  - `GET /api/v2/workflows/node-templates`（默认值）
  - `GET /api/v1/model-configs/enabled`（模型目录）
- 校验分层：
  - 第 1 层：Schema 字段校验
  - 第 2 层：端口与 inputMappings 校验
  - 第 3 层：业务跨字段规则（definitions.validate）

## 风险提示

- 当前矩阵默认要求同时覆盖：
  - Coze registry / form-meta / node-test 行为
  - Atlas adapter 与模型目录映射行为

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
| KnowledgeRetriever | 是 | 是 | 是 |
| KnowledgeIndexer | 是 | 是 | 是 |
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

> 范围：M19 工作流父级工程能力（AI 生成 / 批量 / 异步 / 封装解散 / 配额）。
> 与 `Atlas.AppHost.Controllers.DagWorkflowEngineeringController` 完全对应。

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
