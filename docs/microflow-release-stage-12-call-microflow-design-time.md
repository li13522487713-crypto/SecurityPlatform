# Microflow Release Stage 12 - Call Microflow Design Time

## 1. Scope

本轮完成 `/space/:workspaceId/mendix-studio/:appId` 页面中 Call Microflow 节点的设计态互调能力：

- real microflow target metadata：通过真实 `metadataAdapter` 和后端 metadata API 加载目标微流列表。
- MicroflowSelector：支持 loading/error/empty/retry/search，显示 displayName、qualifiedName、moduleName、status。
- targetMicroflowId stable save：目标选择写入当前 source 微流 schema 的 `targetMicroflowId`。
- target qualifiedName/displayName snapshot：保存 `targetMicroflowQualifiedName`、`targetMicroflowDisplayName` 等显示快照。
- target parameters：读取目标微流 schema-level parameters，并展示 required/default/类型信息。
- parameterMappings：按目标参数重建映射，保留同名/同 id mapping，表达式和 source variable 写回 schema。
- return binding：根据目标 returnType 启用/禁用返回值绑定，绑定结果写回 schema。
- output variable：返回值 output variable 进入当前微流 variable index。
- reference rebuild verification：保存 source schema 后，后端 `MicroflowReferenceIndexer` 基于 `callMicroflow.targetMicroflowId` 重建引用。
- target rename stability：以 `targetMicroflowId` 为权威，qualifiedName 仅为快照；rename 后保留 id 并显示 stale warning。
- self-call warning：当前微流自身在 selector 中禁用，已有 self-call 配置显示 warning。
- dirty state integration：target、mapping、return binding、callMode、caption/description 修改通过 `patchObject`/`commitSchema` 触发当前 tab dirty。
- save reload recovery：配置随当前 source 微流 schema 通过 `PUT /api/microflows/{id}/schema` 持久化并可刷新恢复。
- A/B/C microflow isolation：配置只写入当前 editor instance 的 `MicroflowAuthoringSchema`，metadata cache 可共享但 action config 不共享。

本轮不做：

- Call Microflow runtime executor。
- async call runtime。
- full references drawer。
- full cycle detection。
- publish/run/trace。
- expression execution engine。

依赖缺口与本轮最小补齐点：

| 依赖缺口 | 本轮最小补齐点 |
|---|---|
| metadata microflow ref 缺少 displayName/moduleId/version/schemaId 等设计态展示字段 | 扩展前后端 metadata DTO/type，并由 `MicroflowMetadataService` 从 `MicroflowResourceEntity` 填充 |
| target parameter metadata 缺少 id/default/description/order | 扩展前后端 parameter DTO/type，并从 schema parameters 读取 |
| metadata 请求可能乱序污染当前 form | `MicroflowMetadataProvider` 与 `MicroflowSelector` 增加 request sequence guard |
| 后端 metadata search 未覆盖 displayName | `MicroflowMetadataService` keyword match 增加 displayName |
| legacy call config adapter 缺少新增字段透传 | `microflow-adapters.ts` 与 `LegacyMicroflowActivityConfig` 做兼容扩展 |

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/schema/types.ts` | 修改 | 扩展 Call Microflow action、parameter mapping、return binding 与 runtime DTO 字段 |
| `src/frontend/packages/mendix/mendix-microflow/src/metadata/metadata-catalog.ts` | 修改 | 扩展 metadata microflow/parameter 类型与搜索字段 |
| `src/frontend/packages/mendix/mendix-microflow/src/metadata/metadata-provider.tsx` | 修改 | metadata 请求序号防乱序，不 fallback mock |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/selectors/MicroflowSelector.tsx` | 修改 | 使用真实 `getMicroflowRefs`，显示状态、self-call warning、error/retry/empty |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/action-activity-form.tsx` | 修改 | Call Microflow target summary、参数映射、return binding、stale warning |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/utils/call-microflow-config.ts` | 修改 | target 更新/清空、mapping rebuild、return binding helper |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 修改 | 新建 Call Microflow action 默认字段补齐 |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/microflow-adapters.ts` | 修改 | legacy config 到 authoring schema 的新增字段透传 |
| `src/frontend/packages/mendix/mendix-microflow/src/runtime/map-authoring-p0-runtime.ts` | 修改 | runtime DTO 保留 target display/module/version/schema 快照 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/__tests__/call-microflow-config.test.ts` | 修改 | 覆盖 target 写入/清空、mapping 保留、return binding |
| `src/backend/Atlas.Application.Microflows/Models/MicroflowMetadataCatalogDto.cs` | 修改 | 扩展 metadata DTO 字段 |
| `src/backend/Atlas.Application.Microflows/Services/MicroflowMetadataService.cs` | 修改 | 从真实 microflow resource/schema 读取 target metadata 和 parameters |
| `docs/microflow-p1-release-gap.md` | 修改 | 更新 Stage 12 状态与后续缺口 |

