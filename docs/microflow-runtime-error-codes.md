# Microflow Runtime Error Codes

本文定义 Microflow Runtime 发布版错误码。API 层不得把所有 runtime error 泛化成 500。

## Error Shape

```json
{
  "code": "VARIABLE_NOT_FOUND",
  "message": "Variable was not found.",
  "detail": "amount",
  "nodeId": "decision-1",
  "nodeName": "Check amount",
  "microflowId": "microflow-id",
  "traceId": "00-...",
  "callStack": [],
  "severity": "error",
  "recoverable": false
}
```

## Runtime Error Codes

| Code | HTTP | Severity | Recoverable | Description |
|---|---:|---|---:|---|
| `MICROFLOW_NOT_FOUND` | 404 | error | false | 微流资源不存在。 |
| `MICROFLOW_SCHEMA_INVALID` | 422 | error | false | schema 无法解析或不符合 runtime schema。 |
| `START_NODE_NOT_FOUND` | 422 | error | false | 未找到 Start 节点。 |
| `MULTIPLE_START_NODES` | 422 | error | false | 存在多个 root Start 节点。 |
| `END_NODE_NOT_FOUND` | 422 | error | false | 未找到 End 节点。 |
| `NODE_NOT_FOUND` | 422 | error | false | flow 指向的节点不存在。 |
| `FLOW_TARGET_NOT_FOUND` | 422 | error | false | 连线目标不存在。 |
| `EXPRESSION_PARSE_ERROR` | 422 | error | false | 表达式解析失败。 |
| `EXPRESSION_EVALUATION_ERROR` | 200 | error | false | 表达式运行失败。 |
| `VARIABLE_NOT_FOUND` | 200 | error | false | 变量不存在。 |
| `VARIABLE_TYPE_MISMATCH` | 200 | error | false | 变量类型不匹配。 |
| `PARAMETER_MISSING` | 422 | error | false | 必填参数缺失。 |
| `PARAMETER_TYPE_MISMATCH` | 422 | error | false | 参数类型不匹配。 |
| `DECISION_NO_MATCHING_BRANCH` | 200 | error | false | Decision 没有匹配分支且无 default。 |
| `CALL_TARGET_NOT_FOUND` | 404 | error | false | Call Microflow 目标不存在。 |
| `CALL_PARAMETER_MAPPING_ERROR` | 422 | error | false | Call Microflow 参数映射失败。 |
| `CALL_DEPTH_EXCEEDED` | 200 | error | false | 调用深度超过上限。 |
| `CALL_RECURSION_DETECTED` | 200 | error | false | 检测到递归调用且 schema 未允许。 |
| `LOOP_LIMIT_EXCEEDED` | 200 | error | false | Loop 超过最大迭代数。 |
| `RUNTIME_MAX_STEPS_EXCEEDED` | 200 | error | false | 总执行步数超过上限。 |
| `UNSUPPORTED_ACTION` | 200 | error | false | 节点 action 尚不支持。 |
| `PERMISSION_DENIED` | 403 | error | false | 当前用户无运行权限。 |
| `RUNTIME_TIMEOUT` | 408 / 200 | error | true | 执行超时。 |
| `RUNTIME_CANCELLED` | 200 | warning | true | 运行被取消。 |
| `EXTERNAL_CALL_BLOCKED` | 200 | error | false | REST call 被安全策略阻断。 |
| `REST_URL_BLOCKED` | 200 | error | false | REST URL 不符合安全策略。 |
| `REST_TIMEOUT` | 200 | error | true | REST call 超时。 |
| `REST_RESPONSE_TOO_LARGE` | 200 | error | false | REST 响应体超过限制。 |
| `OBJECT_NOT_FOUND` | 200 | error | false | 对象不存在。 |
| `OBJECT_PERMISSION_DENIED` | 403 | error | false | 对象访问权限不足。 |
| `OBJECT_TYPE_MISMATCH` | 200 | error | false | 对象类型不匹配。 |
| `TRANSACTION_ROLLBACK` | 200 | error | true | 运行失败导致事务回滚。 |
| `CONNECTOR_REQUIRED` | 422 | error | false | 需要真实 connector/domain model 能力。 |
| `TRACE_WRITE_FAILED` | 500 | error | true | trace 持久化失败。 |

## Mapping Rules

1. Design-time schema/validation 错误返回 HTTP 422。
2. Permission/auth 错误返回 HTTP 401/403。
3. Resource not found 返回 HTTP 404。
4. Runtime business failure 默认 HTTP 200，`data.success=false`。
5. Timeout 可返回 HTTP 408；若已有 run session，返回 HTTP 200 + `RUNTIME_TIMEOUT`。
6. 系统异常、存储异常、未处理异常返回 HTTP 500。

## Frontend Categories

`normalizeMicroflowApiError` 应把错误映射为：

| Category | Conditions |
|---|---|
| `auth` | HTTP 401 / `MICROFLOW_UNAUTHORIZED` |
| `permission` | HTTP 403 / `PERMISSION_DENIED` / `MICROFLOW_PERMISSION_DENIED` |
| `validation` | HTTP 422 / `MICROFLOW_VALIDATION_FAILED` / schema issues |
| `conflict` | HTTP 409 / version conflict / reference blocked |
| `notFound` | HTTP 404 / `MICROFLOW_NOT_FOUND` |
| `network` | fetch failed / timeout / service unavailable |
| `server` | HTTP 500+ |
| `runtime` | HTTP 200 + `data.success=false` |
| `unknown` | fallback |

## Trace Requirements

Runtime error 必须能追溯：

- `traceId`
- `runId`
- `microflowId`
- `nodeId`
- `nodeName`
- `nodeType`
- `callStack`
- `callDepth`
- `parentFrameId`

如果错误发生在 Call Microflow 子图中，父图 trace 必须显示 call frame，子图 trace 必须包含 child run。
