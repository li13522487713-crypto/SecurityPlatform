# 接口契约说明

## 目标

- 统一前后端响应结构与分页模型。
- 明确多租户与认证相关的请求头。

## 通用请求头

- `Authorization: Bearer <accessToken>`：JWT 访问令牌。
- `X-Tenant-Id: <tenantId>`：租户标识（GUID）。
- `X-App-Id: <appId>`：应用标识（字符串）。已登录请求默认取 JWT 的 `app_id` Claim；当 `App.AllowHeaderOverrideWhenAuthenticated=true` 且满足受控条件时可被 Header 覆盖。
- `X-App-Workspace: 1`：应用工作台请求标记。当前默认策略下，已登录请求仅在该标记存在时允许 `X-App-Id` 覆盖 JWT `app_id`。
- `X-Client-Type: WebH5 | Mobile | Backend`：客户端类型。
- `X-Client-Platform: Web | Android | iOS`：客户端平台。
- `X-Client-Channel: Browser | App`：客户端通道。
- `X-Client-Agent: Chrome | Edge | Safari | Firefox | Other`：客户端代理（浏览器或环境）。
- `X-Project-Id: <projectId>`：项目标识（仅当应用启用项目模式 `EnableProjectMode = true` 时必填）。
- `Idempotency-Key: <uuid>`：关键写接口必填（创建/提交/开通/触发任务），幂等键冲突返回 409。
- `X-CSRF-TOKEN: <token>`：已登录 Web 写请求必填，需先获取 Anti-Forgery Token。
- `Accept-Language: zh-CN | en-US`：界面与 API 国际化语言标识。前端语言切换后必须同步发送该请求头。

## 平台统一规则（封板基线）

- Schema 主路线：AMIS；`LF(vform3)` 仅保留历史兼容，不再扩展新功能。
- 表达式统一：CEL（`CelExpressionEngine`），前后端共享一致的校验与求值语义。
- 上下文优先级：`Tenant > App > Project`。

### 上下文优先级细则

- Tenant：
  - 权威来源为 JWT claim + `X-Tenant-Id` 一致性校验
  - 不一致返回 `CROSS_TENANT_FORBIDDEN`
- App：
  - 运行态以 `appKey` 路由参数为准（`/r/:appKey/:pageKey`）
  - 应用工作台以 `appId` 路由参数为准（`/apps/:appId/*`）
- Project：
  - 仅在应用启用项目模式时要求 `X-Project-Id`
  - 缺失或非法时返回 `PROJECT_REQUIRED`
  - 用户未分配项目时返回 `PROJECT_FORBIDDEN`

### 发布态与草稿态行为矩阵

| 场景 | Draft | Published |
|---|---|---|
| 设计器编辑 | 允许 | 允许（编辑后生成新草稿） |
| `GET /api/v1/runtime/apps/{appKey}/pages/{pageKey}/schema` | 拒绝 | 允许 |
| 运行态菜单展示 | 否 | 是 |
| 运行态路由 `/r/:appKey/:pageKey` | 不可达 | 可达 |
| 表单提交 `/api/v1/runtime/apps/{appKey}/pages/{pageKey}/records` | 拒绝 | 允许 |

## 平台控制面/应用工作台/运行交付面路由约定（前端）

- 平台控制台入口：`/console`
- 控制台应用视图：`/console/apps`
- 控制台数据源：`/console/datasources`
- 控制台系统配置：`/console/settings/system/configs`
- 应用工作台根路由：`/apps/:appId`（重定向至 `/apps/:appId/dashboard`）
- 应用工作台页面：
  - `/apps/:appId/dashboard`
  - `/apps/:appId/builder`
  - `/apps/:appId/settings`
  - `/apps/:appId/run/:pageKey`
- 运行交付面入口：`/r/:appKey/:pageKey`

说明：

- 登录成功默认跳转 `/console`（若 `redirect` 参数存在且可访问则优先）。
- 旧有 `/settings/*` 与 `/lowcode/*` 路由保持兼容并标记 `Deprecated`（弃用窗口 6 个月）。

### 路由与菜单国际化约定

- 前端 `RouterMeta` 新增可选字段 `titleKey?: string`。
- 标题解析顺序：
  1. 若存在 `titleKey`，前端必须优先按当前语言翻译。
  2. 若不存在 `titleKey`，回退显示原始 `title`。
  3. 若为动态业务数据或用户自定义菜单，允许仅返回 `title`，不强制要求翻译。
- 后端菜单/动态路由返回结构保持兼容：
  - `title` 继续保留。
  - 系统内置菜单和内置路由可额外返回 `titleKey`。
- 验收要求：
  - 不得出现空白标题。
  - 不得直接向用户暴露未解析的 i18n key。

## 产品化重构 v1 契约增量（12 Sprint 基线）

### 统一模型（新增）

- `AppManifest`：应用元数据根对象（应用配置、资源绑定、发布策略）。
- `AppRelease`：发布快照、回滚点、影响分析摘要。
- `RuntimeRoute`：`appKey + pageKey` 到页面定义的发布态映射。
- `PackageArtifact`：导入导出包元数据（结构包/基础数据包/完整副本）。
- `LicenseGrant`：离线授权实体（功能项、席位、节点、有效期）。
- `ToolAuthorizationPolicy`：工具授权策略（主体、工具集、动作、环境、限流、审批）。
- `FlowDefinition`：审批流与工作流统一流程定义挂载对象。
- `DataClassification`：分类分级定义对象，描述等级、分类编码、适用范围和强制控制基线。
- `SensitiveLabel`：敏感标签对象，描述字段、对象、文件等可复用标签语义。
- `DataAsset`：应用数据资产对象，描述业务对象、敏感字段、敏感文件类型与责任归属。
- `DlpPolicy`：统一泄露防护策略对象，描述查看、脱敏、导出、下载、分享、外发、AI 使用规则。
- `OutboundChannel`：导出、下载、Webhook、Email、SMS、Connector、Plugin、AI、KnowledgeBase 等通道定义。
- `ExportControlPolicy`：导出下载管控对象，描述审批、额度、频次、脱敏、水印、留痕要求。
- `FileProtectionPolicy`：文件保护对象，描述上传校验、下载鉴权、外链有效期、水印、追踪与扫描接入点。
- `ExternalShareApproval`：外发审批记录对象，描述申请、审批、执行、撤销与审计关联。
- `LeakageEvent`：泄露风险事件对象，描述命中策略、阻断 / 放行结果、操作者、时间与对象范围。
- `EvidencePackage`：审计 / 合规证据包对象，描述事件、附件、摘要、处置记录与导出元数据。

### DLP 四层落位与五层控制模型

- 四层落位：
  - 平台层：统一规则、统一策略、统一审计、统一通道治理
  - 租户层：启用范围、例外策略、数据源绑定、合规口径
  - 应用层：对象标注、页面 / 接口 / 流程节点绑定、业务特例
  - 运行层：API、导出、下载、文件、消息、Webhook、AI / 知识库执行拦截与留痕
- 五层控制模型：
  - L1 数据识别层：分类分级、敏感标签、数据资产清单
  - L2 访问展示层：字段脱敏、最小可见、明文查看控制
  - L3 操作外发层：导出、下载、复制、分享、打印、报表生成
  - L4 跨边界传输层：Webhook、开放 API、消息通知、邮件、连接器、插件、AI / 知识库出站
  - L5 追踪处置层：水印、指纹、审计、异常检测、证据导出、处置闭环

### 新增路由约定（前端）

- 运行态入口：`/r/:appKey/:pageKey`
- 治理入口建议分组：
  - `/console/governance/licenses`
  - `/console/governance/packages`
  - `/console/governance/tools`

### 新增 API 分组（后端，`api/v1`）

- 平台面：
  - `GET /api/v1/platform/overview`
  - `GET /api/v1/platform/resources`
  - `GET /api/v1/platform/releases`
- 应用面：
  - `GET /api/v1/app-manifests`
  - `POST /api/v1/app-manifests`
  - `GET /api/v1/app-manifests/{id}`
  - `PUT /api/v1/app-manifests/{id}`
  - `GET /api/v1/app-manifests/{id}/workspace/{module}`
- 运行面：
  - `GET /api/v1/runtime/apps/{appKey}/pages/{pageKey}`
  - `POST /api/v1/runtime/apps/{appKey}/pages/{pageKey}/actions`
  - `GET /api/v1/runtime/tasks`（等价于 inbox）
  - `GET /api/v1/runtime/tasks/inbox`
  - `GET /api/v1/runtime/tasks/done`
  - `POST /api/v1/runtime/tasks/{taskId}/actions`
- 治理面：
  - `POST /api/v1/packages/export`
  - `POST /api/v1/packages/import`
  - `POST /api/v1/packages/analyze`
  - `POST /api/v1/licenses/offline-request`
  - `POST /api/v1/licenses/import`
  - `GET /api/v1/licenses/validate`
  - `GET /api/v1/tools/authorization-policies`
  - `POST /api/v1/tools/authorization-policies`
  - `PUT /api/v1/tools/authorization-policies/{id}`
  - `POST /api/v1/tools/simulate`
  - `GET /api/v1/tools/audit`
  - `GET /api/v1/tools/authorization-audits`（兼容别名，后续弃用）
  - `GET /api/v1/dlp/classifications`
  - `POST /api/v1/dlp/classifications`
  - `GET /api/v1/dlp/labels`
  - `POST /api/v1/dlp/labels`
  - `GET /api/v1/dlp/policies`
  - `POST /api/v1/dlp/policies`
  - `GET /api/v1/dlp/outbound-channels`
  - `POST /api/v1/dlp/outbound-channels`
  - `POST /api/v1/dlp/bindings`
  - `POST /api/v1/dlp/export-jobs`
  - `POST /api/v1/dlp/download-jobs`
  - `POST /api/v1/dlp/external-share-approvals`
  - `POST /api/v1/dlp/outbound-checks`
  - `GET /api/v1/dlp/events`
  - `GET /api/v1/dlp/evidence-packages`

### 写接口安全约束（强制）

- 所有新增写接口必须同时校验：
  - `Idempotency-Key`
  - `X-CSRF-TOKEN`
- 幂等语义：
  - 同 key + 同 payload：返回同一业务结果。
  - 同 key + 不同 payload：返回 `IDEMPOTENCY_CONFLICT`。
- 访问控制语义：
  - 跨租户访问必须拒绝并返回 `CROSS_TENANT_FORBIDDEN`。
- 敏感字段语义：
  - 涉及账号、联系方式、密钥片段等敏感字段时，返回值必须按脱敏规则处理。
  - 审计日志写入禁止记录明文敏感值，仅允许记录脱敏摘要或哈希摘要。
- DLP 语义：
  - 导出、下载、文件外链、Webhook、连接器、插件、AI / 知识库出站必须先经过策略判定。
  - 命中阻断规则时返回明确业务拒绝结果，并写入 `LeakageEvent`。
  - 同一外发申请的审批、执行、审计、证据归档必须可串联到同一个 `EvidencePackage`。

### 兼容与弃用策略

- 旧接口与旧路由保留 6 个月弃用窗口，并标记 Deprecated。
- 弃用窗口内仅做安全修复与关键缺陷修复，不新增功能。
- 窗口结束移除时需同步更新：
  - `docs/contracts.md`
  - 变更日志/发布说明
  - 对应 `Bosch.http` 示例

### 术语收敛与命名兼容（P0/P1/P2 基线补充）

#### 主术语收敛

- `ApplicationCatalog`：平台目录定义对象（原 `AppManifest` 语义归并目标）。
- `TenantApplication`：租户开通关系对象（原 `Tenant-App` 语义归并目标）。
- `TenantAppInstance`：租户应用实例对象（原 `LowCodeApp`/`AiApp` 运行实例语义归并目标）。
- `RuntimeContext`：运行上下文对象（请求上下文、绑定关系、执行前快照）。
- `RuntimeExecution`：运行执行实例对象（状态流转、日志、审计追溯）。

#### v1/v2 命名映射（兼容窗口）

| v1（兼容保留） | v2（目标命名） | 兼容策略 |
|---|---|---|
| `/api/v1/lowcode-apps/*` | `/api/v2/tenant-app-instances/*` | v1 保留 6 个月，响应附弃用提示 |
| `/api/v1/app-manifests/*` | `/api/v2/application-catalogs/*` | v1 保留只读/兼容映射 |
| `/api/v1/runtime/*`（混用） | `/api/v2/runtime-contexts/*` + `/api/v2/runtime-executions/*` | 先并行，后下线 |

#### v2 最小读接口（P0 占位基线）

- `GET /api/v2/application-catalogs`
  - 支持筛选参数：`status`（`Draft/Published/Disabled/Archived`）、`category`、`appKey`。
- `GET /api/v2/application-catalogs/{id}`
- `GET /api/v2/tenant-applications`
  - 支持筛选参数：`status`（`Provisioning/Active/Disabled/Archived`）。
- `GET /api/v2/tenant-applications/{id}`
- `GET /api/v2/tenant-app-instances`
- `GET /api/v2/tenant-app-instances/{id}`
  - `TenantAppInstanceDetail` 新增 `pageCount` 字段，直接返回页面数量，前端不再依赖 `pages.length` 推断。
- `GET /api/v2/runtime-contexts`
- `GET /api/v2/runtime-contexts/{id}`
- `GET /api/v2/runtime-contexts/{appKey}/{pageKey}`
- `GET /api/v2/runtime-executions`
- `GET /api/v2/runtime-executions/{id}`

#### v2 P1 扩展读接口（资源中心与绑定关系）

- `GET /api/v2/tenant-app-instances/data-source-bindings?appIds=1&appIds=2`
  - 用于查询租户应用实例与租户数据源绑定关系。
  - `appIds` 可选；未传时返回当前租户全部实例绑定。
  - 响应字段补充：`bindingId`、`bindingType`、`bindingActive`、`boundAt`、`source`（`BindingTable` / `LegacyLowCodeApp.DataSourceId` / `Unbound`）。
- `GET /api/v2/resource-center/groups`
  - 返回资源中心分组聚合（`catalogs` / `tenant-applications` / `instances` / `releases` / `runtime-contexts` / `runtime-executions` / `datasources` / `audit-summary` / `debug-entries`）。
  - `ResourceCenterGroupEntry` 补充导航关联字段：`navigationPath`、`relatedCatalogId`、`relatedInstanceId`、`relatedReleaseId`、`relatedRuntimeContextId`、`relatedExecutionId`。
  - 返回模型升级为 `ResourceCenterGroupsResponse`：`groups` + `warnings`。
  - 容错语义：当个别应用实例未绑定可用数据源时，接口仍返回 `200`；该实例从 `groups` 聚合结果中跳过，并在 `warnings` 中返回 `appInstanceId/appName/errorCode/message`。
  - 要求服务端采用批量查询 + 内存聚合，禁止循环内数据库访问。
- `GET /api/v2/resource-center/groups/summary`
  - 首页卡片摘要接口，返回 `ResourceCenterGroupsSummaryResponse`：
    - `groups`：仅包含 `groupKey/groupName/total`
    - `warningCount`：告警计数
    - `lastUpdatedAt`：汇总生成时间（ISO 8601）
- `GET /api/v2/resource-center/datasource-consumption`
  - 返回数据源双层消费模型：
    - 平台级数据源（`Platform`）
    - 应用级数据源（`AppScoped`）
    - 未绑定数据源的租户应用实例清单
  - 响应包含每个数据源的绑定应用数量、绑定应用列表与 `bindingRelations`（绑定关系明细：`bindingId/tenantAppInstanceId/dataSourceId/bindingType/isActive/boundAt/updatedAt/source`）。
  - 响应补充治理字段：`isOrphan/isDuplicate/isInvalid/isUnbound/impactScope/repairSuggestion`。
  - 服务端实现必须通过批量查询 + 字典聚合完成，禁止循环内数据库访问。
- `GET /api/v2/resource-center/datasource-consumption/summary`
  - 首页卡片摘要接口，返回 `ResourceCenterDataSourceConsumptionSummaryResponse`：
    - 保留 `Platform/AppScoped/Unbound` 总量与列表
    - 列表项仅包含首页展示所需字段（不返回 `bindingRelations/repairSuggestion` 等明细字段）
    - `lastUpdatedAt`：汇总生成时间（ISO 8601）
- `POST /api/v2/resource-center/datasource-consumption/repair/disable-invalid-binding`
- `POST /api/v2/resource-center/datasource-consumption/repair/switch-primary-binding`
- `POST /api/v2/resource-center/datasource-consumption/repair/unbind-orphan-binding`
  - 三个修复接口统一要求 `Idempotency-Key` + `X-CSRF-TOKEN`。
  - 返回 `ResourceCenterRepairResult`（`action/resourceId/success/message`）。
  - 修复动作需写入审计日志：`resource.datasource-binding.disable-invalid`、`resource.datasource-binding.switch-primary`、`resource.datasource-binding.unbind-orphan`。

#### v2 P1 扩展写接口（TenantAppInstance）

