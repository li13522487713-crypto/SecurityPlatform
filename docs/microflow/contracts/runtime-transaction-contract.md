# Runtime Transaction Contract

本仓库前端不实现真实事务；Mock Runtime 只生成 transaction preview，用于 Trace/DebugPanel 与后端契约对齐。

## P0 动作

- `CommitAction`：trace/log 记录 committed preview。
- `DeleteAction`：trace/log 记录 deleted preview。
- `RollbackAction`：trace/log 记录 rolledBack preview。
- ErrorHandling `rollback` / `customWithRollback`：失败时用 `RUNTIME_TRANSACTION_ROLLED_BACK` 表示事务回滚语义。

真实数据库提交、回滚、锁与权限由后端 Runtime 实现。
