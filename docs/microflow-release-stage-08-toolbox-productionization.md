# Microflow Release Stage 08 - Toolbox Productionization

## 1. Scope

本轮直接基于当前源码推进 `/space/:workspaceId/mendix-studio/:appId` 的 Microflow Toolbox 发布化，完成 Toolbox 分类治理、registry metadata 标准化、搜索、drag add、click add、default config 工厂、mock/demo 默认值清理、disabled reason、metadata dependency 标记、dirty 联动、save reload recovery 与 A/B/C microflow isolation 的代码级闭环。

本轮不做 full property panel forms、Call Microflow metadata selector、Domain Model metadata selector、validation/problems panel 专项、publish/run/trace、execution engine、mock API 或孤立 demo 页面。

依赖缺口：当前缺少浏览器 E2E 环境中的真实 A/B/C 微流数据与人工后端会话，无法在本轮自动化脚本中完成真实页面保存刷新验收。本轮最小补齐点：Toolbox 新增/拖拽统一写入当前 `MicroflowEditor` 持有的 active `MicroflowSchema`，保存仍经 `MicroflowResourceAdapter.saveMicroflowSchema` 的 HTTP `PUT /api/microflows/{microflowId}/schema`，并用单测覆盖 schema 隔离与默认配置清理。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/default-node-config.ts` | 修改 | 扩展 node create context，加入 position/source/schemaLoaded/readonly/supportedActionKinds。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts` | 修改 | 标准化 id/label/createDefaultConfig/metadataRequirements/featureStatus，拆分 Inputs/Loops 分类，扩展搜索字段与 disabled reason。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/action-registry.ts` | 修改 | 为 action registry 增加纯 `createDefaultConfig` 工厂。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/factories.ts` | 修改 | 创建 action activity 时调用 default config factory。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/drag-drop.ts` | 修改 | Break/Continue 改为可添加并提示 Loop context warning，继续写入真实 authoring schema。 |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 修改 | 从 registry factory 获取默认配置，Annotation 默认 text 为空。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-panel/index.tsx` | 修改 | 增加 200ms debounce 搜索、单击添加、上下文 disabled reason/warning、Inputs/Loops 分类。 |
| `src/frontend/packages/mendix/mendix-microflow/src/editor/index.tsx` | 修改 | 移除 editor fallback 到 `createLocalMicroflowApiClient`，Toolbox context 带 schema/readonly 状态。 |
| `src/frontend/packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts` | 修改 | 补 registry metadata、搜索、disabled reason、metadata dependency 与 Break/Continue warning 测试。 |
| `src/frontend/packages/mendix/mendix-studio-core/src/store.ts` | 修改 | 初始 store 不再引用 `sampleOrderProcessingMicroflow` 作为真实微流画布来源。 |
| `docs/microflow-p1-release-gap.md` | 修改 | 更新 Toolbox 发布化状态。 |

## 3. Toolbox Category Matrix

| Category | Nodes | 说明 |
|---|---|---|
| Events | Start, End, Error | 微流入口、结束与错误事件。 |
| Inputs | Parameter | 微流入参对象。 |
| Flow Control | Decision, Object Type Decision, Merge | 条件分支、对象类型分支与分支合并。 |
| Loops | Loop, Break, Continue | 循环容器与循环控制。Break/Continue 可添加但显示 Loop context warning。 |
| Variables | Create Variable, Change Variable | 局部变量创建与赋值。 |
| Objects | Cast Object, Create Object, Retrieve Object, Change Object, Commit Object, Delete Object, Rollback Object | 对象生命周期与成员变更。 |
| Lists / Collections | Create List, Change List, Aggregate List, List Operation | 列表创建、变更、聚合与集合操作。 |
| Integration | Call Microflow, Call REST Service, Web Service, Import/Export Mapping 等 | 集成与调用类节点；connector-backed 节点保留 unsupported/preview 状态。 |
| Documentation | Annotation | 画布注释。 |

## 4. Registry Metadata Contract

每个 registry item 现在具备稳定 `id/key`、`type`、`kind`、可选 `actionKind`、`category`、`label/title`、`description`、`iconKey`、`keywords`、`defaultSize`、`createDefaultConfig`、`warning`、`metadataRequirements` 与 `featureStatus`。Action 节点的 `actionKind` 来自后端/运行时已存在的 `MicroflowActionKind`，本轮没有新增 fake actionKind。

## 5. Default Config Strategy

