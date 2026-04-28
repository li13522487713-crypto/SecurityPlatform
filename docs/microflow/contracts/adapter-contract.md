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

## 第 31 轮：统一 Adapter Bundle

`mendix-studio-core` 现在提供统一切换层：

- `MicroflowAdapterMode = "mock" | "local" | "http"`。
- `MicroflowAdapterFactoryConfig` 只接收宿主上下文：`mode`、`apiBaseUrl`、`workspaceId`、`tenantId`、`currentUser`、请求头与错误回调。
- `MicroflowAdapterBundle` 统一输出 `resourceAdapter`、`metadataAdapter`、`runtimeAdapter`、`validationAdapter` 与可选 HTTP `apiClient`。
- `createMicroflowAdapterBundle(config)` 是默认入口；组件可直接传 `adapterBundle`，也可传 `adapterConfig` 由 core 内部创建。

生产模式默认 `http`，开发模式默认 `local`。`http` 模式必须显式配置 `apiBaseUrl`，不会无声 fallback 到 mock；mock 仅用于 dev/test/sample，local 仅用于本地开发或离线调试。

## MicroflowStorageAdapter / 独立 ValidationAdapter

资源存储合并在 Local Adapter + `microflow-resource-storage`；前后端联合校验统一为同一 `MicroflowValidationIssue` 结构。第 31 轮新增 `MicroflowValidationAdapter`：

- `createLocalMicroflowValidationAdapter` 调用本地 `validateMicroflowSchema`。
- `createHttpMicroflowValidationAdapter` 调用 `POST /api/microflows/{id}/validate`。
- mock/local 默认使用 local validation；http 默认使用 HTTP validation，可通过 `validationMode: "local"` 在开发联调时强制本地校验。
