# 后端 REST 草案（历史短表）

> **第 21 轮权威**：[backend-api-contract.md](./backend-api-contract.md)、[openapi-draft.yaml](./openapi-draft.yaml) 与 `mendix-studio-core` 的 `microflow/contracts/api/*.ts`（路径、方法、部分命名已演进，如下表可能**过时**）。

本轮**不**在前端仓实现真实后端；下表为早期 URL 草图，仅作留档。

```
GET    /api/microflows
POST   /api/microflows
GET    /api/microflows/{id}
PUT    /api/microflows/{id}/schema
POST   /api/microflows/{id}/validate
POST   /api/microflows/{id}/test-run
POST   /api/microflows/{id}/test-run/{runId}/cancel
GET    /api/microflows/{id}/test-run/{runId}/trace
POST   /api/microflows/{id}/publish
GET    /api/microflows/{id}/versions
GET    /api/microflows/{id}/versions/{versionId}
POST   /api/microflows/{id}/versions/{versionId}/rollback
POST   /api/microflows/{id}/versions/{versionId}/duplicate
GET    /api/microflows/{id}/references
GET    /api/microflow-metadata
POST   /api/microflow-metadata/refresh
```

请求/响应体与 `MicroflowResourceAdapter`、`TestRunMicroflowRequest/Response`、`ValidateMicroflowRequest/Response` 等类型对齐；正文为 **JSON**，主业务 body 为 **AuthoringSchema**（非 FlowGram）。
