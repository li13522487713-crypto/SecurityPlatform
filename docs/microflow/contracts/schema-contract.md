# MicroflowAuthoringSchema 契约

## 权威类型

TypeScript 定义见 `@atlas/microflow`（`schema/types.ts`）中的 `MicroflowAuthoringSchema`。

**P0 动作**（retrieve … logMessage 共 11 种）在 `ActionActivity.action` 上必须为**强类型** `Microflow*Action` 成员，不得落在 `MicroflowGenericAction`；P1/P2 可使用 Generic（kind 已排除 P0）。

## 必选顶层字段（冻结）

| 字段 | 说明 |
|------|------|
| `schemaVersion` | 语义化版本字符串 |
| `mendixProfile` | `mx10` \| `mx11` |
| `id` / `stableId` | 资源与同步主键 |
| `name` / `displayName` | 技术名与展示名 |
| `description` / `documentation` | 可选说明 |
| `moduleId` / `moduleName` | 模块归属 |
| `parameters` | 与 ParameterObject 并存；入参定义 |
| `returnType` / `returnVariableName` | 返回值 |
| `objectCollection` | 含嵌套 Loop 的 `MicroflowObject` 树 |
| `flows` | 仅 `SequenceFlow` / `AnnotationFlow`（决策/对象类型/错误线由 `SequenceFlow` 表达） |
| `security` / `concurrency` / `exposure` | 安全、并发、暴露 |
| `variables` | 可选；通常为 VariableIndex 派生缓存 |
| `validation` | 校验问题列表状态 |
| `debug` | 可选；编辑器内调试/高亮快照，**不**作为业务语义主数据（见下「调试 trace 类型」） |
| `editor` | 编辑器视口/选择，非业务语义 |
| `audit` | 审计/版本元数据 |

## 业务主模型

- **唯一主模型**：`MicroflowAuthoringSchema`。资源/发布快照中的语义版本请使用 `audit.version` 与资源行 `version` 字段；**不再**在 schema 顶层使用已废弃的 `version` 字段。
- **Legacy**：旧 demo 的 `nodes`/`edges` 图仅能通过 `normalizeMicroflowSchema` / `migrateLegacyMicroflowSchema`（`@atlas/microflow/schema/legacy`）迁入 Authoring；不得作为 Runtime、校验或 FlowGram 的持久化输入。
- **禁止**：将 FlowGram `WorkflowJSON` 作为业务主 schema 落库；FlowGram 仅由 `authoringToFlowGram` 派生。

## 调试 trace 类型（与运行时区分）

- **Authoring 内可选持久化**：`debug.traceFrames` / `debug.lastTrace` 的元素类型为 **`MicroflowAuthoringPersistedTraceFrame`**（定义在 `schema/types.ts`）。用于会话内 FlowGram/面板高亮等，形状刻意兼容 FlowGram 叠加层，但**不是**后端 test-run 返回的权威 trace DTO。
- **运行时 / API**：执行轨迹以 **`MicroflowTraceFrame`** 为准（`@atlas/microflow/debug`，`trace-types.ts`）；`authoringToFlowGram` 等可同时接受持久化帧与运行时帧的并集，便于用 `lastTrace` 或本次运行的 `frames` 驱动同一套叠加逻辑。
- 详见 [runtime-trace-contract.md](./runtime-trace-contract.md)。

## 破坏性变更风险

- 重命名/删除 `objectId`/`flowId` 稳定键将导致引用与发布快照失效。
- 将 `flows` 迁回仅挂在 `objectCollection.flows` 而根级 `flows` 为空，会破坏与现有校验/编辑器一致假设（当前实现以**根级** `schema.flows` 为准）。

## 后端存储建议

- 以 **AuthoringSchema JSON** 为真相源；发布时再存 **只读 snapshot JSON**。
- 资源行级元数据（名称、工作区、标签、权限）可拆表，与 `schemaId` 关联。

## 第 26 轮 P0 表单字段契约

- P0 属性面板字段必须直接回写 `MicroflowAuthoringSchema` 中的强类型 action 字段，禁止以 FlowGram JSON、generic config 或 raw JSON dump 作为主数据。
- 稳定 `fieldPath` 以 `action.*` 为准：Retrieve `action.retrieveSource.*` / `action.outputVariableName`，CreateObject `action.entityQualifiedName` / `action.memberChanges.*`，CallMicroflow `action.parameterMappings.*.argumentExpression` / `action.returnValue.outputVariableName`，RestCall `action.request.*` / `action.response.*`。
- 输出变量统一进入 VariableIndex：Retrieve、CreateObject、CreateVariable、CallMicroflow return、RestCall response/status/headers；重复名、非法名和空名必须挂到对应 `fieldPath`。

## 第 27 轮变量作用域 v2

- `MicroflowVariableVisibility` 固定为 `definite` / `maybe` / `unavailable`。
- `MicroflowVariableScope.kind` 覆盖 `global`、`downstream`、`branch`、`loop`、`errorHandler`、`system`、`collection`。
- `MicroflowVariableKind` 覆盖参数、P0 action 输出、local/create variable、object/list/primitive output、microflow return、REST response、loop iterator、system、error context、modeledOnly 与 unknown。
- `VariableIndex` 是派生结构，禁止作为业务主存储；构建必须显式接收 `MicroflowMetadataCatalog`，不得直接依赖 mock metadata。
- FlowGraph 分析基于 AuthoringSchema：AnnotationFlow 不参与 normal graph，ErrorHandlerFlow 只参与 error scope，Loop nested collection 单独建图。
- P0 Action 输出类型规则见 `runtime-variable-scope-contract.md`；GenericAction 只允许 modeledOnly/unknown 输出并产生 warning。

## 第 28 轮表达式与校验契约

- `MicroflowExpression.raw/text` 是 AuthoringSchema 内的表达式主载荷；解析结果 AST/tokens 只作为派生诊断结构。
- 表达式 P0 子集见 `runtime-expression-contract.md`，不声明支持完整 Mendix 表达式语言。
- Validator 必须显式接收 MetadataCatalog 和 VariableIndex，不得从 schema 内部或 app-web 回落 mock。
- `ValidationIssue.fieldPath` 必须使用 AuthoringSchema 字段路径，与 PropertyPanel `FieldError` 对齐。

## 第 29 轮 Flow / Port 协议

- `portId` 冻结为 `{objectId}:{portKind}:{connectionIndex}`，必须可由 `parsePortId` 反解；旧 `{objectId}:in/out/error/...` 仅作为兼容解析输入。
- `originConnectionIndex` / `destinationConnectionIndex` 是 AuthoringSchema 的持久化端口锚点；FlowGram `sourcePortID` / `targetPortID` 必须由它们派生，不得随机生成。
- `SequenceFlow` 承载 `sequence`、`decisionCondition`、`objectTypeCondition`、`errorHandler` 四类执行/错误边；`errorHandler` 必须由 `isErrorHandler=true` 与 `editor.edgeKind="errorHandler"` 同时表达。
- `decisionCondition` / `objectTypeCondition` 使用 `caseValues` 表达分支；普通 `sequence` 与 `errorHandler` 不得保留业务 case。
- `AnnotationFlow` 必须是 `Microflows$AnnotationFlow`，至少一端为 Annotation；P0 不支持 Annotation 到 Edge attachment，不参与 Runtime control flow。
- Loop 内部 flow 必须写入对应 `objectCollection.flows`，root 与 Loop internal object 之间禁止直接连线；LoopedActivity 本身仍作为 root collection 节点参与 root flow。
