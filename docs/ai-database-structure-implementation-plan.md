# AI 数据库独立物理库结构管理实现计划

> 生成时间：2026-04-25。  
> 范围：仅实现独立 AI 数据库的 draft/online 物理库、结构管理、DDL、安全校验与 app-web 全屏结构管理页。不改 TenantDataSource、数据迁移控制台、通用资源中心数据源模型。

## 1. 当前扫描结论

1. 当前 `AiDatabase` 实体路径：`src/backend/Atlas.Domain/AiPlatform/Entities/AiDatabase.cs`。已存在 `StorageMode`、`DriverCode`、`EncryptedDraftConnection`、`EncryptedOnlineConnection`、`PhysicalDatabaseName`、`ProvisionState`、`ProvisionError`，但缺少 `DraftDatabaseName`、`OnlineDatabaseName`、`DialectVersion`，旧 JSON 行模型字段未标记 `Obsolete`。
2. 当前 `AiDatabase` Controller 路径：`src/backend/Atlas.AppHost/Controllers/AiDatabasesController.cs` 与历史兼容 `src/backend/Atlas.PlatformHost/Controllers/AiDatabasesController.cs`。当前运行拓扑以 `AppHost` 为准。
3. 当前 AI 数据库前端列表页路径：`src/frontend/apps/app-web/src/app/pages/workspace-library-page.tsx` 的资源库 `database` Tab；另有 `src/frontend/packages/module-studio-react/src/pages.tsx` 的 Studio 数据库中心。
4. 当前资源库数据库 Tab 实现路径：`src/frontend/apps/app-web/src/app/pages/workspace-library-page.tsx`，关键符号 `TAB_DEFS`、`handleOpenStructure`、`handleDeleteDatabase`。
5. 当前路由配置路径：`src/frontend/apps/app-web/src/app/app.tsx`，已存在 `/space/:space_id/database/:databaseId/structure`，渲染 `DatabaseStructurePage`。
6. 当前 `requestApi` 封装路径：`src/frontend/apps/app-web/src/services/api-core.ts`，底层 `createApiClient` 在 `src/frontend/packages/shared-react-core/src/api/createApiClient.ts`。
7. 当前 i18n messages 路径：`src/frontend/apps/app-web/src/app/messages.ts`；AI 数据库 API 错误文案另有 `src/frontend/apps/app-web/src/services/ai-database.i18n.ts`。
8. 当前 `PermissionPolicies` 路径：`src/backend/Atlas.Presentation.Shared/Authorization/PermissionPolicies.cs`，已存在 `DataSourcesView`、`DataSourcesQuery`、`DataSourcesSchemaWrite`。
9. 当前认证策略注册路径：`src/backend/Atlas.AppHost/Program.cs` 的 `AddAuthorization`；历史兼容宿主为 `src/backend/Atlas.PlatformHost/Program.cs`。
10. 当前 `DataSourceDriverRegistry` 路径：`src/backend/Atlas.Infrastructure/Services/DataSourceDriverRegistry.cs`，未知 driver 当前会返回原字符串，需要在 AI 数据库创建/provision 前改为显式拒绝。
11. 当前 SqlSugar 注册路径：`src/backend/Atlas.Infrastructure/ServiceCollectionExtensions.cs`、`src/backend/Atlas.AppHost/Program.cs`、`src/backend/Atlas.Infrastructure/PlatformServiceCollectionExtensions.cs`。
12. 当前 `TenantDbConnectionFactory` 与加密方法路径：`src/backend/Atlas.Infrastructure/Services/TenantDbConnectionFactory.cs`，关键符号 `Encrypt`、`Decrypt`。
13. 当前 `AppDbScopeFactory` 或类似连接池缓存路径：`src/backend/Atlas.Infrastructure/Services/AppDbScopeFactory.cs`；AI 数据库结构侧已有初版 `src/backend/Atlas.Infrastructure/Services/DatabaseStructure/AiDatabaseClientFactory.cs`，但仅临时创建 client，未实现要求的缓存、淘汰与超时配置。
14. 当前 `SqlQueryService` 或可复用数据预览代码路径：`src/backend/Atlas.Infrastructure/Services/SqlQueryService.cs`；结构管理已有初版 `DatabaseStructureService`，需要替换为符合 draft/online、分页、截断、错误映射的实现。
15. 当前 `AuditWriter` 路径：接口 `src/backend/Atlas.Application/Audit/Abstractions/IAuditWriter.cs`，控制器使用 `AuditRecord` 直接写审计。
16. 当前 RateLimiter 配置：未发现结构管理/AI 数据库专属 ASP.NET `AddRateLimiter/UseRateLimiter` 策略；仅有 OpenAPI 项目限流 `OpenApiProjectRateLimiter` 与基础 `IRateLimiter`。
17. 当前 `bot-monaco-editor` 组件路径：`src/frontend/packages/arch/bot-monaco-editor/src/index.tsx`，结构管理页当前仍用 `Input.TextArea`，需要改接 `@coze-arch/bot-monaco-editor`。
18. 当前 Semi Design 使用模式：app-web 页面使用 `@douyinfe/semi-ui` 的 `Button`、`Dropdown`、`Table`、`Tabs`、`Modal`、`Toast`、`Typography`；新增 UI 必须沿用 Semi 与 `_shared` 组件。
19. 当前工作区路由 helper 路径：`src/frontend/packages/app-shell-shared/src/workspace-routes.ts`；缺少 `workspaceDatabaseStructurePath(workspaceId: string, databaseId: string)`。
20. 当前 `Number(id)` 历史遗留位置清单：
    - `src/frontend/apps/app-web/src/services/api-ai-database.ts`：`createAiDatabase`、`createAiDatabaseRecord`、`submitAiDatabaseImport` 返回值转 number，多个 API 参数类型为 number。
    - `src/frontend/apps/app-web/src/app/pages/database-structure-page.tsx`：`getAiDatabaseById(Number(databaseId))`。
    - `src/frontend/apps/app-web/src/app/pages/workspace-library-page.tsx`：`deleteAiDatabase(Number(record.resourceId))`。
    - `src/frontend/apps/app-web/src/app/app.tsx`：`StudioDatabaseDetailRoute`、`SpaceDatabaseDetailRoute` 使用 `Number(id)`。
    - `src/frontend/apps/app-web/src/app/pages/components/migration-wizard/migration-wizard-drawer.tsx`：迁移向导仍将 `aiDatabaseId`/`dataSourceId` 转 number；本任务不改迁移设计，但需文档标注旧入口风险。