新增文件：

| 文件 | 说明 |
|---|---|
| `docs/microflow-release-stage-12-call-microflow-design-time.md` | Stage 12 设计态互调交付文档 |

## 3. Metadata API Contract

前端 metadata adapter 接入路径：

- `MendixStudioApp` 创建 HTTP adapter bundle。
- `MicroflowResourceEditorHost` 要求 `adapterBundle.mode === "http"` 且存在 `metadataAdapter`。
- `MendixMicroflowEditorEntry` 将 `metadataAdapter`、`metadataWorkspaceId`、`metadataModuleId` 传给 `MicroflowEditor`。
- `MicroflowMetadataProvider` 和 `MicroflowSelector` 使用该 adapter。

实际 API：

- Catalog：`GET /api/microflow-metadata?workspaceId={workspaceId}&moduleId={moduleId}`。
- Target refs：`GET /api/microflow-metadata/microflows?workspaceId={workspaceId}&moduleId={moduleId}`。

target DTO 字段：

| 字段 | 说明 |
|---|---|
| `id` | 稳定目标微流 id，Call Microflow 引用主键 |
| `name` / `displayName` | 源码名与显示名 |
| `qualifiedName` | 显示快照，不作为唯一主键 |
| `moduleId` / `moduleName` | 目标模块信息 |
| `parameters` | 目标微流参数列表 |
| `returnType` | 目标返回类型 |
| `status` | draft/published/archived 等状态 |
| `version` / `schemaId` | 快照辅助信息 |

loading/error/empty/retry：

- metadata adapter 缺失或 API 失败时，selector 显示 error + retry。
- API 返回空数组时显示 `No microflows available`。
- 不使用 `mock-metadata.ts`、不 fallback mock，不从 localStorage 拼目标列表。

## 4. Call Microflow Schema Contract

字段盘点：

| 语义 | 源码字段 | 类型 | 当前是否存在 | 当前是否写回 schema | 缺口 | 本轮处理 |
|---|---|---|---|---|---|---|
| action.kind / actionKind | `action.kind` / registry `actionKind` | `"callMicroflow"` | 是 | 是 | 无 | 保持 |
| targetMicroflowId | `targetMicroflowId` | `string` | 是 | 是 | 需作为权威主键 | target 选择必须写入 |
| targetMicroflowName | `targetMicroflowName` | `string?` | 是 | 是 | 快照字段 | target 选择更新 |
| targetMicroflowDisplayName | `targetMicroflowDisplayName` | `string?` | 新增 | 是 | DTO/adapter 缺失 | 最小扩展 |
| targetMicroflowQualifiedName | `targetMicroflowQualifiedName` | `string?` | 是 | 是 | rename 后 stale | stale warning，不清 id |
| targetModuleId | `targetModuleId` | `string?` | 新增 | 是 | 目标模块快照缺失 | 最小扩展 |
| targetVersion / targetSchemaId | `targetVersion` / `targetSchemaId` | `string?` | 新增 | 是 | 快照辅助缺失 | 最小扩展 |
| parameterMappings | `parameterMappings` | `MicroflowParameterMapping[]` | 是 | 是 | 字段不完整 | 扩展 mapping contract |
| mapping targetParameterId | `mapping.targetParameterId` | `string?` | 新增 | 是 | 参数 rename 稳定性弱 | 按 id 优先保留 |
| mapping targetParameterName | `mapping.targetParameterName` | `string?` | 新增 | 是 | display 缺失 | 保存快照 |
| mapping expression | `mapping.expression` / `argumentExpression` | `MicroflowExpression` | 扩展 | 是 | 旧字段不一致 | 两者同步 |
| mapping sourceVariableId/sourceVariableName | `mapping.sourceVariableId` / `sourceVariableName` | `string?` | 是 | 是 | selector 需仅当前微流 | 使用当前 schema variable index |
| returnType | `returnValue.dataType` / target `returnType` | `MicroflowDataType?` | 是 | 是 | target void 需禁用 | 按 target returnType 同步 |
| returnValueTarget | `returnValue.storeResult` | `boolean` | 是 | 是 | 无 | 保持 |
| outputVariableId/outputVariableName | `returnValue.outputVariableId` / `outputVariableName` | `string?` | 扩展 | 是 | id 缺失 | 最小扩展 |
| resultVariableName | `returnValue.resultVariableName` | `string?` | 新增 | 是 | 兼容旧 result 字段 | 写回 |
| callMode | `callMode` | `"sync" \| "asyncReserved"` | 是 | 是 | runtime async 不做 | UI 保留 reserved |
| error handling strategy | 无独立字段 | - | 否 | 否 | 本轮不做 | 文档记录 |
| node id / action id | `object.id` / `action.id` | `string` | 是 | 是 | reference source 依赖 action id | 保持 |
| description / caption | `object.caption` / `object.description` | `string?` | 是 | 是 | 无 | 通过 base form 写回 |

