# Coze 中国版治理与 Assistant 补齐计划

> 来源：`coze_lowcode_replica_architecture_report_v3.docx` 第 22–26 章（Assistant 体系、账号与权限、缺口清单、官方来源、最终判断）。  
> 基线仓库：`Atlas.SecurityPlatform`（本 worktree）。  
> 衔接文档：[plan-coze-lowcode-FINAL-report.md](plan-coze-lowcode-FINAL-report.md)（M01–M20 低代码已完工）、[plan-coze-atlas-round2.md](plan-coze-atlas-round2.md)（AiPlatform 落仓骨架）。

本文档只做 **Gap 调研矩阵 + 可执行路线图**，不替代 [contracts.md](contracts.md) 与低代码系列 spec。

---

## 1. 范围与约束

- **范围**：对照 v3 报告 22–26 章，盘点当前实现缺口，并按 P0/P1/P2 拆里程碑。
- **约束**：路线图阶段仅定义目标与验证方式；**实施各里程碑时再**同步更新契约、`.http`、测试与 i18n。
- **命名约定**：官方文档中的「Assistant / 智能体」在仓库域模型中主要对应 **`Agent`** 聚合；`AiAssistantsController` 为 REST 别名。下文 **「Assistant」= 产品语义**，**`Agent`** = 代码实体，除非另行说明。

---

## 2. Gap 调研矩阵

### 2.1 第 22 章：Assistant 产品壳

| 要求维度（摘自报告22 章） | 结论 | 现状摘要 | 关键路径 |
| --- | --- | --- | --- |
| Assistant 非 Workflow 别名，独立产品壳 | **部分实现** | `Agent` + `TeamAgent` 双轨；前端 `AgentWorkbench`、`DevelopPage` 与 Workflow 画布分路由 | [Agent.cs](src/backend/Atlas.Domain/AiPlatform/Entities/Agent.cs)、[TeamAgentEntities.cs](src/backend/Atlas.Domain/AiPlatform/Entities/TeamAgentEntities.cs)、[module-studio-react assistant](src/frontend/packages/module-studio-react/src/assistant/) |
| 创建入口：低代码 / AI 创建 / 模板 | **部分实现** | 手动 `POST api/v1/agents`、`POST api/v1/ai-assistants`；`TeamAgent` 模板 `from-template` | [AgentsController.cs](src/backend/Atlas.Presentation.Shared/Controllers/Ai/AgentsController.cs)、[AiAssistantsController.cs](src/backend/Atlas.PlatformHost/Controllers/AiAssistantsController.cs)、[TeamAgentsController.cs](src/backend/Atlas.PlatformHost/Controllers/TeamAgentsController.cs) |
| 技能装配（插件/工作流/知识库/数据库/记忆等） | **部分实现** |多表绑定：`AgentPluginBinding`、`AgentKnowledgeLink`、`AgentWorkflowBinding`（含 Skill 角色）、`AgentDatabaseBinding`、`AgentVariableBinding` 等 | [AgentBindings.cs](src/backend/Atlas.Domain/AiPlatform/Entities/Agent/AgentBindings.cs)、[agent-workbench.tsx](src/frontend/packages/module-studio-react/src/assistant/agent-workbench.tsx) |
| 触发器、卡片与 Assistant 强绑定 | **缺失/旁路** | 触发器多在 Workflow/兼容层；未见 `AgentTrigger`/`AgentCard` 一等实体 | [CozeWorkflowCompatControllerBase.cs](src/backend/Atlas.Presentation.Shared/Controllers/Ai/CozeWorkflowCompatControllerBase.cs) |
| 发布渠道（飞书/微信/Web SDK 等） | **部分实现** | `WorkspacePublishChannel` 类型字符串；`AgentPublication` 嵌入发布；抖音/豆包未见域对齐 | [WorkspacePublishChannel.cs](src/backend/Atlas.Domain/AiPlatform/Entities/WorkspacePublishChannel.cs)、[AgentPublication.cs](src/backend/Atlas.Domain/AiPlatform/Entities/AgentPublication.cs)、[WorkspacePublishChannelsController.cs](src/backend/Atlas.PlatformHost/Controllers/WorkspacePublishChannelsController.cs) |
| Assistant 调试台（提示词/技能/渠道一体） | **部分实现** | `AgentChatController` 对话/流式；`AgentDebugPanel`；无统一「渠道回包」联调 API | [AgentChatController.cs](src/backend/Atlas.Presentation.Shared/Controllers/Ai/AgentChatController.cs) |
| 独立前端域包 `assistant-*` | **缺失** | 能力落在 `@atlas/module-studio-react` | [module-studio-react/package.json](src/frontend/packages/module-studio-react/package.json) |
| Runtime：`publish` / `channel` / `collaborator` / `logs` | **部分实现** | `publish`、`publications`、`embed-token` 有；协作者兼容层桩；专用 Agent 日志 API 不明确 | [AiAssistantsController.cs](src/backend/Atlas.PlatformHost/Controllers/AiAssistantsController.cs)、Coze 兼容 `list_collaborators` 空列表 |

