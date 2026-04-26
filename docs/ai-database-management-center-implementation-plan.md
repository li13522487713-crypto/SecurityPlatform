# AI 数据库管理中心实现计划

本文档是“数据库数据源管理 + 数据库/Schema 结构管理一体化页面”的实施前扫描结果与实现计划。结论来自当前仓库只读探查，后续实现必须以本文列出的真实路径和边界为准。

## 1. 当前资源库页面路径

- 资源库页面：`src/frontend/apps/app-web/src/app/pages/workspace-library-page.tsx`
- 关键符号：`WorkspaceLibraryPage`、`TAB_DEFS`、`cozeLibraryTabDatabase`
- 当前入口：`/space/:space_id/library?tab=database`
- 旧资源路径重定向：`src/frontend/apps/app-web/src/app/pages/workspace-resources-redirect.tsx`

补充要求中的 `https://www.coze.cn/space/7460319026326405171/library` 应落在该资源库页面的数据库 Tab 下。实现时优先保持 `/space/:space_id/library?tab=database` 作为入口，并把数据库 Tab 内的内容升级为管理中心视图。

## 2. 当前数据库 Tab 实现路径

- 数据库 Tab 定义：`src/frontend/apps/app-web/src/app/pages/workspace-library-page.tsx`
- 当前数据库 Tab 通过 `getLibraryPaged` 加载资源库数据库资源。
- 当前结构管理动作：`handleOpenStructure()` 跳转到 `/space/${workspace.id}/database/${record.resourceId}/structure`。
- 当前独立 AI 数据库判断：`record.resourceType === "database" && record.subType !== "datasource"`。

## 3. 当前 app 路由路径

- 路由文件：`src/frontend/apps/app-web/src/app/app.tsx`
- `/space/:space_id/library`：资源库页面。
- `/space/:space_id/database/:databaseId/structure`：当前结构管理页。
- `/space/:space_id/database/:id` 与 `/space/:space_id/databases/:id`：当前数据库详情兼容路由。

本次会继续嵌入 WorkspaceShell 与资源库体系，避免新增脱离资源库的孤立顶层入口。

## 4. 当前 workspace route helper 路径

- `src/frontend/packages/app-shell-shared/src/workspace-routes.ts`
- `src/frontend/packages/app-shell-shared/src/routes.ts`

当前已有 library、database detail 的部分 helper；本次需要补充数据库中心/结构管理 helper，避免继续手写 `/space/.../database/...`。

## 5. 当前 requestApi 封装路径

- app-web 导出：`src/frontend/apps/app-web/src/services/api-core.ts`
- 共享实现：`src/frontend/packages/shared-react-core/src/api/createApiClient.ts`

`requestApi` 默认基于 `/api/v1`，并装配 `Authorization` 与 `X-Tenant-Id`。新增前端 API client 必须全部使用 `requestApi`，不得 mock。

## 6. 当前 i18n messages 路径

- 主 messages：`src/frontend/apps/app-web/src/app/messages.ts`
- i18n hook：`src/frontend/apps/app-web/src/app/i18n.tsx`
- AI 数据库服务层消息：`src/frontend/apps/app-web/src/services/ai-database.i18n.ts`

语言持久化键为 `atlas_locale`。本次新增 `databaseCenter.*` 与 `databaseStructure.*` 中英文文案，并运行 `pnpm run i18n:check`。

## 7. 当前 Semi Design 使用方式

- 资源库页面直接使用 `@douyinfe/semi-ui` 与 `@douyinfe/semi-icons`。
- 当前结构页使用 `Button / Dropdown / Space / Spin / Table / Tabs / Tag / Toast / Typography`。
- 全局样式：`src/frontend/apps/app-web/src/app/app.css`

本次 UI 继续使用 Semi Design，不引入 Ant Design、MUI、Chakra UI 等新组件库；业务样式放到数据库中心专属样式文件或组件内，不扩写无关全局样式。

## 8. 当前 bot-monaco-editor 路径

- 当前 SQL 编辑器组件：`src/frontend/apps/app-web/src/app/pages/database-structure/sql-code-editor.tsx`
- 包路径：`src/frontend/packages/arch/bot-monaco-editor`
- 包名：`@coze-arch/bot-monaco-editor`

SQL 编辑器继续复用该包；SQL 格式化在 app-web 新增纯函数工具。

## 9. 当前 AiDatabase 实体路径

- `src/backend/Atlas.Domain/AiPlatform/Entities/AiDatabase.cs`