21. 需要废弃但不删除的旧路径：
    - `src/backend/Atlas.Infrastructure/Services/AiPlatform/AiDatabasePhysicalTableService.cs`。
    - `src/backend/Atlas.Domain/AiPlatform/Entities/AiDatabaseRecord.cs` 与 `AiDatabaseRecordRepository`。
    - `AiDatabase.TableSchema`、`DraftTableName`、`OnlineTableName`、`RecordCount`、`SchemaVersion`、`PublishedVersion`。
    - 依赖 JSON 行模型的 workflow 节点与低代码兼容入口：`AiDatabaseNodeHelper`、`DatabaseQueryNodeExecutor`、`DatabaseInsertNodeExecutor`、`DatabaseUpdateNodeExecutor`、`DatabaseDeleteNodeExecutor`、`WorkflowEngineeringServices`。

## 2. 已有初版实现与缺口

仓库已存在结构管理初版：

- 后端：`src/backend/Atlas.Infrastructure/Services/DatabaseStructure/*`、`src/backend/Atlas.Application/AiPlatform/Models/DatabaseStructureModels.cs`、`src/backend/Atlas.Application/AiPlatform/Abstractions/IDatabaseStructureService.cs`。
- Controller：仅 `src/backend/Atlas.PlatformHost/Controllers/DatabaseStructureController.cs`，而权威宿主 `AppHost` 缺失。
- 前端：`src/frontend/apps/app-web/src/services/api-database-structure.ts`、`src/frontend/apps/app-web/src/app/pages/database-structure-page.tsx`、`data-type-options.ts`。

主要缺口：

