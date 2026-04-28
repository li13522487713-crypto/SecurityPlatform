# 第 43 轮 Resource / Schema 前后端联调

## 范围

本轮只覆盖微流 Resource / Schema 真实 API 链路：资源列表、创建、详情加载、Schema 加载、Schema 保存、重命名、收藏、复制、归档、恢复、删除与基础错误态。不覆盖 Publish / Version 深度逻辑、Metadata 深度联调、Validation 深度联调、References / Impact 深化、Runtime / TestRun / Trace。

依赖后端至少完成第 37 轮 Resource CRUD + Schema Save/Load，前端至少完成第 31-34 轮 HTTP Adapter、错误处理与 Contract Mock。

## 配置

app-web 微流入口默认使用：

```bash
VITE_MICROFLOW_ADAPTER_MODE=http
VITE_MICROFLOW_API_BASE_URL=/api
```

开发代理可把 `/api` 转发到 `http://localhost:5002`。也可以直接设置：

```bash
VITE_MICROFLOW_API_BASE_URL=http://localhost:5002
```

`http://localhost:5002/api` 也受支持，HTTP client 会避免拼成 `/api/api/microflows`。

## 联调步骤

1. 启动后端：`dotnet run --project src/backend/Atlas.AppHost`。
2. 启动前端：在 `src/frontend` 执行 `pnpm run dev:app-web`。
3. 进入工作区资源库的“微流” Tab，确认列表请求 `GET /api/microflows`。
4. 新建微流，确认 `POST /api/microflows` 创建资源与初始 SchemaSnapshot，并跳转编辑器。
5. 刷新编辑器，确认 `GET /api/microflows/{id}` 能恢复 Resource 与 AuthoringSchema。
6. 修改节点或连线后保存，确认 `PUT /api/microflows/{id}/schema` 返回新资源，刷新后修改仍存在。
7. 在资源库执行 rename / favorite / duplicate / archive / restore / delete，确认刷新后状态保持。
8. 执行 `pnpm run verify:microflow-resource-schema-integration` 做后端真实回归。

## 常见错误

- 404：资源已删除或当前工作区不可见，编辑器显示 Not Found，不白屏。
- 409：`MICROFLOW_VERSION_CONFLICT` 或 `MICROFLOW_ARCHIVED`；前端不得清空 dirty schema。
- schema invalid：后端拒绝缺少 AuthoringSchema 根字段或 FlowGram JSON。
- service unavailable / network：资源库显示服务错误，不回退 mock/local 数据。

## 验证入口

- 自动脚本：`src/frontend/scripts/verify-microflow-resource-schema-integration.mjs`。
- REST Client：`src/backend/Atlas.AppHost/Bosch.http/MicroflowBackend.http` 的 Microflow Resource / Schema 段落。
- 边界脚本：`pnpm run verify:microflow-no-production-mock`、`pnpm run verify:microflow-adapter-modes`。

## 不覆盖内容

本轮不验证真实 Runtime、TestRun、Trace，不做 Metadata selector 深度联调，不深化 Validation / ProblemPanel，不改 Publish / Version / References 核心逻辑。

下一轮建议进入 Metadata / Selector 联调，重点验证 EntitySelector、AttributeSelector、EnumerationSelector 与 MicroflowSelector 的真实后端 catalog。
