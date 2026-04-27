# Microflow Stage 15 - Call Microflow Foundation

## 1. Scope

本轮完成 Call Microflow 节点配置闭环：真实目标微流列表、`targetMicroflowId` / `targetMicroflowQualifiedName` / `targetMicroflowName` 保存、目标参数读取、参数映射、返回值绑定、当前 active microflow schema 写回、保存刷新恢复基础、后端 references 扫描字段写入，以及 A/B 微流 schema 隔离。

本轮不做 Call Microflow 执行器、完整引用管理 UI、完整循环调用检测、表达式执行引擎、trace/debug、Domain Model metadata 绑定、历史 schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/metadata/metadata-provider.tsx` | 修改 | 缺少 metadata adapter 时显示错误，不再回落 mock metadata；透传 workspace/module 请求上下文。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/selectors/MicroflowSelector.tsx` | 修改 | 目标列表改为通过 `metadataAdapter.getMicroflowRefs` 加载，支持 loading/error/empty/retry、搜索、清空、禁用当前微流。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/action-activity-form.tsx` | 修改 | Call Microflow 表单写入真实 target、参数映射、当前变量/参数表达式、返回值绑定。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/utils/call-microflow-config.ts` | 新增 | 提供 target 更新、清空、mapping 重建、return binding 和 reference descriptor 纯函数。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/__tests__/call-microflow-config.test.ts` | 新增 | 覆盖 target 保存、mapping 保留/清理、void return、return binding、reference 字段。 |
| `src/frontend/packages/mendix/mendix-microflow/src/schema/types.ts` | 修改 | 扩展 Call Microflow schema 字段：`targetMicroflowName`、mapping source variable。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/action-registry.ts` | 修改 | 新节点默认 target 仍为空，同时初始化 name/qualifiedName 空值。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/default-node-config.ts` | 修改 | 默认配置补齐 `targetMicroflowName` 空值。 |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 修改 | authoring 创建路径保留 target name/qualifiedName 空值。 |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/microflow-adapters.ts` | 修改 | legacy adapter 转换时保留 target name/qualifiedName 和 mapping source。 |
| `src/frontend/packages/mendix/mendix-microflow/src/runtime/map-authoring-p0-runtime.ts` | 修改 | Runtime DTO config 带出 `targetMicroflowName`。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/editor/MendixMicroflowEditorEntry.tsx` | 修改 | 将 workspaceId/moduleId 传给 `MicroflowEditor` metadata provider。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/studio/StudioEmbeddedMicroflowEditor.tsx` | 修改 | 嵌入式真实编辑器传递当前 moduleId，缺省使用 resource.moduleId。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 Stage 15 相关 gap 状态。 |

## 3. Metadata API Contract

目标列表使用 `adapterBundle.metadataAdapter.getMicroflowRefs`，HTTP 实现位于 `createHttpMicroflowMetadataAdapter`，实际请求：

`GET /api/microflow-metadata/microflows?workspaceId={workspaceId}&moduleId={moduleId}&includeArchived=false`

`workspaceId` 来自 `/space/:workspaceId/mendix-studio/:appId` 嵌入上下文，`moduleId` 优先使用当前 workbench tab 的 moduleId，缺省回退到当前资源 `moduleId`。loading、error、empty、retry 均在选择器中可见。缺失 adapter 或 API 失败时显示错误，不 fallback 到 `createMockMicroflowMetadataAdapter`。列表为空时显示 `No microflows available`，不会显示 `Sales.*` mock 目标。

## 4. Call Microflow Schema Contract

| 语义 | 源码字段 | 类型 | 同步规则 |
|---|---|---|---|
| 目标稳定 ID | `action.targetMicroflowId` | `string` | 选择目标时写入，清空时置空；供后端 reference scanner 优先识别。 |
| 目标名称 | `action.targetMicroflowName` | `string?` | 选择目标时保存 metadata `name`，用于刷新恢复和展示。 |
| 目标限定名 | `action.targetMicroflowQualifiedName` | `string?` | 选择目标时保存 metadata `qualifiedName`，作为后端扫描和展示辅助字段。 |
| 参数映射 | `action.parameterMappings[]` | `MicroflowParameterMapping[]` | 按目标参数重建，同名参数保留旧 expression/source，目标清空时清空。 |
| 返回绑定 | `action.returnValue.storeResult/outputVariableName/dataType` | object | 绑定到当前变量或新变量名；Void return 清空并禁用。 |
| 调用模式 | `action.callMode` | `"sync" \| "asyncReserved"` | 属性面板编辑后直接写回当前 schema。 |
| action/node id | `action.id` / `object.id` | `string` | 节点已有稳定 id 不变，供 references 描述和运行 DTO 使用。 |

## 5. Target Selection Strategy

目标列表来自真实 metadata API 的 microflow refs。选择器展示 name、qualifiedName、moduleName、status，并支持本地搜索与清空。当前微流自身通过 `schema.id` 禁用并提示 `Cannot call itself directly in Stage 15.`。archived 目标被过滤。清空目标会清空 target、mappings 和 return binding；重选目标按同名参数保留可复用 mapping，其余 stale mapping 不写入新 target。

## 6. Parameter Mapping Strategy

目标参数来自 `MetadataMicroflowRef.parameters`。每个参数显示 name/type，并提供当前微流 variable index 的 source selector，以及可手写的 expression 文本。source selector 只读取当前 schema 的 parameters、createVariable 和已有 action outputs，不读取其他微流变量。required 参数缺 expression 时显示 warning。保存时 mapping 写入当前 active microflow schema，刷新后从 schema 恢复。

## 7. Return Binding Strategy

目标 returnType 来自 `MetadataMicroflowRef.returnType`。非 Void 时可选择当前变量作为 `returnValue.outputVariableName`，也可通过 `OutputVariableEditor` 创建新接收变量；该变量继续进入当前微流 variable index。Void/None return 禁用绑定并清空 `outputVariableName`，显示 `Target microflow has no return value.`。类型筛选按 returnType.kind 做基础匹配，复杂类型校验留给后续 validator。

## 8. Reference Generation Verification

后端 `MicroflowReferenceIndexer` 扫描 `schema.objectCollection.objects[].action`，当 `kind === "callMicroflow"` 且存在 `targetMicroflowId` 或 `targetMicroflowQualifiedName` 时生成引用。本轮 schema 写入这两个字段，不直接写 reference 表。

保存后可手工验证：

1. 在 `MF_SubmitPurchaseRequest` 中配置 Call Microflow 指向 `MF_ValidatePurchaseRequest`。
2. 保存触发 `PUT /api/microflows/{MF_SubmitPurchaseRequest.id}/schema`。
3. 调用 `GET /api/microflows/{MF_ValidatePurchaseRequest.id}/references`。
4. references 中应出现 source 指向 `MF_SubmitPurchaseRequest`。

Stage 16 将继续做引用抽屉、删除保护 UI、callees/callers 联动。

## 9. Verification

自动测试：

- `packages/mendix/mendix-microflow/src/property-panel/__tests__/call-microflow-config.test.ts`

建议运行：

- `cd src/frontend && pnpm exec vitest run packages/mendix/mendix-microflow/src/property-panel/__tests__/call-microflow-config.test.ts`
- `cd src/frontend && pnpm --filter @atlas/microflow run typecheck`

手工验收覆盖：真实页面打开、Procurement 微流列表、目标列表无 `Sales.*`、选择 `MF_ValidatePurchaseRequest`、参数映射、返回绑定、保存 PUT 当前 source microflow schema、刷新恢复、references API、当前微流自身禁用、A/B 微流配置隔离。
