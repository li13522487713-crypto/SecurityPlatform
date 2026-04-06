# Setup E2E（真实浏览器）

本测试使用 Playwright 启动真实 Chromium，覆盖以下链路：

- PlatformWeb 平台级 setup：`/setup`
- AppWeb 应用级 setup：`/app-setup`

为避免污染正在使用中的默认开发主库，平台级 setup 在测试中会显式写入 `Data Source=atlas.e2e.db`，并复用当前开发端口完成整条初始化链路。

## 运行方式

在 `src/frontend` 目录执行：

```powershell
pnpm install
pnpm run test:e2e:setup
```

如需观察真实浏览器过程：

```powershell
pnpm run test:e2e:setup:headed
```

## 执行前会清理的本地状态

测试开始前会删除或清空以下内容：

- 当前工作区相关的 `PlatformHost`、`AppHost`、`Vite` 开发进程会被自动停止
- `src/backend/Atlas.PlatformHost/setup-state.json`
- `src/backend/Atlas.PlatformHost/appsettings.runtime.json`
- `src/backend/Atlas.PlatformHost/atlas.e2e.db` 及其 `-wal/-shm`
- `src/backend/Atlas.PlatformHost/hangfire-platformhost.db` 及其 `-wal/-shm`
- `src/backend/Atlas.AppHost/app-setup-state.json`
- `src/backend/Atlas.AppHost/hangfire-apphost.db` 及其 `-wal/-shm`
- `runtime/instances`
- `runtime/artifacts`
- 浏览器 `localStorage/sessionStorage` 中除 `atlas_locale` 外的残留状态

测试结束后不会自动恢复到执行前状态。当前开发环境会保留测试跑完后的 setup 结果。

## 失败排查

优先查看：

- Playwright HTML 报告：`src/frontend/playwright-report/setup/index.html`
- Playwright 测试产物：`src/frontend/test-results`

服务侧日志来源：

- `dotnet run --project src/backend/Atlas.PlatformHost`
- `dotnet run --project src/backend/Atlas.AppHost`
- `pnpm run dev:platform-web`
- `pnpm run dev:app-web`

如果环境缺少浏览器或 Playwright 依赖，可先执行：

```powershell
pnpm exec playwright install chromium
```
