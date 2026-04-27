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
