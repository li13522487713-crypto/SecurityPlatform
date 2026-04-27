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

第 43 轮前端编辑器保存会把最近加载资源的 `schemaId` 作为 `baseVersion` 传入；保存成功后重新读取资源并更新标题、`updatedAt` 与 dirty 状态。若返回 `MICROFLOW_VERSION_CONFLICT`，前端必须保留本地 dirty schema，不覆盖为远端版本。

```json
{
  "baseVersion": "current-schema-snapshot-id",
  "saveReason": "editor-save",
  "schema": {
    "schemaVersion": "1.0.0",
    "id": "mf-1",
    "name": "OrderFlow",
    "objectCollection": { "id": "root-collection", "objects": [] },
    "flows": [],
    "parameters": [],
    "returnType": { "kind": "void" }
  }
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

响应：

```json
{
  "success": true,
  "data": {
    "issues": [
      {
        "id": "stable-hash",
        "severity": "error",
        "code": "MF_START_MISSING",
        "message": "root collection 必须包含一个 StartEvent。",
        "fieldPath": "objectCollection.objects",
        "source": "event"
      }
    ],
    "summary": { "errorCount": 1, "warningCount": 0, "infoCount": 0 },
    "serverValidatedAt": "2026-04-27T00:00:00Z"
  }
}
```

不传 `schema` 时后端读取当前保存的 `MicroflowSchemaSnapshot`；传入 `schema` 时校验 inline AuthoringSchema。后端 validator 不接受 FlowGram-only 根字段。

## 5. 测试运行

`POST /api/microflows/{id}/test-run`

```json
{
  "schema": {
    "schemaVersion": "1.0.0",
    "id": "mf-1",
    "name": "OrderFlow",
    "objectCollection": {
      "id": "root-collection",
      "objects": [
        { "id": "start", "kind": "startEvent" },
        { "id": "end", "kind": "endEvent" }
      ]
    },
    "flows": [{ "id": "flow-start-end", "originObjectId": "start", "destinationObjectId": "end" }],
    "parameters": [],
    "returnType": { "kind": "void" }
  },
  "input": { "orderId": "O-1" },
  "options": {
    "simulateRestError": false,
    "decisionBooleanResult": true,
    "loopIterations": 2,
    "maxSteps": 500
  }
}
```

响应：

```json
{
  "success": true,
  "data": {
    "session": {
      "id": "run-id",
      "schemaId": "snapshot-or-current-id",
      "resourceId": "mf-1",
      "version": "0.1.0",
      "status": "success",
      "input": { "orderId": "O-1" },
      "trace": [
        {
          "id": "frame-id",
          "runId": "run-id",
          "objectId": "start",
          "outgoingFlowId": "flow-start-end",
          "status": "success",
          "startedAt": "2026-04-27T00:00:00Z",
          "endedAt": "2026-04-27T00:00:00Z",
          "durationMs": 0
        }
      ],
      "logs": [],
      "variables": []
    }
  }
}
```

后端第 42 轮会先调用 Validation `mode=testRun`；如有 error，返回 `MICROFLOW_VALIDATION_FAILED`，`error.validationIssues` 可进入 ProblemPanel。Mock Runtime 不执行真实数据库或外部 REST。

## 6. 发布

`POST /api/microflows/{id}/publish`

```json
{
  "version": "1.1.0",
  "description": "Fix params",
  "confirmBreakingChanges": true
}
```

## 7. 引用查询与重建

`POST /api/microflows/{sourceId}/references/rebuild` 会基于当前保存的 `MicroflowAuthoringSchema` 重建 source 微流的 outgoing references；`GET /api/microflows/{targetId}/references` 返回引用 target 的来源列表。

```json
{
  "success": true,
  "data": [
    {
      "id": "ref-1",
      "targetMicroflowId": "mf-notify-user",
      "sourceType": "microflow",
      "sourceId": "mf-order-process",
      "sourceName": "Order Process",
      "sourcePath": "Sales.OrderProcess",
      "sourceVersion": "0.1.0",
      "referencedVersion": null,
      "referenceKind": "callMicroflow",
      "impactLevel": "medium",
      "description": "Microflow call from Sales.OrderProcess to mf-notify-user",
      "canNavigate": true
    }
  ],
  "error": null
}
```

## 8. Impact Analysis

`GET /api/microflows/{id}/impact?includeBreakingChanges=true&includeReferences=true` 会返回引用数量、breaking changes 与综合 impact level。没有发布快照时 `impactLevel=none`，但 summary 仍可带 reference count。

```json
{
  "success": true,
  "data": {
    "resourceId": "mf-notify-user",
    "currentVersion": "1.0.0",
    "nextVersion": "1.1.0",
    "references": [],
    "breakingChanges": [
      {
        "id": "stable-id",
        "severity": "high",
        "code": "PARAMETER_REMOVED",
        "message": "参数 userId 已删除。",
        "fieldPath": "parameters.userId",
        "before": "userId",
        "after": null
      }
    ],
    "impactLevel": "high",
    "summary": {
      "referenceCount": 1,
      "breakingChangeCount": 1,
      "highImpactCount": 1,
      "mediumImpactCount": 0,
      "lowImpactCount": 0
    }
  },
  "error": null
}
```

第 38 轮真实后端发布成功后，`data` 包含：

- `resource`：已更新为 `status=published`、`publishStatus=published`。
- `version`：新增 `MicroflowVersionSummary`，含 `schemaSnapshotId` 与 `isLatestPublished=true`。
- `snapshot`：不可变 `MicroflowPublishedSnapshot`，含 AuthoringSchema 与 `schemaHash`。
- `validationSummary` / `impactAnalysis`：发布前基础校验和影响分析结果。

## 7. 获取版本

`GET /api/microflows/{id}/versions` → `MicroflowVersionSummary[]`

`GET /api/microflows/{id}/versions/{versionId}` → `MicroflowVersionDetail`，其中 `snapshot.schema` 为历史发布 AuthoringSchema，`diffFromCurrent` 为当前 schema 与该版本的基础 diff。

## 8. 回滚

`POST /api/microflows/{id}/versions/{versionId}/rollback`  
Body: `{ "reason": "rollback to stable" }`

回滚返回 `MicroflowResource`。后端会从历史 version snapshot 创建新的 current schema snapshot，不修改旧 snapshot / publish snapshot。

复制历史版本：

```http
POST /api/microflows/{id}/versions/{versionId}/duplicate
Content-Type: application/json
```

```json
{
  "name": "OrderFlowCopy",
  "displayName": "Order Flow Copy",
  "moduleId": "sales",
  "tags": ["copied"]
}
```

比较当前版本：

`GET /api/microflows/{id}/versions/{versionId}/compare-current` → `MicroflowVersionDiff`

发布前影响分析：

`GET /api/microflows/{id}/impact?version=1.1.0&includeBreakingChanges=true` → `MicroflowPublishImpactAnalysis`

## 9. 获取引用

`GET /api/microflows/{id}/references?includeInactive=false`

## 10. 元数据

`GET /api/microflow-metadata?workspaceId=ws-1`

响应为 `MicroflowApiResponse`，`success: true` 时 `data` 为 `MicroflowMetadataCatalog`（与 TypeScript 类型一致，含必填 `updatedAt` 等）。前端 `createHttpMicroflowMetadataAdapter` 负责解包；校验器、表达式推断、变量索引均依赖该 catalog，**不**在业务代码中硬编码实体/枚举列表。

常用子资源：

```http
GET /api/microflow-metadata/entities/Sales.Order
GET /api/microflow-metadata/enumerations/Sales.OrderStatus
GET /api/microflow-metadata/microflows?includeArchived=false
GET /api/microflow-metadata/pages
GET /api/microflow-metadata/workflows
GET /api/microflow-metadata/health
```

`qualifiedName` 含点号时可直接传路径或 URL encode；后端使用 catch-all route 解析。未知实体或枚举返回 `MICROFLOW_METADATA_NOT_FOUND` 标准 envelope。

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
