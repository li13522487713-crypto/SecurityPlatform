# Microflow Stage 19 - Object Activity + Domain Model Metadata

## 1. Scope

本轮完成 Domain Model metadata 接入基础核对、Entity selector、Attribute/member selector、Association/enum 基础支持、Create Object 配置、Retrieve Object 配置、Change Object / Change Members 配置、Commit Object 配置、Delete Object 配置、Rollback Object 既有配置纳入、Object variable index、stale metadata warning、dirty 状态同步、保存刷新恢复与 A/B 微流隔离。

本轮不做 Object Activity 执行器、真实数据库 CRUD runtime、完整表达式引擎、trace/debug、Domain Model 编辑器本身、历史 schema migration。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/variables/object-activity-foundation.ts` | 新增 | Object Activity schema helper、Object/List<Object> 变量查询与 stale warning |
| `src/frontend/packages/mendix/mendix-microflow/src/variables/index.ts` | 修改 | 导出 Object Activity foundation helper |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/action-activity-form.tsx` | 修改 | Object Activity 表单支持 association member、enum value、stale warning 与 Object/List<Object> target 过滤 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/selectors/EntitySelector.tsx` | 修改 | 真实 metadata loading/error/empty/retry 状态与 attribute name 搜索基础支持 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/selectors/VariableSelector.tsx` | 修改 | 增加变量 predicate 过滤，供 Object/List<Object> target 使用 |
| `src/frontend/packages/mendix/mendix-studio-core/src/microflow/metadata/http-metadata-adapter.ts` | 修改 | entity/enumeration detail 请求显式带 workspaceId |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 修改 | 复制 Create/Retrieve Object 时生成不冲突 output variable name |
| `src/frontend/packages/mendix/mendix-microflow/src/schema/__tests__/object-activity-helpers.test.ts` | 新增 | Object Activity helper 与 variable index 回归测试 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 P1 中 Stage 19 相关状态 |

## 3. Metadata API Contract

使用 `createHttpMicroflowMetadataAdapter`，catalog 路径为 `GET /api/microflow-metadata?workspaceId={workspaceId}&moduleId={moduleId}`。实体详情路径为 `GET /api/microflow-metadata/entities/{qualifiedName}`，枚举详情路径为 `GET /api/microflow-metadata/enumerations/{qualifiedName}`。catalog 请求由 `MicroflowMetadataProvider` 传入 `workspaceId` / `moduleId`，HTTP client 同时通过 `X-Workspace-Id` 头携带 workspace。

| 语义 | 源码字段 | 类型 | 来源 | 当前是否存在 | 本轮处理 |
|---|---|---|---|---|---|
| Entity id | `MetadataEntity.id` | `string` | metadata API | 是 | selector/value warning 使用 |
| Entity name | `MetadataEntity.name` | `string` | metadata API | 是 | 展示与搜索 |
| Entity qualifiedName | `MetadataEntity.qualifiedName` | `string` | metadata API | 是 | schema 主存储字段 |
| Entity module | `MetadataEntity.moduleName` | `string` | metadata API | 是 | 展示保留 |
| Entity documentation | `MetadataEntity.documentation` | `string?` | metadata API | 是 | 搜索保留 |
| Entity persistable | `MetadataEntity.isPersistable` | `boolean` | metadata API | 是 | Retrieve 可限制 persistable |
| Entity abstract | 未发现 | - | metadata API | 否 | 本轮不新增 |
| Attribute id | `MetadataAttribute.id` | `string` | metadata API | 是 | selector 保留 |
| Attribute name | `MetadataAttribute.name` | `string` | metadata API | 是 | 展示与搜索 |
| Attribute qualifiedName | `MetadataAttribute.qualifiedName` | `string` | metadata API | 是 | memberChanges 主存储字段 |
| Attribute type | `MetadataAttribute.type` | `MicroflowDataType` | metadata API | 是 | Expression expectedType / enum selector |
| Attribute required | `MetadataAttribute.required` | `boolean` | metadata API | 是 | 文档盘点，本轮不强校验 |
| Attribute defaultValue | `MetadataAttribute.defaultValue` | `string?` | metadata API | 是 | 保留 |
| Attribute enumerationName | `MetadataAttribute.enumQualifiedName` / `type.enumerationQualifiedName` | `string?` | metadata API | 是 | enum value selector |
| Attribute readonly | `MetadataAttribute.isReadonly` | `boolean?` | metadata API | 是 | writableOnly 与 warning |
| Association id | `MetadataAssociation.id` | `string` | metadata API | 是 | selector 保留 |
| Association qualifiedName | `MetadataAssociation.qualifiedName` | `string` | metadata API | 是 | memberChanges / retrieve association 主存储字段 |
| Association source | `sourceEntityQualifiedName` | `string` | metadata API | 是 | association selector 过滤 |
| Association target | `targetEntityQualifiedName` | `string` | metadata API | 是 | output/expected type 基础推断 |
| Association multiplicity | `multiplicity` | union | metadata API | 是 | list/object 推断 |
| Association owner | `ownerEntityQualifiedName` | `string?` | metadata API | 是 | 文档盘点 |
| Association direction | `direction` | union | metadata API | 是 | 展示保留 |
| Enumeration qualifiedName | `MetadataEnumeration.qualifiedName` | `string` | metadata API | 是 | enum value selector |
| Enumeration values | `MetadataEnumeration.values[].key/caption` | array | metadata API | 是 | enum value selector |
| Object entity | `entityQualifiedName` | `string` | action schema | 是 | Create/Retrieve 写回 |
| Object target | `changeVariableName` / `objectOrListVariableName` | `string` | action schema | 是 | selector 过滤 |
| Member changes | `memberChanges[]` | `MicroflowMemberChange[]` | action schema | 是 | attribute/association/enum 写回 |
| Commit options | `commit` / `withEvents` / `refreshInClient` | object/boolean | action schema | 是 | 表单写回 |
| Retrieve filter | `retrieveSource.xPathConstraint` | `MicroflowExpression?` | action schema | 是 | 字符串表达式保存 |
| Retrieve range/sort | `range` / `sortItemList` | object/array | action schema | 是 | 表单写回 |