默认配置工厂路径为 `src/frontend/packages/mendix/mendix-microflow/src/node-registry/default-node-config.ts`。工厂是纯函数，不依赖 React，不调用 API，不读取 mock metadata，不写 workspace/module/microflow 固定值，也不写 fake entity/fake microflow/fake variable。

默认结果：Call Microflow `targetMicroflowId=""`、`targetMicroflowQualifiedName=""`、`parameterMappings=[]`；Object/List entity、list、variable 字段为空或待配置；REST Call `url=""`；Decision expression `raw=""`；Annotation `text=""`。

## 6. Demo Default Cleanup

| 旧默认值 | 所在文件 | 新默认值 | 原因 |
|---|---|---|---|
| Annotation 默认说明文案 | `node-registry/registry.ts` | `text: ""` | 发布路径不写演示文案。 |
| Parameter 默认 `input` | `node-registry/registry.ts` | registry config name 为空；添加时仍生成安全 `parameter` 以保持 schema 可编辑 | 避免业务含义默认名。 |
| `sampleOrderProcessingMicroflow` 初始 store | `mendix-studio-core/src/store.ts` | `Unloaded` 空 schema | 真实页不得把 sample schema 当真实画布。 |

## 7. Search Strategy

Toolbox 搜索输入 200ms debounce，大小写不敏感，字段覆盖 label/title/titleZh、description、category、group/subgroup、keywords、tags、type、kind、actionKind 和 registry key。搜索结果保留 category 分组，无结果显示 `No matching nodes`，清空搜索恢复完整分类。

## 8. Drag / Click Add Strategy

Drag payload 包含 `registryId/registryKey/type/actionKind/objectKind`。Drop 时 FlowGram 从 registry key 反查 definition，通过 `addMicroflowObjectFromDragPayload` 和 default config factory 创建 authoring object，按 drop 坐标写入当前 editor schema。

Click add 改为节点卡片单击添加，仍复用同一套 `handleAddNode -> addMicroflowObjectFromDragPayload` 路径。位置取当前 viewport 的安全中心点并按当前节点数偏移，避免完全重叠。两者都会触发 `commitSchema`，dirty=true，保存成功 dirty=false。

## 9. Disabled / Warning Strategy

无 active microflow：`Open a microflow to add nodes.`；schema 未加载：`Microflow schema is loading.`；readonly：`This microflow is read-only.`；unsupported：`Not supported in current release.`。Object/List/Call Microflow 在 metadata 不可用时仍允许添加，但显示 `Metadata required for full configuration.`；Break/Continue 显示 `Requires a Loop context.`。

## 10. Metadata Dependency Strategy

`Call Microflow` 标记 `["microflows"]`；Object 节点标记 `["entities", "attributes", "associations", "enumerations"]`；List 节点标记 `["entities"]`；REST Call 为空数组。本轮只声明依赖，不实现 selector，不使用 mock metadata 作为生产目标。

## 11. Property Panel Compatibility

空配置通过现有表单的 empty string/empty array/optional fallback 展示为待配置状态：Call Microflow target 为空、Object entity 为空、List config 为空、REST url 为空、Decision expression 为空均不会写入 fake 值。属性面板深度表单专项留后续轮次。

## 12. Verification

自动验证：

| 命令 | 结果 |
|---|---|
| `pnpm --filter @atlas/microflow run typecheck` | 通过 |
| `pnpm exec vitest run packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts` | 39 passed |

手工验收待真实前后端会话执行：打开目标页，依次验证分类、搜索、点击/拖拽 Start/End/Decision/Call Microflow/Create Object/REST Call，保存后检查 `PUT /api/microflows/{A.id}/schema` body 不含 demo 默认值，刷新后 A/B/C schema 互不污染。当前代码路径不展示 `sampleOrderProcessingMicroflow` 作为真实画布，不使用 `createLocalMicroflowApiClient/localStorage` 作为真实保存。

## 13. Toolbox Current Capability Matrix

