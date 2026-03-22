# SecurityPlatform 统一组织引擎架构分析与重构方案

## 1. 背景与核心目标

在当前 SecurityPlatform 代码库的组织架构设计中，平台级组织数据（如 `Department`、`Role`、`UserAccount`）与应用级组织数据（如 `AppDepartment`、`AppRole`、`AppMember`）之间存在明显的边界模糊问题。这种模糊主要源于 `LowCodeApp` 实体中引入的共享策略机制（包含 `UseSharedUsers`、`UseSharedRoles`、`UseSharedDepartments` 等开关）。这些开关允许应用实例直接跨越层级，复用平台级的组织数据，导致应用无法形成真正独立、隔离的运行环境，也直接违反了“应用不允许集成平台级别的数据”这一核心系统要求。

为了彻底解决这一问题，本次重构的核心目标分为三个方面。首先，必须强制抹除所有允许应用集成或引用平台级组织数据的相关代码，这包括底层实体字段、服务层逻辑、API 端点以及前端的用户界面。其次，需要设计并实现一个统一的组织引擎，该引擎按作用域（Scope）进行复用，而不是针对每个应用进行特殊定制。最后，必须明确界定平台级、应用级和项目级三层组织范围的职责与权限边界，确保数据流向和权限控制的绝对清晰。

## 2. 三层 Scope 模型设计

为了构建统一且职责分明的组织引擎，我们将系统划分为三个清晰的层级：平台级（Tenant Scope）、应用级（App Scope）和项目级（Project Scope）。每个层级都有其不可逾越的职责边界。

### 2.1 平台级组织（Tenant Scope）

平台级组织是整个租户全局的唯一身份与组织源头。它负责维护全租户的基础员工池（即 `UserAccount` 实体），构建企业的标准行政组织架构树（即 `Department` 实体），并管理跨应用的全局系统角色（例如 `PlatformAdmin` 或 `TenantAdmin`）。

在边界约束方面，平台级组织对于应用级而言是一个绝对的“只读源头”。应用级业务逻辑严禁直接在其内部关系中绑定或引用平台级的 `Department` 或 `Role`。平台级仅负责提供基础的用户身份，而不再直接参与应用内的权限流转。

### 2.2 应用级组织（App Scope）

应用级组织被定位为平台组织在特定应用实例内部的“隔离投影”或“继承副本”。它拥有完全独立的组织模型。应用通过“拉取”或“投影”的方式，将平台用户池中的用户引入为应用内部的成员（即 `AppMember`）。应用内部的任何业务单据或流程，都只能关联 `AppMember.UserId`，而不能直接关联平台的原生用户实体。

此外，应用可以构建完全独立于平台行政架构的虚拟部门树（即 `AppDepartment`），或者在应用初始化时将平台部门作为快照同步到应用内，但同步后两者即刻切断物理联系。应用内独有的角色定义（即 `AppRole`）在计算数据范围（`DataScope`）时，必须且只能针对应用内的 `AppDepartment` 进行过滤。绝对禁止在应用级业务中直接查询平台部门仓储（`IUserDepartmentRepository`），也禁止使用平台角色（`Role`）来控制应用内资源的访问。

### 2.3 项目级范围（Project Scope）

项目级模型不再承担组织主模型的职责，它仅仅作为业务数据的隔离容器与访问边界。它的主要职责是将一组具体的业务数据（如特定表单、任务）圈定在一个特定的项目上下文中，并通过关联表（如 `ProjectUser`）将特定的用户或部门与该项目绑定，从而限制这部分数据的可见性和操作权限。

在新的架构中，项目级不再包含独立的部门树或角色体系。它仅仅作为数据范围过滤器（`DataScopeFilter`）中的一个独立维度（例如 `DataScopeType.Project`）来发挥作用，确保业务数据的访问控制轻量且高效。

## 3. 核心改造点与需抹除代码清单

为了落实上述三层 Scope 模型，我们需要在代码库的各个分层中进行精准的代码抹除与重构。以下表格详细列出了各层需要改造的具体文件、抹除内容及其背后的架构原因。

