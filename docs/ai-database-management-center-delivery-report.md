# AI 数据库管理中心交付报告

## 1. 需求范围

本次实现围绕资源库数据库 Tab 内嵌的 AI 数据库管理中心，覆盖托管配置、Draft/Online 物理实例、数据源列表、Schema/对象浏览、结构接口、SQL 安全校验、SQL 执行预览、DDL/数据预览、前端页面与 API client。

## 2. 不做范围

未改造 TenantDataSource 表结构；未替换现有数据库迁移控制台；未删除旧 AI 数据库 JSON 行模型路径；未引入新的前端组件库。外部 MySQL/PostgreSQL 真机联调需要目标实例与高权账号，当前环境未提供。

## 3. 架构说明

- 新增托管配置实体保存加密 AdminConnection，替代新路径中 appsettings 写死管理连接。
- 新增物理实例实体保存 AI 数据库 Draft/Online 独立实例连接。
- 新 Database Center API 以 `sourceId = ai:{physicalInstanceId}` 作为前端源 ID。
- 结构服务优先读取 `AiDatabasePhysicalInstance.EncryptedConnection`，避免托管实例回落旧 `AiDatabase.EncryptedDraftConnection`。
- 前端入口嵌入 `/space/:space_id/library?tab=database`。

## 4. 后端实体清单

- `AiDatabaseHostProfile`
- `AiDatabasePhysicalInstance`
- `AiDatabase` 新增 `DefaultHostProfileId`、`DraftInstanceId`、`OnlineInstanceId`

## 5. 后端服务清单

- `IAiDatabaseSecretProtector` / `AiDatabaseSecretProtector`
- `IAiDatabaseHostProfileService` / `AiDatabaseHostProfileService`
- `IAiDatabaseProvisioningService` / `AiDatabaseProvisioningService`
- `IAiDatabasePhysicalInstanceService` / `AiDatabasePhysicalInstanceService`
- `IDatabaseManagementService` / `DatabaseManagementService`
- 复用并增强 `IDatabaseStructureService`
- 增强 `ISqlSafetyValidator`

## 6. 后端接口清单

- `/api/v1/ai-database-host-profiles`
- `/api/v1/ai-database-host-profiles/drivers`
- `/api/v1/database-center/sources`
- `/api/v1/database-center/sources/{sourceId}/schemas`
- `/api/v1/database-center/sources/{sourceId}/instance-summary`
- `/api/v1/database-center/sql/execute`
- `/api/v1/database-center/sources/{sourceId}/schemas/{schemaName}/structure/**`
- `/api/v1/ai-databases` 创建响应新增 `draftSourceId` / `onlineSourceId`

## 7. 前端页面清单

- `src/frontend/apps/app-web/src/app/pages/database-center/database-center-page.tsx`
- 资源库数据库 Tab 集成：`workspace-library-page.tsx`

## 8. 前端组件清单

- `DatabaseCenterShell`
- `DatabaseSourcePanel`
- `DatabaseSchemaTree`
- `DatabaseStructureWorkbench`
- `ErDiagramCanvas`
- `TableObjectList`
- `SqlEditorPanel`
- `InstanceDetailPanel`
- `HostProfileManageDrawer`
- `AiDatabaseCreateWizard`
- `database-center-dock`
- `sql-format-utils`
- `use-database-center`

## 9. API client 清单

- `api-ai-database-host-profiles.ts`
- `api-database-center.ts`
- `api-database-structure.ts` 扩展新 structure 能力

## 10. 权限策略

- 查看：`DataSourcesView`
- 查询/预览/DDL：`DataSourcesQuery`
- 结构写操作：`DataSourcesSchemaWrite`
- 托管配置新增/编辑/删除/临时测试：`AiDatabaseHostProfileManage`

## 11. SQL 安全策略

