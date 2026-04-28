# Microflow Release Stage 11 - Object & List Nodes

## 1. Scope

本轮完成 Microflow 设计器 Object Activity 与 List / Collection Activity 的基础建模闭环：真实 Domain Model metadata adapter 接入、Entity selector、Attribute/member selector、Association selector、Enumeration value selector、Create Object、Retrieve Object、Change Object / Change Members、Commit Object、Delete Object、Rollback Object、Create List、Change List、Aggregate List、List Operation、Object/List variable index、TypeDescriptor/entity/list type 策略、stale metadata warning、dirty state integration、save reload recovery、A/B/C microflow isolation。

本轮不做 Object/List runtime executor、真实数据库 CRUD runtime、完整表达式执行引擎、完整 Problems 面板、Domain Model 编辑器、publish/run/trace 与执行引擎。

依赖缺口：真实 app/module tree 仍由既有 Studio 上下文提供 `moduleId`，本轮不重做资产树；后端 association 没有单体查询接口，本轮使用 catalog 中的 `associations` 与 `entities[].associations`；metadata 无缓存时后端仍可能返回 seed catalog，本轮前端不使用 mock adapter，不新增 fake metadata。

本轮最小补齐点：去除 legacy authoring 创建 Object 动作时的 `System.Object/object/context` 默认值；补齐 List Operation 的 right list 与 take/skip 配置；Aggregate List 对 List<Object> 源支持 attribute selector；过期的“后续阶段接 metadata”提示改为真实 metadata/stale 警告语义。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 修改 | legacy/兼容 action 创建路径不再为 Object/Retrieve/Commit/Delete/Rollback 写入默认 object/result/context/System.Object。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/action-activity-form.tsx` | 修改 | Object/List 表单使用真实 metadata 提示；Aggregate List 增加基于源 List<Object> 的 attribute selector；List Operation 增加 right list、limit/offset 字段。 |
| `src/frontend/packages/mendix/mendix-microflow/src/property-panel/forms/parameter-object-form.tsx` | 修改 | Object/List 参数类型提示改为真实 metadata 约束，不再指向后续阶段。 |
| `src/frontend/packages/mendix/mendix-microflow/src/variables/variable-index.ts` | 修改 | Create List object element type 缺 entity 的 warning 改为真实 metadata 语义。 |
| `docs/microflow-p1-release-gap.md` | 修改 | 更新 Object/List actions、metadata selector、variable index 状态与仍待后续项。 |
| `docs/microflow-release-stage-11-object-list-nodes.md` | 新增 | 记录本轮范围、契约、策略、验证与缺口。 |

## 3. Metadata API Contract

使用 `adapterBundle.metadataAdapter` 注入的 HTTP metadata adapter：`createHttpMicroflowMetadataAdapter`。实际 catalog API 为 `GET /api/microflow-metadata?workspaceId={workspaceId}&moduleId={moduleId}&includeSystem={bool}&includeArchived={bool}`。单实体 API 为 `GET /api/microflow-metadata/entities/{qualifiedName}`，单枚举 API 为 `GET /api/microflow-metadata/enumerations/{qualifiedName}`，微流/页面/工作流引用分别为 `/microflows`、`/pages`、`/workflows`。

前端 provider 为 `MicroflowMetadataProvider`，由 `MicroflowEditor` 接收 `metadataAdapter`、`metadataWorkspaceId`、`metadataModuleId` 后加载 catalog。loading/error/empty/retry 由 `EntitySelector`、`AttributeSelector`、`AssociationSelector`、`EnumerationSelector`、`EnumerationValueSelector` 展示；adapter 缺失或请求失败时显示错误，不 fallback 到 `mock-metadata.ts`。目标真实 Workbench tab 要求 `adapterBundle.mode === "http"`。

| 语义 | DTO/前端字段 | 类型 | 来源 API | 当前是否存在 | 本轮处理 |
|---|---|---|---|---|---|
| Entity id | `MetadataEntityDto.id` / `MetadataEntity.id` | `string` | `GET /api/microflow-metadata` | 是 | selector option identity 保留。 |
| Entity name | `name` | `string` | catalog | 是 | 用于搜索与展示。 |
| Entity qualifiedName | `qualifiedName` | `string` | catalog | 是 | 写入 object/list type 与 action config。 |
| Entity module | `moduleName`，前端无 `moduleId` | `string` | catalog | 部分 | 按 `moduleId` 过滤依赖后端 `MatchesModule(moduleName,moduleId)`。 |
| Entity description | 后端 `documentation` / 前端 `documentation` | `string?` | catalog | 是 | 搜索覆盖 documentation。 |
| Entity attributes/members | `attributes` | `MetadataAttribute[]` | catalog | 是 | Attribute/member selector 使用。 |
| Entity associations | `associations` refs + catalog `associations` | refs/full list | catalog | 是 | Association selector 使用 catalog full list。 |
| Entity persistable/abstract | `isPersistable`；无 abstract | `boolean` | catalog | 部分 | Entity selector 支持 `onlyPersistable`。 |
| Attribute name | `MetadataAttribute.name` | `string` | catalog | 是 | member selector 展示/搜索。 |
| Attribute qualifiedName | `qualifiedName` | `string` | catalog | 是 | `memberChanges[].memberQualifiedName` 持久化。 |
| Attribute type | `type` | `MicroflowDataType` / JSON | catalog | 是 | expression expected type 与 enum selector 使用。 |
| Attribute required | `required` | `boolean` | catalog | 是 | 本轮只展示/校验基础 warning。 |
| Attribute defaultValue | `defaultValue` | `string?` | catalog | 是 | 不自动写入 action 默认值。 |
| Attribute enumeration | `enumQualifiedName` / type `enumerationQualifiedName` | `string?` | catalog | 是 | Enumeration value selector 使用 type 指向的 qualifiedName。 |
| Attribute length/precision | 无 | - | - | 否 | 记录缺口，不臆造。 |
| Attribute readonly/calculated | `isReadonly` | `boolean` | catalog | 部分 | readonly attribute 显示 warning。 |
| Association name | `MetadataAssociationDto.name` | `string` | catalog | 是 | Association selector 展示。 |
| Association qualifiedName | `qualifiedName` | `string` | catalog | 是 | `memberChanges` / retrieve association 持久化。 |
| Association sourceEntity | `sourceEntityQualifiedName` | `string` | catalog | 是 | selector 过滤。 |
| Association targetEntity | `targetEntityQualifiedName` | `string` | catalog | 是 | List/Object type 推断。 |
| Association multiplicity | `multiplicity` | string union | catalog | 是 | Retrieve association 输出 Object 或 List<Object>。 |
| Association owner/direction | `ownerEntityQualifiedName` / `direction` | `string?` / string union | catalog | 是 | 展示与过滤使用。 |
| Enumeration name | `MetadataEnumerationDto.name` | `string` | catalog | 是 | selector 展示/搜索。 |
| Enumeration qualifiedName | `qualifiedName` | `string` | catalog | 是 | enum type identity。 |
| Enumeration values | `values[].key` | `string[]` | catalog | 是 | Enumeration value selector 使用。 |
| Enumeration captions | `values[].caption` | `string` | catalog | 是 | value selector 展示。 |
| Metadata catalog entities | `entities` | array | catalog | 是 | Entity selector 数据源。 |
| Metadata catalog enumerations | `enumerations` | array | catalog | 是 | enum selector 数据源。 |
| workflows/pages/microflows | `workflows/pages/microflows` | array | catalog/子 API | 是 | 本轮只沿用，不做 Call Microflow selector 专项。 |

## 4. Object Action Schema Contract

| Action | 关键字段 | 类型 | 同步规则 |
|---|---|---|---|
| `createObject` | `entityQualifiedName`、`outputVariableName`、`memberChanges`、`commit` | `string`、`MicroflowMemberChange[]` | entity/output/member/commit 修改写回当前 action；output 进入 `objectOutputs`，类型为 Object(entity)。 |
| `retrieve` | `retrieveSource`、`outputVariableName` | database/association source | database range `first` 输出 Object(entity)，`all/custom` 输出 List<Object(entity)>；association 由 multiplicity 推断。 |
| `changeMembers` | `changeVariableName`、`memberChanges`、`commit`、`validateObject` | object variable + changes | target 从当前 schema variable index 的 Object 变量选择；member 来自 target entity metadata。 |
| `commit` | `objectOrListVariableName`、`withEvents`、`refreshInClient` | Object/List<Object> variable | selector 只显示 Object 或 List<Object>。 |
| `delete` | `objectOrListVariableName`、`withEvents`、`deleteBehavior` | Object/List<Object> variable | selector 只显示 Object 或 List<Object>。 |
| `rollback` | `objectOrListVariableName`、`refreshInClient` | Object/List<Object> variable | 源码已支持；selector 只显示 Object 或 List<Object>。 |

## 5. List Action Schema Contract

| Action | 关键字段 | 类型 | 同步规则 |
|---|---|---|---|
| `createList` | `outputListVariableName/listVariableName`、`elementType`、`initialItemsExpression`、`listType` | `string`、`MicroflowDataType` | output 进入 `listOutputs`，类型为 List(elementType)。 |
| `changeList` | `targetListVariableName`、`operation`、`itemExpression/itemsExpression/conditionExpression` | List variable + expression | selector 只显示当前 schema List 变量；表达式只保存字符串。 |
| `aggregateList` | `listVariableName/sourceListVariableName`、`aggregateFunction`、`attributeQualifiedName/member/aggregateExpression`、`outputVariableName` | List source + primitive result | result 进入 `localVariables`，count 为 integer，sum/average 为 decimal，其他按 unknown/member 基础推断。 |
| `listOperation` | `leftListVariableName/sourceListVariableName`、`operation`、`filterExpression`、`sortExpression`、`rightListVariableName`、`limit/offset`、`outputVariableName` | List source + List output | output 进入 `listOutputs`，elementType 默认继承 source list。 |

## 6. Entity / Member Selection Strategy

Entity selector 使用 `searchEntities(catalog)`，支持 name、qualifiedName、documentation 搜索，option 文案包含 entity attributes，metadata error 时提供 retry，empty 时显示 `No entities available`。

Attribute selector 使用当前 entity 的 `attributes`，Change/Create Object 的 `memberChanges` 写 `memberQualifiedName`。Association selector 使用 catalog `associations`，按 start entity 过滤。Enumeration value selector 使用 attribute type 中的 `enumerationQualifiedName`，从 catalog `enumerations[].values` 读取，不生成 fake enum values。

## 7. Object Variable Index Strategy

`buildVariableIndex(schema, metadata)` 聚合 parameters、Create Variable、Create Object output、Retrieve output、Create List/List Operation/Aggregate output、Loop variable 与 error/system variables。Create Object output 写入 `objectOutputs`，dataType 为 `{ kind: "object", entityQualifiedName }`。Retrieve database `first` 写 Object，`all/custom` 写 List<Object>。Commit/Delete/Rollback selector 用 `variableFilter` 限制 Object 或 List<Object>。

删除节点后 index 由 schema 重新构建，变量自然消失；复制节点时 `duplicateObject` 为 Create/Retrieve Object output 生成 `_Copy` 唯一名称。

## 8. List Variable Index Strategy

Create List output 写入 `listOutputs`，dataType 为 `{ kind: "list", itemType: elementType }`。Aggregate List result 写入 `localVariables`，List Operation output 写入 `listOutputs` 且尽量继承源 list elementType。Change List / Aggregate List / List Operation selector 使用当前 active microflow schema 构建的 index，不读取全局变量数组。

删除 Create List 后 index 重建移除变量；复制 Create List / Aggregate / List Operation 时输出变量名改为唯一 `_Copy`。

## 9. Action Form Strategy

Create Object：支持 entity selector、output variable、member changes、commit enabled/with events/refresh in client、caption/documentation 基础字段。entity 为空显示 warning，不写 demo entity。

Retrieve Object：支持 database/association source、entity selector、start variable、association selector、XPath constraint、range、sort items、output variable。表达式只保存，不执行查询。

Change Object / Change Members：target selector 只显示 Object 变量；member selector 按 target object entity 读取 attribute/association；readonly attribute 与 stale member 显示 warning。

Commit/Delete/Rollback：target selector 只显示 Object 或 List<Object>；支持源码已有 withEvents、refreshInClient、deleteBehavior。

Create List：支持 list variable name、elementType、listType、initialItemsExpression、description。Object elementType 必须从真实 metadata 选 entity。

Change List：支持 target list、operation、item/items/condition expression，并对缺失 target/expression 显示 warning。

Aggregate List：支持 source list、aggregateFunction、List<Object> 属性选择、aggregateExpression、result variable 与 resultType 展示。

List Operation：支持 source list、operation、filter/sort expression、union/intersect right list、take/skip limit/offset、output list variable。

## 10. Stale Metadata Strategy

stale entity：`entityQualifiedName` 找不到时显示 `Metadata unavailable / stale entity`，保留原配置，不自动清空。

stale member：`memberQualifiedName` 找不到 attribute/association 时显示 `Metadata unavailable / stale member`，保留原字段名。

stale association：association retrieve 或 member change 找不到时显示 stale association/member warning，保留配置。

stale enum：enum attribute 的 enumeration 或 value 找不到时 selector 不生成 fake values，表达式原文保留。

不自动清空配置的原因：schema 中保存的是用户的设计态引用，metadata 可能只是临时加载失败、权限变更或跨模块过滤导致不可见，静默删除会造成不可恢复的数据丢失。

## 11. Verification

自动验证：

- `ReadLints`：`action-activity-form.tsx`、`authoring-operations.ts`、`parameter-object-form.tsx`、`variable-index.ts` 无新增 IDE lint。
- 待执行命令：`pnpm --filter @atlas/microflow typecheck`、相关 Vitest object/list helper 测试。

手工验收建议：

1. 启动 AppHost 与 AppWeb，打开 `/space/:workspaceId/mendix-studio/:appId`。
2. 打开真实微流 A，拖入 Create Object，确认 Entity selector 来自 `/api/microflow-metadata`，不显示 `Sales.Order`、`Sales.Customer`、`Sales.Product`。
3. 选择真实 entity，配置 output variable、memberChanges，保存，确认 `PUT /api/microflows/{id}/schema` body 包含 createObject 配置，刷新恢复。
4. 继续验证 Retrieve、Change/Commit/Delete/Rollback Object、Create/Change/Aggregate/List Operation 保存刷新恢复。
5. 打开 B/C 微流，确认 A 的 Object/List variables 不进入 B/C selector。
6. 模拟 metadata API 失败，确认显示错误与 retry，不 fallback mock metadata。
