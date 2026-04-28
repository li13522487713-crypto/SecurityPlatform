# Known Limitations

| Limitation | Impact | Risk | Workaround | Plan |
| --- | --- | --- | --- | --- |
| Mendix 完整表达式语言未完全覆盖 | 复杂表达式可能无法执行或降级为校验问题 | Major | 内测只使用已在 runtime scenarios 中覆盖的表达式 | 扩展 ExpressionEvaluator 兼容矩阵 |
| Mendix 完整事务语义未完全覆盖 | 跨对象、跨 connector 的提交/回滚不等价 | Major | 使用当前 TransactionManager 支持的对象操作，不承诺分布式事务 | 引入真实对象仓储事务适配 |
| Domain Model 能力不完整 | 复杂实体、关联、权限元数据依赖 metadata cache | Major | 内测预置元数据并检查 metadata health | 扩展 metadata resolver 与实体模型 |
| Security / EntityAccess 不是完整 Mendix 安全模型 | DenyUnknownEntity 可防守，但角色矩阵不完整 | Major | 生产启用 `EntityAccessMode=DenyUnknownEntity` | 接入完整角色/实体访问矩阵 |
| Client UI 行为与 Mendix 客户端不完全一致 | showPage、showMessage 等 RuntimeCommand 需前端消费 | Major | 内测限制 RuntimeCommand 使用范围 | 完成客户端 RuntimeCommand 消费规范 |
| Connector-backed action 依赖外部连接器 | SOAP/XML/Document/ML/Workflow 等会返回 connector required | Major | 文档化 `RUNTIME_CONNECTOR_REQUIRED`，避免试点场景依赖 | 分阶段实现 connector 平台 |
| RestCall 默认禁止真实网络 | 生产 RestCall 不会直接出网 | Major | 仅在明确 allowlist、关闭 private network 后启用 | 增加连接器级出网审批 |
| RuntimeCommand 客户端消费有限 | 部分 UI 命令只在 trace/log 中可见 | Minor | 内测仅验证 showMessage 等基础命令 | 补前端消费和 UX |
| 异步 job / distributed runtime 未实现 | 长运行、多实例、排队、恢复能力有限 | Major | 内测使用单实例和限流配置 | 后续接入 job queue / distributed lock |
| 多租户权限边界为基础版 | 生产 guard 要求认证 + workspace，租户靠现有平台 JWT/tenant header | Major | 试点单租户或受控租户；权限失败查 `MICROFLOW_PERMISSION_DENIED` | 接入完整 workspace membership |
| 生产监控第一版 | 以 health、结构化日志、readiness report 为主 | Major | 配置日志采集和告警规则 | 接入 OTel metrics / tracing |
| 自动 retention job 未实现 | run/trace/log 可能持续增长 | Major | 手工按 runbook dry-run 清理测试前缀和历史运行数据 | 实现 `IMicroflowRetentionService` |
| 协作式取消未贯穿所有执行段 | cancel API 可更新会话状态，但长同步段可能不能即时停 | Major | 配置 timeout、max steps、max logs | 将 cancellation token 贯穿 runner |