- `POST /api/v2/tenant-app-instances`
- `PUT /api/v2/tenant-app-instances/{id}`
- `POST /api/v2/tenant-app-instances/{id}/publish`
- `DELETE /api/v2/tenant-app-instances/{id}`
- `GET /api/v2/tenant-app-instances/{id}/export`
- `POST /api/v2/tenant-app-instances/import`
- `GET /api/v2/tenant-app-instances/{id}/sharing-policy`
- `PUT /api/v2/tenant-app-instances/{id}/sharing-policy`
- `GET /api/v2/tenant-app-instances/{id}/entity-aliases`
- `PUT /api/v2/tenant-app-instances/{id}/entity-aliases`
- `GET /api/v2/tenant-app-instances/{id}/datasource`
- `POST /api/v2/tenant-app-instances/{id}/datasource/test`
- `GET /api/v2/tenant-app-instances/{id}/file-storage`
- `PUT /api/v2/tenant-app-instances/{id}/file-storage`
  - `PUT /api/v2/tenant-app-instances/{id}` 支持数据源治理字段：
    - `dataSourceId: long?`：绑定或切换目标数据源。
    - `unbindDataSource: bool`：为 `true` 时解绑当前数据源并清空 `dataSourceId`。
  - `GET /file-storage` 返回 `TenantAppFileStorageSettings`：
    - `tenantAppInstanceId/appId`
    - `effectiveBasePath/effectiveMinioBucketName`（当前生效值）
    - `overrideBasePath/overrideMinioBucketName`（应用级覆盖值）
    - `inheritBasePath/inheritMinioBucketName`（是否继承平台）
  - `PUT /file-storage` 请求体 `TenantAppFileStorageSettingsUpdateRequest`：
    - `inheritBasePath/inheritMinioBucketName`
    - `overrideBasePath/overrideMinioBucketName`（继承关闭时必填）
    - `inherit=true` 时服务端删除应用级覆盖配置（恢复平台继承）
  - `PUT /sharing-policy` 与 `PUT /entity-aliases` 的请求体契约沿用 v1：`LowCodeAppSharingPolicyUpdateRequest`、`LowCodeAppEntityAliasesUpdateRequest`。
  - `POST /datasource/test` 返回 `TestConnectionResult`（`success/errorMessage/latencyMs`），应用未绑定数据源时返回 `success=false`。
  - 写接口统一要求 `Idempotency-Key` + `X-CSRF-TOKEN`。
  - `export` 为只读接口，不要求幂等头。
  - `import` 的 `package` 契约沿用 `LowCodeAppExportPackage`，用于兼容窗口内平滑迁移。

#### v1 动态配置中心接口（SystemConfig）

- `GET /api/v1/system-configs?pageIndex=&pageSize=&keyword=`
- `GET /api/v1/system-configs/by-key/{key}?appId=`
- `GET /api/v1/system-configs/query?groupName=&appId=&keys=key1,key2`
- `GET /api/v1/system-configs/feature-flags`
- `POST /api/v1/system-configs`
- `PUT /api/v1/system-configs/{id}`
- `POST /api/v1/system-configs/batch-upsert`
- `DELETE /api/v1/system-configs/{id}`

字段与行为约束：
- `SystemConfigDto` 扩展字段：`appId/groupName/isEncrypted/version/configType/targetJson`。
- `batch-upsert` 请求体：`SystemConfigBatchUpsertRequest`（`items` + 可选 `appId/groupName`）。
- 批量写入必须走批量查询 + 批量写，禁止循环内数据库往返。
- 写接口统一要求 `Idempotency-Key` + `X-CSRF-TOKEN`。

#### v2 P0 应用成员与应用角色接口（App-level Isolation）

- 应用成员（`UseSharedUsers=false` 时启用）：
  - `GET /api/v2/tenant-app-instances/{appId}/members`
  - `GET /api/v2/tenant-app-instances/{appId}/members/{userId}`
  - `POST /api/v2/tenant-app-instances/{appId}/members`
  - `PUT /api/v2/tenant-app-instances/{appId}/members/{userId}/roles`
  - `DELETE /api/v2/tenant-app-instances/{appId}/members/{userId}`
  - 授权策略：GET 要求 `apps:members:view`，写接口要求 `apps:members:update`
- 应用角色（`UseSharedRoles=false` 时启用）：
  - `GET /api/v2/tenant-app-instances/{appId}/roles`
  - `GET /api/v2/tenant-app-instances/{appId}/roles/governance-overview`
    - 返回 `TenantAppRoleGovernanceOverview`（总角色数、系统/自定义角色数、成员覆盖数、权限覆盖率、角色治理项列表）。
  - `GET /api/v2/tenant-app-instances/{appId}/roles/{roleId}`
  - `POST /api/v2/tenant-app-instances/{appId}/roles`
  - `PUT /api/v2/tenant-app-instances/{appId}/roles/{roleId}`
  - `PUT /api/v2/tenant-app-instances/{appId}/roles/{roleId}/permissions`
  - `DELETE /api/v2/tenant-app-instances/{appId}/roles/{roleId}`
  - 授权策略：GET 要求 `apps:roles:view`，写接口要求 `apps:roles:update`

约束说明：

- 当应用启用共享用户/共享角色时，对应接口返回 `VALIDATION_ERROR`。
- 应用工作台中 `app:user` / `app:admin` 受保护接口会触发成员中间件校验：
  - `UseSharedUsers=false` 且用户未入组时返回 `FORBIDDEN`。
  - 平台管理员/系统管理员允许绕过成员校验。
- 成员与角色绑定操作必须采用批量查询与批量写入，禁止在循环内数据库操作。

#### v2 组织管理聚合接口（Organization Workspace）

- 聚合工作区：
  - `GET /api/v2/tenant-app-instances/{appId}/organization/workspace?pageIndex=1&pageSize=20&keyword=&roleId=`
  - `roleId` 可选；传入后成员分页仅返回该角色下成员（用于角色授权中心成员 Tab 的服务端分页）
  - 返回 `AppOrganizationWorkspaceResponse`：
    - `members`：成员分页数据（`PagedResult<TenantAppMemberListItem>`）
    - `roleGovernance`：角色治理概览（`TenantAppRoleGovernanceOverview`）
    - `roles/departments/positions/projects`：组织分类全量列表（用于左侧导航与弹窗选择）
- 成员管理（统一入口）：
  - `POST /api/v2/tenant-app-instances/{appId}/organization/members`
  - `POST /api/v2/tenant-app-instances/{appId}/organization/members/users`（新建账号并加入应用，字段校验与平台级 `POST /api/v1/users` 一致，支持 `projectIds` 多选分配）
  - `PUT /api/v2/tenant-app-instances/{appId}/organization/members/{userId}/profile`（编辑成员姓名/邮箱/手机号/状态）
  - `PUT /api/v2/tenant-app-instances/{appId}/organization/members/{userId}/roles`（保留原路径，入参扩展为 `roleIds + projectIds`）
  - `POST /api/v2/tenant-app-instances/{appId}/organization/members/{userId}/reset-password`（管理员重置成员密码）
  - `DELETE /api/v2/tenant-app-instances/{appId}/organization/members/{userId}`
- 角色管理（统一入口）：
  - `POST /api/v2/tenant-app-instances/{appId}/organization/roles`
  - `PUT /api/v2/tenant-app-instances/{appId}/organization/roles/{roleId}`
  - `DELETE /api/v2/tenant-app-instances/{appId}/organization/roles/{roleId}`
- 部门管理（统一入口）：
  - `POST /api/v2/tenant-app-instances/{appId}/organization/departments`
  - `PUT /api/v2/tenant-app-instances/{appId}/organization/departments/{id}`
  - `DELETE /api/v2/tenant-app-instances/{appId}/organization/departments/{id}`
- 职位管理（统一入口）：
  - `POST /api/v2/tenant-app-instances/{appId}/organization/positions`
  - `PUT /api/v2/tenant-app-instances/{appId}/organization/positions/{id}`
  - `DELETE /api/v2/tenant-app-instances/{appId}/organization/positions/{id}`
- 项目管理（统一入口）：
  - `POST /api/v2/tenant-app-instances/{appId}/organization/projects`
  - `PUT /api/v2/tenant-app-instances/{appId}/organization/projects/{id}`
  - `DELETE /api/v2/tenant-app-instances/{appId}/organization/projects/{id}`
- 鉴权策略：
  - `workspace` 要求 `apps:view`
  - 成员写接口要求 `apps:members:update`
  - 角色/部门/职位/项目写接口要求 `apps:roles:update`
- 导航授权定版：
  - 当前阶段“角色菜单/导航授权”统一使用应用页面分配接口（`/roles/available-pages` + `/roles/{roleId}/pages`）。
  - 不新增独立 app-level menu 表，避免与 permission/page 双模型冲突。
  - 如后续要支持目录分组、隐藏页、外链，可新增轻量 `AppNavigationNode` 作为导航投影层（不改变角色授权主模型）。
- 入参规范：
  - 统一入口写接口中的实体 ID 使用字符串表达（前端避免 64 位整型精度问题），服务层强类型转换校验。

> 说明：原 `/members`、`/roles`、`/departments`、`/positions`、`/projects` 分散接口仍保留兼容能力，但组织管理工作台默认使用 `/organization/*` 聚合入口。

#### v2 P2 发布闭环接口（首批）

- `GET /api/v2/release-center/releases`
  - 支持筛选参数：`status`（`Pending/Released/RolledBack`）、`appKey`、`manifestId`。
- `GET /api/v2/release-center/releases/{releaseId}`
- `GET /api/v2/release-center/releases/{releaseId}/diff`
  - 返回 `ReleaseDiffSummary`：`baselineReleaseId`、`addedCount/removedCount/changedCount` 与字段路径列表（`addedKeys/removedKeys/changedKeys`）。
- `GET /api/v2/release-center/releases/{releaseId}/impact`
  - 返回 `ReleaseImpactSummary`：运行路由数量、激活路由数量、运行上下文数量、近 24 小时执行次数、运行中/失败执行数量。
- `POST /api/v2/release-center/releases/{releaseId}/rollback`
  - 回滚请求按写接口统一要求 `Idempotency-Key` + `X-CSRF-TOKEN`。
  - 服务端必须基于当前租户上下文校验发布记录归属后再执行回滚。
  - 回滚响应返回 `ReleaseRollbackResult`（目标版本、原版本、是否发生切换、重绑路由数量、结果说明）。
  - 回滚后要求同步落库：
    - 原当前版本标记 `RolledBack`
    - 目标版本切回 `Released`
    - `RuntimeRoute` 按目标发布版本重绑 `SchemaVersion`
    - 审计记录写入 `release.rollback`、`release.switch` 与 `runtime.route.rebind`
- `GET /api/v2/runtime-executions/{executionId}/audit-trails`
  - 通过执行ID关联审计轨迹，支持分页与关键字检索。
  - 审计目标匹配口径包含：`WorkflowExecution:{executionId}`、`RuntimeExecution:{executionId}`，以及执行记录上的 `ReleaseId` / `RuntimeContextId` / `AppId` 派生目标（`Release:*`、`RuntimeContext:*`、`AppManifest:*`）。
- `GET /api/v2/runtime-executions`
  - 支持多条件筛选参数：`appId`、`status`、`startedFrom`、`startedTo`（ISO8601）。
  - `status` 取值口径与 `ExecutionStatus` 对齐（`Pending/Running/Completed/Failed/Cancelled/Interrupted`）。
- `POST /api/v2/runtime-executions/{executionId}/cancel`
- `POST /api/v2/runtime-executions/{executionId}/retry`
- `POST /api/v2/runtime-executions/{executionId}/resume`
- `POST /api/v2/runtime-executions/{executionId}/debug`
  - 写接口统一要求 `Idempotency-Key` + `X-CSRF-TOKEN`。
  - 统一返回 `RuntimeExecutionOperationResult`（`action/executionId/status/message/newExecutionId`）。
  - 审计动作：`runtime.execution.cancel`、`runtime.execution.retry`、`runtime.execution.resume`、`runtime.execution.debug`。
- `GET /api/v2/runtime-executions/{executionId}/timeout-diagnosis`
  - 返回 `RuntimeExecutionTimeoutDiagnosis`（耗时、风险标记、诊断结论、建议列表）。
- `GET /api/v2/coze-mappings/overview`
  - 返回 Coze 六层映射总览（目录/实例/发布/上下文/执行/审计）。
- `GET /api/v2/debug-layer/embed-metadata`
  - 返回调试层嵌入元数据（tenant/app/project + 资源权限列表）。
  - 访问策略：接口需 `debug:view`；资源项按当前用户已授权权限动态裁剪（`debug:view` / `debug:run` / `debug:manage`）。
- `GET /api/v2/migration-governance/overview`
  - 返回迁移治理指标总览（`legacyRouteHits`、`rewriteHits`、`notFoundCount/notFoundRate`、`fallbackCount`、`v1EntryHits`、`v2EntryHits`、`newEntryCoverageRate`）。
  - 指标由 `ApiVersionRewriteMiddleware` 运行时采集，覆盖窗口起点由服务启动时间定义（`windowStartedAt`）。

#### 前端主路径约定与弃用窗口（SEC-92）

| 主路径（规范） | 兼容路径（Deprecated） | 弃用窗口 | 备注 |
|---|---|---|---|
| `/console/*` | `/settings/*`、`/system/*` | 6 个月 | 平台控制台主入口 |
| `/apps/:appId/*` | `/ai/*`、`/lowcode/*`、`/workflow/*` | 6 个月 | 应用工作台主入口，编辑页需下沉 |
| `/r/:appKey/:pageKey` | `/apps/:appId/run/:pageKey` | 6 个月 | 运行交付面主入口 |

#### App Workspace 编辑入口收敛清单（2026-03-17）

| 能力域 | 规范路径 | 兼容路径（Deprecated） |
|---|---|---|
| Agent | `/apps/:appId/agents`、`/apps/:appId/agents/:id/edit` | `/ai/agents`、`/ai/agents/:id/edit` |
| Workflow | `/apps/:appId/workflows`、`/apps/:appId/workflows/:id/editor` | `/workflow`、`/workflow/:id/editor`、`/ai/workflows`、`/ai/workflows/:id/edit` |
| Prompt | `/apps/:appId/prompts` | `/ai/prompts` |
| PluginConfig | `/apps/:appId/plugins`、`/apps/:appId/plugins/:id`、`/apps/:appId/plugins/:id/apis/:apiId` | `/ai/plugins`、`/ai/plugins/:id`、`/ai/plugins/:id/apis/:apiId` |

- 兼容路径必须返回可追踪的迁移提示（页面提示或响应头提示），禁止直接返回 404。
- 兼容窗口内仅允许新增 redirect 与安全修复，不允许在兼容路径上新增业务功能。
- 兼容窗口结束后移除旧路径时，必须同步更新 `router`、`titleKey`、`.http` 与发布说明。

#### 迁移卡 Done 与观测口径（治理约束）

- Done 最低标准：
  - 主路径可访问、兼容路径可重定向、关键菜单高亮正确。
  - 页面内部返回链路回到 `/apps/:appId/*`，不再写死 legacy 路径。
  - 至少完成一次 `dotnet build` 与 `npm run build` 验证。
- 观测指标建议：
  - Redirect 命中率：legacy -> 新路径命中比例。
  - 404 率：迁移相关路径 404 数量。
  - 入口收敛率：新入口 `/apps/:appId/*` 访问占比。
  - 回退率：legacy 路径直接访问占比。

#### 迁移联调顺序与禁止事项（SEC-94）

1. `contracts` 先行：先改文档契约，再改后端实现，再改前端调用。
2. 接口并行：先提供 v2 读接口，再迁移前端主读链路，最后处理 v1 下线。
3. 入口迁移：先补 redirect，再迁页面入口，最后清理 legacy 菜单与文案。
4. 运行闭环：先打通发布/运行/审计主链路，再补调试层入口与权限细节。

禁止事项：

- 禁止前端先切换到尚未落地的 v2 写接口。
- 禁止删除 v1 接口或 legacy 路由后再补 redirect。
- 禁止在兼容窗口内修改旧接口响应语义导致联调回归失败。
- 禁止在循环内执行数据库查询/更新以拼装迁移聚合数据。

#### Runtime 分层契约约束

- 运行态接口不得继续使用 `Runtime` 裸词承载定义态与执行态双语义。
- 设计态对象（如 `WorkflowDefinition`）与运行态对象（`RuntimeExecution`）必须在契约中分开定义。
- Trace/Audit 链路必须可通过 `tenantId + appId + executionId` 反查。

## LowCodeApp 扩展字段与应用设置 API（Sprint 2）

### `LowCodeApp` 扩展字段

- `dataSourceId: string | null`（创建时可设置，后续只读）
- `useSharedUsers: boolean`
- `useSharedRoles: boolean`
- `useSharedDepartments: boolean`

### 应用设置 API

- `GET /api/v1/lowcode-apps/{id}/sharing-policy`
- `PUT /api/v1/lowcode-apps/{id}/sharing-policy`
- `GET /api/v1/lowcode-apps/{id}/entity-aliases`
- `PUT /api/v1/lowcode-apps/{id}/entity-aliases`
- `GET /api/v1/lowcode-apps/{id}/datasource`
- `POST /api/v1/lowcode-apps/{id}/datasource/test`

## 幂等与 Anti-Forgery

### 幂等（Idempotency-Key）

- 服务端唯一键：`tenant_id + user_id + api_name + idempotency_key`。
- 首次请求成功后保存处理结果（状态 + 资源ID/响应摘要）；重复请求返回相同业务结果。
- 同一幂等键但 payload 不一致返回 `IDEMPOTENCY_CONFLICT`。
- 幂等记录保留 N 小时/天后过期（按配置清理）。

### Anti-Forgery Token

- 获取方式：`GET /api/v1/secure/antiforgery`（需登录）。
- 请求头：`X-CSRF-TOKEN`。
- 校验失败返回 `ANTIFORGERY_TOKEN_INVALID`。
## 通用响应模型

