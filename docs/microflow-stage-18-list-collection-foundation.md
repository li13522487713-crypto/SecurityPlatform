# Microflow Stage 18 - List / Collection Foundation

## 1. Scope

本轮完成 Create List、Change List、Aggregate List、List Operation 的基础建模能力；集合变量进入当前 Microflow variable index；List type / elementType 使用现有 `MicroflowDataType`；属性面板通过当前 schema 的 List variable selector 写回 action 字段；dirty/save/reload 继续复用 Stage 06/11 的编辑器保存桥，保存到 `PUT /api/microflows/{activeMicroflowId}/schema`；A/B 微流隔离由 active schema 派生索引保证。

本轮不做 Domain Model metadata、Object Activity 实体绑定、集合执行引擎、表达式执行引擎、trace/debug、复杂泛型推断、历史 schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/schema/types.ts` | schema 扩展 | 增加 Create/Change/Aggregate/ListOperation action 强类型字段与 list action variable source |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/action-registry.ts` | 默认 action | 为 List action 创建真实字段默认值，默认不含 demo entity/list |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/default-node-config.ts` | 默认配置 | 补齐 List action 空白安全默认配置 |
| `src/frontend/packages/mendix/mendix-microflow/src/variables/variable-index.ts` | variable index | 从 createList / aggregateList / listOperation 派生变量 |
| `src/frontend/packages/mendix/mendix-microflow/src/variables/list-collection-foundation.ts` | helper 新增 | 新增 List collection 纯函数 helper |
| `src/frontend/packages/mendix/mendix-microflow/src/variables/index.ts` | export | 导出 Stage 18 helper |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 复制同步 | 复制 Create List / Aggregate / List Operation 时生成不冲突变量名与 action id |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/action-activity-form.tsx` | 属性面板 | 增加四类 List action 专项表单 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/generic-action-fields-form.tsx` | 表单分发 | List action 不再走泛型文本字段 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/selectors/VariableSelector.tsx` | selector | 支持 List selector 空状态文案 |
| `src/frontend/packages/mendix/mendix-microflow/src/schema/__tests__/list-collection-helpers.test.ts` | 测试新增 | 覆盖 List variable index、复制、隔离、无 demo 默认值 |
| `docs/microflow-stage-01-gap-matrix.md` | 文档更新 | 标记 Stage 18 List/Collection 状态 |

## 3. List Schema Contract

| 语义 | 源码字段 | 类型 | 当前是否存在 | 当前是否同步 | 本轮处理 |
|---|---|---|---|---|---|
| Create List kind | `action.kind` | `"createList"` | 是 | 是 | 保持 |
| Create List variable name | `outputListVariableName` / `listVariableName` | `string` | 部分存在 | 是 | 双写保存，索引以 `outputListVariableName` 优先 |
| Create List variable id | `listVariableId` | `string?` | 否 | 是 | 新增，默认使用 action id |
| Create List element type | `elementType` / `itemType` | `MicroflowDataType` | 否 | 是 | 新增，默认 `string` |
| Create List entity | `entityQualifiedName` | `string?` | 是 | 是 | 保留空值，不接 metadata |
| Create List initial items | `initialItemsExpression` | `MicroflowExpression?` | 否 | 是 | 新增，只保存表达式文本 |
| Create List description | `description` / `documentation` | `string?` | 部分存在 | 是 | 双写保存 |
| Change List kind | `action.kind` | `"changeList"` | 是 | 是 | 保持 |
| Change List target | `targetListVariableName` | `string` | 是 | 是 | selector 从当前 List index 选择 |
| Change List operation | `operation` | `add/addRange/remove/removeWhere/clear/set` | 是 | 是 | 保存到 action |
| Change List expressions | `itemExpression` / `itemsExpression` / `conditionExpression` / `indexExpression` | `MicroflowExpression?` | 否 | 是 | 新增，不执行 |
| Aggregate List kind | `action.kind` | `"aggregateList"` | 是 | 是 | 保持 |
| Aggregate source | `listVariableName` / `sourceListVariableName` | `string` | 部分存在 | 是 | 双写保存 |
| Aggregate function | `aggregateFunction` | `count/sum/average/min/max` | 是 | 是 | 保存到 action |
| Aggregate member | `attributeQualifiedName` / `member` / `aggregateExpression` | `string?` / `MicroflowExpression?` | 部分存在 | 是 | 保存基础表达式 |
| Aggregate result | `outputVariableName` / `resultVariableName` / `resultVariableId` | `string` / `string?` | 部分存在 | 是 | 进入 variable index |
| Aggregate result type | `resultType` | `MicroflowDataType?` | 否 | 是 | count=Integer，sum/average=Decimal，min/max=Unknown |
| List Operation kind | `action.kind` | `"listOperation"` | 是 | 是 | 保持 |
| List Operation source | `leftListVariableName` / `sourceListVariableName` | `string` | 是 | 是 | selector 从当前 List index 选择 |
| List Operation target/output | `outputVariableName` / `outputListVariableName` | `string` | 是 | 是 | 输出 list 变量进入 index |
| List Operation operation | `operation` | `filter/sort/map/distinct/take/skip/union/intersect` | 是 | 是 | 保存到 action |
| List Operation expressions | `filterExpression` / `sortExpression` / `expression` | `MicroflowExpression?` | 部分存在 | 是 | 不执行表达式 |