后端 `SqlSafetyValidator` 支持单引号、双引号、反引号、方括号、行注释、块注释、MySQL 特殊注释、字符串分号和多语句拆分。危险关键字包括 DROP、DELETE、UPDATE、INSERT、ALTER、TRUNCATE、EXEC、MERGE、GRANT、REVOKE、ATTACH、DETACH、LOAD_FILE、INTO OUTFILE、COPY TO PROGRAM、xp_cmdshell 等。

## 12. 数据源托管配置设计

托管配置存储 driver、host、port、默认 charset/collation/schema、SQLite 根目录、加密密码、加密 AdminConnection。DTO 不返回加密字段，仅返回 masked summary。SQLite root 限制为平台目录下相对路径。

## 13. Draft/Online 实例设计

每个 AI 数据库创建 Draft / Online 两条 `AiDatabasePhysicalInstance`。结构写接口强制 Draft；Online 实例只读展示。前端环境选择器根据物理 source 固定展示，避免“切换 UI 但后端仍读 Draft”的假切换。

## 14. 方言支持矩阵

- SQLite：真实可用，结构查询、DDL、预览、建表路径通过测试。
- MySQL：实现托管建库、对象查询、DDL、分页 SQL 路径，未接真实实例验证。
- PostgreSQL：实现 schema/database 托管路径和对象查询，未接真实实例验证。
- SQL Server / Oracle / DM / Kingbase / Oscar：实现基础方言 SQL 和对象查询，不标记为已验证。

## 15. SQLite 验证结果

`DatabaseStructureServiceSqliteTests` 通过，覆盖 Draft/Online 文件创建、建表、字段查询、DDL、数据预览。

## 16. MySQL 验证结果

代码编译通过，MySQL 方言测试覆盖 SQL 生成与未知 driver 不回退；当前环境未提供真实 MySQL 管理实例，未做 live provision。

## 17. PostgreSQL 验证结果

代码编译通过，PostgreSQL 方言路径已实现；当前环境未提供真实 PostgreSQL 管理实例，未做 live provision。

## 18. SQL Server/Oracle/DM/Kingbase/Oscar 限制

这些方言具备基础对象查询和 SQL 生成能力，但未进行真实连接、DDL、分页查询端到端验证；生产启用前需补驱动级集成测试。

## 19. 旧 atlas_data_json 影响说明

旧 `AiDatabasePhysicalTableService` 和 `atlas_data_json` 路径保留兼容，新的 Database Center 结构管理不再以 JSON 行模型作为来源。

## 20. 编译结果

- `dotnet build Atlas.SecurityPlatform.slnx`：通过，0 warning / 0 error。
- `pnpm run build:app-web`：通过。

## 21. 测试结果

- `SqlSafetyValidatorTests`、`DatabaseDialectTests`、`DatabaseStructureServiceSqliteTests`：100 个定向测试通过。
- `pnpm run lint`：通过。
- `pnpm run i18n:check`：通过。

## 22. 浏览器验证结果

使用本地 `http://localhost:5181` 登录后进入资源库，点击数据库 Tab，页面可渲染 AI 数据库管理中心、数据源面板、Schema 面板、工作区、实例详情区。当前本地旧 SQLite 元数据库缺少历史列，已兼容为空列表展示，不再出现 500 toast。

## 23. 已知风险

- 本地旧元数据库需要正式迁移补齐 `AiDatabase` 历史新增列，否则旧资源查询会降级为空列表。
- MySQL/PostgreSQL live provision 未验证。
- ER 关系线基于当前对象/字段信息展示，外键关系需真实元数据后增强。
- 前端新增页面使用 labels 字典，严格 i18n 审计通过，但后续可继续把所有 label 绑定到 `useAppI18n` key。

## 24. 后续建议

- 增加 HostProfileService 与 ProvisioningService 的真实 SQLite 集成测试。
- 提供 MySQL/PostgreSQL CI 容器，补 live provision / DDL / preview 测试。
- 为旧元数据库补正式迁移脚本，避免开发库列漂移。
- 继续拆分专用权限，减少复用 `system:datasource:*` 带来的语义混淆。
