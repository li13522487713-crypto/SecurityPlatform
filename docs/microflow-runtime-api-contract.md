# Microflow Runtime API Contract

本文定义 Microflow Runtime 发布版 API 契约。权威路径前缀为 `api/v1`。

## Common Headers

| Header | Required | Description |
|---|---|---|
| `Authorization: Bearer <token>` | Yes | 登录态。 |
| `X-Tenant-Id` | Yes | 租户隔离。 |
| `X-Workspace-Id` | Yes | 工作区上下文。 |
| `X-User-Id` | Optional | 调试上下文，服务端仍以认证主体为准。 |

## Response Envelope

正常 API 使用统一 envelope：

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "OK",
  "traceId": "00-...",
  "data": {}
}
```

Runtime business failure 可返回 HTTP 200 且 `data.success=false`，系统错误仍按 HTTP status 返回。

## POST /api/v1/microflows/{id}/test-run

### Request

```json
{
  "workspaceId": "workspace-id",
  "moduleId": "module-id",
  "schemaId": "schema-snapshot-id",
  "version": "1.0.0",
  "schema": {},
  "inputs": {
    "amount": 120,
    "userName": "Alice"
  },
  "mode": "debug",
  "useDraft": true,
  "timeoutMs": 30000,
  "maxSteps": 1000,
  "maxLoopIterations": 1000,
  "maxCallDepth": 20,
  "correlationId": "correlation-id",
  "options": {
    "allowRealHttp": false,
    "dryRun": true,
    "includeVariableSnapshots": true
  }
}
```

兼容字段：

- `input`：旧字段，等价于 `inputs`。
- `options.maxSteps`：旧字段，等价于根级 `maxSteps`。
- `schema`：存在时执行 draft schema；不存在时执行资源当前 schema 或指定 snapshot。

### Success Response

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "OK",
  "traceId": "00-...",
  "data": {
    "runId": "run-id",
    "microflowId": "microflow-id",
    "traceId": "00-...",
    "success": true,
    "status": "succeeded",
    "output": true,
    "durationMs": 23,
    "executedNodes": ["start", "decision", "endTrue"],
    "trace": [
      {
        "id": "frame-id",
        "runId": "run-id",
        "microflowId": "microflow-id",
        "nodeId": "decision",
        "nodeName": "Check amount",
        "nodeType": "exclusiveSplit",
        "eventType": "branch",
        "timestamp": "2026-04-29T00:00:00Z",
        "durationMs": 3,
        "status": "success",
        "callDepth": 0,
        "parentFrameId": null,
        "inputSnapshot": {},
        "outputSnapshot": {},
        "error": null
      }
    ],
    "callStack": [],
    "error": null,
    "warnings": []
  }
}
```

### Runtime Failure Response

Runtime business failure 使用 HTTP 200，`data.success=false`：

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "OK",
  "traceId": "00-...",
  "data": {
    "runId": "run-id",
    "traceId": "00-...",
    "success": false,
    "status": "failed",
    "output": null,
    "durationMs": 9,
    "executedNodes": ["start", "decision"],
    "trace": [],
    "callStack": [],
    "error": {
      "code": "EXPRESSION_EVALUATION_ERROR",
      "message": "Expression failed.",
      "detail": "Variable 'amount' was not found.",
      "nodeId": "decision",
      "nodeName": "Check amount",
      "microflowId": "microflow-id",
      "traceId": "00-...",
      "severity": "error",
      "recoverable": false
    }
  }
}
```

### HTTP Status Mapping

| Case | HTTP | Envelope / Data |
|---|---:|---|
| Resource not found | 404 | error envelope |
| Permission denied | 403 | error envelope |
| Schema/design validation failed | 422 | error envelope with validation issues |
| Runtime business failure | 200 | `data.success=false` |
| Timeout | 408 or 200 | Prefer `data.success=false`, code `RUNTIME_TIMEOUT` |
| System/storage failure | 500 | ProblemDetails/error envelope |

## POST /api/v1/microflows/{id}/validate

### Request

```json
{
  "schema": {},
  "mode": "testRun",
  "includeWarnings": true,
  "includeInfo": true
}
```

### Response

```json
{
  "summary": {
    "errorCount": 0,
    "warningCount": 0,
    "infoCount": 0
  },
  "issues": [
    {
      "code": "FLOW_TARGET_NOT_FOUND",
      "severity": "error",
      "message": "Flow target does not exist.",
      "objectId": "node-id",
      "flowId": "flow-id",
      "edgeId": "flow-id",
      "fieldPath": "flows[0].targetObjectId"
    }
  ]
}
```

Requirements:

- Publish 前必须跑 design-time validation。
- Test-run 前必须跑 runtime validation。
- UnsupportedAction 必须在 validation issue 中提示。
- issue 必须携带 `objectId`/`flowId`/`fieldPath`，前端可定位。

## POST /api/v1/microflows/{id}/publish

### Request

```json
{
  "version": "1.0.0",
  "description": "Release version",
  "schemaId": "schema-snapshot-id"
}
```

### Response

```json
{
  "published": true,
  "version": "1.0.0",
  "schemaSnapshotId": "snapshot-id",
  "publishedAt": "2026-04-29T00:00:00Z",
  "issues": []
}
```

## GET /api/v1/microflows/{id}/references

### Response

```json
{
  "items": [
    {
      "sourceMicroflowId": "submit-id",
      "targetMicroflowId": "validate-id",
      "sourceObjectId": "call-node",
      "sourceActionId": "call-action",
      "referenceKind": "callMicroflow"
    }
  ],
  "total": 1
}
```

Delete/archive 必须在 inbound reference 大于 0 时返回 409。

## GET /api/v1/microflow-metadata

### Response

```json
{
  "version": "metadata-version",
  "modules": [],
  "entities": [],
  "microflows": []
}
```

Production path 不得回退到 `mock-metadata.ts`。

## Frontend Parsing Rules

前端 runtime adapter 必须：

1. 不伪造结果。
2. 读取 `data.runId`、`data.traceId`、`data.success`、`data.output`、`data.error`、`data.trace`。
3. 409/422/403/401/500 通过 `normalizeMicroflowApiError` 归一化。
4. Runtime failure `data.success=false` 显示为运行失败，但不当作网络错误。
5. trace frame 点击定位到 `nodeId/objectId`。
