---
name: Atlas 架构演进实施计划
overview: 基于仓库改造清单 v1 核验结果，制定分优先级实施计划，涵盖前端 P0 补缺、进程管理核心实现、AppHost HealthController、前端 Store/Service 拆分、systemd/Docker 部署模板等剩余任务。
todos:
  - id: t1-fe-guards
    content: "T-1: 前端 router guards 存根（platformGuard.ts + appRuntimeGuard.ts）"
    status: completed
  - id: t2-apphost-health
    content: "T-2: AppHost HealthController（/health/live, /health/ready, /health/info）"
    status: completed
  - id: t3-tenantappinstance-fields
    content: "T-3: 确认 TenantAppInstance 实体新字段（Port/Pid/RuntimeStatus/LastHeartbeat/LastExitCode 等）"
    status: completed
  - id: t4-processmanager
    content: "T-4: LocalChildProcessManager（System.Diagnostics.Process Start/Stop/Restart）"
    status: completed
  - id: t5-healthprobe
    content: "T-5: HttpAppHealthProbe（GET /health/ready 超时 3s）"
    status: completed
  - id: t6-supervisor
    content: "T-6: AppRuntimeSupervisorHostedService（5s 轮询/失败退避）"
    status: completed
  - id: t7-fe-stores
    content: "T-7: 前端 Store 分离（platformUser/platformPermission/appRuntimeContext）"
    status: completed
  - id: t8-fe-services
    content: "T-8: 前端 Service 分离（platform-api-core/platform-auth-client/app-auth-client）"
    status: completed
  - id: t9-apprelease-fields
    content: "T-9: AppRelease 实体新字段（ArtifactId/Checksum/InstallSpec/RollbackMetadata）"
    status: completed
  - id: t10-install-rollback-api
    content: "T-10: ReleaseCenterV2Controller install/rollback 端点 + .http 测试"
    status: completed
  - id: t11-systemd
    content: "T-11: systemd 模板（atlas-apphost@.service + atlas-platformhost.service）"
    status: completed
  - id: t12-dockerfile
    content: "T-12: AppHost Dockerfile 模板"
    status: completed
  - id: t13-windows-service
    content: "T-13: Windows Service install.ps1"
    status: completed
  - id: t14-yarp-proxy
    content: "T-14: PlatformHost YARP 动态路由（IDynamicProxyConfigProvider）"
    status: completed
isProject: false
---

# Atlas 架构演进蓝图 — 剩余任务实施计划

## 现状总结

| 批次 | 状态 | 说明 |
|---|---|---|
| PR-1：新宿主骨架 | ✅ 已完成 | 4 项目壳 + Program.cs + slnx |
| PR-2：DI 注册拆分 | ✅ 已完成 | Shared/Platform/Runtime 三段注册 |
| PR-3：PageRuntimeController 下沉 | ✅ 已完成 | AppHost 实装 + PlatformHost 兼容代理 |
| PR-4：前端多入口 | ⚠️ 部分完成 | 入口文件/路由文件存在，但物理未拆分 |
| PR-5：平台进程管理 | ⚠️ 部分完成 | 目录结构/注册存在，核心组件未实装 |
| PR-6：Build/Deploy/AppPackage | ⚠️ 部分完成 | 构建脚本存在，部署模板缺失 |

---

## Phase 0 — 止血收尾（必须先行）

### T-1：前端 router guards 存根

新建两个空存根文件，仅 export 函数，不修改 `router/index.ts`：

- [`src/frontend/Atlas.WebApp/src/router/guards/platformGuard.ts`](src/frontend/Atlas.WebApp/src/router/guards/platformGuard.ts)
  

```typescript
  export async function checkPlatformAuth(to: RouteLocationNormalized, from: RouteLocationNormalizedNormalized, next: NavigationGuardNext) {
    next(); // 空实现，后续 Phase 3 填入
  }
  

```

- [`src/frontend/Atlas.WebApp/src/router/guards/appRuntimeGuard.ts`](src/frontend/Atlas.WebApp/src/router/guards/appRuntimeGuard.ts)
  

```typescript
  export async function checkAppRuntimeAuth(to: RouteLocationNormalized, from: RouteLocationNormalizedNormalized, next: NavigationGuardNext) {
    next(); // 空实现，后续 Phase 3 填入
  }
  

```

验收：`npm run build` 无报错。

### T-2：AppHost HealthController

新建 [`src/backend/Atlas.AppHost/Controllers/HealthController.cs`](src/backend/Atlas.AppHost/Controllers/HealthController.cs)，三个端点：

