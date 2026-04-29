 # Microflow Action Descriptor 命名规范

> 轮次：R1-07。约束后端 `MicroflowActionExecutorRegistry.BuiltInDescriptors()`，避免 descriptor 与前端 registry / schema 语义漂移。

## Descriptor 字段约束

| 字段 | 规则 | 示例 |
|---|---|---|
| `ActionKind` | canonical lowerCamelCase，必须与 schema `action.kind` 一致 | `webServiceCall` |
| `SchemaType` | Mendix semantic action type，PascalCase + `Action` 后缀 | `WebServiceCallAction` |
| `RegistryCategory` | 稳定分类，用于矩阵与 toolbox 分组 | `integration` |
| `RuntimeCategory` | 只能是 `ServerExecutable` / `ConnectorBacked` / `RuntimeCommand` / `ExplicitUnsupported` | `ConnectorBacked` |
| `SupportLevel` | 只能使用 `MicroflowActionSupportLevel` 常量语义 | `RequiresConnector` |
| `Executor` | 真实 executor 类型名、connector 规划名或 command executor 名 | `RestCallActionExecutor` |
| `ConnectorCapability` | ConnectorBacked 必填；非 ConnectorBacked 除 RuntimeCommand 外应为空 | `SoapWebService` |
| `ErrorCode` | ConnectorBacked 必须是 `RUNTIME_CONNECTOR_REQUIRED`；Unsupported 必须是 `RUNTIME_UNSUPPORTED_ACTION` | `RUNTIME_CONNECTOR_REQUIRED` |
| `Reason` | 必须说明限制、真实执行边界或 connector 要求 | `SOAP/WSDL execution requires web service connector.` |

## RuntimeCategory 语义

| RuntimeCategory | 允许行为 | 禁止行为 |
|---|---|---|
| `ServerExecutable` | 可在服务端 runtime 真实执行，或 R1/R3 明确标记 modeled-only blocker | connector 缺失时 silent success |
| `ConnectorBacked` | 缺 capability 返回 `RUNTIME_CONNECTOR_REQUIRED`，publish 阻断 | 返回 Success 伪装执行 |
| `RuntimeCommand` | 服务端只返回 command preview，由客户端处理 | 写业务数据库或伪装服务端真实执行 |
| `ExplicitUnsupported` | 返回 `RUNTIME_UNSUPPORTED_ACTION` | 返回 Success |

## R1 特例清单

| actionKind | 当前 descriptor | R1 决策 | R3 要求 |
|---|---|---|---|
| `rollback` | `ServerExecutable/ModeledOnlyConverted/ConfiguredMicroflowActionExecutor` | `blocked-before-r3` | 替换为 `RollbackObjectActionExecutor` |
| `cast` | `ServerExecutable/ModeledOnlyConverted/ConfiguredMicroflowActionExecutor` | `blocked-before-r3` | 替换为 `CastObjectActionExecutor` |
| `listOperation` | `ServerExecutable/ModeledOnlyConverted/ConfiguredMicroflowActionExecutor` | `blocked-before-r3` | 替换为 `ListOperationActionExecutor` |

## Executor 命名规则

- 真实服务端 executor：`{Semantic}ActionExecutor`，如 `RetrieveObjectActionExecutor`。
- Connector placeholder：`ConnectorBackedActionExecutor:{actionKind}` 或明确规划名，如 `WorkflowActionExecutor`。
- Runtime command executor：`{Command}ActionExecutor`，但矩阵必须标 `server-command-preview-only`。
- Unsupported executor：统一 `ExplicitUnsupportedActionExecutor`。

## Verify 对应关系

- `verify-microflow-action-descriptor-naming.ts`：检查旧别名不得进入 descriptor / schema / registry。
- `verify-microflow-executor-coverage.ts`：检查 supported 不得 fake success，ConnectorBacked 必须有 capability gate。
- `verify-microflow-node-capability-matrix.ts`：检查 descriptor 必须被矩阵覆盖。