同步规则：

| 语义 | 源码字段 | 类型 | 同步规则 |
|---|---|---|---|
| targetMicroflowId | `action.targetMicroflowId` | `string` | 选择目标写入；清空目标清空 |
| targetMicroflowName/displayName | `action.targetMicroflowName` / `targetMicroflowDisplayName` | `string?` | 选择目标时保存显示快照；重命名后可保存刷新 |
| targetMicroflowQualifiedName | `action.targetMicroflowQualifiedName` | `string?` | 只作显示快照；与 metadata 不一致时 warning |
| targetModuleId | `action.targetModuleId` | `string?` | 选择目标时保存 |
| parameterMappings | `action.parameterMappings` | `MicroflowParameterMapping[]` | 按 target parameters rebuild；同 id/同名保留 |
| return binding | `action.returnValue` | object | target 有返回值时可绑定；void 时禁用/清理 |
| callMode | `action.callMode` | string | UI 修改写回 schema |
| action/node id | `action.id` / `object.id` | string | 创建时生成，reference scanner 使用 action id 作为 source object id |

## 5. Target Selection Strategy

- selector 数据来源：`adapter.getMicroflowRefs({ workspaceId, moduleId, includeArchived: false })`，不使用 mock。
- search：selector 本地搜索覆盖 `name/displayName/qualifiedName/moduleName/description`。
- self-call：`currentMicroflowId` 对应目标 disabled，并显示 `Cannot call itself directly in Stage 12.`。
- archived/unavailable target：metadata status/unavailableReason 会显示；当前请求默认 `includeArchived: false`。
- clear target：清空 `targetMicroflowId/name/displayName/qualifiedName/moduleId/version/schemaId`，清空 mappings 和 return binding。
- retarget mapping rebuild：先按 `targetParameterId` 保留，再按 `targetParameterName`/`parameterName` 保留；无法匹配的 mapping 不静默带到新 target。

## 6. Parameter Mapping Strategy

- target parameters 来源：优先 metadata API 返回的 `parameters`。当前 backend 已从目标 schema parameters 读取 id/name/type/required/default/description/order。
- current variables 来源：`VariableSelector` 使用当前 source 微流 schema 的 variable index，不读取 target 微流变量。
- expression mapping：用户手写 `ExpressionEditor` 后写回 `argumentExpression` 与 `expression`。
- source variable mapping：选择当前 source variable 时自动填同名 expression，并保存 `sourceVariableName`。
- required parameter warning：required 且 expression 为空时显示 warning。
- stale mapping 处理：target 参数找不到时显示 warning；重选 target 时按 id/name 保留可匹配 mapping，其余不静默残留。
- save/reload：mapping 位于当前 source schema action config，保存走 `PUT /api/microflows/{sourceId}/schema`，刷新后从 schema 恢复。