### ApiResponse

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "OK",
  "traceId": "00-abc123...",
  "data": {}
}
```

字段说明：

- `success`：是否成功。
- `code`：错误码或成功码。
- `message`：错误信息或 OK。
- `traceId`：请求链路追踪 ID。
- `data`：业务数据，失败时为 `null`。

### 错误码

- `SUCCESS`：成功
- `VALIDATION_ERROR`：参数校验错误
- `UNAUTHORIZED`：未认证
- `FORBIDDEN`：无权限
- `NOT_FOUND`：资源不存在
- `SERVER_ERROR`：服务端错误
- `ACCOUNT_LOCKED`：账号锁定
- `PASSWORD_EXPIRED`：密码过期
- `TENANT_NOT_FOUND`：租户不存在
- `PROJECT_REQUIRED`：项目必填
- `PROJECT_NOT_FOUND`：项目不存在
- `PROJECT_DISABLED`：项目已停用
- `PROJECT_FORBIDDEN`：当前用户无项目访问权限
- `APP_CONTEXT_REQUIRED`：缺少应用上下文
- `INVALID_CREDENTIALS`：账号或密码错误
- `TOKEN_EXPIRED`：令牌过期
- `IDEMPOTENCY_REQUIRED`：缺少幂等键
- `IDEMPOTENCY_CONFLICT`：幂等键冲突
- `IDEMPOTENCY_IN_PROGRESS`：幂等键处理中
- `ANTIFORGERY_TOKEN_INVALID`：CSRF 校验失败
- `CROSS_TENANT_FORBIDDEN`：跨租户访问被拒绝
- `SENSITIVE_DATA_POLICY_VIOLATION`：敏感数据策略违规

## 分页模型

### PagedRequest

```json
{
  "pageIndex": 1,
  "pageSize": 10,
  "keyword": "search",
  "sortBy": "createdAt",
  "sortDesc": true
}
```

### 分页参数命名冻结（2026-03 封板）

- OpenAPI 契约统一使用 `PageIndex` / `PageSize` 作为 Query 参数名（与后端 `PagedRequest` 对齐）。
- 前端与第三方新接入方应优先使用 `PageIndex` / `PageSize`。
- 为兼容历史调用，服务端继续接受 `pageIndex` / `pageSize`（大小写不敏感绑定）。

## 认证与授权契约

### 登录

`POST /api/v1/auth/token`

请求（需同时携带 `X-Tenant-Id` 请求头）：

```json
{
  "username": "admin",
  "password": "P@ssw0rd!"
}
```

响应（`ApiResponse` 包装）：

```json
{
  "accessToken": "jwt-access-token",
  "expiresAt": "2026-01-30T10:00:00Z",
  "refreshToken": "refresh-token",
  "refreshExpiresAt": "2026-01-30T22:00:00Z",
  "sessionId": 10010001
}

JWT Claims（新增）：

- `sid`：会话 ID
- `jti`：访问令牌唯一标识
- `app_id`：应用标识（用于租户 + 应用维度的 ID 生成）
- `client_type`：客户端类型（`WebH5`/`Mobile`/`Backend`）
- `client_platform`：客户端平台（`Web`/`Android`/`iOS`）
- `client_channel`：客户端通道（`Browser`/`App`）
- `client_agent`：客户端代理（`Chrome`/`Edge`/`Safari`/`Firefox`/`Other`）
```

### 刷新令牌（使用当前登录态）

`POST /api/v1/auth/refresh`

请求（需携带 `X-Tenant-Id`）：

```json
{
  "refreshToken": "refresh-token"
}
```

响应（`ApiResponse` 包装）：

```json
{
  "accessToken": "new-jwt-access-token",
  "expiresAt": "2026-01-30T11:00:00Z",
  "refreshToken": "new-refresh-token",
  "refreshExpiresAt": "2026-01-30T23:00:00Z",
  "sessionId": 10010001
}
```

### 当前用户

`GET /api/v1/auth/me`

响应（`ApiResponse` 包装）：

```json
{
  "id": "1001",
  "username": "admin",
  "displayName": "系统管理员",
  "tenantId": "00000000-0000-0000-0000-000000000001",
  "roles": ["Admin"],
  "permissions": ["workflow:design", "system:admin", "users:view", "roles:view"],
  "clientContext": {
    "clientType": "WebH5",
    "clientPlatform": "Web",
    "clientChannel": "Browser",
    "clientAgent": "Chrome"
  }
}
```

认证校验补充：

- 访问令牌在每次请求都会校验用户状态（`IsActive=true`）。
- 若用户被禁用，已签发 access token 在下一次请求即失效（返回 401）。
- 同时校验会话 `sid` 是否存在、是否已撤销、是否过期。

### 获取个人资料

`GET /api/v1/auth/profile`

响应（`ApiResponse` 包装）：

```json
{
  "displayName": "系统管理员",
  "email": "admin@atlas.local",
  "phoneNumber": "13800000000"
}
```

### 更新个人资料

`PUT /api/v1/auth/profile`

请求（需携带 `Authorization` 与 `X-Tenant-Id`）：

```json
{
  "displayName": "系统管理员",
  "email": "admin@atlas.local",
  "phoneNumber": "13800000000"
}
```

响应：通用 `ApiResponse`

### 修改密码

`PUT /api/v1/auth/password`

请求（需携带 `Authorization` 与 `X-Tenant-Id`）：

```json
{
  "currentPassword": "OldP@ssw0rd!",
  "newPassword": "NewP@ssw0rd!2026",
  "confirmPassword": "NewP@ssw0rd!2026"
}
```

响应：通用 `ApiResponse`

### 注销

`POST /api/v1/auth/logout`

请求：无（需携带 `Authorization` 与 `X-Tenant-Id`）

响应：通用 `ApiResponse`

## 租户数据源契约

### 管理接口

- `GET /api/v1/tenant-datasources`：查询数据源列表
- `GET /api/v1/tenant-datasources/drivers`：查询驱动定义与可视化字段元数据
- `POST /api/v1/tenant-datasources`：新增数据源
- `PUT /api/v1/tenant-datasources/{id}`：更新数据源
- `DELETE /api/v1/tenant-datasources/{id}`：删除数据源
- `POST /api/v1/tenant-datasources/test`：测试数据源连接
- `POST /api/v1/tenant-datasources/{id}/test`：测试已保存数据源连接（无需回传明文连接串）

### 数据源类型

- `SQLite`
- `SqlServer`
- `MySql`
- `PostgreSQL`
- `Oracle`
- `Dm`
- `Kdbndp`
- `Oscar`
- `Access`

说明：

- 所有驱动均支持 `mode=raw`（直接连接字符串）。
- 支持可视化模式的驱动可使用 `mode=visual` + `visualConfig` 组装连接串。
- 推荐通过 `GET /api/v1/tenant-datasources/drivers` 获取前端动态表单字段，避免硬编码。

### 测试连接

`POST /api/v1/tenant-datasources/test`

请求示例：

```json
{
  "connectionString": "Host=127.0.0.1;Port=5432;Database=atlas;Username=postgres;Password=postgres",
  "dbType": "PostgreSQL",
  "mode": "raw"
}
```

响应示例：

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "OK",
  "traceId": "00-...",
  "data": {
    "success": true,
    "errorMessage": null,
    "latencyMs": 12
  }
}
```

### 更新数据源（连接串可选）

`PUT /api/v1/tenant-datasources/{id}`

说明：

- `connectionString` 允许留空（或不传），表示保持现有密文连接串不变，仅更新名称/类型/池参数。
- 若传入 `connectionString`，服务端按配置重新加密后覆盖存储。
- 当 `mode=visual` 时，服务端使用 `visualConfig` 组装连接串并加密存储（`connectionString` 可为空）。

## 应用数据库迁移契约（补充）

- `POST /api/v1/app-migrations`：创建迁移任务（`appInstanceId` 为字符串，避免长整型精度丢失）
- `POST /api/v1/app-migrations/repair-primary-binding`：显式修复应用实例主数据源绑定（混合模式）
- `POST /api/v1/app-migrations/{id}/reset`：失败任务重置为可重试状态（仅 `Failed` 状态可执行）

### SQLite 应用库结构自修复（迁移执行阶段）

- 当目标应用数据源为 **SQLite** 时，执行 `POST .../start` 会在数据同步前对应用库内应用域表做 **按需** 结构对齐：检测历史上错误建成 `NOT NULL` 或缺列的字段，与当前实体定义不一致时 **整表按 ORM 重建并回灌数据**（与主库 `DatabaseInitializerHostedService` 共用同一套规则入口）。
- 首批覆盖表：`AppRole`（如 `DeptIds`）、`AppDepartment`（`ParentId`）、`AppPermission` / `AppPosition` / `AppProject`（`Description` 等可空列）等。
- 任务实体与进度快照包含 **`schemaRepairLog`**（文本摘要），列表 `GET /api/v1/app-migrations`、详情 `GET /api/v1/app-migrations/{id}`、进度 `GET .../progress` 均可能返回该字段，供控制台「结构自修复」列展示。
- 迁移失败若由 SQLite 约束引起（如 `NOT NULL constraint failed`），`errorSummary` 中会附带可读的列定位与重试/结构对齐提示。

**回归场景（手工验证建议）：**

1. 应用库为历史 SQLite，`AppRole.DeptIds` 列为 `NOT NULL`：执行迁移 `start` 后应完成结构对齐（`schemaRepairLog` 含 `AppRole`），数据复制不因 `DeptIds` 中断。
2. 应用库为历史 SQLite，`AppDepartment.ParentId` 为 `NOT NULL`：同上，对齐后根部门复制使用自引用策略，任务可继续。
3. 多租户下并发创建/执行迁移任务：各租户任务独立；同一应用实例失败 `reset` 后再次 `start`，结构阶段应幂等（已对齐表不重复破坏数据）。

请求示例（修复主绑定）：

```json
{
  "appInstanceId": "1482690002860118000"
}
```

响应示例：

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "OK",
  "traceId": "00-...",
  "data": {
    "appInstanceId": "1482690002860118000",
    "dataSourceId": "1482690002868506624",
    "repaired": true,
    "message": "主数据源绑定修复成功。"
  }
}
```

## 通知公告契约

### 用户侧

- `GET /api/v1/notifications/inbox`：我的收件箱（支持 `isRead` 过滤）
- `GET /api/v1/notifications/unread-count`：未读数量
- `PUT /api/v1/notifications/{id}/read`：单条标记已读
- `PUT /api/v1/notifications/read-all`：全部标记已读

### 管理侧

- `GET /api/v1/notifications/manage`：公告管理列表
- `POST /api/v1/notifications/manage`：发布公告
- `PUT /api/v1/notifications/manage/{id}`：编辑公告
- `PUT /api/v1/notifications/manage/{id}/revoke`：撤回公告（置为非激活）
- `DELETE /api/v1/notifications/manage/{id}`：删除公告

说明：

- `noticeType` 统一返回为 `Announcement | System | Reminder`。
- 历史输入值（如 `1/2`、中文“通知/公告”）由服务端归一化为上述标准枚举。
- 阅读行为会记录审计事件 `NOTIFICATION_READ`。

## 角色、权限与菜单契约

### 角色列表（分页）

`GET /api/v1/roles?pageIndex=1&pageSize=10&keyword=管理员&isSystem=true`

响应：

```json
{
  "items": [
    {
      "id": "1",
      "name": "管理员",
      "code": "Admin",
      "description": "系统内置角色",
      "isSystem": true
    }
  ],
  "total": 1,
  "pageIndex": 1,
  "pageSize": 10
}
```

说明：

- `isSystem`：可选，`true`=系统内置，`false`=自定义。
- 删除约束：角色已被用户绑定时不允许删除，返回 `VALIDATION_ERROR`，需先解绑用户与角色关系。
- 角色权限生效规则：后端每次鉴权都会实时按当前 `用户-角色-权限` 关系解析，不依赖令牌中的角色快照，角色调整后无需重新登录。

### 角色详情

`GET /api/v1/roles/{id}`

响应：

```json
{
  "id": "1",
  "name": "管理员",
  "code": "Admin",
  "description": "系统内置角色",
  "isSystem": true,
  "dataScope": 1,
  "permissionIds": [101, 102],
  "menuIds": [201, 202]
}
```

说明：

- `dataScope` 数据范围枚举：
  - `1`：全部数据（当前租户）
  - `2`：自定义部门
  - `3`：本部门
  - `4`：本部门及下级
  - `5`：仅本人
  - `6`：项目维度

### 设置角色数据范围

`PUT /api/v1/roles/{id}/data-scope`

请求体：

```json
{
  "dataScope": 4
}
```

响应：通用 `ApiResponse`

### 权限列表（分页）

`GET /api/v1/permissions?pageIndex=1&pageSize=10&keyword=workflow&type=Api`

响应：

```json
{
  "items": [
    {
      "id": "1",
      "code": "workflow:design",
      "name": "工作流设计",
      "type": "Api",
      "description": "流程配置"
    }
  ],
  "total": 1,
  "pageIndex": 1,
  "pageSize": 10
}
```

说明：

- `type`：可选，支持 `Api`、`Menu`、`Application`、`Page`、`Action`。

### 菜单列表（分页）

`GET /api/v1/menus?pageIndex=1&pageSize=10&keyword=system&isHidden=false`

响应：

```json
{
  "items": [
    {
      "id": "10",
      "name": "系统管理",
      "path": "/system",
      "parentId": null,
      "sortOrder": 0,
      "component": "Layout",
      "icon": "settings",
      "permissionCode": "system:admin",
      "isHidden": false
    }
  ],
  "total": 1,
  "pageIndex": 1,
  "pageSize": 10
}
```

说明：

- `isHidden`：可选，`true`=仅隐藏，`false`=仅显示。

### 菜单全量

`GET /api/v1/menus/all`

响应：

```json
[
  {
    "id": "10",
    "name": "系统管理",
    "path": "/system",
    "parentId": null,
    "sortOrder": 0,
    "component": "Layout",
    "icon": "settings",
    "permissionCode": "system:admin",
    "isHidden": false
  }
]
```

### 权限码清单（默认）

- `system:admin`：系统管理员
- `workflow:design`：工作流设计器
- `users:view`：用户查看
- `users:create`：用户新增
- `users:update`：用户更新
- `users:assign-roles`：用户分配角色
- `users:assign-departments`：用户分配部门
- `roles:view`：角色查看
- `roles:create`：角色新增
- `roles:update`：角色更新
- `roles:assign-permissions`：角色分配权限
- `roles:assign-menus`：角色分配菜单
- `permissions:view`：权限查看
- `permissions:create`：权限新增
- `permissions:update`：权限更新
- `departments:view`：部门查看
- `departments:all`：部门全量
- `departments:create`：部门新增
- `departments:update`：部门更新
- `menus:view`：菜单查看
- `menus:all`：菜单全量
- `menus:create`：菜单新增
- `menus:update`：菜单更新
- `apps:view`：应用配置查看
- `apps:update`：应用配置更新
- `apps:members:view`：应用成员查看
- `apps:members:update`：应用成员维护
- `apps:roles:view`：应用角色查看
- `apps:roles:update`：应用角色维护
- `projects:view`：项目查看
- `projects:create`：项目新增
- `projects:update`：项目更新
- `projects:delete`：项目删除
- `projects:assign-users`：项目分配人员
- `projects:assign-departments`：项目分配部门
- `projects:assign-positions`：项目分配岗位
- `audit:view`：审计查看
- `assets:view`：资产查看
- `assets:create`：资产新增
- `alert:view`：告警查看
- `approval:flow:view`：审批流查看
- `approval:flow:create`：审批流创建
- `approval:flow:update`：审批流更新
- `approval:flow:publish`：审批流发布
- `approval:flow:delete`：审批流删除
- `approval:flow:disable`：审批流停用
- `visualization:view`：可视化查看
- `visualization:process:save`：可视化流程保存
- `visualization:process:update`：可视化流程更新
- `visualization:process:publish`：可视化流程发布
- `notification:view`：通知查看
- `notification:create`：通知创建
- `notification:update`：通知更新
- `notification:delete`：通知删除
- `file:upload`：文件上传
- `file:download`：文件下载
- `file:delete`：文件删除

字段说明：

- `pageIndex`：页码，从 1 开始。
- `pageSize`：每页数量。
- `keyword`：关键字检索。
- `sortBy`：排序字段。
- `sortDesc`：是否降序。

### PagedResult

```json
{
  "items": [],
  "total": 100,
  "pageIndex": 1,
  "pageSize": 10
}
```

字段说明：

- `items`：当前页数据。
- `total`：总数量。
- `pageIndex`：页码。
- `pageSize`：每页数量。

## 示例：分页响应包装

## 审批流设计器契约

### 审批流定义（ApprovalFlowDefinition）

字段说明（响应）：

- `id`：流程定义 ID
- `name`：流程名称
- `definitionJson`：流程定义 JSON（设计器保存结构）
- `version`：版本号
- `status`：状态
- `publishedAt`：发布时间
- `publishedByUserId`：发布人
- `category`：流程分类
- `description`：流程说明
- `visibilityScopeJson`：可见范围配置 JSON
- `isQuickEntry`：是否快捷入口

### DefinitionJson 结构（新格式）

```json
{
  "meta": {
    "flowName": "采购申请审批",
    "description": "采购类流程",
    "category": "采购",
    "visibilityScope": {
      "scopeType": "Department",
      "departmentIds": [10, 11]
    },
    "isQuickEntry": false,
    "isLowCodeFlow": true
  },
  "lfForm": {
    "formJson": { "widgetList": [], "formConfig": {} },
    "formFields": [
      {
        "fieldId": "input_1",
        "fieldName": "金额",
        "fieldType": "input-number",
        "valueType": "Number",
        "options": []
      }
    ]
  },
  "nodes": {
    "rootNode": {
      "nodeId": "start_1",
      "nodeType": "start",
      "nodeName": "发起人",
      "childNode": {
        "nodeId": "approve_1",
        "nodeType": "approve",
        "nodeName": "部门负责人",
        "approverConfig": {
          "setType": 1,
          "signType": 1,
          "noHeaderAction": 0,
          "nodeApproveList": [
            { "targetId": "role-manager", "name": "部门经理" }
          ]
        },
        "childNode": {
          "nodeId": "end_1",
          "nodeType": "end",
          "nodeName": "结束"
        }
      }
    }
  }
}
```

### 节点模型说明

- `nodeType` 支持：`start`、`approve`、`copy`、`condition`、`dynamicCondition`、`parallelCondition`、`parallel`、`end`  
- `conditionNodes`：条件分支数组（每个分支含 `branchName`、`conditionRule`、`childNode`、`isDefault`）  
- `parallelNodes`：并行审批分支数组  
- `approverConfig`：审批人设置（setType/signType/noHeaderAction/人员列表）  
- `buttonPermissionConfig`：按钮权限（发起页/审批页/查看页）  
- `formPermissionConfig`：表单字段权限（R/E/H）  
- `noticeConfig`：通知设置（渠道与模板）

### 可见范围（VisibilityScope）

```json
{
  "scopeType": "All|Department|Role|User",
  "departmentIds": [1, 2],
  "roleCodes": ["Manager", "Admin"],
  "userIds": [100, 200]
}
```

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "OK",
  "traceId": "00-abc123...",
  "data": {
    "items": [],
    "total": 0,
    "pageIndex": 1,
    "pageSize": 10
  }
}
```