- 方言接口未达到任务要求，SQLite/MySQL/PG 缺少完整 DDL/类型/对象列表/注释/自增规则，Oracle/DM/Kingbase/Oscar 只是继承 PG，属于名义支持。
- SQL safety 仍主要靠正则与注释剥离，未满足 tokenizer、多语句、MySQL 特殊注释、危险函数等要求。
- provision 仅真实支持 SQLite；MySQL/PG 未实现 admin connection 创建独立 database/schema。
- `AiDatabaseClientFactory` 无缓存淘汰、无 timeout、无 mask 异常，接口名也不符合 `GetClientAsync/RemoveFromCache/TestConnectionAsync`。
- 结构服务缺少 `procedure/trigger` 独立接口、对象不存在 404、系统对象保护、预览截断、审计统一、错误映射与 view mode A/B。
- 前端结构管理页是全屏路由，但内部大量使用 `SideSheet`，未拆 `CreateTableDrawer`、`CreateViewDrawer`、`DatabaseObjectDetailDrawer`、`DangerDeleteModal`，SQL 编辑器未用 monaco，`databaseId` 被转 number。

## 3. 推荐实现方向

1. 以现有初版为基础重构，不再新增平行体系。
2. `AiDatabase` 元数据保留旧字段兼容，但新结构管理只读写 `DriverCode`、`EncryptedDraftConnection`、`EncryptedOnlineConnection`、`DraftDatabaseName`、`OnlineDatabaseName`、`DialectVersion`、`ProvisionState`。
3. PostgreSQL 采用“同一 admin connection 指向的 database 内创建独立 schema”方案：每个 AI 数据库创建 `draft` 与 `online` 两个 schema，业务连接串复用 admin connection 的 database，但连接后由方言使用 schema 限定对象名。该方案比动态 `CREATE DATABASE` 更易落地，权限要求更低。
4. SQLite 每个 AI 数据库创建两个 `.db` 文件；MySQL 每个 AI 数据库创建两个 database；PostgreSQL 每个 AI 数据库创建两个 schema。
5. 所有结构写操作仅 draft；online 只 provision 和连接测试，不开放 UI 编辑。
6. `DatabaseStructureController` 必须迁入/复制到 `AppHost`，`PlatformHost` 只保留兼容。
7. 前端所有 AI 数据库 ID 类型改为 string，重点清理结构管理链路与资源库操作链路的 `Number(id)`。

## 4. 需要修改的文件清单

- `src/backend/Atlas.Domain/AiPlatform/Entities/AiDatabase.cs`
- `src/backend/Atlas.Application/AiPlatform/Models/AiDatabaseModels.cs`
- `src/backend/Atlas.Application/AiPlatform/Models/DatabaseStructureModels.cs`
- `src/backend/Atlas.Application/AiPlatform/Abstractions/IAiDatabaseProvisioner.cs`
- `src/backend/Atlas.Application/AiPlatform/Abstractions/IDatabaseStructureService.cs`
- `src/backend/Atlas.Application/AiPlatform/Abstractions/ISqlSafetyValidator.cs`
- `src/backend/Atlas.Infrastructure/Options/AiDatabaseHostingOptions.cs`
- `src/backend/Atlas.Infrastructure/Services/AiPlatform/AiDatabaseProvisionService.cs`
- `src/backend/Atlas.Infrastructure/Services/DatabaseStructure/*`
- `src/backend/Atlas.Infrastructure/PlatformServiceCollectionExtensions.cs`
- `src/backend/Atlas.Presentation.Shared/Authorization/PermissionPolicies.cs`
- `src/backend/Atlas.Application/Identity/PermissionCodes.cs`
- `src/backend/Atlas.Application/Identity/AppPermissionSeedCatalog.cs`
- `src/backend/Atlas.AppHost/Program.cs`
- `src/backend/Atlas.AppHost/appsettings*.json`
- `src/frontend/apps/app-web/src/services/api-ai-database.ts`
- `src/frontend/apps/app-web/src/services/api-database-structure.ts`
- `src/frontend/apps/app-web/src/app/pages/workspace-library-page.tsx`
- `src/frontend/apps/app-web/src/app/pages/database-structure-page.tsx`
- `src/frontend/apps/app-web/src/app/app.tsx`
- `src/frontend/apps/app-web/src/app/messages.ts`
- `src/frontend/packages/app-shell-shared/src/workspace-routes.ts`
- `src/frontend/packages/app-shell-shared/src/index.ts`
- `docs/contracts.md`

