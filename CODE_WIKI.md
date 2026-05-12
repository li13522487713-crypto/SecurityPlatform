# SecurityPlatform (Atlas) — Code Wiki

> 生成日期: 2026-05-12  
> 版本: 基于当前 `main` 分支最新代码

---

## 一、项目概述

**SecurityPlatform** 是一套面向**等保2.0合规建设**的统一安全支撑平台与基础设施支撑平台解决方案。采用**多租户 + Clean Architecture**分层架构，提供从身份认证、权限管理、资产审计、审批流程到AI智能体、低代码开发、微流引擎等全方位的企业级安全管控能力。

### 核心技术栈

| 层面 | 技术 |
|------|------|
| 后端框架 | .NET 10.0 / ASP.NET Core |
| ORM | SqlSugar |
| 认证 | JWT Bearer + 证书认证 + PAT |
| 后台任务 | Hangfire (SQLite) |
| 工作流引擎 | WorkflowCore |
| 可观测性 | OpenTelemetry (Trace + Metrics) |
| 日志 | NLog |
| API文档 | NSwag (Swagger/OpenAPI) |
| 前端框架 | React 18 + TypeScript |
| UI组件库 | Semi Design (Douyinfe) |
| 构建工具 | Rsbuild |
| 状态管理 | Zustand |
| 路由 | React Router v6 |
| 测试 | Playwright (E2E) + Vitest (Unit) |
| 包管理 | pnpm (monorepo workspace) |

---

## 二、项目整体架构