## 审批实例与任务契约

### 审批实例

- `POST /api/v1/approval/instances`：发起流程实例
- `GET /api/v1/approval/instances/my`：我发起的流程实例
- `GET /api/v1/approval/instances/{id}`：流程实例详情
- `GET /api/v1/approval/instances/{id}/history`：流程实例历史
- `GET /api/v1/approval/instances/{id}/history/export`：导出流程历史（CSV）
- `POST /api/v1/approval/instances/{id}/cancellation`：取消流程实例
- `POST /api/v1/approval/instances/{id}/operations`：实例操作（撤回/转办/加签等）
- `GET /api/v1/approval/instances/{id}/preview`：预览流程实例
- `GET /api/v1/approval/instances/{id}/print-view`：打印视图

`GET /api/v1/approval/instances/{id}` 返回 `ApprovalInstanceDetailDto`，其中 `status` 使用数值枚举：

- `-3`：已作废（Destroy）
- `-2`：已挂起（Suspended）
- `-1`：草稿（Draft）
- `0`：运行中（Running）
- `1`：已完成（Completed）
- `2`：已驳回（Rejected）
- `3`：已取消（Canceled）
- `4`：超时结束（TimedOut）
- `5`：强制终止（Terminated）
- `6`：自动通过（AutoApproved）
- `7`：自动拒绝（AutoRejected）
- `8`：AI 处理中（AiProcessing）
- `9`：AI 转人工（AiManualReview）

`GET /api/v1/approval/instances/{id}/history` 返回 `PagedResult<ApprovalHistoryEventDto>`，其中 `eventType` 为字符串枚举（如 `InstanceStarted`、`TaskApproved`、`TaskRejected`、`InstanceCompleted`、`InstanceSuspended` 等）。

### 审批任务

- `GET /api/v1/approval/tasks/my`：我的待办任务
- `GET /api/v1/approval/instances/{instanceId}/tasks`：实例内任务列表
- `POST /api/v1/approval/tasks/{taskId}/decision`：任务审批（`approved=true|false`）

## 运维监控与合规取证契约

- `GET /api/v1/monitor/server-info`：服务监控快照（CPU/内存/磁盘/运行时）
- `GET /api/v1/monitor/compliance/evidence-package`：导出等保证据包（zip）
  - 鉴权：`system:admin`
  - 响应：`application/zip` 文件流（非 `ApiResponse` 包装）
  - 默认证据项：合规映射文档、核心配置快照、取证 `.http` 样例、健康与任务取证脚本

## 可视化模块契约

### 流程概览

`GET /api/v1/visualization/overview`

响应数据：

```json
{
  "totalProcesses": 0,
  "runningInstances": 0,
  "blockedNodes": 0,
  "alertsToday": 0,
  "riskHints": []
}
```

### 流程列表与详情

`GET /api/v1/visualization/processes?pageIndex=1&pageSize=10`

```json
{
  "items": [
    {
      "id": "1",
      "name": "示例流程",
      "version": 1,
      "status": "Draft",
      "publishedAt": "2026-01-30T10:00:00Z"
    }
  ],
  "total": 0,
  "pageIndex": 1,
  "pageSize": 10
}
```

`GET /api/v1/visualization/processes/{id}`

```json
{
  "id": "1",
  "name": "示例流程",
  "version": 1,
  "status": "Draft",
  "publishedAt": "2026-01-30T10:00:00Z",
  "definitionJson": "{ \"nodes\": [], \"edges\": [] }"
}
```

### 保存草稿 / 发布

`POST /api/v1/visualization/processes`

```json
{
  "processId": "1",
  "version": 1,
  "status": "Draft"
}
```

`POST /api/v1/visualization/processes/{id}/publication`

```json
{
  "processId": "1",
  "version": 1,
  "status": "Published"
}
```

### 运行态实例

`GET /api/v1/visualization/instances?pageIndex=1&pageSize=10&processId=1&status=Running`

```json
{
  "items": [
    {
      "id": "1001",
      "flowName": "示例流程",
      "status": "Running",
      "currentNode": "审批节点",
      "startedAt": "2026-01-30T10:00:00Z",
      "durationMinutes": 35
    }
  ],
  "total": 0,
  "pageIndex": 1,
  "pageSize": 10
}
```

### 指标聚合

`GET /api/v1/visualization/metrics`

```json
{
  "totalProcesses": 0,
  "draftProcesses": 0,
  "runningInstances": 0,
  "completedInstances": 0,
  "pendingTasks": 0,
  "overdueTasks": 0,
  "assetsTotal": 0,
  "alertsToday": 0,
  "auditEventsToday": 0
}
```

### 审计查询

`GET /api/v1/visualization/audit?pageIndex=1&pageSize=10`

```json
{
  "items": [
    {
      "id": "1",
      "actor": "admin",
      "action": "可视化流程-发布",
      "result": "成功",
      "target": "流程ID: 1",
      "ipAddress": "127.0.0.1",
      "userAgent": "Mozilla/5.0",
      "clientType": "WebH5",
      "clientPlatform": "Web",
      "clientChannel": "Browser",
      "clientAgent": "Chrome",
      "occurredAt": "2026-01-30T10:00:00Z"
    }
  ],
  "total": 0,
  "pageIndex": 1,
  "pageSize": 10
}
```

## 表格视图（个人）

说明：

- 仅个人视图：与当前登录用户唯一绑定，不支持共享。
- `tableKey` 统一标识表格来源：
  - `system.users`：员工管理
  - `system.roles`：角色管理
  - `system.permissions`：权限管理
  - `system.menus`：菜单管理
  - `system.departments`：部门管理
  - `system.positions`：职位管理
  - `system.projects`：项目管理
  - `system.apps`：应用管理
- 系统默认配置来源于 `appsettings.json` 的 `TableViewDefaults` 配置节。

### 查询视图列表

`GET /api/v1/table-views?tableKey=system.users&pageIndex=1&pageSize=20&keyword=我的`

响应：`PagedResult<TableViewListItem>`

### 获取默认视图

`GET /api/v1/table-views/default?tableKey=system.users`

### 获取系统默认配置

`GET /api/v1/table-views/default-config?tableKey=system.users`

### 获取视图详情

`GET /api/v1/table-views/{id}`

### 创建视图（需幂等 + CSRF）

`POST /api/v1/table-views`

请求：

```json
{
  "tableKey": "system.users",
  "name": "我的视图",
  "config": {
    "columns": [
      { "key": "username", "visible": true, "order": 0 },
      { "key": "displayName", "visible": true, "order": 1 }
    ],
    "density": "default",
    "pagination": { "pageSize": 10 }
  }
}
```

### 更新视图（需幂等 + CSRF）

`PUT /api/v1/table-views/{id}`

### 更新视图配置（需幂等 + CSRF）

`PATCH /api/v1/table-views/{id}/config`

### 设为默认视图（需幂等 + CSRF）

`POST /api/v1/table-views/{id}/set-default`

### 复制视图（需幂等 + CSRF）

`POST /api/v1/table-views/{id}/duplicate`

请求：

```json
{
  "name": "我的视图（副本）"
}
```

### 删除视图（需幂等 + CSRF）

`DELETE /api/v1/table-views/{id}`

### TableViewConfig

```json
{
  "columns": [
    { "key": "username", "visible": true, "order": 0, "width": 120, "pinned": "left" },
    { "key": "displayName", "visible": true, "order": 1 }
  ],
  "density": "compact|default|comfortable",
  "pagination": { "pageSize": 10 },
  "sort": [{ "key": "createdAt", "order": "ascend", "priority": 1 }],
  "filters": [{ "key": "status", "operator": "eq", "value": "Active" }],
  "groupBy": { "key": "departmentId", "collapsedKeys": [] },
  "aggregations": [{ "key": "amount", "op": "sum" }],
  "queryPanel": { "open": true, "autoSearch": false, "savedFilterId": "filter-1" },
  "queryModel": {
    "logic": "AND",
    "conditions": [{ "field": "status", "operator": "eq", "value": "Active" }],
    "groups": []
  }
}
```

## AMIS 低代码页面契约

  - **Schema 入口**：每个管理页面（员工/角色/权限/菜单/部门/职位/项目/应用）统一通过 `GET /api/v1/amis/pages/{key}` 拉取 Baidu AMIS 模式 JSON。
    - 请求必须带 `Authorization`、`X-Tenant-Id`（GET 请求无需 `Idempotency-Key`），写接口由前端 fetcher 自动附带 `Idempotency-Key` 与 `X-CSRF-TOKEN`。
    - 响应遵循通用 `ApiResponse<AmisPageDefinition>`，`data` 包含 `{ key, title, description, tableKey, schema }`，其中 `schema` 为完整的 AMIS JSON，即 `type: "page"` 的配置体。
    - 前端按模块加载 schema；schema 变更必须同步更新 `docs/contracts.md`。
- **tableKey 一览与模块映射**：
  - `system.users`：员工管理
  - `system.roles`：角色列表
  - `system.permissions`：权限列表
  - `system.menus`：菜单树
  - `system.departments`：部门
  - `system.positions`：职位
  - `system.projects`：项目
  - `system.apps`：应用配置
  这些 `tableKey` 同时用于表格视图存储与后端 `TableView` API。
  - **Schema 要求**：
  1. 所有列表型组件须默认分页 `pageSize=20`，并支持关键字（`keyword`）远程搜索，搜索字段归前端自由组合，但必须把 `keyword` 参数传给后端接口。
  2. 所有 `select`/`autocomplete` 组件必须使用远程接口（如 `/roles`, `/departments/all`）并通过 `pageSize=20` 限制，预设 `adaptor` 将 `ApiResponse` 转为 AMIS 可识别的 `{ status: 0, msg, data }`。
  3. 通过 `headerToolbar`/`toolbar` 按钮的 `dialog`/`ajax` 操作调用后端的写接口（`POST/PUT/DELETE`），在 schema 中可复用 `saveApi`/`deleteApi`，写操作由前端 fetcher 自动附带 `Idempotency-Key` + `X-CSRF-TOKEN`，后端统一返回 `ApiResponse`。
    4. 所有 schema 均包含 `tableKey` 字段，用于与个人视图（`TableView`）联动，具体联动策略以前端实现为准。
    5. schema 文件存放于后端 `src/backend/Atlas.WebApi/AmisSchemas/{key}.json`，由后端直接读取并返回。

- **示例响应**：

```json
{
  "success": true,
  "code": "SUCCESS",
  "message": "OK",
  "traceId": "00-abc123...",
  "data": {
    "key": "system.users",
    "title": "员工管理",
    "description": "基于 AMIS 的员工列表与表单",
    "tableKey": "system.users",
    "schema": {
      "type": "page",
      "title": "员工管理",
      "body": [ ... ]
    }
  }
}
```

前端 `AmisRenderer` 组件会把 `schema` 直接传给 `render` 并依赖 `env.fetcher` 将请求透传到后端 `ApiResponse`，还会用 `notify`/`alert` 显示提示。

## 低代码应用与页面契约

### 低代码应用（LowCodeApp）

- `GET /api/v1/lowcode-apps?pageIndex=1&pageSize=10&keyword=&category=`：分页查询应用
- `GET /api/v1/lowcode-apps/{id}`：应用详情（含页面列表）
- `GET /api/v1/lowcode-apps/by-key/{appKey}`：按应用标识查询详情
- `POST /api/v1/lowcode-apps`：创建应用（需幂等 + CSRF）
- `PUT /api/v1/lowcode-apps/{id}`：更新应用（需幂等 + CSRF）
- `POST /api/v1/lowcode-apps/{id}/publish`：发布应用（需幂等 + CSRF）
- `GET /api/v1/lowcode-apps/{id}/versions?pageIndex=1&pageSize=10`：分页查询应用版本历史（仅系统管理员）
- `POST /api/v1/lowcode-apps/{id}/versions/{versionId}/rollback`：按应用版本回滚（需幂等 + CSRF，仅系统管理员）
- `POST /api/v1/lowcode-apps/{id}/disable`：停用应用（需幂等 + CSRF）
- `GET /api/v1/lowcode-apps/{id}/export`：导出应用 JSON 包
- `POST /api/v1/lowcode-apps/import`：导入应用 JSON 包（需幂等 + CSRF，支持 `Rename/Overwrite/Skip` 冲突策略）
- `GET /api/v1/lowcode-apps/{appId}/environments`：查询应用环境配置
- `GET /api/v1/lowcode-apps/environments/{id}`：环境配置详情
- `POST /api/v1/lowcode-apps/{appId}/environments`：创建应用环境配置（需幂等 + CSRF）
- `PUT /api/v1/lowcode-apps/environments/{id}`：更新应用环境配置（需幂等 + CSRF）
- `DELETE /api/v1/lowcode-apps/environments/{id}`：删除应用环境配置（需幂等 + CSRF）
- `DELETE /api/v1/lowcode-apps/{id}`：删除应用（需幂等 + CSRF）

授权策略：

- 读接口（GET）要求 `apps:view`
- 写接口（POST/PUT/PATCH/DELETE）要求 `apps:update`
- 应用版本查询/回滚接口要求 `system:admin`

### LowCodeAppVersionListItem

```json
{
  "id": "3001",
  "appId": "2001",
  "version": 5,
  "actionType": "Rollback",
  "sourceVersionId": "2998",
  "note": "Rollback to version 3",
  "createdAt": "2026-03-04T10:00:00Z",
  "createdBy": 10001
}
```

### 低代码页面（LowCodePage）

- `POST /api/v1/lowcode-apps/{appId}/pages`：创建页面（需幂等 + CSRF）
- `PUT /api/v1/lowcode-apps/pages/{pageId}`：更新页面元数据（需幂等 + CSRF）
- `PATCH /api/v1/lowcode-apps/pages/{pageId}/schema`：仅更新页面 Schema（需幂等 + CSRF）
- `POST /api/v1/lowcode-apps/pages/{pageId}/publish`：发布页面（需幂等 + CSRF）
- `GET /api/v1/lowcode-apps/pages/{pageId}/versions`：页面版本历史
- `GET /api/v1/lowcode-apps/pages/{pageId}/runtime?mode=draft|published&environmentCode=dev`：运行态 Schema（草稿/已发布，支持环境变量替换）
- `POST /api/v1/lowcode-apps/pages/{pageId}/rollback/{versionId}`：按历史版本回滚并生成新发布版本（需幂等 + CSRF）
- `DELETE /api/v1/lowcode-apps/pages/{pageId}`：删除页面（需幂等 + CSRF）
- `GET /api/v1/lowcode-apps/pages/{pageId}`：页面详情（含完整 `schemaJson`）
- `GET /api/v1/lowcode-apps/{appId}/pages/tree`：页面树（按 `parentPageId` + `sortOrder`）

授权策略：

- 页面读接口要求 `apps:view`
- 页面写接口要求 `apps:update`

### LowCodePageTreeNode

```json
{
  "id": "1001",
  "appId": "2001",
  "pageKey": "customer-list",
  "name": "客户列表",
  "pageType": "List",
  "routePath": "/customers",
  "description": "客户管理列表页面",
  "icon": "unordered-list",
  "sortOrder": 1,
  "parentPageId": null,
  "version": 3,
  "isPublished": true,
  "createdAt": "2026-03-03T09:00:00Z",
  "permissionCode": "customers:view",
  "dataTableKey": "crm_customers",
  "children": []
}
```

### LowCodePageDetail

```json
{
  "id": "1001",
  "appId": "2001",
  "pageKey": "customer-list",
  "name": "客户列表",
  "pageType": "List",
  "schemaJson": "{ \"type\": \"page\", \"body\": [] }",
  "routePath": "/customers",
  "description": "客户管理列表页面",
  "icon": "unordered-list",
  "sortOrder": 1,
  "parentPageId": null,
  "version": 3,
  "isPublished": true,
  "createdAt": "2026-03-03T09:00:00Z",
  "updatedAt": "2026-03-03T10:00:00Z",
  "createdBy": 10001,
  "updatedBy": 10001,
  "permissionCode": "customers:view",
  "dataTableKey": "crm_customers"
}
```

## 动态表与低代码 CRUD 契约（草案）

### 命名与校验规则

- `tableKey`：`^[A-Za-z][A-Za-z0-9_]{1,63}$`，禁止保留字与系统表名。
- `fieldName`：`^[A-Za-z][A-Za-z0-9_]{0,63}$`，禁止保留字与系统字段。
- 不允许使用 `drop/alter/insert/update/delete` 等危险关键字作为名称。

### 字段类型枚举

- `Int`、`Long`、`Decimal`、`String`、`Text`、`Bool`、`DateTime`、`Date`
- `Decimal` 需指定 `precision`/`scale`
- `String` 需指定 `length`
- 自增仅允许 `Int/Long` 且必须主键

### 动态表接口

