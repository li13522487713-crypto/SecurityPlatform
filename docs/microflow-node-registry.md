# Microflow Node Registry（30+ 节点权威清单）

> 本表对齐用户 §5 节点清单，记录每个节点的 ID（`registry.key`）、中英文名、分类、对应
> `kind` / `actionKind`、属性表单、`engineSupport` 标级和后端 executor 文件。所有数据均来自
> `packages/mendix/mendix-microflow/src/node-registry/registry.ts` 与 `action-registry.ts`，可被
> [`toolbox-cleanliness.spec.ts`](../src/frontend/packages/mendix/mendix-microflow/src/node-registry/toolbox-cleanliness.spec.ts)
> 自动校验。

## engineSupport 等级

| 等级 | 含义 | 后端表现 |
|------|------|---------|
| `supported` | runtime 引擎已对接具体 executor，testRun 真实执行 | `MicroflowActionExecutionStatus.Success` |
| `partial` | runtime 仅在 connector / allowRealHttp 启用后才真实执行 | `RUNTIME_CONNECTOR_REQUIRED` 或 `Success`（视开关） |
| `unsupported` | runtime 显式不支持，仅作为画布建模占位 | `RUNTIME_UNSUPPORTED_ACTION` |

## 5.1 事件 Events

| ID / Type | 中 / 英文 | 后端 Executor | engineSupport |
|----------|-----------|---------------|----------------|
| `startEvent` | 开始事件 / Start Event | `MicroflowRuntimeEngine.ExecuteSingleOutgoing` | supported |
| `endEvent` | 结束事件 / End Event | `MicroflowRuntimeEngine.ExecuteEnd`（求 `returnValue`） | supported |
| `errorEvent` | 错误事件 / Error Event | `MicroflowRuntimeEngine` 主循环 | supported |
| `breakEvent` | 中断事件 / Break Event | `BreakActionExecutor`（loop 内有效） | supported |
| `continueEvent` | 继续事件 / Continue Event | `ContinueActionExecutor`（loop 内有效） | supported |

## 5.2 网关 / 决策 Gateways & Decisions

| ID / Type | 中 / 英文 | 后端 Executor | engineSupport |
|----------|-----------|---------------|----------------|
| `decision` | 决策 / Decision (XOR) | `MicroflowRuntimeEngine.ExecuteDecision` | supported |
| `objectTypeDecision` | 对象类型决策 / Object Type Decision | runtime 主路径需 metadata | partial |
| `merge` | 合并 / Merge | `MicroflowRuntimeEngine.ExecuteSingleOutgoing` (`exclusiveMerge`) | supported |
| `parallelGateway` | 并行网关 / Parallel Gateway (AND) | 仅画布建模 | unsupported |
| `inclusiveGateway` | 包含网关 / Inclusive Gateway (OR) | 仅画布建模 | unsupported |

## 5.3 对象操作 Object Actions

| ID / activityType / actionKind | 中 / 英文 | 后端 Executor 文件 | engineSupport |
|-------------------------------|-----------|--------------------|---------------|
| `activity:objectRetrieve` / `retrieve` | 检索对象 / Retrieve Object(s) | `RetrieveObjectActionExecutor` | supported |
| `activity:objectCreate` / `createObject` | 创建对象 / Create Object | `CreateObjectActionExecutor` | supported |
| `activity:objectChange` / `changeMembers` | 修改对象 / Change Object | `ChangeObjectActionExecutor` | supported |
| `activity:objectCommit` / `commit` | 提交对象 / Commit Object(s) | `CommitObjectActionExecutor` | supported |
| `activity:objectDelete` / `delete` | 删除对象 / Delete Object(s) | `DeleteObjectActionExecutor` | supported |
| `activity:objectRollback` / `rollback` | 回滚对象 / Rollback Object | `ConfiguredMicroflowActionExecutor`（描述符） | supported |
| `activity:objectCast` / `cast` | 转换对象 / Cast Object | `ConfiguredMicroflowActionExecutor` | supported (modeledOnlyConverted) |

## 5.4 列表操作 List Actions

