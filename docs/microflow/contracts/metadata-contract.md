# MetadataCatalog 与 MetadataAdapter 契约

## 权威类型

`@atlas/microflow/metadata`（经 `@atlas/mendix-studio-core` 再导出）：`MicroflowMetadataCatalog` 及 `MetadataEntity`、`MetadataAttribute`、`MetadataAssociation`、`MetadataEnumeration*`、`MetadataMicroflowRef`、`MetadataPageRef`、`MetadataWorkflowRef`、`MetadataConnector` 等。

## 加载入口（唯一异步边界）

- **`MicroflowMetadataAdapter`**：元数据唯一加载入口（不依赖 React、不依赖 app-web）。方法见 `metadata-adapter.ts`：`getMetadataCatalog`、`refreshMetadataCatalog?`、`getEntity?`、`getEnumeration?`、各类 `get*Refs?`。
- **`createMockMicroflowMetadataAdapter`**：本地默认开发用；返回内置 sample catalog。
- **`createLocalMicroflowMetadataAdapter`**：第一版与 mock 等价；后续可接 localStorage 等。
- **`createHttpMicroflowMetadataAdapter`**（`@atlas/mendix-studio-core`）：`GET {apiBaseUrl}/api/microflow-metadata`，解包 `MicroflowApiResponse<MicroflowMetadataCatalog>`；**不作为默认**，需宿主显式注入。
- **`getDefaultMockMetadataCatalog()`**：与 mock adapter 默认 catalog **内容相同**的同步 getter，**仅**用于测试、契约脚本、以及暂时无法 `await` Adapter 的过渡桥接；**业务与编辑器应通过 `MicroflowMetadataProvider` / 注入的 Adapter 获取 catalog**，禁止在生产 UI 中依赖「偷偷 fallback 到 mock」。

## React 上下文

- **`MicroflowMetadataProvider`**：持有 `catalog | null`、`loading`、`error`、`version`、`reload` / `refresh`。
- **Hooks**：`useMicroflowMetadata`、`useMicroflowMetadataCatalog`、`useEntityCatalog`、`useAssociationCatalog`、`useEnumerationCatalog`、`useMicroflowRefCatalog`、`usePageCatalog`、`useWorkflowCatalog`、`useMetadataStatus`。
- 未传 `adapter` 时 Provider 使用 **mock adapter**（开发可用）；显式传入 `adapter` 时使用宿主数据。无 catalog 且非 loading 时，Selector / Case 等应展示「元数据未加载」或空态，**不得**在组件内再 import mock 列表。

## 查询 API（与 `metadata-query` 对齐）

实现适配器与 `metadata-query.ts` / `metadata-catalog.ts` 中下列函数语义一致即可，Selector、Validator、表达式推断、变量索引、Case 选项均应通过此类函数访问 catalog，避免重复手写查找：

- 实体 / 属性：`getEntityByQualifiedName`、`getEntityAttributes`、`getAttributeByQualifiedName`
- 关联：`getAssociationByQualifiedName`、`getAssociationsForEntity`、`getTargetEntityByAssociation`
- 枚举：`getEnumerationByQualifiedName`、`getEnumerationValues`
- 微流 / 页面 / 工作流：`getMicroflowById`、`getMicroflowByQualifiedName`、`getPageById`、`getWorkflowById`
- 继承：`getSpecializations`、`isEntitySpecializationOf`
- 搜索：`searchEntities`、`searchAttributes`、…（见 `metadata-query.ts`）

## 校验与元数据缺失

- **`validateMicroflowSchema`** 对象形式须包含 **`metadata`**（可为 `null`/`undefined` 以表示缺失）。缺失时发射 **`MF_METADATA_CATALOG_MISSING`**，不内部 fallback 到 mock。
- 元数据加载失败（Provider / Adapter）可映射为 **`MF_METADATA_LOAD_FAILED`**（与 UI 错误态一致）。
- 引用解析失败使用现有 `MF_METADATA_*_NOT_FOUND` 等码。

## Mock 数据合法位置

- `mock-metadata.ts`（须带边界注释）、`metadata-adapter.ts` 内默认 catalog。
- `*.spec.ts` / 样例 manifest / 内部 demo。
- **禁止**：`validators`、`expressions`、`variables`、`property-panel`、`flowgram` 生产源码直接出现 `mockMicroflowMetadataCatalog` / `mockEntities` 等（由 `scripts/verify-microflow-metadata-contract.mjs` 门禁）。

## 后端对齐

- `GET /api/microflow-metadata` 返回与 `MicroflowMetadataCatalog` **同构** JSON（及契约要求的 `updatedAt` 等），字段需与前端类型一致；分页或按模块拆分须可还原为单一 Catalog。
- 第 39 轮后端已实现 `GET /api/microflow-metadata`、`/entities/{qualifiedName}`、`/enumerations/{qualifiedName}`、`/microflows`、`/pages`、`/workflows`、`/health`。
- 后端不读取前端 `mock-metadata.ts`。无真实建模器或 cache 时使用后端 `seed-v1` catalog，并可在 Development 启动时写入 `MicroflowMetadataCache`。
- `MicroflowMetadataCache.CatalogJson` 保存完整 `MicroflowMetadataCatalog`；`catalog.microflows` 在返回前由 `MicroflowResource` 表生成并覆盖缓存中的 microflows。
- 当前限制：Page / Workflow 第一版可为空；完整 Domain Model 后端服务尚未接入；seed catalog 后续会被真实模型服务替代。
- 详见 [backend-api-contract.md](./backend-api-contract.md)、[request-response-examples.md](./request-response-examples.md)。

## 宿主（app-web）边界

- app-web **不**维护实体/枚举列表，**不**构造 `MicroflowMetadataCatalog`；仅通过 `@atlas/mendix-studio-core` 使用微流能力，并可选择传入 **`metadataAdapter`**（或预加载的 `metadataCatalog` 由 core 注入 Provider）。