- `GET /api/v1/dynamic-tables`：分页查询动态表
- `GET /api/v1/dynamic-tables/{tableKey}`：动态表详情
- `GET /api/v1/dynamic-tables/{tableKey}/summary`：动态表概览（轻量字段计数与预览）
- `POST /api/v1/dynamic-tables`：新建动态表（需幂等 + CSRF）
- `PUT /api/v1/dynamic-tables/{tableKey}`：更新表元数据（需幂等 + CSRF）
- `POST /api/v1/dynamic-tables/{tableKey}/schema/alter`：变更字段（需幂等 + CSRF）
- `POST /api/v1/dynamic-tables/{tableKey}/schema/alter/preview`：预览变更 SQL（只读，不落库）
- `GET /api/v1/dynamic-tables/{tableKey}/migrations`：分页查询结构迁移记录
- `DELETE /api/v1/dynamic-tables/{tableKey}`：删除动态表（需幂等 + CSRF）
- `GET /api/v1/dynamic-tables/{tableKey}/relations`：查询轻量关系
- `PUT /api/v1/dynamic-tables/{tableKey}/relations`：覆盖更新轻量关系（需幂等 + CSRF）
- `GET /api/v1/dynamic-tables/{tableKey}/field-permissions`：查询字段级权限规则
- `PUT /api/v1/dynamic-tables/{tableKey}/field-permissions`：覆盖更新字段级权限规则（需幂等 + CSRF）

### 动态迁移记录接口（骨架）

- `GET /api/v1/dynamic-migrations?pageIndex=1&pageSize=10&tableKey=orders`：分页查询迁移记录
- `GET /api/v1/dynamic-migrations/{id}`：迁移记录详情
- `POST /api/v1/dynamic-migrations`：创建迁移草稿记录（需幂等 + CSRF）
- `POST /api/v1/dynamic-migrations/detect/{tableKey}`：检测结构变更并生成预览脚本（需幂等 + CSRF）
- `POST /api/v1/dynamic-migrations/{id}/execute`：执行迁移（需幂等 + CSRF）
- `POST /api/v1/dynamic-migrations/{id}/precheck`：迁移预检查（需幂等 + CSRF）
- `POST /api/v1/dynamic-migrations/{id}/retry`：重试迁移（需幂等 + CSRF）

```json
{
  "tableKey": "orders",
  "version": 1,
  "upScript": "ALTER TABLE ...",
  "downScript": "ALTER TABLE ...",
  "isDestructive": false
}
```

`detect` 响应：

```json
{
  "tableKey": "orders",
  "upScript": "ALTER TABLE ...",
  "downScript": "-- no-op",
  "isDestructive": false,
  "warnings": []
}
```

`execute/retry` 响应：

```json
{
  "id": "1001",
  "tableKey": "orders",
  "version": 1,
  "status": "Succeeded",
  "executedAt": "2026-03-03T12:00:00Z",
  "errorMessage": null
}
```

`precheck` 响应：

```json
{
  "id": "1001",
  "tableKey": "orders",
  "version": 1,
  "requiresConfirmation": true,
  "canExecute": true,
  "checks": [
    "迁移记录存在",
    "当前状态：Draft",
    "检测到破坏性变更，执行前需要确认"
  ]
}
```

### dbType 枚举

- `Sqlite`、`SqlServer`、`MySql`、`PostgreSql`

### 字段元数据接口

- `GET /api/v1/dynamic-tables/{tableKey}/fields`：字段列表
- `GET /api/v1/dynamic/meta/field-types?dbType=Sqlite`：字段类型枚举（用于前端联动）

### 记录 CRUD 接口

- `GET /api/v1/dynamic-tables/{tableKey}/records`：分页查询记录（关键词 + 排序）
- `GET /api/v1/dynamic-tables/{tableKey}/records/{id}`：单条记录
- `POST /api/v1/dynamic-tables/{tableKey}/records`：新增记录（需幂等 + CSRF）
- `PUT /api/v1/dynamic-tables/{tableKey}/records/{id}`：更新记录（需幂等 + CSRF）
- `DELETE /api/v1/dynamic-tables/{tableKey}/records/{id}`：删除记录（需幂等 + CSRF）
- `POST /api/v1/dynamic-tables/{tableKey}/records/query`：复杂筛选（支持 `eq/ne/gt/gte/lt/lte/like/in/between`，需幂等 + CSRF）
- `POST /api/v1/dynamic-tables/{tableKey}/records/export`：按筛选条件导出 CSV（需幂等 + CSRF，单次最多 10,000 条，分批查询并流式写出避免 OOM）
- `POST /api/v1/dynamic-tables/{tableKey}/records/batch`：批量新增（需幂等 + CSRF）
- `DELETE /api/v1/dynamic-tables/{tableKey}/records`：批量删除（需幂等 + CSRF）

说明：

- 当目标表配置了字段级权限规则时，查询/详情/导出将按当前用户角色自动裁剪可见字段；
- 写入（create/update）会校验可编辑字段，越权字段写入将返回 `FORBIDDEN`。
- 当当前角色数据权限为“仅本人”时，动态记录查询/详情/导出将自动注入 owner 过滤（基于 `ownerId/createdBy/creatorId` 字段约定）。
- `records/export` 响应为文件流（`text/csv; charset=utf-8` + `Content-Disposition`），不包装 `ApiResponse<T>`。

### AMIS Schema 接口

- `GET /api/v1/amis/dynamic-tables/designer`：表结构设计器 Schema
- `GET /api/v1/amis/dynamic-tables/{tableKey}/crud`：动态 CRUD Schema（按字段实时生成列定义、排序与分页）
- `GET /api/v1/amis/dynamic-tables/{tableKey}/forms/create`：新建表单 Schema
- `GET /api/v1/amis/dynamic-tables/{tableKey}/forms/edit?id=1001`：编辑表单 Schema
- `GET /api/v1/amis/dynamic-tables/{tableKey}/forms/detail?id=1001`：详情只读 Schema

### DynamicTableSummary

```json
{
  "id": "1001",
  "appId": "2001",
  "tableKey": "orders",
  "displayName": "订单",
  "description": "订单主表",
  "dbType": "Sqlite",
  "status": "Active",
  "fieldCount": 18,
  "indexCount": 4,
  "approvalFlowDefinitionId": 3001,
  "approvalStatusField": "approvalStatus",
  "previewFields": [
    {
      "name": "id",
      "displayName": "主键",
      "fieldType": "Long",
      "allowNull": false,
      "isPrimaryKey": true
    }
  ]
}
```

### TableCreateRequest

```json
{
  "tableKey": "orders",
  "displayName": "订单",
  "description": "订单主表",
  "dbType": "Sqlite",
  "fields": [
    {
      "name": "id",
      "displayName": "主键",
      "fieldType": "Long",
      "isPrimaryKey": true,
      "isAutoIncrement": true,
      "allowNull": false
    },
    {
      "name": "orderNo",
      "displayName": "订单号",
      "fieldType": "String",
      "length": 50,
      "isUnique": true,
      "allowNull": false
    },
    {
      "name": "amount",
      "displayName": "金额",
      "fieldType": "Decimal",
      "precision": 18,
      "scale": 2,
      "allowNull": false
    }
  ],
  "indexes": [
    {
      "name": "idx_orders_no",
      "isUnique": true,
      "fields": ["orderNo"]
    }
  ]
}
```

### TableAlterRequest

> 当前阶段（M9 增量）支持 `addFields` 与 `updateFields`（仅允许更新字段显示名 `displayName` 与排序 `sortOrder`）；`removeFields` 仍返回 `VALIDATION_ERROR`。

```json
{
  "addFields": [
    { "name": "remark", "displayName": "备注", "fieldType": "String", "length": 200, "allowNull": true }
  ],
  "updateFields": [
    { "name": "amount", "displayName": "金额", "precision": 18, "scale": 4 }
  ],
  "removeFields": ["legacyField"]
}
```

### SchemaMigrationListItem

```json
{
  "id": "193500000000000001",
  "tableId": "193400000000000001",
  "tableKey": "orders",
  "operationType": "ADD_FIELDS",
  "status": "Succeeded",
  "appliedSql": "ALTER TABLE ...",
  "rollbackSql": "当前版本不支持自动回滚，请通过备份恢复。",
  "createdBy": 10001,
  "createdAt": "2026-03-03T10:00:00Z"
}
```

### AlterPreviewResponse

```json
{
  "tableKey": "orders",
  "operationType": "ADD_FIELDS",
  "sqlScripts": [
    "ALTER TABLE \"orders\" ADD COLUMN \"remark\" TEXT;",
    "CREATE UNIQUE INDEX \"uk_orders_remark\" ON \"orders\" (\"remark\");"
  ],
  "rollbackHint": "当前版本不支持自动回滚，请通过备份恢复。"
}
```

### FieldDefinition

```json
{
  "name": "orderNo",
  "displayName": "订单号",
  "fieldType": "String",
  "length": 50,
  "precision": 18,
  "scale": 2,
  "allowNull": false,
  "isPrimaryKey": false,
  "isAutoIncrement": false,
  "isUnique": true,
  "defaultValue": null,
  "validation": {
    "regex": "^[A-Za-z0-9_-]+$",
    "minLength": 1,
    "maxLength": 50
  }
}
```

### FieldValueDto

```json
{
  "field": "amount",
  "valueType": "Decimal",
  "decimalValue": 199.99
}
```

约束：仅允许填写一个具体值字段（`stringValue/intValue/longValue/decimalValue/boolValue/dateTimeValue/dateValue`）。

### DynamicRecordUpsertRequest

```json
{
  "values": [
    { "field": "orderNo", "valueType": "String", "stringValue": "SO-10001" },
    { "field": "amount", "valueType": "Decimal", "decimalValue": 199.99 },
    { "field": "createdAt", "valueType": "DateTime", "dateTimeValue": "2026-01-31T10:00:00Z" }
  ]
}
```

### DynamicRecordDto

```json
{
  "id": "1001",
  "values": [
    { "field": "orderNo", "valueType": "String", "stringValue": "SO-10001" },
    { "field": "amount", "valueType": "Decimal", "decimalValue": 199.99 }
  ]
}
```

### DynamicRecordQueryRequest

```json
{
  "pageIndex": 1,
  "pageSize": 20,
  "keyword": "SO-10001",
  "sortBy": "createdAt",
  "sortDesc": true,
  "filters": [
    { "field": "amount", "operator": "gte", "value": 100 },
    { "field": "status", "operator": "eq", "value": "Paid" }
  ]
}
```

### FilterOperator

- `eq`、`ne`、`gt`、`gte`、`lt`、`lte`、`like`、`in`、`between`

### DynamicColumnDef（AMIS 列配置）

```json
{
  "name": "orderNo",
  "label": "订单号",
  "type": "text",
  "sortable": true,
  "quickEdit": false,
  "searchable": true
}
```

### 动态列表响应（支持列定义）

当 `includeColumns=true` 时，响应数据附带 `columns` 供 AMIS 渲染。

```json
{
  "items": [
    {
      "id": "1001",
      "values": [
        { "field": "orderNo", "valueType": "String", "stringValue": "SO-10001" }
      ]
    }
  ],
  "total": 1,
  "pageIndex": 1,
  "pageSize": 20,
  "columns": [
    { "name": "orderNo", "label": "订单号", "type": "text", "sortable": true }
  ]
}
```

### AMIS 模板引用

- `docs/amis-templates/dynamic-table-list.json`：动态表列表
- `docs/amis-templates/dynamic-table-designer.json`：表结构设计器
- `docs/amis-templates/dynamic-table-crud.json`：动态 CRUD 页面

## 用户/部门/职位管理契约

## 插件管理契约（AssemblyLoadContext）

- `GET /api/v1/plugins`：查询插件清单
- `POST /api/v1/plugins/reload`：重载插件目录并刷新清单

### PluginDescriptor

```json
{
  "code": "demo.plugin",
  "name": "Demo Plugin",
  "version": "1.0.0",
  "assemblyName": "Atlas.Plugin.Demo",
  "filePath": "/workspace/src/backend/Atlas.WebApi/plugins/Atlas.Plugin.Demo.dll",
  "state": "Loaded|Failed|NoEntryPoint",
  "loadedAt": "2026-03-03T12:00:00Z",
  "errorMessage": null
}
```

## 用户/部门/职位管理契约

### 用户

- `GET /api/v1/users`：分页查询用户
- `GET /api/v1/users/{id}`：用户详情
- `POST /api/v1/users`：新增用户
- `PUT /api/v1/users/{id}`：更新用户
- `DELETE /api/v1/users/{id}`：删除用户
- `PUT /api/v1/users/{id}/roles`：更新用户角色
- `PUT /api/v1/users/{id}/departments`：更新用户部门
- `PUT /api/v1/users/{id}/positions`：更新用户职位

### 部门

- `GET /api/v1/departments`：分页查询部门
- `GET /api/v1/departments/all`：获取全部部门
- `POST /api/v1/departments`：新增部门
- `PUT /api/v1/departments/{id}`：更新部门
- `DELETE /api/v1/departments/{id}`：删除部门

### 职位

- `GET /api/v1/positions`：分页查询职位
- `GET /api/v1/positions/{id}`：职位详情
- `GET /api/v1/positions/all`：获取全部职位
- `POST /api/v1/positions`：新增职位
- `PUT /api/v1/positions/{id}`：更新职位
- `DELETE /api/v1/positions/{id}`：删除职位

## 项目管理契约

### 项目

- `GET /api/v1/projects?PageIndex=1&PageSize=10&Keyword=核心`：分页查询项目
- `GET /api/v1/projects/{id}`：项目详情
- `POST /api/v1/projects`：新增项目
- `PUT /api/v1/projects/{id}`：更新项目
- `DELETE /api/v1/projects/{id}`：停用项目（逻辑删除）
- `PUT /api/v1/projects/{id}/users`：项目分配人员
- `PUT /api/v1/projects/{id}/departments`：项目分配部门
- `PUT /api/v1/projects/{id}/positions`：项目分配职位
- `GET /api/v1/projects/my`：当前用户可切换项目列表
- `GET /api/v1/projects/my/paged?PageIndex=1&PageSize=20&Keyword=核心`：当前用户可切换项目分页列表（用于下拉远程检索，默认返回 20 条）

说明：

- 项目编码 `code` 建议在租户范围内保持唯一。

### ProjectListItem

```json
{
  "id": "1001",
  "code": "ops-core",
  "name": "运维核心项目",
  "isActive": true,
  "description": "运维平台核心业务线",
  "sortOrder": 0
}
```

### ProjectCreateRequest

```json
{
  "code": "ops-core",
  "name": "运维核心项目",
  "isActive": true,
  "description": "运维平台核心业务线",
  "sortOrder": 0
}
```

### ProjectUpdateRequest

```json
{
  "name": "运维核心项目",
  "isActive": true,
  "description": "运维平台核心业务线",
  "sortOrder": 0
}
```

## 应用配置契约

### 应用配置

- `GET /api/v1/apps`：分页查询应用配置
- `GET /api/v1/apps/current`：当前应用配置
- `GET /api/v1/apps/{id}`：应用配置详情
- `PUT /api/v1/apps/{id}`：更新应用配置

### AppConfigResponse

```json
{
  "id": "1",
  "appId": "security-platform",
  "name": "SecurityPlatform",
  "isActive": true,
  "enableProjectScope": true,
  "description": "默认应用配置",
  "sortOrder": 0
}
```

### AppConfigUpdateRequest

```json
{
  "name": "SecurityPlatform",
  "isActive": true,
  "enableProjectScope": false,
  "description": "默认应用配置",
  "sortOrder": 0
}
```

字段说明：

- `enableProjectScope`：是否启用项目模式（应用级开关，启用后业务数据必须带 `project_id`）。

## Workflow Designer APIs (草案)
- POST `/api/v1/approval/flows` : 保存流程定义
  - body: { tenantId: string, definition: FlowDefinition }
  - resp: { id: string, version?: number }
- GET `/api/v1/approval/flows/{id}` : 加载流程定义
  - resp: { definition: FlowDefinition }
- PUT `/api/v1/approval/flows/{id}` : 更新流程定义
  - body 同保存
  - resp: { success: boolean }
- POST `/api/v1/approval/flows/{id}/publication` : 发布流程
  - resp: { success: boolean, version: number }
- POST `/api/v1/approval/flows/validation` : 前/后端联合校验
  - body: { tenantId: string, definition: FlowDefinition }
  - resp: { isValid: boolean, errors: string[], warnings?: string[] }
- POST `/api/v1/approval/flows/{id}/preview` : 预览（返回节点线性/树形展开视图数据）
  - resp: { definition: FlowDefinition, preview: any }

### FlowDefinition / FlowNode (见前端 types/workflow.ts)
- 节点类型：start/end/approve/condition/parallel/parallel-join/copy/task
- 审批人规则：fixedUser/role/departmentLeader/selfSelect/hrbp/formField/outsideApi
- 条件：ConditionGroup{ relation AND/OR, items: [{ field, op, value, group }] }
- 按钮：NodeButton { pageType, buttonType, name, remark }

### 约束与校验（后端应补充验证）
- 必须存在唯一 start 与 end 节点
- 并行节点必须有聚合节点 parallel-join
- condition 节点需有默认分支或全覆盖
- 审批节点必须配置 approverRule
- 节点 name/code 长度与特殊字符校验
- 发布前需要通过 validation

## 审批流 V1V2 增量契约（2026-03）

### 流程定义管理新增接口

- `POST /api/v1/approval/flows/{id}/copy`：复制现有流程为新草稿。
  - 请求：`{ "name"?: string }`
  - 响应：`ApprovalFlowDefinitionResponse`
- `GET /api/v1/approval/flows/{id}/export`：导出流程定义 JSON。
  - 响应：`ApprovalFlowExportResponse`
