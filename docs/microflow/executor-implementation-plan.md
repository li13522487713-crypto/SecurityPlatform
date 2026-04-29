# Microflow Executor 生产化实施计划

> 轮次：R1 文档锚点。R3/R4/R5 按本计划逐项闭环，不得只改文档或把 modeled-only 返回当生产成功。

## R1 结论

- `rollback`、`cast`、`listOperation` 是当前 ServerExecutable 中的生产 blocker。
- connector-backed action 已有 capability gate，但 R3 前只有通用 connector-required 行为。
- runtime command action 只产客户端命令预览，不视为服务端真实执行。

## R3 Executor 交付清单

| actionKind | 新 executor | 必要语义 | 后端测试 | 前端 spec |
|---|---|---|---|---|
| rollback | `RollbackObjectActionExecutor` | reverted/noop/invalidated 三态；productionRun 必须 UnitOfWork；trace 输出事务结果 | `MicroflowRollbackExecutorTests` | Rollback form 字段与 reload roundtrip |
| cast | `CastObjectActionExecutor` | metadata inheritance；strict/allowNull；entity access denied | `MicroflowCastExecutorTests` | Cast form 字段与类型错误展示 |
| listOperation | `ListOperationActionExecutor` | union/intersect/subtract/contains/equals/isEmpty/head/tail/find/first/last/distinct/reverse/size；不修改输入列表 | `MicroflowListOperationExecutorTests` | 14 operation 动态字段与 outputType 推断 |

## R3 命名迁移

- 新增 `MicroflowActionDescriptorNormalizer`。
- 新增 `MicroflowSchemaMigrationService`。
- load 时 normalize；save/publish snapshot 只写 canonical。
- 旧别名只允许出现在 migration map；verify 脚本会扫描 schema/registry/docs。
- migration 失败必须阻断 publish 并返回 `MIGRATION_FAILED`。

## R3 Connector Stub

| 能力 | 接口 | 默认行为 |
|---|---|---|
| Java/server action | `IServerActionRuntime` | capability=false；返回 `RUNTIME_CONNECTOR_REQUIRED` |
| SOAP/XML | `ISoapWebServiceConnector` / `IXmlMappingConnector` | capability=false；返回 `RUNTIME_CONNECTOR_REQUIRED` |
| document | `IDocumentGenerationRuntime` | capability=false；返回 `RUNTIME_CONNECTOR_REQUIRED` |
| workflow | `IWorkflowRuntimeClient` | capability=false；返回 `RUNTIME_CONNECTOR_REQUIRED` |
| ML | `IMlRuntime` | capability=false；返回 `RUNTIME_CONNECTOR_REQUIRED` |
| external | `IExternalActionConnector` / `IExternalObjectConnector` | capability=false；返回 `RUNTIME_CONNECTOR_REQUIRED` |

## R4 Runtime 抽象与并发

- R3 先引入 `IBranchScheduler` / `SequentialBranchScheduler`，行为不变。
- R4 再实现 `ParallelBranchScheduler` + `Task.WhenAll` trueParallel。
- 引入 Gateway token、activation set、per-branch UnitOfWork、write conflict 检测。

## R4 Expression / Debug

- Expression API 必须通过 `api/v1/microflow-expressions/*` 暴露。
- 后端 lexer/parser/typechecker/evaluator/formatter/completion/diagnostics/preview 与前端 TypeScript port 语义一致。
- Debug session API 必须鉴权、限制 payload、脱敏 secret/token/password。

## R5 验收

- `verify-microflow-executor-coverage.ts` strict 模式无 fake success。
- production gate Blocker=0。
- E2E 覆盖 create/edit/save/publish/testRun/reference/debug。
- 性能报告覆盖 100/300/500 节点。