现有字段已包含 `StorageMode`、`DriverCode`、`EncryptedDraftConnection`、`EncryptedOnlineConnection`、`PhysicalDatabaseName`、`DraftDatabaseName`、`OnlineDatabaseName`、`ProvisionState`。旧 `TableSchema`、`SchemaVersion`、`PublishedVersion`、`RecordCount`、`DraftTableName`、`OnlineTableName` 已标记 `Obsolete`，后续结构管理不得依赖。

## 10. 当前 AiDatabase Controller 路径

- AppHost：`src/backend/Atlas.AppHost/Controllers/AiDatabasesController.cs`
- 历史兼容资产（退役）：`src/backend/Atlas.PlatformHost/Controllers/AiDatabasesController.cs`

AppHost 是当前权威后端入口。调整创建流程时保持现有 CRUD、records、bulk、imports、mode、channel-config 兼容。

## 11. 当前 TenantDataSource 相关路径

- 实体：`src/backend/Atlas.Domain/System/Entities/TenantDataSource.cs`
- DTO：`src/backend/Atlas.Application/System/Models/TenantDataSourceModels.cs`
- 服务接口：`src/backend/Atlas.Application/System/Abstractions/ITenantDataSourceService.cs`
- 服务实现：`src/backend/Atlas.Infrastructure/Services/TenantDataSourceService.cs`
- 仓储：`src/backend/Atlas.Infrastructure/Repositories/TenantDataSourceRepository.cs`
- 历史控制器兼容资产（退役）：`src/backend/Atlas.PlatformHost/Controllers/TenantDataSourcesController.cs`

本次不修改 `TenantDataSource` 表结构，不把独立 AI 数据库物理实例混入租户数据源通用表。

## 12. 当前 DataSourceDriverRegistry 路径

- `src/backend/Atlas.Infrastructure/Services/DataSourceDriverRegistry.cs`

当前支持 SQLite、SqlServer、MySql、PostgreSQL、Oracle、Dm、Kdbndp、Oscar、Access。新结构管理未知 driver 必须抛错，禁止 fallback 到 SQLite。

## 13. 当前 SqlSugar 注册路径

- 主注册：`src/backend/Atlas.Infrastructure/ServiceCollectionExtensions.cs`
- 平台服务注册：`src/backend/Atlas.Infrastructure/PlatformServiceCollectionExtensions.cs`
- ORM 实体目录：`src/backend/Atlas.Infrastructure/Services/AtlasOrmSchemaCatalog.cs`

新增实体必须加入 `AtlasOrmSchemaCatalog`，并注册仓储与服务。

## 14. 当前权限策略 PermissionPolicies 路径

- `src/backend/Atlas.Presentation.Shared/Authorization/PermissionPolicies.cs`

现有 `DataSourcesView`、`DataSourcesQuery`、`DataSourcesSchemaWrite`、`AiDatabaseView/Create/Update/Delete` 可复用。本次新增或复用 `AiDatabaseHostProfileManage` 用于托管配置管理。

## 15. 当前审计日志能力路径

- 审计接口：`src/backend/Atlas.Application/Audit/Abstractions/IAuditWriter.cs`
- 审计实体：`src/backend/Atlas.Domain/Audit/Entities/AuditRecord.cs`
- AI 数据库审计调用：`src/backend/Atlas.AppHost/Controllers/AiDatabasesController.cs`
- 当前结构管理审计调用：`src/backend/Atlas.AppHost/Controllers/DatabaseStructureController.cs`

新增 HostProfile、结构 DDL、删除表/视图、连接测试等写操作需要审计，并 mask 敏感信息。

## 16. 当前连接串加密能力路径

- 加密配置：`src/backend/Atlas.Infrastructure/Options/DatabaseEncryptionOptions.cs`
- AES 实现：`src/backend/Atlas.Infrastructure/Services/TenantDbConnectionFactory.cs`
- 迁移控制台保护：`src/backend/Atlas.Infrastructure/Services/SetupConsole/MigrationSecretProtector.cs`
- 当前连接串脱敏：`src/backend/Atlas.Infrastructure/Services/DatabaseStructure/ConnectionStringMasker.cs`

本次新增 `IAiDatabaseSecretProtector`，优先复用现有加密能力，输出 DTO 不返回加密字段和明文连接串。

## 17. 当前 atlas_data_json 使用点

