# Microflow Stage 07 - Node Toolbox Registry & Default Config Governance

## 1. Scope

本轮完成：

- 盘点 `@atlas/microflow` 节点工具箱 registry、action registry、拖拽创建链路与属性面板空配置风险。
- 将节点工具箱分类治理为稳定的 Events / Parameters / Flow Control / Variables / Objects / Lists / Integration / Documentation / Other。
- 新增纯函数默认配置工厂 `default-node-config.ts`，集中生成新拖入节点的安全默认配置。
- 清理新节点默认配置中的 `Sales.*`、`MF_ValidateOrder`、`OrderProcessing`、`ProcessOrder`、`CheckInventory`、`NotifyUser` 等样例业务引用。
- `Call Microflow` 默认 target 为空，`Object/List/REST/Variable` 默认进入待配置状态。
- 为重点待配置节点增加 registry warning，工具箱可根据 `metadataAvailable` 等 context 展示提示。
- 确保属性面板在空 target、空 entity、空 list、空 url、空 expression 下不因默认值为空而崩溃。

本轮不做：

- 不接入 `Call Microflow` 真实 metadata 选择。
- 不接入 Domain Model metadata 绑定。
- 不实现执行引擎、trace、debug。
- 不做历史 schema migration，不批量清洗数据库中已经保存的旧 `Sales.*` schema。

## 2. Changed Files

| 文件 | 修改类型 | 说明 |
|---|---|---|
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/default-node-config.ts` | 新增 | 默认配置纯函数工厂，集中输出安全空值。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/action-registry.ts` | 修改 | P0 action 默认配置改为调用工厂，清理对象、列表、变量、REST、Call Microflow 的 demo 默认值。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/registry.ts` | 修改 | 工具箱分类重排，legacy panel entry 使用安全默认配置，增加 warning 字段。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-registry/index.ts` | 修改 | 导出默认配置工厂。 |
| `src/frontend/packages/mendix/mendix-microflow/src/node-panel/index.tsx` | 修改 | 支持新分类与 registry warning 展示，保留搜索和拖拽能力。 |
| `src/frontend/packages/mendix/mendix-microflow/src/adapters/authoring-operations.ts` | 修改 | 拖拽创建 action activity 改为优先使用 action registry 工厂，避免旧 fallback 写入 demo URL/实体。 |
| `src/frontend/packages/mendix/mendix-microflow/src/microflow-interactions.spec.ts` | 修改 | 增加默认配置无 demo 关键词和安全默认值测试。 |
| `docs/microflow-stage-01-gap-matrix.md` | 修改 | 更新 Stage 07 registry/default config 治理状态。 |

## 3. Node Registry Matrix

| 节点类型 | 分类 | 默认配置策略 | 是否仍含 demo 值 | 属性表单 | 本轮结果 |
|---|---|---|---|---|---|
| `startEvent` | Events | 空配置 | 否 | event/general | 保持可拖拽。 |
| `endEvent` | Events | 空配置 | 否 | event/output | 保持可拖拽。 |
| `parameter` | Parameters | 拖拽时生成 `parameter` / string 参数 | 否 | parameter | 保持可拖拽，不默认 order/user。 |
| `annotation` | Documentation | 通用说明文本 | 否 | annotation | 保持可拖拽。 |
| `decision` | Flow Control | expression 为空，authoring 创建时也为空 | 否 | decision | 不再默认业务表达式。 |
| `merge` | Flow Control | `firstAvailable` / `firstArrived` 合并策略 | 否 | merge | 保持可拖拽。 |
| `loop` | Flow Control | list 为空，iterator 为 `currentItem` | 否 | loop | 保持可拖拽。 |
| `breakEvent` | Flow Control | 空配置 | 否 | event/general | 保持能力，仍由拖拽校验限制 loop 内使用。 |
| `continueEvent` | Flow Control | 空配置 | 否 | event/general | 保持能力，仍由拖拽校验限制 loop 内使用。 |
| `variableCreate` / `createVariable` | Variables | `newVariable`、String、空初始表达式 | 否 | ActionActivityForm | 安全默认变量，不含 order/customer/product。 |
| `variableChange` / `changeVariable` | Variables | target 为空，表达式为空 | 否 | ActionActivityForm | 待配置。 |
| `objectCreate` / `createObject` | Objects | entity/output 为空，commit=false | 否 | ActionActivityForm | 不再默认 `Sales.Order`。 |
| `objectChange` / `changeMembers` | Objects | object variable 为空，memberChanges=[] | 否 | ActionActivityForm | 待配置。 |
| `objectRetrieve` / `retrieve` | Objects | entity/source 为空，sorts=[] | 否 | ActionActivityForm | 不再默认 `Sales.Order`。 |
| `objectCommit` / `commit` | Objects | object/list variable 为空 | 否 | ActionActivityForm | 待配置。 |
| `objectDelete` / `delete` | Objects | object/list variable 为空 | 否 | ActionActivityForm | 待配置。 |
| `objectRollback` / `rollback` | Objects | object/list variable 为空 | 否 | ActionActivityForm | 待配置。 |
| `listCreate` / `createList` | Lists / Collections | entity/list 为空 | 否 | ActionActivityForm | 不再默认 Sales list。 |
| `listChange` / `changeList` | Lists / Collections | list/object 为空，operation=`add` | 否 | ActionActivityForm | 待配置。 |
| `listAggregate` / `aggregateList` | Lists / Collections | list/output 为空，operation=`count` | 否 | ActionActivityForm | 待配置。 |
| `listOperation` | Lists / Collections | list/output/expression 为空，operation=`filter` | 否 | ActionActivityForm | 待配置。 |
| `callMicroflow` | Integration | target 为空，parameterMappings=[]，`configurationState=incomplete` | 否 | ActionActivityForm | 不再默认 `MF_ValidateOrder`。 |
| `callRest` / `restCall` | Integration | method=`GET`，url/body/headers/query/response 为空 | 否 | ActionActivityForm | 不再默认业务 URL。 |

