# Runtime Transaction Contract

本仓库前端不实现真实事务；Mock Runtime 只生成 transaction preview，用于 Trace/DebugPanel 与后端契约对齐。

## 第 53 轮 Runtime TransactionManager / UnitOfWork

后端已建立运行时事务地基：

- `IMicroflowTransactionManager` / `MicroflowTransactionManager` 只作用于 `RuntimeExecutionContext`，不访问业务数据库，不调用 ORM `SaveChanges`。
- `IMicroflowUnitOfWork` / `MicroflowUnitOfWork` 只维护内存 staged changes 与 operation log。
- `RuntimeExecutionContext.Transaction` 保存 `MicroflowRuntimeTransactionContext`，包含 `changedObjects`、`committedObjects`、`rolledBackObjects`、`deletedObjects`、`savepoints`、`logs`、`diagnostics`。
- 状态包含 `none`、`active`、`committed`、`rolledBack`、`failed`；模式包含 `none`、`singleRunTransaction`、`actionScoped`、`custom`。
- `CreateSavepoint` 记录 operation index 与 changed object count；`RollbackToSavepoint` 只标记/整理内存 staged changes，本轮不回滚 `VariableStore`。
- `withEvents`、`refreshInClient`、`validateObject` 本轮只记录，不触发对象事件、客户端刷新或真实校验。
- `TraceFrame.output.transaction` 输出短 preview；`RunSession.transactionSummary` 与成功 `RunSession.output.transactionSummary` 输出会话摘要。
- `MicroflowRuntimeTransactionLogEntry` 可转为 `MicroflowRuntimeLogDto`，当前以短文本写入 logs tab。

本轮明确不做真实 CRUD、真实事务持久化、完整 ErrorHandling、EntityAccess enforcement 或业务表更新。

## P0 动作

- `CreateObject`：`TrackCreate` 记录 create change，`commit.enabled=true` 记录 implicit commit action。
- `ChangeMembers`：`TrackUpdate` 记录 changed members、before/after preview 与 `validateObject` 标记。
- `CommitAction`：`TrackCommitAction` 记录提交动作，可将匹配 staged changes 标记为 committed；不等于真实数据库提交。
- `DeleteAction`：`TrackDelete` 记录 delete change 与 delete behavior preview。
- `RollbackAction`：`TrackRollbackObject` 只记录指定对象 rollback operation，不等于整个 transaction rollback。
- ErrorHandling `rollback` / `customWithRollback`：调用事务 rollback 基础接口；`customWithoutRollback` / `continue` 保持事务 active，由后续正常结束提交。

真实数据库提交、回滚、锁与权限由后端 Runtime 实现。

第 54 轮 Object CRUD Actions 应通过 `TransactionManager.TrackCreate/TrackUpdate/TrackDelete/TrackCommitAction/TrackRollbackObject` 复用本轮 change set，不应绕过 `RuntimeExecutionContext.Transaction`。