- `src/backend/Atlas.Infrastructure/Services/AiPlatform/AiDatabasePhysicalTableService.cs`
- `src/backend/Atlas.Infrastructure/Services/SetupConsole/MigrationSqlSugarScopeFactory.cs`
- 旧记录实体：`src/backend/Atlas.Domain/AiPlatform/Entities/AiDatabaseRecord.cs`

旧 JSON 行模型保留兼容，但新数据库/Schema 结构管理不得以 `atlas_data_json` 为数据来源。

## 18. 当前 Number(id) / parseInt(id) 历史遗留位置

- `src/frontend/apps/app-web/src/services/api-ai-database.ts`：`recordId/taskId/fileId` 转 number，属于记录与任务旧契约，不得扩散到 `databaseId`。
- `src/frontend/apps/app-web/src/app/pages/components/migration-wizard/migration-wizard-drawer.tsx`：`aiDatabaseId: Number(source.resourceId)` 是数据库相关风险点。
- `src/frontend/apps/app-web/src/app/pages/components/library-create-modal.tsx`：知识库/插件分支存在 `Number(workspaceId)`。
- `src/frontend/apps/app-web/src/app/app.tsx`：插件/知识库详情存在 `Number(id)`；数据库详情本身使用字符串 `databaseId={id}`。
- `parseInt` 主要在状态页与 setup migration 页，当前未发现数据库链路使用。

本次新增 `databaseId/profileId/instanceId/spaceId/resourceId` 全部 string 透传，禁止新增 `Number(id)`、`parseInt(id)`、`+id`。

## 19. 本次新增后端实体清单

- `AiDatabaseHostProfile`
  - 用于保存数据库托管配置，替代 appsettings 中写死 MySQL/PostgreSQL AdminConnection。
  - 密码、AdminConnection 加密存储。
- `AiDatabasePhysicalInstance`
  - 保存每个 AI 数据库 Draft / Online 物理实例。
  - 记录 HostProfile、物理库名、Schema、连接、ProvisionState、连接测试信息。

同时修改 `AiDatabase`：

- 新增 `DefaultHostProfileId`
- 新增 `DraftInstanceId`
- 新增 `OnlineInstanceId`
- 补齐创建流程需要的 provision 相关 setter
- 保留旧字段且继续 `Obsolete`

## 20. 本次新增后端服务清单

- `IAiDatabaseHostProfileService`
- `IAiDatabaseProvisioningService`
- `IAiDatabasePhysicalInstanceService`
- `IAiDatabaseSecretProtector`
- `IDatabaseStructureService` 扩展 schema/object/tree 能力
- `ISqlSafetyValidator` 补强扫描器能力
- `IDatabaseDialectRegistry` 与各方言补强

现有 `IAiDatabaseProvisioner` 和 `AiDatabaseProvisionService` 会迁移/适配到新服务，不能继续依赖 appsettings AdminConnection。

## 21. 本次新增后端 Controller 清单

- `AiDatabaseHostProfilesController`
  - `/api/v1/ai-database-host-profiles`
- `DatabaseManagementController`
  - `/api/v1/database-center`
- `DatabaseStructureController` 新路由补强
  - `/api/v1/database-center/sources/{sourceId}/schemas/{schemaName}/structure`

同时调整 `AiDatabasesController` 创建流程，支持 driver、hostProfile、provisionMode、environmentMode、物理库/Schema 参数。

## 22. 本次新增前端页面清单

- `src/frontend/apps/app-web/src/app/pages/database-center/database-center-page.tsx`

该页面作为资源库数据库 Tab 下的内容载体。路由层可以继续保留当前结构页兼容路径，同时数据库 Tab 内直接渲染管理中心。

## 23. 本次新增前端组件清单

- `DatabaseCenterPage.tsx`
- `DatabaseCenterShell.tsx`
- `DatabaseSourcePanel.tsx`
- `DatabaseSourceList.tsx`
- `DatabaseSchemaTree.tsx`
- `DatabaseStructureWorkbench.tsx`
- `StructureWorkbenchTabs.tsx`
- `ErDiagramCanvas.tsx`
- `ErTableNode.tsx`
- `ErRelationEdge.tsx`
- `TableObjectList.tsx`
- `ViewObjectList.tsx`
- `ProcedureObjectList.tsx`
- `TriggerObjectList.tsx`
- `SqlEditorPanel.tsx`
- `SqlFormatPanel.tsx`
- `DataPreviewPanel.tsx`
- `DdlPanel.tsx`
- `InstanceDetailPanel.tsx`
- `InstanceBasicInfo.tsx`
- `InstanceConnectionInfo.tsx`
- `InstanceConfigInfo.tsx`
- `InstancePerformancePanel.tsx`
- `CreateTableDrawer.tsx`
- `VisualTableDesigner.tsx`
- `SqlCreateTablePanel.tsx`
- `CreateViewDrawer.tsx`
- `DangerDeleteModal.tsx`
- `HostProfileManageDrawer.tsx`
- `HostProfileForm.tsx`
- `AiDatabaseCreateWizard.tsx`
- `data-type-options.ts`
- `sql-format-utils.ts`
- `useDatabaseCenter.ts`
- `useDatabaseStructure.ts`
- `useErDiagramState.ts`
- `useSqlFormatter.ts`