- `POST /api/v1/approval/flows/import`：导入流程定义 JSON 为新草稿。
  - 请求：`ApprovalFlowImportRequest`
  - 响应：`ApprovalFlowDefinitionResponse`
- `GET /api/v1/approval/flows/{id}/versions/{targetVersion}/compare`：按流程名 + 版本号对比定义差异。
  - 响应：`ApprovalFlowCompareResponse`

### 流程校验返回增强

- `POST /api/v1/approval/flows/validation` 响应新增 `details` 字段（兼容 `errors`/`warnings`）。

```json
{
  "isValid": false,
  "errors": ["并行节点必须配置汇聚后的后续节点"],
  "warnings": ["并行块缺少 groupId 标识"],
  "details": [
    {
      "code": "PARALLEL_JOIN_REQUIRED",
      "message": "并行节点必须配置汇聚后的后续节点",
      "severity": "error",
      "nodeId": "parallel_xxx",
      "edgeId": null
    }
  ]
}
```

### 运行时实例管理新增接口

- `GET /api/v1/approval/instances/admin`：管理端实例分页查询（支持过滤）。
  - Query 参数：
    - `pageIndex`, `pageSize`
    - `definitionId?`
    - `initiatorUserId?`
    - `businessKey?`
    - `startedFrom?`（ISO8601）
    - `startedTo?`（ISO8601）
    - `status?`（`Running|Completed|Rejected|Canceled`）
  - 响应：`PagedResult<ApprovalInstanceListItem>`

### 前端运行态新增页面路由（动态菜单）

- `/process/start`：发起审批
- `/process/inbox`：我的待办
- `/process/done`：我的已办
- `/process/my-requests`：我发起的
- `/process/cc`：我的抄送
- `/process/manage/flows`：流程定义管理
- `/process/manage/instances`：流程实例管理
- `/process/designer/:id`、`/process/tasks/:id`、`/process/instances/:id`：隐藏详情路由

## 低代码设计器版本管理契约（2026-03）

### 表单版本管理

#### FormDefinitionVersionListItem

```json
{
  "id": 123456789,
  "formDefinitionId": 987654321,
  "snapshotVersion": 3,
  "name": "客户信息登记表",
  "description": "第三版",
  "category": "CRM",
  "icon": "file-form",
  "dataTableKey": "customer_info",
  "createdBy": 111222333,
  "createdAt": "2026-03-01T10:00:00Z"
}
```

#### FormDefinitionVersionDetail

在 `FormDefinitionVersionListItem` 基础上增加：

```json
{
  "schemaJson": "{ ... }"
}
```

#### 接口列表

- `GET /api/v1/form-definitions/{id}/versions`：查询表单版本历史列表。
  - 响应：`FormDefinitionVersionListItem[]`
- `GET /api/v1/form-definitions/{id}/versions/{versionId}`：查询指定版本详情（含完整 schemaJson）。
  - 响应：`FormDefinitionVersionDetail`
- `POST /api/v1/form-definitions/{id}/rollback/{versionId}`：将表单定义回滚至指定历史版本，并创建新快照。
  - 请求：无 Body（路由参数）
  - 需要：`Idempotency-Key` + `X-CSRF-TOKEN`
  - 触发审计：`LowCode.FormDefinition.RolledBack`

### 审批流版本管理

#### ApprovalFlowVersionListItem

```json
{
  "id": 123456789,
  "definitionId": 987654321,
  "snapshotVersion": 2,
  "name": "采购审批流",
  "description": "V2 优化",
  "category": "采购",
  "createdBy": 111222333,
  "createdAt": "2026-03-02T09:00:00Z"
}
```

#### ApprovalFlowVersionDetail

在 `ApprovalFlowVersionListItem` 基础上增加：

```json
{
  "definitionJson": "{ ... }",
  "visibilityScopeJson": "{ ... }"
}
```

#### 接口列表

- `GET /api/v1/approval/flows/{id}/versions`：查询审批流版本历史列表。
  - 响应：`ApprovalFlowVersionListItem[]`
- `GET /api/v1/approval/flows/{id}/versions/{versionId}/detail`：查询指定版本详情（含完整 definitionJson）。
  - 响应：`ApprovalFlowVersionDetail`
- `POST /api/v1/approval/flows/{id}/rollback/{versionId}`：将审批流定义回滚至指定历史版本，并创建新快照。
  - 请求：无 Body（路由参数）
  - 需要：`Idempotency-Key` + `X-CSRF-TOKEN`
  - 触发审计：`Approval.FlowDefinition.RolledBack`

### 设计态审计埋点

发布和回滚操作均写入 `AuditRecord`，`action` 字段取值规范：

| 场景 | `action` |
|---|---|
| 表单定义发布 | `LowCode.FormDefinition.Published` |
| 表单定义回滚 | `LowCode.FormDefinition.RolledBack` |
| 审批流定义发布 | `Approval.FlowDefinition.Published` |
| 审批流定义回滚 | `Approval.FlowDefinition.RolledBack` |
| 低代码页面发布 | `LowCode.Page.Published` |
| 低代码页面回滚 | `LowCode.Page.RolledBack` |

`target` 格式：`{EntityType}:{id}` 或 `{EntityType}:{id}:Version:{versionId}`（回滚时附带版本 ID）。

## AI 平台增量契约（Phase 11-19）

### AI Marketplace（Phase 11）

- `GET /api/v1/ai-marketplace/categories`
- `POST /api/v1/ai-marketplace/categories`
- `PUT /api/v1/ai-marketplace/categories/{id}`
- `DELETE /api/v1/ai-marketplace/categories/{id}`
- `GET /api/v1/ai-marketplace/products`
- `GET /api/v1/ai-marketplace/products/{id}`
- `POST /api/v1/ai-marketplace/products`
- `PUT /api/v1/ai-marketplace/products/{id}`
- `DELETE /api/v1/ai-marketplace/products/{id}`
- `POST /api/v1/ai-marketplace/products/{id}/publish`
- `POST /api/v1/ai-marketplace/products/{id}/favorite`
- `DELETE /api/v1/ai-marketplace/products/{id}/favorite`
- `POST /api/v1/ai-marketplace/products/{id}/download`

### 上传增强（Phase 12）

- 分片上传：
  - `POST /api/v1/files/upload/init`
  - `POST /api/v1/files/upload/{sessionId}/part/{partNumber}`
  - `POST /api/v1/files/upload/{sessionId}/complete`
  - `GET /api/v1/files/upload/{sessionId}/progress`
- Tus 协议上传（Resumable Upload）：
  - `OPTIONS /api/v1/files/tus`
  - `OPTIONS /api/v1/files/tus/{sessionId}`
  - `POST /api/v1/files/tus`
  - `HEAD /api/v1/files/tus/{sessionId}`
  - `PATCH /api/v1/files/tus/{sessionId}`
  - 约束：
    - 头部必须携带 `Tus-Resumable: 1.0.0`
    - 创建会话必须携带 `Upload-Length`，可选 `Upload-Metadata`（`filename`、`contentType` 使用 Base64 值）
    - PATCH 必须携带 `Upload-Offset` 且 `Content-Type=application/offset+octet-stream`
    - Tus 端点免幂等键校验（协议分片重传依赖 `Upload-Offset`），但保持登录与权限控制
- 签名 URL：
  - `GET /api/v1/files/{id}/signed-url`
  - `GET /api/v1/files/signed/{id}?tenantId=<guid>&expires=<unix>&sig=<hmac>`
- Range 下载：
  - `GET /api/v1/files/{id}` 支持 `Range: bytes=start-end`
  - 响应包含 `Accept-Ranges`、`ETag`、`Last-Modified`
  - 命中范围请求时返回 `206 Partial Content` + `Content-Range`
  - 携带 `If-Range` 且校验失败时回退全量下载
- 秒传校验：
  - `GET /api/v1/files/instant-check?sha256=<hex>&sizeBytes=<long>`
  - 返回 `FileInstantCheckResult`：
    - `exists`：是否命中
    - `fileId/originalName/contentType/sizeBytes`：命中时返回
- 图片流程：
  - `POST /api/v1/files/images/apply`
  - `POST /api/v1/files/images/commit`

#### 上传下载性能基线建议

- 默认分片大小：`2MB`（`FileStorage:ChunkPartSizeBytes`）。
- 默认并发建议：
  - 上传并发：2~4（前端按网络与设备能力动态调节）
  - 下载并发：顺序 Range 拉取（避免浏览器内存峰值抖动）
- 建议压测口径：
  - 文件大小：10MB / 100MB / 1GB
  - 指标：成功率、平均耗时、P95、服务端错误率、客户端中断恢复成功率
- 生产建议：
  - 保持 `SignedUrlDefaultExpireSeconds` 在最小可用范围（推荐 300~900 秒）
  - 启用上传会话清理任务，避免临时目录膨胀

### 管理台 AI 配置（Phase 14）

- `GET /api/v1/admin/ai-config`
- `PUT /api/v1/admin/ai-config`

配置对象 `AdminAiConfig` 字段：

- `enableAiPlatform`
- `enableOpenPlatform`
- `enableCodeSandbox`
- `enableMarketplace`
- `enableContentModeration`
- `maxDailyTokensPerUser`
- `maxKnowledgeRetrievalCount`

### 模型配置删除治理（严格阻断）

- 删除接口：`DELETE /api/v1/model-configs/{id}`
- 治理策略：当模型配置被 Agent 强关联（`Agent.ModelConfigId`）引用时，禁止删除并返回业务错误。
- 强关联阻断范围（当前）：Agent。
- 阻断响应约定：
  - `code`: `VALIDATION_ERROR`
  - `message`: 包含引用数量与示例 Agent（如：`模型配置被 2 个 Agent 引用，禁止删除...请先解除关联后重试。`）

失败示例：

```json
{
  "success": false,
  "code": "VALIDATION_ERROR",
  "message": "模型配置被 2 个 Agent 引用，禁止删除，示例：客服助手(ID:1001)、流程编排助手(ID:1002)。请先解除关联后重试。",
  "traceId": "00-..."
}
```

#### 运行时弱关联风险说明（非强引用阻断）

- Workflow/LowCode 的部分 AI 运行路径通过 `provider/model` 字符串解析默认模型，不属于 `ModelConfigId` 强引用。
- 因此“删除阻断”不会覆盖此类弱关联场景；若删除后命中默认回退，可能出现运行时模型变化。
- 治理建议：
  - 删除前优先排查 workflow 配置与运行日志中的 provider/model 使用情况；
  - 发布前在测试环境做回归，确认不存在静默回退导致的行为偏差。

### 统一搜索（Phase 15）

- `GET /api/v1/ai-search?keyword=&limit=`
- `GET /api/v1/ai-search/recent?limit=`
- `POST /api/v1/ai-search/recent`
- `DELETE /api/v1/ai-search/recent/{id}`

搜索结果模型 `AiSearchResultItem`：

- `resourceType`
- `resourceId`
- `title`
- `description`
- `path`
- `updatedAt`

### Workspace（Phase 16）

- `GET /api/v1/ai-workspaces/current`
- `PUT /api/v1/ai-workspaces/current`
- `GET /api/v1/ai-workspaces/library`

### DevOps UI（Phase 17）

- 前端页面与组件契约：
  - `ai/AiTestSetsPage`
  - `ai/AiMockSetsPage`
  - `components/ai/TraceViewer`
  - `components/ai/PreviewPanel`
- 当前以前端模拟数据为主，保留后续对接真实评测/追踪后端 API 的扩展位。

### 快捷命令与引导（Phase 19）

- `GET /api/v1/ai-shortcuts`
- `POST /api/v1/ai-shortcuts`
- `PUT /api/v1/ai-shortcuts/{id}`
- `DELETE /api/v1/ai-shortcuts/{id}`
- `GET /api/v1/ai-shortcuts/popup`
- `POST /api/v1/ai-shortcuts/popup/dismiss`

写接口继续强制：

- `Idempotency-Key`
- `X-CSRF-TOKEN`

---

## V2 Workflow API 契约（Coze 风格 DAG 引擎）

### 概述

V2 工作流 API 采用 Coze 风格的 DAG 执行引擎，与 V1（`api/v1/ai-workflows`）并行运行。V2 引入独立的 Meta/Draft/Version 草稿-发布模型，支持 SSE 流式执行和可扩展的节点执行器注册表。

### 路由前缀

`api/v2/workflows`

### 端点列表

| 方法 | 路由 | 说明 | 权限 |
|---|---|---|---|
| POST | `/` | 创建工作流 | `ai-workflow:create` |
| GET | `/` | 工作流列表（分页） | `ai-workflow:view` |
| GET | `/published` | 已发布工作流列表（分页） | `ai-workflow:view` |
| GET | `/{id}` | 工作流详情（含 Draft） | `ai-workflow:view` |
| PUT | `/{id}/meta` | 更新元信息 | `ai-workflow:update` |
| PUT | `/{id}/draft` | 保存草稿 | `ai-workflow:update` |
| DELETE | `/{id}` | 删除工作流（软删除） | `ai-workflow:delete` |
| POST | `/{id}/copy` | 复制工作流 | `ai-workflow:create` |
| POST | `/{id}/publish` | 发布版本 | `ai-workflow:update` |
| GET | `/{id}/versions` | 版本列表 | `ai-workflow:view` |
| POST | `/{id}/run` | 同步运行 | `ai-workflow:execute` |
| POST | `/{id}/stream` | SSE 流式运行 | `ai-workflow:execute` |
| POST | `/executions/{id}/cancel` | 取消执行 | `ai-workflow:execute` |
| POST | `/executions/{id}/resume` | 恢复执行 | `ai-workflow:execute` |
| GET | `/executions/{id}/checkpoint` | 获取执行检查点 | `ai-workflow:view` |
| POST | `/executions/{id}/recover` | 基于检查点恢复执行 | `ai-workflow:execute` |
| GET | `/executions/{id}/process` | 执行进度 | `ai-workflow:view` |
| GET | `/executions/{id}/debug-view` | 执行调试聚合视图 | `ai-workflow:view` |
| GET | `/executions/{id}/nodes/{key}` | 节点执行详情 | `ai-workflow:view` |
| POST | `/{id}/debug-node` | 单节点调试 | `ai-workflow:debug` |
| GET | `/node-types` | 节点类型列表 | `ai-workflow:view` |

### 请求模型

```typescript
// 创建
interface WorkflowV2CreateRequest {
  name: string;        // 2-100 字符
  description?: string;
  mode: 0 | 1;        // 0=Standard, 1=ChatFlow
}

// 保存草稿
interface WorkflowV2SaveDraftRequest {
  canvasJson: string;  // Canvas JSON（必填）
  commitId?: string;
}

// 更新元信息
interface WorkflowV2UpdateMetaRequest {
  name: string;
  description?: string;
}

// 发布
interface WorkflowV2PublishRequest {
  changeLog?: string;  // ≤500 字符
}

// 运行
interface WorkflowV2RunRequest {
  inputsJson?: string; // JSON 字符串，输入变量
}

// 单节点调试
interface WorkflowV2NodeDebugRequest {
  nodeKey: string;     // 目标节点 Key
  inputsJson?: string;
}
```

### 响应模型

```typescript
interface WorkflowV2ListItem {
  id: number;
  name: string;
  description?: string;
  mode: number;
  status: number;      // 0=Draft, 1=Published, 2=Archived
  latestVersionNumber: number;
  creatorId: number;
  createdAt: string;
  updatedAt: string;
  publishedAt?: string;
}

interface WorkflowV2DetailDto extends WorkflowV2ListItem {
  canvasJson: string;
  commitId?: string;
}

interface WorkflowV2VersionDto {
  id: number;
  workflowId: number;
  versionNumber: number;
  changeLog?: string;
  canvasJson: string;
  publishedAt: string;
  publishedByUserId: number;
}

interface WorkflowV2ExecutionDto {
  id: number;
  workflowId: number;
  versionNumber: number;
  status: number;       // 0=Pending..5=Interrupted
  inputsJson?: string;
  outputsJson?: string;
  errorMessage?: string;
  startedAt: string;
  completedAt?: string;
  nodeExecutions: WorkflowV2NodeExecutionDto[];
}

interface WorkflowV2NodeExecutionDto {
  id: number;
  executionId: number;
  nodeKey: string;
  nodeType: number;
  status: number;
  inputsJson?: string;
  outputsJson?: string;
  errorMessage?: string;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
}

interface WorkflowV2ExecutionCheckpointDto {
  executionId: number;
  workflowId: number;
  status: number;
  lastNodeKey?: string;
  startedAt: string;
  completedAt?: string;
  inputsJson?: string;
  outputsJson?: string;
  errorMessage?: string;
}

interface WorkflowV2ExecutionDebugViewDto {
  execution: WorkflowV2ExecutionDto;
  focusNode?: WorkflowV2NodeExecutionDto;
  focusReason: string;
}
```

### SSE 流式事件格式

`POST /api/v2/workflows/{id}/stream` 返回 `text/event-stream`：

```
event: execution_start
data: {"executionId":"123456"}

event: node_start
data: {"executionId":"123456","nodeKey":"text_1","nodeType":"TextProcessor"}

event: node_output
data: {"executionId":"123456","nodeKey":"text_1","nodeType":"TextProcessor","outputs":{"text_output":"hello"}}

event: node_complete
data: {"executionId":"123456","nodeKey":"text_1","nodeType":"TextProcessor","durationMs":15}

event: node_failed
data: {"executionId":"123456","nodeKey":"loop_1","nodeType":"Loop","durationMs":2,"errorMessage":"Loop 节点未找到集合变量：items","interruptType":"None"}

event: llm_output
data: 大模型生成的文本内容...

# 仅在执行成功时发送
event: execution_complete
data: {"executionId":"123456","outputsJson":"{\"result\":\"ok\"}"}

# 执行失败（包含业务失败/运行时异常）
event: execution_failed
data: {"executionId":"123456","errorMessage":"节点 text_1 执行异常"}

# 执行被取消
event: execution_cancelled
data: {"executionId":"123456"}

# 执行中断（等待人工介入/外部输入）
event: execution_interrupted
data: {"executionId":"123456","interruptType":"ManualApproval","nodeKey":"approval_1","outputsJson":"{\"draft\":\"pending\"}"}
```