| ID / activityType / actionKind | 中 / 英文 | 后端 Executor | engineSupport |
|-------------------------------|-----------|---------------|---------------|
| `activity:listCreate` / `createList` | 创建列表 / Create List | `CreateListActionExecutor` | supported |
| `activity:listChange` / `changeList` | 修改列表 / Change List | `ChangeListActionExecutor` | supported |
| `activity:listOperation` / `listOperation` | 列表操作 / List Operation | `ConfiguredMicroflowActionExecutor` | supported |
| `activity:listAggregate` / `aggregateList` | 聚合列表 / Aggregate List | `AggregateListActionExecutor` | supported |
| `activity:listFilter` / `filterList` | 过滤列表 / Filter List | `FilterListActionExecutor` | supported |
| `activity:listSort` / `sortList` | 排序列表 / Sort List | `SortListActionExecutor` | supported |

## 5.5 变量操作 Variable Actions

| ID / activityType / actionKind | 中 / 英文 | 后端 Executor | engineSupport |
|-------------------------------|-----------|---------------|---------------|
| `activity:variableCreate` / `createVariable` | 创建变量 / Create Variable | `MicroflowRuntimeEngine.ExecuteCreateVariable` | supported |
| `activity:variableChange` / `changeVariable` | 修改变量 / Change Variable | `MicroflowRuntimeEngine.ExecuteChangeVariable` | supported |

## 5.6 调用操作 Call Actions

| ID / actionKind | 中 / 英文 | 后端 Executor | engineSupport |
|-----------------|-----------|---------------|---------------|
| `callMicroflow` | 调用微流 / Call Microflow | `MicroflowRuntimeEngine.ExecuteCallMicroflowAsync`（递归 + 调用栈） | supported |
| `callJavaAction` | 调用 Java 动作 | `ConfiguredMicroflowActionExecutor`（Connector） | partial |
| `callJavaScriptAction` | 调用 JavaScript 动作 | nanoflow only | unsupported |
| `callNanoflow` | 调用纳流 | nanoflow only | unsupported |
| `restCall` | 调用 REST 服务 | `RestCallActionExecutor`（默认安全策略阻断） | partial |
| `webServiceCall` | 调用 Web Service | Connector | partial |
| `callExternalAction` | 调用外部动作 | Connector | partial |

## 5.7 循环控制 Loop Control

| ID / Type | 中 / 英文 | 后端 Executor | engineSupport |
|----------|-----------|---------------|---------------|
| `loop` | 循环 / Loop | `MicroflowFlowNavigator` + `MicroflowLoopExecutor` (Navigator 主路径) | partial |
| `breakEvent` | Break | `BreakActionExecutor` | supported |
| `continueEvent` | Continue | `ContinueActionExecutor` | supported |

## 5.8 错误处理 Error Handling

| ID / Type / actionKind | 中 / 英文 | 后端 Executor | engineSupport |
|------------------------|-----------|---------------|---------------|
| `activity:throwException` / `throwException` | 抛出异常 / Throw Exception | `ThrowExceptionActionExecutor` | supported |
| `tryCatch` | 捕获异常 / Try-Catch | 仅画布建模 | unsupported |
| `errorHandler` | 错误处理器 / Error Handler | Activity errorHandling 字段（rollback/continue/customWith*） | partial |

## 5.9 注释 Annotation

| ID / Type | 中 / 英文 | 后端 Executor | engineSupport |
|----------|-----------|---------------|---------------|
| `annotation` | 注释 / Annotation | 不参与 runtime；持久化到 schema | supported |

## 5.10 参数 Parameters

| ID / Type | 中 / 英文 | 后端 Executor | engineSupport |
|----------|-----------|---------------|---------------|
| `parameter` | 输入参数 / Parameter | `MicroflowRuntimeEngine.BindParameters` | supported |

## 默认配置一致性检查

`toolbox-cleanliness.spec.ts` 守门：

- `microflowObjectNodeRegistries / microflowActionNodePanelRegistries / defaultMicroflowActionRegistry`
  的 `defaultConfig` 序列化结果中**不允许出现** `Sales\.` 或 mock 形式的 `MF_<MicroflowName>`。
- `createDefaultActionConfig(actionKind)` 的输出对每个 P0 actionKind 都不能引入 mock。
- 7 个新增节点（parallelGateway / inclusiveGateway / tryCatch / errorHandler / throwException /
  filterList / sortList）必须存在并具备明确的 engineSupport 等级。

## 关联文档

- 界面结构：[`microflow-canvas-ui-design.md`](microflow-canvas-ui-design.md)
- 后端引擎：[`microflow-runtime-engine-design.md`](microflow-runtime-engine-design.md)
- 端到端流程：[`microflow-e2e-checklist.md`](microflow-e2e-checklist.md)
