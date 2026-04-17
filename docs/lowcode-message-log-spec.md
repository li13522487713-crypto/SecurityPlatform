# 低代码消息日志 + 执行链路统一视图规格（lowcode-message-log-spec）

> 状态：M00 预创建 stub。
> 范围：M11 后端 `RuntimeMessageLogService` + M13 前端"运行监控"Tab + M18 智能体消息日志。
>
> 统一时间线模型聚合 chatflow 消息 + workflow trace + agent 调用 + 工具调用 + dispatch 事件，跨域聚合。

## 章节占位

- §1 时间线统一模型（TimelineEntry: source / kind / timestamp / payload / traceId / spanId / sessionId / agentId / workflowId）
- §2 跨域聚合规则（chatflow 消息 / workflow trace / agent 调用 / 工具调用 / dispatch 事件 / 错误链路）
- §3 端点：`GET /api/runtime/message-log?sessionId=&workflowId=&agentId=&from=&to=`
- §4 Trace 6 维检索：traceId / 页面 / 组件 / 时间范围 / 错误类型 / 用户 / 租户
- §5 调试日志脱敏规则（密钥 / token / 手机号 / 邮箱）
- §6 与 OTel metric / trace / log 的对齐

> 完整内容由 M11 / M13 / M18 落地。
