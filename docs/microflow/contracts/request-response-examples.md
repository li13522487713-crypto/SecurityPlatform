# 请求 / 响应示例（与 TS  types 一致）

更完整的片段见 `mendix-studio-core` 中 `src/microflow/contracts/examples/*.ts`（`exampleListMicroflowsEnvelope`、`samplePublishRequest` 等）。

## 1. 创建微流

```http
POST /api/microflows
Content-Type: application/json
```

```json
{
  "workspaceId": "ws-1",
  "input": {
    "name": "OrderFlow",
    "moduleId": "sales",
    "tags": [],
    "parameters": [],
    "returnType": { "kind": "void" }
  }
}
```

## 2. 查询列表（1-based 分页）

`GET /api/microflows?pageIndex=1&pageSize=20&sortBy=updatedAt&sortOrder=desc&status=draft&status=published&tags=order&tags=sales`

## 3. 保存 Schema

`PUT /api/microflows/{id}/schema`

```json
{
  "schema": { "id": "mf-1" },
  "baseVersion": "1.0.0",
  "saveReason": "manual"
}
```

## 4. 校验

`POST /api/microflows/{id}/validate`

```json
{
  "mode": "publish",
  "schema": { "id": "mf-1" },
  "includeWarnings": true,
  "includeInfo": true
}
```

## 5. 测试运行

`POST /api/microflows/{id}/test-run`

```json
{
  "schema": { "id": "mf-1" },
  "input": { "orderId": "O-1" }
}
```

## 6. 发布

`POST /api/microflows/{id}/publish`

```json
{
  "version": "1.1.0",
  "description": "Fix params",
  "confirmBreakingChanges": true
}
```

## 7. 获取版本

`GET /api/microflows/{id}/versions` → `MicroflowVersionSummary[]`

## 8. 回滚

`POST /api/microflows/{id}/versions/{versionId}/rollback`  
Body: `{ "reason": "rollback to stable" }`

## 9. 获取引用

`GET /api/microflows/{id}/references?includeInactive=false`

## 10. 元数据

`GET /api/microflow-metadata?workspaceId=ws-1`

## 11. 通用 API 错误

```json
{
  "success": false,
  "error": {
    "code": "MICROFLOW_NOT_FOUND",
    "message": "Microflow not found"
  },
  "timestamp": "2026-04-27T00:00:00.000Z"
}
```

## 12. 校验失败

`error.code` 为 `MICROFLOW_VALIDATION_FAILED`，且 `error.validationIssues` 为 `MicroflowValidationIssue[]`（见 `api-examples` 中 `exampleValidationErrorEnvelope`）。