| 节点类型 | 注册路径 | category | label | actionKind/objectKind | 默认配置来源 | 是否含 demo 值 | 是否可拖拽 | 是否可点击添加 | 属性表单 | 本轮处理 |
|---|---|---|---|---|---|---|---|---|---|---|
| Start | `registry.ts` | Events | Start Event | startEvent | registry factory | 否 | 是 | 是 | event form | 标准化 metadata |
| End | `registry.ts` | Events | End Event | endEvent | registry factory | 否 | 是 | 是 | event form | return 待配置 |
| Parameter | `registry.ts` | Inputs | Parameter | parameterObject | registry + drag helper | 否 | 是 | 是 | parameter form | 默认名治理 |
| Annotation | `registry.ts` | Documentation | Annotation | annotation | registry factory | 否 | 是 | 是 | annotation form | text 清空 |
| Decision / If | `registry.ts` | Flow Control | Decision | exclusiveSplit | authoring factory | 否 | 是 | 是 | decision form | expression 为空 |
| Merge | `registry.ts` | Flow Control | Merge | exclusiveMerge | authoring factory | 否 | 是 | 是 | merge form | 分类治理 |
| Loop | `registry.ts` | Loops | Loop | loopedActivity | registry factory | 否 | 是 | 是 | loop form | warning/分类 |
| Break | `registry.ts` | Loops | Break Event | breakEvent | authoring factory | 否 | 是 | 是 | event form | 可添加 + warning |
| Continue | `registry.ts` | Loops | Continue Event | continueEvent | authoring factory | 否 | 是 | 是 | event form | 可添加 + warning |
| Create Variable | `action-registry.ts` | Variables | Create Variable | createVariable | `default-node-config.ts` | 否 | 是 | 是 | action form | factory 标准化 |
| Change Variable | `action-registry.ts` | Variables | Change Variable | changeVariable | `default-node-config.ts` | 否 | 是 | 是 | action form | factory 标准化 |
| Create Object | `action-registry.ts` | Objects | Create Object | createObject | `default-node-config.ts` | 否 | 是 | 是 | action form | metadata warning |
| Retrieve Object | `action-registry.ts` | Objects | Retrieve Object(s) | retrieve | `default-node-config.ts` | 否 | 是 | 是 | action form | metadata warning |
| Change Object | `action-registry.ts` | Objects | Change Object | changeMembers | `default-node-config.ts` | 否 | 是 | 是 | action form | metadata warning |
| Commit Object | `action-registry.ts` | Objects | Commit Object(s) | commit | `default-node-config.ts` | 否 | 是 | 是 | action form | metadata 标记 |
| Delete Object | `action-registry.ts` | Objects | Delete Object(s) | delete | `default-node-config.ts` | 否 | 是 | 是 | action form | metadata 标记 |
| Rollback Object | `action-registry.ts` | Objects | Rollback Object | rollback | `default-node-config.ts` | 否 | 是 | 是 | action form | metadata 标记 |
| Create List | `action-registry.ts` | Lists / Collections | Create List | createList | `default-node-config.ts` | 否 | 是 | 是 | action form | metadata warning |
| Change List | `action-registry.ts` | Lists / Collections | Change List | changeList | `default-node-config.ts` | 否 | 是 | 是 | action form | metadata warning |
| Aggregate List | `action-registry.ts` | Lists / Collections | Aggregate List | aggregateList | `default-node-config.ts` | 否 | 是 | 是 | action form | metadata warning |
| List Operation | `action-registry.ts` | Lists / Collections | List Operation | listOperation | `default-node-config.ts` | 否 | 是 | 是 | action form | metadata warning |
| Call Microflow | `action-registry.ts` | Integration | Call Microflow | callMicroflow | `default-node-config.ts` | 否 | 是 | 是 | action form | target 为空 |
| REST Call | `action-registry.ts` | Integration | Call REST Service | restCall | `default-node-config.ts` | 否 | 是 | 是 | action form | url 为空 |
| 其他已存在节点 | `action-registry.ts` | Integration/Workflow/Metrics/Client 等 | 源码 label | 源码 actionKind | registry factory 或 modeled default | 否 | 按 featureStatus | 按 featureStatus | 通用 action form | 未新增 fake kind |

## 14. Keyword Cleanup Matrix

| 关键词 | 命中路径 | 是否生产默认配置 | 处理方式 | 备注 |
|---|---|---|---|---|
| Sales / Sales. / Order / Customer / Product / Inventory | `metadata/mock-metadata.ts`, local/mock adapters, tests | 否 | 保留测试/mock；registry 路径已验证无命中 | mock metadata 不接生产目标页。 |
| MF_ValidateOrder / ValidateOrder / ProcessOrder / CheckInventory / NotifyUser | tests/docs/mock 搜索范围 | 否 | registry default config 无命中 | 不作为 Toolbox 默认 target。 |
| sample / demo / localhost | samples/local adapter/http base helper/docs | 否 | 生产编辑器不 fallback local；HTTP helper仅 URL base | local adapter 保留离线调试用途。 |
