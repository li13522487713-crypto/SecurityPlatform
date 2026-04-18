# Coze Studio 开源版 vs 中国商用版差异表（治理 M-G09-C1）

> 来源：[coze-studio](https://github.com/coze-dev/coze-studio) README + 官方运营介绍页 + 本仓库 `docs/coze-api-gap.md` 与 `docs/plan-coze-platform-governance.md`。
> 用途：在裁剪 / 选型决策时一站式对照功能差异 + Atlas.SecurityPlatform 当前实现状态。

| 能力分类 | 开源版（coze-studio） | 中国商用版 (coze.cn) | Atlas 现状 | 备注 |
| --- | --- | --- | --- | --- |
| Assistant / Agent 设计 | 提供 | 提供 | 已实现（`Agent` + `AgentBindings` + Studio 工作台） | M01–M20 闭环；治理 M-G01 完成命名收口 |
| 多 Agent 编排 / TeamAgent | 实验 | 提供 | 已实现（`TeamAgent` + `TeamAgentExecution`） | — |
| 知识库 / RAG | 提供 | 提供 | 已实现（`KnowledgeBase` + 向量检索） | — |
| 插件市场 + 自定义插件 | 部分 | 提供 | 已实现（`AiPlugin` + Lowcode plugin store） | — |
| 工作流 (DAG / Logic Flow) | 提供 | 提供 | 已实现（`AiWorkflow` + LogicFlow + DAG executor） | — |
| 模板市场（Agent / 工作流） | 部分 | 提供 | 已实现（`AppTemplate` + `TeamAgentTemplate`） | — |
| 调试 + 试运行 | 提供 | 提供 | 已实现（`AgentChatController` + `AgentDebugPanel`） | — |
| 发布渠道：Web SDK | 部分（snippet 模板） | 提供 | 已实现（`WebSdkChannelConnector` + HMAC + Origin） | 治理 M-G02-C3 |
| 发布渠道：Open API | 提供（基础 token） | 提供（含限流） | 已实现（`OpenApiChannelConnector` + token + per-channel rate limit） | 治理 M-G02-C4 |
| 发布渠道：飞书机器人 | 缺失 | 提供 | 已实现（`FeishuChannelConnector` + ApiClient + webhook + 凭据加密） | 治理 M-G02-C5..C8 |
| 发布渠道：微信公众号 | 缺失 | 提供 | 已实现（`WechatMpChannelConnector` + ApiClient + webhook） | 治理 M-G02-C9..C11 |
| 发布渠道：抖音 / 豆包 | 缺失 | 提供 | 计划中（沿用 IWorkspaceChannelConnector 抽象） | M-G02 follow-up |
| 渠道发布版本与回滚 | 缺失 | 提供 | 已实现（`WorkspaceChannelRelease` + 状态机） | 治理 M-G02-C2 |
| 平台 RBAC | 提供 | 提供 | 已实现（Role / Permission / RoleMenu / RoleDept） | — |
| 工作空间角色 | 部分 | 提供 | 已实现（`WorkspaceRole` + `WorkspaceMember`） | — |
| 资源协作（per-user ACL） | 缺失 | 提供 | 已实现（`WorkspaceResourcePermission` + `IResourceCollaboratorService` + CRUD API） | 治理 M-G03-C6 + C7 |
| PDP 资源失效 + ResourceAccessGuard | 缺失 | 提供（隐式） | 已实现（`IResourceAccessGuard` + `IResourceWriteGate` + 资源 cache tag） | 治理 M-G03-C1..C5 |
| 审计资源化（owner / collaborator 收口） | 部分 | 提供 | 已实现（AuditRecord 增列 + `IResourceVisibilityResolver`） | 治理 M-G04（解析器；service 端读路径下次迭代切换） |
| 企业组织 / 部门 | 缺失 | 提供 | 已实现（`Organization` + `OrganizationMember` + 跨组织 workspace 迁移 API） | 治理 M-G05 |
| 成员邀请（邮件 + token） | 缺失 | 提供 | 已实现（`MemberInvitation` + `SmtpInvitationEmailSender` 占位 + accept/revoke） | 治理 M-G06-C1 |
| 用户状态机 | 缺失 | 提供 | 已实现（`UserAccount.Status` active/pending/disabled/offboarded） | 治理 M-G06-C2 |
| 离职资产移交 | 缺失 | 提供 | 已实现（`ResourceOwnershipTransfer` + `OffboardController`） | 治理 M-G06-C3 |
| 组织间成员迁移 | 缺失 | 提供 | 已实现（`OffboardController.MoveMember`） | 治理 M-G06-C4 |
| OIDC SSO | 部分（部署级） | 提供（租户级多 IdP） | 已实现存储（`TenantIdentityProvider` CRUD） | 运行时动态 scheme 注入：M-G07-C3 follow-up |
| SAML SSO | 缺失 | 提供 | 已实现存储 + SP metadata 端点；ACS/SLO 待 ITfoxtec.Identity.Saml2.MvcCore 接入 | 治理 M-G07-C1 follow-up |
| SSO 首次登录策略 | 缺失 | 提供 | 已实现（`SsoLoginPolicyService` + 默认组织 join + audit） | 治理 M-G07-C5 |
| 租户网络策略（IP 白名单） | 缺失 | 提供 | 已实现存储（`TenantNetworkPolicy` audit/enforce） | IP middleware：M-G08-C1 follow-up |
| 数据驻留策略 | 缺失 | 提供 | 已实现存储（`TenantDataResidencyPolicy`） | 存储端点解析器接入：M-G08-C2 follow-up |
| Trigger（定时 / Webhook） | 部分（Workflow 内置） | 提供（Agent 一等绑定） | 已实现（`AgentTrigger`） | 治理 M-G10-C1 |
| Card（飞书 / 微信卡片） | 缺失 | 提供 | 已实现存储（`AgentCard`） | 渲染联动：M-G10-C2 follow-up |

> **总结**：开源版 ≈ 内核（Studio + Workflow + Knowledge）；商用版 ≈ 开源版 + 渠道生态 + 治理（组织 / 成员 / 协作 / 审计 / SSO / 网络）+ 触发器 / 卡片一等装配。
> Atlas 通过 16 session 治理后已基本对齐商用版 P0 / P1，剩余 follow-up 集中在外部库/中间件接入（SAML 库、OIDC 动态 scheme、IP 中间件、存储端点解析器、抖音 / 豆包 connector、卡片渲染）。
