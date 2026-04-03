---
name: Atlas 架构演进蓝图
overview: 将当前 Atlas Security Platform 从"平台+应用+运行时混宿主单体"演进为"Atlas.PlatformHost 平台主进程 + Atlas.AppHost 每应用独立进程 + 多入口前端"架构，满足应用进程级隔离、独立入口、独立登录页、独立运行包的硬目标。
todos:
  - id: p0-contracts-csproj
    content: P0-1 后端：新建 src/backend/Atlas.Shared.Contracts/Atlas.Shared.Contracts.csproj（net10.0 空类库，无业务依赖），加入 slnx
    status: 未实施
  - id: p0-apphost-csproj
    content: P0-2 后端：新建 src/backend/Atlas.AppHost/Atlas.AppHost.csproj（net10.0 ASP.NET Core WebApp 壳，引用 Atlas.Shared.Contracts），加入 slnx
    status: 未实施
  - id: p0-platformhost-csproj
    content: P0-3 后端：新建 src/backend/Atlas.PlatformHost/Atlas.PlatformHost.csproj（net10.0 ASP.NET Core WebApp 壳，引用与 Atlas.WebApi 相同），加入 slnx
    status: 未实施
  - id: p0-build-verify
    content: P0-4 后端：执行 dotnet build，确认三个新项目 0 错误 0 警告，Atlas.WebApi 仍可正常编译
    status: 未实施
  - id: p0-pageruntime-adr
    content: P0-5 后端：在 PageRuntimeController.cs 文件顶部添加 ADR 注释，标记为「首批下沉到 Atlas.AppHost」目标
    status: 未实施
  - id: p0-contracts-doc
    content: P0-6 文档：在 docs/contracts.md 新增「控制面/数据面边界」章节，定义平台 API 前缀规范（/api/v1/）与 AppHost API 前缀规范（/app-api/v1/）
    status: 未实施
  - id: p0-fe-platform-guard
    content: P0-7 前端：新建 src/frontend/Atlas.WebApp/src/router/guards/platformGuard.ts（空函数存根，仅 export，不修改 index.ts）
    status: 未实施
  - id: p0-fe-appruntime-guard
    content: P0-8 前端：新建 src/frontend/Atlas.WebApp/src/router/guards/appRuntimeGuard.ts（空函数存根，仅 export，不修改 index.ts）
    status: 未实施
  - id: p1-platformhost-program
    content: P1-1 后端：Atlas.PlatformHost/Program.cs 复制 Atlas.WebApi/Program.cs，仅保留平台控制面服务（Auth/Tenant/RBAC/Catalog/Release/RuntimeRegistry/Hangfire/监控/审计），移除应用运行时服务注册
    status: 未实施
  - id: p1-platformhost-csproj-refs
    content: P1-2 后端：Atlas.PlatformHost.csproj 保留平台级 ProjectReference（Atlas.Application、Atlas.Infrastructure、Atlas.Core），移除纯应用层引用（Atlas.Application.Workflow、LogicFlow、BatchProcess、AgentTeam、Alert、Approval、Assets）
    status: 未实施
  - id: p1-apphost-program
    content: P1-3 后端：Atlas.AppHost/Program.cs 新建最小启动（Kestrel :5100、JWT 验证、健康端点、NLog），不含 Hangfire Server 和 WorkflowHostedService
    status: 未实施
  - id: p1-apphost-pageruntime
    content: P1-4 后端：将 PageRuntimeController.cs 复制到 Atlas.AppHost/Controllers/，Atlas.WebApi 原件保留（兼容窗口），确认 AppHost 可编译
    status: 未实施
  - id: p1-apphost-lowcode-rt
    content: P1-5 后端：Atlas.AppHost 注册 LowCode runtime 服务（ILowCodeAppQueryService / ILowCodePageQueryService 等只读运行态服务），引用 Atlas.Application + Atlas.Infrastructure
    status: 未实施
  - id: p1-apphost-dynamictables-rt
    content: P1-6 后端：Atlas.AppHost 注册 DynamicTables runtime 服务（IDynamicTableRecordQueryService / IDynamicTableRecordCommandService），确认编译 0 警告
    status: 未实施
  - id: p1-triple-build-verify
    content: P1-7 后端：执行 dotnet build，三个宿主（WebApi/PlatformHost/AppHost）同时 0 错误 0 警告
    status: 未实施
  - id: p1-fe-entries-dir
    content: P1-8 前端：新建 src/frontend/Atlas.WebApp/src/entries/ 目录，创建 platform-console.html、app-studio.html、app-runtime.html、app-login.html 四个 HTML 入口文件
    status: 未实施
  - id: p1-fe-platform-console-entry
    content: P1-9 前端：新建 src/entries/platform-console.ts 和 PlatformConsoleApp.vue（挂载 ConsoleLayout 路由子集，router-view 仅含 /console/* 路由）
    status: 未实施
  - id: p1-fe-app-studio-entry
    content: P1-10 前端：新建 src/entries/app-studio.ts 和 AppStudioApp.vue（挂载 AppWorkspaceLayout 路由子集，/apps/* 路由）
    status: 未实施
  - id: p1-fe-app-runtime-entry
    content: P1-11 前端：新建 src/entries/app-runtime.ts 和 AppRuntimeApp.vue（挂载 RuntimeLayout 路由子集，/r/* 路由）
    status: 未实施
  - id: p1-fe-app-login-entry
    content: P1-12 前端：新建 src/entries/app-login.ts 和 AppLoginApp.vue（挂载 LoginPage，路由仅含 /app-login）
    status: 未实施
  - id: p1-fe-vite-multi-input
    content: P1-13 前端：vite.config.ts rollupOptions.input 新增四个入口（platform-console/app-studio/app-runtime/app-login），保留原 app 入口兼容；执行 npm run build 验证四套产物均可输出
    status: 未实施
  - id: p2-contracts-appprocessspec
    content: P2-1 后端：Atlas.Shared.Contracts 定义 AppProcessSpec 记录型（AppKey, AppInstanceId, Port, InstallPath, ReleaseVersion, EnvVars）
    status: 未实施
  - id: p2-contracts-appprocessstatus
    content: P2-2 后端：Atlas.Shared.Contracts 定义 AppRuntimeStatus 枚举（Unknown/Starting/Running/Stopping/Stopped/Crashed）和 AppProcessInfo 记录型（含 Pid/Port/Status/LastHeartbeat/LastExitCode）
    status: 未实施
  - id: p2-contracts-iappprocessmanager
    content: P2-3 后端：Atlas.Shared.Contracts 定义 IAppProcessManager 接口（StartAsync/StopAsync/RestartAsync/GetStatusAsync）
    status: 未实施
  - id: p2-contracts-apphealthreport
    content: P2-4 后端：Atlas.Shared.Contracts 定义 AppHealthReport 记录型（AppKey/AppInstanceId/ReleaseVersion/Uptime/DbConnected/MigrationStatus）
    status: 未实施
  - id: p2-contracts-iapphealthprobe
    content: P2-5 后端：Atlas.Shared.Contracts 定义 IAppHealthProbe 接口（ProbeAsync(baseUrl, ct) -> AppHealthReport）
    status: 未实施
  - id: p2-contracts-iappruntimesupervisor
    content: P2-6 后端：Atlas.Shared.Contracts 定义 IAppRuntimeSupervisor 接口（EnsureRunningAsync/EvictAsync/GetDesiredStateAsync）
    status: 未实施
  - id: p2-entity-runtime-fields
    content: P2-7 后端：TenantAppInstance 实体新增 Port（int?）、Pid（int?）、RuntimeStatus（string）三个字段，主库 SQLite 自动补列
    status: 未实施
  - id: p2-entity-deploy-fields
    content: P2-8 后端：TenantAppInstance 实体新增 InstallPath（string?）、IngressUrl（string?）、CurrentReleaseVersion（string?）三个字段
    status: 未实施
  - id: p2-entity-health-fields
    content: P2-9 后端：TenantAppInstance 实体新增 LastHeartbeat（DateTimeOffset?）、LastExitCode（int?）两个字段；更新 TenantAppInstancesV2Controller 响应 DTO 包含新字段
    status: 未实施
  - id: p2-localprocessmanager
    content: P2-10 后端：Atlas.PlatformHost 实现 LocalChildProcessManager（System.Diagnostics.Process，Start/Stop/Restart，维护 pid/port 字典，进程退出时更新 TenantAppInstance.RuntimeStatus）
    status: 未实施
  - id: p2-httphealthprobe
    content: P2-11 后端：Atlas.PlatformHost 实现 HttpAppHealthProbe（HttpClient GET /health/ready，超时 3s，反序列化 AppHealthReport）
    status: 未实施
  - id: p2-supervisor-hostedservice
    content: P2-12 后端：Atlas.PlatformHost 实现 AppRuntimeSupervisorHostedService（5s 轮询 desired/actual state，失败退避重启，超 3 次停止自动重启并写 LastExitCode）
    status: 未实施
  - id: p2-apphost-healthcontroller
    content: P2-13 后端：Atlas.AppHost 新增 HealthController（GET /health/live 200、GET /health/ready 含 DB 检测、GET /health/info 返回 AppHealthReport）
    status: 未实施
  - id: p2-appinstance-start-stop-api
    content: P2-14 后端：TenantAppInstancesV2Controller 新增 POST /{id}/start、POST /{id}/stop、POST /{id}/restart 三个端点，调用 IAppProcessManager；更新 AppInstances.http 测试文件
    status: 未实施
  - id: p2-fe-instance-status-panel
    content: P2-15 前端：平台 Console TenantApplicationsPage 新增「运行状态」列（PID/端口/RuntimeStatus/health/版本/失败原因），每 15s 轮询刷新
    status: 未实施
  - id: p3-platform-app-entry-api
    content: P3-1 后端：平台 AuthController 新增 GET /api/v1/auth/app-entry?appKey={key}（返回 AppLoginEntry：appName/logo/slogan/authMode/callbackUrl），更新 Auth.http
    status: 未实施
  - id: p3-apphost-auth-callback
    content: P3-2 后端：Atlas.AppHost 新增 AppAuthController（GET /auth/app/callback 处理 OIDC 回调、POST /auth/app/logout，颁发 audience=atlas-app:{appKey} 的 JWT）
    status: 未实施
  - id: p3-apphost-jwt-audience
    content: P3-3 后端：Atlas.AppHost JWT 验证配置 audience = atlas-app:{appKey}（从 appsettings.AppHost.json 读取 AppKey），与 PlatformHost audience 分离
    status: 未实施
  - id: p3-fe-app-login-page
    content: P3-4 前端：新建 src/pages/app-runtime/AppLoginPage.vue（按 appKey 调 /api/v1/auth/app-entry 拉取 branding，展示品牌化登录表单）
    status: 未实施
  - id: p3-fe-app-entry-gateway
    content: P3-5 前端：新建 src/pages/app-runtime/AppEntryGatewayPage.vue（未登录时拦截 /r/* 路由，跳转 AppLoginPage）
    status: 未实施
  - id: p3-fe-appruntime-guard-active
    content: P3-6 前端：appRuntimeGuard.ts 正式实现：读取 app session token，无 token 则跳 AppEntryGatewayPage；router/index.ts 对 /r/* 路由启用此 guard
    status: 未实施
  - id: p3-fe-auth-client-split
    content: P3-7 前端：拆分 api-auth.ts 为 platform-auth.ts（平台 token/refresh）和 app-auth.ts（应用 token/logout），api-core.ts 按路由前缀选择 auth client
    status: 未实施
  - id: p3-yarp-apphost-proxy
    content: P3-8 后端：Atlas.PlatformHost 引入 YARP，配置 /app-host/{appKey}/* -> http://localhost:{port}/* 动态路由（从 IAppInstanceRegistry 读取 port），更新 AppHostProxy.http
    status: 未实施
  - id: p4-contracts-packagemanifest
    content: P4-1 后端：Atlas.Shared.Contracts 定义 AppPackageManifest 记录型（AppKey/Version/BuildHash/FrontendBundle/BackendExecutable/MigrationManifest/HealthEndpoints/ConfigTemplate）
    status: 未实施
  - id: p4-contracts-installer-interface
    content: P4-2 后端：Atlas.Shared.Contracts 定义 IAppPackageInstaller 接口（InstallAsync/UninstallAsync/ValidateAsync）
    status: 未实施
  - id: p4-apprelease-artifact-fields
    content: P4-3 后端：AppRelease 实体新增 ArtifactId（string?）、Checksum（string?）、InstallSpec（string?）、RollbackMetadata（string?）四个字段
    status: 未实施
  - id: p4-packager-script
    content: P4-4 构建：新建 build/app-packager/pack.ps1（接收 appKey/version 参数，构建 frontend/runtime + frontend/login bundle，发布 Atlas.AppHost，打包为 app-package.zip，写入 manifest.json + checksums.sha256）
    status: 未实施
  - id: p4-package-installer-impl
    content: P4-5 后端：Atlas.PlatformHost 实现 FileSystemAppPackageInstaller（解压 zip 到 /opt/atlas/apps/{instanceId}/{version}，注入 env，调用 AppMigrationService，注册 AppRelease）
    status: 未实施
  - id: p4-release-install-api
    content: P4-6 后端：ReleaseCenterV2Controller 新增 POST /{releaseId}/install 端点（触发 IAppPackageInstaller.InstallAsync），更新 ReleaseCenter.http
    status: 未实施
  - id: p4-release-rollback-api
    content: P4-7 后端：ReleaseCenterV2Controller 新增 POST /{releaseId}/rollback 端点（切回上一个成功安装的版本，更新 RuntimeContext），更新 ReleaseCenter.http
    status: 未实施
  - id: p4-governance-artifact-builder
    content: P4-8 后端：GovernanceServices.PackageService 升级：生成时写入 manifest.json 和 checksums.sha256，PackageArtifact 绑定 AppRelease.ArtifactId
    status: 未实施
  - id: p5-otel-resource-tags
    content: P5-1 后端：Atlas.AppHost Program.cs OTEL 配置新增 atlas.scope=app / atlas.appKey / atlas.tenantId / atlas.releaseVersion 资源标签
    status: 未实施
  - id: p5-nlog-structured-fields
    content: P5-2 后端：Atlas.AppHost nlog.config 新增 AppKey/TenantId/ReleaseVersion 结构化字段（从 appsettings 读取注入）
    status: 未实施
  - id: p5-systemd-template
    content: P5-3 部署：新建 deploy/app-host/systemd/atlas-apphost@.service（%i 为 appKey，ExecStart 指向 AppHost 可执行文件，注入 ASPNETCORE_URLS/APP_KEY 环境变量）
    status: 未实施
  - id: p5-windows-service
    content: P5-4 部署：新建 deploy/app-host/windows-service/install.ps1（New-Service，参数化 appKey/port/installPath）
    status: 未实施
  - id: p5-fe-log-stream-panel
    content: P5-5 前端：平台 Console TenantApplicationsPage 新增「实时日志」抽屉（SSE 连接 PlatformHost /api/v1/app-instances/{id}/logs/stream，转发 AppHost 日志流）
    status: 未实施
  - id: p5-fe-resource-usage-card
    content: P5-6 前端：平台 Console 新增「资源占用」卡片（CPU%/内存 MB，来自 AppHost /health/info，15s 刷新）
    status: 未实施
  - id: p5-dockerfile
    content: P5-7 部署：新建 deploy/app-host/Dockerfile（FROM mcr.microsoft.com/dotnet/aspnet:10.0，COPY app-package.zip，ENTRYPOINT Atlas.AppHost）
    status: 未实施
isProject: false
---

# Atlas 架构演进蓝图

## 最小实施 Case 总览

每个 case 独立可编译/可验证，前一 case 完成后方可开始下一 case。**括号内为依赖前置 case 编号。**

### Phase 0 — 止血与边界固化（8 cases）


| #    | Case                                    | 目标文件 / 操作                                                          | 验收标准                                                      |
| ---- | --------------------------------------- | ------------------------------------------------------------------ | --------------------------------------------------------- |
| P0-1 | 新建 `Atlas.Shared.Contracts` 项目壳         | `src/backend/Atlas.Shared.Contracts/Atlas.Shared.Contracts.csproj` | `dotnet build` 0 错误 0 警告                                  |
| P0-2 | 新建 `Atlas.AppHost` 项目壳 *(依赖 P0-1)*      | `src/backend/Atlas.AppHost/Atlas.AppHost.csproj`                   | `dotnet build` 0 错误 0 警告                                  |
| P0-3 | 新建 `Atlas.PlatformHost` 项目壳 *(依赖 P0-1)* | `src/backend/Atlas.PlatformHost/Atlas.PlatformHost.csproj`         | `dotnet build` 0 错误 0 警告                                  |
| P0-4 | 三个新项目加入 slnx，验证整体构建 *(依赖 P0-1/2/3)*     | `Atlas.SecurityPlatform.slnx`                                      | `dotnet build` 全项目 0 错误 0 警告                              |
| P0-5 | 标记 `PageRuntimeController` 为下沉目标        | `Atlas.WebApi/Controllers/PageRuntimeController.cs` 文件顶部 ADR 注释    | 注释清晰标明归属 AppHost，构建无影响                                    |
| P0-6 | `docs/contracts.md` 新增边界章节              | `docs/contracts.md`                                                | 含平台 API 前缀 `/api/v1/`、AppHost API 前缀 `/app-api/v1/` 的规范文字 |
| P0-7 | 前端新建 `platformGuard.ts` 存根              | `src/router/guards/platformGuard.ts`                               | 仅 export 空函数，`npm run build` 无报错                          |
| P0-8 | 前端新建 `appRuntimeGuard.ts` 存根            | `src/router/guards/appRuntimeGuard.ts`                             | 仅 export 空函数，`npm run build` 无报错                          |


---

### Phase 1A — 后端控制面/数据面分层（7 cases）


| #    | Case                                                        | 目标文件 / 操作                                            | 验收标准                                                                                                                      |
| ---- | ----------------------------------------------------------- | ---------------------------------------------------- | ------------------------------------------------------------------------------------------------------------------------- |
| P1-1 | `Atlas.PlatformHost` 复制 Program.cs 并精简为平台控制面启动 *(依赖 P0-3)*  | `Atlas.PlatformHost/Program.cs`                      | 仅注册 Auth/Tenant/RBAC/Catalog/Release/Hangfire/监控/审计，移除 Workflow/LogicFlow/Agent/Batch 运行时注册                               |
| P1-2 | `Atlas.PlatformHost.csproj` 调整 ProjectReference *(依赖 P1-1)* | `Atlas.PlatformHost/Atlas.PlatformHost.csproj`       | 移除 `Atlas.Application.Workflow`、`LogicFlow`、`BatchProcess`、`AgentTeam`、`Alert`、`Approval`、`Assets` 引用，`dotnet build` 0 警告 |
| P1-3 | `Atlas.AppHost` 最小 Program.cs *(依赖 P0-2)*                   | `Atlas.AppHost/Program.cs`                           | Kestrel :5100、JWT 验证、NLog，无 Hangfire Server，`dotnet run` 启动不报错                                                            |
| P1-4 | `PageRuntimeController` 复制到 AppHost *(依赖 P1-3)*             | `Atlas.AppHost/Controllers/PageRuntimeController.cs` | AppHost 编译通过；WebApi 原件保留（兼容窗口）                                                                                            |
| P1-5 | AppHost 注册 LowCode runtime 服务 *(依赖 P1-4)*                   | `Atlas.AppHost/Program.cs` 服务注册段                     | 引用 Atlas.Application + Atlas.Infrastructure，注册 ILowCodeAppQueryService 等只读服务，编译 0 警告                                      |
| P1-6 | AppHost 注册 DynamicTables runtime 服务 *(依赖 P1-5)*             | `Atlas.AppHost/Program.cs` 服务注册段                     | 注册 IDynamicTableRecordQueryService / CommandService，编译 0 警告                                                               |
| P1-7 | 三宿主同时编译验证 *(依赖 P1-1/3/6)*                                   | `dotnet build` 全项目                                   | WebApi + PlatformHost + AppHost 全部 0 错误 0 警告                                                                              |


---

### Phase 1B — 前端多入口构建（6 cases）


| #     | Case                                        | 目标文件 / 操作                                                              | 验收标准                                                              |
| ----- | ------------------------------------------- | ---------------------------------------------------------------------- | ----------------------------------------------------------------- |
| P1-8  | 新建 entries 目录和四个 HTML 入口文件 *(依赖 P0-7/8)*    | `src/entries/{platform-console,app-studio,app-runtime,app-login}.html` | 文件存在，HTML 合法                                                      |
| P1-9  | `platform-console` entry *(依赖 P1-8)*        | `src/entries/platform-console.ts` + `PlatformConsoleApp.vue`           | 挂载 ConsoleLayout 路由子集，`npm run build` 产出 `dist/platform-console/` |
| P1-10 | `app-studio` entry *(依赖 P1-8)*              | `src/entries/app-studio.ts` + `AppStudioApp.vue`                       | 挂载 AppWorkspaceLayout 路由子集，`dist/app-studio/`                     |
| P1-11 | `app-runtime` entry *(依赖 P1-8)*             | `src/entries/app-runtime.ts` + `AppRuntimeApp.vue`                     | 挂载 RuntimeLayout 路由子集，`dist/app-runtime/`                         |
| P1-12 | `app-login` entry *(依赖 P1-8)*               | `src/entries/app-login.ts` + `AppLoginApp.vue`                         | 挂载 LoginPage，`dist/app-login/`                                    |
| P1-13 | `vite.config.ts` 接入四入口 *(依赖 P1-9/10/11/12)* | `vite.config.ts` rollupOptions.input                                   | 保留原 `app` 入口，新增四入口，`npm run build` 输出五套产物，0 TypeScript 错误         |


---

### Phase 2A — 共享契约层定义（6 cases）


| #    | Case                                                       | 目标文件                                                      | 验收标准                                                                     |
| ---- | ---------------------------------------------------------- | --------------------------------------------------------- | ------------------------------------------------------------------------ |
| P2-1 | 定义 `AppProcessSpec` 记录型 *(依赖 P0-1)*                        | `Atlas.Shared.Contracts/Process/AppProcessSpec.cs`        | 含 AppKey/AppInstanceId/Port/InstallPath/ReleaseVersion/EnvVars，编译通过      |
| P2-2 | 定义 `AppRuntimeStatus` 枚举和 `AppProcessInfo` 记录型 *(依赖 P0-1)* | `Atlas.Shared.Contracts/Process/AppProcessInfo.cs`        | 状态枚举：Unknown/Starting/Running/Stopping/Stopped/Crashed，编译通过              |
| P2-3 | 定义 `IAppProcessManager` 接口 *(依赖 P2-1/2)*                   | `Atlas.Shared.Contracts/Process/IAppProcessManager.cs`    | StartAsync/StopAsync/RestartAsync/GetStatusAsync 签名完整                    |
| P2-4 | 定义 `AppHealthReport` 记录型 *(依赖 P0-1)*                       | `Atlas.Shared.Contracts/Health/AppHealthReport.cs`        | 含 AppKey/AppInstanceId/ReleaseVersion/Uptime/DbConnected/MigrationStatus |
| P2-5 | 定义 `IAppHealthProbe` 接口 *(依赖 P2-4)*                        | `Atlas.Shared.Contracts/Health/IAppHealthProbe.cs`        | ProbeAsync(baseUrl, ct) 签名完整                                             |
| P2-6 | 定义 `IAppRuntimeSupervisor` 接口 *(依赖 P2-3)*                  | `Atlas.Shared.Contracts/Process/IAppRuntimeSupervisor.cs` | EnsureRunningAsync/EvictAsync/GetDesiredStateAsync 签名完整                  |


---

### Phase 2B — TenantAppInstance 实体扩展（3 cases）


| #    | Case                                                           | 目标文件                                                                | 验收标准                                                  |
| ---- | -------------------------------------------------------------- | ------------------------------------------------------------------- | ----------------------------------------------------- |
| P2-7 | 新增 Port/Pid/RuntimeStatus 字段 *(依赖 P2-2)*                       | `Atlas.Domain.*/Entities/TenantAppInstance.cs`                      | 字段加入实体，主库 SQLite 补列（`ALTER TABLE ADD COLUMN`），迁移服务不报错 |
| P2-8 | 新增 InstallPath/IngressUrl/CurrentReleaseVersion 字段 *(依赖 P2-7)* | `TenantAppInstance.cs`                                              | 同上，`dotnet build` 0 警告                                |
| P2-9 | 新增 LastHeartbeat/LastExitCode + DTO 更新 *(依赖 P2-8)*             | `TenantAppInstance.cs` + `TenantAppInstancesV2Controller.cs` 响应 DTO | DTO 包含新字段，GET 接口返回新字段；更新 `AppInstances.http`          |


---

### Phase 2C — PlatformHost 进程管理实现（5 cases）


| #     | Case                                                                 | 目标文件                                                              | 验收标准                                                                                       |
| ----- | -------------------------------------------------------------------- | ----------------------------------------------------------------- | ------------------------------------------------------------------------------------------ |
| P2-10 | 实现 `LocalChildProcessManager` *(依赖 P2-3/P1-1)*                       | `Atlas.PlatformHost/Process/LocalChildProcessManager.cs`          | Start/Stop/Restart 调用 `System.Diagnostics.Process`，进程退出时更新 TenantAppInstance.RuntimeStatus |
| P2-11 | 实现 `HttpAppHealthProbe` *(依赖 P2-5/P1-1)*                             | `Atlas.PlatformHost/Health/HttpAppHealthProbe.cs`                 | GET /health/ready，超时 3s，反序列化 AppHealthReport；单元 case：mock HttpClient 验证超时处理                |
| P2-12 | 实现 `AppRuntimeSupervisorHostedService` *(依赖 P2-10/11)*               | `Atlas.PlatformHost/Process/AppRuntimeSupervisorHostedService.cs` | 5s 轮询；失败 3 次停止自动重启并写 LastExitCode；批量查询 TenantAppInstance，不在循环内单条 SELECT                    |
| P2-13 | AppHost 新增 `HealthController` *(依赖 P1-3/P2-4)*                       | `Atlas.AppHost/Controllers/HealthController.cs`                   | GET /health/live 200、GET /health/ready DB 探测、GET /health/info 返回 AppHealthReport           |
| P2-14 | TenantAppInstancesV2Controller 新增 start/stop/restart 端点 *(依赖 P2-10)* | `TenantAppInstancesV2Controller.cs`                               | POST /{id}/start、/{id}/stop、/{id}/restart；更新 `AppInstances.http`；幂等键必填                     |


---

### Phase 2D — 前端进程状态展示（1 case）


| #     | Case                             | 目标文件                                           | 验收标准                                                                          |
| ----- | -------------------------------- | ---------------------------------------------- | ----------------------------------------------------------------------------- |
| P2-15 | Console 进程状态面板 *(依赖 P2-9/P2-14)* | `src/pages/console/TenantApplicationsPage.vue` | 新增「运行状态」列（PID/端口/RuntimeStatus/Health/版本/失败原因），15s 轮询，start/stop/restart 操作按钮 |


---

### Phase 3 — 独立入口与独立登录页（8 cases）


| #    | Case                                          | 目标文件                                                                   | 验收标准                                                                                               |
| ---- | --------------------------------------------- | ---------------------------------------------------------------------- | -------------------------------------------------------------------------------------------------- |
| P3-1 | 平台 AuthController 新增 app-entry 接口 *(依赖 P0-6)* | `Atlas.WebApi/Controllers/AuthController.cs` + `Auth.http`             | GET /api/v1/auth/app-entry?appKey={key} 返回 AppLoginEntry（appName/logo/slogan/authMode/callbackUrl） |
| P3-2 | AppHost AppAuthController *(依赖 P1-3)*         | `Atlas.AppHost/Controllers/AppAuthController.cs`                       | GET /auth/app/callback（OIDC 回调）、POST /auth/app/logout，颁发 audience=atlas-app:{appKey} JWT           |
| P3-3 | AppHost JWT audience 配置 *(依赖 P3-2)*           | `Atlas.AppHost/appsettings.AppHost.json`                               | audience = atlas-app:{appKey}，与 PlatformHost audience 分离，不影响平台接口调用                                 |
| P3-4 | 前端 `AppLoginPage.vue` *(依赖 P1-12/P3-1)*       | `src/pages/app-runtime/AppLoginPage.vue`                               | 按 appKey 调 /api/v1/auth/app-entry 拉取品牌，展示品牌化登录表单                                                   |
| P3-5 | 前端 `AppEntryGatewayPage.vue` *(依赖 P3-4)*      | `src/pages/app-runtime/AppEntryGatewayPage.vue`                        | 未登录时跳转 AppLoginPage（含 redirect 参数）                                                                 |
| P3-6 | `appRuntimeGuard.ts` 正式实现 *(依赖 P3-5/P0-8)*    | `src/router/guards/appRuntimeGuard.ts` + `router/index.ts`             | /r/* 路由无 app token 时跳 AppEntryGatewayPage；登录后能正常进入 AppRuntimeShell                                 |
| P3-7 | 拆分 api-auth.ts *(依赖 P3-3/P3-6)*               | `src/services/platform-auth.ts` + `src/services/app-auth.ts`           | api-core.ts 按路由前缀（/api/ vs /app-api/）选择 auth client；旧 api-auth.ts 保留 re-export 兼容                  |
| P3-8 | PlatformHost YARP 动态反代 *(依赖 P2-10/P2-12)*     | `Atlas.PlatformHost/ReverseProxy/AppHostProxyConfig.cs` + `Program.cs` | /app-host/{appKey}/* 动态路由到 AppHost 对应端口；更新 `AppHostProxy.http`                                     |


---

### Phase 4 — 应用打包与发布（8 cases）


| #    | Case                                                                         | 目标文件                                                          | 验收标准                                                                                                         |
| ---- | ---------------------------------------------------------------------------- | ------------------------------------------------------------- | ------------------------------------------------------------------------------------------------------------ |
| P4-1 | 定义 `AppPackageManifest` *(依赖 P0-1)*                                          | `Atlas.Shared.Contracts/Package/AppPackageManifest.cs`        | 含 AppKey/Version/BuildHash/FrontendBundle/BackendExecutable/MigrationManifest/HealthEndpoints/ConfigTemplate |
| P4-2 | 定义 `IAppPackageInstaller` 接口 *(依赖 P4-1)*                                     | `Atlas.Shared.Contracts/Package/IAppPackageInstaller.cs`      | InstallAsync/UninstallAsync/ValidateAsync 签名完整                                                               |
| P4-3 | `AppRelease` 实体新增 ArtifactId/Checksum/InstallSpec *(依赖 P4-1)*                | `Atlas.Domain.*/Entities/AppRelease.cs`                       | 三字段加入实体，主库补列，编译 0 警告                                                                                         |
| P4-4 | `AppRelease` 实体新增 RollbackMetadata *(依赖 P4-3)*                               | `AppRelease.cs`                                               | 字段加入，GET 响应 DTO 包含该字段                                                                                        |
| P4-5 | 打包脚本 `pack.ps1` *(依赖 P1-13/P4-1)*                                            | `build/app-packager/pack.ps1`                                 | 接收 appKey/version 参数，输出含 manifest.json + checksums.sha256 的 app-package.zip                                  |
| P4-6 | 实现 `FileSystemAppPackageInstaller` *(依赖 P4-2/P4-5/P2-10)*                    | `Atlas.PlatformHost/Package/FileSystemAppPackageInstaller.cs` | 解压 zip → 注入 env → 调 AppMigrationService → 注册 AppRelease.ArtifactId                                           |
| P4-7 | ReleaseCenterV2Controller 新增 install 端点 *(依赖 P4-6)*                          | `ReleaseCenterV2Controller.cs` + `ReleaseCenter.http`         | POST /{releaseId}/install 触发安装，返回安装状态                                                                        |
| P4-8 | ReleaseCenterV2Controller 新增 rollback 端点 + GovernanceServices 升级 *(依赖 P4-7)* | `ReleaseCenterV2Controller.cs` + `GovernanceServices.cs`      | POST /{releaseId}/rollback 切回上一版本；GovernanceServices 生成时写 manifest.json 和 checksums.sha256                   |


---

### Phase 5 — 独立部署与运维完善（7 cases）


| #    | Case                               | 目标文件                                                | 验收标准                                                                                    |
| ---- | ---------------------------------- | --------------------------------------------------- | --------------------------------------------------------------------------------------- |
| P5-1 | AppHost OTEL 资源标签 *(依赖 P1-3)*      | `Atlas.AppHost/Program.cs` OTel 配置段                 | atlas.scope/atlas.appKey/atlas.tenantId/atlas.releaseVersion 出现在 trace span 的 resource  |
| P5-2 | AppHost NLog 结构化字段 *(依赖 P5-1)*     | `Atlas.AppHost/nlog.config`                         | AppKey/TenantId/ReleaseVersion 出现在日志 layout                                             |
| P5-3 | systemd service 模板 *(依赖 P4-5)*     | `deploy/app-host/systemd/atlas-apphost@.service`    | %i 为 appKey，ExecStart 指向 AppHost，注入 ASPNETCORE_URLS/APP_KEY                             |
| P5-4 | Windows Service 安装脚本 *(依赖 P4-5)*   | `deploy/app-host/windows-service/install.ps1`       | New-Service，参数化 appKey/port/installPath                                                 |
| P5-5 | 平台 Console 实时日志面板 *(依赖 P3-8/P5-2)* | `src/pages/console/TenantApplicationsPage.vue` 日志抽屉 | SSE 连接 /api/v1/app-instances/{id}/logs/stream，实时输出 AppHost 日志                           |
| P5-6 | 平台 Console 资源占用卡片 *(依赖 P2-13)*     | `src/pages/console/TenantApplicationsPage.vue` 资源卡片 | CPU%/内存 MB，来自 /health/info，15s 刷新                                                       |
| P5-7 | AppHost Dockerfile 模板 *(依赖 P4-5)*  | `deploy/app-host/Dockerfile`                        | FROM mcr.microsoft.com/dotnet/aspnet:10.0，COPY app-package.zip，ENTRYPOINT Atlas.AppHost |


---

## 现状确认（已核验）

- **后端**：`Atlas.WebApi` 单进程，10 个 ProjectReference，150+ 个控制器，平台控制面/应用设计面/应用运行面三种职责混入同一宿主；`Atlas.AppHost`、`Atlas.PlatformHost`、`Atlas.Shared.Contracts` 均不存在。
- **前端**：`Atlas.WebApp` 单 SPA，`App.vue` 按路径切换布局，不是独立入口；`vite.config.ts` 已有 `app` + `embed-chat` 双 entry，但主业务仍是单入口。
- **关键可复用对象**：`ApplicationCatalog`、`TenantAppInstance`、`AppRelease`、`RuntimeContext`、`RuntimeExecution`、`TenantAppDataSourceBinding`、`IAppDbScopeFactory`（已有 `InvalidateAppClientCache`）、`AppMigrationService`。
- **关键下沉目标**：`PageRuntimeController.cs` 是首批必须下沉到 AppHost 的运行时接口。

## 目标架构

```
Browser
 ├─ PlatformConsole (/console/*)
 ├─ AppStudio (/apps/*)
 │
 └─ AppRuntimeShell (/r/* 或 {appKey}.domain.com)
     └─ AppLogin (/app-host/{appKey}/login)

     ↓ via YARP / path proxy

Atlas.PlatformHost  (平台主进程 :5000)
 ├─ Auth / Tenant / RBAC
 ├─ Catalog / Release / Ops
 ├─ RuntimeRegistry / Ingress Resolver
 └─ IAppProcessManager / IAppRuntimeSupervisor
         │
         │  子进程拉起 + 健康探测 + 流量路由
         ↓
Atlas.AppHost A (:5100)          Atlas.AppHost B (:5101)
 ├─ PageRuntime / LowCode RT      ├─ Workflow RT / AgentTeam RT
 ├─ DynamicTables RT              ├─ LogicFlow RT
 └─ App DB A                      └─ App DB B
```

## 分阶段计划

### Phase 0 — 止血与边界固化

**目标**：建立宿主项目壳和边界规范，所有新功能不再加入旧混合宿主。

- 新建项目：
  - `src/backend/Atlas.PlatformHost/` — 平台主进程（从 `Atlas.WebApi` 派生）
  - `src/backend/Atlas.AppHost/` — 应用运行进程（新建）
  - `src/backend/Atlas.Shared.Contracts/` — 进程间协议/包协议/健康契约（新建）
- 标记 `PageRuntimeController.cs` 为首批下沉目标（添加 ADR 注释）
- 标记 `AppManifestsController.cs` 为兼容接口（待 Phase 4 废弃）
- 前端新增 `platformGuard` / `appRuntimeGuard` 抽象，分离路由守卫逻辑
- 更新 `docs/contracts.md` 固化控制面/数据面命名

**验收**：三个新项目可编译（空壳）；边界文档定版；旧宿主不动，不切流。

---

### Phase 1 — 控制面 / 数据面代码分层

**目标**：`Atlas.WebApi` 收缩为 `Atlas.PlatformHost`；`Atlas.AppHost` 接入首批运行时能力。

后端改造：

- `Atlas.PlatformHost` 保留控制器：`ApplicationCatalogsV2Controller`、`TenantAppInstancesV2Controller`、`ReleaseCenterV2Controller`、`RuntimeContextsV2Controller`、`RuntimeExecutionsV2Controller`、`AuthController`
- `Atlas.AppHost` 接入：`PageRuntimeController`、lowcode runtime、DynamicTables runtime
- 运行时写操作（执行、提交、任务处理）不再由平台主宿主直接调用

前端改造（`vite.config.ts` 多入口扩展）：

- 在现有 `input.app` 基础上新增 `platform-console`、`app-studio`、`app-runtime`、`app-login` 四个 entry
- `App.vue` 布局切换逻辑迁移到各自 entry 的根组件

页面归属：

- 留平台：`pages/console/`*、`pages/system/`*、`pages/settings/*`、`pages/monitor/*`
- 进 AppStudio：`pages/apps/*`、`pages/lowcode/*`、app-scoped 的 `pages/ai/*`、`pages/workflow/*`、`pages/logic-flow/*`
- 进 AppRuntimeShell：`pages/runtime/PageRuntimeRenderer.vue`

**验收**：`PageRuntimeController` 已在 AppHost 编译；前端可分别构建 4 个产物；旧宿主保留兼容路径，不破坏现有 E2E 流程。

---

### Phase 2 — 应用独立进程化

**目标**：每个 `TenantAppInstance` 对应一个独立 `Atlas.AppHost` 进程，平台通过本地子进程拉起。

后端核心接口（放入 `Atlas.Shared.Contracts`）：

```csharp
public interface IAppProcessManager
{
    Task StartAsync(AppProcessSpec spec, CancellationToken ct);
    Task StopAsync(long appInstanceId, CancellationToken ct);
    Task<AppProcessStatus> GetStatusAsync(long appInstanceId, CancellationToken ct);
}

public interface IAppRuntimeSupervisor  // HostedService
{
    // 维护 desired/actual state，失败退避重启，超阈值告警并摘流量
}

public interface IAppHealthProbe
{
    Task<AppHealthReport> ProbeAsync(string baseUrl, CancellationToken ct);
}
```

`TenantAppInstance` 必须扩展的字段：

- `Port`、`Pid`、`RuntimeStatus`（running/stopped/crashed/starting）
- `CurrentReleaseVersion`、`InstallPath`、`IngressUrl`、`LastHeartbeat`、`LastExitCode`

AppHost 必须提供健康端点：

- `GET /health/live`、`GET /health/ready`、`GET /health/info`

**验收**：每个 TenantAppInstance 可单独 start/stop/restart；AppHost 崩溃不影响 PlatformHost；平台 Console 可查看 PID、端口、health、版本、失败原因。

---

### Phase 3 — 独立入口与独立登录页

**目标**：应用有独立 URL 入口和品牌化登录页，会话分平台与应用两套。

后端新增（AppHost 侧认证端点）：

- `GET /auth/app/entry` — 返回 app 登录配置（品牌、认证模式、回跳 URL）
- `GET /auth/app/callback` — OIDC/SSO 回调
- `POST /auth/app/logout`

URL 规划：

- Phase 3：`https://platform.example.com/app-host/{appKey}` （路径模式，DNS 不变）
- Phase 5+：`https://{appKey}.example.com` （子域模式）

前端改造：

- 新增 `AppLoginPage.vue`（按 `appKey` 拉取 branding/auth-mode）
- 新增 `AppEntryGatewayPage.vue`（入口路由）
- 拆分 `api-auth.ts` 为 `platform-auth.ts` + `app-auth.ts`
- `router/index.ts` 拆分为 `platformGuard` + `appRuntimeGuard` 两个独立守卫

会话设计：

- 共享 IdP（平台 `AuthController` 作为统一身份网关）
- 分离 audience：平台 token `atlas-platform`，应用 token `atlas-app:{appKey}`
- AppHost logout 只清 app session，联动平台全局 logout 可配置

**验收**：未登录访问 AppRuntime 跳 AppLogin；登录后进入应用而不是 `/console`；AppLogout 不破坏平台主会话。

---

### Phase 4 — 应用打包与发布

**目标**：每个 AppRelease 产出独立可安装 app package，平台托管安装/回滚。

App Package 规范（`Atlas.Shared.Contracts.AppPackageManifest`）：

```
app-package.zip
  manifest.json          # AppPackageManifest
  frontend/runtime/      # AppRuntimeShell bundle
  frontend/login/        # AppLogin bundle
  backend/Atlas.AppHost  # AppHost 可执行文件
  config/env.template    # 部署时注入模板
  migrations/            # SQL + migration-manifest.json
  health/endpoints.json
  metadata/checksums.sha256
```

后端改造：

- 新建 `build/app-packager/` — 构建脚本，输出 `app-package.zip`
- 新增 `IAppPackageInstaller` — 解压、注入 config、执行 migrations、注册
- 升级 `AppRelease` 实体：新增 `ArtifactId`、`Checksum`、`InstallSpec`、`RollbackMetadata`
- 升级 `GovernanceServices.cs` 的 `PackageService` 升级为 canonical artifact builder
- `ReleaseCenterV2Controller` 接入 artifact 维度（下载、安装、校验、回滚）

**验收**：每个 release 产出独立 zip；平台托管安装成功；可回滚到上一版本。

---

### Phase 5 — 独立部署与运维完善

**目标**：AppHost 可脱离平台主机独立部署，完善运维观测体系。

- systemd 单元文件（`deploy/app-host/systemd/`）
- Windows Service 适配（`deploy/app-host/windows-service/`）
- per-app 实时日志查看（平台 Console 透传 AppHost 日志 stream）
- CPU/内存资源观测（Linux cgroup / Windows Job Object）
- OpenTelemetry 资源标签规范：`atlas.scope`、`atlas.appKey`、`atlas.tenantId`、`atlas.releaseVersion`
- 可选 Docker 化（`FROM atlasbase:10 / COPY app-package.zip`）

**验收**：app package 可独立部署到任意环境；可复用平台 IdP 或切外部 OIDC；平台 Console 可查看健康、日志、版本、资源。

---

## 关键文件改造索引

| 文件 | Phase | 操作 |

- `[src/backend/Atlas.WebApi/Program.cs](src/backend/Atlas.WebApi/Program.cs)` | 0→1 | 收缩为 PlatformHost 配置入口
- `[src/backend/Atlas.WebApi/Atlas.WebApi.csproj](src/backend/Atlas.WebApi/Atlas.WebApi.csproj)` | 1 | 逐步迁移 ProjectReference 到 PlatformHost
- `[src/backend/Atlas.WebApi/Controllers/PageRuntimeController.cs](src/backend/Atlas.WebApi/Controllers/PageRuntimeController.cs)` | 1 | 迁移到 AppHost
- `[src/backend/Atlas.Infrastructure/Services/GovernanceServices.cs](src/backend/Atlas.Infrastructure/Services/Governance/GovernanceServices.cs)` | 4 | PackageService 升级为 canonical artifact builder
- `[src/backend/Atlas.Infrastructure/Services/IAppDbScopeFactory.cs](src/backend/Atlas.Infrastructure/Services/IAppDbScopeFactory.cs)` | 2 | 扩展为 AppHost DB client factory
- `[src/frontend/Atlas.WebApp/vite.config.ts](src/frontend/Atlas.WebApp/vite.config.ts)` | 1 | 多入口扩展（4 entry）
- `[src/frontend/Atlas.WebApp/src/App.vue](src/frontend/Atlas.WebApp/src/App.vue)` | 1 | 布局切换逻辑迁出到各自 entry 根组件
- `[src/frontend/Atlas.WebApp/src/router/index.ts](src/frontend/Atlas.WebApp/src/router/index.ts)` | 1→3 | 拆分 guard；Phase 3 后拆 auth client
- `[src/frontend/Atlas.WebApp/src/services/api-core.ts](src/frontend/Atlas.WebApp/src/services/api-core.ts)` | 3 | 拆分 platform/app base URL 策略
- `[docs/contracts.md](docs/contracts.md)` | 0 | 固化控制面/数据面命名；新增包规范章节

## 强约束说明

- **不推倒仓库**：保留 `Atlas.Core`、`Atlas.Domain`、`Atlas.Infrastructure`、`Atlas.Application.`* 分层；只新增宿主层。
- **旧宿主兼容窗口**：`Atlas.WebApi` 在 Phase 1 完成前保持可运行，不切流，仅用于兼容回退。
- **禁止把 ALC/PluginLoadContext 当进程隔离方案**：插件机制保留为程序集扩展用途，不承担 AppHost 隔离职责。
- **数据库禁止循环内操作**（现有约束继续遵守）：进程管理/健康采集批量查询，不在进程轮询循环内单条 SELECT。

