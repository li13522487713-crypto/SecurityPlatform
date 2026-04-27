# 前端 P0 强类型实现（第 25 轮）

- **源类型**：`@atlas/microflow/schema` 中各 `Microflow*Action`（P0）与 `MicroflowGenericAction`（排除 P0 kind）。
- **映射**：`mapAuthoringP0ToRuntimeBlocks` / `tryMapP0ActionToDiscriminatedDto`（`@atlas/microflow/runtime`）。
- **校验**：`p0-action-guards` + `validate-actions` 错误码 `MF_ACTION_P0_MUST_BE_STRONGLY_TYPED` 等。
- **样例**：`verifyMicroflowContracts()` 会检查 `p0RuntimeActionBlocks` 存在性与 supportLevel 一致性。

## 第 26 轮产品化补充

- P0 表单入口仍集中在 `ActionActivityForm`，通用输入能力沉淀到 `property-panel/common`，包括 `FieldRow`、`OutputVariableEditor`、`VariableNameInput`、`ErrorHandlingEditor`。
- Metadata/Variable/Expression 分别通过 Selector、VariableIndex/VariableSelector、ExpressionEditor 接入，不从 app-web 或 mock metadata 承载核心逻辑。
- FlowGram subtitle 从 Authoring action 派生，覆盖 REST method/url、Log level、CallMicroflow target 与 P0 输出变量。
- Contract verify 增加 P0 runtime block、fieldPath 契约和输出变量进入 VariableIndex 的检查。

## 第 28 轮表达式与校验补充

- P0 表达式字段统一走 microflow 包内 `ExpressionEditor`、`parseExpression`、`inferExpressionType`、`validateExpression`。
- 支持变量、对象属性、literal、comparison、and/or/not、`empty()`、`if then else` 与 enumeration value 第一版。
- `validateActions` 覆盖 P0 必填字段、Rest header/query/body、CallMicroflow 参数与 void return storeResult。
- `validateExpressions` 覆盖 Retrieve custom range、REST form body、LogMessage arguments 等第 27 轮遗漏字段。
- modeledOnly / requiresConnector / nanoflowOnly 进入 Validator，并按 edit/save/publish/testRun 模式调整 severity。
