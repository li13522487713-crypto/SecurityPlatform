# 低代码弹性策略契约（lowcode-resilience-spec）

> 状态：M09 落地。
> 范围：M09 Workflow + M11 Chatflow + M19 工作流父级工程能力。

## 1. 默认策略

| 项 | 默认值 | 可配范围 | 备注 |
| --- | --- | --- | --- |
| timeoutMs | 30_000 | 1_000 - 600_000 | 单次调用超时 |
| retry.maxAttempts | 3 | 1 - 10 | 含首次尝试 |
| retry.backoff | exponential | fixed / exponential | exponential = initialDelay * 2^(n-1) |
| retry.initialDelayMs | 500 | 0 - 60_000 | |
| circuitBreaker.failuresThreshold | 5 | 1 - 100 | 滑窗内累计 |
| circuitBreaker.windowMs | 60_000 | 1_000 - 600_000 | 失败累计窗口 |
| circuitBreaker.openMs | 30_000 | 1_000 - 600_000 | 半开等待 |

## 2. 配置层级

1. **全局默认**：M09 `RuntimeWorkflowExecutor` / M11 `RuntimeChatflowExecutor` 内置常量。
2. **每次调用**：`request.resilience` 完整覆盖。
3. **租户级覆盖**（M19 落地）：`WorkflowQuotaService` 按 tenant 注入默认策略。

## 3. 降级策略

- `fallback.kind = "static"`：直接返回固定 `staticValue`，包装为 `{ value }`。
- `fallback.kind = "workflow"`：调用 `fallback.workflowId` 作为兜底；嵌套 fallback 不再展开（避免环）。
- 无 fallback 且重试用尽 → 抛 `BusinessException("WORKFLOW_INVOKE_FAILED", ...)`。

## 4. 配额耗尽降级（M19）

- 当 `WorkflowQuotaService` 返回租户已超出工作流执行配额：
  1. 直接走 `fallback`（若配置）。
  2. 否则返回 HTTP 429 + `code = "WORKFLOW_QUOTA_EXCEEDED"`。
- Atlas Alert 模块同步告警。

## 5. 灰度策略

- 新版本工作流可配 `canary.weight`（M14 / M19 联动）。
- dispatch 控制器按 weight 分流；命中 canary 的请求标记 `traceAttr.canary=true`。
- canary 失败率超过阈值（默认 10%）时自动回退主版本。

## 6. OTel 指标命名

> M13 收尾（2026-04）已落地：`Atlas.Infrastructure.Services.LowCode.LowCodeOtelInstrumentation` 暴露 ActivitySource `lowcode.runtime` + Meter `lowcode.runtime` v1.0.0；AppHost.Program 已 `AddSource(...) / AddMeter(...)` 接入主 OTel pipeline。

| 指标 | 类型 / 单位 | 标签 |
| --- | --- | --- |
| `lowcode.dispatch_latency` | Histogram (ms) | tenant.id / lowcode.app_id / status |
| `lowcode.workflow_latency` | Histogram (ms) | tenant.id / status |
| `lowcode.error_count` | Counter | tenant.id / source / error.kind \| span.name |
| `lowcode.circuit_state` | UpDownCounter | tenant.id / lowcode.workflow_id / state |
| `lowcode.chatflow.stream_chunk` | Counter | tenant.id / lowcode.chatflow_id / chunk.kind |

ActivitySource span 命名约定：

- `lowcode.dispatch.start`：dispatch 入口（StartTraceAsync）
- `action.{kind}` / `dispatcher.start` / `workflow.invoke` / `chatflow.stream` / `asset.upload` / `state.patch` / `error`：AddSpanAsync 写入

## 7. 调试日志脱敏

所有 trace/log 中表达式参数与 outputs 必须经脱敏中间件：

- 密钥模式：`AKIA[0-9A-Z]{16}` / `sk-[A-Za-z0-9]{32,}` → `***REDACTED***`
- 手机号 / 身份证 / 邮箱按 GB/T 35273 规则掩码（`138****1234`）
- 自定义脱敏策略由租户级 `LowCodeSensitiveLabel`（M14 引入）配置

## 8. 反例（禁止用法）

- 在 React 组件内直接捕获工作流异常并 retry —— 必须经 `withResilience` / 后端策略统一处理。
- 关闭 timeout 触发"无限等待" —— `timeoutMs` 必须 > 0；前端 propertyPanels 在 0 时给出告警。
