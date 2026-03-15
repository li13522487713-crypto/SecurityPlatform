# Atlas Security Platform — 项目架构地图

> 分析时间：2026-03-14  
> 标注说明：✅ 明确确认  ⚠️ 推测

---

## 一、技术栈

### 后端 ✅

| 层面 | 技术 | 版本/说明 |
|------|------|-----------|
| 运行时 | .NET 10 + ASP.NET Core | `net10.0` |
| ORM | SqlSugar | 5.1.4.169，支持 SQLite/MySQL/PostgreSQL/SqlServer |
| 主数据库 | SQLite | [atlas.db](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/atlas.db) |
| 定时任务存储 | SQLite（Hangfire） | [hangfire.db](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/hangfire.db) |
| 认证 | JWT Bearer + 客户端证书 + PAT | 三种 scheme 并联 |
| 授权 | RBAC + 自定义 PermissionPolicy | `PermissionPolicyProvider` |
| 验证 | FluentValidation 11.4.0 | 自动接入 `AddFluentValidationAutoValidation` |
| 对象映射 | AutoMapper 12.0.1 | 各模块独立 Profile |
| ID 生成 | Snowflake（IdGen 3.0.1） | 租户级别映射配置 |
| 日志 | NLog 5.3.4 | 4 个滚动文件 + 控制台 |
| 定时任务 | Hangfire + SQLite Storage | |
| 工作流引擎 | WorkflowCore 自研封装（`Atlas.WorkflowCore`） | 含 DSL 扩展 |
| 可观测性 | OpenTelemetry（Tracing + Metrics） | 支持 OTLP 导出 |
| API 文档 | NSwag（OpenAPI/Swagger） | Dev 环境 `/swagger` |
| 限流 | ASP.NET Core RateLimiter | 登录接口 10次/分/IP |
| 国际化 | `AddLocalization`，支持 zh-CN / en-US | |
| AI 平台集成 | OpenAI / DeepSeek / Ollama | `appsettings.json AiPlatform` 节 |
| OIDC（可选） | OpenIdConnect | `Oidc:Enabled` 控制 |
| 消息队列 | SQLite 自研（`SqliteMessageQueue`） | 进程内 |

### 前端 ✅

| 层面 | 技术 | 版本 |
|------|------|------|
| 框架 | Vue 3 + Composition API | 3.5.27 |
| 构建工具 | Vite | 7.2.4 |
| 类型 | TypeScript 严格模式 | 5.9.3 |
| UI 库 | Ant Design Vue | 4.2.6 |
| 路由 | Vue Router | 4.6.4 |
| 状态管理 | Pinia | — |
| 低代码渲染 | Amis（百度开源） | 引入 `amis/lib` 主题 |
| i18n | vue-i18n | 自定义 [i18n.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/i18n.ts) |

---

## 二、目录结构与职责