### Canvas JSON Schema

```json
{
  "nodes": [
    {
      "key": "entry_1",
      "type": 1,
      "label": "开始",
      "config": {},
      "layout": { "x": 100, "y": 100, "width": 120, "height": 60 }
    },
    {
      "key": "text_1",
      "type": 15,
      "label": "文本处理",
      "config": {
        "template": "处理结果: {{input_var}}",
        "outputKey": "text_output"
      },
      "layout": { "x": 300, "y": 100, "width": 120, "height": 60 }
    },
    {
      "key": "exit_1",
      "type": 2,
      "label": "结束",
      "config": {},
      "layout": { "x": 500, "y": 100, "width": 120, "height": 60 }
    }
  ],
  "connections": [
    {
      "sourceNodeKey": "entry_1",
      "sourcePort": "output",
      "targetNodeKey": "text_1",
      "targetPort": "input",
      "condition": null
    },
    {
      "sourceNodeKey": "text_1",
      "sourcePort": "output",
      "targetNodeKey": "exit_1",
      "targetPort": "input",
      "condition": null
    }
  ]
}
```

### 已注册节点类型（14 种）

| Key | Name | Category | NodeType 值 |
|---|---|---|---|
| Entry | 开始 | Flow | 1 |
| Exit | 结束 | Flow | 2 |
| Llm | 大模型 | AI | 3 |
| Selector | 条件分支 | Flow | 8 |
| SubWorkflow | 子工作流 | Flow | 9 |
| TextProcessor | 文本处理 | Transform | 15 |
| Loop | 循环 | Flow | 21 |
| AssignVariable | 变量赋值 | Data | 40 |
| VariableAggregator | 变量聚合 | Data | 32 |
| DatabaseQuery | 数据库查询 | Data | 43 |
| HttpRequester | HTTP 请求 | Integration | 45 |
| CodeRunner | 代码执行 | Compute | 5 |
| JsonSerialization | JSON 序列化 | Transform | 58 |
| JsonDeserialization | JSON 反序列化 | Transform | 59 |

---

## Multi-Agent Orchestration API 契约（Phase 2）

### 路由前缀

`api/v1/multi-agent-orchestrations`

### 端点列表

| 方法 | 路由 | 说明 | 权限 |
|---|---|---|---|
| GET | `/` | 多 Agent 编排分页查询 | `agent:view` |
| GET | `/{id}` | 编排详情 | `agent:view` |
| POST | `/` | 创建编排 | `agent:create` |
| PUT | `/{id}` | 更新编排（含启停状态） | `agent:update` |
| DELETE | `/{id}` | 删除编排 | `agent:delete` |
| POST | `/{id}/run` | 同步执行编排 | `agent:update` |
| POST | `/{id}/stream` | SSE 流式执行编排 | `agent:update` |
| GET | `/executions/{executionId}` | 查询执行详情 | `agent:view` |

写接口要求：

- `Idempotency-Key`
- `X-CSRF-TOKEN`
- `X-Tenant-Id`

### 请求模型

```typescript
interface MultiAgentMemberInput {
  agentId: number;
  alias?: string;
  sortOrder: number;
  isEnabled: boolean;
  promptPrefix?: string;
}

interface MultiAgentOrchestrationCreateRequest {
  name: string; // <= 128
  description?: string; // <= 1024
  mode: 0 | 1; // 0=Sequential, 1=Parallel
  members: MultiAgentMemberInput[];
}

interface MultiAgentOrchestrationUpdateRequest extends MultiAgentOrchestrationCreateRequest {
  status?: 0 | 1 | 2; // 0=Draft, 1=Active, 2=Disabled
}

interface MultiAgentRunRequest {
  message: string; // <= 8000
  enableRag?: boolean;
}
```

### 响应模型

```typescript
interface MultiAgentOrchestrationListItem {
  id: number;
  name: string;
  description?: string;
  mode: 0 | 1;
  status: 0 | 1 | 2;
  memberCount: number;
  creatorUserId: number;
  createdAt: string;
  updatedAt: string;
}

interface MultiAgentExecutionStep {
  agentId: number;
  agentName: string;
  alias?: string;
  inputMessage: string;
  outputMessage?: string;
  status: 0 | 1 | 2 | 3 | 4 | 5; // ExecutionStatus
  errorMessage?: string;
  startedAt: string;
  completedAt?: string;
}

interface MultiAgentExecutionResult {
  executionId: number;
  orchestrationId: number;
  status: 0 | 1 | 2 | 3 | 4 | 5;
  outputMessage?: string;
  errorMessage?: string;
  steps: MultiAgentExecutionStep[];
  startedAt: string;
  completedAt?: string;
}
```

### SSE 事件格式（`POST /{id}/stream`）

```text
event: execution_start
data: {"executionId":1485491907248263168,"orchestrationId":1485491863413592064}

event: agent_start
data: {"AgentId":1481933398292303872,"Alias":"分析Agent","startedAt":"2026-03-23T04:14:29.8039646Z"}

event: agent_finish
data: {"AgentId":1481933398292303872,"Status":3,"ErrorMessage":"..."}

event: execution_finish
data: {"executionId":1485491907248263168,"status":3,"outputMessage":"","errorMessage":"..."}
```

---

## Multimodal API 契约（Phase 2）

### 路由前缀

`api/v1/multimodal`

### 端点列表

| 方法 | 路由 | 说明 | 权限 |
|---|---|---|---|
| POST | `/assets` | 创建多模态资产记录（图片/音频/视频/文本） | `agent:view` |
| GET | `/assets/{id}` | 查询多模态资产详情 | `agent:view` |
| POST | `/vision/analyze` | 视觉分析（图像理解） | `agent:view` |
| POST | `/asr/transcribe` | 语音转写（ASR） | `agent:view` |
| POST | `/tts/synthesize` | 文本转语音（TTS） | `agent:view` |

写接口要求：

- `Idempotency-Key`
- `X-CSRF-TOKEN`
- `X-Tenant-Id`

### 请求与响应模型（简版）

```typescript
type MultimodalAssetType = 0 | 1 | 2 | 3; // Image/Audio/Video/Text
type MultimodalSourceType = 0 | 1 | 2; // Upload/Url/Generated
type MultimodalAssetStatus = 0 | 1 | 2; // Pending/Processed/Failed

interface MultimodalAssetCreateRequest {
  assetType: MultimodalAssetType;
  sourceType: MultimodalSourceType;
  name?: string;
  mimeType?: string;
  fileId?: string;
  sourceUrl?: string;
  contentText?: string;
  metadataJson?: string;
}

interface VisionAnalyzeRequest {
  assetId?: number;
  imageUrl?: string;
  prompt?: string;
}

interface AsrTranscribeRequest {
  assetId?: number;
  audioUrl?: string;
  languageHint?: string;
  prompt?: string;
}

interface TtsSynthesizeRequest {
  text: string;
  voice?: string;
  format?: string; // mp3/wav/ogg
  language?: string;
}
```

### Agent Chat 多模态输入扩展

`POST /api/v1/agents/{agentId}/chat` 与对应 stream/open/embed 接口新增可选 `attachments`：

```json
{
  "conversationId": null,
  "message": "请结合附件说明风险重点",
  "enableRag": false,
  "attachments": [
    {
      "type": "image",
      "url": "https://example.com/topology.png",
      "fileId": null,
      "mimeType": "image/png",
      "name": "topology.png",
      "text": null
    }
  ]
}
```

约束：

- `message` 与 `attachments` 至少提供一项；
- 每个附件至少提供 `url/fileId/text` 之一；
- 附件元数据会进入对话上下文与消息 metadata，供 Agent 推理链路使用。

---

## Evaluation API 契约（Phase 2）

### 路由前缀

`api/v1/evaluations`

### 端点列表

| 方法 | 路由 | 说明 | 权限 |
|---|---|---|---|
| POST | `/datasets` | 创建评测数据集 | `agent:create` |
| GET | `/datasets` | 分页查询数据集 | `agent:view` |
| POST | `/datasets/{datasetId}/cases` | 添加评测用例 | `agent:update` |
| GET | `/datasets/{datasetId}/cases` | 查询评测用例 | `agent:view` |
| POST | `/tasks` | 创建评测任务（Hangfire 入队，开发环境无 Worker 时内联执行） | `agent:update` |
| GET | `/tasks` | 分页查询评测任务 | `agent:view` |
| GET | `/tasks/{taskId}` | 查询任务详情 | `agent:view` |
| GET | `/tasks/{taskId}/results` | 查询任务结果明细 | `agent:view` |

对比接口：

- `GET /api/v1/evaluations/comparisons?leftTaskId=...&rightTaskId=...`

### 核心状态枚举

```typescript
type EvaluationTaskStatus = 0 | 1 | 2 | 3; // Pending / Running / Completed / Failed
type EvaluationCaseStatus = 0 | 1 | 2 | 3; // Pending / Passed / Failed / Error
```

---

## Open API Projects 契约（Phase 3）

### 路由前缀

`api/v1/open-api-projects`

### 管理端点

| 方法 | 路由 | 说明 | 权限 |
|---|---|---|---|
| GET | `/` | 分页查询当前用户创建的开放应用 | `pat:view` |
| POST | `/` | 创建开放应用（返回一次性 `appSecret`） | `pat:create` |
| PUT | `/{id}` | 更新开放应用（名称/描述/scopes/状态/到期时间） | `pat:update` |
| POST | `/{id}/rotate-secret` | 轮换 `appSecret`（返回一次性明文） | `pat:update` |
| DELETE | `/{id}` | 软删除（禁用）开放应用 | `pat:delete` |

### 令牌交换端点

| 方法 | 路由 | 说明 | 鉴权 |
|---|---|---|---|
| POST | `/token` | 使用 `AppId + AppSecret` 交换开放平台访问令牌 | 匿名（要求 `X-Tenant-Id`） |

请求体：

```json
{
  "appId": "atlas_77b7dce58d6e0a8b",
  "appSecret": "osk_..."
}
```

响应体（`data`）：

```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIs...",
  "tokenType": "Bearer",
  "expiresAt": "2026-03-23T12:00:00+00:00",
  "projectId": 123,
  "appId": "atlas_77b7dce58d6e0a8b",
  "scopes": ["open:*"]
}
```

### Open 接口鉴权扩展

以下 Open 接口现支持两种 Bearer 令牌：

1. 个人访问令牌（PAT）；
2. 开放应用访问令牌（由 `/open-api-projects/token` 交换得到）。

适用接口：

- `/api/v1/open/bots`
- `/api/v1/open/chat/*`
- `/api/v1/open/knowledge/*`
- `/api/v1/open/workflows/*`
- `/api/v1/open/files/*`

两种令牌均继续遵循 scope 校验（如 `open:bots:read`、`open:chat`、`open:*`）。

### 调用治理与统计

#### 速率限制（开放应用维度）

- 对 `token_type=open_project` 的 Open 接口调用按 `tenant + projectId` 维度限流；
- 默认阈值由 `AiPlatform:OpenApiGovernance:ProjectRateLimitPerMinute` 控制；
- 超限返回 HTTP 429，错误码 `RATE_LIMITED`，并附带 `Retry-After` 头。

#### 统计接口

- `GET /api/v1/open-api-stats/summary?projectId=...&fromUtc=...&toUtc=...`
- 权限：`pat:view`

返回 `data` 示例：

```json
{
  "projectId": 123,
  "fromUtc": null,
  "toUtc": null,
  "totalCalls": 200,
  "successCalls": 190,
  "failedCalls": 10,
  "successRate": 0.95,
  "averageDurationMs": 84.2,
  "maxDurationMs": 460
}
```

### 开放事件 Webhook（Open API）

路由前缀：`api/v1/open-api-webhooks`

| 方法 | 路由 | 说明 | 权限 |
|---|---|---|---|
| GET | `/` | 查询订阅列表 | `pat:view` |
| POST | `/` | 创建订阅 | `pat:create` |
| PUT | `/{id}` | 更新订阅 | `pat:update` |
| DELETE | `/{id}` | 删除订阅 | `pat:delete` |
| GET | `/{id}/deliveries` | 查询投递记录 | `pat:view` |
| POST | `/{id}/test` | 发送测试事件 | `pat:update` |

支持关键事件类型：

- `workflow.completed`
- `agent.message`

回调请求头包含：

- `X-Atlas-Signature`（`sha256=...`，HMAC-SHA256）
- `X-Atlas-Event`（事件类型）

### Open API SDK 下载

路由前缀：`api/v1/open-api-sdk`

| 方法 | 路由 | 说明 | 权限 |
|---|---|---|---|
| GET | `/openapi.json` | 下载 OpenAPI 规范 | `pat:view` |
| GET | `/download?language=typescript|csharp` | 下载 SDK 生成包（`openapi.json` + README） | `pat:view` |

## Dynamic Views（v1）与删除检查（P0）

### Dynamic Views API

- `GET /api/v1/dynamic-views`
- `GET /api/v1/dynamic-views/{viewKey}`
- `POST /api/v1/dynamic-views`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `PUT /api/v1/dynamic-views/{viewKey}`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `DELETE /api/v1/dynamic-views/{viewKey}`（需 `Idempotency-Key` + `X-CSRF-TOKEN`，先执行 delete-check）
- `POST /api/v1/dynamic-views/preview`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `POST /api/v1/dynamic-views/{viewKey}/publish`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `GET /api/v1/dynamic-views/{viewKey}/history`
- `POST /api/v1/dynamic-views/{viewKey}/rollback/{version}`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `POST /api/v1/dynamic-views/{viewKey}/records/query`（返回 `DynamicRecordListResult`）
- `GET /api/v1/dynamic-views/{viewKey}/references`
- `GET /api/v1/dynamic-views/{viewKey}/delete-check`

### Delete Check API

- `GET /api/v1/dynamic-tables/{tableKey}/delete-check`
- `GET /api/v1/dynamic-views/{viewKey}/delete-check`

### Dynamic Views（P1 增量）

- `POST /api/v1/dynamic-views/preview-sql`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `DynamicViewSqlPreviewDto`
  - `sql: string`
  - `parameters: Array<{ name: string; value: unknown }>`
  - `warnings: string[]`
  - `fullyPushdown: boolean`
- 执行语义扩展：
  - `join` 支持 `inner/left/right/full`（`right/full` 在 SQLite 场景允许运行时补偿）
  - `union` 支持 `byName/byPosition`
  - `aggregate` 要求节点配置与 `definition.groupBy` 一致，不一致返回业务错误 `DynamicViewAggregateGroupByMismatch`

### Dynamic Transform Jobs（P2 骨架）

- `GET /api/v1/dynamic-transform-jobs`
- `POST /api/v1/dynamic-transform-jobs`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `POST /api/v1/dynamic-transform-jobs/{jobKey}/run`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `POST /api/v1/dynamic-transform-jobs/{jobKey}/pause`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `GET /api/v1/dynamic-transform-jobs/{jobKey}/history`
- DTO：
  - `DynamicTransformJobDto { id, appId, jobKey, name, status, definitionJson, createdAt, updatedAt }`
  - `DynamicTransformExecutionDto { id, jobKey, status, startedAt, endedAt, message }`

### 外部数据源抽取（P2 骨架）

- `POST /api/v1/dynamic-views/external-extract/preview`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- 请求：`DynamicExternalExtractPreviewRequest { dataSourceId, sql, limit }`
- 返回：`DynamicExternalExtractPreviewResult { success, errorMessage, columns[], rows[] }`

### 物理 VIEW 发布（P2 骨架）

- `POST /api/v1/dynamic-views/{viewKey}/publish-physical`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- 请求：`DynamicPhysicalViewPublishRequest { replaceIfExists, physicalViewName }`
- 返回：`DynamicPhysicalViewPublishResult { viewKey, physicalViewName, success, message }`

### 批量导入 / Excel 粘贴（P2 骨架）

- `POST /api/v1/dynamic-tables/{tableKey}/records/import`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `POST /api/v1/dynamic-tables/{tableKey}/records/excel-paste`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- 请求：`DynamicRecordImportRequest { format, content, dryRun }`
- 返回：`DynamicRecordImportResult { totalRows, importedRows, skippedRows, warnings[], errors[] }`

`DeleteCheckResult`:

```json
{
  "canDelete": false,
  "blockers": [
    {
      "type": "view",
      "id": "customer_view",
      "name": "Customer View",
      "path": "/apps/1001/data/designer?viewKey=customer_view"
    }
  ],
  "warnings": []
}
```

### Dynamic Transform Jobs（P2 全量）

- `GET /api/v1/dynamic-transform-jobs`
- `GET /api/v1/dynamic-transform-jobs/{jobKey}`
- `POST /api/v1/dynamic-transform-jobs`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `PUT /api/v1/dynamic-transform-jobs/{jobKey}`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `POST /api/v1/dynamic-transform-jobs/{jobKey}/run`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `POST /api/v1/dynamic-transform-jobs/{jobKey}/pause`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `POST /api/v1/dynamic-transform-jobs/{jobKey}/resume`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `DELETE /api/v1/dynamic-transform-jobs/{jobKey}`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `GET /api/v1/dynamic-transform-jobs/{jobKey}/history?PageIndex=&PageSize=`
- `GET /api/v1/dynamic-transform-jobs/{jobKey}/executions/{executionId}`
- `DynamicTransformJobDto`
  - `id, appId, jobKey, name, status, cronExpression, enabled, lastRunAt, lastRunStatus, lastError, sourceConfigJson, targetConfigJson, definitionJson, createdAt, updatedAt`
