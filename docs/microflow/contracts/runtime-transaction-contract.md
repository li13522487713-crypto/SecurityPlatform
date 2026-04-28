# Runtime Transaction Contract

## 第 58 轮 ErrorHandling 事务策略

- `rollback` 与 `customWithRollback` 必须调用 `TransactionManager.Rollback` 路径；本实现通过 `RollbackForError` / `PrepareCustomWithRollback` 写 transaction log 与 snapshot。
- `customWithoutRollback` 不 rollback，transaction 保持 active；若 handler 到 EndEvent，后续 run 正常结束策略可继续 commit 当前 staged changes。
- `continue` 不 rollback、不建 error scope，仅写 `ErrorHandlingContinue` transaction log 并沿 normal flow 继续。
- ErrorEvent 不擅自二次 rollback；它保持 upstream errorHandling 已决定的 transaction 状态，并导致 run failed。
- 本事务模型仍为 Runtime 内存 UnitOfWork，不是分布式事务或真实 DB 补偿事务。

本仓库前端不实现真实事务；Mock Runtime 只生成 transaction preview，用于 Trace/DebugPanel 与后端契约对齐。

## 第 53 轮 Runtime TransactionManager / UnitOfWork

后端已建立运行时事务地基：

- `IMicroflowTransactionManager` / `MicroflowTransactionManager` 只作用于 `RuntimeExecutionContext`，不访问业务数据库，不调用 ORM `SaveChanges`。
- `IMicroflowUnitOfWork` / `MicroflowUnitOfWork` 只维护内存 staged changes 与 operation log。
- `RuntimeExecutionContext.Transaction` 保存 `MicroflowRuntimeTransactionContext`，包含 `changedObjects`、`committedObjects`、`rolledBackObjects`、`deletedObjects`、`savepoints`、`logs`、`diagnostics`。
- 状态包含 `none`、`active`、`committed`、`rolledBack`、`failed`；模式包含 `none`、`singleRunTransaction`、`actionScoped`、`custom`。
- `CreateSavepoint` 记录 operation index 与 changed object count；`RollbackToSavepoint` 只标记/整理内存 staged changes，本轮不回滚 `VariableStore`。
- `Commit` 只把仍处于 `staged` 或已由 `TrackCommitAction` 标记为 `committed` 的变更写入 `committedObjects`，不会把 savepoint rollback 或对象级 rollback 的变更重新提交。
- `withEvents`、`refreshInClient`、`validateObject` 本轮只记录，不触发对象事件、客户端刷新或真实校验。
- `TraceFrame.output.transaction` 输出短 preview；`RunSession.transactionSummary` 与成功 `RunSession.output.transactionSummary` 输出会话摘要。
- `MicroflowRuntimeTransactionLogEntry` 可转为 `MicroflowRuntimeLogDto`，当前以短文本写入 logs tab。

本轮明确不做真实 CRUD、真实事务持久化、完整 ErrorHandling、EntityAccess enforcement 或业务表更新。

## P0 动作

- `CreateObject`：`TrackCreate` 记录 create change，`commit.enabled=true` 记录 implicit commit action。
- `ChangeMembers`：`TrackUpdate` 记录 changed members、before/after preview 与 `validateObject` 标记。
- `CommitAction`：`TrackCommitAction` 记录 `operation=commit` 的结构化变更，并可将匹配 staged changes 标记为 committed；不等于真实数据库提交。
- `DeleteAction`：`TrackDelete` 记录 delete change 与 delete behavior preview。
- `RollbackAction`：`TrackRollbackObject` 只记录指定对象 rollback operation，不等于整个 transaction rollback。
- ErrorHandling `rollback` / `customWithRollback`：调用事务 rollback 基础接口；`customWithoutRollback` / `continue` 保持事务 active，并写入 `errorHandlingKeepActive` / `errorHandlingContinue` 日志，由后续正常结束提交。

真实数据库提交、回滚、锁与权限由后端 Runtime 实现。

第 54 轮 Object CRUD Actions 应通过 `TransactionManager.TrackCreate/TrackUpdate/TrackDelete/TrackCommitAction/TrackRollbackObject` 复用本轮 change set，不应绕过 `RuntimeExecutionContext.Transaction`。

## 第 54 阶段 ActionExecutor 复用点

- `CreateObjectActionExecutor` / `ChangeMembersActionExecutor` / `DeleteActionExecutor` / `CommitActionExecutor` / `RollbackActionExecutor` 继续通过 `TransactionManager` 记录对象变更。
- List、Variable、Client RuntimeCommand、ConnectorBacked、ExplicitUnsupported action 不直接写业务事务；如后续 connector 返回对象变更，也必须回到同一 `RuntimeExecutionContext.Transaction`。
- `TraceFrame.output.transaction` 与第 54 阶段 `executorCategory/supportLevel/runtimeCommands/connectorRequests` 可以共存，DebugPanel 只按 JSON 展示。

## 第 55 轮 Loop Transaction

- Loop 不创建独立 transaction；`singleRunTransaction` 下所有 iteration 共享同一个 `RuntimeExecutionContext.Transaction`。
- Loop body 内 `CreateObject`、`ChangeMembers`、`Commit`、`Delete`、`RollbackAction` 继续写 TransactionManager log，并在对应 action trace output 中带 transaction preview。
- `BreakEvent` / `ContinueEvent` 不自动 commit 或 rollback；已 staged 的 changes 保留在当前 run transaction 中，完整 error rollback 策略留第 58 轮。
- Loop summary 可通过 `RuntimeExecutionContext.CreateTransactionSnapshot("loop")` 输出当前 transaction preview，RunSession transaction summary 包含 loop 内变更。