## 7. Return Binding Strategy

- target returnType 来源：target metadata 的 `returnType`。
- current variable selector：`OutputVariableEditor` 基于当前 source 微流 schema 与 action 创建/绑定 output variable。
- create output variable：写入 `returnValue.outputVariableName/resultVariableName/outputVariableId`，变量由当前 schema variable index 重建。
- void/none return：`isVoidMicroflowReturn` 为 true 时禁用 store result，并清理 output binding。
- type mismatch warning：现有 field issues/validation 入口可展示；完整类型校验留 Problems 深化。
- save/reload：return binding 写入当前 action `returnValue`，随 schema 保存恢复。

## 8. Reference Rebuild Strategy

后端 scanner 使用字段：

- `action.kind === "callMicroflow"`。
- `targetMicroflowId` 优先。
- `targetMicroflowQualifiedName` 仅作为缺 id 时的兼容 fallback。
- `action.id` 作为 source object id。

保存 source schema 后，`MicroflowResourceService.SaveSchemaAsync` 调用 `TryRebuildOutgoingReferencesAsync`，由 `MicroflowReferenceIndexer` 递归扫描 schema 并写入 reference 表。本轮不从前端直接写 reference 表，避免绕过 schema scanner。

手工验证路径：

1. A = `MF_SubmitPurchaseRequest` 配置 Call B = `MF_ValidatePurchaseRequest`。
2. 保存 A，确认 `PUT /api/microflows/{A.id}/schema` body 包含 `targetMicroflowId`。
3. 查询 `GET /api/microflows/{B.id}/references`，确认 callers 中包含 A。

## 9. Rename Stability Strategy

- `targetMicroflowId` 是权威引用主键。
- `targetMicroflowQualifiedName`、`targetMicroflowDisplayName`、`targetMicroflowName` 是显示快照。
- target rename 后，metadata 使用同一个 id 返回新 name/qualifiedName；UI 通过 id 解析当前 target。
- 若 schema 中 stored qualifiedName 与 metadata qualifiedName 不一致，显示 stale qualifiedName warning，不清空 target。
- A 再保存时可更新显示快照；reference scanner 仍按 id 解析。

## 10. Self-call / Cycle Warning

- 当前微流自身在 selector option 中 disabled。
- 已有 self-call 配置时，属性面板显示 self-call warning。
- 本轮不做完整调用图遍历，也不伪造 references；完整 runtime cycle detection 留到 validation/runtime 阶段。

## 11. Verification

自动测试：

- `call-microflow-config.test.ts` 覆盖 target 写入、清空、同名 mapping 保留、return binding 写回。
- `microflow-references.test.ts` 已覆盖 reference scanner 读取 `callMicroflow.targetMicroflowId` 的基础契约。

手工验收：

1. 启动前后端，打开 `/space/:workspaceId/mendix-studio/:appId`。
2. 打开 B = `MF_ValidatePurchaseRequest`，配置 `amount:Number`、`userName:String` 和 Boolean return，保存。
3. 打开 A = `MF_SubmitPurchaseRequest`，拖入 Call Microflow。
4. 打开属性面板，确认目标列表调用 `GET /api/microflow-metadata/microflows`，不显示 `Sales.ProcessOrder` / `Sales.CheckInventory` / `Sales.NotifyUser`。
5. 选择 B，确认 `targetMicroflowId` 写入 schema，target summary 显示 B 的 display/qualified/module/status。
6. 映射 `amount` 与 `userName`，绑定 return 到 `validationResult`，保存 A。
7. 刷新页面重新打开 A，确认 target/mappings/return binding 恢复。
8. 查询 `GET /api/microflows/{B.id}/references`，确认 A 是 caller。
9. 重命名 B，重新打开 A，确认 `targetMicroflowId` 不变，并显示新名称或 stale qualifiedName warning。
10. 尝试选择 A 自己，确认 disabled/warning。
11. 打开 C，确认 C 不显示 A 的 target/mapping；C 自己配置并保存后 A/C 互不污染。
12. 模拟 metadata API 失败，确认 error + retry，不 fallback mock。

本轮未完成浏览器 E2E 自动化，以上作为手工验收清单保留。