- `DynamicTransformExecutionDto`
  - `id, jobKey, status, triggerType, inputRows, outputRows, failedRows, durationMs, errorDetailJson, startedBy, startedAt, endedAt, message`

### 外部数据源抽取（P2 全量）

- `GET /api/v1/dynamic-views/external-extract/data-sources`
- `GET /api/v1/dynamic-views/external-extract/{dataSourceId}/schema`
- `POST /api/v1/dynamic-views/external-extract/preview`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- 安全约束：仅允许当前应用绑定且启用的数据源，SQL 仅允许 `SELECT/CTE` 预览。
- `DynamicExternalExtractPreviewResult`
  - `success, errorMessage, columns[{name,type}], rows[]`

### 物理 VIEW 发布（P2 全量）

- `POST /api/v1/dynamic-views/{viewKey}/publish-physical`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `GET /api/v1/dynamic-views/{viewKey}/physical-publications`
- `POST /api/v1/dynamic-views/{viewKey}/physical-rollback/{version}`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `DELETE /api/v1/dynamic-views/{viewKey}/physical-publications/{publicationId}`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- 约束：`fullyPushdown=true` 才允许发布物理 VIEW；否则返回业务错误。
- `DynamicPhysicalViewPublishResult`
  - `viewKey, publicationId, version, physicalViewName, dataSourceId, status, publishedAt, success, message`
- `DynamicPhysicalViewPublicationDto`
  - `id, viewKey, version, physicalViewName, status, comment, dataSourceId, publishedBy, publishedAt`

### 批量导入 / Excel 粘贴（P2 全量）

- `POST /api/v1/dynamic-tables/{tableKey}/records/import/analyze`（`multipart/form-data`，需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `POST /api/v1/dynamic-tables/{tableKey}/records/import/commit`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `POST /api/v1/dynamic-tables/{tableKey}/records/import`（兼容入口）
- `POST /api/v1/dynamic-tables/{tableKey}/records/excel-paste`（需 `Idempotency-Key` + `X-CSRF-TOKEN`）
- `DynamicRecordImportAnalyzeResult`
  - `sessionId, format, headers[], suggestedMappings[], previewRowCount, previewRows[]`
- `DynamicRecordImportCommitRequest`
  - `sessionId, dryRun, batchSize, mappings[]`
- `DynamicRecordImportResult`
  - `totalRows, importedRows, skippedRows, warnings[], errors[], rowErrors[]`
- `DynamicRecordImportRowError`
  - `rowIndex, field, errorCode, message`

错误码建议：

- `DynamicTableDeleteBlocked`
- `DynamicViewDeleteBlocked`
- `DynamicViewNotFound`
- `DynamicViewVersionNotFound`
- `DynamicViewDefinitionInvalid`

## Team Agent API 契约（Phase 3）

### 路由前缀

- `api/v1/team-agents`
- `api/v1/team-agent-conversations`

### Team Agent 核心模型

```json
{
  "agentType": "team",
  "id": 1482000000000000000,
  "name": "数据建模团队",
  "description": "面向数据管理的建表协作团队",
  "teamMode": "GroupChat | Workflow | Handoff",
  "status": "Draft | Active | Disabled",
  "capabilityTags": ["schema_builder", "knowledge"],
  "defaultEntrySkill": "schema_builder",
  "memberCount": 3,
  "publishedVersion": 1,
  "boundDataAssets": [],
  "members": [
    {
      "agentId": 1,
      "roleName": "业务分析 Agent",
      "responsibility": "拆解业务实体与字段",
      "alias": "analyst",
      "sortOrder": 1,
      "isEnabled": true,
      "promptPrefix": "你负责业务分析。",
      "capabilityTags": ["analysis"]
    }
  ]
}
```

### Team Agent 资源接口

- `GET /api/v1/team-agents`
- `GET /api/v1/team-agents/{id}`
- `POST /api/v1/team-agents`
- `PUT /api/v1/team-agents/{id}`
- `DELETE /api/v1/team-agents/{id}`
- `POST /api/v1/team-agents/{id}/duplicate`
- `POST /api/v1/team-agents/{id}/publish`
- `GET /api/v1/team-agents/templates`
- `POST /api/v1/team-agents/from-template`
- `GET /api/v1/team-agents/dashboard`
- `POST /api/v1/team-agents/migrations/multi-agent-orchestrations`
- `GET /api/v1/team-agents/migrations/multi-agent-orchestrations/status`
- `GET /api/v1/team-agents/executions/{executionId}`

### Team Agent 会话接口

- `GET /api/v1/team-agents/{id}/conversations`
- `POST /api/v1/team-agents/{id}/conversations`
- `POST /api/v1/team-agents/{id}/chat`
- `POST /api/v1/team-agents/{id}/chat/stream`
- `POST /api/v1/team-agents/{id}/chat/cancel`
- `GET /api/v1/team-agent-conversations/{conversationId}`
- `GET /api/v1/team-agent-conversations/{conversationId}/messages`
- `PUT /api/v1/team-agent-conversations/{conversationId}`
- `DELETE /api/v1/team-agent-conversations/{conversationId}`
- `POST /api/v1/team-agent-conversations/{conversationId}/clear-context`
- `POST /api/v1/team-agent-conversations/{conversationId}/clear-history`

### Team Agent SSE 事件

- `orchestration.runtime.selected`
- `conversation.started`
- `round.started`
- `member.message`
- `execution.step`
- `schema.draft.updated`
- `conversation.completed`
- `conversation.failed`

### Team Agent SSE 顺序约束

- 所有模式都必须先输出 `orchestration.runtime.selected`，再输出 `conversation.started`。
- `GroupChat`
  - 典型顺序：`orchestration.runtime.selected -> conversation.started -> round.started -> member.message -> execution.step -> schema.draft.updated? -> conversation.completed|conversation.failed`
  - `round.started` 至少出现 1 次，后续可按轮次重复。
- `Workflow`
  - 典型顺序：`orchestration.runtime.selected -> conversation.started -> execution.step(按工作流节点顺序重复) -> schema.draft.updated? -> conversation.completed|conversation.failed`
  - `round.started` 与 `member.message` 可选，不作为必须事件。
- `Handoff`
  - 典型顺序：`orchestration.runtime.selected -> conversation.started -> execution.step(按交接链顺序重复) -> conversation.completed|conversation.failed`
  - 每个 `execution.step` 应能在明细中看出接手成员与上一步产物。
- 当 `generateSchemaDraft=true` 且团队成功产出草案时，`schema.draft.updated` 应出现在结束事件之前。

### Team Agent 编排运行时

- `GroupChat`、`Workflow`、`Handoff` 统一走 `Semantic Kernel Agents Orchestration`
- 旧 `Multi-Agent` 兼容运行时中，`mode=Parallel` 统一映射到 `Semantic Kernel Concurrent Orchestration`
- 运行时选择通过配置节 `AgentFramework` 控制，当前仅保留 Semantic Kernel 原生运行时
- 流式事件 `orchestration.runtime.selected` 会返回 `runtimeKey`、`frameworkFamily`、`packageId`、`packageVersion`
- 单 Agent 与 Team Agent 成员统一启用 `FunctionChoiceBehavior.Auto(autoInvoke=true)`，并开启 `AllowParallelCalls`、`AllowConcurrentInvocation`
- Agent 绑定插件会在运行时挂载为 Semantic Kernel `KernelPlugin`；知识库检索通过 `knowledge_search.search_knowledge` 函数暴露，不再在业务层预先拼接 RAG system message
- 会话压缩统一使用 `ChatHistoryAgentThread` + `WhiteboardProvider` + `ChatHistoryTruncationReducer`，不再保留自定义工具选择或锚点摘要协议
- 当前仓库约定的默认包版本：
  - `Microsoft.SemanticKernel.Agents.Orchestration`: `1.74.0-preview`
  - `Microsoft.SemanticKernel.Agents.Core`: `1.74.0`
  - `Microsoft.SemanticKernel`: `1.74.0`

### Schema Draft 接口

- `POST /api/v1/team-agents/{id}/schema-drafts`
- `GET /api/v1/team-agents/{id}/schema-drafts`
- `GET /api/v1/team-agents/{id}/schema-drafts/{draftId}`
- `GET /api/v1/team-agents/{id}/schema-drafts/{draftId}/execution-audits`
- `PUT /api/v1/team-agents/{id}/schema-drafts/{draftId}`
- `POST /api/v1/team-agents/{id}/schema-drafts/{draftId}/confirm-create`
- `POST /api/v1/team-agents/{id}/schema-drafts/{draftId}/discard`

### Schema Draft 最小对象

```json
{
  "schemaDraft": "根据需求生成的草案，建议创建 客户表、合同表、回款表。",
  "entities": [
    {
      "tableKey": "customer_123",
      "displayName": "客户表",
      "description": "客户主数据"
    },
    {
      "tableKey": "contract_123",
      "displayName": "合同表",
      "description": "合同主表"
    }
  ],
  "fields": [
    {
      "tableKey": "customer_123",
      "name": "Id",
      "displayName": "主键",
      "fieldType": "Long",
      "allowNull": false,
      "isPrimaryKey": true,
      "isAutoIncrement": true,
      "isUnique": true,
      "sortOrder": 1
    },
    {
      "tableKey": "contract_123",
      "name": "CustomerId",
      "displayName": "客户",
      "fieldType": "Long",
      "allowNull": false,
      "isPrimaryKey": false,
      "isAutoIncrement": false,
      "isUnique": false,
      "sortOrder": 11
    }
  ],
  "relations": [
    {
      "sourceTableKey": "contract_123",
      "sourceField": "CustomerId",
      "relatedTableKey": "customer_123",
      "targetField": "Id",
      "relationType": "ManyToOne",
      "cascadeRule": "Restrict"
    }
  ],
  "indexes": [
    {
      "tableKey": "contract_123",
      "name": "UX_contract_123_ContractNo",
      "isUnique": true,
      "fields": ["ContractNo"]
    }
  ],
  "securityPolicies": [
    {
      "tableKey": "contract_123",
      "fieldName": "TenantId",
      "roleCode": "tenant_admin",
      "canView": true,
      "canEdit": false
    }
  ],
  "openQuestions": [
    {
      "code": "approval_flow",
      "question": "合同是否需要审批流状态字段与审批历史？"
    }
  ],
  "confirmationState": "Pending | Confirmed | Discarded"
}
```

### confirm-create 映射说明

- 先将 `entities + fields + indexes` 映射到 `DynamicTableCreateRequest`
- 再将 `relations` 映射到 `DynamicRelationUpsertRequest`
- 最后将 `securityPolicies` 映射到 `DynamicFieldPermissionUpsertRequest`
- `confirm-create` 必须携带 `Idempotency-Key` 与 `X-CSRF-TOKEN`
- 当 `openQuestions` 非空时，后端必须拒绝确认创建
- 当同一 `Idempotency-Key` 对应不同 payload 时，后端必须返回 `409 / IDEMPOTENCY_CONFLICT`
- 当草案已确认时，后端应直接返回已持久化的 `SchemaDraftConfirmationResponse`，而不是重复创建动态表
- 动态表创建、关系写入、字段权限写入任一步失败时，后端必须尝试补偿删除已创建表
- 无论成功、复用已确认结果、失败还是回滚失败，后端都必须写入 `execution-audits` 明细

### Schema Draft 执行审计对象

`GET /api/v1/team-agents/{id}/schema-drafts/{draftId}/execution-audits`

```json
[
  {
    "id": 1,
    "draftId": 1001,
    "sequence": 1,
    "stage": "confirm-create",
    "action": "create_table",
    "status": "Success",
    "resourceKey": "customer_123",
    "resourceId": "20001",
    "detail": "动态表创建成功",
    "createdAt": "2026-04-01T08:00:00Z"
  },
  {
    "id": 2,
    "draftId": 1001,
    "sequence": 2,
    "stage": "confirm-create",
    "action": "rollback_table",
    "status": "Success",
    "resourceKey": "customer_123",
    "resourceId": "20001",
    "detail": "创建失败后已回滚",
    "createdAt": "2026-04-01T08:00:02Z"
  }
]
```

约束：

- `sequence` 按同一草案内递增。
- `status` 当前最少支持 `Success` / `Failed`。
- `action` 典型值包括：
  - `create_table`
  - `set_relations`
  - `set_field_permissions`
  - `reuse_confirmed_result`
  - `rollback_table`
  - `rollback_failed`

## Team Agent 第二阶段契约增量

### 列表筛选与工作台聚合

- `GET /api/v1/team-agents`
  - 新增查询参数：
    - `teamMode=GroupChat|Workflow|Handoff`
    - `status=Draft|Active|Disabled`
    - `capabilityTag=<tag>`
    - `defaultEntrySkill=<skill>`
- `GET /api/v1/team-agents/dashboard`
  - 返回：
    - `totalCount`
    - `teamCount`
    - `availableSubAgentCount`
    - `recentRunCount`
    - `schemaBuilderCount`
    - `recentActivities[]`

### 成员绑定模型

- `TeamAgentMemberInput.agentId` 改为可空。
- `TeamAgentMemberItem` 新增：
  - `bindingState: "bound" | "unbound"`
- 约束：
  - 启用成员必须绑定单 Agent。
  - 整个 Team 至少一个启用且已绑定成员。

### 模板创建

- `POST /api/v1/team-agents/from-template`
  - 新增 `memberBindings[]`

```json
{
  "templateKey": "schema_builder",
  "name": "模板创建的数据建模团队",
  "description": "模板创建示例",
  "memberBindings": [
    {
      "roleName": "业务分析 Agent",
      "agentId": 1,
      "isEnabled": true
    }
  ]
}
```

### 发布历史

- `POST /api/v1/team-agents/{id}/publish`
  - 请求体：

```json
{
  "releaseNote": "阶段二官方运行时与发布快照"
}
```

- `GET /api/v1/team-agent-publications/team-agents/{id}`

### Schema Draft 列表

- `GET /api/v1/team-agents/{id}/schema-drafts`

### 旧 Multi-Agent 迁移

- `POST /api/v1/team-agents/migrations/multi-agent-orchestrations`
- `GET /api/v1/team-agents/migrations/multi-agent-orchestrations/status`

```json
{
  "legacyIds": [1001, 1002]
}
```

迁移状态返回要点：

- `totalCount`
- `migratedCount`
- `pendingCount`
- `items[]`
  - `legacyId`
  - `legacyName`
  - `legacyMode`
  - `migrationStatus`
  - `teamAgentId`
  - `teamAgentName`
  - `migratedAt`
  - `replacementApi`
  - `sunsetAt`

### 旧 Multi-Agent 弃用说明

- `api/v1/multi-agent-orchestrations` 自 `2026-04-01` 起进入弃用窗口。
- 替代资源为 `api/v1/team-agents`。
- 当前弃用截止日期为 `2026-10-01`。
- 弃用窗口内旧接口仅允许安全修复与关键缺陷修复，不再新增产品能力。

## Agent Team 协同系统（MVP）

### 核心资源

- 团队定义：`AgentTeamDefinition`
- 子代理：`SubAgentDefinition`
- 编排节点：`OrchestrationNodeDefinition`
- 发布版本：`TeamVersion`
- 执行实例：`ExecutionRun`
- 节点执行：`NodeRun`

### 团队管理

- `GET /api/v1/agent-teams`
- `POST /api/v1/agent-teams`
- `GET /api/v1/agent-teams/{id}`
- `PUT /api/v1/agent-teams/{id}`
- `DELETE /api/v1/agent-teams/{id}`（仅 Draft）
- `POST /api/v1/agent-teams/{id}/duplicate`
- `POST /api/v1/agent-teams/{id}/disable`
- `POST /api/v1/agent-teams/{id}/enable`

### 子代理管理

- `GET /api/v1/agent-teams/{teamId}/sub-agents`
- `POST /api/v1/agent-teams/{teamId}/sub-agents`
- `PUT /api/v1/agent-teams/{teamId}/sub-agents/{subAgentId}`
- `DELETE /api/v1/agent-teams/{teamId}/sub-agents/{subAgentId}`

### 编排节点管理

- `GET /api/v1/agent-teams/{teamId}/nodes`
- `POST /api/v1/agent-teams/{teamId}/nodes`
- `PUT /api/v1/agent-teams/{teamId}/nodes/{nodeId}`
- `DELETE /api/v1/agent-teams/{teamId}/nodes/{nodeId}`
- `POST /api/v1/agent-teams/{teamId}/nodes/validate`

### 发布与版本

- `POST /api/v1/agent-teams/{id}/publish`
- `GET /api/v1/agent-teams/{id}/versions`
- `POST /api/v1/agent-teams/{id}/rollback/{versionId}`

### 运行与介入

- `POST /api/v1/agent-team-runs`
- `GET /api/v1/agent-team-runs/{runId}`
- `GET /api/v1/agent-team-runs/{runId}/nodes`
- `GET /api/v1/agent-team-runs/{runId}/interventions`
- `POST /api/v1/agent-team-runs/{runId}/cancel`
- `POST /api/v1/agent-team-runs/{runId}/nodes/{nodeRunId}/intervene`

### 调试

- `POST /api/v1/agent-teams/{id}/debug`
- `POST /api/v1/agent-teams/{id}/sub-agents/{agentId}/debug`

### 状态枚举（MVP）

- 团队状态：`Draft | Ready | Published | Disabled | Archived`
- 执行状态：`Pending | Planning | Dispatching | Running | WaitingTool | WaitingHuman | Retrying | PartiallyFailed | Failed | Completed | Cancelled | TimedOut`
- 节点状态：`Idle | Ready | WaitingDependency | Assigned | Running | WaitingInput | WaitingTool | WaitingApproval | Retrying | Succeeded | Failed | Skipped | Cancelled`
