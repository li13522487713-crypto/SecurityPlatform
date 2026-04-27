# Microflow Stage 17 - Loop / Break / Continue Foundation

## 1. Scope

本轮完成 Loop 节点基础配置、`loopType` / iterable / condition 配置、loop variable 建模、Loop body / exit flow 基础持久化、Break / Continue target loop 配置与合法性 warning、dirty 状态同步、保存刷新恢复基础，以及 A/B 微流隔离的 helper 级验证。

本轮不做循环执行引擎、完整作用域/拓扑校验、表达式执行引擎、List / Collection 专项、Domain Model metadata、trace/debug、schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `packages/mendix/mendix-microflow/src/schema/types.ts` | schema | Break / Continue 增加 `targetLoopObjectId`；forEach loop source 增加可选 `iteratorVariableDataType` |
| `packages/mendix/mendix-microflow/src/schema/utils/loop-helpers.ts` | 新增 helper | Loop 配置、loop variable、body/exit flow、Break/Continue warning 纯函数 |
| `packages/mendix/mendix-microflow/src/schema/utils/index.ts` | export | 导出 Loop helper |
| `packages/mendix/mendix-microflow/src/variables/variable-index.ts` | variable index | Loop iterator 进入当前 schema 的 `loopVariables`，支持显式类型，空 iterator 不生成变量 |
| `packages/mendix/mendix-microflow/src/variables/microflow-variable-foundation.ts` | helper | 变量重名检测支持排除当前 loop iterator |
| `packages/mendix/mendix-microflow/src/node-registry/registry.ts` | registry | Loop 默认 source list / iterator 为空 |
| `packages/mendix/mendix-microflow/src/node-registry/edge-registry.ts` | edge rule | 允许 Loop body entry / body return 跨 loop collection 边界 |
| `packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | authoring | Loop 创建/复制不复用旧 iterator id/name；复制不复制 body 子图与连线 |
| `packages/mendix/mendix-microflow/src/property-panel/forms/loop-node-form.tsx` | UI | Loop caption/doc 之外的 loopType、source、condition、iterator name/type、flow summary、warnings |
| `packages/mendix/mendix-microflow/src/property-panel/forms/event-nodes-form.tsx` | UI | Break / Continue target loop、incoming/outgoing summary、legal state warning |
| `packages/mendix/mendix-microflow/src/property-panel/forms/flow-edge-form.tsx` | UI | Loop body/exit flow role 展示与设置 |
| `packages/mendix/mendix-microflow/src/schema/__tests__/loop-helpers.test.ts` | test | Stage 17 helper 覆盖 |
| `docs/microflow-stage-01-gap-matrix.md` | docs | P1 gap 状态更新 |

## 3. Loop Schema Contract

| 语义 | 源码字段 | 类型 | 同步规则 |
|---|---|---|---|
| loop object kind | `object.kind` | `"loopedActivity"` | UI 名称 Loop 映射到源码 `Microflows$LoopedActivity` |
| loopType | `loopSource.kind` | `"iterableList"` / `"whileCondition"` | `iterableList` 显示为 `forEach`，`whileCondition` 显示为 `while`；源码未支持 `repeatUntil` |
| iterable expression | `loopSource.listVariableName` | `string` | 可手写表达式或选择当前变量索引中的 List 变量 |
| condition expression | `loopSource.expression.raw` | `string` | while 模式保存表达式文本，不执行 |
| loop variable | `loopSource.iteratorVariableName` / `iteratorVariableDataType` | `string` / `MicroflowDataType?` | forEach 模式写入 schema，并由变量索引派生成 loop iterator |
| incoming flow | `destinationObjectId` + `destinationConnectionIndex` | flow fields | 普通入边保存 source/target/index |
| body flow | `originObjectId=loopId` + `originConnectionIndex=2` | sequence flow | FlowEdgeForm 标为 Body，刷新后映射到 Loop body handle |
| exit flow | `originObjectId=loopId` + `originConnectionIndex=1` | sequence flow | FlowEdgeForm 标为 After / Exit，刷新后映射到 Loop out handle |

## 4. Loop Variable Strategy

Loop variable 从 `Microflows$IterableList.iteratorVariableName` 派生进入 `schema.variables.loopVariables`。索引项使用 `source.kind="loopIterator"`、`source.loopObjectId=<loop object id>`、`scope.kind="loop"`、`scope.collectionId=<loop objectCollection id>`、`readonly=false`。

重命名只更新 loop config 与变量索引，不重写已有表达式，并在表单显示 warning。删除 Loop 时，Loop 对象、子对象、关联 body/exit flows 被删除，变量索引随 schema 重新派生而清理。复制 Loop 生成新 loop object id、新 loop body collection id，并把 iterator name 加 `_Copy` 后缀；不复制子图和连线。严格 body 内可见性和拓扑作用域校验后置到 Stage 20。

## 5. Break / Continue Strategy

Break / Continue 仍是事件对象：`breakEvent` / `continueEvent`，官方类型分别为 `Microflows$BreakEvent` / `Microflows$ContinueEvent`。本轮新增可选 `targetLoopObjectId` 保存目标 Loop。无 Loop 时显示 warning；多个 Loop 且未选择 target 时显示 ambiguous warning；target 指向已删除 Loop 时显示 stale warning；不在 Loop body collection 中时显示 Stage 20 后续校验提示。普通 outgoing flow 不阻止保存，但显示 warning。

## 6. Body / Exit Flow Strategy

源码已有 Loop 端口：`sequenceIn`、`sequenceOut`、`loopBodyIn`、`loopBodyOut`、`errorOut`。本轮按 connection index 映射：`1=After/Exit`，`2=Body`，`3=Body Return`。FlowGram 连线保存 `originObjectId`、`destinationObjectId`、`originConnectionIndex`、`destinationConnectionIndex`；刷新后通过 `toEditorGraph` 重新映射 handle。删除 body/exit flow 后 helper warning 分别提示 no body / no exit。

## 7. Warning Strategy

Loop warning 覆盖 loopType/source list/condition/loop variable 缺失、no body flow、no exit flow、body/exit 多条。Break / Continue warning 覆盖无 Loop、不在 Loop body、target stale、多个 Loop target ambiguous、存在 outgoing flow。Loop variable 重名与参数/变量冲突复用当前变量索引诊断，并提示重命名不会重写表达式。

## 8. Verification

自动测试：新增 `loop-helpers.test.ts`，覆盖 loopType、iterable、condition、loop variable upsert/remove/index、body/exit flow 标记、删除 Loop 清理、Break/Continue warning、stale target、A/B schema 隔离与无 `Sales.*` 默认值。

手工验收建议按 Stage 17 清单在 `/space/:workspaceId/mendix-studio/:appId` 打开真实 `MF_CalculateApprovalLevel`，拖入 Loop / Break / Continue，配置 `approvalUsers` 与 `currentApprover`，保存并检查 `PUT /api/microflows/{id}/schema` body，刷新后再打开 A/B 两个微流确认配置与 loop variable 不互相污染。
