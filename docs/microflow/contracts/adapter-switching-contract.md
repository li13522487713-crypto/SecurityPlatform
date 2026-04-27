# Adapter 切换契约

第 31 轮开始，微流产品入口统一收敛到 `mendix-studio-core` 的 `createMicroflowAdapterBundle`。

## Mode

- `mock`：dev/test/sample only；Resource/Metadata 使用 mock，Runtime 使用本地 mock runner，Validation 使用本地校验。
- `local`：local development/offline only；Resource 使用本地存储，Metadata 使用本地目录，Runtime 使用本地 mock runner，Validation 使用本地校验。
- `http`：integration/production path；Resource/Metadata/Runtime/Validation 通过 HTTP API，不自动 fallback 到 mock。

生产模式默认 `http`，开发模式默认 `local`。`http` 必须配置 `apiBaseUrl`；服务不可用时 UI 显示“微流服务未连接”或具体 API 错误。

## 环境变量

- `VITE_MICROFLOW_ADAPTER_MODE` / `MICROFLOW_ADAPTER_MODE`：`mock`、`local`、`http`。
- `VITE_MICROFLOW_API_BASE_URL` / `MICROFLOW_API_BASE_URL`：HTTP adapter base url。
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

## 联调步骤

1. 后端启动并提供 `/api/microflows`、`/api/microflow-metadata`、validate、test-run、runs/trace 等契约接口。
2. app-web 设置 `VITE_MICROFLOW_ADAPTER_MODE=http` 与 `VITE_MICROFLOW_API_BASE_URL`。
3. 进入资源库微流 Tab，确认列表、详情、保存、发布、版本、引用、校验、测试运行均走 HTTP。
4. 后端 mock API server 可使用同一契约返回 `MicroflowApiResponse<T>`，前端无需改代码，只切换 base url。

## 边界验证

执行：

```bash
pnpm run verify:microflow-adapter-modes
```

该脚本检查三种 bundle 工厂、HTTP adapters、ValidationAdapter、app-web 边界和生产默认不回 mock。
