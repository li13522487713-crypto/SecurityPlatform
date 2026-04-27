# Adapter 切换契约

第 31 轮开始，微流产品入口统一收敛到 `mendix-studio-core` 的 `createMicroflowAdapterBundle`。

## Mode

- `mock`：dev/test/sample only；Resource/Metadata 使用 mock，Runtime 使用本地 mock runner，Validation 使用本地校验。
- `local`：local development/offline only；Resource 使用本地存储，Metadata 使用本地目录，Runtime 使用本地 mock runner，Validation 使用本地校验。
- `http`：integration/production path；Resource/Metadata/Runtime/Validation 通过 HTTP API，不自动 fallback 到 mock。

生产模式默认 `http`，开发模式默认 `local`。`http` 必须配置 `apiBaseUrl`；服务不可用时 UI 显示“微流服务未连接”或具体 API 错误。

## Runtime Policy

`MicroflowAdapterRuntimePolicy` 冻结生产边界：

- `development`：默认 `local`，允许 `mock/local/http`，允许显式开发 fallback。
- `test`：默认 `mock`，允许 `mock/local/http`。
- `storybook`：默认 `mock`，允许 `mock/local`。
- `production`：默认 `http`，禁止 `mock`、禁止 `local`、禁止 `enableMockFallback`、禁止 local fallback。
- `unknown`：默认 `local`；若存在 `apiBaseUrl` 可走 `http`。生产构建不得落入 unknown。

`validateMicroflowAdapterRuntimePolicy` 会在生产中拒绝 `mode=mock`、`mode=local`、`enableMockFallback=true` 与 `validationMode=local`。

## 环境变量

- `VITE_MICROFLOW_ADAPTER_MODE` / `MICROFLOW_ADAPTER_MODE`：`mock`、`local`、`http`。
- `VITE_MICROFLOW_API_BASE_URL` / `MICROFLOW_API_BASE_URL`：HTTP adapter base url。
- `VITE_MICROFLOW_API_MOCK` / `MICROFLOW_API_MOCK`：`msw` 时仅在 dev/test 启动 Contract Mock worker，adapter 仍强制为 `http`。
- `VITE_API_BASE`：未设置微流专用 base url 时的 app-web fallback。

## app-web 接入

`app-web` 只构造宿主上下文：

```tsx
<MendixMicroflowResourceTab adapterConfig={{ mode, apiBaseUrl, workspaceId, tenantId, currentUser }} />
<MendixMicroflowEditorPage adapterConfig={{ mode, apiBaseUrl, workspaceId, tenantId, currentUser }} />
```

`app-web` 不直接 fetch 微流 API，不直接读写微流 localStorage，不 import mock/local/http adapter 内部实现。

## HTTP 错误处理

`MicroflowApiClient` 负责：

- 拼接 `apiBaseUrl`。
- 附加 `X-Workspace-Id`、`X-Tenant-Id`、`X-User-Id` 与自定义 header。
- 解包 `MicroflowApiResponse<T>`。
- 将 HTTP error / API error 统一转换为 `MicroflowApiClientError`。
- 触发 `onUnauthorized`、`onForbidden`、`onApiError` 回调。

第 33 轮起，`MicroflowApiClientError` 兼容并继承 `MicroflowApiException`；`MicroflowApiError` 固定携带 `httpStatus`、`traceId`、`raw`。错误工具提供 `isUnauthorizedError`、`isForbiddenError`、`isNotFoundError`、`isVersionConflictError`、`isValidationFailedError`、`isPublishBlockedError`、`isNetworkError` 与统一用户提示。

入口 UI 行为：

- ResourceTab：列表失败显示“微流服务未连接”、`apiBaseUrl`、错误消息和重试按钮。
- EditorPage：按 404/403/409/服务异常显示明确错误，并提供返回资源库。
- Metadata selector：元数据加载失败时禁用并显示“元数据加载失败”。
- Validation / Publish / TestRun：HTTP validation 或 runtime API 失败时阻止发布/运行，不生成 mock trace。
- VersionsDrawer / ReferencesDrawer：加载失败显示错误态和重试按钮，回滚/复制/比较失败只提示并保留当前状态。

## 联调步骤

1. 后端启动并提供 `/api/microflows`、`/api/microflow-metadata`、validate、test-run、runs/trace 等契约接口。
2. app-web 设置 `VITE_MICROFLOW_ADAPTER_MODE=http` 与 `VITE_MICROFLOW_API_BASE_URL`。
3. 进入资源库微流 Tab，确认列表、详情、保存、发布、版本、引用、校验、测试运行均走 HTTP。
4. 后端 mock API server 可使用同一契约返回 `MicroflowApiResponse<T>`，前端无需改代码，只切换 base url。

## Contract Mock

第 34 轮起，`@atlas/mendix-studio-core` 提供 `startMicroflowContractMockWorker()`。app-web 仅在 `MICROFLOW_API_MOCK=msw` 且非生产时调用该公开 helper；Resource/Metadata/Runtime/Validation 仍由 HTTP Adapter 发请求，MSW 返回标准 `MicroflowApiResponse<T>`。详见 `contract-mock-readme.md`。

## 边界验证

执行：

```bash
pnpm run verify:microflow-adapter-modes
pnpm run verify:microflow-no-production-mock
pnpm run verify:microflow-contract-mock
```

这些脚本检查三种 bundle 工厂、runtime policy、HTTP adapters、ValidationAdapter、app-web 边界、生产默认不回 mock，以及生产路径没有微流 mock/local import。