**差距小结**：内核（`Agent` 绑定 + IDE + 对话 + 发布记录）已具备；**渠道真接入、协作者真数据、一体化调试与运维日志、AI 创建链、抖音/豆包** 仍为缺口。

---

### 2.2 第 23 章：主数据（企业 / 组织 / 空间 / 成员）

| 要求维度 | 结论 | 现状摘要 | 关键路径 |
| --- | --- | --- | --- |
| 企业 → 组织 → 工作空间 → 成员 | **部分实现** | 产品语义上「企业」近似 `Tenant`；无独立 Organization 实体；`orgId` 路由常等价 `tenantId` | [Tenant.cs](src/backend/Atlas.Domain/System/Entities/Tenant.cs)、[OrganizationWorkspacesController.cs](src/backend/Atlas.AppHost/Controllers/OrganizationWorkspacesController.cs) |
| 工作空间与租户、应用实例关系 | **已实现（模型+服务）** | `Workspace` 继承 `TenantEntity`，含 `AppInstanceId`/`AppKey` | [WorkspaceEntities.cs](src/backend/Atlas.Domain/AiPlatform/Entities/WorkspaceEntities.cs)、[WorkspacePortalService.cs](src/backend/Atlas.Infrastructure/Services/Platform/WorkspacePortalService.cs) |
| 租户内应用实例 | **已实现** | `TenantApplication`（`AppInstanceId` / `AppKey`等）与工作空间、资源 IDE 入口绑定 | [ProductizationEntities.cs](src/backend/Atlas.Domain/Platform/Entities/ProductizationEntities.cs)（`TenantApplication`） |
| 应用成员 | **已实现** | `AppMember` + `TenantAppMembersV2Controller` + Command/Query 服务 | [AppMembershipEntities.cs](src/backend/Atlas.Domain/Platform/Entities/AppMembershipEntities.cs)、[TenantAppMembersV2Controller.cs](src/backend/Atlas.Presentation.Shared/Controllers/TenantAppV2/TenantAppMembersV2Controller.cs)、[TenantAppMembershipServices.cs](src/backend/Atlas.Infrastructure/Services/Platform/TenantAppMembershipServices.cs) |
| 组织内部门/岗位（应用组织） | **已实现（应用维）** | `TenantAppOrganizationController` 等 | [TenantAppOrganizationController.cs](src/backend/Atlas.Presentation.Shared/Controllers/TenantAppV2/TenantAppOrganizationController.cs) |
| 跨组织/空间迁移 | **缺失** | 兼容层多为 fallback | [CozeWorkflowCompatControllerBase.cs](src/backend/Atlas.Presentation.Shared/Controllers/Ai/CozeWorkflowCompatControllerBase.cs) |

**差距小结**：**缺与 Coze 对齐的独立「组织」主数据层**；当前更接近 **租户 → 应用实例 → 工作空间 → 成员**。

---

### 2.3 第 23 章：三层权限（成员角色 / 操作权限 / 资源协作）

