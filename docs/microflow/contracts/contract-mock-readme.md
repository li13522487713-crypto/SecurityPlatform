# Contract Mock API

第 34 轮引入契约级 API Mock：前端仍使用 `mode=http` 和 `MicroflowApiClient` 真实发起 HTTP 请求，由 MSW 在浏览器/测试进程内按第 21 轮 REST 契约返回 `MicroflowApiResponse<T>`。

## 与旧 mock/local adapter 的区别

- 旧 `mock/local` adapter 直接返回业务 DTO，用于离线开发。
- Contract Mock 拦截 HTTP 请求，返回标准 envelope，覆盖错误码、traceId、timestamp 与非 2xx 状态。
- app-web 不读取 mock store、不 import handler、不绕过 HTTP Adapter。
- 生产环境不会启动 MSW，也不会在后端不可用时 fallback 到 mock/local adapter。

## 启动方式

在 `src/frontend` 启动：

```bash
VITE_MICROFLOW_API_MOCK=msw VITE_MICROFLOW_ADAPTER_MODE=http VITE_MICROFLOW_API_BASE_URL=/api pnpm run dev:app-web
```

`app-web` 会调用 `startMicroflowContractMockWorker()`，并把微流 adapter 配置为 `http`。当前 HTTP Adapter 的路径已包含 `/api/*`，mock handler 同时支持 `/api/*` 与开发配置下可能出现的 `/api/api/*`。

## 覆盖 API

- Resource / Schema：`GET|POST /api/microflows`、`GET|PATCH|DELETE /api/microflows/{id}`、schema get/save/migrate、duplicate、rename、favorite、archive、restore。
- Metadata：catalog、entities、enumerations、microflows、pages、workflows。
- Validation：`POST /api/microflows/{id}/validate`。
- Publish：`POST /api/microflows/{id}/publish`，含 validation blocked、high impact confirm、version conflict。
- Version：versions list/detail/rollback/duplicate/compare-current。
- Reference / Impact：references 过滤与 impact analysis。
- Runtime / TestRun：test-run、runs get/cancel/trace。

## 错误场景

任意 mock API 可通过 header 或 query 模拟错误：

- `x-microflow-mock-error: unauthorized` → 401。
- `x-microflow-mock-error: forbidden` → 403。
- `x-microflow-mock-error: not-found` → 404。
- `x-microflow-mock-error: version-conflict` → 409。
- `x-microflow-mock-error: validation-failed` → 422。
- `x-microflow-mock-error: publish-blocked` → 422。
- `x-microflow-mock-error: service-unavailable` → 500。
- `x-microflow-mock-error: network` → network-like error。

也可使用 `?mockError=...`。

## 验证

```bash
pnpm run verify:microflow-contract-mock
pnpm run verify:microflow-adapter-modes
pnpm run verify:microflow-no-production-mock
pnpm run verify:microflow-http-error-handling
```

Contract Mock 只用于 development/test/contract 阶段，用来在真实后端完成前验证 HTTP Adapter、错误处理、资源库、编辑器、Metadata、Validation、Publish、Versions、References 与 DebugPanel 的联调链路。