可复用现有 `database-structure` 目录组件，但需要拆分职责，不能把所有逻辑塞进单文件。

## 24. 本次新增 API client 清单

- `src/frontend/apps/app-web/src/services/api-ai-database-host-profiles.ts`
- `src/frontend/apps/app-web/src/services/api-database-center.ts`
- `src/frontend/apps/app-web/src/services/api-database-structure.ts` 扩展到新 `/database-center` 路由并保留旧兼容导出

全部真实请求，全部使用 `requestApi`，错误展示后端 message，traceId 可复制。

## 25. 风险点和验证策略

### 主要风险

- 连接串泄漏：DTO、日志、异常、审计、前端详情面板均不得返回或展示明文密码、明文连接串、明文 AdminConnection。
- 权限扩大：前端按钮隐藏不是安全边界，后端必须使用 `Authorize`、资源写门禁和审计。
- Draft/Online 混淆：结构编辑只允许 Draft；Online 默认只读展示。
- 旧 JSON 行模型兼容：旧 records、导入、工作流节点、迁移控制台 `CurrentSystemAiDatabase` 仍可能依赖旧字段，不得破坏。
- appsettings AdminConnection：当前 `AiDatabaseHostingOptions` 仍支持 MySQL/PG AdminConnection，本次必须迁移到数据库 HostProfile 加密存储，保留配置项仅作兼容且不再作为新路径核心。
- SQL 安全绕过：后端 `ISqlSafetyValidator` 必须识别字符串、注释、反引号、方括号、MySQL executable comments、多语句注入。
- ID 类型回退：当前后端结构 Controller 仍 `long.TryParse(databaseId)`，本次新 API 必须 string 透传；内部如需兼容旧 long 主键，解析边界只允许在仓储查询适配层，不能向前端暴露 number 语义。

### 验证策略

- 后端：
  - `dotnet build Atlas.SecurityPlatform.slnx`
  - `dotnet test tests/Atlas.SecurityPlatform.Tests --filter "FullyQualifiedName~SqlSafetyValidatorTests|FullyQualifiedName~DatabaseDialectTests|FullyQualifiedName~DatabaseStructureServiceSqliteTests|FullyQualifiedName~SqliteSchemaAlignmentTests"`
  - 补充 HostProfile、PhysicalInstance、SQLite provision、MySQL/PostgreSQL dialect/provision 测试。
  - 更新 `src/backend/Atlas.AppHost/Bosch.http/DatabaseStructure.http`，新增 `DatabaseCenter.http` 与 `AiDatabaseHostProfiles.http`。
- 前端：
  - `cd src/frontend && pnpm run build:app-web`
  - `cd src/frontend && pnpm run lint`
  - `cd src/frontend && pnpm run i18n:check`
- 浏览器：
  - 进入 `/space/:space_id/library?tab=database`
  - 新建托管配置、测试连接、新建 AI 数据库、加载数据源、加载 Schema 树、查看 ER 图、表/视图/DDL/预览、可视化建表、SQL 建表、创建视图、危险 SQL 拒绝、删除表/视图二次确认、权限不足 403。

## 26. 实施顺序

严格按用户要求顺序推进：

1. 新增后端 HostProfile / PhysicalInstance 实体。
2. 修改 `AiDatabase` 实体。
3. 实现 `SecretProtector`。
4. 实现 HostProfile、Provisioning、PhysicalInstance、Dialect、SqlSafety、DatabaseStructure 服务。
5. 实现 HostProfiles、AiDatabase 创建调整、DatabaseCenter、DatabaseStructure Controller。
6. 注册 DI / 权限 / 配置。
7. 写 `.http` 和后端测试。
8. 实现前端 API client。
9. 实现资源库数据库 Tab 下的一体化管理中心页面。
10. 补齐 i18n。
11. 运行 build/lint/test。
12. 输出 delivery report。