| 要求维度 | 结论 | 现状摘要 | 关键路径 |
| --- | --- | --- | --- |
| 平台 RBAC（角色 + 权限码 + 菜单 + 部门数据范围） | **已实现** | `Role`、`RolePermission`、`RoleMenu`、`RoleDept`、`Permission` | [Role.cs](src/backend/Atlas.Domain/Identity/Entities/Role.cs)、[RolePermission.cs](src/backend/Atlas.Domain/Identity/Entities/RolePermission.cs)、[Permission.cs](src/backend/Atlas.Domain/Identity/Entities/Permission.cs) |
| 应用级权限定义 | **已实现** | `AppPermission`、`AppRole`、`AppRolePermission`；`TenantAppPermissionsController` 管权限点 CRUD | [AppMembershipEntities.cs](src/backend/Atlas.Domain/Platform/Entities/AppMembershipEntities.cs)、[TenantAppPermissionsController.cs](src/backend/Atlas.Presentation.Shared/Controllers/TenantAppV2/TenantAppPermissionsController.cs) |
| 工作空间角色与默认动作 | **已实现** | `WorkspaceRole`、`WorkspaceMember`；`RequireWorkspaceAccessAsync` | [WorkspaceEntities.cs](src/backend/Atlas.Domain/AiPlatform/Entities/WorkspaceEntities.cs)、[WorkspacePortalService.cs](src/backend/Atlas.Infrastructure/Services/Platform/WorkspacePortalService.cs) |
| 资源级协作（协作者 ACL） | **部分实现/未接入运行时** | `WorkspaceResourcePermission` 表存在；`WorkflowCollaborator` 实体存在；服务层列表可恒空；兼容层协作者桩 | [WorkspaceEntities.cs](src/backend/Atlas.Domain/AiPlatform/Entities/WorkspaceEntities.cs)、[WorkflowCompatServices.cs](src/backend/Atlas.Infrastructure/Services/AiPlatform/WorkflowCompatServices.cs) |
| PDP | **已实现** | `IPermissionDecisionService`、`PermissionAuthorizationHandler` | [PermissionDecisionService.cs](src/backend/Atlas.Infrastructure/Services/PermissionDecisionService.cs)、[PermissionAuthorizationHandler.cs](src/backend/Atlas.Presentation.Shared/Authorization/PermissionAuthorizationHandler.cs) |

**差距小结**：**资源协作表与 API 有雏形，但未与资源命令/查询统一鉴权**；**通用 ResourceAcl/ResourceMember 抽象缺失**。

---

### 2.4 第 23 章：企业安全 / SSO / 网络 / 数据访问

| 要求维度 | 结论 | 现状摘要 | 关键路径 |
| --- | --- | --- | --- |
| 登录主路径 | **已实现** | JWT Bearer + Cookie `access_token` 回退 | [Program.cs](src/backend/Atlas.PlatformHost/Program.cs)、[AuthController.cs](src/backend/Atlas.PlatformHost/Controllers/AuthController.cs) |
| OIDC / SSO 脚手架 | **部分实现** | `AddOpenIdConnect` 多 scheme、`SsoController`、`OidcLink` | 同上、[SsoController.cs](src/backend/Atlas.PlatformHost/Controllers/SsoController.cs)、[OidcOptions.cs](src/backend/Atlas.Infrastructure/Security/OidcOptions.cs)、[OidcLink.cs](src/backend/Atlas.Domain/Identity/Entities/OidcLink.cs) |
| SAML | **缺失** | 仓库未见 SAML 实现 | - |
| 租户自助多 IdP | **部分实现** | 部署级 `Providers[]`；无租户级 IdP 领域 CRUD | [OidcOptions.cs](src/backend/Atlas.Infrastructure/Security/OidcOptions.cs) |
| 私网连接 / IP 白名单 / 网络策略 | **缺失（租户级）** | 前端插件表单可能存在 `privateNetwork` 类 UI，非企业网络策略 | - |
| 数据驻留 / 数据访问策略 | **缺失** | 无 `DataResidency`/`DataAccessPolicy` 域 | - |