```
┌─────────────────────────────────────────────────────────────────┐
│                        前端 (src/frontend)                       │
│  ┌──────────────┐  ┌──────────────────┐  ┌───────────────────┐  │
│  │  app-web      │  │ lowcode-mini-host│  │ coze-workflow-host│  │
│  │ (主应用,RSbuild)│  │ (低代码宿主)      │  │ (Coze工作流宿主)   │  │
│  └──────┬───────┘  └──────────────────┘  └───────────────────┘  │
│         │                                                        │
│  ┌──────┴───────────────────────────────────────────────────┐   │
│  │  packages (共享包层)                                       │   │
│  │  arch/ · common/ · workflow/ · app-shell-shared/ 等       │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────┬───────────────────────────────────┘
                              │ HTTP/WebSocket (SignalR)
┌─────────────────────────────┴───────────────────────────────────┐
│                      后端 (src/backend)                          │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Atlas.AppHost (.NET 10 Web Host, 端口5002)               │   │
│  │  Controllers · Middleware · SignalR Hubs · HealthChecks   │   │
│  └──────────────┬───────────────────────────────────────────┘   │
│                 │                                                │
│  ┌──────────────┴───────────────────────────────────────────┐   │
│  │  Presentation Layer                                       │   │
│  │  Atlas.Presentation.Shared (共享控制器/中间件/DTO/验证)      │   │
│  └──────────────┬───────────────────────────────────────────┘   │
│                 │                                                │
│  ┌──────────────┴───────────────────────────────────────────┐   │
│  │  Application Layer (业务逻辑层)                            │   │
│  │  Atlas.Application + 领域模块                              │   │
│  │  (Alert/Approval/Assets/Audit/Workflow/AgentTeam/         │   │
│  │   LowCode/Microflows/LogicFlow/ExternalConnectors 等)      │   │
│  └──────────────┬───────────────────────────────────────────┘   │
│                 │                                                │
│  ┌──────────────┴───────────────────────────────────────────┐   │
│  │  Domain Layer (领域层)                                     │   │
│  │  Atlas.Domain + 领域实体模块                                │   │
│  └──────────────┬───────────────────────────────────────────┘   │
│                 │                                                │
│  ┌──────────────┴───────────────────────────────────────────┐   │
│  │  Infrastructure Layer (基础设施层)                          │   │
│  │  Atlas.Infrastructure (仓储/缓存/ORM/事件总线/后台服务)       │   │
│  └──────────────┬───────────────────────────────────────────┘   │
│                 │                                                │
│  ┌──────────────┴───────────────────────────────────────────┐   │
│  │  Core Layer (核心层)                                       │   │
│  │  Atlas.Core (基础抽象/枚举/事件/响应模型/租户ID)             │   │
│  └──────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

### 分层职责

| 层次 | 项目/模块 | 职责 |
|------|-----------|------|
| **Host** | `Atlas.AppHost` | Web应用入口，组装DI、中间件管道、路由映射 |
| **Presentation** | `Atlas.Presentation.Shared` | 共享控制器、DTO/ViewModel、中间件、验证器、认证处理器 |
| **Application** | `Atlas.Application.*` | 业务用例编排、DTO映射(AutoMapper)、服务接口定义、FluentValidation |
| **Domain** | `Atlas.Domain.*` | 领域实体、值对象、聚合根、领域事件、枚举 |
| **Infrastructure** | `Atlas.Infrastructure.*` | 仓储实现、ORM配置、缓存、事件总线、后台HostedService |
| **Core** | `Atlas.Core` | 跨层基础抽象：ApiResponse、ErrorCodes、TenantId、DataScopeType、领域事件接口 |
| **SDK/Plugin** | `Atlas.AppHost.Sdk` / `Atlas.Sdk.*` | AppHost支撑SDK、连接器/逻辑流插件 |
| **Connector** | `Atlas.Connectors.*` | 外部系统连接器(DingTalk/Feishu/WeCom) |
| **Shared Contracts** | `Atlas.Shared.Contracts` | 前后端共享契约定义 |

---

## 三、目录结构

```
SecurityPlatform/
├── AGENTS.md                        # 团队执行规范（启动/重启/E2E/约束）
├── README.md                        # 项目说明
├── Atlas.SecurityPlatform.slnx      # VS解决方案文件
├── Directory.Build.props            # 全局MSBuild属性
├── .env.example                     # 环境变量模板
├── atlas.db                         # 默认SQLite数据库
├── hangfire-apphost.db              # Hangfire持久化数据库
│
├── src/
│   ├── backend/                     # 后端C#项目
│   │   ├── Atlas.AppHost/           # Web宿主入口
│   │   ├── Atlas.AppHost.Sdk/       # AppHost SDK
│   │   ├── Atlas.Core/              # 核心基础层
│   │   ├── Atlas.Domain/            # 平台级领域实体
│   │   ├── Atlas.Application/       # 平台级应用服务
│   │   ├── Atlas.Infrastructure/    # 平台级基础设施
│   │   ├── Atlas.Presentation.Shared/ # 共享表现层
│   │   ├── Atlas.Shared.Contracts/  # 共享契约
│   │   ├── Atlas.WorkflowCore/      # 工作流引擎
│   │   ├── Atlas.WorkflowCore.DSL/  # 工作流DSL解析
│   │   ├── Atlas.Sdk.ConnectorPlugins/ # 连接器插件SDK
│   │   ├── Atlas.Sdk.LogicFlowPlugins/ # 逻辑流插件SDK
│   │   ├── Atlas.Infrastructure.Channels/ # 通道基础设施
│   │   ├── Atlas.Infrastructure.BatchProcess/ # 批处理基础设施
│   │   ├── Atlas.Infrastructure.ExternalConnectors/ # 外部连接器基础设施
│   │   ├── Atlas.Infrastructure.LogicFlow/ # 逻辑流基础设施
│   │   ├── Atlas.Connectors.Core/   # 连接器核心
│   │   ├── Atlas.Connectors.DingTalk/ # 钉钉连接器
│   │   ├── Atlas.Connectors.Feishu/ # 飞书连接器
│   │   ├── Atlas.Connectors.WeCom/  # 企业微信连接器
│   │   ├── Atlas.Application.AgentTeam/ # 智能体团队
│   │   ├── Atlas.Application.Alert/ # 告警模块
│   │   ├── Atlas.Application.Approval/ # 审批流模块
│   │   ├── Atlas.Application.Assets/ # 资产管理模块
│   │   ├── Atlas.Application.Audit/ # 审计模块
│   │   ├── Atlas.Application.Workflow/ # 工作流应用
│   │   ├── Atlas.Application.LogicFlow/ # 逻辑流
│   │   ├── Atlas.Application.LowCode/ # 低代码应用
│   │   ├── Atlas.Application.Microflows/ # 微流引擎应用
│   │   ├── Atlas.Application.BatchProcess/ # 批处理应用
│   │   ├── Atlas.Application.ExternalConnectors/ # 外部连接器应用
│   │   ├── Atlas.Domain.AgentTeam/  # 智能体团队领域
│   │   ├── Atlas.Domain.Alert/      # 告警领域
│   │   ├── Atlas.Domain.Approval/   # 审批流领域
│   │   ├── Atlas.Domain.Assets/     # 资产领域
│   │   ├── Atlas.Domain.Audit/      # 审计领域
│   │   ├── Atlas.Domain.Workflow/   # 工作流领域
│   │   ├── Atlas.Domain.LogicFlow/  # 逻辑流领域
│   │   ├── Atlas.Domain.LowCode/    # 低代码领域
│   │   ├── Atlas.Domain.Microflows/ # 微流领域
│   │   ├── Atlas.Domain.BatchProcess/ # 批处理领域
│   │   └── Atlas.Domain.ExternalConnectors/ # 外部连接器领域
│   │
│   ├── frontend/                    # 前端 monorepo
│   │   ├── apps/
│   │   │   ├── app-web/             # 主Web应用 (React + Rsbuild)
│   │   │   └── lowcode-mini-host/   # 低代码微型宿主
│   │   ├── packages/                # 共享包
│   │   │   ├── arch/                # 架构基础包 (api/http/hooks/i18n/logger/utils)
│   │   │   ├── common/              # 公共包 (assets/auth)
│   │   │   └── workflow/            # 工作流包 (base/sdk)
│   │   ├── e2e/                     # E2E测试用例
│   │   ├── config/                  # 共享构建配置 (eslint/rsbuild/tailwind/ts/vitest)
│   │   └── infra/                   # 基础设施工具 (eslint-plugin/fs-enhance/idl)
│   │
│   └── coze-workflow-host/          # Coze工作流独立宿主
│
├── docs/                            # 项目文档与规格
│   ├── contracts.md                 # 接口契约
│   ├── microflow/                   # 微流引擎文档
│   ├── lowcode-mendix-mvp/          # Mendix低代码MVP文档
│   └── coze/                        # Coze相关文档
│
├── scripts/                         # 运维与开发脚本
├── tests/                           # 后端测试项目
└── tools/                           # 工具集
```

---

## 四、后端架构详解

### 4.1 核心层 (Atlas.Core)

基础抽象与跨层共享类型，**零外部依赖**，被所有其他项目引用。

| 路径 | 类型 | 说明 |
|------|------|------|
| `Models/ApiResponse.cs` | `ApiResponse<T>` record | 统一API响应格式，包含 `Success/Code/Message/TraceId/Data`，提供 `Ok()` 和 `Fail()` 工厂方法 |
| `Models/ErrorCodes.cs` | 静态常量类 | 系统错误码定义（Success、ValidationError、NotFound、Unauthorized 等） |
| `Models/PagedRequest.cs` | class | 分页请求基类（PageIndex、PageSize、Keyword） |
| `Models/PagedResult.cs` | class | 分页结果封装（Items、Total、PageIndex、PageSize） |
| `Tenancy/TenantId.cs` | `TenantId` readonly record struct | 多租户ID值对象，封装 `Guid`，支持空值判断和隐式类型转换 |
| `Enums/DataScopeType.cs` | enum | 数据权限范围：All/CurrentTenant/CustomDept/CurrentDept/CurrentDeptAndBelow/OnlySelf/Project（等保2.0最小化授权） |
| `Events/IDomainEvent.cs` | interface | 领域事件接口 |
| `Events/IEventBus.cs` | interface | 事件总线接口 |
| `Plugins/INodeSpi.cs` | interface | 节点SPI插件接口 |
| `Setup/AppSetupState.cs` | class | 应用初始化状态管理 |
| `Setup/SetupState.cs` | class | 平台初始化状态管理 |

### 4.2 领域层 (Atlas.Domain)

#### 平台级身份与权限实体 (Atlas.Domain/Identity/Entities/)

| 实体 | 基类 | 核心属性 | 说明 |
|------|------|---------|------|
| `UserAccount` | `TenantEntity` | Username, PasswordHash, DisplayName, Email, PhoneNumber, IsActive, FailedLoginCount | 用户账户实体 |
| `Role` | `TenantEntity` | Name, Code, Description, IsSystem, DataScope | 角色定义，支持数据权限范围 |
| `Permission` | `TenantEntity` | Code, Name, Description, GroupName | 权限点定义 |
| `Menu` | `TenantEntity` | Name, Code, Path, Icon, ParentId, SortOrder | 菜单定义（树形结构） |
| `Department` | `TenantEntity` | Name, Code, ParentId, SortOrder | 部门定义 |
| `Position` | `TenantEntity` | Name, Code, Description, IsActive | 职位定义 |
| `Project` | `TenantEntity` | Code, Name, Description, Status, SortOrder | 项目定义 |
| `UserRole` | `TenantEntity` | UserId, RoleId | 用户-角色关联 |
| `UserPosition` | `TenantEntity` | UserId, PositionId | 用户-职位关联 |
| `UserDepartment` | `TenantEntity` | UserId, DepartmentId | 用户-部门关联 |
| `RoleMenu` | `TenantEntity` | RoleId, MenuId | 角色-菜单关联 |
| `ProjectUser` | `TenantEntity` | ProjectId, UserId | 项目-用户关联 |
| `ProjectDepartment` | `TenantEntity` | ProjectId, DepartmentId | 项目-部门关联 |

#### 平台级应用组织实体 (Atlas.Domain/Platform/Entities/)

| 实体 | 说明 |
|------|------|
| `AppDepartment` | 应用级部门（与平台Department隔离） |
| `AppPosition` | 应用级职位（与平台Position隔离） |
| `AppProject` | 应用级项目（与平台Project隔离） |
| `AppProjectUser` | 应用成员-项目关联 |

#### 业务模块领域实体

| 模块 | 核心实体 |
|------|---------|
| **Alert** (告警) | `AlertRecord` — 告警记录 |
| **Approval** (审批) | `ApprovalTask`、`ApprovalTimerJob` — 审批任务与定时任务 |
| **Assets** (资产) | `Asset` — 资产台账 |
| **Audit** (审计) | `AuditRecord` — 审计日志 |
| **Workflow** (工作流) | `PersistedEvent` — 持久化工作流事件 |
| **AgentTeam** (智能体团队) | Agent团队相关实体 |
| **LowCode** (低代码) | 低代码资产、组件、发布相关实体 |
| **Microflows** (微流) | 微流定义、执行记录、变量存储 |
| **LogicFlow** (逻辑流) | `FlowExecution`、`FlowNodeBinding` — 流执行与节点绑定 |
| **BatchProcess** (批处理) | `BatchJobStatus` 枚举 — 批任务状态 |

### 4.3 应用服务层 (Atlas.Application)

#### 平台级服务接口 (Atlas.Application/AppHost/)

| 接口 | 说明 |
|------|------|
| `IPlatformServices` | 平台级核心服务（用户/角色/部门/职位/项目CRUD） |
| `IAppServices` | 应用级核心服务 |
| `IPlatformRepositories` | 平台级仓储访问接口 |
| `IAppRepositories` | 应用级仓储访问接口 |

#### 业务模块服务

| 模块 | 关键服务 |
|------|---------|
| **Identity** | `IAuthTokenService` — JWT令牌签发与刷新; `IUserCommandService` — 用户命令; `IUserAccountRepository` — 用户仓储; `IAuthProfileService` — 用户画像 |
| **Alert** | Alert相关验证器、DTO映射 |
| **Approval** | Approval流程定义/任务处理服务、种子数据 |
| **Assets** | Asset CRUD服务与验证 |
| **Audit** | AuditRecord记录与查询服务 |
| **Workflow** | 工作流发布、事件管理 |
| **AgentTeam** | Agent团队CRUD |
| **LowCode** | 低代码发布预览、资产GC、协同编辑 |
| **Microflows** | 微流执行器、调试器、变量存储、步骤调度 |
| **LogicFlow** | 逻辑流编排与执行 |
| **ExternalConnectors** | 外部连接器手动绑定 |

### 4.4 基础设施层 (Atlas.Infrastructure)

| 组件 | 说明 |
|------|------|
| **Repositories** | 仓储实现（基于SqlSugar），含 `ProjectRepository`、`UserRepository` 等 |
| **Services/AtlasOrmSchemaCatalog** | 统一ORM Schema目录，运行时自愈建表 |
| **Services/WorkflowHostedService** | WorkflowCore引擎托管后台服务 |
| **Services/LowCode/** | 低代码资产GC调度(`LowCodeAssetGcSchedulerHostedService`)、协同快照(`LowCodeCollabSnapshotSchedulerHostedService`)、Cron触发器(`LowCodeTriggerReconcileHostedService`)、OTel插桩(`LowCodeOtelInstrumentation`) |
| **Cache** | 内存缓存服务 |
| **EventBus** | 事件总线实现 |

### 4.5 表现层 (Atlas.Presentation.Shared)

| 组件 | 说明 |
|------|------|
| **Controllers** | 共享API控制器（如 `Ai/AgentsController.cs`、`Identity/UsersControllerBase.cs`） |
| **Middlewares** | `ExceptionHandlingMiddleware`、`ClientContextMiddleware`、`TenantContextMiddleware`、`AppContextMiddleware`、`ApiVersionRewriteMiddleware` |
| **Authorization** | `PermissionPolicyProvider` — 动态权限策略; `PermissionAuthorizationHandler` — 权限校验; `ApiAuthorizationMiddlewareResultHandler` — 401/403统一处理; `PatAuthenticationHandler` — Personal Access Token认证 |
| **Security** | `SensitiveObjectConverterFactory` — 敏感数据脱敏; `XssOptions` — XSS防护 |
| **Hubs** | `NotificationHub` — 实时通知; `LowCodePreviewHub` — 低代码预览推送 |
| **Json** | `FlexibleLongJsonConverter` — 灵活长整型序列化 |
| **Tenancy** | `TenancyOptions` — 租户配置（Header名称等） |

### 4.6 宿主层 (Atlas.AppHost)

统一后端入口，端口默认 `5002`。

#### Program.cs 启动流程

1. **配置加载**: 从 `ATLAS_PLATFORM_CONFIG_ROOT` 或默认内容根目录加载 `appsettings.json`、`appsettings.{Environment}.json`、`appsettings.runtime.json`
2. **数据库配置**: 如未提供 `Database:ConnectionString`，默认使用 `atlas.db` (SQLite)
3. **Options注册**: Jwt、Security、Xss、FileStorage、PasswordPolicy、LockoutPolicy、BootstrapAdmin、ApprovalSeedData、Tenancy、TableViewDefaults、App
4. **MVC注册**: 控制器 + System.Text.Json自定义转换器 + NSwag OpenAPI文档
5. **基础设施注册**: HealthChecks、MemoryCache、HttpContextAccessor、CORS、SignalR
6. **本地化**: 支持 zh-CN / en-US
7. **FluentValidation**: 从所有模块程序集注册验证器
8. **认证授权**: JWT Bearer + Certificate + PAT(个人访问令牌) + OpenProject认证方案
9. **中间件管道**: ExceptionHandling → AppSetupMode → SetupConsoleAudit → Swagger(Dev) → CORS → ResponseCompression → RequestLocalization → ClientContext → WebSocket → Routing → Auth → App/Tenant/ApiVersion中间件
10. **端点映射**: HealthChecks、根信息、Controllers、SignalR Hubs
11. **Hangfire**: 仅客户端注册（SQLite存储），不在AppHost内运行Server

#### 控制器一览 (Atlas.AppHost/Controllers/)

| 控制器 | 路由前缀 | 功能 |
|--------|---------|------|
| `AuthController` | `/api/v1/auth` | 登录/令牌/刷新/登出/个人信息/密码修改/验证码 |
| `UsersController` | `/api/v1/users` | 用户CRUD |
| `AgentTeamsController` | `/api/v1/agent-teams` | 智能体团队管理 |
| `AiAppsController` | `/api/v1/ai-apps` | AI应用管理 |
| 其他 | `/api/v1/*` | 审批/资产/告警/审计/工作流/低代码/微流/外部连接器 等 |

### 4.7 工作流引擎 (Atlas.WorkflowCore)

基于 WorkflowCore 开源框架封装的流程引擎：

| 组件 | 说明 |
|------|------|
| `Atlas.WorkflowCore` | 工作流核心：定义、执行器、持久化 |
| `Atlas.WorkflowCore.DSL` | 工作流DSL解析（支持命名空间注册，如 `Atlas.WorkflowCore.Primitives`） |
| `WorkflowTracing` | OTel ActivitySource 集成，追踪工作流/步骤/结果 |

### 4.8 外部连接器 (Atlas.Connectors.*)

| 连接器 | 说明 |
|--------|------|
| `Atlas.Connectors.Core` | 连接器核心抽象 |
| `Atlas.Connectors.DingTalk` | 钉钉集成 |
| `Atlas.Connectors.Feishu` | 飞书集成 |
| `Atlas.Connectors.WeCom` | 企业微信集成 |

---

## 五、前端架构详解

### 5.1 Monorepo 结构

前端采用 **pnpm workspace monorepo** 管理，根目录 `src/frontend/`。

```
src/frontend/
├── apps/
│   ├── app-web/          # 主Web应用 ← 主要入口
│   └── lowcode-mini-host/ # 低代码微型宿主
├── packages/             # 共享包
│   ├── arch/             # 架构基础
│   │   ├── bot-api       # API调用封装
│   │   ├── bot-env       # 环境变量
│   │   ├── bot-http      # HTTP请求
│   │   ├── hooks         # 通用React Hooks
│   │   ├── i18n          # 国际化
│   │   ├── logger        # 日志
│   │   ├── tea           # 基础组件
│   │   └── utils         # 工具函数
│   ├── common/
│   │   ├── assets        # 公共资源
│   │   └── auth          # 认证模块
│   └── workflow/
│       ├── base          # 工作流基础
│       └── sdk           # 工作流SDK
├── config/               # 共享构建配置
├── e2e/                  # Playwright E2E测试
├── infra/                # 基础设施工具
└── scripts/              # 前端脚本
```

### 5.2 主应用 (app-web) 架构

**技术栈**: React 18 + TypeScript + Rsbuild + React Router v6 + Zustand + Semi Design

#### 入口与启动流程

```
main.tsx
  └── AppRoot (app.tsx)
       └── AppI18nProvider        # 国际化上下文
            └── AppErrorBoundary  # 全局错误边界
                 └── BootstrapProvider  # 应用启动引导
                      └── AuthProvider  # 认证上下文
                           └── AppStartupKernel  # 启动内核
                                └── AppRouter   # 路由系统
```

#### 核心上下文 (Context Providers)

| Provider | 职责 |
|----------|------|
| `AppI18nProvider` | 国际化 (zh-CN/en-US) |
| `BootstrapProvider` | 应用引导：加载 appKey、spaceId、appReady 状态 |
| `AuthProvider` | 认证状态：登录、令牌管理、权限检查、用户画像 |
| `OrganizationProvider` | 组织上下文 (orgId) |
| `PermissionProvider` | 权限校验 |
| `WorkspaceProvider` | 当前工作区上下文 |

#### 路由结构 (appRoutes)

```text
/                                   → HomePage (根入口)
/platform-not-ready                 → PlatformNotReadyPage
/app-setup                          → AppSetupPage
/setup-console[/:tab]              → SetupConsolePage (平台初始化控制台)
/sign                               → LoginPage (登录页)

/space                              → 工作区列表入口
/space/:space_id                    → SpaceShellLayout (工作区外壳布局)
  /home                             → WorkspaceHomePage
  /library                          → WorkspaceLibraryPage
  /projects[/folder/:folderId]      → WorkspaceProjectsPage
  /resources[/:type]                → WorkspaceResourcesRedirect
  /tasks[/:taskId]                  → WorkspaceTasksPage
  /evaluations[/:evaluationId]      → WorkspaceEvaluationsPage
  /manage[/:tab]                    → WorkspaceManageRoute (用户/角色/部门/职位/审批/报表/仪表盘/可视化)
  /settings/:tab                    → WorkspaceSettingsRoute (成员/权限/发布/模型/系统)
  /agents/:id[/publish]             → Agent详情/发布
  /workflows[/:workflowId]          → 工作流工作台 (CozeWorkflowPage)
  /chatflows[/:workflowId]          → Chatflow工作台
  /mendix-studio[/:appId]           → Mendix Studio
  /bot/:bot_id                      → Coze Agent编辑器 (CozeAgentLayout)
  /plugin/:id[/tool/:toolId]        → 插件详情
  /knowledge/:id[/upload]           → 知识库详情/上传
  /knowledge-bases[/new][/jobs][/provider-configs] → 知识库管理
  /database/:id                     → 数据库详情

/me                                 → 个人中心
  /profile                          → MeProfilePage
  /settings/:tab                    → MeSettingsPage
  /notifications                    → MeNotificationsPage

/market                             → 市场
  /templates                        → MarketTemplatesPage
  /plugins                          → MarketPluginsPage

/community/works                    → CommunityWorksPage
/open/api                           → OpenApiPage
/docs[/:slug]                       → DocsPage

/explore/plugin[/:productId]        → 探索-插件
/explore/template[/:templateId]     → 探索-模板
/search/:word                       → 探索搜索

/agent/:agentId/editor              → AgentEditorRoute
/agent/:agentId/publish             → AgentPublishRoute
/app/:projectId/editor              → AppEditorRoute
/app/:projectId/publish             → AppPublishRoute
/workflow/:workflowId/editor        → WorkflowEditorRoute
/chatflow/:chatflowId/editor        → ChatflowEditorRoute
/microflow[/:microflowId/editor]    → MicroflowDemoPage / MicroflowEditorPage

/apps/:appKey/studio/*              → AppShellRoute (应用级Studio路由)
/apps/lowcode/:id/studio            → CanonicalLowcodeStudioRoute (低代码Studio)
/forbidden                          → ForbiddenPage (403)
/*                                  → 重定向到 /
```

#### API 服务层 (services/)

前端 API 层采用模块化设计，所有请求通过统一的 `api-core.ts` 发出：

| 服务文件 | 对应功能 |
|---------|---------|
| `api-core.ts` | 核心请求封装（requestApi、getConfiguredAppKey、setUnauthorizedHandler） |
| `api-auth.ts` | 认证相关（登录/登出/刷新令牌） |
| `api-admin.ts` | 管理后台（用户/角色/部门/职位CRUD） |
| `api-workflow.ts` | 工作流（创建/列表/保存草稿） |
| `api-ai-app.ts` | AI应用（CRUD/发布/预览/构建器配置） |
| `api-ai-assistant.ts` | AI助手（CRUD/发布/嵌入令牌） |
| `api-ai-database.ts` | AI数据库（CRUD/导入/渠道配置） |
| `api-ai-variable.ts` | AI变量（CRUD/系统变量） |
| `api-ai-workspace.ts` | AI工作区（库资源导入导出/移动） |
| `api-explore.ts` | 探索（插件/模板/市场/搜索） |
| `api-knowledge.ts` | 知识库（文档/切片/检索/版本/绑定/权限/任务） |
| `api-conversation.ts` | 对话（会话管理/消息/流式Agent） |
| `api-model-config.ts` | 模型配置（CRUD/连接测试/Prompt测试） |
| `api-org-management.ts` | 组织管理（概览） |
| `api-org-workspaces.ts` | 组织工作区（成员/资源/权限） |
| `api-publish-channels.ts` | 发布渠道 |
| `api-reports.ts` | 报表/仪表盘 |
| `api-visualization.ts` | 可视化 |
| `api-workspace-ide.ts` | IDE工作区（摘要/仪表盘/活动/收藏） |
| `api-db-maintenance.ts` | 数据库维护（备份/连接测试/信息） |
| `app-instance-context.ts` | 应用实例上下文解析 |

#### 页面与组件

| 页面/组件 | 说明 |
|-----------|------|
| `HomePage` | 根入口页面，根据认证状态重定向 |
| `LoginPage` | 登录页，支持验证码、多租户 |
| `EntryGatewayPage` | 入口网关，分发不同入口 |
| `WorkspaceHomePage` | 工作区首页/仪表盘 |
| `WorkspaceProjectsPage` | 项目/资源列表（文件夹树） |
| `WorkspaceLibraryPage` | 资源库页面 |
| `WorkspaceSettingsPage` | 工作区设置（成员/权限管理） |
| `WorkspaceManageRoute` | 管理路由分发（用户/角色/部门/职位/审批等） |
| `WorkspaceSwitcher` | 工作区切换器 |
| `GlobalCreateModal` | 全局创建弹窗（应用/Agent/工作流等） |
| `EditorShellLayout` | 编辑器通用布局 |
| `SpaceShellLayout` | 工作区外壳布局（含侧边导航） |
| `PlatformShellLayout` | 平台级外壳布局 |
| `CozeWorkspaceConsolePage` | Coze工作区控制台 |
| `SetupConsolePage` | 平台初始化控制台 |
| `MicroflowEditorPage` | 微流编辑器 |
| `MendixStudioAppRoute` | Mendix Studio路由 |

---

## 六、核心模块职责汇总

### 6.1 身份与权限模块

- **功能**: 用户账户管理、角色(RBAC)、权限点、菜单树、部门/职位/项目组织架构
- **后端**: `Atlas.Domain.Identity` + `Atlas.Application` Identity服务
- **前端**: `api-admin.ts` + `AdminUsersRoute` / `AdminRolesRoute` 等
- **特色**: 等保2.0数据权限范围(DataScopeType)、账户锁定策略、密码策略、验证码阈值

### 6.2 审批流模块

- **功能**: 审批流程定义、发起实例、任务处理(同意/驳回/转交)、抄送、催办
- **后端**: `Atlas.Domain.Approval` + `Atlas.Application.Approval`
- **前端**: `api-approval.ts` + `ApprovalAdminPage`
- **特色**: 定时任务(`ApprovalTimerJob`)、种子数据

### 6.3 资产管理模块

- **功能**: 资产台账管理（增删改查）
- **后端**: `Atlas.Domain.Assets` + `Atlas.Application.Assets`
- **前端**: `api-admin.ts` 集成

### 6.4 审计模块

- **功能**: 关键操作审计记录、审计日志查询
- **后端**: `Atlas.Domain.Audit` + `Atlas.Application.Audit`
- **前端**: 集成在管理后台

### 6.5 工作流引擎

- **功能**: 可视化工作流定义、WorkflowCore执行引擎、DSL解析、事件持久化
- **后端**: `Atlas.WorkflowCore` + `Atlas.WorkflowCore.DSL`
- **前端**: `CozeWorkflowPage` (Coze工作流Playground适配) + 工作流编辑器
- **特色**: OTel全链路追踪、Workflow/Chatflow双模式

### 6.6 AI智能体团队

- **功能**: Agent团队创建与管理
- **后端**: `Atlas.Domain.AgentTeam` + `Atlas.Application.AgentTeam`
- **前端**: `AgentTeamsController` → API

### 6.7 低代码平台

- **功能**: 低代码应用Studio、可视化搭建、发布管理、资产GC、协同编辑、Cron触发器
- **后端**: `Atlas.Domain.LowCode` + `Atlas.Application.LowCode` + 3个HostedService
- **前端**: `CanonicalLowcodeStudioRoute` + `MendixStudioAppRoute`
- **特色**: 预览Hub(SignalR推送)、离线快照、OTel Metrics埋点

### 6.8 微流引擎 (Microflow)

- **功能**: 微流定义与执行、步骤调试、变量存储、条件分支、循环、并行网关
- **后端**: `Atlas.Domain.Microflows` + `Atlas.Application.Microflows`
- **前端**: `MicroflowEditorPage` + `MicroflowDemoPage`
- **特色**: WebSocket实时推送、调试断点、执行追踪

### 6.9 逻辑流

- **功能**: 逻辑流编排、节点绑定、流执行
- **后端**: `Atlas.Domain.LogicFlow` + `Atlas.Application.LogicFlow`

### 6.10 外部连接器

- **功能**: 对接钉钉/飞书/企业微信、手动绑定管理
- **后端**: `Atlas.Connectors.*` + `Atlas.Application.ExternalConnectors`
- **前端**: `ConnectorsPage` / `ConnectorDetailPage`

### 6.11 批处理

- **功能**: 批量任务处理（异步导入/批量操作）
- **后端**: `Atlas.Domain.BatchProcess` + `Atlas.Application.BatchProcess`

---

## 七、依赖关系图

### 7.1 后端项目依赖（简化）

```
Atlas.AppHost
  ├── Atlas.AppHost.Sdk
  ├── Atlas.Core
  ├── Atlas.Domain / Atlas.Domain.*
  ├── Atlas.Application / Atlas.Application.*
  ├── Atlas.Infrastructure / Atlas.Infrastructure.*
  ├── Atlas.Presentation.Shared
  ├── Atlas.Shared.Contracts
  ├── Atlas.WorkflowCore / Atlas.WorkflowCore.DSL
  ├── Atlas.Sdk.ConnectorPlugins / Atlas.Sdk.LogicFlowPlugins
  └── Atlas.Connectors.Core / Atlas.Connectors.*

Atlas.Application.* → Atlas.Domain.* → Atlas.Core
Atlas.Infrastructure.* → Atlas.Domain.* / Atlas.Core
Atlas.Presentation.Shared → Atlas.Application / Atlas.Core
```

### 7.2 主要NuGet包依赖

| 包名 | 版本 | 用途 |
|------|------|------|
| `FluentValidation.AspNetCore` | 11.3.1 | 请求验证 |
| `Hangfire.AspNetCore` + `Hangfire.Storage.SQLite` | 1.8.23 | 后台任务调度 |
| `Microsoft.AspNetCore.Authentication.JwtBearer` | 10.0.2 | JWT认证 |
| `Microsoft.AspNetCore.Authentication.Certificate` | 10.0.2 | 证书认证 |
| `Microsoft.AspNetCore.Authentication.OpenIdConnect` | 10.0.3 | OIDC认证 |
| `NLog.Web.AspNetCore` | 5.3.11 | 结构化日志 |
| `NSwag.AspNetCore` | 14.6.3 | Swagger/OpenAPI |
| `OpenTelemetry.*` | 1.15.x | 可观测性(Trace/Metrics) |
| `AutoMapper` | 14.0.0 | 对象映射 |
| `SqlSugar` | - | ORM |
| `WorkflowCore` | - | 工作流引擎 |

### 7.3 前端依赖关系

```
app-web
  ├── @atlas/app-shell-shared        # 应用外壳共享
  ├── @atlas/shared-react-core       # 共享React核心
  ├── @atlas/module-admin-react      # 管理模块
  ├── @atlas/module-studio-react     # Studio模块
  ├── @atlas/module-explore-react    # 探索模块
  ├── @atlas/library-module-react    # 知识库模块
  ├── @atlas/lowcode-studio-react    # 低代码Studio
  ├── @atlas/mendix-studio-core      # Mendix Studio核心
  ├── @atlas/external-connectors-react # 外部连接器
  ├── @coze-arch/bot-api             # Coze API层
  ├── @coze-arch/bot-monaco-editor   # Monaco编辑器
  ├── @coze-arch/bot-space-api       # Space API
  ├── @coze-arch/coze-design         # Coze设计系统
  ├── @coze-foundation/*             # Coze基础层
  ├── @coze-studio/workspace-adapter # Coze Studio适配器
  ├── @coze-workflow/playground-adapter # Coze工作流适配器
  ├── @coze-agent-ide/*              # Coze Agent IDE
  ├── @douyinfe/semi-ui ^2.82.0      # Semi Design UI
  ├── react ^18.2.0                  # React
  ├── react-router-dom ^6.30.1       # 路由
  └── zustand ^4.4.7                 # 状态管理
```

---

## 八、项目运行方式

### 8.1 环境要求

- **.NET SDK**: 10.0+
- **Node.js**: 18+ (推荐 20+)
- **pnpm**: 10.0+ (推荐 10.33.0)
- **操作系统**: Windows / Linux / macOS

### 8.2 后端启动

```powershell
# 还原依赖
dotnet restore Atlas.SecurityPlatform.slnx

# 构建
dotnet build Atlas.SecurityPlatform.slnx

# 运行 (默认端口 5002)
dotnet run --project src/backend/Atlas.AppHost
```

**配置环境变量**（初始化管理员）:

```powershell
$env:Security__BootstrapAdmin__Enabled="true"
$env:Security__BootstrapAdmin__TenantId="00000000-0000-0000-0000-000000000001"
$env:Security__BootstrapAdmin__Username="admin"
$env:Security__BootstrapAdmin__Password="P@ssw0rd!"
$env:Security__BootstrapAdmin__Roles="Admin"
```

### 8.3 前端启动

```powershell
cd src/frontend

# 安装依赖
pnpm install

# 启动开发服务器 (app-web)
pnpm run dev:app-web

# 构建生产版本
pnpm run build:app-web
```

### 8.4 联调启动

```powershell
# 一键启动 AppHost + AppWeb
powershell -ExecutionPolicy Bypass -File .\scripts\dev-start-app-direct.ps1
```

### 8.5 测试运行

```powershell
# 后端单元测试
dotnet test tests/Atlas.AppHost.Tests
dotnet test tests/Atlas.SecurityPlatform.Tests
dotnet test tests/Atlas.WorkflowCore.Tests

# 前端单元测试
cd src/frontend
pnpm run test:unit

# E2E 测试 (Playwright)
pnpm run test:e2e:app          # 全量
pnpm run test:e2e:app:only     # 仅app配置
pnpm run test:e2e:app:headed   # 有头模式
pnpm run test:e2e:app:smoke    # 冒烟测试
```

### 8.6 Docker部署

```bash
# 准备环境变量
cp .env.example .env
# 编辑 .env 配置 JWT_SIGNING_KEY / BOOTSTRAP_ADMIN_PASSWORD / CORS_ALLOWED_ORIGIN

# 准备TLS证书
# /opt/atlas/certs/server.crt
# /opt/atlas/certs/server.key

# 部署
IMAGE_TAG=<tag> JWT_SIGNING_KEY=<...> BOOTSTRAP_ADMIN_PASSWORD=<...> bash deploy/scripts/deploy.sh

# 健康检查
curl http://localhost:5002/api/v1/health

# 回滚
bash deploy/scripts/rollback.sh
```

---

## 九、配置说明

### 9.1 关键配置项 (appsettings.json / 环境变量)

| 配置路径 | 说明 |
|----------|------|
| `Jwt:SigningKey` | JWT签名密钥（生产环境≥32字符） |
| `Jwt:Issuer` | JWT签发者 |
| `Jwt:Audience` | JWT受众 |
| `Security:BootstrapAdmin:Enabled` | 是否启用Bootstrap管理员 |
| `Security:BootstrapAdmin:Username` | 管理员用户名 |
| `Security:BootstrapAdmin:Password` | 管理员密码 |
| `Security:PasswordPolicy:*` | 密码策略配置 |
| `Security:LockoutPolicy:*` | 账户锁定策略 |
| `Security:CaptchaThreshold` | 验证码触发阈值（失败次数） |
| `Security:EnforceHttps` | 是否强制HTTPS |
| `Cors:AllowedOrigins` | CORS白名单 |
| `Database:ConnectionString` | 数据库连接串（默认SQLite） |
| `ATLAS_PLATFORM_CONFIG_ROOT` | 平台配置根目录 |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | OTel导出端点 |

### 9.2 构建配置 (Directory.Build.props)

- `Nullable`: enable
- `ImplicitUsings`: enable
- `TreatWarningsAsErrors`: true
- `AnalysisLevel`: latest
- 压制警告: `NU1608`(AutoMapper版本兼容)、`NU1903`(AutoMapper安全公告，风险可控)

---

## 十、开发规范（摘要）

> 详见 `AGENTS.md`

### 变更闭环规则

每个任务必须完成：
1. **自审**: 需求满足、回归风险、权限安全
2. **验证**: build → test → API实测 → E2E
3. **规则对照**: 对照AGENTS.md + 文档契约

### 重启规则

- 后端改动 (Controller/Service/Repository/Entity/DI/appsettings/migration) → 必须重启 AppHost
- 前端改动 (vite.config/.env/package.json/路由入口) → 必须重启前端
- 普通组件改动 → 可依赖HMR

### 禁止行为

1. 改后端不重启就页面验证
2. 端口未重启继续跑测试
3. E2E失败不读 `error-context.md`
4. 改断言代替修复
5. 新增桥接层承载主链路业务逻辑

---

## 十一、关键类与函数速查

### 后端

| 类/接口 | 位置 | 说明 |
|---------|------|------|
| `Program.cs` | `Atlas.AppHost/Program.cs` | 应用启动入口，全部DI注册和中间件配置 |
| `ApiResponse<T>` | `Atlas.Core/Models/ApiResponse.cs` | 统一API响应格式 |
| `TenantId` | `Atlas.Core/Tenancy/TenantId.cs` | 多租户ID值对象 |
| `DataScopeType` | `Atlas.Core/Enums/DataScopeType.cs` | 数据权限范围枚举 |
| `AuthController` | `Atlas.AppHost/Controllers/AuthController.cs` | 认证API: Token/Refresh/Me/Logout/Captcha |
| `AgentTeamsController` | `Atlas.AppHost/Controllers/AgentTeamsController.cs` | Agent团队API |
| `UsersController` | `Atlas.AppHost/Controllers/UsersController.cs` | 用户管理API |
| `PermissionPolicyProvider` | `Atlas.Presentation.Shared/Authorization/` | 动态权限策略提供者 |
| `PatAuthenticationHandler` | `Atlas.Presentation.Shared/Authorization/` | PAT(个人访问令牌)认证处理器 |
| `ExceptionHandlingMiddleware` | `Atlas.Presentation.Shared/Middlewares/` | 全局异常处理中间件 |
| `AppSetupModeMiddleware` | `Atlas.AppHost/Middleware/` | 初始化模式门禁中间件 |
| `AtlasOrmSchemaCatalog` | `Atlas.Infrastructure/Services/` | ORM Schema自愈 |
| `LowCodeOtelInstrumentation` | `Atlas.Infrastructure/Services/LowCode/` | 低代码OTel插桩 |
| `WorkflowTracing` | `Atlas.WorkflowCore/Services/` | 工作流OTel追踪 |

### 前端

| 文件/组件 | 位置 | 说明 |
|-----------|------|------|
| `app.tsx` | `apps/app-web/src/app/app.tsx` | 前端根组件、路由定义、API适配器 |
| `api-core.ts` | `apps/app-web/src/services/api-core.ts` | 统一请求封装 |
| `auth-context.ts` | `apps/app-web/src/app/auth-context.ts` | 认证上下文 |
| `workspace-context.ts` | `apps/app-web/src/app/workspace-context.ts` | 工作区上下文 |
| `workspace-shell.tsx` | `apps/app-web/src/app/layouts/workspace-shell.tsx` | 工作区布局 |
| `editor-shell.tsx` | `apps/app-web/src/app/layouts/editor-shell.tsx` | 编辑器布局 |
| `workspace-switcher.tsx` | `apps/app-web/src/app/components/workspace-switcher.tsx` | 工作区切换 |
| `api-auth.ts` | `apps/app-web/src/app/services/api-auth.ts` | 认证API |
| `api-admin.ts` | `apps/app-web/src/app/services/api-admin.ts` | 管理API |
| `api-workflow.ts` | `apps/app-web/src/app/services/api-workflow.ts` | 工作流API |
| `api-knowledge.ts` | `apps/app-web/src/app/services/api-knowledge.ts` | 知识库API |
| `api-explore.ts` | `apps/app-web/src/app/services/api-explore.ts` | 探索API |
| `route-handles.ts` | `apps/app-web/src/app/route-handles.ts` | 路由元数据定义 |

---

> **文档维护**: 本文档应在项目架构发生重大变更时同步更新。