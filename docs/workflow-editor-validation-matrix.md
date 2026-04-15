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

## 全节点覆盖矩阵（40+）

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
