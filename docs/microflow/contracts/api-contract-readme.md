# 微流前端/后端契约导航

## 第 21 轮：API / 存储 / 映射（冻结，实现见 TS）

与 `src/frontend/packages/mendix/mendix-studio-core/src/microflow/contracts/**` 中类型 **一一对应**；后端实现不读子路径，仅通过 `@atlas/mendix-studio-core` 的 public export 与下列文档对账。

| 文档 | 说明 |
|------|------|
| [backend-api-contract.md](./backend-api-contract.md) | REST 路由、Envelope、各域请求/响应摘要 |
| [openapi-draft.yaml](./openapi-draft.yaml) | OpenAPI 3.0.3 草案（路径 + 主 schema 占位） |
| [api-error-code-contract.md](./api-error-code-contract.md) | `MicroflowApiErrorCode` 释义 |
| [storage-model-contract.md](./storage-model-contract.md) | DB/JSON 表字段草案与不可变/索引策略 |
| [schema-migration-contract.md](./schema-migration-contract.md) | `schemaVersion` / `migrationVersion` 与迁移策略 |
| [frontend-backend-mapping.md](./frontend-backend-mapping.md) | Resource/Runtime/Metadata Adapter → HTTP |
| [request-response-examples.md](./request-response-examples.md) | 请求/响应样例；详例见包内 `contracts/examples/*.ts` |
| [backend-implementation-readme.md](./backend-implementation-readme.md) | 后端分阶段落地建议（本仓库不实现后端） |

**旧版草案**（仍保留历史短表，**不作为唯一权威**）：

| 文档 | 说明 |
|------|------|
| [backend-api-draft.md](./backend-api-draft.md) | 早期 URL 草图 → 以 `backend-api-contract` + OpenAPI 为准 |
| [storage-model-draft.md](./storage-model-draft.md) | 早期三行表描述 → 以 `storage-model-contract` + `storage-types.ts` 为准 |

## 第 20 轮：Schema / 资源 / 运行时 / 验收

| 文档 | 说明 |
|------|------|
| [schema-contract.md](./schema-contract.md) | MicroflowAuthoringSchema 主模型 |
| [resource-contract.md](./resource-contract.md) | 资源 / 版本 / 发布 / 引用（产品模型） |
| [metadata-contract.md](./metadata-contract.md) | MetadataCatalog 与查询语义 |
| [validation-contract.md](./validation-contract.md) | MicroflowValidationIssue 与 MF_* 码 |
| [validation-codes.md](./validation-codes.md) | `microflowValidationCodes` 冻结与维护说明 |
| [runtime-trace-contract.md](./runtime-trace-contract.md) | 调试会话与 TraceFrame |
| [runtime-dto-contract.md](./runtime-dto-contract.md) | Runtime DTO v1 与 toRuntimeDto |
| [runtime-contract-readme.md](./runtime-contract-readme.md) | 运行时契约总览（DTO + Trace） |
| [adapter-contract.md](./adapter-contract.md) | Resource / Runtime / Metadata Adapter |
| [../acceptance/frontend-acceptance-checklist.md](../acceptance/frontend-acceptance-checklist.md) | 前端人工验收清单 |
| [../acceptance/sample-validation-matrix.md](../acceptance/sample-validation-matrix.md) | 样例验收集 |

## 可执行验证

在 `src/frontend` 下：

```bash
pnpm --filter @atlas/mendix-studio-core run verify-contracts
```

对应 `verifyMicroflowContracts()` 与样例 manifest（不访问网络）。
