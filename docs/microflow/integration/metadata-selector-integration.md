# Metadata / Selector 联调说明

第 44 轮范围只覆盖 Microflow Metadata API 到前端 MetadataProvider、Selector、ExpressionEditor、VariableIndex 与 CaseEditor 的真实 HTTP 联调，不实现完整 Domain Model 管理器、Page/Workflow 后端资源服务或 Runtime。

## 前置条件

- 第 39 轮后端 `GET /api/microflow-metadata`、`/entities/{qualifiedName}`、`/enumerations/{qualifiedName}`、`/microflows`、`/pages`、`/workflows`、`/health` 已可用。
- 前端 `adapterMode=http`，`apiBaseUrl` 指向 AppHost，例如 `http://localhost:5002`。
- app-web 只注入 adapterConfig / adapter bundle，不直接 fetch metadata API，不构造 `MetadataCatalog`。

## 联调路径

- `createHttpMicroflowMetadataAdapter` 通过 `MicroflowApiClient` 解包 `MicroflowApiResponse<T>`。
- `MicroflowMetadataProvider` 接收宿主注入的 HTTP adapter，加载失败时暴露 `provider.error`，不 fallback mock。
- Entity / Attribute / Association / Enumeration / EnumerationValue / Microflow / Page / Workflow / DataType selector 只通过 metadata hooks 消费 provider catalog。
- Page/Workflow 后端返回空数组时 selector 保持空态，不阻断其它属性表单。

## 后端 seed 边界

当前 `MicroflowSeedMetadataCatalog` 是后端临时元数据源，`MetadataCache.source` / health `source` 用于标识 `seed/generated/imported`。如果前端看到 `Sales.Order`，它来自后端 seed catalog，不是前端 mock。后续真实 Domain Model 服务替代 seed catalog 时保持同一 DTO 契约。

## 回归清单

- 运行 `scripts/verify-microflow-metadata-integration.ts` 检查 health、catalog、实体、枚举、microflow refs、includeSystem 与 404 envelope。
- 使用 `src/backend/Atlas.AppHost/Bosch.http/MicroflowBackend.http` 的 metadata 段手工验证 includeSystem/includeArchived/moduleId、unknown entity/enum、pages/workflows 空数组。
- 断开后端时，Provider 与 Selector 应显示错误态或禁用态，不白屏，不回落前端 mock。
