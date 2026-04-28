# Microflow Stage 01 — 资产盘点清单

> **轮次**：Stage 01（Inventory & Gap Analysis）
> **状态**：已完成（已由 Stage 02 修正）

---

## 1. 前端已实现能力

| 能力项 | 路径 / 组件 | 状态 |
|---|---|---|
| Microflow 资源适配器接口 | `microflow/adapter/microflow-resource-adapter.ts` | ✅ 已实现 |
| HTTP 资源适配器 | `microflow/adapter/http/http-resource-adapter.ts` | ✅ 已实现 |
| Mock 资源适配器 | `microflow/adapter/mock-microflow-resource-adapter.ts` | ✅ 已实现 |
| Local 资源适配器 | `microflow/adapter/local-microflow-resource-adapter.ts` | ✅ 已实现（见注 A） |
| Adapter Factory | `microflow/adapter/microflow-adapter-factory.ts` | ✅ 已实现 |
| 适配器工厂配置 | `microflow/config/microflow-adapter-config.ts` | ✅ 已实现 |
| Metadata 适配器 | `microflow/metadata/` | ✅ 已实现 |
| 微流验证适配器 | `microflow/adapter/microflow-validation-adapter.ts` | ✅ 已实现 |
| HTTP API Client | `microflow/adapter/http/microflow-api-client.ts` | ✅ 已实现 |
| API 合约（OpenAPI draft） | `microflow/contracts/api/` | ✅ 已定义 |
| Mock API / Contract Mock | `microflow/contracts/mock-api/` | ✅ 已实现 |
| 微流 Schema 定义 | `microflow/schema/` | ✅ 已实现 |
| 微流引用类型 | `microflow/references/` | ✅ 已实现 |
| 微流版本类型 | `microflow/versions/` | ✅ 已实现 |
| MendixStudioApp shell | `mendix-studio-core/src/index.tsx` | ✅ 已实现（sample shell） |
| Studio store | `mendix-studio-core/src/store.ts` | ✅ 已实现（Stage 02 已扩展） |
| App Explorer（静态 sample） | `components/app-explorer` | ⚠️ 静态展示，无真实 API |

---

## 2. 注记

### 注 A：Local Adapter 与持久化边界（Stage 02 修正）

`createLocalMicroflowApiClient` / Local Adapter **不属于**后端真实保存。

它可能使用 `localStorage`，也可能 fallback 到内存，但**不能**作为真实持久化的验收依据。
任何"保存"验收必须基于 HTTP 适配器 + 后端 `MicroflowResourceService` 的持久化路径。

### 注 B：微流引用保护（Stage 02 修正）

后端 `MicroflowResourceService.DeleteAsync` 已调用 `EnsureNoActiveTargetReferencesAsync`，
因此**后端具备被引用保护**。

前端缺口是：**App Explorer 删除入口尚未接入 references 预检查和后端错误友好提示**。
后端若返回 409 / 422 等引用冲突错误，前端当前会以通用错误 Toast 展示，尚无专项 UI 引导。

---

## 3. Stage 边界说明（Stage 02 修正）

| Stage | 范围 |
|---|---|
| Stage 01 | 盘点现有适配器、合约、schema 等基础能力；输出本文档与 gap-matrix |
| **Stage 02** | **仅做 asset foundation/context/model/adapter bundle**；不做 App Explorer 动态列表、不做 CRUD、不做真实保存 |
| Stage 03+ | App Explorer 真实 API 接入、CRUD 入口、真实保存画布、Call Microflow 目标选择 |