---

### 2.5 第 23 章：成员生命周期

| 要求维度 | 结论 | 现状摘要 | 关键路径 |
| --- | --- | --- | --- |
| 邀请（令牌、邮件/链接） | **缺失** | 加人多为「用户已存在」；兼容层 invite 桩 | [TenantAppMembershipServices.cs](src/backend/Atlas.Infrastructure/Services/Platform/TenantAppMembershipServices.cs) |
| 成员激活（企业场景） | **部分实现** | `UserAccount` 启用/禁用 ≠ Coze 式待激活成员 | [UserAccount.cs](src/backend/Atlas.Domain/Identity/Entities/UserAccount.cs)、[UserCommandService.cs](src/backend/Atlas.Infrastructure/Services/UserCommandService.cs) |
| 离职与资产交接 | **部分实现** | 审批域离职转办；无通用资源所有权转移 | [BatchTransferHandler.cs](src/backend/Atlas.Infrastructure/Services/ApprovalFlow/OperationHandlers/BatchTransferHandler.cs) |
| 角色变更 | **已实现** | `RoleCommandService` | [RoleCommandService.cs](src/backend/Atlas.Infrastructure/Services/RoleCommandService.cs) |

---

### 2.6 第 24 章：P0 / P1 / P2 与 Atlas 映射

| 报告优先级 | 报告要点 | Atlas 映射结论 |
| --- | --- | --- |
| **P0** | Assistant 产品壳独立建模 | **部分具备**：`Agent` + Studio；需 **渠道真能力、日志、协作者、AI 创建** |
| **P0** | 企业/组织/空间/成员主数据 | **部分具备**：`Tenant`+`Workspace`+`AppMember`；**缺独立 Organization 层** |
| **P0** | RBAC + 成员权限 + 协作者 ACL 三层 | **部分具备**：平台/应用/工作空间权限有；**资源 ACL 未统一 enforced** |
| **P0** | 审计与日志可见性（owner/collaborator） | **部分具备**：审计按 Actor/部门范围；**消息日志未按资源协作收缩**（见 [AuditQueryService.cs](src/backend/Atlas.Infrastructure/Services/AuditQueryService.cs)、[RuntimeSessionAndChatflowServices.cs](src/backend/Atlas.Infrastructure/Services/LowCode/RuntimeSessionAndChatflowServices.cs)） |
| **P1** | SSO/SAML、私网、成员生命周期、企业发布治理 | **大比例缺口**：OIDC 有；SAML/私网/邀请链/企业商店需补 |
| **P2** | 开源 vs 商用差异表 | **文档缺口**：需单独附录（本路线图 M-G09） |

### 2.7 第 25–26 章（来源与结论）

- **第 25 章**：官方链接清单（S01–S11）应并入总报告附录；实施阶段每开一条能力应对应可追溯 URL。
- **第 26 章**：**最难复刻的是 Assistant 产品层 + 账号权限治理层**；与本 Gap 结论一致：画布与低代码内核已强，**治理与渠道/协作**为短板。

---

## 3. 路线图（里程碑）

以下里程碑字段统一包含：**目标 / 优先级 / 现状基线 / 修改范围 / 涉及文件 / 契约影响 / 库表变更 / 验证 / 依赖**。

### M-G01：Assistant 命名收口与边界说明（P0）

| 字段 | 内容 |
| --- | --- |
| **目标** | 在仓库文档中固定「Assistant ≡ `Agent` + `AiAssistantsController`」产品映射，避免与 `TeamAgent`、Coze `Bot` 混用。 |
| **现状基线** | [plan-coze-atlas-round2.md](plan-coze-atlas-round2.md) 已写智能体=`Agent`。 |
| **修改范围** | 文档（可选：根 `README` 或 `docs/coze/` 下说明页，**不强制改 contracts**）。 |
| **涉及文件** | [Agent.cs](src/backend/Atlas.Domain/AiPlatform/Entities/Agent.cs)、[AiAssistantsController.cs](src/backend/Atlas.PlatformHost/Controllers/AiAssistantsController.cs)（实施时）、本文件。 |
| **契约影响** | 无（仅澄清语义）。 |
| **库表变更** | 无。 |
| **验证** | 文档审阅。 |
| **依赖** | 无。 |