## 4. List Type Strategy

本轮复用现有 `MicroflowDataType`，List 变量类型统一表示为 `{ kind: "list", itemType }`。primitive element type 使用现有 `boolean/integer/long/decimal/string/dateTime/json/object` 命名；Entity/Object 可以为空，并在 UI 与 variable diagnostics 中提示 “Entity metadata will be connected in Stage 19.” 不接真实 Domain Model metadata，不写入 `Sales.*` 等 demo entity。

## 5. Variable Index Strategy

`buildVariableIndex` 现在包含 parameters、Create Variable、Loop iterator、Create List list variables、Aggregate result variables、List Operation output list variables。Create List 以 `source.kind="createList"` 写入 `listOutputs`；Aggregate 以 `source.kind="aggregateList"` 写入 `localVariables`；List Operation 以 `source.kind="listOperation"` 写入 `listOutputs`。selector 通过 `allowedTypeKinds={["list"]}` 只显示当前 schema 派生的 List 变量，因此 A/B 微流不会互相污染。

## 6. Create / Change / Aggregate / Operation Strategy

Create List 支持变量名、elementType、listType、initialItemsExpression、description。变量名为空、重复或与 parameter 冲突由现有 variable diagnostics/OutputVariableEditor 提示；删除节点后索引自然消失；复制节点时生成新的 action id 与 `_Copy` 变量名。

Change List 支持 target list selector、operation、item/items/condition/index expression。target 缺失、无 List 变量、stale target、操作所需表达式缺失均显示 warning，所有字段写回当前 action。

Aggregate List 支持 source list selector、aggregateFunction、member/expression、result variable。result 变量进入 variable index，基础 resultType 由 aggregateFunction 推断；min/max 暂不做复杂推断。

List Operation 支持 source list selector、operation、filter/sort expression、output list variable。输出 elementType 优先继承 source list 的 itemType，未能推断时为 unknown warning，不执行集合操作。

## 7. Verification

自动测试：

| 验证项 | 结果 |
|---|---|
| `pnpm exec vitest run packages/mendix/mendix-microflow/src/schema/__tests__/list-collection-helpers.test.ts packages/mendix/mendix-microflow/src/schema/__tests__/microflow-variables.test.ts` | 2 files / 14 tests passed |
| `pnpm --filter @atlas/microflow run typecheck` | passed |

手工验收步骤需在运行环境打开 `/space/:workspaceId/mendix-studio/:appId` 后执行。代码层已验证保存仍由 `MicroflowEditor` 调用 `apiClient.saveMicroflow({ schema })`，嵌入路径的真实 adapter 继续负责 `PUT /api/microflows/{activeMicroflowId}/schema`；本轮未新增 mock API、localStorage fallback 或 demo 页面。