实际源码映射：legacy toolbox 使用 `activity:<MicroflowActivityType>` 作为 registry key；authoring action 使用 `MicroflowActionKind`，例如 `activity:objectRetrieve` 映射为 `retrieve`，`activity:callRest` 映射为 `restCall`。

## 4. Removed Demo Defaults

| 旧默认值 | 所在文件 | 新默认值 | 原因 |
|---|---|---|---|
| `Sales.Professor` | `node-registry/registry.ts` | 空 target entity | Cast Object 不能默认绑定 demo 实体。 |
| `Sales.Order` | `node-registry/registry.ts` | 空 entity | Create/Retrieve/List 节点必须由用户选择实体。 |
| `newOrder` / `order` / `orders` / `orderCount` | `node-registry/registry.ts` | 空变量名或通用 `newVariable` | 避免新节点自带订单业务语义。 |
| `MF_ValidateOrder` | `node-registry/registry.ts` | 空 target + incomplete | Call Microflow 不能引用不存在的样例微流。 |
| `Order.Detail` | `node-registry/registry.ts` | 空 page target | Client page 节点不应绑定订单页面。 |
| `/api/orders/sync` | `node-registry/registry.ts` | 空 URL，method=`GET` | REST 节点不能默认调用业务/demo 地址。 |
| `Order processed` | `node-registry/registry.ts` | 空日志表达式 | Log 节点不应默认写订单文本。 |
| `WF_Order` / `Workflow.Context` / `contextObject` / `workflows` | `node-registry/registry.ts` | 空 workflow/context 字段 | Workflow 节点不应绑定订单上下文。 |
| `System.Object` | `node-registry/action-registry.ts` | 空 entity | Object/List 默认保持待配置。 |
| `https://api.example.com` | `adapters/authoring-operations.ts` fallback | 空 URL | 拖拽 fallback 不能写入 demo URL。 |

`metadata/mock-metadata.ts`、`schema/samples/*`、local runtime adapter 中的 demo 值仍保留在开发/测试/样例路径，不作为真实节点默认配置来源。

## 5. Default Config Strategy

默认配置工厂路径：`src/frontend/packages/mendix/mendix-microflow/src/node-registry/default-node-config.ts`。

工厂提供：

- `MicroflowNodeCreateContext`：包含 `microflowId`、`moduleId`、`workspaceId`、`metadataAvailable`，本轮仅用于 warning/可用性语义，不生成 fake 业务引用。
- `createDefaultActionConfig(actionKind, context)`：按 `MicroflowActionKind` 输出安全默认配置。
- `createDefaultActivityConfig(activityType, context)`：将 legacy `MicroflowActivityType` 映射到 action 默认配置，保持旧 registry key 与 schema 兼容。

设计约束：

- 纯函数，不依赖 React。
- 不调用 API。
- 不读取 `mock-metadata.ts`。
- 不写死 workspace/module/microflow。
- 不生成 fake entity、fake microflow、fake variable。

## 6. Property Panel Compatibility

- `Call Microflow`：`targetMicroflowId=""` 时 `MicroflowSelector` 可空，表单显示空 target；registry 和 action validation 给出待配置 warning。
- `Object`：entity 为空时 `EntitySelector` 可空；member changes 使用空数组，Add member change 在 entity/variable 未配置时禁用。
- `List`：list/entity/output 为空时表单可渲染，后续由 validation 或用户配置补齐。
- `Variable`：`Create Variable` 使用 `newVariable`，`Change Variable` target 为空；变量选择器支持空值。
- `REST`：URL expression 为空，headers/query/form fields 为空数组；表单可继续编辑 method、body、response。
- `Decision`：表达式为空，不再生成订单金额/status 之类业务表达式。

## 7. Verification

自动/源码验证：

- `pnpm --filter @atlas/microflow run typecheck` 通过。
- 新增 spec 覆盖：registry defaultConfig 序列化后不包含 `Sales`、`MF_ValidateOrder`、`ProcessOrder`、`CheckInventory`、`NotifyUser`、`/api/orders` 等 demo 关键词。
- 新增 spec 覆盖：`createDefaultActionConfig("callMicroflow")` target 为空且 `configurationState="incomplete"`；`createObject` 不含 `Sales`；`restCall` URL 为空。
- 源码复查：`node-registry` 目录中无上述 demo 关键词命中。

手工验收建议：

1. 打开 `/space/:workspaceId/mendix-studio/:appId`。
2. 展开 `Procurement`，打开一个真实微流。
3. 打开节点工具箱，确认分类为 Events / Parameters / Flow Control / Variables / Objects / Lists / Integration / Documentation / Other。
4. 搜索并拖入 Start、Call Microflow、Create Object、Retrieve Object、List、REST Call、Decision。
5. 检查属性面板：Call Microflow target 为空，Object/List entity 为空，REST URL 为空，Decision expression 为空。
6. 保存 schema，确认请求 body 中新节点不含 `Sales` / `MF_ValidateOrder` / demo 关键词。
7. 刷新后重新打开，确认新节点正常显示；旧 schema 中已有 demo 值不会被自动清洗。