### M-G02：渠道适配器化 + 渠道配置与回执（P0）

| 字段 | 内容 |
| --- | --- |
| **目标** | `WorkspacePublishChannel` 从「类型 + 占位 Reauthorize」升级为可插拔 **Connector**（至少 Web SDK / 微信 / 飞书其一端到端打通）；与 `AgentPublication` 建立可选关联或发布快照引用。 |
| **现状基线** | [WorkspacePublishChannel.cs](src/backend/Atlas.Domain/AiPlatform/Entities/WorkspacePublishChannel.cs)、[WorkspaceFolderService.cs](src/backend/Atlas.Infrastructure/Services/Coze/WorkspaceFolderService.cs) 内 `WorkspacePublishChannelService`、`ReauthorizeAsync` 占位。 |
| **修改范围** | Domain（可选版本/快照实体）、Infrastructure（Connector接口与实现）、PlatformHost（Controller）、前端 Publish Center。 |
| **涉及文件** | [WorkspacePublishChannelsController.cs](src/backend/Atlas.PlatformHost/Controllers/WorkspacePublishChannelsController.cs)、[publish-center-page.tsx](src/frontend/packages/module-studio-react/src/publish/publish-center-page.tsx)。 |
| **契约影响** | **是**：新增/变更渠道 API 时同步 [contracts.md](contracts.md) 与 `.http`。 |
| **库表变更** | **可能**：渠道发布版本、OAuth state、回执字段。 |
| **验证** | `dotnet build`；相关 `dotnet test`；`pnpm run build` /针对性 E2E（若有）。 |
| **依赖** | M-G01（语义清晰）。 |

### M-G03：资源 ACL 闭环 + PDP 接入（P0）

| 字段 | 内容 |
| --- | --- |
| **目标** | 将 `WorkspaceResourcePermission` / `WorkflowCollaborator` / 未来通用 `ResourceMember` **接入** Agent、Workflow、App、Knowledge、Database 等 Command/Query 的统一鉴权；淘汰「仅有管理 API、运行时放行」模式。 |
| **现状基线** | [WorkspacePortalService.cs](src/backend/Atlas.Infrastructure/Services/Platform/WorkspacePortalService.cs) `RequireWorkspaceAccessAsync`；[WorkflowCompatServices.cs](src/backend/Atlas.Infrastructure/Services/AiPlatform/WorkflowCompatServices.cs)。 |
| **修改范围** | Application（授权抽象）、Infrastructure（各资源 Service）、Presentation（兼容层真实数据替代桩）。 |
| **涉及文件** | [WorkspaceEntities.cs](src/backend/Atlas.Domain/AiPlatform/Entities/WorkspaceEntities.cs)、各 `*CommandService` / `*QueryService`。 |
| **契约影响** | **可能**：若对外暴露协作者 CRUD，需更新 contracts + Coze 兼容 API。 |
| **库表变更** | **可能**：统一资源成员表或扩展现有表索引。 |
| **验证** | `dotnet test`（鉴权回归）；关键 `.http` 负例用例。 |
| **依赖** | 与 M-G04 强相关（日志可见性依赖同一 ACL 模型）。 |

### M-G04：审计与消息日志按 owner + collaborator 收口（P0）