- `GET /health/live` → 200（存活探测）
- `GET /health/ready` → 含 DB 连接检测（SqlSugar ping）
- `GET /health/info` → 返回 `AppHealthReport`（AppKey/TenantId/ReleaseVersion/Uptime/DbConnected/MigrationStatus）

验收：`dotnet build src/backend/Atlas.AppHost` 0 错误。

### T-3：确认 TenantAppInstance 实体新字段

检查 `TenantAppInstance` 实体类是否已包含清单要求字段：`Port`、`Pid`、`RuntimeStatus`、`LastHeartbeat`、`LastExitCode`、`InstallPath`、`IngressUrl`、`CurrentReleaseVersion`。

如缺失，在实体类补充，并在 [`scripts/verify-db-migration.ps1`](scripts/verify-db-migration.ps1) 中补充对应 ALTER TABLE 迁移 SQL。

验收：`dotnet build` 0 警告；SQLite 主库 `DESCRIBE TenantAppInstance` 含所有字段。

---

## Phase 1 — 核心进程管理实现

### T-4：LocalChildProcessManager

在 [`src/backend/Atlas.Infrastructure/Services/PlatformRuntime/`](src/backend/Atlas.Infrastructure/Services/PlatformRuntime/) 新建 `LocalChildProcessManager.cs`：

- `StartAsync(AppProcessSpec spec, CancellationToken ct)`：用 `System.Diagnostics.Process.Start()` 拉起 AppHost，记录 Pid
- `StopAsync(long appInstanceId, CancellationToken ct)`：SendQuit → WaitForExit(5s) → Kill
- `RestartAsync(long appInstanceId, CancellationToken ct)`：Stop + Start
- `GetStatusAsync(long appInstanceId, CancellationToken ct)`：返回 `AppProcessStatus`（含 Pid/Port/状态）
- 维护 `ConcurrentDictionary<long, Process>` 进程字典
- 进程退出时自动更新 TenantAppInstance.RuntimeStatus → Crashed/Stopped

验收：单元测试验证超时 Kill；`dotnet build` 0 警告。

### T-5：HttpAppHealthProbe

在 [`src/backend/Atlas.Infrastructure/Services/PlatformRuntime/`](src/backend/Atlas.Infrastructure/Services/PlatformRuntime/) 新建 `HttpAppHealthProbe.cs`：

- `ProbeAsync(string baseUrl, CancellationToken ct) → Task<AppHealthReport>`
- GET `{baseUrl}/health/ready`，超时 3s
- 反序列化响应为 `AppHealthReport`
- 异常（超时/非 200）返回 `AppHealthReport` 状态为 Unhealthy

验收：mock HttpClient 验证超时处理逻辑。

### T-6：AppRuntimeSupervisorHostedService

在 [`src/backend/Atlas.Infrastructure/Services/PlatformRuntime/`](src/backend/Atlas.Infrastructure/Services/PlatformRuntime/) 新建 `AppRuntimeSupervisorHostedService.cs`：

- 5s 轮询间隔（`Timer` / `PeriodicTimer`）
- 批量查询所有 TenantAppInstance（不在循环内单条 SELECT）
- 对比 desired state（RuntimeContext） vs actual state（`IAppHealthProbe.ProbeAsync`）
- 失败 3 次停止自动重启并写 `LastExitCode`
- 失败退避：`1×5s → 2×5s → 3×5s → 停止自动重启`，写审计日志

验收：`dotnet build` 0 警告；在 `PlatformServiceCollectionExtensions.cs` 注册为 `AddHostedService`。

---

## Phase 2 — 前端 Store/Service 拆分

### T-7：前端 Store 分离

新建以下 store 文件（`src/frontend/Atlas.WebApp/src/stores/`）：

- [`platformUser.ts`](src/frontend/Atlas.WebApp/src/stores/platformUser.ts) — 平台用户信息（从 `user.ts` 分叉）
- [`platformPermission.ts`](src/frontend/Atlas.WebApp/src/stores/platformPermission.ts) — 平台权限（从 `permission.ts` 分叉）
- [`appRuntimeContext.ts`](src/frontend/Atlas.WebApp/src/stores/appRuntimeContext.ts) — 应用运行上下文（appKey/instanceId/runtimeToken）

验收：`npm run build` 无 TypeScript 错误。

### T-8：前端 Service 分离

新建以下 service 文件（`src/frontend/Atlas.WebApp/src/services/`）：

- [`platform/platform-api-core.ts`](src/frontend/Atlas.WebApp/src/services/platform/platform-api-core.ts) — 平台 API 客户端，baseURL = `/api`
- [`auth/platform-auth-client.ts`](src/frontend/Atlas.WebApp/src/services/auth/platform-auth-client.ts) — 平台登录/Token 刷新
- [`auth/app-auth-client.ts`](src/frontend/Atlas.WebApp/src/services/auth/app-auth-client.ts) — 应用 Token 管理

