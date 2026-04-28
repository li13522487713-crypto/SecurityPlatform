# Microflow Stage 14 - Decision / If / Merge Foundation

## 1. Scope

本轮完成 Decision / If 表达式配置、分支类型基础配置、true / false 出边绑定、case / condition 持久化、FlowEdgeForm 分支编辑、Merge 节点基础配置、Merge incoming/outgoing summary、分支 warning、dirty 状态同步、保存刷新恢复和 A/B 微流隔离。

本轮不做表达式执行引擎、后端执行引擎、trace/debug、Domain Model metadata、Call Microflow metadata 和 schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/schema/utils/decision-merge.ts` | 新增 | Decision expression、branch case、flow label、Merge summary/behavior 纯 helper。 |
| `src/frontend/packages/mendix/mendix-microflow/src/schema/utils/index.ts` | 修改 | 导出 Stage 14 helper。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/exclusive-split-form.tsx` | 修改 | Decision 表单补空表达式、缺 true/false、重复 case 和 branch summary warning。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/flow-edge-form.tsx` | 修改 | Decision 出边只通过 selector 编辑 case，普通连线不再手写 caseValues。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/merge-node-form.tsx` | 修改 | Merge 策略可编辑、incoming/outgoing summary 和 warning。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/object-panel.tsx` | 修改 | 将 patch 传入 Merge 表单。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts` | 修改 | Merge defaultConfig 与真实 schema `firstArrived` 对齐。 |
| `src/frontend/packages/mendix/mendix-microflow/src/schema/__tests__/decision-merge-helpers.test.ts` | 新增 | 覆盖 Decision expression、true/false case、重复检测、释放 case、Merge summary、A/B 隔离。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 Stage 14 P1 状态。 |

## 3. Decision Schema Contract

| 语义 | 源码字段 | 类型 | 当前是否存在 | 当前是否同步 | 本轮处理 |
|---|---|---|---|---|---|
| UI Decision / If | `MicroflowExclusiveSplit` | `object.kind="exclusiveSplit"` | 是 | 已持久化 | 文档明确 UI Decision 映射源码 ExclusiveSplit。 |
| expression | `splitCondition.expression` | `MicroflowExpression` | 是 | 表单已写回 object | 补 helper 与 warning，空表达式不崩溃。 |
| decision type | `splitCondition.kind` / `resultType` | `expression/rule` + `boolean/enumeration` | 是 | 已写回 object | 切换 expression 不注入 fake 表达式。 |
| outgoing flows | `flows[]` / nested `objectCollection.flows` | `MicroflowSequenceFlow[]` | 是 | Stage 10 已同步 | 本轮 helper 读取并生成 branch summary。 |
| true/false/case values | `flow.caseValues` | `MicroflowCaseValue[]` | 是 | Stage 10 已同步 | 本轮 helper/FlowEdgeForm 统一编辑。 |
| source handle | `originConnectionIndex` | number | 是 | Stage 10 已同步 | true/false 由 FlowGram port + caseValues 恢复。 |
| target handle | `destinationConnectionIndex` | number | 是 | Stage 10 已同步 | 本轮不改。 |
| branch label | `flow.editor.label` | string | 是 | 已写回 flow | 本轮 helper 和 FlowEdgeForm 保持保存刷新恢复。 |

## 4. Branch Assignment Strategy

Boolean Decision 的分支值保存为 `MicroflowCaseValue`：true 为 `{ kind: "boolean", value: true, persistedValue: "true" }`，false 为 `{ kind: "boolean", value: false, persistedValue: "false" }`。FlowGram 从 True/False port 建线时会写入对应 `caseValues` 与 `originConnectionIndex`；FlowEdgeForm 修改分支时只通过 case selector 写回 schema。

重复 true/false 不自动静默覆盖，现有连接校验阻止同一 port 重复，validation 与 helper `getDecisionBranchConflicts` 提供 warning。删除出边直接删除 flow，因此该 flow 的 `caseValues` 随 flow 一起消失；`releaseDecisionBranchCase` 可将存量 flow 标记为 `noCase`，供后续编辑释放 case。

源码已支持 enumeration/object type case，本轮保留已有 selector 能力；不接 Domain Model metadata，不新增后端字段。

## 5. FlowEdgeForm Strategy

普通连线展示 source/target、connection index、routing、label、description，不显示 true/false 必填逻辑。Decision / InheritanceSplit 出边显示 boolean/enumeration/object type case selector，写回 `flow.caseValues`、`flow.editor.edgeKind` 与 label。空 case 显示 warning，重复 case 显示 warning。手工 TextArea 写非法 caseValues 的 fallback 已移除，避免 UI 写出 fake enum metadata。

## 6. Merge Strategy

源码 Merge 为 `MicroflowExclusiveMerge`，`object.kind="exclusiveMerge"`，真实策略字段为 `mergeBehavior.strategy="firstArrived"`。本轮将 registry defaultConfig 对齐为 `firstArrived`，Merge 表单显示并可保存该策略，同时展示 incoming/outgoing flow count 和 summary。

warning 策略：incoming 少于 2 时提示 Merge 通常需要多个输入；outgoing 缺失时提示；多个 outgoing 时提示 ExclusiveMerge 通常只有一个输出。删除 Merge 沿用现有 `deleteObject`，相关 flows 会被清理。

## 7. Expression Context

Decision expression 使用现有 `ExpressionEditor`，传入当前 schema、metadata 和 `variableIndex`。`variableIndex` 来自当前 active microflow schema，包含 Stage 12 parameters 与 Stage 13 variables；不会读取其他微流变量。本轮只保存表达式文本和类型提示，不执行表达式，也不做复杂类型推断。

## 8. Verification

自动测试：

- `src/frontend/packages/mendix/mendix-microflow/src/schema/__tests__/decision-merge-helpers.test.ts`
- 覆盖 `updateDecisionExpression`、true/false case 分配、重复 true 检测、释放/删除 flow case、flow label、Merge behavior/summary、删除 Merge 清理 flows、A/B schema 隔离。

手工验收建议：

- 在 `/space/:workspaceId/mendix-studio/:appId` 打开 `MF_ValidatePurchaseRequest`，配置 Start -> Decision -> Merge -> End。
- 设置 Decision expression 为 `totalAmount > 100`，Decision true/false 出边分别设置 label，Merge caption 设置为 `MergeApprovalBranch`。
- 保存并确认 `PUT /api/microflows/{activeMicroflowId}/schema` body 包含 `splitCondition.expression`、`caseValues`、`originConnectionIndex`、`editor.label` 与 `mergeBehavior`。
- 刷新后重新打开该微流确认恢复，再打开另一微流确认分支配置不串数据。