| 字段 | 内容 |
| --- | --- |
| **目标** | `AuditQueryService` 与 `RuntimeMessageLogService`（及低代码 `message-log`）查询 **默认** 收缩为「当前用户可访问资源集合」（所有者 + 协作者 + 空间管理员策略）。 |
| **现状基线** | [AuditQueryService.cs](src/backend/Atlas.Infrastructure/Services/AuditQueryService.cs)；[LowCodeSessionRepository.cs](src/backend/Atlas.Infrastructure/Repositories/LowCode/LowCodeSessionRepository.cs) / Runtime 消息查询。 |
| **修改范围** | Infrastructure（查询过滤）、Application（DTO 不变或扩展 scope）。 |
| **涉及文件** | 同上；低代码 trace：`src/backend/Atlas.AppHost` 下 runtime 控制器（实施时精确定位）。 |
| **契约影响** | **可能**：若客户端依赖「宽列表」行为，需 major 版本或 feature flag。 |
| **库表变更** | 可选索引（tenant_id + resource_id + user_id）。 |
| **验证** | `dotnet test`；手工 `.http` 多用户场景。 |
| **依赖** | **必须在 M-G03 之后或并行**，ACL 模型需一致。 |

### M-G05：Organization 主数据层 + Tenant 解耦（P1）

| 字段 | 内容 |
| --- | --- |
| **目标** | 引入与企业订阅对齐的 **Organization**（或 EnterpriseOrg）实体，使 `orgId` 不再恒等于 `tenantId`；路由与 JWT claim 渐进迁移。 |
| **现状基线** | [OrganizationWorkspacesController.cs](src/backend/Atlas.AppHost/Controllers/OrganizationWorkspacesController.cs)。 |
| **修改范围** | Domain、迁移、全链路 `TenantContext` / 中间件、前端 `org/:orgId` 路由。 |
| **契约影响** | **是**：大范围。 |
| **库表变更** | **是**。 |
| **验证** | `dotnet build` + 集成测试 + 前端 `pnpm run i18n:check`。 |
| **依赖** | 建议在 P0 稳定后启动。 |

### M-G06：成员邀请 / 激活 / 离职 / 资产移交（P1）

| 字段 | 内容 |
| --- | --- |
| **目标** | `Invitation` 令牌流、成员待激活状态、离职触发资源交接（与审批 `BatchTransfer` 对齐或统一）。 |
| **现状基线** | [TenantAppMembershipServices.cs](src/backend/Atlas.Infrastructure/Services/Platform/TenantAppMembershipServices.cs)；[BatchTransferHandler.cs](src/backend/Atlas.Infrastructure/Services/ApprovalFlow/OperationHandlers/BatchTransferHandler.cs)。 |
| **修改范围** | Domain + Identity + Platform服务 + 邮件/通知（可选）。 |
| **契约影响** | **是**。 |
| **库表变更** | **是**。 |
| **验证** | `dotnet test` + E2E（注册/邀请链路）。 |
| **依赖** | M-G05（组织清晰后更自然）。 |

### M-G07：SAML / 租户级 IdP 自助配置（P1）

| 字段 | 内容 |
| --- | --- |
| **目标** | SAML SP/IdP 集成或与 OIDC 并列；租户管理员可配置 IdP（脱敏存储证书/元数据）。 |
| **现状基线** | [Program.cs](src/backend/Atlas.PlatformHost/Program.cs) OIDC；[OidcOptions.cs](src/backend/Atlas.Infrastructure/Security/OidcOptions.cs)。 |
| **修改范围** | Infrastructure Security + Admin UI + 密钥保管。 |
| **契约影响** | **是**。 |
| **库表变更** | **是**（`TenantIdentityProvider` 等）。 |
| **验证** | 安全相关单测 + 手工 IdP 联调。 |
| **依赖** | 企业客户环境。 |

### M-G08：私网 / IP 白名单 / DataResidency 占位（P1）

| 字段 | 内容 |
| --- | --- |
| **目标** | 租户级网络策略与数据驻留 **策略模型**（可先只读占位 + 审计），为网关/连接器执行留扩展点。 |
| **现状基线** | 无一等域模型。 |
| **修改范围** | Domain（策略实体 JSON）、Admin API、文档。 |
| **契约影响** | 新增只读 API 时同步 contracts。 |
| **库表变更** | **是**（小）。 |
| **验证** | `dotnet build` + 契约测试。 |
| **依赖** | 与基础设施（YARP/防火墙）协同。 |

### M-G09：开源版 vs 商用版差异表 + 文档同步（P2）