验收：`npm run lint` 无新增 error。

---

## Phase 3 — AppPackage 与安装链路

### T-9：AppRelease 实体新字段

在 `AppRelease` 实体（`Atlas.Domain.*/Entities/AppRelease.cs`）补充：

- `ArtifactId`（string?）
- `Checksum`（string?）
- `InstallSpec`（string?）
- `RollbackMetadata`（string?）

补充 SQL migration 脚本（`sql/platform/`）ALTER TABLE ADD COLUMN。

验收：`dotnet build` 0 警告。

### T-10：ReleaseCenterV2Controller 安装/回滚端点

在 `TenantAppInstancesV2Controller.cs`（或 `ReleaseCenterV2Controller.cs`）已确认有 start/stop/restart 端点。

补充：

- `POST /{id}/install` — 调用 `IAppPackageInstaller.InstallAsync`
- `POST /{id}/rollback` — 切换 current 指针回上一版本
- 更新对应 `.http` 测试文件

验收：`dotnet build`；HTTP 端点可响应。

---

## Phase 4 — 部署模板

### T-11：systemd 模板

新建：

- [`deploy/app-host/systemd/atlas-apphost@.service`](deploy/app-host/systemd/atlas-apphost@.service)
  - `%i` 为 appKey，ExecStart 指向 AppHost 可执行文件
  - 注入 `ASPNETCORE_URLS=http://+:${PORT}` / `APP_INSTANCE_HOME=/opt/atlas/instances/{tenant}/{instance}` 环境变量
  - Restart=on-failure，RestartSec=5s

- [`deploy/platform-host/systemd/atlas-platformhost.service`](deploy/platform-host/systemd/atlas-platformhost.service)
  - 平台宿主 systemd 单元

### T-12：Dockerfile 模板

新建 [`deploy/app-host/Dockerfile`](deploy/app-host/Dockerfile)：

```dockerfile
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY app-package.zip /app/
RUN apt-get update && apt-get install -y unzip
RUN unzip -oq app-package.zip -d /app/instance
ENV ASPNETCORE_URLS=http://+:5100
EXPOSE 5100
ENTRYPOINT ["/app/instance/backend/Atlas.AppHost"]
```

### T-13：Windows Service 安装脚本

新建 [`deploy/app-host/windows-service/install.ps1`](deploy/app-host/windows-service/install.ps1)，参数化 `appKey` / `port` / `installPath`，使用 `New-Service`。

---

## Phase 5 — YARP 动态反代（收尾）

### T-14：PlatformHost YARP 动态路由

在 [`src/backend/Atlas.PlatformHost/Program.cs`](src/backend/Atlas.PlatformHost/Program.cs) 中已有 `builder.Services.AddReverseProxy()`，补充：

- 新建 [`src/backend/Atlas.PlatformHost/ReverseProxy/AppHostProxyConfig.cs`](src/backend/Atlas.PlatformHost/ReverseProxy/AppHostProxyConfig.cs)
- 从 `IAppInstanceRegistry` 读取各实例 `IngressUrl`
- 动态路由：`/app-host/{appKey}/*` → `http://127.0.0.1:{port}/*`
- 需实现 `IDynamicProxyConfigProvider` 或 `IProxyConfigProvider` 的热更新

验收：本地启动 PlatformHost + AppHost，访问 `/app-host/{appKey}/health/info` 能代理到 AppHost。

---

## 任务依赖关系图

```
T-1 (guards 存根)  ──────────────────────────────┐
T-2 (HealthController)  ──→ T-6 (Supervisor)  │
T-3 (实体字段确认)    ──→ T-4 (ProcessManager)  │
                                            ↓
T-4 (ProcessManager) ──→ T-5 (HealthProbe) ──→ T-6 (Supervisor) ──→ T-10 (install/rollback API) ──→ T-14 (YARP)
              │
              └─→ T-9 (AppRelease 字段)
                                             │
T-7 (Store 分离) ──→ T-8 (Service 分离) ────┘
T-11 (systemd) ──→ T-12 (Dockerfile) ──→ T-13 (Windows Service)
```

---

## 实施顺序建议

**第一批（本周，可闭环）**：T-1 → T-2 → T-3  
**第二批（下周，核心链路）**：T-4 → T-5 → T-6 → T-10  
**第三批（前端拆分）**：T-7 → T-8 → T-14  
**第四批（部署模板）**：T-11 → T-12 → T-13  
**收尾**：T-9（AppRelease 字段，确认已有则跳过）