| 架构分层 | 文件路径 | 抹除或修改的具体内容 | 改造原因与目标 |
| :--- | :--- | :--- | :--- |
| **Domain 实体层** | `Atlas.Domain/LowCode/Entities/LowCodeApp.cs` | 抹除 `UseSharedUsers`、`UseSharedRoles`、`UseSharedDepartments` 属性及 `UpdateSharingPolicy` 方法。 | 强制应用实例保持独立，彻底关闭动态切换平台数据共享策略的后门。 |
| **Domain 实体层** | `Atlas.Domain/Identity/Entities/Permission.cs` | 移除 `appId` discriminator 鉴别器。 | 确保平台权限与应用权限在物理模型上彻底分离，互不干扰。 |
| **Domain 实体层** | `Atlas.Domain/Platform/Entities/AppRole.cs` | 明确 `DeptIds` 字段必须且只能存储 `AppDepartment.Id`，禁止存储平台部门 ID。 | 消除平台级部门与应用级部门在角色数据范围配置中的歧义。 |
| **Application 服务层** | `Atlas.Application/Platform/Abstractions/IPlatformServices.cs` | 删除 `UpdateSharingPolicyAsync` 和 `GetSharingPolicyAsync` 接口定义。 | 共享策略机制被废弃，相关对外暴露的服务接口必须同步移除。 |
| **Application 服务层** | `Atlas.Application/Platform/Services/TenantAppMemberCommandService.cs` | 修改 `AddMembersAsync` 方法，移除对平台 `UserAccount` 的直接依赖验证。 | 解耦应用层服务对平台级数据模型的直接强依赖，改用统一投影服务。 |
| **Infrastructure 实现层** | `Atlas.Infrastructure/Services/Platform/TenantAppMembershipServices.cs` | 删除所有 `EnsureDedicatedUsers(app)` 和 `EnsureDedicatedRoles(app)` 的守卫调用及方法定义。 | 架构默认全部应用为隔离副本（Dedicated），无需再进行分支判断。 |
| **Infrastructure 实现层** | `Atlas.Infrastructure/Services/DataScopeFilter.cs` | 重构数据范围过滤逻辑，拆分为 `TenantDataScopeFilter` 和 `AppDataScopeFilter`。 | 当前逻辑强耦合平台部门，必须确保应用级查询只能基于 `AppDepartment` 计算。 |
| **Infrastructure 实现层** | `Atlas.Infrastructure/Services/ApprovalFlow/ApprovalUserService.cs` | 修改审批流中查找部门负责人的逻辑，使其具备上下文感知能力。 | 避免应用级审批流强制回退绑定到平台组织树，确保应用独立组织在审批中生效。 |
| **WebApi 控制器层** | `Atlas.WebApi/Controllers/TenantAppInstancesV2Controller.cs` | 删除 `GetSharingPolicy` 和 `UpdateSharingPolicy` 端点。 | 关闭前端或外部系统配置应用共享策略的 HTTP API 入口。 |
| **WebApi 控制器层** | `Atlas.WebApi/Middlewares/AppMembershipMiddleware.cs` | 删除 `if (app.UseSharedUsers)` 的判断分支逻辑。 | 强制所有进入应用的请求都必须经过应用独立的成员鉴权体系。 |
| **前端页面层** | `AppSettingsPage.vue` | 移除 `sharingPolicy` 相关的 UI 控件（三个 Switch 开关）及其保存逻辑。 | 前端界面不再向用户提供复用平台组织数据的配置选项。 |
| **前端页面层** | `AppCreateWizard.vue` | 移除创建应用向导中的共享策略选择步骤。 | 确保新创建的应用默认且强制使用独立的组织架构体系。 |
| **前端页面层** | `services/api-tenant-app-instances.ts` | 删除 `getTenantAppInstanceSharingPolicy` 等相关的 API 调用函数。 | 配合后端控制器端点的移除，清理前端的僵尸代码。 |

## 4. 组织结构范围与角色权限边界规范

为了确保“平台级是平台级，应用级是应用级”的设计原则在后续的日常开发和系统运行中得到严格遵守，特制定以下边界规范，作为团队开发的准则。

在实体与数据库表设计方面，严禁跨域外键关联。应用级实体（例如 `AppRole`、`AppDepartment`）绝对不允许包含指向平台级实体（例如 `Department`、`Role`）的直接外键。两套体系唯一允许的交叉点是 `UserId`，它作为平台用户在应用中的投影标识存在。此外，虽然 `DataScopeType` 是一个通用的枚举类型，但其解析逻辑必须严格隔离。当 `AppRole` 的数据范围设置为自定义部门（`CustomDept`）时，其关联的 `DeptIds` 必须且只能解析为 `AppDepartment.Id`，绝不能与平台 `Department.Id` 发生混用。

在服务层与依赖注入方面，必须实行严格的服务接口隔离。`Identity` 命名空间下的服务（如 `IUserQueryService`）仅用于处理平台级操作，而 `Platform` 命名空间下的服务（如 `IAppOrgQueryService`）则专属于应用级操作。在处理应用级业务逻辑的服务实现中（例如 `TenantAppMembershipServices`），系统架构严禁开发者注入 `IDepartmentRepository` 或 `IRoleRepository` 等平台级的底层仓储，以从物理上切断依赖。

在数据范围过滤（DataScopeFilter）方面，现有的 `DataScopeFilter` 将被重命名并改造为 `TenantDataScopeFilter`，它仅在查询平台级全局资源（如系统用户列表、全租户审计日志）时使用，并基于平台的 `Role` 和 `Department` 计算权限。同时，系统将新增 `AppDataScopeFilter`，专门用于查询应用内部的业务数据（如表单数据、流程实例），它必须且只能基于 `AppRole` 和 `AppDepartment` 来计算可见范围。

最后，在审批流引擎方面，引擎内部的组件（如 `AssigneeResolver` 和 `ApprovalUserService`）必须具备敏锐的上下文感知能力。当审批实例明确属于某个特定应用时，其“寻找部门负责人”或“查找直属上级”的底层逻辑必须自动路由到 `IAppOrgQueryService`，去查询应用独立的 `AppDepartment` 和 `AppPosition`，而绝对不能默认回退到查询平台级的行政组织树。这一规范确保了应用级组织架构在复杂业务流中的真正落地。