| 字段 | 内容 |
| --- | --- |
| **目标** | 在 `docs/` 增加「Coze Studio 开源 vs 中国商用」能力对照表，指导 MVP 裁剪。 |
| **现状基线** | 外部：[coze-studio](https://github.com/coze-dev/coze-studio) README。 |
| **修改范围** | 仅文档。 |
| **涉及文件** | 新建 `docs/coze-open-vs-cn-commercial.md`（实施时）或扩写本节附录。 |
| **契约影响** | 无。 |
| **验证** | 产品/架构评审。 |

### M-G10：Trigger / Card 升为 Agent 一等装配（P2）

| 字段 | 内容 |
| --- | --- |
| **目标** | 将应用/智能体侧触发器、卡片配置从 Workflow 兼容 fallback 收束到 `Agent` 或 `AiApp` 绑定表，IDE 统一展示。 |
| **现状基线** | [CozeWorkflowCompatControllerBase.cs](src/backend/Atlas.Presentation.Shared/Controllers/Ai/CozeWorkflowCompatControllerBase.cs)。 |
| **修改范围** | Domain + Studio UI + 兼容层映射。 |
| **契约影响** | **是**（Coze API行为变化需谨慎）。 |
| **库表变更** | **可能**。 |
| **验证** | `dotnet test` +前端单测 + i18n。 |

---

## 4. 验证与文档同步规则

每个里程碑合并前建议至少：

| 层级 | 命令 / 动作 |
| --- | --- |
| 后端 | `dotnet build`（全 sln 0 警告 0 错误） |
| 后端测试 | 受影响模块 `dotnet test tests/...` |
| 前端 | `cd src/frontend && pnpm run build` |
| 前端测试 | `pnpm run test:unit`（改 UI 时） |
| 国际化 | `pnpm run i18n:check`（用户可见文案） |
| API | 更新对应 `src/backend/Atlas.PlatformHost/Bosch.http/` 或 `Atlas.AppHost/Bosch.http/` |
| 契约 | 凡对外 REST 变更，更新 [contracts.md](contracts.md) |

---

## 5. 风险与依赖

1. **Tenant ↔ Organization 拆分**：影响 JWT、`TenantContext`、全量 `TenantEntity` 查询与缓存键；需分阶段迁移与数据回填。
2. **Coze 兼容层桩**：`list_collaborators` 等空实现易导致前端误判能力；治理里程碑应 **显式区分「已支持」与「stub」**。
3. **PDP 缓存一致性**：扩展资源权限后，需协调 `PermissionDecisionService` 失效策略，避免权限变更滞后。
4. **双轨智能体**：`Agent` 与 `TeamAgent` 并存，产品壳与路由需统一叙事，避免重复实现发布/调试。
5. **安全与合规**：日志可见性收紧可能破坏依赖宽接口的集成；需版本策略或 scoped query参数。

---

## 6. 附录：Gap 调研引用文件清单

### 6.1 Assistant / 发布 / 对话

- [src/backend/Atlas.Domain/AiPlatform/Entities/Agent.cs](src/backend/Atlas.Domain/AiPlatform/Entities/Agent.cs)
- [src/backend/Atlas.Domain/AiPlatform/Entities/Agent/AgentBindings.cs](src/backend/Atlas.Domain/AiPlatform/Entities/Agent/AgentBindings.cs)
- [src/backend/Atlas.Domain/AiPlatform/Entities/AgentPublication.cs](src/backend/Atlas.Domain/AiPlatform/Entities/AgentPublication.cs)
- [src/backend/Atlas.Domain/AiPlatform/Entities/WorkspacePublishChannel.cs](src/backend/Atlas.Domain/AiPlatform/Entities/WorkspacePublishChannel.cs)
- [src/backend/Atlas.PlatformHost/Controllers/AiAssistantsController.cs](src/backend/Atlas.PlatformHost/Controllers/AiAssistantsController.cs)
- [src/backend/Atlas.Presentation.Shared/Controllers/Ai/AgentsController.cs](src/backend/Atlas.Presentation.Shared/Controllers/Ai/AgentsController.cs)
- [src/backend/Atlas.Presentation.Shared/Controllers/Ai/AgentChatController.cs](src/backend/Atlas.Presentation.Shared/Controllers/Ai/AgentChatController.cs)
- [src/backend/Atlas.PlatformHost/Controllers/TeamAgentsController.cs](src/backend/Atlas.PlatformHost/Controllers/TeamAgentsController.cs)
- [src/frontend/packages/module-studio-react/src/assistant/agent-workbench.tsx](src/frontend/packages/module-studio-react/src/assistant/agent-workbench.tsx)
- [src/frontend/packages/module-studio-react/src/publish/publish-center-page.tsx](src/frontend/packages/module-studio-react/src/publish/publish-center-page.tsx)

### 6.2 主数据 / 工作空间 / 成员

- [src/backend/Atlas.Domain/System/Entities/Tenant.cs](src/backend/Atlas.Domain/System/Entities/Tenant.cs)
- [src/backend/Atlas.Domain/AiPlatform/Entities/WorkspaceEntities.cs](src/backend/Atlas.Domain/AiPlatform/Entities/WorkspaceEntities.cs)
- [src/backend/Atlas.Domain/Platform/Entities/AppMembershipEntities.cs](src/backend/Atlas.Domain/Platform/Entities/AppMembershipEntities.cs)
- [src/backend/Atlas.AppHost/Controllers/OrganizationWorkspacesController.cs](src/backend/Atlas.AppHost/Controllers/OrganizationWorkspacesController.cs)
- [src/backend/Atlas.Infrastructure/Services/Platform/WorkspacePortalService.cs](src/backend/Atlas.Infrastructure/Services/Platform/WorkspacePortalService.cs)
- [src/backend/Atlas.Infrastructure/Services/Platform/TenantAppMembershipServices.cs](src/backend/Atlas.Infrastructure/Services/Platform/TenantAppMembershipServices.cs)
- [src/backend/Atlas.Presentation.Shared/Controllers/TenantAppV2/TenantAppMembersV2Controller.cs](src/backend/Atlas.Presentation.Shared/Controllers/TenantAppV2/TenantAppMembersV2Controller.cs)

### 6.3 权限 / 兼容层 / 审计 / 消息日志

- [src/backend/Atlas.Domain/Identity/Entities/Role.cs](src/backend/Atlas.Domain/Identity/Entities/Role.cs)
- [src/backend/Atlas.Infrastructure/Services/PermissionDecisionService.cs](src/backend/Atlas.Infrastructure/Services/PermissionDecisionService.cs)
- [src/backend/Atlas.Infrastructure/Services/AiPlatform/WorkflowCompatServices.cs](src/backend/Atlas.Infrastructure/Services/AiPlatform/WorkflowCompatServices.cs)
- [src/backend/Atlas.Presentation.Shared/Controllers/Ai/CozeWorkflowCompatControllerBase.cs](src/backend/Atlas.Presentation.Shared/Controllers/Ai/CozeWorkflowCompatControllerBase.cs)
- [src/backend/Atlas.Infrastructure/Services/AuditQueryService.cs](src/backend/Atlas.Infrastructure/Services/AuditQueryService.cs)
- [src/backend/Atlas.Infrastructure/Services/LowCode/RuntimeSessionAndChatflowServices.cs](src/backend/Atlas.Infrastructure/Services/LowCode/RuntimeSessionAndChatflowServices.cs)

### 6.4 认证 / SSO

- [src/backend/Atlas.PlatformHost/Program.cs](src/backend/Atlas.PlatformHost/Program.cs)
- [src/backend/Atlas.PlatformHost/Controllers/SsoController.cs](src/backend/Atlas.PlatformHost/Controllers/SsoController.cs)
- [src/backend/Atlas.Infrastructure/Security/OidcOptions.cs](src/backend/Atlas.Infrastructure/Security/OidcOptions.cs)

---

**文档版本**：2026-04-18（与 v3 报告 22–26 章对齐首版）。
