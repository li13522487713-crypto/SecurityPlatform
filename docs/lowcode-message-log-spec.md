# 低代码消息日志 + 执行链路统一视图（lowcode-message-log-spec）

> 状态：M11 后端落地（RuntimeMessageLogService + LowCodeMessageLogEntry 实体）；M13 前端"运行监控"Tab 与 OTel 关联在 M13 完整化。
> 范围：M11 / M13 / M18。

## 1. 时间线统一模型

```ts
interface TimelineEntry {
  entryId: string;
  source: 'chatflow' | 'workflow' | 'agent' | 'tool' | 'dispatch';
  kind: string;          // message / tool_call / error / final / start / end / user_input / user_inject / pause / resume ...
  sessionId?: string;
  workflowId?: string;
  agentId?: string;
  traceId?: string;
  payload?: JsonValue;
  occurredAt: string;
}
```

后端实体：`Atlas.Domain.LowCode.Entities.LowCodeMessageLogEntry`。

## 2. 跨域聚合规则

| 源 | 写入时机 | source 字段 | kind |
| --- | --- | --- | --- |
| Chatflow SSE | 每个 user_input / user_inject / pause / resume / final 帧 | `chatflow` | 同 ChatChunk.kind |
| Workflow Run | invoke 开始 / 结束 / 错误 | `workflow` | start / end / error |
| Agent 调用 | M18 智能体调用前后 | `agent` | call / response |
| Tool 调用 | LLM tool_call 与回流 | `tool` | call / result |
| Dispatch | M13 dispatch 控制器入口 / 出口 | `dispatch` | enter / exit / patch |

## 3. 端点

`GET /api/runtime/message-log?sessionId=&workflowId=&agentId=&from=&to=&pageIndex=&pageSize=`

返回：按 `occurredAt` 降序的 `TimelineEntry[]`。pageSize 默认 100，最大 500。

## 4. Trace 6 维检索（M13 联动）

`GET /api/runtime/traces/{traceId}` + 6 维查询参数：`?page=&component=&from=&to=&errorType=&userId=&tenantId=`

M11 阶段消息日志已含 `traceId` 字段；M13 RuntimeTraceService 直接基于此实现 6 维过滤。

## 5. 调试日志脱敏

参见 `docs/lowcode-resilience-spec.md` §7。所有 payloadJson 写入前由 M13 脱敏中间件处理（M11 阶段 chatflow / workflow 的 user_input / outputs 已经过 JSON 序列化，待 M13 接入脱敏中间件）。

## 6. 与 OTel 的对齐

M13 RuntimeTraceService 落地后：

- TimelineEntry 同时落 OTel Span（attribute 含 source / kind / sessionId / workflowId）。
- TraceId 复用 Activity.Current.TraceId。

## 7. 反例

- 在 React 组件直接消费 message-log 进行业务决策 —— 必须经 dispatch / dedicated Tab；message-log 仅为只读观察视图。
- 写入未经脱敏的 token / 密钥 / 手机号 —— 由 M13 脱敏中间件守门，违反将告警 + 拒绝持久化。
