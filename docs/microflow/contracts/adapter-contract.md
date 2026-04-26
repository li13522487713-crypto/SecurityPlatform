# Adapter 契约

## MicroflowResourceAdapter

定义于 `mendix-studio-core`：`adapter/microflow-resource-adapter.ts`。

必须实现：列表、获取、创建、更新、**saveMicroflowSchema**（可带第三参 `SaveMicroflowSchemaOptions`，与 `PUT /api/microflows/{id}/schema` 的 `baseVersion` / `saveReason` 对齐）、复制、重命名、收藏、归档/恢复、删除、**publishMicroflow**、**getMicroflowReferences**（可带 `GetMicroflowReferencesRequest` 查询：来源类型、影响级别、是否含失效引用）、**引用/版本/对比/回滚/复制版本/影响分析** 等（与接口方法签名一致）。

**本地/Mock 实现**：`createLocalMicroflowResourceAdapter`、`createMockMicroflowResourceAdapter`（Mock 为 Local 的别名）。本地存储见 `adapter/microflow-resource-storage`；字段与 [storage-model-contract.md](./storage-model-contract.md) 中的行草案**语义对齐**（不落库，localStorage 仅为演示）。

## MicroflowRuntimeAdapter

`contracts/adapter-contracts.ts` 中 `MicroflowRuntimeAdapter`；具体客户端见 `@atlas/microflow` 的 `createLocalMicroflowApiClient` 与 `MicroflowApiClient`。与 REST 的 `POST .../validate`、`.../test-run`、运行/Trace 端点的**字段映射**见 [frontend-backend-mapping.md](./frontend-backend-mapping.md)（`ValidateMicroflowRequest` 的 package 内旧形仅 `schema`；接后端时需补 `mode` 等，见该文档）。

## MicroflowMetadataAdapter

同文件中的 `MicroflowMetadataAdapter`；与 `getEntityByQualifiedName` 等纯函数组合可实现完整查询语义。全量/缓存响应 body 在 API 层要求含 **`updatedAt`**（及可选 `catalogVersion` / `version`），与 `microflow/contracts/api/microflow-metadata-api-contract.ts` 及 [backend-api-contract.md](./backend-api-contract.md) 一致。

## 与后端、HTTP 映射

- **权威对账**：[frontend-backend-mapping.md](./frontend-backend-mapping.md) + [backend-api-contract.md](./backend-api-contract.md) + [openapi-draft.yaml](./openapi-draft.yaml)。
- **统一响应 Envelope**（`MicroflowApiResponse`）由 **HTTP 客户端** 解析；**Adapter 方法** 仍返回业务 DTO，不强制包 Envelope（与本地实现一致）。

## MicroflowStorageAdapter / 独立 ValidationAdapter

本仓库**未**单独抽象；资源存储合并在 Local Adapter + `microflow-resource-storage`；前后端联合校验可统一为同一 `MicroflowValidationIssue` 结构。REST 校验见 `microflow/contracts/api/microflow-validation-api-contract.ts`。