metadata loading/error/empty/retry 由 selector 与 provider 直接呈现。Provider 缺 adapter 或请求失败时不 fallback mock，因此不会继续显示 `Sales.*` sample entity。

## 4. Object Action Schema Contract

| Action | 关键字段 | 类型 | 同步规则 |
|---|---|---|---|
| `createObject` | `entityQualifiedName`, `outputVariableName`, `memberChanges`, `commit` | action schema | entity/output/member/options 改动写回当前 object，变量索引派生 Object(entity) |
| `retrieve` | `outputVariableName`, `retrieveSource.entityQualifiedName`, `xPathConstraint`, `range`, `sortItemList`, association source | action schema | range=first 输出 Object(entity)，all/custom 输出 List<Object(entity)> |
| `changeMembers` | `changeVariableName`, `memberChanges`, `commit`, `validateObject` | action schema | target 来自当前 index Object 变量，memberChanges 写回 schema |
| `commit` | `objectOrListVariableName`, `withEvents`, `refreshInClient` | action schema | target 过滤 Object/List<Object> |
| `delete` | `objectOrListVariableName`, `withEvents`, `deleteBehavior` | action schema | target 过滤 Object/List<Object> |
| `rollback` | `objectOrListVariableName`, `refreshInClient` | action schema | 源码已支持，target 过滤 Object/List<Object> |

## 5. Entity / Member Selection Strategy

Entity selector 使用真实 metadata catalog，支持 loading/error/empty/retry，不再使用 mock fallback。Attribute selector 只读取当前 entity 的 attributes，Association selector 只读取当前 entity 可达 associations。枚举 attribute 使用 metadata enumeration values 选择 key，并保存为表达式文本，不执行表达式。

stale 策略：entity/member/association/enum 找不到时保留 schema 原值，显示 `Metadata unavailable / stale ...` warning，不自动清空、不用 mock 补齐。

## 6. Object Variable Index Strategy

Create Object output variable 由 `createObject.outputVariableName + entityQualifiedName` 派生为 Object(entity)。Retrieve Object output variable 根据 range/source 派生为 Object(entity) 或 List<Object(entity)>。selector 通过 `VariableSelector.variableFilter` 限制 Object 与 List<Object>，不会显示 primitive/list primitive。

删除节点后 index 由当前 schema 重建，输出变量自然清理。复制 Create/Retrieve Object 时 `duplicateObject` 生成 `_Copy` 名称，避免变量名冲突。A/B 微流隔离依赖每个 active schema 独立调用 `buildMicroflowVariableIndex(schema)`。

## 7. Action Form Strategy

Create Object 支持真实 Entity selector、output variable、attribute/association memberChanges、enum value selector、commit options 与 stale warning。

Retrieve Object 支持 database/association source、真实 entity/association selector、XPath constraint、range、sort、output variable，并根据 range 推断变量类型。

Change Object / Change Members 支持从当前 variable index 选择 Object target，按 target entity 配置 attribute/association memberChanges，支持 readonly/stale/空值 warning。

Commit/Delete/Rollback 支持从当前 variable index 选择 Object 或 List<Object> target，primitive variables 不会出现在 selector 中，并保留各自源码已有 options。

## 8. Verification

自动测试新增 `object-activity-helpers.test.ts`，覆盖 Create Object entity 写入、output variable 进入 index、Change Object memberChanges、Retrieve range 类型推断、Commit/Delete target 过滤策略、stale warning、A/B 隔离、删除与复制输出变量、无 `Sales.*` 默认值。

手工验收建议按 Stage 19 请求中的 `/space/:workspaceId/mendix-studio/:appId` 流程执行：打开 Procurement，选择 `MF_SubmitPurchaseRequest`，配置 Create/Retrieve/Change/Commit/Delete Object，保存刷新验证 PUT `/api/microflows/{activeMicroflowId}/schema` body 与 UI 恢复，再切到 `MF_ValidatePurchaseRequest` 验证变量隔离。