```
e:\codeding\SecurityPlatform\
├── src/
│   ├── backend/
│   │   ├── Atlas.Core/                  # ✅ 基础抽象：EntityBase/TenantEntity/接口/异常/事件总线
│   │   │   ├── Abstractions/            # 实体基类、聚合根
│   │   │   ├── Models/                  # ApiResponse<T>、PagedResult、ErrorCodes
│   │   │   ├── Tenancy/                 # TenantId、ITenantProvider
│   │   │   ├── Identity/                # ICurrentUserAccessor、IClientContextAccessor
│   │   │   ├── Messaging/               # IMessageQueue 接口
│   │   │   ├── Saga/                    # ISagaOrchestrator 接口
│   │   │   └── Exceptions/              # BusinessException
│   │   │
│   │   ├── Atlas.Domain/                # ✅ 领域基类（无具体实体）
│   │   ├── Atlas.Domain.Alert/          # 告警上下文实体
│   │   ├── Atlas.Domain.Assets/         # 资产上下文实体
│   │   ├── Atlas.Domain.Audit/          # 审计上下文实体
│   │   ├── Atlas.Domain.Approval/       # 审批上下文实体（含 ApprovalTask 乐观锁 RowVersion）
│   │   ├── Atlas.Domain.Workflow/       # 工作流上下文实体
│   │   │
│   │   ├── Atlas.Application/           # ✅ 应用层基础（AutoMapper 注册、公共 DTO）
│   │   ├── Atlas.Application.Alert/     # 告警应用层（抽象+模型+验证器）
│   │   ├── Atlas.Application.Assets/    # 资产应用层
│   │   ├── Atlas.Application.Audit/     # 审计应用层
│   │   ├── Atlas.Application.Approval/  # 审批应用层（最复杂，含 Saga/DSL）
│   │   ├── Atlas.Application.Workflow/  # 工作流应用层
│   │   │
│   │   ├── Atlas.Infrastructure/        # ✅ 基础设施实现层
│   │   │   ├── DependencyInjection/     # 模块化 DI 注册（10 个 *Registration.cs）
│   │   │   ├── Services/                # *QueryService / *CommandService 实现（80+个文件）
│   │   │   ├── Repositories/            # SqlSugar Repository 实现（112 个文件）
│   │   │   ├── DataSource/              # 多数据源连接（SqliteDataSourceConnector）
│   │   │   ├── DynamicTables/           # 动态表结构管理
│   │   │   ├── Events/                  # OutboxPublisher / EventSubscriptionService
│   │   │   ├── Messaging/               # SqliteMessageQueue + Processor HostedService
│   │   │   ├── Saga/                    # SagaOrchestrator
│   │   │   ├── Security/                # Pbkdf2PasswordHasher / OidcAccountMapper
│   │   │   ├── Workflow/                # WorkflowEngine 子目录
│   │   │   ├── AiPlatform/              # AI 服务实现
│   │   │   ├── LowCode/                 # 低代码应用服务
│   │   │   ├── Plugins/                 # PluginPackageService / PluginMetricsStore
│   │   │   ├── Governance/              # 数据治理相关服务
│   │   │   └── ServiceCollectionExtensions.cs  # 入口聚合注册
│   │   │
│   │   ├── Atlas.WebApi/                # ✅ 表示层（API 入口）
│   │   │   ├── Program.cs               # 启动入口（460 行，含全部中间件注册）
│   │   │   ├── Controllers/             # 85 个 Controller 文件 + Open/ 子目录
│   │   │   ├── Middlewares/             # 10 个中间件（见下文）
│   │   │   ├── Authorization/           # PermissionPolicyProvider / Handler
│   │   │   ├── Filters/                 # IdempotencyFilter（全局过滤器）
│   │   │   ├── Identity/                # HttpContext 上下文访问器（当前用户/应用/项目）
│   │   │   ├── Tenancy/                 # HttpContextTenantProvider
│   │   │   ├── Security/                # PatAuthenticationHandler（PAT 认证）
│   │   │   ├── Json/                    # FlexibleLongJsonConverter / SensitiveObjectConverterFactory
│   │   │   ├── Validators/              # WebApi 层 FluentValidation 验证器
│   │   │   ├── Bosch.http/              # REST Client 接口测试文件
│   │   │   ├── AmisSchemas/             # Amis JSON Schema 文件（低代码页面定义）
│   │   │   ├── appsettings.json         # 生产默认配置
│   │   │   ├── appsettings.Development.json  # 开发覆盖配置（含 Bootstrap Admin 密码）
│   │   │   ├── appsettings.Production.json   # 生产严格配置
│   │   │   ├── nlog.config              # NLog 日志配置
│   │   │   ├── atlas.db                 # SQLite 主数据库（开发期）
│   │   │   └── hangfire.db              # Hangfire 任务存储
│   │   │
│   │   ├── Atlas.WorkflowCore/          # ✅ 自研工作流引擎内核
│   │   └── Atlas.WorkflowCore.DSL/      # 工作流 DSL 解析扩展
│   │
│   └── frontend/
│       └── Atlas.WebApp/
│           ├── src/
│           │   ├── main.ts              # Vue 入口（暖启动 Auth Session）
│           │   ├── App.vue              # 根组件
│           │   ├── router/index.ts      # 路由定义 + 全局路由守卫
│           │   ├── stores/              # Pinia Store（user.ts / permission.ts / tagsView.ts）
│           │   ├── services/            # API 客户端（39 个 api-*.ts 文件）
│           │   │   └── api-core.ts      # 统一 HTTP 基础设施（认证/刷新/CSRF/幂等）
│           │   ├── pages/               # 页面组件（27 个 .vue + 12 个子目录）
│           │   ├── components/          # 可复用组件
│           │   ├── composables/         # Vue Composables
│           │   ├── layouts/             # 布局组件
│           │   ├── types/               # TypeScript 接口类型定义
│           │   ├── utils/               # 工具函数（auth.ts / app-context.ts 等）
│           │   ├── directives/          # 自定义指令（permission.ts：v-hasPermi / v-hasRole）
│           │   ├── i18n.ts              # 国际化初始化
│           │   ├── locales/             # 语言包
│           │   ├── styles/              # 全局样式（含 amis-overrides.css）
│           │   ├── amis/                # Amis 渲染封装
│           │   ├── compat/              # 兼容层
│           │   └── plugins/             # Vue 插件注册
│           ├── vite.config.ts           # Vite 配置（代理 /api → :5000）
│           └── package.json
│
├── docs/                                # 文档：PRD、计划、契约、架构分析
├── tests/                               # 测试项目（Atlas.WorkflowCore.Tests / SecurityPlatform.Tests）
├── deploy/                              # 部署脚本
├── CLAUDE.md                            # 技术详细指导
├── AGENTS.md                            # AI 助理协作规范
└── 等保2.0要求清单.md                   # 等保合规要求清单
```

