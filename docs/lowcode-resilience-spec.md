# 低代码弹性策略契约（lowcode-resilience-spec）

> 状态：M00 预创建 stub。
> 范围：M09 Workflow + M11 Chatflow + M19 工作流父级工程能力的超时 / 重试 / 退避 / 熔断 / 降级 / 配额策略。

## 章节占位

- §1 默认策略
  - 超时：默认 30s，可配 1-300s
  - 重试：默认 3 次，指数退避（500ms → 1s → 2s）
  - 熔断：5 次失败 / 60s 窗口触发，open 状态保持 30s
  - 降级：允许配置 fallback workflow id 或静态值
- §2 可配范围与租户级覆盖
- §3 配额耗尽降级策略（M19 父级工程能力联动）
- §4 灰度策略（按租户 / 按工作流 / 按节点）
- §5 OTel 指标命名约定（dispatch_latency / workflow_latency / error_count / circuit_state）
- §6 调试日志脱敏规则
- §7 反例（禁止用法）

> 完整内容由 M09 / M11 / M19 落地。
