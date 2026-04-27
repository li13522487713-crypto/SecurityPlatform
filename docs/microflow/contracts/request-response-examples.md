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
  "schema": {
    "schemaVersion": "1.0.0",
    "id": "mf-1",
    "name": "OrderFlow",
    "objectCollection": { "id": "root-collection", "objects": [] },
    "flows": [],
    "parameters": [],
    "returnType": { "kind": "void" }
  },
  "baseVersion": "1.0.0",
  "saveReason": "manual"
}
```

第 37 轮真实后端会为每次保存新增 `MicroflowSchemaSnapshot`，并拒绝根级 `nodes` / `edges` / `workflowJson` / `flowgram` 这类 FlowGram-only JSON。

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

响应为 `MicroflowApiResponse`，`success: true` 时 `data` 为 `MicroflowMetadataCatalog`（与 TypeScript 类型一致，含必填 `updatedAt` 等）。前端 `createHttpMicroflowMetadataAdapter` 负责解包；校验器、表达式推断、变量索引均依赖该 catalog，**不**在业务代码中硬编码实体/枚举列表。

## 11. 通用 API 错误

```json
{
  "success": false,
  "error": {
    "code": "MICROFLOW_NOT_FOUND",
    "message": "Microflow not found",
    "httpStatus": 404,
    "traceId": "00-demo"
  },
  "traceId": "00-demo",
  "timestamp": "2026-04-27T00:00:00.000Z"
}
```

## 12. 校验失败

`error.code` 为 `MICROFLOW_VALIDATION_FAILED`，且 `error.validationIssues` 为 `MicroflowValidationIssue[]`（见 `api-examples` 中 `exampleValidationErrorEnvelope`）。

## 13. HTTP 错误展示策略

- `401` → `MICROFLOW_UNAUTHORIZED`，触发宿主 `onUnauthorized`。
- `403` → `MICROFLOW_PERMISSION_DENIED`，触发宿主 `onForbidden`。
- `404` → EditorPage 显示资源不存在。

## 14. Contract Mock 错误模拟

在 MSW Contract Mock 模式下，任意 API 可加 header 或 query：

```http
GET /api/microflows?mockError=version-conflict
x-microflow-mock-error: version-conflict
```

支持 `unauthorized`、`forbidden`、`not-found`、`version-conflict`、`validation-failed`、`publish-blocked`、`reference-blocked`、`service-unavailable`、`network`。响应仍为 `MicroflowApiResponse<never>`，network 场景使用 MSW network-like error。
- `409` → 保存/发布显示版本冲突，保留本地 dirty schema。
- `422` → `MICROFLOW_VALIDATION_FAILED`，`validationIssues` 进入 ProblemPanel。
- `5xx/network/timeout` → ResourceTab / Metadata selector / DebugPanel 显示服务不可用并提供重试。