---

## 三、启动入口

### 后端 ✅

**文件**：[src/backend/Atlas.WebApi/Program.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/Program.cs)（460 行）

启动顺序关键节点：
1. `WebApplication.CreateBuilder(args)` → 配置 NLog、Controllers、OpenAPI
2. Options 注册（Jwt / Security / FileStorage / Idempotency / TableViewDefaults / App 等 12+ 项）
3. OIDC 可选（`Oidc:Enabled`），JWT Bearer + Certificate + PAT 三种认证 scheme
4. CORS、HttpLogging、OpenTelemetry（Tracing + Metrics）
5. `AddAtlasApplication()` + [AddAtlasInfrastructure(config)](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.Infrastructure/ServiceCollectionExtensions.cs#14-161)（模块化 DI 注册）
6. Hangfire（[hangfire.db](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/hangfire.db) SQLite 存储）
7. WorkflowCore（`AddWorkflowCore()` + `AddWorkflowCoreDsl()`）
8. `app.Build()` → AutoMapper 配置校验（启动失败保障）
9. 中间件管道（顺序）：
   ```
   HTTPS重定向 → SecurityHeaders → HttpLogging → RequestLocalization
   → ExceptionHandling → XssProtection → RateLimiter → ApiVersionRewrite
   → [NSwag if Dev] → CORS → ClientContext → Routing
   → Authentication → AppContext → AntiforgeryValidation
   → TenantContext → ProjectContext → LicenseEnforcement → Authorization
   → MapControllers
   ```

**启动 HostedServices（后台服务）**：
- `DatabaseInitializerHostedService` — DB Schema 初始化 + BootstrapAdmin 数据播种（75KB）
- `DatabaseBackupHostedService` — 每日自动备份
- `AuditRetentionHostedService` — 审计日志定期清理（180天）
- `SessionCleanupHostedService` — 过期 Session 清理
- `IdempotencyCleanupHostedService` — 幂等记录清理
- `OutboxProcessorHostedService` — Outbox 事件发布
- `MessageQueueProcessorHostedService` — SQLite 消息队列消费
- `WorkflowHostedService` — WorkflowCore 引擎启动
- `BackgroundWorkQueueProcessor` — 后台任务队列处理器
- Approval 相关 3 个 HostedService（超时处理、定时提醒、定时节点）

### 前端 ✅

**文件**：[src/frontend/Atlas.WebApp/src/main.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/main.ts)

```
设置默认 tenant_id（localStorage，来自 VITE_DEFAULT_TENANT_ID 环境变量）
→ 注册 window.onerror / unhandledrejection（静默上报前端错误至 /api/v1/audit/client-errors）
→ warmupAuthSession()（用 refreshToken 预热 accessToken，避免首批请求 401）
→ createApp + createPinia + use(router) + use(Antd) + use(i18n)
→ 注册自定义指令 v-hasPermi / v-hasRole
→ app.mount('#app')
```

---

## 四、Web/API 请求主链路

### 后端请求处理链路 ✅

```
HTTP Request
  │
  ▼ ExceptionHandlingMiddleware（全局异常 → ApiResponse<T>.Fail）
  │
  ▼ XssProtectionMiddleware（请求体 XSS 扫描，白名单路径跳过）
  │
  ▼ RateLimiter（登录接口 10次/分/IP，返回 ApiResponse 的 429）
  │
  ▼ ApiVersionRewriteMiddleware（路径重写兼容旧版 /api/ → /api/v1/）
  │
  ▼ ClientContextMiddleware（解析 User-Agent 等客户端上下文）
  │
  ▼ Authentication（JWT / Certificate / PAT + OnTokenValidated 校验用户状态+Session）
  │
  ▼ AppContextMiddleware（解析 X-App-Id / X-App-Workspace 头）
  │
  ▼ AntiforgeryValidationMiddleware（写操作校验 X-CSRF-TOKEN，公共/读操作跳过）
  │
  ▼ TenantContextMiddleware（从 X-Tenant-Id 头提取租户，注入 ITenantProvider）
  │
  ▼ ProjectContextMiddleware（从 X-Project-Id / 用户默认项目解析项目上下文）
  │
  ▼ LicenseEnforcementMiddleware（License 有效性检查）
  │
  ▼ Authorization（DefaultPolicy = 已认证用户 + PermissionRequirement）
  │
  ▼ Controller Action
      │
      ├── FluentValidation 自动验证（Action 参数）
      │
      ├── IdempotencyFilter（写操作：检查/记录 Idempotency-Key → 防重放）
      │
      ├── 注入 IXxxQueryService / IXxxCommandService
      │         │
      │         ├── Application Service（业务逻辑）
      │         │         │
      │         │         └── Repository（SqlSugar → SQLite）
      │         │
      │         └── IAuditWriter（审计写入）
      │
      └── 返回 ApiResponse<T>（统一信封）
```

### 前端请求链路 ✅

```
Vue Component → api-*.ts 的 API 函数
  │
  ▼ requestApi(path, init, options)（api-core.ts）
      ├── 附加 Authorization Bearer（localStorage token）
      ├── 附加 X-Tenant-Id
      ├── 附加 X-App-Id / X-App-Workspace（当前应用上下文）
      ├── 附加 X-Project-Id（项目上下文，可选）
      ├── 写操作附加 Idempotency-Key（UUID）
      ├── 写操作附加 X-CSRF-TOKEN（懒加载，从 /api/v1/secure/antiforgery 获取）
      ├── 写请求去重（inFlightWriteRequests 签名缓存）
      ├── credentials: "include"（携带 httpOnly Cookie）
      │
      ▼ fetch(`/api/v1${path}`, requestInit)
      │
      ├── 401 → tryRefreshTokens → 重试一次
      ├── 403 ANTIFORGERY_TOKEN_INVALID → 清缓存 → 重试一次
      ├── 403 其他 → forceLogout 或 showError
      └── 非 200 → showError（去重弹窗）→ throw ApiRequestError
```

---

## 五、定时任务 / 异步任务链路

### Hangfire 定时任务 ✅

- **存储**：[hangfire.db](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/hangfire.db)（SQLite）
- **控制器**：[ScheduledJobsController.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/Controllers/ScheduledJobsController.cs) — 通过 `IScheduledJobService` (→ `HangfireScheduledJobService`) 管理定时任务 CRUD
- **面板**：⚠️ 推测未暴露 Hangfire Dashboard（Program.cs 中未发现 `app.UseHangfireDashboard()`），仅通过 API 管理

### HostedService 常驻任务 ✅

| 服务 | 触发方式 | 位置 |
|------|----------|------|
| `DatabaseBackupHostedService` | 每 X 小时（`IntervalHours=24`） | Infrastructure/Services |
| `AuditRetentionHostedService` | 定期扫描（周期⚠️推测每日） | Infrastructure/Services |
| `SessionCleanupHostedService` | 定期扫描 | Infrastructure/Services |
| `IdempotencyCleanupHostedService` | 定期清理（`CleanupIntervalMinutes=60`） | Infrastructure/Services |
| `OutboxProcessorHostedService` | 轮询 Outbox 表（至少一次投递） | Infrastructure/Events |
| `MessageQueueProcessorHostedService` | 轮询 SQLite 消息队列 | Infrastructure/Messaging |
| `WorkflowHostedService` | WorkflowCore 引擎常驻 | Infrastructure/Services |
| `BackgroundWorkQueueProcessor` | 通道（Channel）驱动 | Infrastructure/Services |
| `ApprovalTimeoutAutoProcessHostedService` | 审批超时自动处理 | Infrastructure/Services |
| `ApprovalTimeoutReminderHostedService` | 审批超时提醒 | Infrastructure/Services |
| `ApprovalTimerNodeHostedService` | 审批定时节点 | Infrastructure/Services |
| `ApprovalExternalCallbackRetryHostedService` | 外部回调重试 | Infrastructure/Services |

### 消息队列（SQLite-backed） ✅

- **接口**：`Atlas.Core.Messaging.IMessageQueue` → `SqliteMessageQueue`
- **消费者注册**：`IQueueMessageHandler` → `ApprovalEventConsumer`
- **异步开关**：`appsettings.json Messaging:ApprovalEvents:AsyncEnabled`（默认 false，同步模式）

### WorkflowCore 工作流引擎 ✅

- **内核**：`Atlas.WorkflowCore` 自研封装（基于 WorkflowCore 开源库）
- **DSL 扩展**：`Atlas.WorkflowCore.DSL`（支持命名空间 `Atlas.WorkflowCore.Primitives`）
- **运行**：`WorkflowHostedService` 启动引擎

---

## 六、数据库访问层组织方式

### 核心模式 ✅

```
Controller → Service（Query/Command）→ Repository → ISqlSugarClient → SQLite
```

### Repository 层 ✅

- **基类**：[RepositoryBase.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.Infrastructure/Repositories/RepositoryBase.cs)（Infrastructure/Repositories）
- **数量**：112 个 Repository 实现文件
- **注册**：通过各模块 `*Registration.cs` （DI 注册 Scoped）
- **命名**：`I{Entity}Repository`（Application 层接口）→ `{Entity}Repository`（Infrastructure 实现）

### SqlSugar 客户端配置 ✅

- **注册方式**：`services.AddScoped<ISqlSugarClient>(...)`（每请求一个实例）
- **多租户隔离**：启动时注入 `TenantId`，`db.QueryFilter.AddTableFilter<TenantEntity>(it => it.TenantIdValue == tenantId.Value)` 全局过滤
- **DbType 支持**：SQLite / SqlServer / MySQL / PostgreSQL（通过 `Database:DbType` 配置切换）
- **TenantId 字段**：ORM 级别 IsIgnore（逻辑存在但 SqlSugar 不自动映射列，手动控制）
- **乐观锁**：`ApprovalTask.RowVersion`（`IsEnableUpdateVersionValidation = true`）

### 事务 ✅

- **UnitOfWork**：`IUnitOfWork` → `SqlSugarUnitOfWork`（Scoped）

### 多数据源 ✅

- **接口**：`ITenantDataSourceService` + `ITenantDbConnectionFactory`
- **注册数据源类型**：当前仅 `SqliteDataSourceConnector`（⚠️ 推测其他类型未完全实现）
- **存储**：`TenantDataSourceRepository`（租户级别多数据源配置）

---

## 七、配置与环境变量组织方式

### 配置文件层级 ✅

```
appsettings.json（基础/默认）
  ↑ 覆盖
appsettings.Development.json（开发环境，含 BootstrapAdmin 密码）
  ↑ 覆盖
appsettings.Production.json（生产严格模式）
  ↑ 覆盖
环境变量（OTEL_EXPORTER_OTLP_ENDPOINT 等）
```

### 主要配置节 ✅

| 配置节 | 说明 | 关键字段 |
|--------|------|---------|
| `Jwt` | JWT 令牌 | `SigningKey`（生产≥32位）、`ExpiresMinutes=15`、`RefreshExpiresMinutes=720` |
| `Security` | 安全策略 | 密码策略、账户锁定、BootstrapAdmin |
| `Database` | 数据库 | `ConnectionString`、[DbType](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.Infrastructure/ServiceCollectionExtensions.cs#162-178)、加密、备份 |
| `Tenancy` | 多租户 | `HeaderName=X-Tenant-Id` |
| `Cors` | 跨域 | `AllowedOrigins` |
| `FileStorage` | 文件存储 | `BasePath`、允许/拒绝扩展名、`SignedUrlSecret` |
| `AiPlatform` | AI 接入 | 多 Provider（openai/deepseek/ollama）及 Embedding 配置 |
| [Idempotency](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/services/api-core.ts#504-515) | 幂等控制 | `RetentionHours=24`、`CleanupIntervalMinutes=60` |
| `TableViewDefaults` | 表格视图默认 | 各页面默认 density/pageSize |
| `Approval.SeedData` | 审批初始数据 | Enabled / InitializeButtonConfigs |
| `Messaging` | 消息队列 | `ApprovalEvents:AsyncEnabled`（默认 false） |
| `Oidc` | OIDC 集成（可选） | `Enabled=false`、Authority/ClientId |
| `Plugins` | 插件 | `RootPath=plugins` |
| `Snowflake` / `IdGenerator` | Snowflake ID | 每租户独立 GeneratorId |
| `CodeExecution` | AI 代码执行 | 超时/最大输出/禁用模块  |
| `Xss` | XSS 防护 | 白名单路径、最大 Body 大小 |
| [App](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/router/index.ts#29-30) | 应用上下文 | `X-App-Id` Header、客户端类型映射 |

### 生产安全约束 ✅

Program.cs 在非 Development 环境强制校验：
- `Jwt:SigningKey` 长度 ≥ 32 且不含 `CHANGE_ME` → 否则启动异常
- `FileStorage:SignedUrlSecret` 同样必须修改且长度 ≥ 32

### 前端环境变量 ✅

| 变量 | 说明 |
|------|------|
| `VITE_API_BASE` | API 基础路径（默认 `/api/v1`） |
| `VITE_DEFAULT_TENANT_ID` | 开发默认租户 ID（GUID） |

---

## 八、权限认证机制

### 认证（Authentication）✅

三种 Scheme 并联，任一通过则认证成功：

| Scheme | 实现 | 说明 |
|--------|------|------|
| `JwtBearer` | ASP.NET Core 内置 | Cookie `access_token` 优先，兜底 Authorization Header |
| `Certificate` | ASP.NET Core 内置 | 客户端证书（等保2.0 双因素） |
| `PAT` | 自研 `PatAuthenticationHandler` | Personal Access Token（API 密钥场景） |

JWT Token 验证时额外执行：
1. 验证 `tenant_id` claim 合法性
2. 验证 `userId` claim 合法性
3. **查库**校验用户账号是否存在且激活
4. 验证 Session（`sid` claim）是否有效且未吊销

### 授权（Authorization）✅

- **默认策略**：`RequireAuthenticatedUser`（三 Scheme 任一通过）
- **权限系统**：`PermissionPolicyProvider` + `PermissionAuthorizationHandler`
  - 权限 string 格式：`resource:action`（如 `users:view`、`roles:update`）
  - 控制器使用 `[Authorize(Policy = "permission:xxx")]` 标注
  - 详细权限定义在 [Authorization/PermissionPolicies.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/Authorization/PermissionPolicies.cs)（11KB）
- **RBAC**：`IRbacResolver` → 解析用户角色和权限列表
- **数据权限**：`IDataScopeFilter` → 按部门/项目范围过滤数据（等保2.0 数据权限）

### 安全加固措施 ✅

| 措施 | 实现文件 |
|------|---------|
| CSRF 防御 | [AntiforgeryValidationMiddleware.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/Middlewares/AntiforgeryValidationMiddleware.cs)（写操作必须 `X-CSRF-TOKEN`） |
| XSS 防御 | [XssProtectionMiddleware.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/Middlewares/XssProtectionMiddleware.cs) |
| 安全响应头 | [SecurityHeadersMiddleware.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/Middlewares/SecurityHeadersMiddleware.cs) |
| 限流 | [Program.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/Program.cs) RateLimiter（登录接口） |
| 幂等防重放 | `IdempotencyFilter.cs`（全局 ActionFilter） |
| 密码策略 | PBKDF2 + 复杂度要求 + 90天过期 + 锁定 |
| 令牌吊销 | Session 表 `RevokedAt` 字段 + 每次验证查库 |
| HttpOnly Cookie | 默认从 Cookie 读 Token（比 localStorage 更安全） |
| MFA | [MfaController.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/Controllers/MfaController.cs) + `ITotpService`（TOTP） |

### 前端权限控制 ✅

- **路由守卫**：[router/index.ts](file:///e:/codeding/SecurityPlatform/src/frontend/Atlas.WebApp/src/router/index.ts) `beforeEach` — 检查 token、加载用户信息、校验 `requiresPermission`
- **动态路由**：`permissionStore.generateRoutes()` → `registerRoutes(router)`（后端菜单驱动）
- **组件级**：`v-hasPermi` / `v-hasRole` 自定义指令

---

## 九、外部系统集成点

| 集成点 | 方式 | 配置/位置 |
|--------|------|----------|
| **AI 大模型**（OpenAI/DeepSeek/Ollama） | HTTP Client | `appsettings.json AiPlatform` → `AiPlatform/` 服务实现 |
| **OIDC 身份提供商**（可选） | OpenIdConnect 中间件 | `appsettings.json Oidc:Enabled` → `OidcAccountMapper` |
| **Webhook 外发** | HTTP Client（30s 超时） | [WebhookService.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.Infrastructure/Services/WebhookService.cs) → `IWebhookService` |
| **外部 API 连接器**（低代码数据源） | HTTP Client | [ApiConnectorService.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.Infrastructure/Services/ApiConnectorService.cs) |
| **OpenTelemetry OTLP 导出** | OTLP Exporter | `OTEL_EXPORTER_OTLP_ENDPOINT` 环境变量（可选） |
| **外部审批回调** | HTTP Client + 重试 | `ApprovalExternalCallbackRetryHostedService` |
| **Plugin 市场**（⚠️推测） | HTTP Client | [PluginMarketService.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.Infrastructure/Services/PluginMarketService.cs) + [PluginCatalogService.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.Infrastructure/Services/PluginCatalogService.cs) |
| **多租户外部数据源**（低代码） | `DataSourceConnectorRegistry` | `TenantDataSourceService` + `SqliteDataSourceConnector`（当前仅 SQLite） |

---

## 十、建议继续深挖的前 10 个关键文件/目录

| 优先级 | 文件/目录 | 原因 |
|--------|-----------|------|
| ★★★ | [src/backend/Atlas.Infrastructure/Services/DatabaseInitializerHostedService.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.Infrastructure/Services/DatabaseInitializerHostedService.cs)（75KB） | 最大单文件，包含全量数据库初始化逻辑、Schema 定义、种子数据；理解它等于理解全部领域模型的持久化定义 |
| ★★★ | [src/backend/Atlas.Infrastructure/Services/ApprovalRuntimeCommandService.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.Infrastructure/Services/ApprovalRuntimeCommandService.cs)（38KB） | 最核心的业务服务，审批引擎运行时；含 Saga、状态机逻辑，最能反映复杂性 |
| ★★★ | [src/backend/Atlas.WebApi/Controllers/AuthController.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/Controllers/AuthController.cs)（19KB） | 认证主流程（登录/刷新/登出/密码策略），连接 JWT、Session、MFA、密码历史 |
| ★★★ | [src/backend/Atlas.Infrastructure/Services/JwtAuthTokenService.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.Infrastructure/Services/JwtAuthTokenService.cs)（22KB） | Token 颁发/刷新/吊销核心实现；与 Session、Snowflake ID 深度耦合 |
| ★★ | [src/backend/Atlas.Infrastructure/Services/DynamicTableCommandService.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.Infrastructure/Services/DynamicTableCommandService.cs)（42KB） | 动态表结构管理（最大业务服务），低代码数据能力的核心 |
| ★★ | [src/backend/Atlas.WebApi/Authorization/PermissionPolicies.cs](file:///e:/codeding/SecurityPlatform/src/backend/Atlas.WebApi/Authorization/PermissionPolicies.cs)（11KB） | 全量权限清单；修改任何权限控制前必读 |
| ★★ | `src/backend/Atlas.Infrastructure/DependencyInjection/`（目录） | 10 个 Registration 文件，可读性高，快速了解各模块注册的服务和依赖关系 |
| ★★ | `src/frontend/Atlas.WebApp/src/services/api-core.ts`（25KB） | 前端所有 HTTP 请求的基础设施；认证/刷新/CSRF/幂等/去重全在这里 |
| ★★ | `src/backend/Atlas.Infrastructure/Services/ApprovalFlow/`（目录） | ApprovalEventConsumer 等审批事件处理器；异步消息队列的消费逻辑 |
| ★ | `src/backend/Atlas.WorkflowCore/ServiceCollectionExtensions.cs`（5.8KB） | 自研工作流引擎的注册入口；了解工作流与审批模块的边界关系 |

---

*注：以上分析基于代码直接读取，标注"✅明确确认"的条目均有源码支撑；标注"⚠️推测"的条目基于命名模式、配置项或架构惯例推断，需进一步阅读相关文件确认。*