## 5. 需要新增的文件清单

- `src/backend/Atlas.Infrastructure/Services/DatabaseStructure/Dialects/*.cs`
- `src/backend/Atlas.Infrastructure/Services/DatabaseStructure/CreateTableDefinition.cs`
- `src/backend/Atlas.Infrastructure/Services/DatabaseStructure/CreateViewDefinition.cs`
- `src/backend/Atlas.Infrastructure/Services/DatabaseStructure/AiDatabasePhysicalNameBuilder.cs`
- `src/backend/Atlas.Infrastructure/Services/DatabaseStructure/ConnectionStringMasker.cs`
- `src/backend/Atlas.AppHost/Controllers/DatabaseStructureController.cs`
- `src/backend/Atlas.AppHost/Bosch.http/DatabaseStructure.http`
- `tests/Atlas.SecurityPlatform.Tests/AiPlatform/DatabaseStructure/*Tests.cs`
- `src/frontend/apps/app-web/src/app/pages/database-structure/CreateTableDrawer.tsx`
- `src/frontend/apps/app-web/src/app/pages/database-structure/TableSqlCreator.tsx`
- `src/frontend/apps/app-web/src/app/pages/database-structure/CreateViewDrawer.tsx`
- `src/frontend/apps/app-web/src/app/pages/database-structure/DatabaseObjectDetailDrawer.tsx`
- `src/frontend/apps/app-web/src/app/pages/database-structure/DangerDeleteModal.tsx`
- `src/frontend/apps/app-web/src/app/pages/database-structure/TableDataPreview.tsx`
- `docs/ai-database-structure-delivery-report.md`

## 6. 验证策略

### SQLite

- 使用临时 `AiDatabaseHosting:Sqlite:Root`。
- 创建 AI 数据库后断言 draft/online `.db` 文件存在。
- 用 `IAiDatabaseClientFactory` 连接 draft，执行可视化建表、SQL 建表、创建视图、list objects、columns、DDL、preview、drop。
- `.http` 覆盖完整链路。

### MySQL

- 依赖 `AiDatabaseHosting:MySql:AdminConnection` 环境变量或本地安全配置。
- 缺失 admin connection 时验证返回明确错误且不创建假连接。
- 有配置时验证创建两个 database、建表、list、DDL、drop。

### PostgreSQL

- 依赖 `AiDatabaseHosting:PostgreSql:AdminConnection`。
- 采用独立 schema 策略，验证创建 draft/online schema、schema 限定建表、list、DDL、drop。
- 缺失 admin connection 时验证明确错误。

## 7. 高风险点

- ID string 改造会触达前端 Studio adapter 与 module 类型，必须避免大范围破坏。
- 现有 `DatabaseStructureController` 只在 `PlatformHost`，必须补 `AppHost`，否则 app-web 无法调用。
- 可视化 DDL 拼接的 `defaultValue/dataType/options` 必须后端白名单与 tokenizer 双重校验。
- 未知 driver 必须在创建前失败，避免持久化 Pending 脏数据。
- 密码、连接串、admin connection 必须全程加密或 mask，不得进入响应、日志、文档示例的真实值。
- 旧 workflow/低代码节点仍依赖 JSON 行模型，本任务只灰显/提示/文档列风险，不重写执行语义。
- `TreatWarningsAsErrors=true`，nullable 和分析器警告会导致 build 失败。

## 8. 回滚策略

- 所有新增结构管理服务通过新增接口/DI 注册接入，保留旧 `AiDatabasePhysicalTableService` 与旧 Controller。
- 若结构管理功能需回滚，可移除 AppHost `DatabaseStructureController` 路由与前端结构管理入口，已创建的独立库文件/database/schema 不自动删除。
- `AiDatabase` 新增字段只做向前兼容，不删除旧字段；CodeFirst 自愈可保留新增列。
- 前端入口单点在资源库 action menu，可快速灰显并提示“结构管理暂不可用”。
